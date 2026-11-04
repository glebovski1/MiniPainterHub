# Reflection: Linux migration execution preparation

Date: 2026-11-02

- The preplan remains the single owner of migration status; no new vault category or competing runbook was introduced.
- Cached `Disabled` subscription metadata must not be confused with a live ARM result. Token refresh failed with expired MFA; public HTTP checks succeeded.
- Local Windows probe checks demonstrate artifact behavior only. Linux hosting, B1 allocation and safe stopped-state deployment remain unverified.
- Solution compilation succeeded with existing vulnerability warnings. Preserve the security gate rather than calling this a full validation pass.
- Resume with authenticated Phase 0 inventory and a concrete capacity operation. Do not use a login blocker as a reason to bypass the capacity-first sequence.

Later continuation after MFA: live ARM confirms Enabled, superseding cached Disabled metadata. The candidate still has production access, so approval must cover real isolation before any paid allocation. Direct-role enumeration alone cannot prove absence of all effective identity access; disabling only the candidate identity for the probe addresses that uncertainty, with a new rehearsal identity required later. Future cost forecasting is unavailable (HTTP 424); report the simple baseline projection separately. Client-dependent public HTTP responses are not proof of an outage. The stopped-state deployment gate remains unexecuted and must abort before B1 if access requires restarting the old payload.

Validation: probe publish and automated local startup/restart/package checks passed. Solution Release build passed with zero errors and 21 existing NuGet vulnerability warnings. Documentation checker passed across 62 documents with zero issues; `git diff --check` passed. Hash comparison confirmed all 13 unrelated pre-existing modified/untracked files were unchanged. Application test suites and Azure operations were not run.

Phase 1 execution continuation: the user subsequently approved the bounded Azure operation. The original Linux-name substring check was invalid for Ubuntu's OS description; the corrected probe uses .NET's explicit platform predicate. Preserve both attempts in evidence. The original deadline was retained, both allocation intervals ended on F1, and the final read-only key/Windows/guard verification passed. A restart API acknowledgement is not a completed process restart: this run took about 103 seconds to expose a new process marker. Full application rehearsal remains a later gate.


Phase 2 continuation: updated the migration execution record, Architecture and Deployment for explicit relational fixtures/evidence, reset-target isolation, separate hosted smoke, runtime-only retained-resource infrastructure and paired artifact provenance. Recorded read-only production migration/compatibility evidence and the licensing/security blocker without weakening CI. npm dependencies were repaired and audited clean. Final Windows results: 495 server tests, 345 client tests and 38 browser cases passed; three hosted anonymous reads passed on Windows production. Ubuntu SQL/application evidence remains pending. The first browser timing failure and the unrelated pre-existing stylesheet-cache audit mismatch remain documented; production and all 13 unrelated dirty files were preserved.
