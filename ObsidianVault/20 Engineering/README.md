# Engineering Index

Purpose: route MiniPainterHub engineering memory without requiring a folder scan.

When to read: architecture, layering, persistence, contracts, deployment, testing strategy, operational hardening, or engineering follow-up work.

Update triggers: source-of-truth architecture changes, deployment process changes, validation command changes, or newly verified engineering risks.

## Current Source Of Truth

- [Architecture](<ARCHITECTURE.md>) - system shape, service boundaries, persistence, API surface, client architecture, and preferred validation commands.
- [Code style](<CODE_STYLE.md>) - local C# and repo conventions.
- [Deployment](<DEPLOYMENT.md>) - Azure/App Service publishing, production settings, and deployment diagnostics.
- [Best practices](<../30 Process/BEST_PRACTICES.md>) - cross-cutting engineering guidance.
- [Anti-patterns](<../30 Process/ANTI_PATTERNS.md>) - known pitfalls to avoid.

## Tests And Quality Gates

- Use [Architecture](<ARCHITECTURE.md>) for the current test project map and preferred validation commands.
- Use [Workflow playbook](<../30 Process/WORKFLOW_PLAYBOOK.md>) for task-level validation expectations.
- Use [UI quality playbook](<../30 Process/UI_QUALITY_PLAYBOOK.md>) for UI/browser screenshot review.

## Historical Audits And Follow-Ups

Historical audit notes are archived when their findings become mixed with completed work. Treat archived audit findings as prompts to verify against current code, not as source-of-truth facts.

Durable follow-up themes extracted from older audits:

- Token storage posture and session invalidation strategy.
- Production secret-store ownership, rotation process, and deployment setting validation.
- Runtime observability for auth, startup, and image/media operations.
- Image pipeline telemetry for processing duration, variant sizes, and storage keys.
- Comment validation and other user-input guardrails, verified against current service behavior before work begins.
- Startup-path simplification only if current code still shows redundant environment branching.

Archived context:

- [Deep project audit](<../_archive/deleted/2026-04-19/20 Engineering/DEEP_AUDIT.md>)
- [Blank testing gap placeholder](<../_archive/deleted/2026-04-19/20 Engineering/TESTING_GAP_ANALYSIS.md>)
