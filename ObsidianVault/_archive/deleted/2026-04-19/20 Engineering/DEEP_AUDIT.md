> Archived by Codex on 2026-04-19.
> Reason: historical audit with durable follow-up themes merged into the engineering index; superseded by [[20 Engineering/README]].

# MiniPainterHub Deep Project Audit

_Date:_ 2026-02-19  
_Scope:_ `MiniPainterHub.Server`, `MiniPainterHub.WebApp`, `MiniPainterHub.Common`, repo documentation/config

## Current Status Update

_Updated:_ 2026-04-18

This audit remains useful as historical context, but several findings have since moved from open gaps to hardened baseline:

- Current local baseline is 398 .NET tests and 19 Playwright smoke scenarios.
- The solution build has been cleaned to 0 warnings.
- Base configuration no longer carries JWT signing material; development-only JWT and seeded account credentials live in `appsettings.Development.json`, while non-development startup validation still requires external secret configuration.
- `appsettings.Production.json` is strict JSON and intentionally omits secrets.
- CI now includes build/test/browser smoke coverage, format verification, JSON config validation, NuGet vulnerability gating, npm audit, and warnings-as-errors.
- Package versions are centrally managed with .NET 8 patch-line alignment.

Remaining follow-up themes are token storage posture, deeper production secret-store/rotation process, runtime observability, and any future move from environment/app-service secrets to a managed secret store.

## Executive Summary

MiniPainterHub has a solid baseline architecture (service layer usage, DTO separation, global exception handling, and meaningful unit-test coverage around core services), but there are several high-impact improvements needed in **security hygiene**, **operational hardening**, and **quality gates**.

Current top priorities:

1. **Maintain zero-warning and no-vulnerability quality gates.**
2. **Keep production secrets external and document rotation/secret-store ownership.**
3. **Tighten authentication token handling and client-side resilience.**
4. **Add structured runtime observability for auth, startup, and image/media operations.**
5. **Keep CI and README status aligned with the current test/browser coverage.**

---

## Methodology

This audit used:

- Architecture/pattern docs ([ARCHITECTURE.md](<../../../../20 Engineering/ARCHITECTURE.md>), [BEST_PRACTICES.md](<../../../../30 Process/BEST_PRACTICES.md>), [ANTI_PATTERNS.md](<../../../../30 Process/ANTI_PATTERNS.md>)).
- Static review of server/webapp/common projects.
- Config and startup-path review.
- Lightweight static grep for anti-patterns and risk indicators.
- **Live validation** by installing .NET 8 SDK in this environment and running restore/build/test commands.

---

## Verification Environment Setup (Completed)

The following package/tooling installation was executed to enable full validation in this environment:

- `sudo apt-get update -y`
- `sudo apt-get install -y dotnet-sdk-8.0`

Installed SDK/runtime baseline:

- `.NET SDK`: `8.0.124`
- `aspnetcore-runtime-8.0`: installed via apt dependency chain

---

## Validation Results (Executed)

### ✅ Restore
- `dotnet restore MiniPainterHub.sln` succeeded.

### ✅ Build
- `dotnet build MiniPainterHub.sln -c Release` succeeded.
- Result: **0 errors, 36 warnings**.
- Most warnings are nullability mismatches and obsolete API usage (`ISystemClock`, exception serialization constructors), plus a Blazor component resolution warning for `RedirectToLogin`.

### ✅ Tests
- `dotnet test MiniPainterHub.sln -c Release --no-build` succeeded.
- Result: **37 passed, 0 failed, 0 skipped**.

### ✅ Runtime smoke (`dotnet run`)
- API startup + `/healthz` smoke now passes in Linux container when `ASPNETCORE_ENVIRONMENT=Development` by using an in-memory fallback for LocalDB development strings.
- This keeps local Linux/container development functional while preserving SQL Server behavior for non-LocalDB configurations.

---

## Strengths

- Clear layered split: controllers → services → EF Core context.
- Centralized exception handling with `ProblemDetails`.
- Good foundational domain services for posts/comments/likes/profile.
- Existing test project with unit coverage across key service areas.
- Image pipeline abstraction (`IImageProcessor`, `IImageStore`) provides extensibility.

---

## Findings & Recommendations

## Critical

### 1) Secret material and default credentials are committed

**Risk:** High security exposure (token forgery risk, credential stuffing against non-prod, accidental prod reuse).

**Observed patterns:**
- JWT signing key present in `appsettings.json`.
- Development seeder includes hard-coded default user/admin credentials.

**Recommendations:**
- Rotate JWT key immediately and invalidate all existing tokens.
- Move JWT secrets to environment/secret store only (`User Secrets`, Azure Key Vault, GitHub Actions secrets).
- Replace hard-coded seed passwords with one-time generated values via env vars, and gate seeding by explicit development flag.
- Add secret scanning (e.g., gitleaks/trufflehog) to CI.

---

## High

### 2) Production config reliability issue

**Risk:** Deployment/runtime startup failures due to invalid JSON shape.

**Observed pattern:**
- `appsettings.Production.json` contains comment syntax (`// ...`), which is invalid JSON and can break parsers/tooling assumptions.

**Recommendations:**
- Remove comments from JSON files and encode guidance in README/docs.
- Add `jq`/JSON linting step in CI for all config files.

### 3) Startup path complexity and duplicate production branch

**Risk:** Maintainability and subtle initialization defects.

**Observed pattern:**
- Nested `if (app.Environment.IsProduction())` block duplicates the same condition.

**Recommendations:**
- Flatten startup logic into a single environment branch.
- Extract startup migration/seeding into dedicated extension methods.
- Add startup smoke test (integration test invoking host boot path).

### 4) Platform portability issue in default DB connection

**Risk:** App cannot run out-of-box in Linux CI/container environments.

**Observed pattern:**
- Default connection string targets LocalDB, which throws `PlatformNotSupportedException` on Linux during startup.

**Recommendations:**
- Provide Linux-friendly development default (`Sqlite` or containerized SQL Server profile).
- Skip `DataSeeder.SeedAsync` automatically when DB is unreachable, with clear startup logging.
- Document environment-specific DB prerequisites in README.

### 5) Client auth token storage posture

**Risk:** XSS token exfiltration due to `localStorage` token persistence.

**Observed pattern:**
- JWT stored/read from `localStorage` and attached by message handler.

**Recommendations:**
- Prefer secure HttpOnly cookie auth for SPA/API where feasible.
- If JWT-in-browser is retained, implement strict CSP, shorter token TTLs, silent refresh strategy, and refresh-token protection controls.
- Add explicit logout/token revocation pattern server-side if business requirements include immediate session invalidation.

---

## Medium

### 6) Compile-time warning debt (46 warnings)

**Risk:** Hidden defects and reduced signal-to-noise for newly introduced issues.

**Recommendations:**
- Prioritize nullability warning cleanup in DTOs/services.
- Replace obsolete APIs (`ISystemClock`) and obsolete exception-serialization constructors where feasible.
- Add `TreatWarningsAsErrors` in CI incrementally (or warning baselines per project).

### 7) Repository contains uploaded image artifacts under server `wwwroot/uploads`

**Risk:** Repository bloat, accidental sensitive content retention, slower clones/CI, poor environment parity.

**Recommendations:**
- Remove runtime upload artifacts from git history where possible.
- Add/strengthen `.gitignore` for `wwwroot/uploads/**` and similar generated assets.
- Keep only minimal fixtures in test-data directories.

### 8) Dependency version skew across projects

**Risk:** Inconsistent runtime behavior and patching drift.

**Observed pattern:**
- Mixed package versions in WebApp (`8.0.2`, `8.0.15`, `8.0.1`) while server mostly aligns to `8.0.15`.

**Recommendations:**
- Centralize package management (`Directory.Packages.props`).
- Align ASP.NET/Extensions package versions to a single patch train.
- Add dependency vulnerability scanning and update cadence.

### 9) Minor robustness gaps in UI error handling

**Risk:** Silent failures and reduced diagnosability.

**Observed pattern:**
- Empty `catch { }` found in UI path.

**Recommendations:**
- Replace with typed exception handling + user notification + structured logging path.
- Add linting rule or analyzer for empty catch blocks.

### 10) Service/controller cleanliness opportunities

**Risk:** Increased code noise and long-term maintenance drag.

**Observed pattern:**
- Some injected dependencies/variables appear unused in controllers.

**Recommendations:**
- Remove unused dependencies and dead code paths.
- Enable analyzers treating warnings as errors in CI for unused members.

---

## Test, CI/CD, and Operations Maturity Gaps

### Recommended baseline pipeline

1. `dotnet restore`
2. `dotnet build --configuration Release`
3. `dotnet test --configuration Release --collect:"XPlat Code Coverage"`
4. `dotnet format --verify-no-changes`
5. SAST/secret scan (`gitleaks` + `dotnet list package --vulnerable`)
6. Config lint (JSON validation)
7. Linux runtime smoke test with portable DB profile

Add branch protection requiring all checks before merge.

---

## Prioritized 30/60/90 Day Improvement Plan

## Days 0–30 (Security + Stability)

- Remove committed JWT key and rotate secrets.
- Remove hard-coded seeded credentials and harden seeding toggles.
- Fix `appsettings.Production.json` to strict JSON.
- Flatten production startup condition and keep one migration/seed path.
- Introduce basic CI (build/test/format).
- Add Linux-friendly dev DB profile and update README.

## Days 31–60 (Reliability + Quality)

- Add secret/vulnerability scanning.
- Normalize package versions via central package management.
- Add analyzers and fail on warnings in CI.
- Clean tracked upload artifacts; enforce `.gitignore` patterns.

## Days 61–90 (Architecture + Observability)

- Add integration tests for auth flows and startup health.
- Add structured telemetry around image pipeline throughput/errors.
- Review token strategy (move toward HttpOnly cookie or hardened refresh-token model).

---

## Suggested KPI Targets

- **Build pass rate:** > 95% on mainline.
- **Critical vulnerability SLA:** fix within 48 hours.
- **Config drift incidents:** 0 invalid config releases.
- **Warnings in Release build:** reduce from 36 to < 10.
- **Test coverage (service + controller):** +15% from current baseline.
- **Time-to-detect auth/session issues:** under 15 minutes with telemetry + alerts.

---

## Closing Assessment

The project is in good shape architecturally, and now has verified build/test execution in this environment. The next step is to close security/configuration gaps and eliminate warning debt so runtime quality matches architectural intent.
