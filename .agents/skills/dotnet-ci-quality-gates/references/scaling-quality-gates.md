# Scaling Quality Gates

## As project grows
- Split workflows by concern:
  - backend-ci.yml
  - webapp-ci.yml
  - security-scan.yml
- Keep required checks small and high-signal.
- Move heavy checks to reusable workflows or nightly schedules.

## Performance guidance
- Use path filters to skip irrelevant jobs.
- Use dependency caching.
- Keep artifact retention short for large binaries.

## Governance guidance
- Require CI checks in branch protection.
- Use code owners for critical paths.
- Track flaky tests and quarantine quickly.

## Evolution model
1. Mandatory: restore, build, tests.
2. Add coverage thresholds.
3. Add formatting and analyzer enforcement.
4. Add integration and visual regression gates where ROI is clear.
