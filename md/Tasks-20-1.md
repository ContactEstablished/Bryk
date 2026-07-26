# Task 20-1 — ADR-0011 + `DailyWellness` entity, repository, DbContext config, migration

## Surface
Backend only. One new ADR (`md/decisions/0011-wellness-metrics.md`), one new domain entity, one
repository contract + implementation, an `ApplicationDbContext` `DbSet` + configuration block with the
**unique composite index on `{ AthleteId, Date }`**, **one reviewed migration (the phase's single Sr. Dev
gate — generate and read it, do not apply blind)**, and **one** `AddScoped` line in `Program.cs`.
No DTO, no validator, no service, no controller, no UI. Nothing in this task is reachable over HTTP;
20-2 wires it up.

## Why
Phase 20's whole surface — the Sleep tile, the Resting HR trend, the weight and HRV tiles, the Today
entry card — is one table away. Everything else in the phase is arithmetic over rows that do not exist
yet, so this task creates them and nothing more. The shape is deliberately dull: one row per athlete per
day, six nullable metrics plus a note, no child tables and no key/value indirection. It carries the one
constraint the phase's headline behaviour depends on — **at most one row per athlete per day** — as a
real unique index, and the ADR lands first because four decisions were taken by the Sr. Dev on
2026-07-26 that the ROADMAP's Phase 20 prose does not state: wellness never writes back to `Athlete`,
HRV never blends into TSB, `RpeSelector` generalizes rather than duplicates, and `DeltaChip` is not
recoloured. A durable record beats a commit message, exactly as ADR-0010 recorded Phase 19's five.

## Depends on
- **ROADMAP Phase 20** (`ROADMAP.md:557–577`) — goal, backend scope, validation ranges, out-of-scope,
  success criteria, the four-task split.
- **ADR-0003 / ADR-0004** — the denormalized-`AthleteId`-with-no-FK convention this entity follows.
- **ADR-0006** — the PMC stays pure; §3 of the new ADR makes "HRV does not enter TSB" binding rather
  than merely recommended.
- **ADR-0010** — the format template (and the precedent that a phase opens with its ADR).
- Nothing in this task depends on 20-2 … 20-4. It lands **first and alone**.

## Required reading
- `ROADMAP.md:557–577` — the Phase 20 entry in full. Note that *Decisions needed* names three open
  questions; all three are resolved and this task's ADR is where they are recorded.
- `md/decisions/0010-activity-file-import.md` — **the ADR format template**: title line, `**Date:**`,
  `**Status:** Accepted (date) — one-sentence summary`, `## Context` with a *Conventions this ADR
  follows* subsection, numbered `## Decision` sections, `## Consequences` with a per-task table,
  `## Alternatives considered`. Match it section-for-section.
- `md/handoffs/2026-07-26-phase-19-complete.md` — the state you are building on (343 xUnit / 288 Vitest,
  16 warnings, one migration per phase, frozen files).
- `api/Bryk.Domain/Entities/Goal.cs` — the entity shape: `Guid Id`, `Guid AthleteId`, scalar props, then
  the two `IAuditable` fields. **This entity omits Goal's `Athlete` nav** — see the ActivityFile
  precedent below.
- `api/Bryk.Domain/Entities/ActivityFile.cs` + `api/Bryk.Domain/Entities/WorkoutStepResult.cs` — the
  header-comment style that names the ADR and explains the denormalized `AthleteId`. Mirror it.
- `api/Bryk.Domain/Entities/Athlete.cs:12–13` — `decimal WeightKg` (non-nullable) and `int? RestingHr`.
  Confirm for yourself that **this task does not touch this file**.
- `api/Bryk.Domain/Interfaces/IGoalRepository.cs` — the contract style: XML `<summary>` on every member,
  `"Uses no-tracking."` / `"Does NOT call SaveChanges."` wording.
- `api/Bryk.Infrastructure/Repositories/GoalRepository.cs` — primary-ctor repository, `AsNoTracking()`
  on display reads, `AddAsync` / `Update` / `Delete` stage only.
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — the `DbSet` block (L13–25); the
  **unique composite index precedent** `entity.HasIndex(e => new { e.AthleteId, e.Sport }).IsUnique();`
  at **L80** (`AthleteSportProfile`) and the four-column unique at **L183** (`AthleteSportZone`); the
  decimal-precision precedent `.HasPrecision(5, 2)` for `Athlete.WeightKg` at **L41**; the
  `Notes` string precedent `HasMaxLength(1000)` at **L88** (`Event`); and the `ActivityFile` block at
  **L233–246**, which is the last block in `OnModelCreating` (the method closes at L247) and carries the
  `// Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).` comment convention.
- `api/Bryk.Infrastructure/Migrations/20260726155712_AddActivityFile.cs` — the most recent migration; the
  exact shape and naming the new one must match (one `CreateTable`, index creations, a `Down` that is a
  single `DropTable`).
- `api/Bryk.API/Program.cs:99–108` — the repositories `AddScoped` block; `IActivityFileRepository` is the
  last entry at **L107**, `IUnitOfWork` follows at L108.
- `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs:11–23` — the factory's own doc comment:
  InMemory has **no unique-index enforcement**. `:73–79` — the InMemory registration does **not** re-add
  `AuditableEntityInterceptor`, so tests must never assert on `CreatedAt`/`UpdatedAt`. `:31–32` —
  `TestAthleteId`.
- `CLAUDE.md` → "When to ask for Sr. Dev approval before proceeding" → *DbContext or data model changes
  that would generate a migration*.

## Acceptance criteria

### 1. `md/decisions/0011-wellness-metrics.md` (new) — write this **first**, before any code

Header:

```
# ADR-0011 — Wellness metrics (storage, double-source policy, analytics boundary)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — `DailyWellness` is one wide row per athlete per day with every
metric nullable, uniquely indexed on `{AthleteId, Date}` and upserted service-side; it is **independent
of `Athlete`** and never writes back to `Athlete.RestingHr`/`WeightKg`; HRV does **not** blend into
TSB/PMC or any readiness score; the soreness/sleep-quality input generalizes `RpeSelector` into a shared
`ScaleSelector` rather than duplicating it; and `DeltaChip` is not recoloured — inverted metrics report
their change in `MetricTile`'s footer slot.
```

`## Context` explains: Phase 20 turns the dashboard's Sleep placeholder into a real tile and gives
Resting HR a history trend, from **manual** daily entry — the honest v1 answer to a placeholder whose
subtitle currently reads *"needs a device or health-app integration."* The ROADMAP's Phase 20 entry
(`ROADMAP.md:557–577`) flags three items under *Decisions needed* (migration approval, HRV-adjusted
readiness blending into TSB, the `RpeSelector` generalization call); all three were resolved by the
Sr. Dev on 2026-07-26, together with a fourth the ROADMAP does not raise — what happens to the two
fields that now have two sources of truth (`Athlete.WeightKg`, `Athlete.RestingHr`). Include a
**### Conventions this ADR follows** subsection stating, grounded in the files above:

- Athlete identity always via `ICurrentUserService`; missing/foreign resources are
  `KeyNotFoundException` → 404. Phase 12 auth stays deferred and approval-gated.
- Errors use the existing middleware contract (`ExceptionHandlingMiddleware`): `ValidationException` →
  400 with `{status, error, errors[], traceId}`, `KeyNotFoundException` → 404,
  `InvalidOperationException` → 409. **No ProblemDetails rework** — Phase 21 owns that.
- Repository pattern; `IUnitOfWork` owns the commit; every write path commits **once**.
- Validation is `await validator.ValidateOrThrowAsync(request, ct)`
  (`Bryk.Application.Common.Validation`), never FluentValidation's `ValidateAndThrowAsync`.
- Field-scoped messages are written explicitly with `.WithMessage("Field: …")`, because
  `ValidateOrThrowAsync` (`ValidationExtensions.cs:16–27`) collects `ErrorMessage` only and drops the
  property name — the convention `ActivityFileUploadRequestValidator.cs:16–28` established.

`## Decision` carries six numbered sections:

**§1 — `DailyWellness` is independent; a wellness write never touches `Athlete`.** Two fields now have
two sources: `Athlete.WeightKg` (`Athlete.cs:12`, non-nullable, precision (5,2)) and
`Athlete.RestingHr` (`Athlete.cs:13`). Record the verified consumer list: `Athlete.WeightKg` is read
only by `OnboardingService` (L35, L48), `OnboardingRequiredRequestValidator:32` and `ProfileService:31`
→ `ProfileRequiredResponse`; `Athlete.RestingHr` is set in the onboarding *recommended* step, surfaced
by `ProfileService:50` → `ProfileRecommendedResponse`, and rendered by `RestingHrCard.vue`. **Neither
feeds any load, zone or PMC calculation**, which is what makes divergence harmless. Decision: the
onboarding/profile values stay exactly as they are (a one-off self-report), wellness rows are the
time series, and **no sync runs in either direction**. The single concession is a **read-only
fallback**: when an athlete has no wellness entries at all, the Resting HR tile displays
`Athlete.RestingHr` so the shipped tile never regresses to `—` (Task 20-4). No fallback for weight —
the weight tile is a trend tile and an onboarding constant is not a trend.

**§2 — One wide, mostly-nullable row per athlete per day.** `DailyWellness` carries `SleepHours`,
`SleepQuality`, `RestingHr`, `WeightKg`, `Soreness`, `HrvMs`, `Notes` — **every metric nullable**,
because partial entries are the norm (an athlete who weighs in but does not own an HRV strap must be
able to log). Uniqueness of `{AthleteId, Date}` is enforced **twice**: a unique composite index in the
model (the `AthleteSportProfile` precedent, `ApplicationDbContext.cs:80`) and, load-bearing, a
service-side read-then-update upsert in Task 20-2. State plainly why both: `BrykWebApplicationFactory`
uses the EF InMemory provider, whose own doc comment (`:11–23`) records that it enforces **no unique
index**, so the index **cannot be proven by an integration test** — it is verified by reading the
generated migration, and the *behaviour* is proven by a service test. **No test may assert that a
duplicate insert throws.** `PUT` replaces the whole day: a metric omitted from the body is cleared, not
preserved. There is **no DELETE endpoint** in v1 (consequence: an all-null day cannot be created and
therefore cannot be reached by clearing — recorded as a known limitation, not a bug).

**§3 — HRV does not blend into TSB, PMC or any readiness score.** ADR-0006 keeps the PMC a pure
function of training load; wellness is context rendered beside it, never an input to it. No
readiness/recovery score, no "should you train today" recommendation, no HRV-adjusted CTL/ATL/TSB in
this phase or as a side effect of it. The ROADMAP already recommends no; this makes it binding and a
parity-doc candidate (`md/product/feature-parity-trainingpeaks.md`).

**§4 — The scale input generalizes; it does not duplicate.** `RpeSelector.vue` (41 lines, a hardcoded
1–10 tap grid) becomes a thin wrapper over a new `ScaleSelector` taking `max` + `labels`; soreness uses
1–10 and sleep quality 1–5. `RpeSelector`'s props/emits contract is unchanged, so
`LogWorkoutForm.vue:252` and the three existing `RpeSelector` specs stay **untouched and passing** —
that is the regression gate on Task 20-3. Record the Tailwind constraint: `grid-cols-10` and
`grid-cols-5` must both appear as **literal** class strings, because an interpolated
`grid-cols-${n}` is invisible to Tailwind's scanner and would silently render a one-column grid.

**§5 — `DeltaChip` is not recoloured; inverted metrics use the footer.** `DeltaChip.vue:8–12` colours
`up` green and `down` red, and `ui/src/lib/weeklyTarget.ts:21–23` carries the standing written
instruction not to "fix" that. For sleep hours and HRV, up is good, so those tiles pass `MetricTile`'s
`delta` prop. For resting HR, weight and soreness, down is good, so those tiles render their 7-day
change in `MetricTile`'s **`#footer` slot** with their own colouring. Result: good news never renders
red, and the chip keeps one meaning across its four existing consumers (`MetricTile.vue:73`,
`ThisWeekCard.vue:92`, `PeaksSection.vue:92`, `FormCard.vue:29`). **No `invert` prop, no new chip.**

**§6 — One migration: `DailyWellness` and nothing else.** Approved: the `DailyWellness` entity + table +
its unique composite index. **Not approved, do not create:** any change to `Athlete` (no
`ICollection<DailyWellness>` nav, no FK, no column), any second table (no `WellnessMetric` key/value
table, no notes child table), any new package. There is **no FK** from `DailyWellness` to `Athlete` —
`AthleteId` is denormalized and covered by the composite index, the convention every entity since
ADR-0003 follows. Any second migration in Phase 20 → **STOP and ask**.

`## Consequences` lists what is closed (all three ROADMAP *Decisions needed* bullets plus the
double-source question) and what is created, with an explicit **"one migration, zero new packages"**
line and this table:

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **20-1** ADR + entity/repo/config/migration | Backend | — | §2 (row shape), §6 (migration scope) |
| **20-2** DTOs + validators + service + controller | Backend | 20-1 | §1 (no write-back), §2 (upsert is the service's job) |
| **20-3** types + service + store + `ScaleSelector` + entry card | Frontend | 20-2 | §4 (wrapper, literal grid classes) |
| **20-4** Sleep/RHR/weight/HRV tiles + dashboard wiring | Frontend | 20-3 | §1 (RHR fallback), §5 (delta vs footer) |

`## Alternatives considered` — at minimum: **writing through to `Athlete.WeightKg`/`RestingHr`** on
every wellness save (rejected, §1 — two write paths to a field consumed by onboarding validation, for no
reader that needs it); **a `WellnessMetric` key/value child table** (rejected, §2 — six fixed metrics
do not need EAV, and it would cost a second table plus a harder day-uniqueness constraint);
**folding wellness onto the workout log** (rejected — wellness is per day, not per session, and a rest
day is exactly when sleep and soreness matter most); **HRV-adjusted TSB / a readiness score** (rejected,
§3 — ADR-0006 keeps the PMC pure and there is no validated model to blend with); **duplicating
`RpeSelector` for soreness** (rejected, §4 — two copies of a tap grid drift); **adding an `invert` prop
to `DeltaChip`** (rejected, §5 — the chip's contract is documented and shared by four consumers);
**device/health sync** (out of scope by ROADMAP lock, not re-litigated).

### 2. `api/Bryk.Domain/Entities/DailyWellness.cs` (new)

```csharp
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
- Header comment in the `ActivityFile.cs` / `WorkoutStepResult.cs:5–8` style, naming ADR-0011 §1/§2/§6
  and stating: one row per athlete per day, uniquely indexed on `{AthleteId, Date}`; **every metric
  nullable** because partial entries are the norm; `AthleteId` is denormalized and indexed with **no
  FK** to `Athlete`; this row is **independent of `Athlete.RestingHr`/`WeightKg`** and never writes to
  them; `CreatedAt`/`UpdatedAt` are owned by `AuditableEntityInterceptor` and **never set manually**.
- **No navigation properties.** Not to `Athlete`, not from `Athlete` — `Athlete.cs` must not appear in
  `git diff`.
- `SleepQuality` (1–5) and `Soreness` (1–10) are plain `int?`, **not** new enums. Bounds are the
  validator's job (Task 20-2); the domain stores the number.
- Property order above is normative — it is the order the DTOs, the migration columns and the UI form
  follow, so the whole phase reads in one sequence.

### 3. `api/Bryk.Domain/Interfaces/IDailyWellnessRepository.cs` (new)

Exactly four members — the complete surface 20-2 consumes, and **no sibling task may extend this file**,
so it ships whole here:

```csharp
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
- The tracked/no-tracking split is the load-bearing detail: the **upsert read must track** (the service
  mutates the entity and commits once), the **range read must not** (it feeds averages and a JSON
  response and never mutates). Say so in the XML docs, not just here.
- **No `GetByIdAsync`, no `Delete`.** Nothing addresses a wellness row by `Id`, and there is no delete
  endpoint in v1 (ADR-0011 §2). Do not ship speculative members.

### 4. `api/Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs` (new)

`public class DailyWellnessRepository(ApplicationDbContext db) : IDailyWellnessRepository` — primary
ctor, matching `GoalRepository`.

- `GetByAthleteAndDateTrackedAsync` →
  `await db.DailyWellness.FirstOrDefaultAsync(w => w.AthleteId == athleteId && w.Date == date, ct)`
  — **no `AsNoTracking()`**, with a one-line comment saying why (the upsert mutates it).
- `GetByAthleteInRangeAsync` → `.AsNoTracking().Where(w => w.AthleteId == athleteId && w.Date >= from &&
  w.Date <= to).OrderBy(w => w.Date).ToListAsync(ct)`.
- `AddAsync` → `await db.DailyWellness.AddAsync(entity, ct)`.
- `Update` → `db.DailyWellness.Update(entity)`.
- No `SaveChangesAsync` anywhere in this file.

### 5. `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` (edit — two additive blocks)

- `DbSet` appended to the block at L13–25:
  ```csharp
  public DbSet<DailyWellness> DailyWellness => Set<DailyWellness>();
  ```
  (Singular property name deliberately — the entity is already a "daily" record and `DailyWellnesses`
  reads badly. The table name follows the `DbSet` name; confirm it in the generated migration.)
- Configuration block appended **after** the `ActivityFile` block (which ends at L246, immediately before
  `OnModelCreating`'s closing brace at L247), in the existing comment style:
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
- Precision rationale, stated in the doc so the reviewer does not have to guess: `WeightKg` is
  **(5,2)**, byte-for-byte the `Athlete.WeightKg` precedent at L41, so the two sources are comparable;
  `SleepHours` is **(4,2)**, enough for quarter-hour granularity inside the 0–16 validator bound;
  `Notes` is **1000**, matching the `Event.Notes` precedent at L88. `SleepQuality`, `RestingHr`,
  `Soreness` and `HrvMs` are `int?` and need no configuration.
- **Do not** add a second, non-unique index on `AthleteId` — the composite index's leading column already
  covers athlete-scoped queries.
- **Do not touch** any other configuration block, in particular `Athlete` (L32–70) and `ActivityFile`
  (L233–246).

### 6. The migration — **approval required before apply**

```
dotnet ef migrations add AddDailyWellness --project api/Bryk.Infrastructure --startup-project api/Bryk.API
```
- Generate it, **read the generated `Up`/`Down` in full, and get Sr. Dev sign-off before
  `dotnet ef database update`** (CLAUDE.md Sr. Dev gate). This is the only migration Phase 20 is
  approved for.
- The `Up` must contain **exactly**: one `CreateTable("DailyWellness")` and **one** `CreateIndex` named
  `IX_DailyWellness_AthleteId_Date` carrying `unique: true`. Confirm the column types read
  `decimal(4,2)` for `SleepHours`, `decimal(5,2)` for `WeightKg`, `nvarchar(1000)` for `Notes`, `int`
  for the four integer metrics (all `nullable: true`), `date` for `Date`, `uniqueidentifier` for `Id`
  and `AthleteId`, `datetime2` for `CreatedAt`/`UpdatedAt`. There must be **no** `AddForeignKey`.
- The `Down` must be a **single `DropTable("DailyWellness")`** and nothing else — compare against
  `20260726155712_AddActivityFile.cs:47–51`.
- If the generated migration touches **any other table** — including `Athletes` — the model has drifted:
  **STOP and ask**. Do not hand-edit the migration to remove the extra operations.
- The snapshot file (`ApplicationDbContextModelSnapshot.cs`) is regenerated by the tool; commit it
  as-is, do not hand-edit. Expect a purely additive diff (one entity, no FK).

### 7. `api/Bryk.API/Program.cs` (edit — exactly one line)

Append inside the repositories block, directly after `IActivityFileRepository` (L107) and before
`IUnitOfWork` (L108):
```csharp
builder.Services.AddScoped<IDailyWellnessRepository, DailyWellnessRepository>();
```
- **Do not** add a validator registration — the assembly scan at `Program.cs:35` picks validators up
  automatically.
- **Do not** pre-add `IWellnessService`. `Program.cs` is the one file shared with Task 20-2:
  **20-1 lands first and adds only this line; 20-2 appends the service line.**

## Non-goals
- **No second migration.** Only `AddDailyWellness`. If anything in this task appears to need another —
  **STOP and ask** (Sr. Dev gate).
- **Do not modify `api/Bryk.Domain/Entities/Athlete.cs`** — no `ICollection<DailyWellness>`, no new
  column, no nullability change to `WeightKg`. It must not appear in `git diff`. If wellness seems to
  need a field on the athlete — **STOP and ask**.
- **No FK** from `DailyWellness` to `Athlete`, and no `HasMany` on `Athlete`.
- **No new NuGet or npm package.** Nothing in this task needs one; if something seems to —
  **STOP and ask** (Sr. Dev gate).
- **No DTO, validator, service, controller or endpoint.** Those are Task 20-2's, including the upsert
  logic itself. This task ships a repository nothing calls yet — expected, not dead code (the same
  shape 19-1 shipped in).
- **No sync to or from `Athlete`** in any direction, at any layer (ADR-0011 §1).
- **No HRV/readiness input to the PMC** — `PmcCalculator.cs`, `AcwrCalculator.cs`, `LoadCalculator.cs`
  and `AnalyticsService.cs` must not appear in `git diff` (ADR-0011 §3).
- **No `ExceptionHandlingMiddleware` change and no ProblemDetails rework** — Phase 21 owns the error
  contract. A middleware change is cross-cutting: **STOP and ask**.
- **No auth code** — Phase 12 stays deferred and approval-gated.
- Do not write files owned by siblings: `api/Bryk.Application/Wellness/*` and
  `api/Bryk.API/Controllers/WellnessController.cs` (20-2); anything under `ui/` (20-3, 20-4).
- No device/health sync (Whoop/Oura/Apple Health), no readiness scores, no
  hydration/nutrition/menstruation fields, no logging reminders or notifications.
- **Do not fix** the two pre-existing nullable warnings in
  `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

`api/Bryk.API.Tests/Wellness/DailyWellnessRepositoryTests.cs` (new folder). The repository needs a
`DbContext`, and only `Bryk.API.Tests` has an EF provider (`Bryk.Application.Tests` references
`Bryk.Application` alone and **must not** gain a project reference — Sr. Dev slow-down gate). Resolve
`ApplicationDbContext` from `factory.Services.CreateScope()`, the pattern `ActivityFileRepositoryTests`
already uses. Derive all dates from `DateOnly.FromDateTime(DateTime.UtcNow)` so the suite does not rot.

- `AddAsync_ThenGetByAthleteAndDateTracked_RoundTripsEveryMetric` — add a row for
  `today.AddDays(-1)` with `SleepHours = 7.5m`, `SleepQuality = 4`, `RestingHr = 48`,
  `WeightKg = 72.40m`, `Soreness = 3`, `HrvMs = 88`, `Notes = "slept well"`; re-read through a **fresh
  scope** and assert all seven round-trip (`SleepHours.Should().Be(7.5m)`,
  `WeightKg.Should().Be(72.40m)`, …). **Do not assert on `CreatedAt`/`UpdatedAt`** — the test factory's
  InMemory registration does not wire `AuditableEntityInterceptor`
  (`BrykWebApplicationFactory.cs:73–79`).
- `GetByAthleteAndDateTracked_ReturnsATrackedInstance` — read the row, set `RestingHr = 44` on the
  returned instance, call `SaveChangesAsync()` on that same context **without** calling `Update`, then
  re-read in a fresh scope and assert `RestingHr.Should().Be(44)`. This is the fact the whole upsert
  depends on; if the repository ever gains an `AsNoTracking()` here, this test fails.
- `GetByAthleteAndDateTracked_ForAnotherAthlete_ReturnsNull` — seed a row for `TestAthleteId` on
  `today`, query `(Guid.NewGuid(), today)` → `Should().BeNull()`.
- `GetByAthleteAndDateTracked_ForADayWithNoEntry_ReturnsNull`.
- `GetByAthleteInRange_IsInclusiveOnBothEndsAndAscending` — seed `today-3`, `today-2`, `today`; query
  `(today-3, today)` → **3** rows with `Should().BeInAscendingOrder(w => w.Date)` and
  `result[0].Date.Should().Be(today.AddDays(-3))`, `result[^1].Date.Should().Be(today)`; then query
  `(today-2, today-2)` → exactly **1** row.
- `GetByAthleteInRange_ExcludesOtherAthletes` — seed one row inside the range for a different athlete id
  → the query for `TestAthleteId` returns only its own rows.
- `GetByAthleteInRange_WithNoEntries_ReturnsEmpty` — `Should().BeEmpty()`, no throw.

**No test asserts that a duplicate `{AthleteId, Date}` insert throws** (ADR-0011 §2 — InMemory does not
enforce unique indexes; the constraint is verified by reading the migration and the behaviour is proven
by Task 20-2's service test). If you find yourself writing one, stop — it will pass for the wrong reason
here and fail against SQL Server later.

No controller/service tests in this task — there is no endpoint and no service yet (20-2 adds both).
State that in the commit body rather than inventing a stub host.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must **rise** from the **343** baseline (196 `Bryk.Application.Tests` + 147 `Bryk.API.Tests`) by
the seven repository facts above, with zero failures — all new tests land in `Bryk.API.Tests`. Vitest
must stay at **exactly 288 / 61 files** — this task touches no UI. Warnings must stay at **16** on a
clean (`--no-incremental`) compile; an incremental build reports 14 because it skips recompiling
`Bryk.API.Tests`, so compare like for like. A new warning from a file this task adds is a **STOP and
ask**.

## Review checklist
- [ ] ADR-0011 exists, is numbered/dated/`Accepted`, matches ADR-0010's section skeleton (including the
      *Conventions this ADR follows* subsection and the per-task consequences table), and records all
      four resolved decisions plus the explicit HRV-into-TSB non-goal.
- [ ] ADR §1 names the verified `Athlete.WeightKg` / `Athlete.RestingHr` consumers and states the
      read-only RHR fallback; §2 states why the unique index cannot be integration-tested; §4 records
      the literal-Tailwind-class trap; §5 quotes the `weeklyTarget.ts:21–23` convention.
- [ ] `DailyWellness` implements `IAuditable`, has **no** navigation properties and **no** FKs; every
      metric is nullable.
- [ ] `ApplicationDbContext` gained exactly one `DbSet` and one configuration block; the index is
      `new { e.AthleteId, e.Date }` with `.IsUnique()`.
- [ ] The generated migration creates **one** table and **one** unique index, nothing else; `Down` is a
      single `DropTable`; it has been **read and approved before apply**.
- [ ] `git diff --stat` shows no change to `Athlete.cs`, any analytics/load calculator, or any file
      under `ui/`.
- [ ] `Program.cs` diff is exactly one `AddScoped` line — no validator line, no service line.
- [ ] The upsert read is tracked and there is a test that fails if `AsNoTracking()` is added to it.
- [ ] No test asserts a duplicate-day insert throws.
- [ ] Warnings still 16 on a clean compile; Vitest still 288 / 61.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
