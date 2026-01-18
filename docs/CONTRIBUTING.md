# Contributing to HobbyCenter (CONTRIBUTING.md)

Thanks for contributing to HobbyCenter! This guide explains how to make changes in a consistent, reviewable way and how to follow the project’s conventions.

This repo includes an in-repo RAG documentation set under `/docs`. These docs are the source of truth for style and architecture decisions.

---

## 1) Key Docs You Must Follow

Before making changes, read these:

* `docs/CODE_STYLE.md` — naming, formatting, async rules
* `docs/ARCHITECTURE.md` — system structure (controllers → services → EF Core)
* `docs/BEST_PRACTICES.md` — recommended patterns and workflows
* `docs/ANTI_PATTERNS.md` — what NOT to do (common pitfalls)

If code conflicts with docs, prefer the docs and refactor toward them.

---

## 2) How to Contribute (Workflow)

### A) Create a Branch (Recommended)

If you have Git locally:

* `feature/<short-description>` for new features
* `fix/<short-description>` for bug fixes
* `refactor/<short-description>` for refactoring/cleanup

Example:

* `feature/image-tags`
* `fix/rating-average-zero`
* `refactor/remove-result-blocking`

### B) Keep Commits Focused

Make small commits that are easy to review:

✅ Good:

* “Fix rating average when no ratings”
* “Refactor ImageService to async/await”

❌ Avoid:

* “Big update”
* “Fixes and refactor and UI changes” all in one

---

## 3) Project Expectations

### Controllers must be thin

Controllers should:

* validate model binding (`ModelState`)
* extract user info from claims
* call services
* return a view/redirect/JSON result

Controllers should not:

* contain business logic
* query EF Core directly (except trivial read-only cases)
* block async calls

### Services own business logic

Services should:

* perform DB operations using EF Core
* enforce business rules (ownership, uniqueness)
* avoid duplicated logic between MVC + API

Services should not:

* depend on MVC (`IActionResult`, `ViewBag`, etc.)
* use `HttpContext` directly

---

## 4) Async Rules (Non-Negotiable)

Never use:

* `.Result`
* `.Wait()`

Always:

* `async/await`
* use EF Core async methods (`ToListAsync`, `SaveChangesAsync`, etc.)

See: `docs/ANTI_PATTERNS.md` and `docs/CODE_STYLE.md`.

---

## 5) Validation and Error Handling

### Validation

* Controller validates request shape via model binding and `ModelState.IsValid`
* Service validates business rules (ownership, entity existence, rating range, etc.)

### Error Handling

* Do not swallow exceptions.
* Log unexpected exceptions using `ILogger<T>`.
* Return consistent failure responses or throw meaningful exceptions.

---

## 6) Authentication & Authorization

If you modify auth or middleware, verify:

* Middleware order is correct in `Program.cs`:

  * `UseRouting()`
  * `UseAuthentication()`
  * `UseAuthorization()`

Any data-modifying endpoint must:

* use `[Authorize]`
* also validate ownership/permissions in the service layer

Do not rely on UI-only security.

---

## 7) Testing / Verification

This repo may not have a full automated test suite yet. Minimum expectations:

* Build succeeds: `dotnet build`
* App runs: `dotnet run`
* Manually test:

  * login/logout
  * upload image
  * delete image (ownership enforced)
  * add comment
  * add/update rating
  * rating average correct when no ratings

If you add a risky change (auth, DB schema, core workflows), include extra verification steps in your PR message/commit notes.

---

## 8) Documentation Updates (Required)

If you change:

* architecture (layers, data model, flow) → update `docs/ARCHITECTURE.md`
* coding conventions → update `docs/CODE_STYLE.md`
* recommended patterns/workflows → update `docs/BEST_PRACTICES.md`
* new pitfalls discovered → update `docs/ANTI_PATTERNS.md`

Docs are part of the deliverable.

---

## 9) Pull Request Checklist

Before submitting:

* [ ] No `.Result` / `.Wait()` added anywhere
* [ ] Controllers remain thin
* [ ] Business rules live in services
* [ ] EF Core calls are async
* [ ] No N+1 query loops introduced
* [ ] Validation is consistent (controller + service)
* [ ] Ownership/security enforced server-side
* [ ] Errors logged (when unexpected)
* [ ] Docs updated if patterns/architecture changed
* [ ] App builds and basic flows tested

---

## 10) Suggested PR Description Template

Use this template when describing your change:

**What changed**

* …

**Why**

* …

**How to test**

* …

**Notes / follow-ups**

* …

---

Thank you for helping keep HobbyCenter clean, consistent, and easy to extend!
