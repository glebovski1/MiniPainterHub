"""Seal a publish directory into a traceable, platform-specific migration ZIP."""
import argparse
import hashlib
import json
import os
import re
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def package(publish, output, runtime, allow_dirty=False):
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    ci_run_id = os.environ.get("GITHUB_RUN_ID") if (
        os.environ.get("GITHUB_ACTIONS") == "true"
        and os.environ.get("GITHUB_WORKFLOW") == "CI"
        and os.environ.get("GITHUB_SHA") == commit) else None
    dirty = bool(subprocess.check_output(
        ["git", "status", "--porcelain", "--untracked-files=normal"], cwd=ROOT, text=True).strip())
    if dirty and not allow_dirty:
        raise ValueError("Migration release artifacts require a clean checkout. --allow-dirty is for local validation only.")
    paths = sorted(publish.rglob("*"))
    files = {}
    seen = set()
    for path in paths:
        if path.is_symlink():
            raise ValueError("Publish payload must not contain symbolic links.")
        if not path.is_file():
            continue
        name = path.relative_to(publish).as_posix()
        folded = name.casefold()
        if folded in seen:
            raise ValueError(f"Case-colliding publish path: {name}")
        seen.add(folded)
        if re.fullmatch(r"appsettings\.local.*\.json", path.name, re.IGNORECASE) \
                or path.suffix.lower() in {".pfx", ".publishsettings", ".pubxml", ".lic"}:
            raise ValueError(f"Private/local configuration in publish payload: {name}")
        files[name] = sha256(path.read_bytes())
    for required in ("MiniPainterHub.Server.dll", "MiniPainterHub.Server.runtimeconfig.json",
                     "Imaging/policy.xml", "Licenses/Magick.NET-LICENSE.txt", "Licenses/Magick.NET-NOTICE.txt",
                     "Magick.NET-Q8-AnyCPU.dll", "Magick.NET.Core.dll",
                     "Magick.Native-Q8-x64.dll.so" if runtime == "linux-x64" else "Magick.Native-Q8-x64.dll",
                     "wwwroot/index.html", "wwwroot/_framework/blazor.webassembly.js"):
        if required not in files:
            raise ValueError(f"Missing required, case-sensitive publish path: {required}")
    config = json.loads((publish / "MiniPainterHub.Server.runtimeconfig.json").read_text())
    if config["runtimeOptions"]["tfm"] != "net10.0":
        raise ValueError("Migration packages require net10.0.")
    deps = json.loads((publish / "MiniPainterHub.Server.deps.json").read_text())
    if not deps["runtimeTarget"]["name"].endswith("/" + runtime):
        raise ValueError("Published runtime does not match the requested migration platform.")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"minipainterhub-{runtime}.zip"
    if archive.exists():
        raise ValueError("Refusing to overwrite a sealed migration ZIP; use a fresh output directory.")
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as bundle:
        for name in files:
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            bundle.writestr(info, (publish / name).read_bytes())
    manifest = {
        "format": 1, "commit": commit, "workingTreeDirty": dirty,
        "releaseEligible": not dirty and ci_run_id is not None,
        "ciRunId": ci_run_id, "runtime": runtime,
        "archive": archive.name, "sha256": sha256(archive.read_bytes()),
        "migrations": sorted(p.stem for p in (ROOT / "MiniPainterHub.Server/Migrations").glob("*.cs")
                             if re.match(r"^\d{14}_", p.stem) and not p.name.endswith(".Designer.cs")),
        "files": files,
    }
    (output / f"minipainterhub-{runtime}.manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({k: manifest[k] for k in ("commit", "runtime", "releaseEligible", "sha256")}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--runtime", choices=("linux-x64", "win-x64"), required=True)
    parser.add_argument("--allow-dirty", action="store_true")
    args = parser.parse_args()
    package(args.publish, args.output, args.runtime, args.allow_dirty)
