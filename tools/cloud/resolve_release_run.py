"""Select a successful trusted CI push and the previously recorded artifact."""
import json
import os
import re
import subprocess


def resolve(run, repository, rollback):
    if (run.get('name') != 'CI' or run.get('path') != '.github/workflows/ci.yml'
            or run.get('conclusion') != 'success' or run.get('status') != 'completed'
            or run.get('event') != 'push' or run.get('head_branch') != 'master'
            or run.get('repository', {}).get('full_name') != repository):
        raise ValueError('Only successful master-push CI releases may deploy')
    fields = {'run_id': str(run['id']), 'commit': run['head_sha'], 'rollback_run': str(rollback['ciRunId']),
              'rollback_commit': rollback['commit'], 'rollback_sha': rollback['sha256']}
    for key, value in fields.items():
        pattern = r'[0-9]+' if key in ('run_id','rollback_run') else r'[0-9a-f]{64}' if key == 'rollback_sha' else r'[0-9a-f]{40}'
        if not re.fullmatch(pattern, value):
            raise ValueError('Invalid release metadata')
    return fields


if __name__ == '__main__':
    selected = os.environ['SELECTED_RUN']
    if not re.fullmatch(r'[0-9]+', selected):
        raise ValueError('A numeric successful CI run is required')
    repo = os.environ['GITHUB_REPOSITORY']
    run = json.loads(subprocess.check_output(['gh','api',f'repos/{repo}/actions/runs/{selected}'],text=True))
    subprocess.run(['git','fetch','--force','origin','refs/tags/production-last-good:refs/tags/production-last-good'],check=True)
    rollback = json.loads(subprocess.check_output(['git','for-each-ref','--format=%(contents)','refs/tags/production-last-good'],text=True))
    values = resolve(run, repo, rollback)
    current_master = subprocess.check_output(
        ['gh', 'api', f'repos/{repo}/git/ref/heads/master', '--jq', '.object.sha'], text=True).strip()
    if values['commit'] != current_master:
        raise ValueError('Selected release has been superseded by a newer master revision')
    with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as output:
        for key,value in values.items():
            output.write(f'{key}={value}\n')
