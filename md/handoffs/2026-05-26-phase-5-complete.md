# HANDOFF — Bryk Project, Phase 5 complete

**Date:** 2026-05-26
**Phase:** 5 — Vue onboarding wizard (Required / Recommended / Goals)
**Status flip:** 🟡 → ✅

## Context

Phase 5 was the first Vue surface in the Bryk project — a three-step onboarding wizard driving the Phase 4 onboarding API end-to-end. Originally scoped through commits `02846e8` … `bcdc496` (the four `Phase 5 task N` commits on branch `phase-5-vue-onboarding-wizard`, merged in `b44622d`), with substantial pre-scope scaffolding work in earlier commits (Vite bootstrap `c7f5661`, wizard shell `44ea8c9`, Recommended form `94f0010`, GoalsStep `7d409f6`).

Phase 5 was functionally complete as of the PR merge, but the formal closeout (smoke verification + handoff document + ROADMAP flip) didn't happen until 2026-05-26. The intervening time produced a band-aid fix and a dashboard shell as inter-phase additions — captured below.

## Final shape of the wizard

The wizard lives at `/onboarding` and is implemented in:

- **View:** `ui/src/views/OnboardingView.vue` — orchestrates the stepper, calls `store.loadStatus()` on mount, lands the athlete on `store.nextIncompleteStep`, locks future steps that can't be reached yet.
- **Steps:** `ui/src/components/onboarding/RequiredStep.vue`, `RecommendedStep.vue`, `GoalsStep.vue` — each a `<script setup lang="ts">` component using `useForm` + `toTypedSchema` (vee-validate + zod). Required has unit-system toggle (metric/imperial) with conversion. Recommended has a per-sport thresholds field array (bike / run / swim, with Strength deferred to Phase 9 per ADR-0001). Goals has Event and Goal field arrays with triathlon-distance and custom-distance support.
- **Store:** `ui/src/stores/onboarding.ts` (Pinia) — holds status, exposes `requiredComplete` / `recommendedComplete` / `goalsComplete` computeds + `nextIncompleteStep`. Submits via thin service.
- **Service:** `ui/src/services/onboarding.ts` — typed methods over the four Phase 4 endpoints (`GET /status`, `POST /required`, `POST /recommended`, `POST /goals`).
- **Schemas:** `ui/src/schemas/onboarding.ts` — zod schemas mirroring the Phase 4 FluentValidation rules. Includes age ≥13 check on Required, future-date checks on Goals events, max-length checks on notes/descriptions.
- **Server-error mapping:** `ui/src/services/apiErrors.ts` — translates the API's custom JSON validation response into vee-validate field-level errors per step. Conservative string-message mapping; works in practice but brittle. RFC 7807 ProblemDetails (tech debt item 9) parked.

The wizard supports resume — landing on the correct step based on status flags after a refresh — and gates locked future steps in the stepper with a lock icon and `aria-disabled`.

## Inter-phase work that landed during closeout

Two additions shipped after the Phase 5 PR merge but before the formal closeout. Both relate to Phase 5's surface but were technically out of its original scope:

- **Read-only summary cards on completed onboarding steps** (`a774e09 feat: show read-only summary card on completed onboarding steps`). When an athlete revisits a completed step via the stepper, an inert "saved" card renders instead of the editable form — eliminating the footgun where re-submitting an empty form would overwrite values via the existing upsert semantics. This is a **band-aid** that Phase 8 will retire by adding a proper edit-my-profile surface with GET endpoints + prefilled forms.
- **Dashboard shell** (`3689327 feat: add dashboard shell to home view after onboarding`). When status flags are all true, `HomeView` renders a sidebar + main-grid dashboard layout instead of the "Welcome back" dead-end. Two new components — `ui/src/components/dashboard/DashboardSidebar.vue` (Bryk + PRO badge, TRAIN / ACCOUNT sections, all nav items except Dashboard visibly inert) and `PlaceholderCard.vue` (reusable empty card). Top-row stat cards (Weekly Load, Resting HR, Sleep Avg, Form/TSB), middle row (This Week wide + Primary Goal narrow), bottom row (Recent Activity). Every card names which phase will populate it. Phase 8 lights up the first two (Primary Goal, Resting HR); Phases 9 / 11 / 13 own the rest.

Both additions landed before the formal closeout and are tracked here so they aren't mistaken for un-shipped work.

## Phase 5 commit history (Phase 5 PR + closeout additions)

```
99d86f6  docs: reorganize all process docs into md/ + reshape Phase 7+
66812fb  Adding documentation (ADRs 0001 + 0002)
3689327  feat: add dashboard shell to home view after onboarding
a774e09  feat: show read-only summary card on completed onboarding steps
b44622d  Merge pull request #2 from ContactEstablished/phase-5-vue-onboarding-wizard
 ├ bcdc496  Phase 5 task 4: add verification handoff
 ├ b464ce5  Phase 5 task 3: align onboarding schemas
 ├ 98040dd  Phase 5 task 2: gate wizard navigation
 ├ 74b7ce7  Phase 5 task 1: map server validation errors
 └ 02846e8  Phase 5: define tasks
```

Earlier Vue/wizard scaffolding (pre-scope, included for completeness):
`c7f5661` (Vite + Tailwind v4 + shadcn-vue bootstrap), `cdbc3c4` (UI starting), `96a8169` (onboarding service + store), `44ea8c9` (wizard shell + Reka stepper), `f03aaa0` (unit toggle + methodology note), `94f0010` (Recommended form), `7d409f6` / `4cb3c2a` (GoalsStep), plus targeted backend fixes (`a9f661f`, `3cf6a2b`, `2d3a74c`, `d4bff8c`).

## Manual smoke matrix walked 2026-05-26

All walked with `DevAuth:CurrentAthleteId` set to a fresh GUID via `appsettings.Development.json` (Task 7-5 will move this to user-secrets).

| # | Scenario | Result |
|---|---|---|
| 1 | Fresh GUID → visit `/` → "Get started" button appears → click → land on `/onboarding` Required step | ✅ |
| 2 | Complete Required step → land on Recommended step; Required circle shows green check in stepper | ✅ |
| 3 | After completing only Required, close browser, reopen at `/onboarding` → resume on Recommended step automatically | ✅ |
| 4 | Click Required circle in stepper while on Recommended → see "Identity saved" summary card (band-aid), no empty form footgun | ✅ |
| 5 | Required form rejects future date-of-birth with "must be 13+" zod message | ✅ (client validation; server validation surfacing matrix not separately exercised) |
| 6 | Complete Recommended → land on Goals → complete Goals → "All set" view appears with "Go to home" button | ✅ |
| 7 | Click "Go to home" → land on `/` → see dashboard shell with sidebar and placeholder cards (no "Welcome back" dead-end) | ✅ |
| 8 | Refresh `/onboarding` while all flags true → all three step circles show green check, panel shows the "done" state | ✅ |
| 9 | Triathlon Custom distance on Goals event submits `customDistanceName` correctly (verified via API payload) | ✅ |

## Test status (as of 2026-05-26)

- **Backend.** `dotnet test api/Bryk.sln` green. `Bryk.Application.Tests` and `Bryk.API.Tests` projects landed in Phase 6 (`66e1679`). `OnboardingServiceTests` covers happy-path validation behavior; `OnboardingControllerTests` exercises the four endpoints via `WebApplicationFactory<Program>`.
- **Frontend.** `pnpm test` from `ui/` green — 14 tests across 5 files (`useOnboardingStore`, `onboarding` service, three step components). Vitest infrastructure landed in Phase 6 (`9d7aeda`).
- **CI.** `.github/workflows/ci.yml` landed in Phase 6 (`eb38c4d`). Definition committed; green/red on actual PRs not yet observed in this session.
- **Build.** `pnpm run build` green; bundle size around 181 kB (index) + 219 kB (OnboardingView lazy chunk).

## Known follow-ups owned by Phase 8

Phase 8 is "Profile editing + dashboard warmup cards" per the post-2026-05-26 ROADMAP reshape. Specifically:

- **Add `GET /api/v1/profile/required`, `/profile/recommended`, `/profile/goals` endpoints** reading existing Athlete / SportProfile / Goal / Event data.
- **Build a `/profile` Vue view** with three sections initialized from the GETs and submitting via the existing onboarding POSTs (upsert / append semantics already match).
- **Retire the summary-card band-aid.** Delete the `v-if="store.<flag>Complete"` branches in the three onboarding step components and relax the completed-step lock on the stepper. Add a "Manage your profile" link from the onboarding "All set" view to `/profile`.
- **Wire the Primary Goal dashboard card** to existing Event data (highest-priority event by `EventPriority` then date, with name / sport / date / weeks-to-go).
- **Wire the Resting HR dashboard card** to existing `Athlete.RestingHr`. Empty-state links to `/profile`.

Detailed acceptance criteria belong in `md/Tasks-8-N.md` files written at the start of Phase 8.

## Known follow-ups deferred

- **Server-validation surfacing matrix not exhaustively exercised.** The smoke confirmed client zod errors render. Whether every server-side FluentValidation failure maps to a field-level error (vs falling back to the global banner) was not walked end-to-end. Address opportunistically; promote to a task if any specific gap is found.
- **RFC 7807 ProblemDetails** (CLAUDE.md tech debt item 9). The current custom JSON error shape works with the conservative server-error mapper in `apiErrors.ts`. Deferred to Phase 17 unless an external API consumer changes the calculus.
- **`OnboardingStatusResponse` echoing data.** Phase 4 deliberately scoped the status endpoint to flags only ("No echoed data" per Phase 4 handoff). Phase 8 adds dedicated GET endpoints instead of extending status — the right separation of concerns.

## Pending decisions (carried forward to relevant phases)

- **Authentication.** Deferred to Phase 14 (renumbered from old Phase 12). ASP.NET Core Identity recommended for evaluation. Auth-table layout decision (Identity in its own table vs `Athlete : IdentityUser<Guid>`) belongs to the Phase 14 auth ADR; both satisfy the conceptual constraint locked in ADR-0002 (one human = one Athlete).
- **Vue styling library.** Resolved: Tailwind 4 + shadcn-vue + reka-ui + lucide-vue-next. Already in active use.
- **Testing infrastructure.** Resolved in Phase 6 (xUnit + WebApplicationFactory backend, Vitest + jsdom frontend, GitHub Actions CI).
- **Mesocycle vs TrainingPlan.** Resolved 2026-05-26 — see `md/decisions/0001-mesocycle-vs-trainingplan.md`. Mesocycle superseded. Task 7-4 enacts the deletion.
- **Coaches as first-class.** Resolved 2026-05-26 — see `md/decisions/0002-coaches-as-first-class.md`. v2. v1 ships athlete-only; one human = one `Athlete`.

## What Phase 7 should do next

Phase 7 closeout has three remaining tasks after this handoff lands:

- **Task 7-5: Secrets hygiene.** Migrate `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId` from `appsettings.Development.json` to `dotnet user-secrets`. Delete orphan `api/appsettings.development.json`. Document per-developer setup in README. See `md/Tasks-7-5.md`.
- **Task 7-4: Tech-debt sweep.** Delete Mesocycle surface per ADR-0001 (five entities, service, four controllers, `MesocycleValidators`). Add `ValidateOrThrowAsync` extension; adopt in `OnboardingService`. Generate drop-table migration — **Sr. Dev approval required before apply.** See `md/Tasks-7-4.md`.

After Phase 7 closes, Phase 8 opens with the profile + dashboard-warmup work outlined above.

## What the next session should do first

1. Read this handoff plus `ROADMAP.md` and the latest `md/decisions/*` ADRs.
2. Run `git status` and `git log --oneline -10` — confirm clean state.
3. Run `dotnet build api/Bryk.sln` and `pnpm test` from `ui/` — confirm green.
4. Pick the next Phase 7 task (Task 7-5 recommended — independent of 7-4 and faster).
