# Playwright CI Integration

## Baseline CI Job
1. Checkout code.
2. Setup .NET SDK.
3. Restore and build solution.
4. Start backend host required for browser tests.
5. Install Playwright browsers.
6. Run smoke suite.
7. Upload failure artifacts.

## Reliability Controls
- Use a health-check wait before launching tests.
- Set clear timeouts for app startup and each test.
- Run smoke tests in a single worker if shared test data cannot be isolated yet.

## Artifact Policy
- Always publish:
  - Playwright traces on failure.
  - Screenshots on failure.
  - Test report output.
- Keep retention short for heavy artifacts.

## Scaling Strategy
- Keep smoke suite required on PR.
- Move broader UI coverage to a non-blocking or nightly workflow.
- Split workflows as test surface grows:
  - backend-ci
  - ui-smoke
  - extended-e2e
