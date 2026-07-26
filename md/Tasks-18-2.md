# Task 18-2 — `PUT /api/v1/trainingplans/{id}` (plan metadata + the event write path)

## Surface
Backend only. A new write DTO `TrainingPlanUpdateRequest` + `TrainingPlanUpdateRequestValidator`, a new
`ITrainingPlanService.UpdateAsync` + implementation, one additive `[HttpPut("{id:guid}")]` action on the
existing `TrainingPlansController`, and tests on both layers. The repository method it needs
(`ITrainingPlanRepository.Update`) **already exists and is currently unused** — no repository change.
**No migration, no new package, no DI line** (validators are picked up by
`AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()` at `Program.cs:35`).

## Why
Verified gap: `TrainingPlansController` has POST/GET/GET-by-id, planned-workout POST/PUT/DELETE, the
schedule PATCH and the structure GET/PUT — **there is no plan-metadata update at all**. Once a plan is
created its name, dates, methodology, event link and the three periodization fields are frozen forever.
That blocks three things at once: Phase 18's whole point (an athlete must be able to *set*
`BuildWeeks`/`RecoveryWeeks`/`RecoveryWeekPercentage`, which no surface has ever written — the POST
accepts them but the Phase-9 UI deliberately omits them), the plan↔event link that Phase 17 shipped
display-only and explicitly deferred to "Phase 18's plan PUT", and ADR-0008 §2's own escape hatch
("athletes who want to push a workout past the plan end must edit the plan itself — Phase 18's
`PUT /trainingplans/{id}` owns plan-metadata edits including dates"). Shrinking the window is the
dangerous direction, so this endpoint enforces the other half of the invariant ADR-0008 §2 protects
(ADR-0009 §5): a window that would strand planned workouts is rejected, not silently accepted.

## Depends on
- **ADR-0009 §5** (orphan policy = 400) and **§6** (`RecoveryWeekPercentage` percent-scale, 30–90 on
  this validator). ADR-0009 is written in Task 18-1 — read it, but **no code dependency**: this task can
  be implemented in parallel with 18-1 and touches none of its files.
- **ADR-0008 §2** — the reschedule rejection whose message shape and 404-vs-400 split this mirrors.
- **ADR-0003 §1** — the `TrainingPlan` field list and `Event → TrainingPlan` `SetNull` FK.
- **Task 9-3** — `TrainingPlanService` staging discipline (fresh nav-free entity carrying `CreatedAt`)
  and `LoadOwnedPlanAsync` ownership → 404.
- **Phase 17 / Task 17-1** — the plan↔event link was shipped read-only pending this endpoint.
- **Shares `TrainingPlansController.cs` with Task 18-3** — implement 18-2 first, 18-3 second. Do not
  edit that file in two parallel sessions.

## Required reading
- `api/Bryk.Application/Training/TrainingPlanService.cs` — **the template**. Specifically:
  the primary-ctor dependency list (L10–16), `ValidateOrThrowAsync` at the top of each write,
  `LoadOwnedPlanAsync` (L155–164, `KeyNotFoundException` → 404), the fresh-nav-free-entity staging
  comment (L84–99), `RescheduleAsync`'s out-of-window rejection (L118–151) — the exact
  `throw new Exceptions.ValidationException(new[] { "Field: message." })` shape to copy — and the
  private `static TrainingPlanResponse Map(TrainingPlan p)` (L179–194).
- `api/Bryk.Application/Training/ITrainingPlanService.cs` — the XML-doc style for the new method
  (state 400 vs 404 conditions like `RescheduleAsync`'s doc does).
- `api/Bryk.Application/Training/TrainingPlanRequest.cs` + `Validators/TrainingPlanRequestValidator.cs`
  — the shape the new DTO diverges from. **Read-only. Both are frozen.**
- `api/Bryk.Application/Training/TrainingPlanResponse.cs` — already exposes all three periodization
  fields; the PUT returns this unchanged type.
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs:65` — `void Update(TrainingPlan entity)`
  already exists ("Stages an existing TrainingPlan for update. Does NOT call SaveChanges."). Use it.
- `api/Bryk.Domain/Interfaces/IEventRepository.cs:14` — `GetByIdAsync` (no-tracking, entity only), the
  read behind the event-ownership check.
- `api/Bryk.Domain/Entities/TrainingPlan.cs` — field names/types to copy onto the staged entity.
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — thin-controller style, `[HttpPut]` precedent
  at L48, XML `<summary>` on every action.
- `api/Bryk.API/Program.cs:35` (validator assembly scan — no manual registration needed) and
  `:100–120` (the manual `AddScoped` list — **nothing new is needed here**; `IEventRepository` is
  already registered at L101).
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` — the integration harness
  (`BrykWebApplicationFactory`, `JsonOptions` with `JsonStringEnumConverter`, the private `ApiError`
  record with `Errors[]`, the foreign-athlete seeding block at L172–227).
- `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs` — the unit harness
  (`StubCurrentUserService`, `StubUnitOfWork`, `StubTrainingPlanRepository` with its `Updated` capture
  property already in place at L196/L215, and `NewService(...)` at L33–38 which must gain the new ctor
  args).

## Acceptance criteria

### `api/Bryk.Application/Training/TrainingPlanUpdateRequest.cs` (new)
```csharp
public class TrainingPlanUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public MethodologyChoice Methodology { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? EventId { get; set; }
    public int? BuildWeeks { get; set; }
    public int? RecoveryWeeks { get; set; }
    public decimal? RecoveryWeekPercentage { get; set; }
}
```
- **No `PlannedWorkouts`.** Planned workouts are edited through their own endpoints (ADR-0003 aggregate
  boundary); a metadata PUT must never add, replace or delete children.
- Class comment: metadata-only replace-style update; `RecoveryWeekPercentage` is **percent-scale**
  (`60.0` = 60 %, ADR-0009 §6); `EventId = null` clears the link.

### `api/Bryk.Application/Training/Validators/TrainingPlanUpdateRequestValidator.cs` (new)
`AbstractValidator<TrainingPlanUpdateRequest>` with exactly these rules (ADR-0009 §6 / ROADMAP Phase 18
"Validation" line):
- `Name` — `NotEmpty().MaximumLength(200)`.
- `Methodology` — `IsInEnum()`.
- `EndDate` — `GreaterThanOrEqualTo(x => x.StartDate).WithMessage("EndDate must be on or after StartDate.")`.
- `BuildWeeks` — `InclusiveBetween(1, 8).When(x => x.BuildWeeks.HasValue)`.
- `RecoveryWeeks` — `GreaterThanOrEqualTo(1).When(x => x.RecoveryWeeks.HasValue)`.
- `RecoveryWeekPercentage` — `InclusiveBetween(30m, 90m).When(x => x.RecoveryWeekPercentage.HasValue)`.
- **No cross-field "all three or none" rule.** A partial cadence is legal at the API boundary and is
  interpreted by `WeeklyTargetCalculator` as "no cadence" (ADR-0009 §2). Do not add one.
- **No** `EventId` rule here — ownership needs a repository read, so it lives in the service (below).
- Registered automatically by the assembly scan; **do not** add a `Program.cs` line.

### `ITrainingPlanService` / `TrainingPlanService`
- Interface addition, with XML `<summary>` in the existing style:
  ```csharp
  /// <summary>
  /// Replaces an owned plan's metadata (name, methodology, window, event link, periodization fields).
  /// Planned workouts are untouched. 400 when the body is invalid, when the requested window would
  /// strand existing planned workouts (ADR-0009 §5), or when EventId names an event the athlete does
  /// not own; 404 when the plan is missing or foreign.
  /// </summary>
  Task<TrainingPlanResponse> UpdateAsync(Guid id, TrainingPlanUpdateRequest request, CancellationToken ct = default);
  ```
- `TrainingPlanService` primary ctor gains **two** parameters, appended after the existing validators /
  before `planRepo` in a readable order:
  `IValidator<TrainingPlanUpdateRequest> updateValidator` and `IEventRepository eventRepo`.
  Final signature: `(ICurrentUserService currentUser, IValidator<TrainingPlanRequest> planValidator,
  IValidator<PlannedWorkoutDto> plannedWorkoutValidator, IValidator<ScheduleRequest> scheduleValidator,
  IValidator<TrainingPlanUpdateRequest> updateValidator, ITrainingPlanRepository planRepo,
  IEventRepository eventRepo, IUnitOfWork unitOfWork)`.
- `UpdateAsync` body, in this exact order:
  1. `await updateValidator.ValidateOrThrowAsync(request, ct);` — the `Bryk.Application.Common.Validation`
     extension. **Never** FluentValidation's `ValidateAndThrowAsync`.
  2. `var plan = await LoadOwnedPlanAsync(id, ct);` — reuse the existing private helper unchanged
     (missing or foreign → `KeyNotFoundException` → 404).
  3. **Orphan guard (ADR-0009 §5).** Collect
     `stranded = plan.PlannedWorkouts.Where(pw => pw.ScheduledDate < request.StartDate || pw.ScheduledDate > request.EndDate).ToList();`
     When non-empty, throw
     ```csharp
     throw new Exceptions.ValidationException(new[]
     {
         $"PlanWindow: {stranded.Count} planned workout(s) fall outside the requested window " +
         $"({request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}); reschedule or remove them first " +
         $"(earliest {stranded.Min(pw => pw.ScheduledDate):yyyy-MM-dd}, latest {stranded.Max(pw => pw.ScheduledDate):yyyy-MM-dd})."
     });
     ```
     Prefix `PlanWindow:` mirrors `RescheduleAsync`'s `ScheduledDate:` convention so the client can key
     on it. Nothing is staged and `SaveChangesAsync` is not called on this path.
  4. **Event-ownership guard (ADR-0009 / ROADMAP "plan↔event write surface").** When
     `request.EventId is { } eventId`: `var ev = await eventRepo.GetByIdAsync(eventId, ct);` and if
     `ev is null || ev.AthleteId != plan.AthleteId` throw
     `new Exceptions.ValidationException(new[] { "EventId: The selected event does not exist or belongs to another athlete." })`
     → **400, not 404** (the plan exists; the body is wrong). A `null` `EventId` clears the link with no
     read.
  5. Stage a **fresh nav-free** `TrainingPlan` — the loaded `plan` came from a no-tracking `Include`, so
     re-attaching it would drag `PlannedWorkouts` into the change tracker. Carry `Id`, `AthleteId`,
     `CreatedAt` over from `plan`; take `Name`, `Methodology`, `StartDate`, `EndDate`, `EventId`,
     `BuildWeeks`, `RecoveryWeeks`, `RecoveryWeekPercentage` from `request`; leave `PlannedWorkouts`
     empty and both navs unset. Never set `UpdatedAt` (the `AuditableEntityInterceptor` owns it).
     Add the same explanatory comment `UpdatePlannedWorkoutAsync` carries (L84–86).
  6. `planRepo.Update(updated);` then `await unitOfWork.SaveChangesAsync(ct);` — **one** commit.
  7. Build the response from the staged entity and **re-attach the untouched children for the
     projection only**:
     ```csharp
     var response = Map(updated);
     response.PlannedWorkouts = plan.PlannedWorkouts.OrderBy(pw => pw.ScheduledDate).Select(Map).ToList();
     return response;
     ```
     This matters: `Map(updated)` alone would return an **empty** `PlannedWorkouts`, and the UI store
     assigns the PUT response straight onto `currentPlan` (18-4) — the plan's sessions would vanish from
     the screen until a reload. Do not mutate `updated.PlannedWorkouts` after `SaveChangesAsync`.
- Do **not** touch `CreateAsync`, `GetByIdAsync`, `GetByAthleteAsync`, the planned-workout methods, or
  `RescheduleAsync`.

### Controller (`api/Bryk.API/Controllers/TrainingPlansController.cs`)
Additive action, placed directly after `GetByIdAsync`:
```csharp
/// <summary>
/// Replaces a training plan's metadata (name, methodology, dates, target event, periodization
/// fields) for the current athlete. Planned workouts are untouched. 404 if the plan is missing or
/// foreign; 400 if the body is invalid, the new window would strand planned workouts, or the target
/// event is not the athlete's.
/// </summary>
[HttpPut("{id:guid}")]
public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] TrainingPlanUpdateRequest request, CancellationToken cancellationToken)
{
    TrainingPlanResponse result = await trainingPlanService.UpdateAsync(id, request, cancellationToken);
    return Ok(result);
}
```
No try/catch (the global middleware maps `ValidationException` → 400 and `KeyNotFoundException` → 404).
Athlete id never comes from route/query/body. The controller ctor is unchanged.

### Known, accepted divergence (record it, do not "fix" it)
After this task the POST and the PUT validate the periodization fields **differently**: POST allows
`BuildWeeks > 0` (unbounded), `RecoveryWeeks > 0`, `RecoveryWeekPercentage` 0–100; PUT allows 1–8, ≥ 1,
30–90. Tightening the POST would change the accepted request set of a shipped endpoint — an **API
breaking change requiring Sr. Dev approval**, explicitly out of Phase 18 scope. Note the divergence in
the commit body and carry it into the phase handoff as a tech-debt item. If a reviewer asks for
alignment: **STOP and ask** — do not edit `TrainingPlanRequestValidator`.

## Non-goals
- **No migration.** No column, no `ApplicationDbContext` change, no `dotnet ef`. If the task appears to
  need one — **STOP and ask** (Sr. Dev gate).
- **No new NuGet or npm package.**
- **Do not modify `TrainingPlanRequest` or `TrainingPlanRequestValidator`** (frozen — see above).
- **Do not** add, remove or reorder `PlannedWorkout` children on this path — not even to "clean up"
  workouts that fall outside the new window. The 400 is the answer; cascading deletes are not.
- **Do not** change `ITrainingPlanRepository` (its `Update` already exists), add a new repo read, or
  touch `TrainingPlanRepository`.
- **Do not** change `RescheduleAsync`, `ScheduleRequest`, or `ScheduleRequestValidator`.
- **Do not modify** `WeeklyLoadCalculator.cs`, `ComplianceClassifier.cs`, `LoadChart.vue`, or
  `lib/charts/load.ts`.
- **Do not** touch `Bryk.Application/Training/Periodization/` (18-1's folder) — this task does not read
  or reference the calculator.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- **No auth code** — Phase 12 stays deferred and approval-gated; ownership is `ICurrentUserService` +
  the existing `KeyNotFoundException` → 404 pattern, nothing else.
- No frontend in this task (18-4 owns the form), no `DELETE /trainingplans/{id}` (not in Phase 18
  scope even though `ITrainingPlanRepository.Delete` exists), no PATCH-style partial update, no
  optimistic-concurrency token.

## Test expectations

**Unit — `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs` (extend).**
`NewService(...)` gains `new TrainingPlanUpdateRequestValidator()` and a new
`StubEventRepository` (implement `IEventRepository` with `GetByIdAsync` returning a settable
`ToReturn`; every other member `throw new NotImplementedException()`, matching the file's existing stub
style). Add a `ValidUpdate()` factory alongside `ValidPlan()`.
- `UpdateAsync_OwnedPlan_StagesFreshEntityAndCommitsOnce` — asserts `repo.Updated` is non-null, has the
  plan's `Id`/`AthleteId`/`CreatedAt`, the request's `Name`/`StartDate`/`EndDate`/`BuildWeeks = 3`/
  `RecoveryWeeks = 1`/`RecoveryWeekPercentage = 60.0m`, an **empty** `PlannedWorkouts` collection, and
  `uow.SaveCount == 1`.
- `UpdateAsync_ForeignPlan_ThrowsKeyNotFound` — plan with a different `AthleteId` →
  `KeyNotFoundException`, `repo.Updated` null, `uow.SaveCount == 0`.
- `UpdateAsync_WindowWouldStrandPlannedWorkouts_ThrowsValidationWithPlanWindowMessage` — plan with a
  planned workout on `Start + 20`, request window `[Start, Start + 10]` →
  `Bryk.Application.Exceptions.ValidationException` whose `Errors` single entry starts with
  `"PlanWindow:"` and contains `"1 planned workout(s)"`; nothing staged; `SaveCount == 0`.
- `UpdateAsync_WindowExactlyContainsEveryPlannedWorkout_Succeeds` — boundary: workouts on exactly
  `StartDate` and exactly `EndDate` are **not** stranded (inclusive comparison).
- `UpdateAsync_ForeignEventId_ThrowsValidation` — `StubEventRepository.ToReturn` is an `Event` with a
  different `AthleteId` → `ValidationException` starting with `"EventId:"`; `SaveCount == 0`.
- `UpdateAsync_UnknownEventId_ThrowsValidation` — `ToReturn = null` → same `"EventId:"` failure.
- `UpdateAsync_NullEventId_ClearsLinkWithoutReadingTheEventRepository` — asserts the staged entity's
  `EventId` is null and the stub's read was never invoked (add a `ReadCount` counter to the stub).
- `UpdateAsync_EndDateBeforeStartDate_ThrowsValidation` — validator path; nothing staged.
- `UpdateAsync_RecoveryWeekPercentageBelow30_ThrowsValidation` — `29.99m` rejected;
  `UpdateAsync_RecoveryWeekPercentageAt30_And90_Succeed` — `30m` and `90m` accepted (inclusive bounds).
- `UpdateAsync_BuildWeeks9_ThrowsValidation` / `UpdateAsync_BuildWeeks1And8_Succeed` — the 1–8 bounds
  pinned at both ends.
- `UpdateAsync_ResponseKeepsExistingPlannedWorkouts` — plan with two children in the window; the
  returned `TrainingPlanResponse.PlannedWorkouts` has **2** entries ordered by `ScheduledDate`
  (guards the staging-vs-projection trap in criterion 7).

**Integration — `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (extend).**
- `Update_RoundTrips_PersistsPeriodizationFieldsAndSurvivesReload` — POST a plan, PUT a body with
  `buildWeeks = 3`, `recoveryWeeks = 1`, `recoveryWeekPercentage = 60.0`, a new name and a widened
  window → 200 with those values echoed; a follow-up `GET /trainingplans/{id}` returns the same values
  **and** the original planned workouts.
- `Update_NonexistentPlan_Returns404` — random `Guid` → 404.
- `Update_ForeignPlan_Returns404` — reuse the existing foreign-athlete seeding block (L172–227).
- `Update_WindowShrinkStrandingPlannedWorkouts_Returns400WithPlanWindowError` — the seeded plan's
  workouts sit at `Start + 1` / `Start + 2`; PUT `[Start + 10, Start + 20]` → 400 and
  `errorBody.Errors.Should().Contain(e => e.StartsWith("PlanWindow:"))`.
- `Update_ForeignEventId_Returns400WithEventIdError` — seed a second athlete + their event through the
  `ApplicationDbContext` (same pattern as the foreign-plan test), PUT that `eventId` → 400 with an
  `"EventId:"` error.
- `Update_OwnEventId_LinksThePlan` — `POST /events` then PUT with its id → 200 with `eventId` echoed;
  `GET /api/v1/events` shows the plan in that event's `linkedPlans` (proves the Phase-17 read path now
  has a live write path).
- `Update_RecoveryWeekPercentageOutOfBounds_Returns400` — `20.0` → 400.
- `Update_DoesNotAlterPlannedWorkouts` — PUT with a valid body → the subsequent GET's
  `plannedWorkouts` count, ids and `scheduledDate`s are unchanged.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must rise from the **201** baseline with zero failures; Vitest stays at **229 / 53 files**
(no UI change). Warning count must not grow past the known 16.

## Review checklist
- [ ] `PUT /api/v1/trainingplans/{id}` returns 200 with the full `TrainingPlanResponse` **including**
      the plan's existing planned workouts.
- [ ] Exactly one `SaveChangesAsync` on the happy path; zero on every rejection path.
- [ ] The staged entity is nav-free, carries `CreatedAt`, and never sets `UpdatedAt`.
- [ ] `ValidateOrThrowAsync` (not `ValidateAndThrowAsync`) is used, at the top of the method.
- [ ] Orphan rejection is 400 with a `PlanWindow:`-prefixed message naming the count and the range;
      foreign/unknown event is 400 with an `EventId:`-prefixed message; missing/foreign plan is 404.
- [ ] Window containment is **inclusive** on both ends.
- [ ] `TrainingPlanRequest`/`TrainingPlanRequestValidator` are untouched in `git diff`; the
      POST/PUT bounds divergence is called out in the commit body.
- [ ] No `Program.cs` diff, no `ITrainingPlanRepository` diff, no migration file.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat: plan-metadata PUT with orphan + event-ownership guards

Close a verified gap: training plans had no metadata update, so a plan's
name, window, methodology, event link and the three ADR-0003 periodization
columns were frozen at creation. PUT /api/v1/trainingplans/{id} takes a new
metadata-only TrainingPlanUpdateRequest (no planned workouts - children stay
on their own endpoints) validated at 1-8 build weeks, >=1 recovery weeks and
a 30-90 percent recovery volume (ADR-0009 6: the field is percent-scale, not
the fraction the ROADMAP prose claims).

Two guards beyond the validator: a window that would leave existing planned
workouts outside [StartDate, EndDate] is rejected 400 with a PlanWindow:
message naming the count and range - ADR-0008 2's reschedule rule applied to
the other side of the same invariant (ADR-0009 5) - and an EventId that names
an event the athlete does not own is rejected 400. This is also the write
path Phase 17 deferred, so the plan-to-event link is finally editable.

Reuses the existing unused ITrainingPlanRepository.Update and the fresh
nav-free staging discipline; the response re-attaches the untouched children
for projection so the client's plan view keeps its sessions. No migration, no
new package, no DI change. Known accepted divergence: the POST validator
keeps its looser bounds (tightening a shipped endpoint is a breaking change).
xUnit covers the round-trip, 404 foreign plan, 400 orphan and foreign event,
the inclusive window boundary and the periodization bounds at both ends.
```
