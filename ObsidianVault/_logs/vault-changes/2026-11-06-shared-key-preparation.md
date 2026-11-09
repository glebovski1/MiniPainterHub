# Shared key provider preparation

Date: 2026-11-06

- Recorded successful Ubuntu CI and independently verified package manifests for PR #85.
- Documented opt-in Blob/Key Vault Data Protection with managed identity, preserved application name, unchanged default filesystem behavior and fail-closed configuration validation.
- Added 19 focused checks for configuration, provider wiring, legacy token compatibility, restart and key rotation. Live Azure wrapping and cross-host continuity remain unverified.
- The user prioritized the quickest, cheapest route. Google test-client setup is deferred; no paid resources, credentials, key imports or production changes were applied.

Current migration status belongs to [Linux Migration Preplan](<../../20 Engineering/Linux Migration Preplan.md>).
