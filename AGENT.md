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
