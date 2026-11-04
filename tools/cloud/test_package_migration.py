"""Regression checks for migration payload isolation and provenance."""
import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

from package_migration import package


class MigrationPackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.publish = self.root / "publish"
        self.output = self.root / "sealed"
        payload = {
            "MiniPainterHub.Server.dll": "test assembly",
            "Imaging/policy.xml": "<policymap/>",
            "Licenses/Magick.NET-LICENSE.txt": "test license",
            "Licenses/Magick.NET-NOTICE.txt": "test notice",
            "Magick.NET-Q8-AnyCPU.dll": "test assembly",
            "Magick.NET.Core.dll": "test assembly",
            "Magick.Native-Q8-x64.dll.so": "test Linux native library",
            "Magick.Native-Q8-x64.dll": "test Windows native library",
            "MiniPainterHub.Server.runtimeconfig.json": json.dumps({"runtimeOptions": {"tfm": "net10.0"}}),
            "MiniPainterHub.Server.deps.json": json.dumps({"runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/linux-x64"}}),
            "wwwroot/index.html": "<html></html>",
            "wwwroot/_framework/blazor.webassembly.js": "// test runtime",
        }
        for name, data in payload.items():
            path = self.publish / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(data)

    def seal(self, dirty=False, runtime="linux-x64"):
        with patch("package_migration.subprocess.check_output", side_effect=["a" * 40, " M file" if dirty else ""]), \
                patch.dict("package_migration.os.environ", {}, clear=True):
            package(self.publish, self.output, runtime)

    def test_sealed_bytes_match_manifest_and_local_build_is_not_release_eligible(self):
        self.seal()
        manifest = json.loads((self.output / "minipainterhub-linux-x64.manifest.json").read_text())
        archive = self.output / manifest["archive"]
        self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(), manifest["sha256"])
        self.assertFalse(manifest["releaseEligible"])
        with zipfile.ZipFile(archive) as bundle:
            for name, digest in manifest["files"].items():
                self.assertEqual(hashlib.sha256(bundle.read(name)).hexdigest(), digest)
        with self.assertRaisesRegex(ValueError, "overwrite"):
            self.seal()

    def test_private_settings_fail_before_archive_creation(self):
        (self.publish / "appsettings.Local.Production.json").write_text('{"private":"fixture"}')
        with self.assertRaisesRegex(ValueError, "Private/local"):
            self.seal()
        self.assertFalse(self.output.exists())

    def test_publish_profile_is_rejected(self):
        (self.publish / "candidate.publishsettings").write_text("fixture")
        with self.assertRaisesRegex(ValueError, "Private/local"):
            self.seal()

    def test_wrong_platform_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "runtime does not match"):
            self.seal(runtime="win-x64")

    def test_dirty_checkout_cannot_silently_become_release_artifact(self):
        with self.assertRaisesRegex(ValueError, "clean checkout"):
            self.seal(dirty=True)

    def test_missing_image_policy_or_native_library_is_rejected(self):
        for name in ("Imaging/policy.xml", "Magick.Native-Q8-x64.dll.so", "Licenses/Magick.NET-NOTICE.txt"):
            with self.subTest(name=name):
                path = self.publish / name
                content = path.read_bytes()
                path.unlink()
                with self.assertRaisesRegex(ValueError, "Missing required"):
                    self.seal()
                path.write_bytes(content)


if __name__ == "__main__":
    unittest.main()
