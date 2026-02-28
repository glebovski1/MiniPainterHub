# Render-State Assertions

## Assertion priorities
1. User-visible content (labels, titles, status text).
2. Critical interaction affordances (buttons, inputs, disabled state).
3. Conditional regions (loading, empty, error, success).

## Async state transitions
- Use `WaitForAssertion` when UI updates after awaited calls.
- Assert both pre-action and post-action states.

## Avoid brittle tests
- Prefer targeted selectors and semantic checks over whole-markup equality.
- Keep assertion scope aligned with component responsibility.

## Typical cases
- Loading spinner disappears after data load.
- Error message appears when service throws.
- Button disable and enable behavior follows form validity.
