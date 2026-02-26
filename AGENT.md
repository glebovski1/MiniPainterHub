## 🧠 Context for AI Agents

This repo includes structured documentation in `/docs` to support Retrieval-Augmented Generation (RAG).

AI agents should:
- Reference `/docs/ARCHITECTURE.md` to understand layered structure
- Use `/docs/CODE_STYLE.md` to follow code conventions
- Avoid practices listed in `/docs/ANTI_PATTERNS.md`
- Always use the service layer (`Services/`) when writing or modifying controller logic
- Access EF Core via `HobbyCenterContext` in `Data/`

## 🔐 Authentication Handling

AI agents should verify that `app.UseAuthentication()` is present and not modify its position in middleware order.

## 🚫 Avoid

- Creating `.Result` on async calls
- Writing logic directly in controllers
- Introducing raw SQL unless specified

## ✅ Preferred Patterns

- `async`/`await` everywhere
- Use dependency injection
- All business logic in `Services/`

## 🧪 Test Expectations

No test suite yet. AI agents should validate code with `dotnet build` and avoid introducing runtime errors.

## 🎨 UI Workflow (Required)

When making any UI change (Razor/CSS/JS in `MiniPainterHub.WebApp`):
- Start the app if needed, then visually verify changes in a browser.
- Use the automated snapshot script to sanity-check desktop + mobile layouts:
  - Run `.\.venv\Scripts\python tools\ui_snapshot_panel.py`
  - Review screenshots in `artifacts/ui-panel-check\` before concluding the change is acceptable.
- Perform a UI smoke test on impacted pages and confirm every visible button still works as expected.

## 📘 How to Use `/docs` for RAG and AI Agents

This repository includes a structured RAG-style documentation folder at `/docs` for AI and developers.

Each document is purpose-specific:

- `CODE_STYLE.md`: C# and ASP.NET naming conventions, formatting rules, async practices.
- `ARCHITECTURE.md`: Overview of system layers, data flow, EF Core models, and auth strategy.
- `BEST_PRACTICES.md`: Preferred patterns for validation, DI, service usage, and async code.
- `ANTI_PATTERNS.md`: Known issues and what to avoid (.Result, fat controllers, swallowed exceptions).
- `CONTRIBUTING.md`: Contributor setup and etiquette.

### Agent Guidelines

- Use full path lookups (e.g. `docs/ARCHITECTURE.md`) for semantic search or embedding.
- When answering questions about project rules or design, retrieve from the most relevant file.
- Prioritize recent `/docs/` content over older inline comments or out-of-date READMEs.
- Do not generate new styles or patterns not aligned with `CODE_STYLE.md` or `BEST_PRACTICES.md`.

### Use Case Mapping (for retrieval)

| Question Type                                 | File to Consult             |
|----------------------------------------------|-----------------------------|
| How should I name a service method?           | `CODE_STYLE.md`             |
| What does ImageService do internally?         | `ARCHITECTURE.md`           |
| Can I use `.Result` in controller logic?      | `ANTI_PATTERNS.md`          |
| Where does validation go — controller or svc? | `BEST_PRACTICES.md`         |
| How do I contribute or open a PR?             | `CONTRIBUTING.md`           |
