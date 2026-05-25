# Task 6-6 — Decisions ADRs (Mesocycle/TrainingPlan, Coaches) + Phase 6 handoff

## Goal
Close Phase 6 by recording the two outstanding decisions as ADRs, formalizing the `docs/decisions/` folder, updating `CLAUDE.md`'s pending-decisions section, and writing the dated Phase 6 handoff. Both decisions gate Phase 7 — Mesocycle vs TrainingPlan drives the data model; Coaches as a first-class user type drives the parity-doc tagging and any coach-facing scope.

## Current code/status
- `docs/decisions/` does **not** yet exist. `ls docs/` today shows only `handoffs/` and `product/`. This task creates `docs/decisions/`.
- Pending decisions list in `CLAUDE.md` carries both items:
  - "Mesocycle vs new TrainingPlan model (decide at Phase 6)"
  - "Coaches as first-class user type (decide before any coach-facing work)"
- `docs/handoffs/` contains `2026-04-29-phase-4-complete.md` and `2026-05-25-phase-5-vue-onboarding-wizard.md`. Phase 6 handoff slots in alongside, dated when written.
- ROADMAP.md Phase 6 success criteria explicitly require both ADRs and a `CLAUDE.md` update before the phase is marked complete.
- ROADMAP.md architect note for Phase 6 is unambiguous: **do not start Phase 7 prompts until the Mesocycle decision is locked.** This task is the gate.
- `docs/product/feature-parity-trainingpeaks.md` carries coach-tagged features. The Coaches ADR decision (v1 / v2 / out-of-scope) drives the tag sweep in that doc.

## Acceptance criteria
- **`docs/decisions/` folder created** with a short README at `docs/decisions/README.md` explaining: ADR numbering (incrementing `NNNN-kebab-title.md`), template (minimal — Context / Decision / Consequences / Status / Date), and the rule that ADRs are immutable after merge (supersession via new ADRs, not edits).
- **ADR-0001 — Mesocycle vs TrainingPlan.** File at `docs/decisions/0001-mesocycle-vs-trainingplan.md`. Captures:
  - Context: the legacy `Mesocycle / Week / Day / DayExercise / Exercise` model (Phase 2) and the incoming `TrainingPlan / PlannedWorkout / Workout` model (Phase 7).
  - Three options weighed: **supersede** (retire legacy entities), **integrate** (one model wraps the other), **coexist** (hard boundary, both stay).
  - Decision (TBD — Sr. Dev makes the call before this task ships; the prompt records what was decided).
  - Consequences for Phase 7 task groups, the eventual migration, the layer-fix tech debt resolved in Task 6-4, and any UI surface that would need to follow.
  - Status: `Accepted` once Sr. Dev signs off; `Proposed` until then.
  - Date: ISO date of acceptance.
- **ADR-0002 — Coaches as a first-class user type.** File at `docs/decisions/0002-coaches-first-class-user.md`. Captures:
  - Context: the parity doc (`docs/product/feature-parity-trainingpeaks.md`) lists several coach-dependent features (dashboard, athlete roster, workout/plan libraries, group calendars). Phase 12 auth is the natural carrier if coaches ship in v1.
  - Three options: **v1** (build the minimum coach surface alongside Phase 12 / Phase 15), **v2** (defer; ship athlete-only v1; coach features in a post-v1 phase), **out-of-scope** (Bryk is athlete-only product; remove coach-tagged features from the parity doc).
  - Decision (TBD — same gating as ADR-0001).
  - Consequences for the parity doc's tagging sweep, for Phase 12 (auth model — does it need to support a `Coach` role from day one?), and for Phase 15 cutover.
- **Two ADRs use the same template.** Keep them short — one printed page each is the target, not a multi-page essay.
- **`CLAUDE.md` updated.** Both pending-decision sections moved out of "Pending decisions" and into a brief "Resolved decisions" pointer, or deleted entirely with a reference like "see [`docs/decisions/0001-...`]" depending on Sr. Dev's preferred shape. Tech-debt list (item 3) updated if Task 6-4 resolved the `MesocycleService` layer fix.
- **`docs/product/feature-parity-trainingpeaks.md` tag sweep.** Coach-tagged features re-tagged per ADR-0002's decision (v1 → in-scope; v2 → deferred; out-of-scope → removed or tagged `out-of-scope`). One commit, scoped to that doc.
- **Phase 6 handoff written.** New file at `docs/handoffs/YYYY-MM-DD-phase-6-complete.md` (date set when written) capturing:
  - Tasks 6-1 through 6-6: what shipped, with commit hashes.
  - Test infrastructure: .NET projects added, Vue test setup, CI workflow URL of the first green run.
  - Tech-debt items addressed (3, 4, 5, 7) with commit hashes.
  - Secrets hygiene: workflow documented, file location of the dev-setup doc.
  - Both ADRs linked.
  - What Phase 7 should do first, given the Mesocycle decision.
  - Any new tech-debt items surfaced during Phase 6 that weren't on the original list.
- **ROADMAP.md updated** — Phase 6 marked ✅ in the ledger table, the Phase 6 entry's Status line flipped, and any cross-phase risks resolved by Phase 6 (test coverage, Mesocycle decision, Coaches decision, dev-secrets) updated or removed. Architect note about not starting Phase 7 until decision is locked is now resolved — note that in the commit.

## Files likely to change/add
- `docs/decisions/README.md` (new) — ADR conventions for this repo.
- `docs/decisions/0001-mesocycle-vs-trainingplan.md` (new).
- `docs/decisions/0002-coaches-first-class-user.md` (new).
- `docs/handoffs/YYYY-MM-DD-phase-6-complete.md` (new).
- `CLAUDE.md` — pending-decisions section pruned; tech-debt list pruned where Phase 6 resolved an item.
- `ROADMAP.md` — ledger table marks Phase 6 ✅; cross-phase risks section pruned for resolved items.
- `docs/product/feature-parity-trainingpeaks.md` — coach-tagged features re-tagged per ADR-0002.

## What NOT to modify
- Do not write Phase 7 code, prompts, or migration plans. The ADR records the decision; enacting it is Phase 7's job.
- Do not edit ADRs after sign-off. Supersession-by-new-ADR is the convention; document this in `docs/decisions/README.md` and follow it from the first commit.
- Do not bundle decisions outside the two named items into ADR-0001/0002. If other Phase 6 decisions deserve ADRs (test-DB strategy from Task 6-1, CI service from Task 6-3, secrets approach from Task 6-5), they can be backfilled as ADR-0003+ in a separate prompt — surface the question in the handoff but don't slip them in here.
- Do not edit prior handoff files (`2026-04-29-phase-4-complete.md`, `2026-05-25-phase-5-vue-onboarding-wizard.md`). Handoffs are immutable snapshots.
- Do not modify the parity doc beyond the coach-tag sweep tied to ADR-0002. Other tag changes belong to their own phases.
- Do not mark Phase 6 complete on ROADMAP.md unless **all six task files (6-1 through 6-6) have shipped and have commit hashes**. If any task is incomplete, the handoff records that and the ledger stays 🟡.

## Approval gates / open questions
- **Approval gate (hard):** the two decisions themselves. Sr. Dev makes both calls; the architect drafts options and recommendations, presents them, and only writes the ADRs to "Accepted" once the answer is on record. Until then, the ADRs sit at "Proposed" — committing a Proposed ADR is fine and useful (records the options weighed) but does not close the Phase 6 gate.
- **Decision question (Mesocycle):** supersede / integrate / coexist. Architect's recommendation should be documented in the ADR draft before Sr. Dev review. Likely default is **supersede** (a clean break before Phase 8+ depends on the new model), but the tradeoff is migration cost on data that may or may not exist yet.
- **Decision question (Coaches):** v1 / v2 / out-of-scope. The parity doc's content drives this — many features depend on it, but none are scheduled for v1 today. Likely default is **v2** to keep v1 small.
- **Open question:** do other Phase 6 decisions warrant ADRs? Test-DB strategy (Task 6-1), CI provider (Task 6-3), secrets approach (Task 6-5). Recommendation: yes, but as ADRs 0003+ in a follow-up, not in this task. Surface in the handoff.
- **Open question:** ADR template — minimal four-section (Context / Decision / Consequences / Status) or Michael Nygard's full template (Context / Decision / Status / Consequences / plus optional notes)? Recommendation: minimal. The ROADMAP.md and CLAUDE.md already carry context elsewhere; the ADR is a decision record, not a wiki page.

## Test plan
1. Verify both ADR files render cleanly as Markdown (preview locally; check the template matches `docs/decisions/README.md`'s specification).
2. Verify `CLAUDE.md` no longer claims the two decisions are pending. Pending list still includes anything else that's genuinely open (auth approach, etc.).
3. Verify ROADMAP.md ledger shows Phase 6 ✅ if and only if all six tasks have shipped. Cross-phase risks section pruned for resolved items.
4. Verify the parity doc's coach features carry tags consistent with ADR-0002's decision.
5. Verify the handoff names each Phase 6 task, links to its commits, and explicitly answers "what should Phase 7 do first."
6. Verify `git diff` for this task touches only the files listed above — no stray changes into `api/`, `ui/`, or `.github/`.
