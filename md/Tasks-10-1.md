# Task 10-1 — Zones: auto-calc service + `AthleteSportZone` + migration + read API

## Goal
Add the sport-tailored training-zone model from ADR-0004 §1: a `ZoneService` that computes default
zones from each sport's `AthleteSportProfile` thresholds, an `AthleteSportZone` table for per-athlete
overrides, the EF migration, and a read endpoint that returns the current athlete's effective zones
(computed, with overrides applied). Backend only (Domain + Infrastructure + Application + API).

**Generates a migration → Sr. Dev approval before apply (CLAUDE.md). Persistence-boundary change
(new table + superseding `CustomZonesJson`) — flag at review.**

## Depends on
- **ADR-0004 §1** — the binding zone schemes, derivation basis, override table, and the inverse pace math.

## Required reading
- `md/decisions/0004-structured-workout-and-zones.md` §1.
- `api/Bryk.Domain/Entities/AthleteSportProfile.cs` — `ThresholdValue`/`Lt1`/`Lt2` per sport; `CustomZonesJson`.
- `api/Bryk.Application/Onboarding/Validators/EventDtoValidator.cs:14` — `DateOnly.FromDateTime(DateTime.UtcNow)` UTC basis (not date math here, but the UTC convention).
- `api/Bryk.Infrastructure/Repositories/EventRepository.cs` + `Data/ApplicationDbContext.cs` — repo + config patterns.
- `api/Bryk.Application/Profile/ProfileService.cs` — read-only service shape (identity from `ICurrentUserService`, no `IUnitOfWork`).

## Acceptance criteria
- **Enum:** `ZoneMetric { Power = 1, Hr = 2, Pace = 3 }` in `Bryk.Domain.Entities/Enums/`.
- **Entity:** `AthleteSportZone` (ADR-0004 §1 field table) — `IAuditable`, denormalized indexed `AthleteId` (no FK), unique `(AthleteId, Sport, ZoneNumber, Metric)`. `DbSet` + config (precision `(7,2)`, the unique index).
- **Service:** `IZoneService` / `ZoneService` — `GetZonesAsync()` returns the current athlete's zones per sport: bike 7-zone power from FTP, run/swim 5-zone pace from threshold pace (HR secondary), computed via the ADR boundaries, with any `AthleteSportZone` rows overriding the computed value. Strength yields no zones. Pace zones use inverse math (faster = lower seconds) — **unit-tested**.
- **Override write:** an `UpsertZonesAsync(...)` (or per-sport) path staging `AthleteSportZone` rows via repo + single `SaveChangesAsync`; ownership implicit (current athlete). Validators: bounds positive, `Upper > Lower` when both set, zone numbers in range for the sport.
- **API:** `GET /api/v1/zones` (effective zones) and a write endpoint for overrides (`PUT /api/v1/zones` or per-sport). Thin controller, XML summaries.
- **Migration:** `dotnet ef migrations add AddAthleteSportZone` — **additive, new table only**; review Up/Down; do not apply without approval.
- **DI:** register `IZoneService`, `IAthleteSportZoneRepository`.
- **Tests:** ≥4 — bike 7-zone from a known FTP; run pace 5-zone inverse boundaries; override replaces computed; missing threshold → that sport yields no zones (graceful).

## What NOT to modify
- Do not compute load/TSS — Phase 11.
- Do not drop `CustomZonesJson` (stop writing it; a later cleanup drops it).
- Do not touch `WorkoutBlock`/`WorkoutStep` (Task 10-3) or `PlannedWorkout` shapes.
- Do not surface zones in onboarding — config UI is Task 10-2.

## Suggested commit
```
feat: add sport-tailored training zones (auto-calc + overrides)

ZoneService computes Coggan 7-zone power (bike) and 5-zone pace (run/swim)
from AthleteSportProfile thresholds, with per-sport overrides in the new
AthleteSportZone table. GET/PUT /api/v1/zones. Pace zones use inverse
math. Migration additive; reviewed and applied with Sr. Dev approval.
```
