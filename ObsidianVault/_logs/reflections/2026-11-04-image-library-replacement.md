# Reflection: image library replacement

Date: 2026-11-04

- Removing ImageSharp resolves the local license/security blocker without downgrading to vulnerable packages or suppressing warnings.
- A native image library broadens decoding capability, so the implementation restricts signatures and coders, denies delegates, and proves the native SVG policy rather than relying only on MIME checks.
- Focused output tests cover actual encoded bytes, orientation, transparency, animation and metadata. Full server and browser checks cover integration.
- Windows tests and cross-publish do not establish Linux execution. Docker startup was rejected by automatic approval review, and no bypass was attempted. Keep the Ubuntu native/SQL gate explicit.
- The preplan remains the single migration status owner. Preserve the old snapshot as history, clearly mark its licensing blocker/artifacts superseded, and retain the 13 unrelated user changes.
