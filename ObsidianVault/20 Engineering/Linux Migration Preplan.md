# Linux Migration Preplan

Status: Phase 1 capacity, runtime/restart and F1 cleanup verified after the user's bounded approval. Phase 2 compatibility work is in draft PR #85 with Linux CI verification in progress. Rehearsal spending and production cutover remain separately gated.

Prepared: 2026-10-28. Revised: 2026-11-02 after three-reviewer migration rounds.

Purpose: own the plan for Linux hosting while Windows remains the development machine.

When to read: before compatibility changes, capacity tests, key migration, rehearsal, cutover, or retirement.

Update triggers: runtime, resource inventory, cost guard, deadline, key/token behavior, deployment workflow, DNS/TLS, or rehearsal evidence changes.

Related notes: [Deployment](<DEPLOYMENT.md>), [Architecture](<ARCHITECTURE.md>), [Workflow playbook](<../30 Process/WORKFLOW_PLAYBOOK.md>).

## Recommendation and boundaries

Use **one Azure App Service Linux B1 instance**, reusing `rollandpaint-prod-ca` and its existing Linux plan `ASP-mphprg-linux-stage` in Canada Central, subject to actual allocation and rehearsal. Keep `MiniPainterHub` on Windows plan `ASP-mphprg-85e2` as the recoverable host during the agreed observation period.

| Area | Working decision |
| --- | --- |
| Development | Existing Windows IDE, PowerShell, .NET 10, native debugging, and LocalDB remain the default. |
| Linux verification | Ubuntu CI with explicit real SQL, then isolated Azure SQL rehearsal. Local WSL2 and Docker are optional; CI containers do not require local Docker. |
| Runtime/package | Built-in .NET 10; publish the server, which includes the Blazor client. No containerization prerequisite. |
| Production data | Retain the existing Azure SQL database and media Blob Storage, with no production data move or schema change for the OS cutover. |
| Public origin | Retain `https://rollandpaint.com`, apex/www behavior, enabled OAuth providers, and email origin. |
| Delivery | Separate rehearsal environment/credentials; one production migration owner; immutable artifacts for both operating systems. |
| Recovery | Key-compatible Windows build, configuration and TLS retained for 72 hours after successful cutover. |
| Budget | Existing $50 subscription cost guard remains enabled; separately approve paid tests and any spending/deadline extension. |

Windows-to-Linux migration requires a separate host and attention to identity, network and domain changes. B1 has no deployment slots; this is a two-host traffic change, not a slot swap. Verify the actual offered .NET runtime again before execution. [Microsoft migration guidance](https://learn.microsoft.com/en-us/azure/app-service/app-service-migration-windows-linux), [slot requirements](https://learn.microsoft.com/en-us/azure/app-service/deploy-staging-slots), [runtime inspection](https://learn.microsoft.com/en-us/azure/app-service/configure-language-dotnetcore).

Essential scope is runtime/packaging, real SQL coverage, isolated rehearsal, token/key continuity, functionality checks and rollback. No public API, client-route or database-schema changes are intended; changes concern hosting configuration, deployment targets and test launch/connection interfaces. Scale-out, Azure SignalR/backplanes, general proxy redesign, media storage authentication redesign, OIDC migration, broad test rewrites and unrelated security hardening are separate work. Existing CI security gates still apply.

## Evidence, readiness and remaining unknowns

### Repository evidence rechecked for this revision

| Evidence | Consequence and affected work |
| --- | --- |
| [Server project](../../MiniPainterHub.Server/MiniPainterHub.Server.csproj), [SDK selection](../../global.json) and [CI](../../.github/workflows/ci.yml) use .NET 10. [Infrastructure](../../infra/main.bicep) still specifies `DOTNETCORE\|8.0`; [bootstrap](../../tools/cloud/bootstrap-dotnet-and-test.sh) installs 8.0. | Confirmed tooling drift. Align with the SDK/runtime requirement. The existing template also creates SQL/storage; do not deploy it unchanged against retained production services. |
| [Production settings](../../MiniPainterHub.Server/appsettings.Production.json) contain `D:\home\data-protection-keys`. [Service registration](../../MiniPainterHub.Server/Infrastructure/Startup/ServiceRegistration.cs) uses filesystem persistence and application name `MiniPainterHub`. | Use an explicit shared production key provider, keeping the application discriminator and purposes stable. A Linux path alone does not preserve existing protected values. |
| Service registration uses InMemory when Linux local tooling receives a LocalDB connection. [Project migrations](../../MiniPainterHub.Server.Tests/Data/HobbyProjectMigrationTests.cs), [email migrations](../../MiniPainterHub.Server.Tests/Data/EmailConfirmationMigrationTests.cs) and a [relational project test](../../MiniPainterHub.Server.Tests/Services/HobbyProjectServiceTests.cs) return early outside Windows. | A green Linux test run can omit SQL behavior. Refactor these relational fixtures and assert SQL Server and actual execution in the Linux gate. |
| [Playwright startup](../../e2e/playwright.config.js) invokes PowerShell/LocalDB outside CI; [smoke tests](../../e2e/tests/smoke.spec.js) reset data. | Keep local smoke disposable; add a separate hosted smoke configuration without reset, fake OAuth or local web-server startup. |
| [Deployment](../../.github/workflows/deploy.yml) validates the SQL secret before an unreachable Kudu fallback containing `powershell.exe` and `D:\home`. It applies migrations and rebuilds rollback code. | Remove the fallback; isolate rehearsal credentials/tags; make the hosting cutover schema-neutral and use pinned artifacts. Rebuilding a parent commit is not a verified Windows rollback. |
| [Program](../../MiniPainterHub.Server/Program.cs) loads local JSON after normal configuration; project publishing has no explicit exclusion. | Exclude all `appsettings.Local*.json`, including examples, from deployment artifacts; inspect final contents. Preserve the native development override workflow. |
| [Startup](../../MiniPainterHub.Server/Infrastructure/Startup/ApplicationStartup.cs) calls production admin seeding independently of the automatic-migration flag. | Disabling migrations alone cannot isolate an existing candidate. Replace its runnable payload and remove production access before any start/scale operation. |
| [Maintenance middleware](../../MiniPainterHub.Server/Middleware/MaintenanceModeMiddleware.cs) combines shared database controls with host configuration; established SignalR connections do not re-enter this middleware. | Keep Public site enabled in SQL; close each host with its own setting, drain requests, and explicitly terminate old connections. |
| [Readiness](../../MiniPainterHub.Server/Infrastructure/Startup/HealthEndpoints.cs) is narrower than end-to-end behavior. | HTTP 200 does not prove real SQL tests, uploads, email, OAuth, keys or SignalR. |

### Earlier live observations: refresh before action

The following were recorded in the earlier read-only inventory in this planning chat. **They were not re-queried during the November 1–2 reviewer debate and are not current-state guarantees.** No secret values belong in this note.

- Both apps were Running in `mphp-rg`. Windows was B1; Linux was F1 with `DOTNETCORE|10.0`. B1 allocation was not demonstrated.
- Both had the same production SQL and media Blob connections, the same JWT signing key, admin seeding enabled, and identities granted access to production ACS email. Google/email confirmation were enabled. Configuration aliases included both colon and double-underscore forms.
- Windows had two Data Protection keys and Linux had one distinct key, with no shared IDs. Inspected XML had no key-level encryption wrapper. This suggests portability; no cross-host unprotect test was performed. Recheck complete records, including revocations, before import.
- Public DNS used Cloudflare and public TLS was valid. ARM reported disabled custom-domain SSL bindings on Windows. This does **not** establish a broken public site or a valid Azure origin TLS arrangement: Cloudflare mode, origin Host/SNI and origin certificate remain unknown.
- SQL reported seven-day point-in-time retention. Blob/container soft-delete retention was seven days; versioning was not confirmed enabled. No restore was executed.
- Successful deployment records were found on both hosts, but deployment IDs were not established as Git commits or recoverable immutable packages.
- The guard was enabled at $50, not dry-run, with three named app targets. The pre-existing local guard source (outside this change set) still targets `MiniPainterHub`, `rollandpaint-prod-ca` and `tastebudz-prod-23df46c9`. A differently named app is not automatically covered.

Outstanding facts: current costs/forecast and guard execution, actual B1 capacity, effective published configuration, SQL migration history/collation/compatibility and firewalls, Blob recovery, immutable rollback artifacts, key unprotection, identity inheritance, and Cloudflare/DNS/TLS routing. Record dated evidence and a named operator for each gate; leave unexecuted checks marked **not run**.

## Phases and acceptance gates

### Phase 0 — finish focused read-only inventory

Refresh app/plan identities, exact resource IDs, attached apps, runtime, settings by name and normalized meaning, deploy source, effective published configuration and permissions. Compare secret values only in memory when needed; report equality/presence, not values. Inspect production references in app settings, connection strings, Key Vault references, local files, startup commands and deployment automation.

Refresh guard state/allowlist, subscription-wide month-to-date spend plus forecast, quote and deadline. Do not run the guard script's `Test` mode: it modifies the live guard even though it avoids stopping apps.

Split the exit gate:

- **For the capacity test:** exact candidate/plan, verified Windows isolation from the proposed changes, safe candidate-stop/probe sequence, guard coverage, forecast, operator, two-hour timebox and F1 cleanup commands are reviewable.
- **Before production preparation:** also resolve keys, artifact provenance, SQL/media recovery, migration history, identity/network access, DNS/TLS and the deadline conflict below. These need not delay a harmless dependency-free capacity probe.

**Next implementation approval is for Phase 1 only**, including candidate isolation and cleanup. This document does not grant it.

### Phase 1 — isolate first, then prove bounded B1 capacity

1. Under separate approval, stop only the Linux candidate and verify it is stopped. Prevent its existing deployment automation from restoring the old payload/settings. Preserve its complete historical Data Protection ring and revocation records in encrypted recovery storage before content/configuration replacement; verify inventory/integrity without exposing key material. Retain the original key directory until recovery is confirmed. This preservation does not require proving token unprotection before the probe. Do not change the Windows app or shared credentials.
2. While stopped, remove candidate production SQL/Blob/JWT/OAuth/email/key references, including aliases and published local overrides. Remove only its production service grants; inventory direct and inherited grants and any secret-reference access. Keep Windows permissions intact. If effective production access cannot be removed safely, stop here and revise the isolation method.
3. Replace the old runnable app with a reviewed, minimal .NET 10 startup probe that has no database, migration, seeding, reset, media, auth or email code. Remove old deployment/startup hooks and runnable content, not unrecovered key history. Use a deployment method proven to preserve the stopped state; if it requires starting the old app, stop and revise the method. Re-read the final payload, configuration, permissions and stopped state. No early restart to “see what happens.”
4. Submit the first B1 allocation request only after this isolation gate. Record that timestamp as the start of the paid test. Confirm Linux/B1, instance count and .NET 10 startup; make a basic HTTP request and one restart. Successful allocation and probe startup are separate results.
5. Investigate a startup failure for at most **60 minutes**. Begin cleanup no later than **minute 90**. Restore the candidate plan to F1 and confirm the actual SKU by **minute 120**, after success, failure or pause, unless an explicit paid extension was approved.
6. Leave the probe and candidate isolated after cleanup. Do not restore its former production settings. If downgrade fails, escalate immediately with actual SKU and continuing hourly cost; stopping the app is not cost cleanup. Do not report the timebox as a hard billing cap.

Exit gate: recorded B1 allocation/runtime outcome and verified F1 cleanup. If allocation fails, stop substantial migration engineering and investigate quota/support without repeated paid experiments. Returning to F1 releases capacity; success does not reserve B1 for rehearsal.

### Phase 2 — minimal compatibility and delivery work

After capacity feasibility is demonstrated:

- Align the infrastructure/runtime and bootstrap with .NET 10. Use a migration-specific definition that references retained services; preview changes and reject production SQL/media recreation. Maintain Windows paths only as explicit transitional configuration.
- Exclude local JSON from publishing; verify filename casing, shell line endings, URLs, static assets and platform dependencies in the server package. Do not rewrite unrelated MSBuild paths or Windows configuration precedence.
- Add a common explicit-SQL fixture for the three relational tests above. Windows retains LocalDB by default; the Ubuntu relational job requires its TCP SQL connection and fails when absent. Use an isolated SQL Server service on CI, matching production collation/compatibility where applicable; record provider and executed tests. Keep existing InMemory unit tests.
- Make local Playwright startup depend on OS and connection type. PowerShell/LocalDB starts only for Windows plus a LocalDB connection. Explicit SQL on Linux uses normal dotnet startup. Keep reset-enabled tests bound to disposable databases.
- Add a hosted Production smoke configuration/spec that uses a supplied HTTPS origin and controlled accounts, with no local server, reset endpoint or fake provider. Verify its destination before any write.
- Remove the unreachable Windows Kudu fallback. Create a separately approved rehearsal environment with target-specific publish credentials, database secret, concurrency and artifact records. Retain the current publish-profile approach for this migration; OIDC can be a later change.
- Freeze feature/schema changes for the OS transition. Verify the selected revision against production migration history; if it needs new schema, finish that as a separate release before resuming. Rehearsal may migrate disposable SQL; the OS cutover must not silently apply production migrations or update `production-last-good` from rehearsal.

Exit gate: Windows build/tests/debugging and local browser smoke still work; Ubuntu relational tests actually execute; the published Linux package starts with hosted configuration; existing CI dependency/security gates pass. Record actual results per commit, not only intended commands.

### Phase 3 — isolated rehearsal, keys and TLS preparation

Obtain separate approval for the rehearsal resources and paid duration. Reuse the candidate; do not create an unguarded app. Reconfirm B1 capacity. Record its shutdown/downgrade deadline and an operator; on a pause, return to F1 after removing rehearsal features that prevent downgrade. Rehearsal is disposable and has no production data or credentials.

Use a dedicated Azure SQL test database, separate media storage credentials, separate key ring/Key Vault key, controlled email service/recipients and test OAuth application. Run in Production mode with seeding, automatic migrations and reset disabled; the deployment operator applies migrations to disposable SQL. Match enabled production providers; use the actual rehearsal HTTPS hostname in allowed hosts, origins and callback registrations. Apply no production registration changes implicitly.

Configure Linux managed identity for rehearsal services first, and add production grants only during the approved production transition. ACS uses identity; media currently uses a connection string. Verify SQL/storage network access and required secret-reference access. For the shared ring, scope Storage Blob Data Contributor to its private container and Key Vault Crypto Service Encryption User to the wrapping key, with a versionless key URI and retained historical versions. Verify the actual role requirements and effective grants. No broad subscription grant is a shortcut. [Key Vault role capabilities](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-crypto-service-encryption-user).

**Data Protection working design:**

- Use a dedicated private key-storage account/container, separate from public media access, shared by the two production hosts. Use `Azure.Extensions.AspNetCore.DataProtection.Blobs` and `Azure.Extensions.AspNetCore.DataProtection.Keys` with managed identity; keep `SetApplicationName("MiniPainterHub")`, protector purposes, cookie names and token lifetimes stable. Configure persistence and explicit encryption together.
- Preserve the required union of historical Windows and Linux key records, including IDs, material and revocations. Import using a rehearsed, integrity-checked process with encrypted recovery copies, without logging or committing key XML. Do not discard a key merely because it expired.
- **Legacy XML remains unwrapped at the Data Protection layer**, protected by private Azure Storage, platform encryption and restricted access. Key Vault wrapping applies to newly generated keys; configuration alone does not retroactively wrap old XML. This is an explicit migration tradeoff to approve with the key-transition runbook. Requiring rewrapping changes scope and must use a separately validated method, not bespoke cryptography.
- Prevent filesystem/key-ring divergence during final capture: close both hosts with admin bypass disabled, drain/terminate connections, stop their application processes and verify quiescence before taking the final ring snapshots. Even the exempt bypass endpoint can issue a protected cookie and trigger key creation while a process is running. Reconcile complete inventories, switch configuration while stopped, then restart under the verified operator-access restriction to load the shared ring. Retain every Key Vault key version needed to decrypt retained records.
- Test Windows-issued protected values on Linux and Linux-issued values on the retained Windows build, after restart and fresh key generation. Cover email confirmation, external-provider correlation/link-intent cookies, maintenance bypass and valid JWTs separately. Keep JWT key/issuer/audience/expiry and database exchange records unchanged.
- Rehearsal proves this using **test keys**. Actual production continuity uses a restricted, approved production transition/check with the real ring, never the disposable rehearsal environment. The Windows rollback artifact must support the final shared provider and permissions; the original filesystem-only build is insufficient.

Microsoft distinguishes persistence from key encryption and documents the supported Blob provider. Azure Storage encryption is a different layer from Data Protection key wrapping. [Key storage](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0), [key encryption](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0), [storage encryption](https://learn.microsoft.com/en-us/azure/storage/common/storage-service-encryption), [key management](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0).

**Domain/TLS gate:**

Inventory Cloudflare proxy status, origin SSL mode, Host/SNI overrides, both hostname records, TTLs, certificate source/expiry and Azure bindings. Preserve edge protection and Windows rollback TLS. Prove apex and www against the intended Linux origin with correct Host/SNI before sending users there. A valid edge certificate is insufficient.

Validate simultaneous domain bindings rather than assuming they work. Preprovision an imported certificate if managed issuance cannot be completed before traffic changes. Do not assume an App Service managed certificate can be exported or rely on certificate issuance during the 30-minute cutover. If the exact binding/routing sequence cannot be proved, defer cutover and revise the runbook. [Active-domain migration](https://learn.microsoft.com/en-us/azure/app-service/manage-custom-dns-migrate-domain), [certificate requirements](https://learn.microsoft.com/en-us/azure/app-service/configure-ssl-certificate), [current ASMC validation rules](https://learn.microsoft.com/en-us/azure/app-service/app-service-managed-certificate-changes-july-2025).

Exit gate: the Phase 2–3 portions of the acceptance matrix pass on named artifacts/configuration using disposable dependencies and test keys; the Windows rollback release supports the shared provider; SQL/media recovery evidence is adequate; host routing and rollback complete within ten minutes in rehearsal. Actual production continuity/public-origin proof belongs to Phase 4, and observation/retirement evidence to Phase 5. All production changes still require the concrete transition/cutover approval.

### Phase 4 — controlled production transition and traffic cutover

Name one operator and record immutable package hashes, configuration/permission snapshots, existing DNS/proxy destination, certificates, schema and rollback commands. Freeze deployment automation that could overwrite either host. Complete any Windows preparatory release under its own reviewed production change; include its downtime in the schedule, not as an invisible prerequisite.

Promote the isolated candidate to a production candidate only after rehearsal passes: replace rehearsal dependencies with the verified production references, same public origins and JWT settings, shared key ring and narrowly scoped production grants. Explicitly disable seeding, reset and automatic migrations. Keep Linux closed while production continuity is checked. Rehearsal credentials/keys must not leak into this state.

The shared database **Public site** control stays enabled. Use `Maintenance__Enabled` per host:

| Traffic state | Windows maintenance | Linux maintenance |
| --- | --- | --- |
| Windows live; Linux connected for controlled checks | false | true |
| Drain and switch | true | true |
| Linux live | true | false |
| Rollback complete | false | true |

Health and maintenance-bypass routes are exempt. During drain/key capture, explicitly set `Maintenance__AllowAdmins=false` on both hosts and prove ordinary requests remain blocked even with an existing bypass cookie. Drain requests/uploads, terminate established SignalR connections and stop application processes for final key capture; configuration flags alone do not prove process quiescence. Keep admin bypass disabled on the closed Windows host while Linux is checked or live, and on closed Linux after rollback.

Before temporarily enabling bypass on the host under test, prove a restricted operator-access path that also blocks non-operators at the direct Azure hostname, not only at Cloudflare. Rehearse this with the actual platform capabilities, retain required health/platform access, and restore the intended restrictions before public opening. If isolation of the operator checks cannot be proved, stop before production transition and revise the runbook. Do not assume an unverified Cloudflare feature exists. Verify reconnects are denied on the closed host; do not serve live chat on both hosts concurrently because the current SignalR setup has no shared backplane.

Allow **at most 30 minutes of visitor maintenance** for the approved cutover. Start the clock when visitors are first closed. Use the rehearsed key finalization, host gating and DNS/proxy sequence. By minute 20, either required public-origin checks pass and Linux opens, or rollback starts. The remaining ten minutes are usable only because that recovery path was timed beforehand; do not schedule a window that depends on unmeasured DNS propagation.

During controlled checks verify password/OAuth and existing tokens, email, a disposable upload and variants, public pages/redirects, readiness, TLS and two-client SignalR through the real public origin. Do not run reset suites or broad load tests against production. Preserve the same SQL/Blob data and keep Windows closed but recoverable once Linux opens.

### Phase 5 — rollback, observe and retire

Rollback immediately for data-integrity/key problems or broken critical login/upload/email/chat behavior; also roll back for sustained readiness failure or material error/latency regression against the recorded baseline. Do not wait for minute 20 when a blocker is already clear.

Close Linux and its existing connections, restore the recorded route, confirm Windows has the correct shared keys/configuration, then reopen Windows and check auth, data/media, TLS and chat. Keep Linux closed. Do not restore an older database or delete new media as part of host rollback. The workflow's code rollback is not traffic rollback.

Keep Windows artifact, permissions, keys, domain/TLS and usable hosting for **72 hours after successful cutover**. Review health/errors, real authentication/email/upload/chat behavior and spend during that period. Retirement requires stable Linux evidence and retained recovery material, not merely elapsed time.

Then, under the approved retirement runbook, verify the Windows plan has no other apps; retire the old app **and paid plan**, obsolete publish credentials and identity grants. Preserve key history, required Key Vault versions and recovery artifacts. Update deployment targets, monitoring, cost controls and [Deployment](<DEPLOYMENT.md>). Verify plan removal/final billing state; an app stopped by the cost guard still leaves plan charges.

## Mandatory acceptance matrix

All rows below are **planned / not executed by this review**. Phase 3 requires only rehearsal evidence; it does not wait for later production or retirement results.

| Area | Evidence stage | Required evidence |
| --- | --- | --- |
| Windows workflow | Phase 2 | Native IDE/debugging/PowerShell, LocalDB and isolated local smoke continue working without WSL/Docker. |
| Linux SQL | Phase 2 | Provider is SQL Server; formerly Windows-only tests execute; migration and relational constraint/race cases pass; representative account/project/post/search/message operations persist after restart. |
| Azure SQL rehearsal | Phase 3 | Production-equivalent collation/compatibility and pending-migration checks; disposable migrations succeed; no production server/database identity in test configuration. |
| Published assets | Phases 2–3; small public check in 4 | Published server output: Blazor boot, deep-link reload, exact-case CSS/JS/fonts/images/WASM, compressed content type/encoding/cache, no local JSON or secret files. |
| Images | Phase 3; controlled check in 4 | Read fixture/existing-image behavior, upload/process/serve variants and delete only disposable media; cover post, avatar and guide paths in rehearsal. |
| Auth/tokens | Phase 3 with test keys; real continuity in 4 | Password, confirmation/resend, enabled OAuth callbacks/linking and failure paths; old JWTs and both directions of Data Protection continuity; restart/new-key behavior proved without invalidating existing tokens. |
| Email/identity | Phase 3 test services; controlled check in 4 | Actual ACS delivery to controlled recipients with correct origin; identity access demonstrated, no tokens/links/secrets in production logs. |
| SignalR/maintenance | Phase 3 rehearsal; verify in 4 | Two clients exchange messages/reconnect; closed host drains/terminates old sessions and rejects reconnects, including existing admin bypass; reopening/rollback restores service. |
| Domain/proxy | Phase 3 route/TLS preparation; public proof in 4 | Apex/www, canonical redirects, forwarded scheme/host, SEO HTML/robots/sitemap, edge and origin TLS, expiry/renewal path and retained Windows route. |
| Recovery/performance | Phase 3 | Comparable short smoke against controlled fixtures, no unexplained critical errors; SQL/media recovery procedure/evidence; traffic rollback timed at no more than ten minutes. |
| Probe/pause costs | Phase 1; repeat on rehearsal pause | Actual SKU F1, or explicit paid extension; guard remains enabled and no production dependencies are restored. |
| Observation/retirement | Phase 5 | Stable Linux behavior through 72 hours; recovery material retained; final old-plan retirement and cost state verified. |

## Effort, costs, deadlines and decisions

**Conditional engineering range: 14–28 hours**, excluding cloud/support waiting and the 72-hour observation period. Allocate roughly 2–4 hours to inventory/capacity procedure, 4–8 to portable packaging/tests/delivery, 5–10 to key/TLS/rehearsal, and 3–6 to controlled migration/recovery/retirement. Key portability, artifact provenance or TLS complexity may exceed this range. Broader hardening is outside it.

Official Azure Retail Prices API was checked on November 1, 2026 for Canada Central B1 Consumption in USD; subscription discounts, Dev/Test pricing and taxes were not assumed. These are compute estimates, not a quotation or approved spending cap. [Azure Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices).

| Item | Approximate compute cost |
| --- | --- |
| Linux B1 | $0.018/hour; $13.14 at 730 hours |
| Windows B1 | $0.083/hour; $1.99/day; $60.59 at 730 hours |
| Two-hour Linux capacity test | $0.036 if allocation and cleanup stay within two hours |
| One week / two weeks of Linux rehearsal | $3.02 / $6.05 |
| 72 hours retaining Windows | $5.98, plus $1.30 for Linux over the same period |

Add isolated SQL/media/email, key storage/Key Vault, logs and requests; refresh their quote before creating them. The $50 guard evaluates the **whole subscription**, including rehearsal and other apps, using delayed cost data on a six-hour cadence. It can stop production during rehearsal and does not stop SQL/storage/paid-plan charges or automatically restart apps next month. Keep it enabled; forecast baseline plus overlap and billing lag before approval. Microsoft bills dedicated compute at plan level. [App Service billing](https://learn.microsoft.com/en-us/azure/app-service/overview-hosting-plans).

**Spending deadline: November 4, 2026, 5 p.m. Eastern.** By then complete observation and retire Windows, use a proven and explicitly approved cheaper rollback/hosting arrangement, or obtain an explicit paid extension with cost and a new deadline. “Postpone rehearsal” is not a spending action while Windows B1 remains allocated.

A full 72-hour observation window before that spending deadline required successful cutover by **November 1, 2026, 5 p.m. Eastern**. It is now November 2; that target has passed and this task has performed no cutover. Resolve an explicitly approved new deadline/budget or proven cheaper recovery arrangement **before any later cutover**. Preserve the full recovery period. Never silently shorten recovery, extend spending, disable the guard or downgrade the live Windows site. Lack of approval is an unresolved cost risk to report, not authority to interrupt production.

| Decision gate | Required decision or default |
| --- | --- |
| Capacity operation | Separately approve exact candidate isolation, first paid request, two-hour window, operator and F1 cleanup. No approval given by this note. |
| Rehearsal spend | Approve exact disposable resources and end time using refreshed forecast; do not inherit an unlimited B1 duration from the probe. |
| Key transition | Review legacy-key encryption tradeoff, exact import/quiescence procedure, required roles and bidirectional proof. Block on incompatibility rather than silently invalidate tokens. |
| TLS/cutover | Resolve actual Cloudflare/origin arrangement and certificates, final route/rollback commands, Windows artifact and measured timing. |
| Calendar | The November 1 cutover target has passed. Approve a revised spending deadline or proven cheaper recovery arrangement before cutover; retirement/extension remains a separate operational decision. |

## Review record and validation

Completed three rounds with three newly spawned reviewers per round: data/token safety, operations/cost/TLS, and delivery/testing. Reviewers exchanged objections directly; acceptance was not presumed from agreement with the hosting direction.

| Round | Findings and resolution |
| --- | --- |
| 1 | Required true pre-start isolation, bounded billing cleanup, SQL tests that execute, separate hosted smoke, explicit legacy-key encryption, compatible Windows rollback and an honest deadline branch. Incorporated into the consolidated plan. |
| 2 | Found a circular rehearsal exit gate, risk of losing candidate key history, and admin bypass undermining quiescence. Added evidence stages, encrypted historical-key preservation, stopped-process capture and verified operator restrictions. |
| 3 | All three reviewers explicitly **ACCEPTED** the same November 2 revision with no further required amendments. The passed November 1 target is explicitly handled. |

The common accepted snapshot had SHA-256 `63472B1CFD38BD826319208F6BE035DE60C9CB9BAD9EB23FADE0D775BF00B638`. The accepted operational plan is unchanged; the later execution progress below is outside that reviewed snapshot. Consensus means the gated plan is suitable to use, not that implementation, spending or cutover has been approved.

This revision changes documentation only. No application tests, builds, Azure settings/resources, deployments, production checks or data changes were executed for the reviewer debate. Historical build and live inventory results above are not current validation.

- `python tools/docs/check-doc-references.py --repo .`: 60 documents checked, zero issues.
- `git diff --check`: no whitespace errors; existing LF/CRLF notices concern pre-existing edits.
- SHA-256 comparisons: all 11 unrelated modified/untracked files present at the start remained unchanged.
- Updated this preplan and added the local vault change log and reflection (`2026-11-01-linux-migration-brainstorm.md`, pre-existing untracked notes outside this change set); existing routing links were retained.

**Smallest useful next step:** finish Phase 0's capacity-specific read-only refresh and prepare the exact isolation/probe/F1 cleanup operation for separate approval. Prove allocation before substantial engineering; proceed to migration only through the listed evidence gates.

## Execution progress — November 2, 2026

The user authorized starting the consolidated preparation/rehearsal work. The separately approved paid-operation and production gates above remain in place.

### Initial authentication interruption (resolved later on November 2)

- At approximately 20:45 UTC (3:45 p.m. Eastern), Azure CLI token acquisition failed with `AADSTS50078`: administrator policy requires renewed MFA. No live ARM inventory could be read.
- `az account show` and cached account listing report `Azure for Students`, subscription `23df46c9-3639-4a03-bb4b-61234224142b`, as `Disabled`. **This is cached metadata, not a verified live subscription state.** Do not infer that the live site is disabled or initiate billing changes from it.
- Anonymous GET requests to `https://rollandpaint.com` and `/healthz/ready` returned HTTP 200. These checks do not establish complete functional health or confirm the current origin host.
- Current guard state, spending/forecast, resource identities/configuration, stopped-state deployment behavior and B1 availability remain **not verified**. No Azure settings, resources, permissions, deployments, guard configuration or production data were changed.
- Resume with interactive Azure login/MFA, then complete Phase 0's capacity gate. Do not scale or start the candidate based on the older inventory.

### Prepared capacity probe

- Added [LinuxCapacityProbe.csproj](../../tools/cloud/LinuxCapacityProbe/LinuxCapacityProbe.csproj) and [Program.cs](../../tools/cloud/LinuxCapacityProbe/Program.cs), a standalone .NET 10 app outside the production solution. It has no project/package dependencies, application assets, database, auth, media, email or key-persistence code.
- GET `/` identifies the probe. GET `/healthz` returns its runtime, OS and a per-process marker for restart verification. It contains no production operations.
- Added [local verification and packaging](../../tools/cloud/verify_linux_capacity_probe.py). It checks the exact four-file payload, absence of dependency packages/application assets, two Production-mode process starts, a changed process marker, and absence of application/reset/OAuth endpoints before creating the ZIP and hash manifest.
- SDK 10.0.401 published the probe successfully. Local verification passed on **Windows**, runtime .NET 10.0.12; this is not Linux or Azure runtime evidence.
- Package: `artifacts/linux-capacity-probe/linux-capacity-probe.zip`. SHA-256: `0288ac2cf43afabcd2ffcd91bd65bca21155a3d427785f3a366435f5f6fa6a2e`. Verification and source/payload hashes: `artifacts/linux-capacity-probe/verification.json` (local ignored artifacts).

Rebuild and verify from the repository root:

```powershell
dotnet publish tools/cloud/LinuxCapacityProbe/LinuxCapacityProbe.csproj --configuration Release --output artifacts/linux-capacity-probe/publish
python tools/cloud/verify_linux_capacity_probe.py
```

### Validation and remaining gates

- `dotnet build MiniPainterHub.sln --configuration Release --no-restore`: passed, zero errors, 21 existing NuGet vulnerability warnings. This is **not** a clean security gate. Reported packages include ASP.NET WebAssembly Server/SignalR, ImageSharp and transitive System.Security.Cryptography.Pkcs; reassess and resolve during Phase 2 before CI acceptance.
- The probe's automated local checks passed. Full server/client suites, native debugger checks, Ubuntu SQL, Azure rehearsal and cutover checks were **not run** in this preparation step.
- Candidate isolation, encrypted historical-key recovery, stopped-state payload replacement, B1 allocation and F1 cleanup are **not run**. No capacity feasibility claim is made.
- The documented November 4 spending-deadline conflict remains unresolved. Preserve the full 72-hour recovery period.

### Live inventory after renewed MFA — November 2, 21:33–21:38 UTC

Device login refreshed the management token. Azure CLI subscription enumeration encountered a connection reset, but direct authenticated ARM reads over IPv4 succeeded. The live subscription is **Enabled**, with `PayAsYouGo_2014-09-01` quota and Azure's built-in spending limit `Off`; the separate $50 Logic App guard is enabled. The earlier cached `Disabled` result is superseded.

| Check | Live finding |
| --- | --- |
| Windows host | `MiniPainterHub` Running; Windows B1 plan `ASP-mphprg-85e2`; one instance; its plan's app enumeration contains only `MiniPainterHub`. |
| Linux candidate | `rollandpaint-prod-ca` Running; Linux F1 plan `ASP-mphprg-linux-stage`; one instance; its plan's app enumeration contains only the candidate. No custom-domain bindings. |
| Runtime | Candidate configured for `DOTNETCORE\|10.0`; current Linux stack catalog offers .NET 10 LTS, not hidden or preview. Both published applications target `net10.0`; Windows' control-plane `v8.0` label does not describe its deployed target framework. |
| Production references | Candidate SQL/Blob/JWT and Google credentials match Windows in memory. Both have admin seeding, Google and email enabled. No separate connection-string entries or Key Vault app-setting references were found. Secret values were not written to the evidence files. |
| Published configuration | Both publish `appsettings.Local.Production.example.json`; actual `appsettings.Local.json` and `appsettings.Local.Production.json` were absent from the root listing. The example's nonempty fields are not evidence of valid credentials. Candidate startup is `dotnet MiniPainterHub.Server.dll`. |
| Keys | Windows has two key XML records at `D:\home\data-protection-keys`; Linux has one at `/home/data-protection-keys`, with no common IDs. Complete current directory listings and XML record hashes were inspected in memory; no revocation records were present in those listings. None had a Data Protection encryption wrapper. No raw key XML was persisted and no cross-host unprotect test was run. |
| Candidate identity | System-assigned principal `d47fc18d-1bcf-4a03-a2a4-b496697817ff`. Subscription role enumeration found one direct `Communication and Email Service Owner` assignment on `minipainterhub-acs`; Windows has a distinct principal and assignment. This does not prove absence of directory-group, ancestor-scope or data-plane grants. Disable the candidate identity for the dependency-free probe to remove that uncertainty; recreate an identity for isolated rehearsal later. |
| Delivery | Both ARM source-control records have no repository, and `scmType` is `None`. GitHub's active `Deploy` workflow and production variables target Windows. Candidate SCM basic auth is enabled; FTP basic auth policy is enabled but FTPS is disabled. Disable candidate-only publishing basic auth during isolation; keep bearer-authenticated management access. |
| Guard | Enabled, non-dry-run, $50 threshold, all three expected apps allowlisted. Latest run at 21:04 UTC succeeded. The 15:04 UTC failure was a billing-query HTTP 429; no stop action executed in that failed run. No guard configuration/test operation was performed. |
| Public HTTP | `curl -4` returned 200 for apex and readiness. Python urllib received 403 for those URLs; that client-dependent result does not establish a site outage. Full origin/TLS/functional proof remains pending. |

Cost evidence:

- Subscription ActualCost month-to-date: **$3.813987530060017 USD**, approximately $46.19 below the guard threshold, subject to billing delay.
- November 1's reported complete-day cost was **$2.34727038607133**. A flat 30-day continuation is approximately **$70.42**; this is a simple planning projection, not an Azure forecast, and exceeds the $50 guard if hosting is left unchanged.
- The forecast request including actuals returned only November 1–2 actual rows. A future-only request returned HTTP 424 `FailedDependency`. **A provider-generated forecast is unavailable**; do not label the actual rows as forecast evidence.
- The Azure Retail Prices API recheck returned Canada Central Consumption rates of **$0.018/hour Linux B1** and **$0.083/hour Windows B1**. The bounded two-hour probe adds approximately **$0.036 compute**, excluding tax/other usage. For conservative near-term planning, two more days at the observed daily baseline plus one extra day for reporting lag and the probe would put reported-plus-estimated spend near **$10.89**, assuming no other usage changes. This is not a billing cap or a spending-deadline extension.
- Sanitized local evidence is in `tmp/linux-migration-2026-11-02/inventory.json`, `details.json` and `preflight.json`. These ignored task artifacts contain metadata, comparisons and record hashes, not credentials or key material.

### Approved Phase 1 operation

This approval scope covers candidate isolation, at most two hours of B1 allocation and verified F1 cleanup. It does not approve rehearsal resources, production preparation, traffic cutover or a deadline extension. Proposed operator: Codex in this chat, with the user available for escalation.

Exact targets, all in subscription `23df46c9-3639-4a03-bb4b-61234224142b`, resource group `mphp-rg`:

- Candidate: `/subscriptions/23df46c9-3639-4a03-bb4b-61234224142b/resourceGroups/mphp-rg/providers/Microsoft.Web/sites/rollandpaint-prod-ca`.
- Candidate plan: `/subscriptions/23df46c9-3639-4a03-bb4b-61234224142b/resourceGroups/mphp-rg/providers/Microsoft.Web/serverfarms/ASP-mphprg-linux-stage`.
- Candidate's direct ACS role-assignment suffix: `97672270-6f8a-4219-91fc-b311ad7ada60` under `Microsoft.Communication/communicationServices/minipainterhub-acs/providers/Microsoft.Authorization/roleAssignments`.
- Protected Windows app/plan: `MiniPainterHub` / `ASP-mphprg-85e2`. No changes to these, public DNS/TLS, production SQL/media, shared credentials or the cost guard are included.

Execution sequence:

1. Re-read target IDs, attached apps, guard and Windows readiness. Stop only `rollandpaint-prod-ca` and verify `Stopped`. Reconfirm that automated deployment still targets Windows; disable SCM and FTP basic publishing policies only on the candidate. Use existing bearer-authenticated access for the remaining work.
2. While stopped, read the complete candidate key directory, including any newly appearing keys/revocations, plus its configuration into memory. Save a Windows CurrentUser DPAPI-encrypted recovery bundle outside the repository under `%LOCALAPPDATA%\MiniPainterHub\MigrationRecovery`. Verify decryption and per-record hashes in memory. Recovery depends on retaining this Windows user profile; retain the original remote key directory too. Do not persist plaintext XML, secrets or plaintext deployment archives. If stopped-state access or recovery verification fails, leave the candidate stopped on F1 and stop the operation.
3. Remove the candidate's exact ACS role assignment, then disable its system-assigned identity for the probe. This deliberately gives the later rehearsal a newly created identity and newly scoped grants. Replace all candidate app settings/connection strings with an explicit minimal probe configuration; remove all application credentials, monitoring connection strings, aliases, local overrides and deployment hooks. Keep server-side build automation disabled, `alwaysOn=false`, and .NET 10 configured. Re-read stopped state after configuration changes.
4. Replace only `/home/site/wwwroot` with the locally verified probe package. Preserve `/home/data-protection-keys` and recovery material. The intended publish operation uses clean ZIP deployment with restart, Kudu warmup and startup tracking all disabled. Verify stopped state before and after; inspect exact file names and hashes, startup command `dotnet LinuxCapacityProbe.dll`, settings, identity and publishing policies. Stop without a paid request if this method cannot operate while stopped. Stopped-state deployment is an execution gate, not an already-proven result.
5. Only after isolation passes, record `T0` immediately before the first B1 allocation request; resize only the candidate plan to B1 with one instance. Start the probe, request `/` and `/healthz` at `https://rollandpaint-prod-ca.azurewebsites.net`, verify Linux/.NET 10 and probe identity, restart once and verify the changed process marker. Record allocation and runtime success separately.
6. After the result, stop the probe and immediately return the candidate plan to F1. Investigate startup failure for no more than 60 minutes, begin cleanup no later than T0+90 minutes, and verify actual F1 by T0+120 minutes. Leave candidate credentials/identity removed and the probe stopped. If downgrade fails, report the actual SKU and continuing cost immediately. No automatic paid extension is authorized.
7. Recheck Windows readiness and guard state; record package hash, timestamps, B1 result and final SKU. Only successful capacity feasibility permits Phase 2 engineering.

Concrete cleanup command (prepared, **not executed**):

```powershell
az webapp stop --subscription 23df46c9-3639-4a03-bb4b-61234224142b --resource-group mphp-rg --name rollandpaint-prod-ca
az appservice plan update --subscription 23df46c9-3639-4a03-bb4b-61234224142b --resource-group mphp-rg --name ASP-mphprg-linux-stage --sku F1
az appservice plan show --subscription 23df46c9-3639-4a03-bb4b-61234224142b --resource-group mphp-rg --name ASP-mphprg-linux-stage --query sku
```

Use authenticated ARM over IPv4 if the CLI's network path resets, with the same exact plan resource ID and `2023-12-01` API: PATCH `{"sku":{"name":"F1","tier":"Free","capacity":1}}`, then GET and verify. Stopping the app alone is not cleanup. Execution results follow.

### Phase 1 execution result — November 2 Eastern

The user approved the bounded capacity operation with "ok go" after the approximately $0.04 incremental-compute estimate, then instructed continued work. Windows production, public DNS/TLS, production SQL/media and the guard were not mutated.

- Stopped the candidate at **21:48:32 UTC**. Disabled only its SCM/FTP basic publishing policies. Bearer-authenticated Kudu reads remained available while stopped.
- Preserved the complete stopped-state candidate key ring and configuration in a Windows CurrentUser DPAPI bundle outside the repository: `%LOCALAPPDATA%\MiniPainterHub\MigrationRecovery\linux-candidate-20261102T214841Z.dpapi`. Encryption/decryption round trip and encrypted-file readback passed. Encrypted SHA-256: `a7acd8bc72fcef13902ba34ec0abf1bf0b8e3fb7f25a39b8c9f5f681dd0319f0`. Retain this Windows user profile for recovery.
- Removed the candidate's exact ACS role assignment and system-assigned identity. Replaced its application settings with four nonsecret probe settings and cleared connection strings. Replaced its startup command and retained `alwaysOn=false`, FTPS disabled and .NET 10. Windows configuration, identity and grants were untouched.
- Clean ZIP deployment with restart/warmup/startup tracking disabled preserved stopped state. Verified the exact four-file payload and each hash. Repository/build hook directories were empty; configuration contained only the platform restart marker. The original `/home/data-protection-keys` directory was retained.
- First B1 request: **21:52:22.968 UTC**, establishing the unchanged **23:52:22.968 UTC** cleanup deadline. B1/one-instance/Linux allocation passed. The first HTTP probe passed its .NET 10 checks but the harness incorrectly required the literal word "Linux" in `OSDescription`; the actual platform describes itself as Ubuntu. Cleanup restored F1 at **21:55:05.742 UTC**.
- Corrected the probe to report `OperatingSystem.IsLinux()` explicitly, verified two local Production-mode starts, and redeployed while stopped on F1. Corrected package SHA-256: `3892ecc0973d72fd7241158f8f7bab70a909ba40d016054c283d12026ce629ca`.
- A second short B1 allocation at **21:58:58.146 UTC** stayed within the original approved window. Allocation passed; GET `/` and `/healthz` passed at **22:01:31 UTC** on **.NET 10.0.14, Ubuntu 24.04.5 LTS, `isLinux=true`**. Restart was requested at 22:01:31 UTC and a new process marker was observed at **22:03:14 UTC**. Azure's restart acknowledgement preceded actual process replacement by about 103 seconds; poll the marker rather than assuming the HTTP 200 acknowledgement proves a completed restart.
- Final cleanup: candidate **Stopped**, plan **F1**, verified **22:03:24.968 UTC**. The two allocation-request-to-F1 intervals total approximately **7 minutes 9 seconds**, about **$0.00215** at the quoted hourly compute rate before tax/other usage; this is an elapsed-time estimate, not a finalized Azure bill. No spending/deadline extension was used.
- A post-downgrade Kudu read initially timed out. The read-only retry passed at **01:53:50 UTC November 3 (8:53:50 p.m. Eastern November 2)**: original remote key inventory/hash unchanged, encrypted recovery artifact intact, candidate still Stopped/F1, Windows root/readiness HTTP 200, guard Enabled.
- Current candidate is a stopped, isolated probe, not a production or rehearsal application. Its managed identity must be recreated with rehearsal-only permissions when that separately approved phase starts. Capacity success does not reserve B1 after returning to F1.

Sanitized execution evidence: `tmp/linux-migration-2026-11-02/phase1-journal.jsonl`, `phase1-recovery.json`, `phase1-isolation.json`, `phase1-capacity.json`, `phase1-capacity-attempt2.json`, `phase1-cleanup.json` and `phase1-final.json`. The initial harness failure remains recorded rather than overwritten. Phase 1 is complete; full-application Linux compatibility and rehearsal gates remain pending.

Additional read-only SQL metadata, refreshed at **02:00:52 UTC November 3 (9:00:52 p.m. Eastern November 2)**: production database is Online, collation `SQL_Latin1_General_CP1_CI_AS`, compatibility **170**, with seven-day point-in-time retention. A direct read-only SQL query found **22 applied migrations**, exactly matching the repository through `20260722193928_OptimizeDatabaseHotPaths`; no schema migration is currently needed for the OS transition. Evidence: `tmp/linux-migration-2026-11-02/sql-metadata.json`. No restore was executed; retention and matching history are not recovery proof.


### Phase 2 local engineering — November 2 Eastern, in progress

Work is isolated on local branch `codex/linux-migration`; nothing has been committed, pushed, merged or application-deployed. The 13 unrelated files already modified/untracked at the start remain byte-for-byte unchanged.

Implemented:

- .NET 10 bootstrap/runtime alignment, shell LF normalization and publish-only exclusion of local settings.
- Explicit real-SQL fixtures for all three relational cases, a pinned Ubuntu SQL Server 2025 CI job matching production collation/compatibility, and a TRX evidence gate that rejects missing/skipped/non-Linux results.
- OS/data-source-aware Playwright startup and server-side validation of the actual reset target before initialization/deletion. Normal native configuration precedence remains intact.
- Separate read-only hosted smoke checks, with optional controlled-account token reads and no reset, fake OAuth or local server.
- Removal of the unreachable Windows Kudu SQL fallback; migration-specific Bicep limited to candidate runtime configuration with retained SQL/media references.
- Paired Linux/Windows artifact packaging and CI publication after all applicable gates, with ZIP/file hashes, exact-case checks, migration inventory and dirty-source rejection for releases.
- .NET package patches to 10.0.14, SQL client 6.1.8 and MessagePack 2.5.303. npm audit initially found 11 high vulnerabilities in the old Lighthouse chain; Lighthouse 13.5.0 and its resolved lockfile now report **zero vulnerabilities**. Version 13.6.0 introduced another vulnerable transitive chain, so the verified clean 13.5.0 release is pinned. CI Node is 24.

At the November 2 snapshot, ImageSharp was an unresolved security gate: 3.1.12 had six advisories and patched 3.2.0 required a Six Labors build-time license. No suppression or license bypass was added. This historical blocker is superseded by the authorized Magick.NET replacement below; the Community application was never submitted. [Six Labors licensing](https://sixlabors.com/pricing/).

Validation:

- Release solution build passes with 12 warnings, all from ImageSharp; `/warnaserror` and the vulnerability gate are **not passed**.
- Final Windows server tests: **495/495 passed**, including real LocalDB cases and reset/SQL-fixture safety checks. WebApp components: **345/345 passed**. TRX evidence: `artifacts/linux-migration/test-results/phase2-final-server.trx` and `phase2-webapp.trx`.
- Full Windows browser smoke: **38/38 passed**. The initial run passed 34, failed one timing-sensitive moderation geometry assertion and left three serial successors unrun. The failed case passed two isolated repetitions unchanged. It now reads both rectangles in a single browser frame with unchanged containment limits and a bounded settling retry. No product UI files were changed by this work.
- Three hosted anonymous checks passed against the current **Windows** production site: readiness, JSON APIs and actual Blazor shell loading. No hosted writes or account-token tests were run; this is not Linux application evidence.
- Linux and Windows framework-dependent publishes and package audits passed. Local dirty packages are explicitly not release eligible. Hashes and file inventories are under `artifacts/linux-migration/sealed-local`; these are not approved deployment artifacts.
- Five browser-platform harness tests and five package-isolation/provenance tests passed. Targeted C# formatting verification passed. Migration Bicep compiled; inspection shows only `Microsoft.Web/sites/config` as mutable. A live ARM what-if has not been run.
- Lighthouse 13.5 executed both real local routes, including cached loads. Its overall audit failed only because the existing audit expected public caching for `MiniPainterHub.WebApp.styles.css`, while the user's pre-existing implementation and test intentionally require `no-cache`. Those pre-existing files were preserved. Performance scores were 69/54 against an existing advisory-only 90 target; no threshold was lowered. Evidence: `artifacts/linux-migration/lighthouse-validation.log` and `lighthouse-13-5`.

**Phase 2 is not complete.** The replacement below resolves the local license/security gate. Run Ubuntu CI with actual SQL evidence, validate native debugger attachment and the Linux full application with isolated hosted dependencies, complete the live infrastructure preview and prepare the target-specific rehearsal environment/deployment. No Phase 3 resources, identity/grants, key transition, production changes or additional paid operations were performed. The revised spending deadline and full 72-hour recovery gate remain unresolved before any eventual cutover.


### ImageSharp replacement — completed locally November 4, 2026

The user chose to avoid the Six Labors application and explicitly authorized replacing ImageSharp with Magick.NET. `Magick.NET-Q8-AnyCPU` 15.0.0 now handles post variants, guide dimension validation, avatars and seed/test image generation. There are no remaining ImageSharp package or source references. The implementation ships the Apache 2.0 license and the NuGet package's bundled dependency notices; no application, account classification or license key is required. No personal details were submitted to Six Labors.

The new shared raster decoder pins formats by file signatures, validates dimensions and aggregate frame pixels before decoding, and loads a restrictive native policy at startup. Coverage includes all six accepted raster formats, EXIF rotation, stripped metadata, transparency, animated WebP, malformed/truncated data, cancellation, aggregate animation limits, avatar JPEG output and native SVG rejection. The migration packager now requires the policy, managed/native Magick libraries and notices.

Validation after replacement:

- Release solution build with `/warnaserror`: **zero warnings and errors**.
- Full Windows server suite: **514/514 passed**, no skips, including LocalDB cases. Focused image/upload/service cases: **69/69 passed**. Evidence: `artifacts/linux-migration/test-results/magick-server.trx` and `magick-focused.trx`.
- Windows browser smoke: **38/38 passed** in 4.4 minutes, including real upload/viewer flows. Evidence: `artifacts/linux-migration/magick-browser.log`.
- NuGet vulnerability audit, including transitives, reports **no vulnerable packages** in all six solution projects. The previous ImageSharp warnings are gone. No security suppression was used.
- Six packaging guard tests pass. All 13 unrelated pre-existing files remain byte-for-byte unchanged.

Linux execution remains unverified for this replacement. Docker's Linux daemon was stopped; automatic approval review rejected the command containing Docker Desktop startup with only `blocked by policy`, so it was left stopped. Ubuntu CI with real SQL and native image processing remains a required migration gate. Cross-publish/package evidence below does not replace that gate. Nothing has been committed, pushed, deployed or cut over by this replacement; there was no additional Azure spending operation. Earlier `sealed-local` artifacts predate the replacement and are stale.

Both framework-dependent RID publishes passed with warnings treated as errors. The matching native library, policy and license notices are present, ImageSharp is absent from both dependency graphs, and ZIP plus every file hash were verified. Current validation packages are under `artifacts/linux-migration/magick-sealed-local`; both declare `releaseEligible: false` because they are local dirty-checkout builds.

- `linux-x64` ZIP SHA-256: `06343be2d1dd4dacaee44cc4b2ded72b6b8eea3f2c3b8641abcfb3e216e5de90`.
- `win-x64` ZIP SHA-256: `2fe20b837b66f1e16b211bc9aab77e827fe4527ae758597dc9508f2f7ecb6bcb`.

Documentation reference check: 64 documents, zero issues. `git diff --check` passed.


### Linux CI and rehearsal preparation - November 4, 2026

Migration changes were committed and pushed to [draft PR #85](https://github.com/glebovski1/MiniPainterHub/pull/85), with all 13 pre-existing unrelated files excluded and byte-for-byte preserved. No merge or production deployment occurred. This supersedes the earlier local-only checkpoint. The PR checks and description own the latest run/commit/artifact links; the evidence below records this note's checkpoint.

- A clean Windows checkout passed a zero-warning build, whole-solution formatting, **513 server tests**, **345 client tests** and the **82.5% server coverage** gate (80% required). The one-test difference from 514 is the unrelated local header test excluded from the PR.
- [Initial Linux run](https://github.com/glebovski1/MiniPainterHub/actions/runs/41922362408) passed build/security, native image processing and repeated unit suites. All three SQL migration/constraint cases passed on Linux SQL Server 2025, compatibility 170 and production collation, with downloaded TRX metadata independently checked. The browser job failed before running tests because `ubuntu-latest` selected Ubuntu 26.04, unsupported by Playwright 1.59.1.
- CI now pins Ubuntu 24.04. The PR browser job supplies disposable SQL Server instead of using the Linux local InMemory fallback. The [corrected run](https://github.com/glebovski1/MiniPainterHub/actions/runs/41923211662) was running at this checkpoint; later outcomes and sealed artifact hashes are recorded on the PR. The initial browser failure was not suppressed or treated as success.
- Live ARM what-if was performed without deployment. The original secure-object union remained unresolved in the direct preview. Materializing that exact compiled union in memory produced only one non-null value difference: candidate `appCommandLine`, from `dotnet LinuxCapacityProbe.dll` to `dotnet MiniPainterHub.Server.dll`. No SQL/media, Windows host, plan or other resource would change. Null/default-field deltas remain provider noise to review at apply time. Sanitized evidence: `tmp/linux-migration-2026-11-02/runtime-whatif-materialized.json`.
- Live reads confirmed candidate **Stopped/F1**, guard **Enabled**, Google and email enabled on production, and Discord not enabled. Retained media lives in **DefaultResourceGroup-EUS**, separately from the app/SQL resource group; preserve that distinction in parameters.
- Placeholder-only settings and a review packet are prepared under `artifacts/linux-migration/rehearsal.settings.template.json` and `rehearsal-readiness.md`. They use the candidate origin, new rehearsal-only secrets and disabled reset/seeding/automatic migrations. They were not applied and are not resource/spend approval.

Remaining gates include native Windows debugger attachment, Production-mode hosted startup/provider flows with isolated dependencies, shared Blob/Key Vault Data Protection implementation and cross-host continuity, recovery proof, and TLS/routing rehearsal. The original November 4 17:00 Eastern spending deadline does not authorize a new rehearsal window. No paid resource operation, candidate start, key transition, production data change or cutover occurred in this continuation.


### Verified CI baseline and shared-provider preparation - November 6, 2026

[CI run 41924053094](https://github.com/glebovski1/MiniPainterHub/actions/runs/41924053094) succeeded for PR head `6c8606c` and tested merge commit `4f1016fcb4f962f59785d5078a925b713177233a`. It passed 510 server unit tests and 345 client tests twice, all three real SQL cases, and **38 SQL-backed browser cases**. Both platform package jobs passed. Downloaded ZIPs and every manifest file hash were verified; both packages declare CI release eligibility. This supersedes the earlier pending Linux checkpoint, but is not deployment approval.

- Linux ZIP SHA-256: `30d72d2b9fcd85d04bb2a5f9f5e58e20cd84579aad833dbee88b45b5f598f4f3`.
- Windows ZIP SHA-256: `a34c6de076b94edc4fb552945b2ba4870eea7d9f23c4ca7108e7eae80e6a3eb3`.
- Verified evidence: `artifacts/linux-migration/ci-6c8606c-verified.json`; downloaded packages/results under `artifacts/linux-migration/ci-6c8606c`.

The next revision adds opt-in shared Blob/Key Vault Data Protection using supported Azure extensions, managed identity for hosted environments and the unchanged `MiniPainterHub` discriminator. Invalid or partial shared settings fail closed. Filesystem behavior remains the default; no live setting or key ring changed. Azure.Storage.Blobs was raised to 12.26.0 to meet the new provider's dependency floor. Local validation passed: 19 focused provider checks, **533 server tests**, **345 client tests**, zero-warning Release build, **82.58% coverage** against 80%, and no known NuGet vulnerabilities. The server count includes the unrelated local header test, which remains outside the PR.

The provider revision requires fresh CI artifacts. It does not yet prove live Azure key wrapping, imported historical keys or bidirectional Windows/Linux continuity. The user requested the quickest and cheapest solution: defer Google test-client setup while completing local/CI preparation. A core-only hosted rehearsal may be considered separately; Google/email parity and continuity still must pass before any cutover. No new paid Azure resource or spending-window extension has been authorized by this continuation.
