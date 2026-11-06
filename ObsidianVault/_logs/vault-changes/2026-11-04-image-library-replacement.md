# Image library replacement

Date: 2026-11-04

- Updated Architecture with Magick.NET image processing, raster/frame limits, native policy, metadata handling and avatar output.
- Updated Deployment with platform native-library, policy, license-notice and temporary-directory requirements.
- Superseded the migration plan's ImageSharp licensing blocker with the user's authorized replacement. The Community application was never submitted.
- Recorded 514 passing server tests, 69 focused cases, 38 browser cases, six packaging tests, a zero-warning Release build and a clean NuGet vulnerability audit.
- Kept Linux native/real-SQL execution, isolated rehearsal and production cutover as unpassed gates. No Azure changes or extra spending occurred during replacement.

Current status and publish evidence belong to [Linux Migration Preplan](<../../20 Engineering/Linux Migration Preplan.md>).

Continuation: isolated and pushed the migration-only draft PR, verified clean Windows coverage/tests and initial Linux native/SQL execution, fixed the Ubuntu runner incompatibility without weakening tests, and made PR browser smoke use real SQL. Recorded the read-only runtime preview, separate media resource group and placeholder-only rehearsal packet. Live deployment and spending remain unapproved. See the migration preplan and PR for run-specific evidence.
