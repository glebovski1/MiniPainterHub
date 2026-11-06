"""Check artifacts/linux-capacity-probe/publish locally, then package it for review.

Run from any directory after publishing tools/cloud/LinuxCapacityProbe in Release.
This script never connects to Azure or production.
"""
import datetime
import hashlib
import json
import os
import pathlib
import platform
import socket
import subprocess
import time
import urllib.error
import urllib.request
import zipfile

repo = pathlib.Path(__file__).resolve().parents[2]
published = repo / 'artifacts/linux-capacity-probe/publish'
artifact_dir = published.parent
files = sorted(published.rglob('*'))
expected = {'LinuxCapacityProbe.dll', 'LinuxCapacityProbe.deps.json',
    'LinuxCapacityProbe.runtimeconfig.json', 'LinuxCapacityProbe.staticwebassets.endpoints.json'}
actual = {p.relative_to(published).as_posix() for p in files if p.is_file()}
assert actual == expected, f'Unexpected payload files: {actual ^ expected}'
deps = json.loads((published / 'LinuxCapacityProbe.deps.json').read_text())
assert all(lib['type'] == 'project' for lib in deps['libraries'].values())
assert len(deps['libraries']) == 1, 'Probe must not reference the application or third-party packages'
assets = json.loads((published / 'LinuxCapacityProbe.staticwebassets.endpoints.json').read_text())
assert assets['Endpoints'] == [], 'Probe must not contain application assets'

opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
results = []
for attempt in range(2):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    origin = f'http://127.0.0.1:{port}'
    env = os.environ.copy()
    env['ASPNETCORE_ENVIRONMENT'] = 'Production'
    with (artifact_dir / f'local-start-{attempt + 1}.log').open('w') as logfile:
        process = subprocess.Popen(['dotnet', str(published / 'LinuxCapacityProbe.dll'), '--urls', origin],
            cwd=published, env=env, stdout=logfile, stderr=subprocess.STDOUT,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        try:
            deadline = time.monotonic() + 30
            while True:
                assert process.poll() is None, 'Probe exited before readiness'
                try:
                    with opener.open(origin + '/healthz', timeout=2) as response:
                        health = json.load(response)
                    break
                except (urllib.error.URLError, TimeoutError):
                    if time.monotonic() >= deadline:
                        raise AssertionError('Probe did not become ready')
                    time.sleep(0.2)
            assert health['status'] == 'ok'
            assert health['probe'] == 'MiniPainterHub.LinuxCapacityProbe'
            assert health['runtime'].startswith('.NET 10.')
            assert health['isLinux'] is (platform.system() == 'Linux')
            with opener.open(origin, timeout=2) as response:
                assert response.read().decode() == 'MiniPainterHub Linux capacity probe'
            for route in ['/api/posts', '/api/test/reset', '/signin-google']:
                try:
                    opener.open(origin + route, timeout=2)
                    raise AssertionError('Unexpected application endpoint: ' + route)
                except urllib.error.HTTPError as error:
                    assert error.code == 404
            results.append(health)
        finally:
            process.terminate()
            process.wait(timeout=10)
assert results[0]['processInstance'] != results[1]['processInstance'], 'Restart marker did not change'
package = artifact_dir / 'linux-capacity-probe.zip'
with zipfile.ZipFile(package, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in files:
        if path.is_file():
            archive.write(path, path.relative_to(published).as_posix())
report = {'observedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'localProductionStarts': results, 'packageFiles': sorted(actual),
    'payloadSha256': {p.relative_to(published).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in files if p.is_file()},
    'sourceSha256': {str(p.relative_to(repo)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in [repo / 'tools/cloud/LinuxCapacityProbe/LinuxCapacityProbe.csproj',
                  repo / 'tools/cloud/LinuxCapacityProbe/Program.cs']},
    'packageSha256': hashlib.sha256(package.read_bytes()).hexdigest(),
    'azureDeployment': 'not run', 'linuxRuntime': 'not run', 'b1Allocation': 'not run',
    'f1Cleanup': 'not run'}
(artifact_dir / 'verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
