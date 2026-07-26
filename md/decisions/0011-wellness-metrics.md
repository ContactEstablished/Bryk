# ADR-0011 — Wellness metrics (storage, double-source policy, analytics boundary)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — `DailyWellness` is one wide row per athlete per day with every
metric nullable, uniquely indexed on `{AthleteId, Date}` and upserted service-side; it is **independent
of `Athlete`** and never writes back to `Athlete.RestingHr`/`WeightKg`; HRV does **not** blend into
TSB/PMC or any readiness score; the soreness/sleep-quality input generalizes `RpeSelector` into a shared
`ScaleSelector` rather than duplicating it; and `DeltaChip` is not recoloured — inverted metrics report
their change in `MetricTile`'s footer slot.

## Context

Phase 20 turns the dashboard's Sleep placeholder (`ui/src/views/HomeView.vue:91–94`, subtitle
*"Post-v1 — needs a device or health-app integration."*) into a real tile and gives Resting HR a history
trend, from **manual** daily entry — the honest v1 answer to that placeholder. Manual entry is not a
compromise on the way to device sync; it is the only thing that can ship without an OAuth integration,
and it produces exactly the same rows a sync would later write.

The ROADMAP's Phase 20 entry (`ROADMAP.md:557–581`) flags three items under *Decisions needed*: the
`DailyWellness` migration approval, whether HRV-adjusted readiness blends into TSB, and the
`RpeSelector` generalization call. All three were resolved by the Sr. Dev on 2026-07-26, together with
a fourth the ROADMAP does not raise at all — what happens to the two fields that now have two sources of
truth (`Athlete.WeightKg`, `Athlete.RestingHr`) — and a fifth that surfaced while reading the dashboard
components: whether `DeltaChip` needs an inverted colour mode for metrics where a drop is good news.

This ADR resolves:

1. **The double-source question** — whether a wellness write ever touches `Athlete`.
2. **Row shape** — one wide nullable row per day vs. a key/value child table, and how day-uniqueness is
   enforced.
3. **The analytics boundary** — whether HRV or any wellness metric enters TSB/PMC or a readiness score.
4. **The scale input** — generalize `RpeSelector` or duplicate it.
5. **Delta colouring** — whether `DeltaChip` learns an inverted mode.
6. **Migration scope** — exactly which table(s) this phase is approved to create.

### Conventions this ADR follows

- Athlete identity always via `ICurrentUserService`; missing/foreign resources are `KeyNotFoundException`
  → 404. Phase 12 auth stays deferred and approval-gated.
- Errors use the existing middleware contract (`ExceptionHandlingMiddleware`): `ValidationException` → 400
  with `{status, error, errors[], traceId}`, `KeyNotFoundException` → 404, `InvalidOperationException` →
  409. **No ProblemDetails rework** — Phase 21 owns that.
- Repository pattern; `IUnitOfWork` owns the commit; every write path commits **once**.
- Validation is `await validator.ValidateOrThrowAsync(request, ct)`
  (`Bryk.Application.Common.Validation`), never FluentValidation's `ValidateAndThrowAsync`.
- Field-scoped messages are written explicitly with `.WithMessage("Field: …")`, because
  `ValidateOrThrowAsync` (`ValidationExtensions.cs:16–27`) collects `ErrorMessage` only and drops the
  property name — the convention `ActivityFileUploadRequestValidator.cs:16–28` established.
- The entity follows the denormalized-`AthleteId`-with-no-FK convention every entity since ADR-0003 uses
  (`Workout`, `WorkoutStepResult`, `ActivityFile`).

## Decision

### 1. `DailyWellness` is independent; a wellness write never touches `Athlete`

Two fields now have two sources. The **verified** consumer list, from a full-solution grep of `WeightKg`
and `RestingHr` across `api/`:

- `Athlete.WeightKg` (`Athlete.cs:12`, non-nullable `decimal`, precision (5,2) at
  `ApplicationDbContext.cs:41`) is written by `OnboardingService` (L35 on insert, L48 on update), bounded
  by `Onboarding/Validators/OnboardingRequiredRequestValidator.cs:32`, and read by `ProfileService:31` →
  `ProfileRequiredResponse`.
- `Athlete.RestingHr` (`Athlete.cs:13`, `int?`) is written by the onboarding *recommended* step
  (`OnboardingService.cs:71`), bounded by `OnboardingRecommendedRequestValidator.cs:9–12` (and used as
  the floor for `MaxHr` at `:20–22`), read by `ProfileService:50` → `ProfileRecommendedResponse`, and
  rendered by `RestingHrCard.vue`.

**Neither feeds any load, zone or PMC calculation** — the same grep returns no hit in `LoadCalculator`,
`PmcCalculator`, `AcwrCalculator`, `TimeInZoneCalculator`, `ZoneHistogramCalculator` or
`AnalyticsService`. That is exactly what makes divergence harmless: the two values are display and
onboarding-validation inputs, not math inputs, so a wellness row that disagrees with the profile cannot
change a single computed number.

Decision: the onboarding/profile values stay as they are (a one-off self-report), wellness rows are the
time series, and **no sync runs in either direction, at any layer**. The single concession is a
**read-only fallback**: when an athlete has no wellness entries at all, the Resting HR tile displays
`Athlete.RestingHr` so the shipped tile never regresses to `—` (Task 20-4). **No fallback for weight** —
the weight tile is a trend tile, and an onboarding constant is not a trend; showing one number with no
history would imply a flat trend that was never measured.

### 2. One wide, mostly-nullable row per athlete per day

`DailyWellness` carries `SleepHours`, `SleepQuality`, `RestingHr`, `WeightKg`, `Soreness`, `HrvMs`,
`Notes` — **every metric nullable**, because partial entries are the norm (an athlete who weighs in but
owns no HRV strap must still be able to log the day). A required-field design would make the common case
impossible.

Uniqueness of `{AthleteId, Date}` is enforced **twice**, on purpose: a unique composite index in the model
(the `AthleteSportProfile` precedent, `ApplicationDbContext.cs:80`, and the four-column unique at `:183`)
and, load-bearing, a service-side read-then-update upsert in Task 20-2. Why both: `BrykWebApplicationFactory`
runs on the EF InMemory provider, whose own doc comment (`BrykWebApplicationFactory.cs:11–23`) records
that it enforces **no unique index**, so the index **cannot be proven by an integration test** — it is
verified by reading the generated migration, and the *behaviour* is proven by a service test in 20-2
that PUTs the same day twice and counts the rows the API returns. **No test may assert that a duplicate
insert throws**: against InMemory such a test would pass for the wrong reason, and against SQL Server it
would pin behaviour the service is designed never to reach.

The composite index's leading column also serves every athlete-scoped range read, so **no second index on
`AthleteId` alone** is created.

`PUT` replaces the whole day: a metric omitted from the body is cleared, not preserved. This keeps the
endpoint honestly idempotent — the same body always produces the same row — at the cost of making partial
updates impossible, which is the right trade for a form that always submits every field it owns. There is
**no DELETE endpoint** in v1 — consequence: an all-null day cannot be created (the validator requires at
least one metric) and therefore cannot be reached by clearing either. Recorded as a known limitation, not
a bug.

### 3. HRV does not blend into TSB, PMC or any readiness score

ADR-0006 keeps the PMC a pure function of training load; wellness is context rendered beside it, never an
input to it. No readiness/recovery score, no "should you train today" recommendation, no HRV-adjusted
CTL/ATL/TSB — in this phase or as a side effect of it. `PmcCalculator.cs`, `AcwrCalculator.cs`,
`LoadCalculator.cs`, `TimeInZoneCalculator.cs` and `AnalyticsService.cs` must not appear in Phase 20's
diff at all.

The ROADMAP already *recommends* no; this makes it **binding**. The reason is not squeamishness about
scope: there is no validated model for blending HRV into a training-stress balance, and a wrong one would
be actively harmful — an athlete told to rest by a number Bryk invented is worse off than one shown the
raw trend and left to judge. Revisiting it is a parity-doc candidate
(`md/product/feature-parity-trainingpeaks.md`), not a Phase 20 stretch goal.

### 4. The scale input generalizes; it does not duplicate

`RpeSelector.vue` (40 lines, a hardcoded 1–10 tap grid) becomes a thin wrapper over a new `ScaleSelector`
taking `max` + `labels`; soreness uses 1–10 and sleep quality 1–5. `RpeSelector`'s props/emits contract is
unchanged, so `LogWorkoutForm.vue:252` and the three existing `RpeSelector` specs stay **untouched and
passing** — that is the regression gate on Task 20-3.

Record the Tailwind constraint, because it is the one way this refactor fails silently: `grid-cols-10` and
`grid-cols-5` must both appear as **literal** class strings in the source. An interpolated
`grid-cols-${n}` is invisible to Tailwind v4's scanner, the utility is never generated, and the grid
renders as a single column — with no build error and no console warning.

### 5. `DeltaChip` is not recoloured; inverted metrics use the footer

`DeltaChip.vue:8–12` colours `up` green (`text-good`) and `down` red (`text-bad`), and
`ui/src/lib/weeklyTarget.ts:21–23` carries the standing written instruction not to "fix" that. For sleep
hours and HRV, up is good, so those tiles pass `MetricTile`'s `delta` prop. For resting HR, weight and
soreness, down is good, so those tiles render their 7-day change in `MetricTile`'s **`#footer` slot** with
their own colouring — the slot `PeaksSection.vue:93` and `FormCard.vue:31` already use for exactly this
kind of tile-specific trailing content.

Result: good news never renders red, and the chip keeps one meaning across its existing consumers — the
render site `MetricTile.vue:73`, the one direct use at `ThisWeekCard.vue:92`, and the two tiles that pass
the prop through `MetricTile` (`PeaksSection.vue:92`, `FormCard.vue:29`). **No `invert` prop, no new
chip.**

### 6. One migration: `DailyWellness` and nothing else

Approved: the `DailyWellness` entity + table + its unique composite index. **Not approved, do not
create:** any change to `Athlete` (no `ICollection<DailyWellness>` nav, no FK, no column, no nullability
change), any second table (no `WellnessMetric` key/value table, no notes child table), any new NuGet or
npm package. There is **no FK** from `DailyWellness` to `Athlete` — `AthleteId` is denormalized and
covered by the composite index, the convention every entity since ADR-0003 follows.

The generated migration must contain exactly one `CreateTable("DailyWellness")`, exactly one
`CreateIndex` named `IX_DailyWellness_AthleteId_Date` with `unique: true`, **zero** `AddForeignKey`, and
a `Down` that is a single `DropTable`. If it touches any other table — including `Athletes` — the model
has drifted; the fix is the model, never a hand-edit of the migration. Any second migration in Phase 20 →
**STOP and ask.**

## Consequences

**Closed by this decision:** all three ROADMAP *Decisions needed* bullets (migration approval,
HRV-into-TSB, the `RpeSelector` generalization call) plus the double-source question and the
delta-colouring question the ROADMAP does not raise. **Created — one migration, zero new packages:**

- `Bryk.Domain/Entities/DailyWellness.cs`, `Bryk.Domain/Interfaces/IDailyWellnessRepository.cs`,
  `Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs`, an `ApplicationDbContext` `DbSet` +
  configuration block, and the `AddDailyWellness` migration (20-1).
- DTOs + validators + `WellnessService` (the upsert) + `WellnessController` (20-2).
- Types + service + store + `ScaleSelector` + the Today entry card (20-3).
- Sleep / Resting HR / weight / HRV tiles + dashboard wiring (20-4).

**Known limitations accepted here, not deferred defects:** an all-null day cannot exist and there is no
DELETE endpoint (§2); the weight tile shows `—` until the athlete logs a weight, by design (§1); and the
profile and wellness views of resting HR may legitimately disagree, because they answer different
questions (§1).

### For Tasks 20-1 … 20-4

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **20-1** ADR + entity/repo/config/migration | Backend | — | §2 (row shape), §6 (migration scope) |
| **20-2** DTOs + validators + service + controller | Backend | 20-1 | §1 (no write-back), §2 (upsert is the service's job) |
| **20-3** types + service + store + `ScaleSelector` + entry card | Frontend | 20-2 | §4 (wrapper, literal grid classes) |
| **20-4** Sleep/RHR/weight/HRV tiles + dashboard wiring | Frontend | 20-3 | §1 (RHR fallback), §5 (delta vs footer) |

## Alternatives considered

- **Writing through to `Athlete.WeightKg`/`Athlete.RestingHr` on every wellness save.** Rejected (§1) —
  two write paths into a field consumed by onboarding validation, for no reader that needs it. The
  profile answers "what did you tell us when you signed up"; the wellness row answers "what was it on
  Tuesday". Collapsing them loses the first and gains nothing.
- **A `WellnessMetric` key/value child table.** Rejected (§2) — six fixed metrics do not need EAV, and it
  costs a second table plus a harder day-uniqueness constraint, in exchange for a flexibility no planned
  feature asks for.
- **Folding wellness onto the workout log.** Rejected — wellness is per day, not per session, and a rest
  day is exactly when sleep and soreness matter most. Attaching it to a workout makes the most
  informative days unloggable.
- **HRV-adjusted TSB / a readiness score.** Rejected (§3) — ADR-0006 keeps the PMC pure and there is no
  validated model to blend with.
- **Duplicating `RpeSelector` for soreness.** Rejected (§4) — two copies of a tap grid drift, and the
  wrapper costs less code than the copy.
- **Adding an `invert` prop to `DeltaChip`.** Rejected (§5) — the chip's contract is documented in
  `weeklyTarget.ts:21–23` and shared by four call sites; an inverted mode makes the colour depend on
  caller configuration rather than on direction, which is precisely what that comment forbids.
- **Device/health sync (Whoop/Oura/Apple Health).** Out of scope by ROADMAP lock, not re-litigated.
