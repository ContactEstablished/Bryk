# Impl 20-1 — Build order: ADR-0011 + `DailyWellness` entity, repository, DbContext config, migration

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-20-1.md`.
**Decision lock:** ADR-0011 (`md/decisions/0011-wellness-metrics.md`, written in Step 1 of this build order
and **reviewed before any `.cs` file is created**) + ADR-0003/ADR-0004 (the denormalized-`AthleteId`-with-
no-FK convention this entity follows) + ADR-0006 (the PMC is a pure function of training load — ADR-0011
§3 makes "HRV never enters TSB" binding rather than merely recommended) + ADR-0010 (the ADR *format*
template this task reproduces section-for-section).
**Scope:** Backend only. One new ADR, one new entity, one repository contract + implementation, one
`ApplicationDbContext` edit (two additive blocks), **one reviewed migration (the phase's single Sr. Dev
gate)**, **one** `Program.cs` line, one new test file. No DTO, no validator, no service, no controller, no
UI. Nothing in this task is reachable over HTTP — 20-2 wires it up. **No new package.**

This is the step-by-step build order. Execute top-to-bottom; each step's **Verify** is the gate to the
next. **Step 1 carries a hard stop** — do not write any `.cs` file until ADR-0011 has been read and
accepted. **Step 6 carries the second hard stop** — the migration is generated and read in full, then
approved, and only then applied. One commit at the end, with the message from `Tasks-20-1.md`.

**Ordering note (deliberate deviation from `Impl-19-1.md`).** `DailyWellnessRepository` references
`db.DailyWellness`, which does not exist as a symbol until the `DbSet` lands. The `ApplicationDbContext`
edit therefore comes **before** the repository implementation here (Step 4 before Step 5), not after it.
Doing it in 19-1's order would leave Step 5's "build green" gate unsatisfiable.

## Step 0 — Pre-flight

- `git status` clean on `main` (the coordinator verified the tree clean at `005481e`; the only untracked
  files should be this task's own spec docs, `md/Tasks-20-1.md` and `md/Impl-20-1.md`). **Do not revert,
  stash, or commit anything else** you find in the working tree.
- Baseline, confirmed by running — do not take these on trust, they are the numbers every later Verify
  compares against:
  - `dotnet build api/Bryk.sln --no-incremental` → 0 errors, **16 warnings**. 14 of the 16 are the
    design-time `System.Security.Cryptography.Xml` NU1903 advisory; the other two are the pre-existing
    nullable warnings at `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121` (CS8604) and `:150`
    (CS8602) — **deliberately not fixed; do not fix them.** An *incremental* build reports **14** because
    it skips recompiling `Bryk.API.Tests`; always compare like for like.
  - `dotnet test api/Bryk.sln` → **343 passed, 0 failed** (**196** `Bryk.Application.Tests` + **147**
    `Bryk.API.Tests`).
  - `cd ui; pnpm run build` green; `cd ui; pnpm exec vitest run --no-file-parallelism` → **288 tests /
    61 files**. This task touches no file under `ui/` — these two numbers must be **byte-for-byte
    unchanged** at the end.
- Confirm the new surface does not exist yet (everything but `ApplicationDbContext.cs`, `Program.cs` and
  the `Migrations/` folder is purely additive this task):
  `md/decisions/0011-wellness-metrics.md`, `api/Bryk.Domain/Entities/DailyWellness.cs`,
  `api/Bryk.Domain/Interfaces/IDailyWellnessRepository.cs`,
  `api/Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs`, `api/Bryk.API.Tests/Wellness/`.
- Re-read `md/Tasks-20-1.md` in full. Open in the editor, and confirm each cited line by eye before you
  rely on it:
  - `ROADMAP.md:557–577` — the Phase 20 entry. Its *Decisions needed* line (571) names three open
    questions; all three are resolved and ADR-0011 is where they are recorded.
  - `md/decisions/0010-activity-file-import.md` — **the format template**: title line, `**Date:**`,
    `**Status:** Accepted (date) — <one-sentence summary>`, `## Context` with a
    `### Conventions this ADR follows` subsection, numbered `### N.` sections under `## Decision`,
    `## Consequences` with a per-task table, `## Alternatives considered`.
  - `md/handoffs/2026-07-26-phase-19-complete.md` — the state you are building on.
  - `api/Bryk.Domain/Entities/Goal.cs` — the entity shape (`Guid Id`, `Guid AthleteId`, scalars, the
    `Athlete` nav, then the two `IAuditable` fields). **This task's entity omits Goal's `Athlete` nav.**
  - `api/Bryk.Domain/Entities/ActivityFile.cs:5–15` and `api/Bryk.Domain/Entities/WorkoutStepResult.cs:5–8`
    — the header-comment style that names the ADR and explains the denormalized `AthleteId`. Mirror it.
  - `api/Bryk.Domain/Entities/Athlete.cs:12–13` — `public decimal WeightKg` (non-nullable) and
    `public int? RestingHr`. Confirm for yourself that **this task does not touch this file**, then leave
    it alone.
  - `api/Bryk.Domain/Interfaces/IGoalRepository.cs` — the contract style: XML `<summary>` on every member,
    the exact `"Uses no-tracking."` / `"Does NOT call SaveChanges."` wording.
  - `api/Bryk.Infrastructure/Repositories/GoalRepository.cs` — primary-ctor repository, `AsNoTracking()`
    on display reads, `AddAsync` / `Update` / `Delete` stage only, no `SaveChangesAsync` anywhere.
  - `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — `DbSet` block **L13–25**; the unique
    composite index precedent `entity.HasIndex(e => new { e.AthleteId, e.Sport }).IsUnique();` at **L80**
    and the four-column unique at **L183**; `.HasPrecision(5, 2)` for `Athlete.WeightKg` at **L41**;
    `entity.Property(e => e.Notes).HasMaxLength(1000);` at **L88**; the `ActivityFile` block at
    **L233–246** with its `// Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).` comment at L244.
  - `api/Bryk.Infrastructure/Migrations/20260726155712_AddActivityFile.cs` — the most recent migration;
    one `CreateTable`, index creations, and a `Down` that is a single `DropTable` (**L47–51**).
  - `api/Bryk.API/Program.cs:99–108` — the repositories `AddScoped` block, and `:35`
    (`AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()` — validators are picked up by
    assembly scan, so no manual validator line is ever needed, and none exists to register this task).
  - `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs:11–23` (the factory's own doc comment: the
    InMemory provider has **no unique-index enforcement**), `:31–32` (`TestAthleteId` =
    `11111111-1111-1111-1111-111111111111`), `:34` (each factory instance gets its own database name) and
    `:73–79` (the InMemory registration does **not** re-add `AuditableEntityInterceptor`).
  - `api/Bryk.API.Tests/ActivityFiles/ActivityFileRepositoryTests.cs` — the
    `factory.Services.CreateScope()` → `ApplicationDbContext` → `new XRepository(db)` pattern the new test
    file reuses verbatim.
  - `CLAUDE.md` → *When to ask for Sr. Dev approval before proceeding* → **DbContext or data model changes
    that would generate a migration.**
- Confirm the two exact insertion points by eye before editing anything (verified this session):
  - `ApplicationDbContext.cs` **L25** is `public DbSet<ActivityFile> ActivityFiles => Set<ActivityFile>();`
    — the last line of the `DbSet` block; the new `DbSet<DailyWellness>` line goes **directly after it**,
    before the blank line at L26. **L246** is the `ActivityFile` config block's closing `});` and **L247**
    is `OnModelCreating`'s closing `}` — the new configuration block goes **between them**.
  - `Program.cs` **L107** is
    `builder.Services.AddScoped<IActivityFileRepository, ActivityFileRepository>();` and **L108** is
    `builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();` — the new line goes **between them**.

**Verify:** all four baseline commands green at the numbers above; the five new paths confirmed absent;
the two insertion points confirmed by eye. Do not start Step 1 until every number matches.

## Step 1 — Write ADR-0011 first (`md/decisions/0011-wellness-metrics.md`)

**New file.** Section-for-section skeleton matches ADR-0010: title line, `**Date:**`, `**Status:**`,
`## Context` (with a `### Conventions this ADR follows` subsection), `## Decision` (six numbered
sections), `## Consequences` (with a *For Tasks 20-1 … 20-4* table), `## Alternatives considered`.

The header block below is **normative — reproduce it verbatim** from `Tasks-20-1.md`. Everything after it
is the skeleton plus the load-bearing claims each section must carry; expand the prose, do not change the
structure or drop a claim.

```markdown
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
trend, from **manual** daily entry — the honest v1 answer to that placeholder. The ROADMAP's Phase 20
entry (`ROADMAP.md:557–577`) flags three items under *Decisions needed*: the `DailyWellness` migration
approval, whether HRV-adjusted readiness blends into TSB, and the `RpeSelector` generalization call. All
three were resolved by the Sr. Dev on 2026-07-26, together with a fourth the ROADMAP does not raise at
all — what happens to the two fields that now have two sources of truth (`Athlete.WeightKg`,
`Athlete.RestingHr`).

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

## Decision

### 1. `DailyWellness` is independent; a wellness write never touches `Athlete`

Two fields now have two sources. Record the **verified** consumer list:

- `Athlete.WeightKg` (`Athlete.cs:12`, non-nullable `decimal`, precision (5,2)) is read only by
  `OnboardingService` (L35, L48), `OnboardingRequiredRequestValidator:32`, and `ProfileService:31` →
  `ProfileRequiredResponse`.
- `Athlete.RestingHr` (`Athlete.cs:13`, `int?`) is set in the onboarding *recommended* step, surfaced by
  `ProfileService:50` → `ProfileRecommendedResponse`, and rendered by `RestingHrCard.vue`.

**Neither feeds any load, zone or PMC calculation**, which is exactly what makes divergence harmless.
Decision: the onboarding/profile values stay as they are (a one-off self-report), wellness rows are the
time series, and **no sync runs in either direction, at any layer**. The single concession is a
**read-only fallback**: when an athlete has no wellness entries at all, the Resting HR tile displays
`Athlete.RestingHr` so the shipped tile never regresses to `—` (Task 20-4). **No fallback for weight** —
the weight tile is a trend tile, and an onboarding constant is not a trend.

### 2. One wide, mostly-nullable row per athlete per day

`DailyWellness` carries `SleepHours`, `SleepQuality`, `RestingHr`, `WeightKg`, `Soreness`, `HrvMs`,
`Notes` — **every metric nullable**, because partial entries are the norm (an athlete who weighs in but
owns no HRV strap must still be able to log).

Uniqueness of `{AthleteId, Date}` is enforced **twice**, on purpose: a unique composite index in the model
(the `AthleteSportProfile` precedent, `ApplicationDbContext.cs:80`) and, load-bearing, a service-side
read-then-update upsert in Task 20-2. Why both: `BrykWebApplicationFactory` runs on the EF InMemory
provider, whose own doc comment (`BrykWebApplicationFactory.cs:11–23`) records that it enforces **no
unique index**, so the index **cannot be proven by an integration test** — it is verified by reading the
generated migration, and the *behaviour* is proven by a service test in 20-2. **No test may assert that a
duplicate insert throws.**

`PUT` replaces the whole day: a metric omitted from the body is cleared, not preserved. There is **no
DELETE endpoint** in v1 — consequence: an all-null day cannot be created and therefore cannot be reached
by clearing. Recorded as a known limitation, not a bug.

### 3. HRV does not blend into TSB, PMC or any readiness score

ADR-0006 keeps the PMC a pure function of training load; wellness is context rendered beside it, never an
input to it. No readiness/recovery score, no "should you train today" recommendation, no HRV-adjusted
CTL/ATL/TSB — in this phase or as a side effect of it. The ROADMAP already *recommends* no; this makes it
**binding**, and a parity-doc candidate (`md/product/feature-parity-trainingpeaks.md`).

### 4. The scale input generalizes; it does not duplicate

`RpeSelector.vue` (41 lines, a hardcoded 1–10 tap grid) becomes a thin wrapper over a new `ScaleSelector`
taking `max` + `labels`; soreness uses 1–10 and sleep quality 1–5. `RpeSelector`'s props/emits contract is
unchanged, so `LogWorkoutForm.vue:252` and the three existing `RpeSelector` specs stay **untouched and
passing** — that is the regression gate on Task 20-3. Record the Tailwind constraint: `grid-cols-10` and
`grid-cols-5` must both appear as **literal** class strings, because an interpolated `grid-cols-${n}` is
invisible to Tailwind's scanner and would silently render a one-column grid.

### 5. `DeltaChip` is not recoloured; inverted metrics use the footer

`DeltaChip.vue:8–12` colours `up` green and `down` red, and `ui/src/lib/weeklyTarget.ts:21–23` carries the
standing written instruction not to "fix" that. For sleep hours and HRV, up is good, so those tiles pass
`MetricTile`'s `delta` prop. For resting HR, weight and soreness, down is good, so those tiles render
their 7-day change in `MetricTile`'s **`#footer` slot** with their own colouring. Result: good news never
renders red, and the chip keeps one meaning across its four existing consumers (`MetricTile.vue:73`,
`ThisWeekCard.vue:92`, `PeaksSection.vue:92`, `FormCard.vue:29`). **No `invert` prop, no new chip.**

### 6. One migration: `DailyWellness` and nothing else

Approved: the `DailyWellness` entity + table + its unique composite index. **Not approved, do not
create:** any change to `Athlete` (no `ICollection<DailyWellness>` nav, no FK, no column), any second
table (no `WellnessMetric` key/value table, no notes child table), any new package. There is **no FK**
from `DailyWellness` to `Athlete` — `AthleteId` is denormalized and covered by the composite index, the
convention every entity since ADR-0003 follows. Any second migration in Phase 20 → **STOP and ask.**

## Consequences

**Closed by this decision:** all three ROADMAP *Decisions needed* bullets (migration approval,
HRV-into-TSB, the `RpeSelector` generalization call) plus the double-source question the ROADMAP does not
raise. **Created — one migration, zero new packages:**

- `Bryk.Domain/Entities/DailyWellness.cs`, `Bryk.Domain/Interfaces/IDailyWellnessRepository.cs`,
  `Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs`, an `ApplicationDbContext` `DbSet` +
  configuration block, and the `AddDailyWellness` migration (20-1).
- DTOs + validators + `WellnessService` (the upsert) + `WellnessController` (20-2).
- Types + service + store + `ScaleSelector` + the Today entry card (20-3).
- Sleep / Resting HR / weight / HRV tiles + dashboard wiring (20-4).

### For Tasks 20-1 … 20-4

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **20-1** ADR + entity/repo/config/migration | Backend | — | §2 (row shape), §6 (migration scope) |
| **20-2** DTOs + validators + service + controller | Backend | 20-1 | §1 (no write-back), §2 (upsert is the service's job) |
| **20-3** types + service + store + `ScaleSelector` + entry card | Frontend | 20-2 | §4 (wrapper, literal grid classes) |
| **20-4** Sleep/RHR/weight/HRV tiles + dashboard wiring | Frontend | 20-3 | §1 (RHR fallback), §5 (delta vs footer) |

## Alternatives considered

- **Writing through to `Athlete.WeightKg`/`Athlete.RestingHr` on every wellness save.** Rejected (§1) —
  two write paths into a field consumed by onboarding validation, for no reader that needs it.
- **A `WellnessMetric` key/value child table.** Rejected (§2) — six fixed metrics do not need EAV, and it
  costs a second table plus a harder day-uniqueness constraint.
- **Folding wellness onto the workout log.** Rejected — wellness is per day, not per session, and a rest
  day is exactly when sleep and soreness matter most.
- **HRV-adjusted TSB / a readiness score.** Rejected (§3) — ADR-0006 keeps the PMC pure and there is no
  validated model to blend with.
- **Duplicating `RpeSelector` for soreness.** Rejected (§4) — two copies of a tap grid drift.
- **Adding an `invert` prop to `DeltaChip`.** Rejected (§5) — the chip's contract is documented and shared
  by four consumers.
- **Device/health sync (Whoop/Oura/Apple Health).** Out of scope by ROADMAP lock, not re-litigated.
```

**Verify (docs-only step — no compiler gate):**
- File exists at `md/decisions/0011-wellness-metrics.md`. Diff its heading outline against
  `md/decisions/0010-activity-file-import.md`: same sections in the same order, including
  `### Conventions this ADR follows` and the per-task consequences table.
- The `**Status:**` block is byte-identical to the one in `Tasks-20-1.md`.
- §1 names the **verified** consumer list for both double-source fields and states the read-only RHR
  fallback with "no fallback for weight". §2 states *why* the unique index cannot be integration-tested
  and forbids a duplicate-insert test. §4 records the literal-Tailwind-class trap. §5 cites
  `weeklyTarget.ts:21–23`. §6 enumerates what is **not** approved.
- Every claim cites the file/line it rests on — no floating assertion.

**STOP — Sr. Dev / reviewer gate.** Per CLAUDE.md and this task's own framing (the ADR is written
"**first**, before any code"), do not create, edit, or stage any `.cs` file until ADR-0011 has been read
and accepted by the reviewer. Do not proceed to Step 2 on your own authority.

## Step 2 — `DailyWellness.cs`

**New file** `api/Bryk.Domain/Entities/DailyWellness.cs`. Property order is **normative** — it is the
order the DTOs (20-2), the migration columns and the UI form (20-3) follow, so the whole phase reads in
one sequence.

```csharp
using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// One manually-entered wellness row per athlete per day (ADR-0011 §1/§2/§6), uniquely indexed on
// {AthleteId, Date}. EVERY metric is nullable because partial entries are the norm — an athlete who
// weighs in but owns no HRV strap must still be able to log the day. AthleteId is denormalized and
// indexed with no FK to Athlete, matching Workout/WorkoutStepResult/ActivityFile (ADR-0003/0004); the
// composite index's leading column covers athlete-scoped queries, so no second index is needed. This row
// is INDEPENDENT of Athlete.RestingHr / Athlete.WeightKg and never writes to them (ADR-0011 §1) — those
// stay the one-off onboarding self-report, this is the time series. SleepQuality (1-5) and Soreness
// (1-10) are plain int?, not enums: bounds are the validator's job (Task 20-2), the domain stores the
// number. CreatedAt/UpdatedAt are owned by AuditableEntityInterceptor and are NEVER set manually
// (CLAUDE.md).
public class DailyWellness : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public DateOnly Date { get; set; }

    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- **No navigation properties.** Not to `Athlete`, not from `Athlete`. `Goal.cs:13` has an
  `Athlete Athlete { get; set; } = null!;` nav — this entity deliberately does **not**, following
  `ActivityFile`/`WorkoutStepResult`. `api/Bryk.Domain/Entities/Athlete.cs` **must not appear in
  `git diff`** at any point in this task.
- No `ValueGeneratedNever()` concern: unlike `Athlete.Id`, `DailyWellness.Id` is service-generated
  (`Guid.NewGuid()`, Task 20-2), the same as `Workout`/`ActivityFile` — no special EF configuration.

**Verify:** `dotnet build api/Bryk.Domain/Bryk.Domain.csproj` green, 0 warnings from this project
(`Bryk.Domain` has zero `PackageReference`s — confirm the build added none). Re-read the file and confirm
there is no `Athlete` property and no `ICollection<>` anywhere in it.

## Step 3 — `IDailyWellnessRepository.cs`

**New file** `api/Bryk.Domain/Interfaces/IDailyWellnessRepository.cs`. **Exactly four members** — the
complete surface 20-2 consumes; no sibling task may extend this file, so it ships whole here.

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the <see cref="DailyWellness"/> row (ADR-0011 §2). Staging methods do NOT call
/// SaveChanges.
/// </summary>
public interface IDailyWellnessRepository
{
    /// <summary>
    /// Loads the athlete's <see cref="DailyWellness"/> row for <paramref name="date"/> <b>tracked</b>,
    /// for the per-day upsert (the service mutates the returned instance in place). Null if the day has
    /// no entry. Deliberately NOT no-tracking: the caller's write depends on change tracking.
    /// </summary>
    Task<DailyWellness?> GetByAthleteAndDateTrackedAsync(Guid athleteId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// The athlete's <see cref="DailyWellness"/> rows in <c>[from, to]</c> (both ends inclusive),
    /// ordered by <see cref="DailyWellness.Date"/> ascending. Sparse — days with no entry are simply
    /// absent. Uses no-tracking (display/aggregate read).
    /// </summary>
    Task<IReadOnlyList<DailyWellness>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Stages a new <see cref="DailyWellness"/> for insertion. Does NOT call SaveChanges.</summary>
    Task AddAsync(DailyWellness entity, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="DailyWellness"/> for update. Does NOT call SaveChanges.</summary>
    void Update(DailyWellness entity);
}
```

- The tracked/no-tracking split is the load-bearing detail and must be stated in the XML docs, exactly as
  written above: the **upsert read tracks** (the service mutates the entity and commits once), the
  **range read does not** (it feeds averages and a JSON response and never mutates).
- **No `GetByIdAsync`, no `Delete`, no `GetAllAsync`.** Nothing addresses a wellness row by `Id`, and
  there is no delete endpoint in v1 (ADR-0011 §2). Do not ship speculative members.

**Verify:** `dotnet build api/Bryk.Domain/Bryk.Domain.csproj` green. `dotnet build api/Bryk.sln` still
green — the interface has no implementation yet and nothing references it, which is expected at this
point.

## Step 4 — `ApplicationDbContext.cs` (edit — two additive blocks)

**File:** `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs`. This step comes **before** the
repository because `DailyWellnessRepository` cannot compile without the `DbSet`.

**4a. `DbSet`** — insert **directly after L25**
(`public DbSet<ActivityFile> ActivityFiles => Set<ActivityFile>();`), before the blank line at L26:

```csharp
    public DbSet<DailyWellness> DailyWellness => Set<DailyWellness>();
```

The property name is **singular on purpose** — the entity is already a "daily" record and
`DailyWellnesses` reads badly. The table name follows the `DbSet` name, so the generated table will be
`DailyWellness`; confirm that in Step 6's migration rather than assuming it.

**4b. Configuration block** — insert **directly after L246** (the `ActivityFile` block's closing `});`)
and **before L247** (`OnModelCreating`'s closing `}`), preceded by one blank line, matching the file's
existing block rhythm:

```csharp

        // DailyWellness configuration (ADR-0011 §2/§6)
        modelBuilder.Entity<DailyWellness>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SleepHours).HasPrecision(4, 2);
            entity.Property(e => e.WeightKg).HasPrecision(5, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            // One row per athlete per day. Denormalized AthleteId, no FK to Athlete (ADR-0003/0004);
            // this composite index both enforces the day constraint and serves the range read.
            entity.HasIndex(e => new { e.AthleteId, e.Date }).IsUnique();
        });
```

Precision rationale, so a reviewer does not have to guess: `WeightKg` is **(5,2)**, byte-for-byte the
`Athlete.WeightKg` precedent at L41, so the two sources are directly comparable; `SleepHours` is
**(4,2)**, enough for quarter-hour granularity inside 20-2's 0–16 validator bound; `Notes` is **1000**,
matching the `Event.Notes` precedent at L88. `SleepQuality`, `RestingHr`, `Soreness` and `HrvMs` are
`int?` and need no configuration.

- **Do not** add a second, non-unique index on `AthleteId` — the composite index's leading column already
  covers athlete-scoped queries.
- **Do not** add any `HasOne`/`HasMany`/`HasForeignKey` — there is no FK (ADR-0011 §6).
- **Do not touch** any other configuration block, in particular `Athlete` (L32–70) and `ActivityFile`
  (L233–246). This file's entire diff for this task is the two additive blocks above.

**Verify:** `dotnet build api/Bryk.sln` green, still **16** warnings on `--no-incremental`.
`git diff api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` shows **only** added lines — one `DbSet`
line and one configuration block — with nothing existing moved, reindented or reformatted.

## Step 5 — `DailyWellnessRepository.cs`

**New file** `api/Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs`. Primary constructor,
matching `GoalRepository` exactly.

```csharp
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class DailyWellnessRepository(ApplicationDbContext db) : IDailyWellnessRepository
{
    public async Task<DailyWellness?> GetByAthleteAndDateTrackedAsync(Guid athleteId, DateOnly date, CancellationToken ct = default)
    {
        // No AsNoTracking() on purpose: the per-day upsert (Task 20-2) mutates this instance in place and
        // commits once through IUnitOfWork. Adding AsNoTracking() here silently breaks that write path.
        return await db.DailyWellness
            .FirstOrDefaultAsync(w => w.AthleteId == athleteId && w.Date == date, ct);
    }

    public async Task<IReadOnlyList<DailyWellness>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await db.DailyWellness
            .AsNoTracking()
            .Where(w => w.AthleteId == athleteId && w.Date >= from && w.Date <= to)
            .OrderBy(w => w.Date)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DailyWellness entity, CancellationToken ct = default)
    {
        await db.DailyWellness.AddAsync(entity, ct);
    }

    public void Update(DailyWellness entity)
    {
        db.DailyWellness.Update(entity);
    }
}
```

- **No `SaveChangesAsync` anywhere in this file** — staging only; `IUnitOfWork` owns the commit
  (CLAUDE.md, ADR-0011 conventions).
- `>= from && <= to` is what makes the range **inclusive on both ends**; Step 8's boundary test pins it.
- No `.Include()`, no `.AsSplitQuery()` — the entity has no navigations to include.

**Verify:** `dotnet build api/Bryk.sln` green, **0 new warnings** (still 16 on `--no-incremental`). At
this point `DailyWellness`, `IDailyWellnessRepository` and `DailyWellnessRepository` all exist and compile
but nothing calls them — expected, not dead code to wire up early (20-2 is the caller). Re-read the file
and confirm `AsNoTracking()` appears **exactly once**, in `GetByAthleteInRangeAsync`.

## Step 6 — The migration — **approval required before apply**

**This is the CLAUDE.md Sr. Dev migration gate and Phase 20's single approved migration.** Generate,
read in full, get sign-off — do not run `dotnet ef database update` until that sign-off exists.

**6a. Generate**, from the repo root (`C:\Projects\Bryk\Site\Bryk`):

```
dotnet ef migrations add AddDailyWellness --project api/Bryk.Infrastructure --startup-project api/Bryk.API
```

This produces three files: `api/Bryk.Infrastructure/Migrations/<timestamp>_AddDailyWellness.cs`,
`<timestamp>_AddDailyWellness.Designer.cs`, and a regenerated
`api/Bryk.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`.

**6b. Read the generated `Up` and `Down` in full** before doing anything else, and check every one of the
following against `20260726155712_AddActivityFile.cs` (the shape template):

- Exactly **one** `migrationBuilder.CreateTable(name: "DailyWellness", ...)` and **nothing else** — no
  `AddColumn`, no `AlterColumn`, no `DropColumn`, no second `CreateTable`. If the generated migration
  touches **any other table** — including `Athletes` — the model has drifted: **STOP and ask.** Do **not**
  hand-edit the migration to remove the extra operations; that means the model, not the migration, is
  wrong.
- Confirm the table name is `DailyWellness` (singular, following the `DbSet` property name from Step 4a).
- Columns and types, read literally off the generated `table: table => new { ... }` block, in entity
  property order:

  | Column | Expected generated type | Nullable |
  |---|---|---|
  | `Id` | `uniqueidentifier` | no |
  | `AthleteId` | `uniqueidentifier` | no |
  | `Date` | `date` | no |
  | `SleepHours` | `decimal(4,2)` (`precision: 4, scale: 2`) | **yes** |
  | `SleepQuality` | `int` | **yes** |
  | `RestingHr` | `int` | **yes** |
  | `WeightKg` | `decimal(5,2)` (`precision: 5, scale: 2`) | **yes** |
  | `Soreness` | `int` | **yes** |
  | `HrvMs` | `int` | **yes** |
  | `Notes` | `nvarchar(1000)` (`maxLength: 1000`) | **yes** |
  | `CreatedAt` | `datetime2` | no |
  | `UpdatedAt` | `datetime2` | no |

- Exactly **one** `migrationBuilder.CreateIndex(...)` call, and it must read:

  ```csharp
  migrationBuilder.CreateIndex(
      name: "IX_DailyWellness_AthleteId_Date",
      table: "DailyWellness",
      columns: new[] { "AthleteId", "Date" },
      unique: true);
  ```

  Two things are load-bearing here: `columns:` (plural, `AthleteId` first) and **`unique: true`**. A
  missing `unique: true` means Step 4b's `.IsUnique()` did not land — go back, do not patch the migration.
  A **second** `CreateIndex` on `AthleteId` alone means an extra index crept into the model — remove it
  from `ApplicationDbContext.cs` and regenerate.
- **Zero** `migrationBuilder.AddForeignKey(...)` calls anywhere in `Up`. This is the single most important
  line-by-line check — a stray FK to `Athletes` would silently violate ADR-0011 §6.
  `table.PrimaryKey("PK_DailyWellness", x => x.Id);` inside the `constraints:` lambda is expected and is
  **not** a foreign key.
- `Down` is exactly one statement — `migrationBuilder.DropTable(name: "DailyWellness");` — and nothing
  else. Compare against `20260726155712_AddActivityFile.cs:47–51`.
- `ApplicationDbContextModelSnapshot.cs` — the tool regenerates it; **commit it as generated, do not
  hand-edit.** Read the diff anyway: it must be purely additive (one new `b.Entity("Bryk.Domain.Entities.DailyWellness", …)`
  block with `HasIndex("AthleteId", "Date").IsUnique()`), with no change to any existing entity block and
  no `HasOne`/`WithMany` relationship added.
- Expected `git diff --stat` at this point: 3 migration files touched (2 new, 1 modified) and no other
  file changed by this step.

**6c. STOP — Sr. Dev / reviewer gate.** Present the generated `Up`/`Down` (or the three files) for
review, together with the checklist results from 6b. Do **not** run `dotnet ef database update` until
sign-off is explicit. This is CLAUDE.md's "Migrations are code-first. Generate, review, get Sr. Dev
approval before applying" and this task's own "generate and read it, do not apply blind".

**6d. Apply**, only after sign-off, from the repo root:

```
dotnet ef database update --project api/Bryk.Infrastructure --startup-project api/Bryk.API
```

**Verify:**
- `dotnet build api/Bryk.sln` green (the migration files compile as ordinary C#; this is a build check,
  not a DB check), still 16 warnings on `--no-incremental`.
- `dotnet ef migrations list --project api/Bryk.Infrastructure --startup-project api/Bryk.API` shows
  `AddDailyWellness` last, and — if the local dev SQL Server is reachable — marked as applied rather than
  `(Pending)`.
- If the local dev DB is reachable, confirm the `DailyWellness` table exists with the twelve columns above
  and a unique index `IX_DailyWellness_AthleteId_Date`; inserting the same `{AthleteId, Date}` twice must
  fail with a duplicate-key error. That is the **only** place the uniqueness constraint is ever exercised
  — it is never a test (ADR-0011 §2).
- If no local dev DB is configured this session, the build-green check plus the by-eye 6b review is the
  gate — do not block the rest of the task on DB reachability, and say so when reporting.
- **If a second migration ever seems necessary — STOP and ask; do not generate one.**

## Step 7 — `Program.cs` (edit — exactly one line)

**File:** `api/Bryk.API/Program.cs`. Insert **directly after L107**
(`builder.Services.AddScoped<IActivityFileRepository, ActivityFileRepository>();`) and **before L108**
(`builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();`):

```csharp
builder.Services.AddScoped<IDailyWellnessRepository, DailyWellnessRepository>();
```

No new `using` is needed — `Bryk.Domain.Interfaces` and `Bryk.Infrastructure.Repositories` are already
imported at the top of the file. **Do not** add a validator registration (there is no validator in this
task, and the assembly scan at L35 would pick one up automatically if there were). **Do not** pre-add
`IWellnessService` or any controller-adjacent option. `Program.cs` is the one file this task shares with
Task 20-2: **20-1 lands first and adds only this line; 20-2 appends the service line** on a fresh working
tree.

**Verify:**
- `dotnet build api/Bryk.sln` green.
- `git diff api/Bryk.API/Program.cs` is **exactly one added line**, nothing else.
- Runtime check that the composition root still boots:
  `dotnet test api/Bryk.sln --filter FullyQualifiedName~GoalsControllerTests` — green. These tests stand
  up the real `Program` through `BrykWebApplicationFactory`, so a broken DI registration fails here even
  though nothing resolves `IDailyWellnessRepository` yet.

## Step 8 — Integration tests: `DailyWellnessRepositoryTests.cs`

**New file** `api/Bryk.API.Tests/Wellness/DailyWellnessRepositoryTests.cs` (**new folder**). The
repository needs a real `DbContext`, and only `Bryk.API.Tests` has an EF provider wired —
`Bryk.Application.Tests` references `Bryk.Application` alone and **must not** gain a project reference
(that is a Sr. Dev slow-down gate: **STOP and ask** if it ever looks necessary). Resolve
`ApplicationDbContext` from `factory.Services.CreateScope()` and construct the repository directly, the
pattern `ActivityFileRepositoryTests` already uses. Derive every date from
`DateOnly.FromDateTime(DateTime.UtcNow)` so the suite does not rot.

```csharp
using Bryk.API.Tests.Fixtures;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using Bryk.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Wellness;

public class DailyWellnessRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByAthleteAndDateTracked_RoundTripsEveryMetric()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date = today.AddDays(-1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);
            await repo.AddAsync(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = date,
                SleepHours = 7.5m,
                SleepQuality = 4,
                RestingHr = 48,
                WeightKg = 72.40m,
                Soreness = 3,
                HrvMs = 88,
                Notes = "slept well"
            });
            await db.SaveChangesAsync();
        }

        // Fresh scope — proves the round trip survives a new DbContext instance, not just the change tracker.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var loaded = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, date);

            loaded.Should().NotBeNull();
            loaded!.Date.Should().Be(date);
            loaded.SleepHours.Should().Be(7.5m);
            loaded.SleepQuality.Should().Be(4);
            loaded.RestingHr.Should().Be(48);
            loaded.WeightKg.Should().Be(72.40m);
            loaded.Soreness.Should().Be(3);
            loaded.HrvMs.Should().Be(88);
            loaded.Notes.Should().Be("slept well");
        }
    }

    [Fact]
    public async Task GetByAthleteAndDateTracked_ReturnsATrackedInstance()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.Add(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = today,
                RestingHr = 50
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var loaded = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, today);
            loaded.Should().NotBeNull();

            // No repo.Update() call: the instance must already be tracked. This is the fact the whole
            // per-day upsert (Task 20-2) rests on — if AsNoTracking() is ever added to the repository
            // read, SaveChangesAsync persists nothing and this test fails.
            loaded!.RestingHr = 44;
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var reloaded = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, today);

            reloaded.Should().NotBeNull();
            reloaded!.RestingHr.Should().Be(44);
        }
    }

    [Fact]
    public async Task GetByAthleteAndDateTracked_ForAnotherAthlete_ReturnsNull()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.Add(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = today,
                SleepHours = 8m
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteAndDateTrackedAsync(Guid.NewGuid(), today);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetByAthleteAndDateTracked_ForADayWithNoEntry_ReturnsNull()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.Add(new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Date = today.AddDays(-5),
                SleepHours = 8m
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteAndDateTrackedAsync(BrykWebApplicationFactory.TestAthleteId, today);

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetByAthleteInRange_IsInclusiveOnBothEndsAndAscending()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Seeded out of order on purpose — the repository, not the insert order, owns the sort.
            db.DailyWellness.AddRange(
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today, RestingHr = 46 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today.AddDays(-3), RestingHr = 47 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today.AddDays(-2), RestingHr = 48 });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteInRangeAsync(
                BrykWebApplicationFactory.TestAthleteId, today.AddDays(-3), today);

            result.Should().HaveCount(3);
            result.Should().BeInAscendingOrder(w => w.Date);
            result[0].Date.Should().Be(today.AddDays(-3));
            result[^1].Date.Should().Be(today);

            // Single-day range: both ends inclusive on the same date.
            var singleDay = await repo.GetByAthleteInRangeAsync(
                BrykWebApplicationFactory.TestAthleteId, today.AddDays(-2), today.AddDays(-2));

            singleDay.Should().ContainSingle();
            singleDay[0].Date.Should().Be(today.AddDays(-2));
        }
    }

    [Fact]
    public async Task GetByAthleteInRange_ExcludesOtherAthletes()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var otherAthleteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DailyWellness.AddRange(
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today.AddDays(-1), HrvMs = 90 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, Date = today, HrvMs = 92 },
                new DailyWellness { Id = Guid.NewGuid(), AthleteId = otherAthleteId, Date = today.AddDays(-1), HrvMs = 50 });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new DailyWellnessRepository(db);

            var result = await repo.GetByAthleteInRangeAsync(
                BrykWebApplicationFactory.TestAthleteId, today.AddDays(-2), today);

            result.Should().HaveCount(2);
            result.Should().OnlyContain(w => w.AthleteId == BrykWebApplicationFactory.TestAthleteId);
        }
    }

    [Fact]
    public async Task GetByAthleteInRange_WithNoEntries_ReturnsEmpty()
    {
        await using var factory = new BrykWebApplicationFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = new DailyWellnessRepository(db);

        var result = await repo.GetByAthleteInRangeAsync(
            BrykWebApplicationFactory.TestAthleteId, today.AddDays(-6), today);

        result.Should().BeEmpty();
    }
}
```

Invariants this file must respect — re-read it against each before moving on:

- **Never assert on `CreatedAt`/`UpdatedAt`.** Those fields are owned by `AuditableEntityInterceptor`
  (registered in `Program.cs`), and `BrykWebApplicationFactory.ConfigureWebHost` (`:73–79`) replaces the
  `AddDbContext` registration with a plain `UseInMemoryDatabase` call that does **not** re-add the
  interceptor. An assertion on them would fail for a reason unrelated to this task.
- **No test asserts that a duplicate `{AthleteId, Date}` insert throws** (ADR-0011 §2). EF InMemory does
  not enforce unique indexes — the factory's own doc comment (`:11–23`) says so — such a test would pass
  for the wrong reason here and fail against SQL Server later. The index is verified by reading the
  migration (Step 6b); the *behaviour* is proven by Task 20-2's service test.
- `GetByAthleteAndDateTracked_ReturnsATrackedInstance` is the guard on the deliberate absence of
  `AsNoTracking()` in `GetByAthleteAndDateTrackedAsync`. If someone "tidies" the repository by adding it,
  this test must be the thing that fails. Do not weaken it by calling `repo.Update(...)`.
- Each `BrykWebApplicationFactory` instance owns one fixed InMemory database name (`:34`), so every
  `CreateScope()` against the **same** factory shares the store: "fresh scope" means a new `DbContext` and
  a new change tracker over the same data — which is exactly the point of the multi-scope structure.
- No controller or service tests in this task — there is no endpoint and no service yet (20-2 adds both).
  State that in the commit body; do not invent a stub host.

**Verify:**
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln --filter FullyQualifiedName~DailyWellnessRepositoryTests
```
Build green, **0 new warnings**. All **7** facts pass by name:
`AddAsync_ThenGetByAthleteAndDateTracked_RoundTripsEveryMetric`,
`GetByAthleteAndDateTracked_ReturnsATrackedInstance`,
`GetByAthleteAndDateTracked_ForAnotherAthlete_ReturnsNull`,
`GetByAthleteAndDateTracked_ForADayWithNoEntry_ReturnsNull`,
`GetByAthleteInRange_IsInclusiveOnBothEndsAndAscending`,
`GetByAthleteInRange_ExcludesOtherAthletes`,
`GetByAthleteInRange_WithNoEntries_ReturnsEmpty`.

**Runtime sanity on the tracked read** (do this once, by hand): temporarily add `.AsNoTracking()` to
`GetByAthleteAndDateTrackedAsync`, re-run the filtered test command, and confirm
`GetByAthleteAndDateTracked_ReturnsATrackedInstance` **fails**. Then remove it and confirm green again.
This proves the guard actually guards. Do not commit the temporary edit.

## Step 9 — Final verification and commit

Run the full command set from `Tasks-20-1.md`:
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build api/Bryk.sln --no-incremental` — 0 errors, **16 warnings**, unchanged from the Step 0
  baseline (14× design-time `System.Security.Cryptography.Xml` NU1903 + the two pre-existing
  `WorkoutsControllerTests.cs:121,150` nullable warnings). An incremental build reporting **14** is the
  same result — compare like for like. **A new warning from a file this task adds is a STOP and ask.**
- `dotnet test api/Bryk.sln` — **350 tests** (343 baseline + the 7 new repository facts), 0 failed.
  `Bryk.Application.Tests` stays at **196** (untouched by this task); `Bryk.API.Tests` rises from **147**
  to **154**.
- `pnpm run build` — green (sanity only; this task touches no UI file).
- `pnpm exec vitest run --no-file-parallelism` — **288 tests / 61 files**, byte-for-byte unchanged. If
  either number moved, something outside this task's scope changed — stop and investigate before
  committing.
- `git add -A && git diff --cached --stat` — confirm **only** these files appear:
  - `md/decisions/0011-wellness-metrics.md` (new)
  - `api/Bryk.Domain/Entities/DailyWellness.cs` (new)
  - `api/Bryk.Domain/Interfaces/IDailyWellnessRepository.cs` (new)
  - `api/Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs` (new)
  - `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` (modified — two additive blocks, additions only)
  - `api/Bryk.Infrastructure/Migrations/<timestamp>_AddDailyWellness.cs` (new)
  - `api/Bryk.Infrastructure/Migrations/<timestamp>_AddDailyWellness.Designer.cs` (new)
  - `api/Bryk.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (regenerated)
  - `api/Bryk.API/Program.cs` (modified — **one** added line)
  - `api/Bryk.API.Tests/Wellness/DailyWellnessRepositoryTests.cs` (new)

  Plus `md/Tasks-20-1.md` and `md/Impl-20-1.md` if they are still untracked — the spec docs travel with
  this commit. **Nothing else.** In particular, if the diff shows `api/Bryk.Domain/Entities/Athlete.cs`,
  `PmcCalculator.cs`, `AcwrCalculator.cs`, `LoadCalculator.cs`, `AnalyticsService.cs`,
  `ExceptionHandlingMiddleware.cs`, any `Bryk.Application/Wellness/*` or `Bryk.API/Controllers/*` file,
  any file under `ui/`, any `.csproj`, or a **second migration** — **STOP**, that is scope creep past
  `Tasks-20-1.md`'s Non-goals fence.
- Spot-check the four fences by eye one last time:
  1. `git diff --cached api/Bryk.API/Program.cs` is exactly one `AddScoped` line — no validator line, no
     service line.
  2. `git diff --cached api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` is additions only, and the
     index reads `entity.HasIndex(e => new { e.AthleteId, e.Date }).IsUnique();`.
  3. `DailyWellness.cs` has no navigation property and every metric is nullable.
  4. `grep` the new test file for `CreatedAt`, `UpdatedAt` and `Throw` — all four must return nothing.
- Commit with the message from `Tasks-20-1.md`. **No AI co-author trailer** — this project omits
  `Co-Authored-By: Claude` from every commit (project convention; it skews GitHub headcount):

```
feat: ADR-0011 + DailyWellness entity, repository and migration

Open Phase 20 with the decisions record. ADR-0011 pins four things the
ROADMAP's Phase 20 entry leaves open or does not raise at all: DailyWellness
is independent of Athlete and a wellness save never writes back to
Athlete.RestingHr or Athlete.WeightKg (neither feeds load, zone or PMC math,
so divergence is harmless, and the only concession is a read-only fallback so
the shipped Resting HR tile never regresses to a dash); HRV does not blend
into TSB, PMC or any readiness score, keeping ADR-0006's calculator pure; the
soreness and sleep-quality inputs generalize RpeSelector into a shared
ScaleSelector rather than duplicating it, with RpeSelector's contract and its
three specs untouched; and DeltaChip is not recoloured - metrics where down is
good report their change in MetricTile's footer slot instead.

The entity is one wide row per athlete per day with every metric nullable,
because partial entries are the norm. Uniqueness of athlete+date is enforced
twice on purpose: a unique composite index in the model, and a read-then-update
upsert in the service (Task 20-2). The index is defence in depth verified by
reading the migration, not by a test - the InMemory provider the integration
suite runs on enforces no unique index, as its own factory doc comment
records, so no test here asserts that a duplicate insert throws.

The repository ships its complete four-method surface up front; the per-day
read is deliberately tracked because the upsert mutates the instance it
returns, and there is a test that fails if AsNoTracking is ever added to it.
One AddScoped line; the service registration is Task 20-2's append to the same
file. Migration AddDailyWellness is generated and reviewed, not applied blind.
```
