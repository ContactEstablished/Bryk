# CLAUDE.md — Bryk Project

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly in Claude Code. Read this file at the start of every session.

> Regenerated 2026-06-07 via `/dotnet-init`, reconciled against the actual codebase. The prior hand-maintained version is preserved in git history at `6fdafbe`.

---

## Universal principles

These four principles bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think before coding

Don't assume. Don't hide confusion. Surface tradeoffs.

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

The "design walkthrough" rhythm — name three options, weigh tradeoffs, present a recommendation, ask the user to confirm — is the working manifestation of this principle for non-trivial decisions.

### 2. Simplicity first

Minimum code that solves the problem. Nothing speculative.

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: would a senior engineer say this is overcomplicated? If yes, simplify.

Resist the urge to bundle "while we're here, let's also fix..." into a single change. One logical change at a time.

### 3. Surgical changes

Touch only what you must. Clean up only your own mess.

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code or smells, mention them — don't delete or fix them. They go on the tech debt list.

When your changes create orphans:

- Remove imports, variables, functions that *your* changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: every changed line should trace directly to the user's request.

When making a change, be explicit about what NOT to modify. The "Do not modify anything else" scoping discipline from earlier phases worked well; preserve that pattern.

### 4. Goal-driven execution

Define success criteria before starting. State a brief plan for multi-step tasks.

Transform tasks into verifiable goals:

- "Add validation" → "validation runs at top of each Submit method, throws `Bryk.Application.Exceptions.ValidationException` on failure, build is green, the new validator has a unit test."
- "Wire API versioning" → "controllers carry `[ApiVersion]` attribute, unversioned requests return 400, `api-supported-versions` header appears on success."
- "Refactor X" → "ensure existing endpoints still respond at original URLs; existing tests stay green."

For multi-step tasks, state a brief plan:

```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

**Note on testing.** Test infrastructure now exists on both layers (see "Testing" below). Strong success criteria include passing tests. "Verify" means: build is green, the relevant `dotnet test` / `pnpm test` suites pass, manual smoke test passes for the affected endpoint or component, and the diff reads cleanly. New behavior should land with a test; bug fixes should land with a regression test.

### 5. Verify what you read

You have direct file access. Use it. Don't take user recall — or this document — at face value when the underlying file is one tool call away.

- Before implementing, read the actual file rather than guessing its shape.
- Before claiming a package or middleware is wired, grep for it (the Roslyn MCP tools are faster than text search for symbols — see "Tooling").
- Before reviewing a change, check `git diff` directly.
- Before suggesting a commit, run `dotnet build` / `pnpm run build` and confirm green.

Repo state claims that turn out to be wrong are expensive — they lead to wrong assumptions and work that has to be redone. Verification is cheap; assumption is not. This document drifts from the code over time; when they disagree, the code wins — fix the doc.

---

## Your role

You are the architect and the implementer: you design the work, write the code, and validate it — all directly in Claude Code.

This does not collapse the design discipline into "just start typing." For non-trivial work — anything in "When to slow down" or "Open decisions" — lead with a design walkthrough (name the options, weigh tradeoffs, recommend, ask the user to confirm) before writing code. For trivial mechanical edits — single-line config, namespace fixes, comment corrections — just make the change. Reading existing code for validation or design is always done directly.

One logical change at a time. Resist bundling unrelated fixes into one change, and keep each change tightly scoped to the request (see "Surgical changes").

---

## Working rhythm

- One logical change at a time unless tasks are trivially small and tightly related.
- Explain the **why** briefly before each change, especially with tradeoffs. Concise.
- Verify a clean working tree (`git status`) before modifying existing code.
- After each change: read the diff yourself, confirm the build is green and affected tests pass, then surface a suggested commit message for the user to review and commit.
- Conventional commit prefixes: `feat:`, `refactor:`, `docs:`, `fix:`, `chore:`.
- One logical change per commit.

### When to slow down

Some changes warrant an explicit design walkthrough before you write code:

- Anything touching cross-cutting concerns (auth, middleware, versioning, transactions).
- Anything in the Open Decisions section below — drive to closure first.
- Anything that adds a NuGet package, npm package, or new project reference.
- Anything that modifies the persistence boundary or repository contracts.
- Migrations.

For these: name the options, weigh tradeoffs, present a recommendation, ask the user to confirm. Don't lead with code.

### When to ask for Sr. Dev approval before proceeding

- DbContext or data model changes that would generate a migration. Review the migration before applying.
- New NuGet or npm packages (first-party `Microsoft.Extensions.*` plumbing is exempt — proceed without flagging).
- API breaking changes — modified routes, removed fields, changed response shapes.
- Cross-cutting concerns — authentication, authorization, caching, logging middleware, transaction handling.
- Changes to the persistence boundary — adding methods to `IUnitOfWork`, introducing transaction wrappers, modifying the repository contract pattern.
- Introducing Dapper for any query (it is **not** currently a dependency — see Tech stack).
- Any deviation from conventions in this document.

Already approved: `IAuditable` + `AuditableEntityInterceptor`, `IUnitOfWork` + `UnitOfWork`, `ICurrentUserService` dev stub, FluentValidation reuse, API versioning configuration, the test stack (xUnit + FluentAssertions + `Mvc.Testing`; Vitest + `@vue/test-utils`), and the Vue UI stack (Tailwind v4 + shadcn-vue).

---

## Tech stack

### Backend (`api/`, solution `api/Bryk.sln`)

- **.NET 10**, controller-based Web API (`Microsoft.NET.Sdk.Web`).
- **EF Core 10** (`Microsoft.EntityFrameworkCore` + `.SqlServer` + `.Design`/`.Tools`), **SQL Server**. Dapper is **not referenced** — EF Core is the only data-access path today; adding Dapper for a complex query requires Sr. Dev approval.
- **FluentValidation 11.11** (`FluentValidation.DependencyInjectionExtensions`, in `Bryk.Application`).
- **Asp.Versioning.Mvc 10** (+ `.ApiExplorer`) for API versioning.
- **Swashbuckle.AspNetCore 7.2** for OpenAPI, **Scalar.AspNetCore 1.2** for the docs UI.
- Connection string `DefaultConnection` (SQL Server; Windows auth in dev). User-secrets hold the local `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId`.

### Frontend (`ui/`, pnpm)

- **Vue 3.5** + Composition API + **TypeScript 6**, **Vite 8**, **Pinia 3**, **Vue Router 4**.
- **Styling: Tailwind CSS v4** (`@tailwindcss/postcss`) + **shadcn-vue** components on **reka-ui** primitives; **lucide-vue-next** icons; `class-variance-authority` + `clsx` + `tailwind-merge` for variant composition; `tw-animate-css`.
- **Forms/validation: vee-validate + `@vee-validate/zod` + zod**; `@vueuse/core` for composable utilities.
- Package manager is **pnpm** (`pnpm@10`). Scripts: `pnpm dev`, `pnpm run build` (`vue-tsc -b && vite build`), `pnpm test` (`vitest run`).

### Testing

- **.NET:** xUnit v2 (`2.9.3`) + **FluentAssertions 6.12**.
  - `Bryk.API.Tests` — integration tests via `Microsoft.AspNetCore.Mvc.Testing` + `Microsoft.EntityFrameworkCore.InMemory`.
  - `Bryk.Application.Tests` — unit tests for services/validators.
  - Run: `dotnet test api/Bryk.sln`.
- **Vue:** **Vitest 4** + `@vue/test-utils` + `@pinia/testing` + `@vitest/coverage-v8`, jsdom environment. Run: `pnpm test` from `ui/`.

### API design

RESTful, versioned (URL segment `api/v{version}` primary, header secondary, strict mode), OpenAPI via Swashbuckle, Scalar for docs UI.

---

## Architecture

Clean Architecture, four projects (under `api/`) plus two test projects:

- `Bryk.Domain` — entities, enums, domain interfaces (including `IUnitOfWork` and repository contracts). No external dependencies, no EF Core types, no framework references in doc comments.
- `Bryk.Application` — DTOs, services, validators, application-layer interfaces (e.g., `ICurrentUserService`). References Domain. Holds FluentValidation.
- `Bryk.Infrastructure` — EF Core, repositories, `UnitOfWork`, `ApplicationDbContext`, interceptors, migrations, and external service implementations (e.g., `CurrentUserService` dev stub). References Domain + Application.
- `Bryk.API` — controllers, middleware, DI composition root, `Program.cs`. References Infrastructure.
- `Bryk.Application.Tests`, `Bryk.API.Tests` — see Testing.

Dependency direction: API → Application → Domain. Infrastructure → Application → Domain. No reverse references.

The Vue SPA lives in `ui/` and talks to the API over HTTP.

## .NET conventions

- Primary constructor syntax for services and repos.
- `IConfiguration["KeyName"]` with null guard. Connection strings via `IConfiguration.GetConnectionString(...)`. Never `Environment.GetEnvironmentVariable`. Never hardcode connection strings.
- No hardcoded IDs or magic numbers — config or constants.
- Async naming: methods returning `Task` or `Task<T>` end with `Async`.
- Entity IDs are `Guid`.
- Audit fields handled globally by `AuditableEntityInterceptor`. Never set manually.
- Repository pattern is mandatory. No DbContext access outside repositories. Services consume repos; controllers consume services.
- `IUnitOfWork` owns the persistence boundary. Repos stage; services commit once via `_unitOfWork.SaveChangesAsync()`.
- EF Core default. Explicit `.Include()`, no lazy loading. `.AsNoTracking()` for display reads. `.AsSplitQuery()` for multiple includes.
- Migrations are code-first. Generate, review, get Sr. Dev approval before applying.
- Thin controllers. `IActionResult` returns. `[ApiController]` + `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}/[controller]")]`. XML `<summary>` on every endpoint.
- Validation lives in services, not controllers. Use the `ValidateOrThrowAsync` extension (in `Bryk.Application.Common.Validation`): `await validator.ValidateOrThrowAsync(request, ct)`, which throws `Bryk.Application.Exceptions.ValidationException` on failure. Do not use FluentValidation's built-in `ValidateAndThrowAsync` — it throws `FluentValidation.ValidationException`, which the global middleware doesn't handle.
- Global exception middleware handles errors. No try/catch in controllers.
- DTO naming: `XxxRequest` for inbound, `XxxResponse` for outbound, `XxxDto` for shared/nested shapes. (`XxxResponse` may extend `XxxDto` to add an `Id` — see the profile endpoints.)

## Vue conventions

- Composition API only. `<script setup lang="ts">` in all SFCs.
- TypeScript throughout. Props via `defineProps<{...}>()`, emits via `defineEmits<{...}>()`.
- Components: one per file, PascalCase filename.
- Composables in `src/composables/`, prefixed `use`.
- API calls go through `src/services/`. Never `fetch` or `axios` directly from a component.
- State: Pinia only, stores in `src/stores/`, one per domain concept. No Vuex.
- Routing: Vue Router 4, typed routes in `src/router/index.ts`, lazy-load route-level components.
- Forms: vee-validate + zod schemas; reuse exported per-row schemas where they exist (e.g. `eventItemSchema`/`goalItemSchema`).
- UI: prefer existing shadcn-vue components; Tailwind utility classes for layout; compose variants with `cva`/`tailwind-merge`.

---

## Validation philosophy

You have direct file access. After each change you make:

1. Read the diff (`git diff` or read modified files directly).
2. Verify build is green (`dotnet build` from `api/`, `pnpm run build` from `ui/`).
3. Run the affected tests (`dotnet test`, `pnpm test`).
4. Spot-check for: subtle logic errors, convention drift, redundant code, forgotten error paths, scope creep beyond the request.
5. If something looks wrong, fix it before suggesting commit.
6. Otherwise, suggest a commit message for the user to review and commit.

For high-risk changes (cross-cutting concerns, migrations, anything in Sr. Dev approval list), explicitly call out what you read and what you verified. Don't silently accept.

The `dotnet-claude-kit:verify` skill runs a 7-phase pipeline (build, analyzers, antipatterns, tests, security, formatting, diff) when you want a comprehensive pre-PR gate.

---

## Open decisions

Only genuinely-open questions live here. Settled decisions are ADRs in `/md/decisions/` (indexed under Project state pointers) — don't re-summarize them here.

### Authentication & Authorization — deferred to Phase 12

No `[Authorize]`, Identity, or `AddAuthentication` exists in the codebase yet — keep it that way until Phase 12. Current stub: `ICurrentUserService` (`Bryk.Application/Common/`) with a dev implementation (`Bryk.Infrastructure/Services/CurrentUserService.cs`) that reads `DevAuth:CurrentAthleteId` from Development config / user-secrets and throws outside Development. When real auth lands, the implementation swaps to read from `ClaimsPrincipal`; consumers don't change.

Direction: custom email+password signup plus Google/Apple OAuth, with Bryk owning the user store. Evaluate ASP.NET Core Identity before hand-rolling — it provides password hashing, token generation, lockout, and external-login plumbing without ceding identity ownership. The Phase 12 auth ADR picks the table layout (Identity in its own table linked 1:1 to `Athlete`, vs `Athlete : IdentityUser<Guid>`). **Approval required before any production auth code** (`[Authorize]`, claims logic, password hashing, token issuance).

---

## Tech debt (working list, not blocking)

Verified against the codebase 2026-06-07. Three prior items are resolved and dropped: `OperationCanceledException` now maps to 499 (`ExceptionHandlingMiddleware`); the validator anchor is the named `ApplicationAssemblyMarker`; the 3-line validation pattern is the `ValidateOrThrowAsync` extension, adopted by the services. Remaining, roughly by impact:

1. **One design-time NuGet vulnerability remains:** `System.Security.Cryptography.Xml` 9.0.0 (**High**) in `Bryk.Infrastructure`, pulled transitively by `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all` — design-time only, does **not** ship in the published app; required for migrations). Accept until EF Core servicing bumps it; do **not** add a direct/shipping reference just to satisfy the auditor. (The prior `Microsoft.Build`/`NuGet.*` High/Low warnings on `Bryk.API`/`Bryk.API.Tests` were cleared by removing the unused `Microsoft.VisualStudio.Web.CodeGeneration.Design` scaffolding package — 2026-06-07.)
2. `DbUpdateException` and concurrency exceptions fall through to a generic 500 with no diagnostics (no case in the `ExceptionHandlingMiddleware` switch). Add specific handlers — at minimum, unique-constraint → 409.
3. `NotImplementedException` falls through to a generic 500 (no switch case). Should map to 501.
4. Custom JSON error shape (`{ status, error, traceId }`) instead of RFC 9457 ProblemDetails. Lower priority unless the API gets external consumers.
5. Single `SwaggerDoc` hardcoded as `"v1"` in `Program.cs` (TODO comment in place). Iterate over `IApiVersionDescriptionProvider` when v2 ships.
6. Test coverage exists but is **partial**, not comprehensive. Newer surfaces (TrainingPlan/zones, profile editing) are better covered than older ones; keep raising coverage as you touch code.
7. CI hook running both test suites on commit not yet confirmed present.

---

## Tooling (dotnet-claude-kit)

This project has the **dotnet-claude-kit** plugin active. Use it rather than reinventing:

- **Roslyn MCP navigator** (`mcp__plugin_dotnet-claude-kit_cwm-roslyn-navigator__*`) — prefer for symbol-level work over text search: `find_symbol`, `find_references`, `find_implementations`, `find_callers`, `get_diagnostics`, `detect_antipatterns`, `get_type_hierarchy`, `detect_circular_dependencies`, `find_dead_code`. Faster and more precise than grep for C#.
- **Skills** worth knowing: `dotnet-claude-kit:verify` (7-phase pre-PR gate), `:health-check` (A–F report card), `:security-scan`, `:migrate` (guided EF migration), `:scaffold` (architecture-aware feature slices), `:code-review`, `:build-fix`. Invoke via the matching `/`-command when the task fits.
- The Roslyn server is wired through the plugin (no root `.mcp.json` required).

---

## Project state pointers

- Current phase: **Phase 13 complete** (Workout history & plan browser — workout edit/delete + load recompute, filtered/paged `GET /workouts`, `WorkoutsView`/`WorkoutDetailView`/plan browser; no migration; see `md/handoffs/2026-06-11-phase-13-complete.md`). Next feature phase: **Phase 14** — Daily-load history & PMC engine (needs the PMC computation-strategy ADR first). **Phase 12** — Authentication & Authorization — remains deferred and **approval-gated** (see Open decisions). Phases 8–11 are complete.
- ADRs (`/md/decisions/`) — read before touching the training/zone domain:
  - **0001** — Mesocycle superseded by TrainingPlan / PlannedWorkout / Workout (Accepted; retirement migration `DropMesocycleSurface` committed).
  - **0002** — Coaches are v2; v1 is athlete-only, one human = one `Athlete` (Accepted).
  - **0003** — TrainingPlan / PlannedWorkout / Workout field shapes (Accepted).
  - **0004** — Structured-workout payload + training-zone model (Accepted).
  - **0005** — Training-load engine + executed-workout capture (Accepted; HR §1=a, strength §2=c).
- `/md/product/feature-parity-trainingpeaks.md` — feature wishlist and status.
- `/md/Tasks-<phase>-<n>.md` — per-task specs (Phase 10: `Tasks-10-1.md` … `Tasks-10-5.md`).
- `/md/handoffs/` — session-end handoff documents. Most recent: `2026-06-08-phase-11-complete.md`.
- `git log --oneline -20` for recent commit history.

On session start: read the latest handoff (or ask for one) and skim the relevant Tasks doc / ADR before starting work. Confirm clean working tree and green build (`dotnet build` + `pnpm run build`) before proposing the first task.
