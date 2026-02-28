# ProblemDetails Assertions

## Baseline assertions
1. Assert HTTP status code.
2. Assert `Content-Type` contains `application/problem+json`.
3. Deserialize payload to `ProblemDetails` (or equivalent JSON object).
4. Assert `title`, `detail`, and `status` values.

## Validation and domain error payloads
- When expecting validation or domain failures, assert extension fields such as:
  - `errors`
  - `requestId`

## Example checklist
- Unauthorized path returns 401 with stable detail text.
- Not found path returns 404 and expected title/detail.
- Validation path returns 400 and expected keyed `errors` entries.

## Stability tips
- Assert stable contract fields first.
- Avoid brittle assertions on full raw JSON text.
