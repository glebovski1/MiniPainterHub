"""Verify a sealed CI release before deployment. Never extract or execute it."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile


def verify(folder, commit, run_id, runtime='linux-x64', expected_sha=None):
    manifest = json.loads((folder / f'minipainterhub-{runtime}.manifest.json').read_text(encoding='utf-8'))
    if not (manifest.get('commit') == commit and str(manifest.get('ciRunId')) == str(run_id)
            and manifest.get('runtime') == runtime and manifest.get('releaseEligible') is True
            and manifest.get('workingTreeDirty') is False):
        raise ValueError('Release provenance does not match the selected successful CI run')
    archive = folder / f'minipainterhub-{runtime}.zip'
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    if manifest.get('archive') != archive.name or manifest.get('sha256') != digest or expected_sha and digest != expected_sha:
        raise ValueError('Release archive checksum mismatch')
    with zipfile.ZipFile(archive) as zipped:
        entries = zipped.infolist()
        names = [entry.filename for entry in entries]
        if len(set(x.casefold() for x in names)) != len(names) or set(names) != set(manifest['files']):
            raise ValueError('Release inventory mismatch or duplicate paths')
        for entry in entries:
            path = PurePosixPath(entry.filename)
            if entry.is_dir() or path.is_absolute() or '..' in path.parts or '\\' in entry.filename or ':' in entry.filename:
                raise ValueError('Unsafe archive path')
            if hashlib.sha256(zipped.read(entry)).hexdigest() != manifest['files'][entry.filename]:
                raise ValueError('Release file checksum mismatch')
    return digest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--sha256')
    args = parser.parse_args()
    print(verify(args.folder, args.commit, args.run_id, expected_sha=args.sha256))
