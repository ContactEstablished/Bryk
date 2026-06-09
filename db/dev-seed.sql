/*───────────────────────────────────────────────────────────────────────────
  Bryk — dev seed data (re-runnable reset + reseed for ONE dev athlete)
  ----------------------------------------------------------------------------
  Target : SQL Server, database `Bryk`.
  Purpose: wipe and rebuild a rich, coherent dataset for the dev athlete so every
           screen reads end-to-end — dashboard (Weekly Load / This Week with
           per-session TSS / Recent Activity / Primary Goal / Resting HR),
           /zones (computed + a Customized sport), the /training structured-workout
           builder, and planned-vs-actual.

  How to run:
    1. Paste your DevAuth:CurrentAthleteId below (dotnet user-secrets list).
    2. sqlcmd -S <server> -d Bryk -f 65001 -i db/dev-seed.sql   (-f 65001 = UTF-8,
       so titles like "Bike — Threshold 4×8'" read correctly; or paste into SSMS / ADS).
    3. Safe to re-run anytime — it resets THIS athlete only, then reseeds.

  Notes:
    - Tables = EF DbSet names; columns = entity property names (EF defaults).
    - Several tables carry a denormalized AthleteId with NO FK, so deleting the
      Athlete row does NOT cascade to them — the reset deletes each explicitly,
      child -> parent (see RESET section).
    - The AuditableEntityInterceptor does NOT run for raw SQL, so CreatedAt /
      UpdatedAt are set manually to @Now on every row.
    - Dates are RELATIVE to today (Monday-based UTC week, matching ThisWeekService)
      so "this week" and "recent" always land in range whenever you run it.
    - Enums are 1-based ints:
        Sport      Swim=1 Bike=2 Run=3 Triathlon=4 Strength=5
        Gender     Male=1 Female=2 Other=3 PreferNotToSay=4
        Methodology Pyramidal=1 Periodization=2 Polarized=3 Norwegian=4
        StepIntent Warmup=1 Work=2 Recovery=3 Cooldown=4 Rest=5
        ZoneMetric Power=1 Hr=2 Pace=3
        EventPriority A=1 B=2 C=3
        GoalType   General=1 EventDriven=2
        EquipmentType PowerMeter=1 HrStrap=2 GpsWatch=3 LactateMeter=4 Trainer=5 Other=6
        TriathlonDistance Sprint=1 Olympic=2 HalfIronman=3 Ironman=4 Custom=5
───────────────────────────────────────────────────────────────────────────*/

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- any error aborts and rolls back the whole transaction

----------------------------------------------------------------------------
-- Parameters
----------------------------------------------------------------------------
DECLARE @AthleteId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- <<< PASTE DevAuth:CurrentAthleteId
DECLARE @Today     DATE      = CAST(SYSUTCDATETIME() AS DATE);  -- relative anchor
DECLARE @Now       DATETIME2 = SYSUTCDATETIME();                -- audit columns

-- Monday of the current week, DATEFIRST-independent (1900-01-01 was a Monday),
-- matching ThisWeekService: start = today - ((DayOfWeek + 6) % 7).
DECLARE @WeekStart DATE = DATEADD(DAY, -(DATEDIFF(DAY, '19000101', @Today) % 7), @Today);

IF @AthleteId = '00000000-0000-0000-0000-000000000000'
BEGIN
    RAISERROR('Set @AthleteId to your DevAuth:CurrentAthleteId (dotnet user-secrets list) before running.', 16, 1);
    RETURN;
END

----------------------------------------------------------------------------
-- Id variables (parents held so children can reference them)
----------------------------------------------------------------------------
DECLARE @Event_A  UNIQUEIDENTIFIER = NEWID();   -- A-priority race (drives the plan + primary goal)
DECLARE @Event_B  UNIQUEIDENTIFIER = NEWID();
DECLARE @Event_C  UNIQUEIDENTIFIER = NEWID();
DECLARE @Plan     UNIQUEIDENTIFIER = NEWID();

-- Planned workouts referenced later (by structure blocks and/or completed workouts)
DECLARE @PW_MonBike  UNIQUEIDENTIFIER = NEWID();  -- current week (structured)
DECLARE @PW_TueRun   UNIQUEIDENTIFIER = NEWID();  -- current week (structured)
DECLARE @PW_WedSwim  UNIQUEIDENTIFIER = NEWID();  -- current week (structured)
DECLARE @PW_ThuStr   UNIQUEIDENTIFIER = NEWID();  -- current week (structured)
DECLARE @PW_W2Bike   UNIQUEIDENTIFIER = NEWID();  -- week -2 (structured, has actuals)
DECLARE @PW_W2Run    UNIQUEIDENTIFIER = NEWID();  -- week -2 (simple)
DECLARE @PW_W1Bike   UNIQUEIDENTIFIER = NEWID();  -- week -1 (structured, has actuals)
DECLARE @PW_W1Run    UNIQUEIDENTIFIER = NEWID();  -- week -1 (simple)
DECLARE @PW_W1Swim   UNIQUEIDENTIFIER = NEWID();  -- week -1 (simple)
DECLARE @PW_W1Str    UNIQUEIDENTIFIER = NEWID();  -- week -1 (simple)
DECLARE @PW_W1Ride   UNIQUEIDENTIFIER = NEWID();  -- week -1 (simple)

-- Reusable block id (reassigned before each block; steps insert immediately after)
DECLARE @Blk UNIQUEIDENTIFIER;

-- Planned step ids needed by WorkoutStepResults (planned-vs-actual)
DECLARE @P2_Warm UNIQUEIDENTIFIER = NEWID(), @P2_Work UNIQUEIDENTIFIER = NEWID(),
        @P2_Rec  UNIQUEIDENTIFIER = NEWID(), @P2_Cool UNIQUEIDENTIFIER = NEWID();
DECLARE @P3_Warm UNIQUEIDENTIFIER = NEWID(), @P3_Work UNIQUEIDENTIFIER = NEWID(),
        @P3_Rec  UNIQUEIDENTIFIER = NEWID(), @P3_Cool UNIQUEIDENTIFIER = NEWID();

-- Completed workouts that carry step results
DECLARE @W_A UNIQUEIDENTIFIER = NEWID();   -- week -2 bike threshold (actuals)
DECLARE @W_C UNIQUEIDENTIFIER = NEWID();   -- week -1 bike VO2 (actuals)

BEGIN TRANSACTION;

----------------------------------------------------------------------------
-- 1. RESET  (delete child -> parent, scoped to this athlete)
----------------------------------------------------------------------------
DELETE FROM WorkoutStepResults   WHERE AthleteId = @AthleteId;
DELETE FROM Workouts             WHERE AthleteId = @AthleteId;
DELETE FROM WorkoutSteps         WHERE AthleteId = @AthleteId;
DELETE FROM WorkoutBlocks        WHERE AthleteId = @AthleteId;
DELETE FROM AthleteSportZones    WHERE AthleteId = @AthleteId;
DELETE FROM PlannedWorkouts      WHERE AthleteId = @AthleteId;
DELETE FROM TrainingPlans        WHERE AthleteId = @AthleteId;
DELETE FROM Goals                WHERE AthleteId = @AthleteId;
DELETE FROM Events               WHERE AthleteId = @AthleteId;
DELETE FROM Equipment            WHERE AthleteId = @AthleteId;
DELETE FROM AthleteSportProfiles WHERE AthleteId = @AthleteId;
DELETE FROM Athletes             WHERE Id        = @AthleteId;

----------------------------------------------------------------------------
-- 2. RESEED
----------------------------------------------------------------------------

-- 2.1 Athlete (fully onboarded) ------------------------------------------------
INSERT INTO Athletes
    (Id, Name, Gender, DateOfBirth, HeightCm, WeightKg, Methodology, RestingHr, MaxHr, YearsTraining, TypicalWeeklyHours, CreatedAt, UpdatedAt)
VALUES
    (@AthleteId, N'Alex Rivera', 1, '1990-04-12', 180.00, 74.50, 3 /*Polarized*/, 48, 190, 6, 10.5, @Now, @Now);

-- 2.2 Sport profiles (thresholds + LT split so HR-load + zones compute) ---------
--     Bike ThresholdValue = FTP (W); Run = threshold pace (sec/km);
--     Swim = threshold pace (sec/100m); Lt2 doubles as threshold-HR for HR-load.
INSERT INTO AthleteSportProfiles
    (Id, AthleteId, Sport, IsActive, ThresholdValue, Lt1, Lt2, CustomZonesJson, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, 2 /*Bike*/,     1, 250.00, 135.0, 165.0, NULL, @Now, @Now),
    (NEWID(), @AthleteId, 3 /*Run*/,      1, 255.00, 140.0, 170.0, NULL, @Now, @Now),  -- 4:15/km
    (NEWID(), @AthleteId, 1 /*Swim*/,     1,  95.00, 130.0, 160.0, NULL, @Now, @Now),  -- 1:35/100m
    (NEWID(), @AthleteId, 5 /*Strength*/, 1, NULL,   NULL,  NULL,  NULL, @Now, @Now);

-- 2.3 Events (A race ~8wk out drives the plan + primary goal; plus B and C) -----
INSERT INTO Events
    (Id, AthleteId, Name, EventDate, Priority, Sport, TriathlonDistance, CustomDistanceName, Notes, CreatedAt, UpdatedAt)
VALUES
    (@Event_A, @AthleteId, N'Indian Wells 70.3', DATEADD(DAY, 56, @Today), 1 /*A*/, 4 /*Triathlon*/, 3 /*HalfIronman*/, NULL, N'Goal race for the block.', @Now, @Now),
    (@Event_B, @AthleteId, N'Spring Gran Fondo', DATEADD(DAY, 28, @Today), 2 /*B*/, 2 /*Bike*/,      NULL,              NULL, N'Tune-up century.',        @Now, @Now),
    (@Event_C, @AthleteId, N'Saturday Parkrun',  DATEADD(DAY, 12, @Today), 3 /*C*/, 3 /*Run*/,       NULL,              NULL, N'5k time trial.',          @Now, @Now);

-- 2.4 Goals (one event-driven, one general) ------------------------------------
INSERT INTO Goals
    (Id, AthleteId, Type, Description, TargetDate, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, 2 /*EventDriven*/, N'Break 5:30 at Indian Wells 70.3', DATEADD(DAY, 56, @Today), @Now, @Now),
    (NEWID(), @AthleteId, 1 /*General*/,     N'Raise bike FTP to 270 W',         DATEADD(DAY, 90, @Today), @Now, @Now);

-- 2.5 Equipment ----------------------------------------------------------------
INSERT INTO Equipment
    (Id, AthleteId, Type, Sport, Notes, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, 1 /*PowerMeter*/, 2 /*Bike*/, N'Favero Assioma Duo',     @Now, @Now),
    (NEWID(), @AthleteId, 2 /*HrStrap*/,    NULL,       N'Polar H10',              @Now, @Now),
    (NEWID(), @AthleteId, 3 /*GpsWatch*/,   NULL,       N'Garmin Forerunner 965',  @Now, @Now);

-- 2.6 Zone overrides — Bike power (Metric=Power=1) so /zones shows "Customized" --
--     Absolute watts, personalised from FTP 250 (computed Coggan bands differ, so
--     IsOverride lights up). Z7 is open-ended (UpperBound NULL).
INSERT INTO AthleteSportZones
    (Id, AthleteId, Sport, ZoneNumber, Metric, LowerBound, UpperBound, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, 2, 1, 1,   0.00, 140.00, @Now, @Now),
    (NEWID(), @AthleteId, 2, 2, 1, 140.00, 190.00, @Now, @Now),
    (NEWID(), @AthleteId, 2, 3, 1, 190.00, 228.00, @Now, @Now),
    (NEWID(), @AthleteId, 2, 4, 1, 228.00, 265.00, @Now, @Now),
    (NEWID(), @AthleteId, 2, 5, 1, 265.00, 300.00, @Now, @Now),
    (NEWID(), @AthleteId, 2, 6, 1, 300.00, 375.00, @Now, @Now),
    (NEWID(), @AthleteId, 2, 7, 1, 375.00, NULL,   @Now, @Now);

-- 2.7 Training plan (active, ~8 weeks: starts week -2, ends +6wk; linked to A) ---
INSERT INTO TrainingPlans
    (Id, AthleteId, Name, StartDate, EndDate, Methodology, EventId, BuildWeeks, RecoveryWeeks, RecoveryWeekPercentage, CreatedAt, UpdatedAt)
VALUES
    (@Plan, @AthleteId, N'Indian Wells 70.3 Build',
     DATEADD(DAY, -14, @WeekStart), DATEADD(DAY, 42, @WeekStart),
     3 /*Polarized*/, @Event_A, 3, 1, 70.00, @Now, @Now);

----------------------------------------------------------------------------
-- 2.8 Planned workouts
--     Current-week structured ones leave PlannedLoad NULL so the engine's
--     ComputedLoad drives EffectiveLoad (the Phase 11 headline). Simple ones
--     carry an explicit PlannedLoad (no structure -> computed is null).
----------------------------------------------------------------------------

-- ---- CURRENT WEEK ----------------------------------------------------------

-- Mon: Bike threshold 4x8'  (structured; computed ~73 TSS) -------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_MonBike, @AthleteId, @Plan, 2, @WeekStart, N'Bike — Threshold 4×8''', N'4×8 min at threshold, 2 min easy between.', 70, NULL, @Now, @Now);

SET @Blk = NEWID();  -- warm-up
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_MonBike, 0, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 1 /*Warmup*/,   N'Warm-up',  600, NULL, 2, 140, 170, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();  -- 4× ( work + recovery )
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_MonBike, 1, 4, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 2 /*Work*/,     N'Threshold', 480, NULL, 4, 250, 270, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now),
       (NEWID(), @AthleteId, @Blk, 1, 3 /*Recovery*/, N'Easy',      120, NULL, 1, 120, 140, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();  -- cool-down
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_MonBike, 2, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 4 /*Cooldown*/, N'Cool-down', 600, NULL, 1, 120, 150, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

-- Tue: Aerobic Run  (structured; pace targets; computed ~63 TSS) -------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_TueRun, @AthleteId, @Plan, 3, DATEADD(DAY, 1, @WeekStart), N'Run — Aerobic 60''', N'Steady aerobic run, Z2 main set.', 60, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_TueRun, 0, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 1 /*Warmup*/,   N'Warm-up',  600, NULL, 1, NULL, NULL, NULL, NULL, 340, 360, NULL, NULL, NULL, NULL, @Now, @Now),
       (NEWID(), @AthleteId, @Blk, 1, 2 /*Work*/,     N'Aerobic', 2400, NULL, 2, NULL, NULL, NULL, NULL, 300, 315, NULL, NULL, NULL, NULL, @Now, @Now),
       (NEWID(), @AthleteId, @Blk, 2, 4 /*Cooldown*/, N'Cool-down', 600, NULL, 1, NULL, NULL, NULL, NULL, 345, 365, NULL, NULL, NULL, NULL, @Now, @Now);

-- Wed: Swim threshold 8×100  (structured; distance-only work; computed ~33 TSS) -
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_WedSwim, @AthleteId, @Plan, 1, DATEADD(DAY, 2, @WeekStart), N'Swim — 8×100 Threshold', N'Warm-up, 8×100 at threshold pace, cool-down.', 45, NULL, @Now, @Now);

SET @Blk = NEWID();  -- warm-up (duration)
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_WedSwim, 0, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 1 /*Warmup*/, N'Warm-up', 600, NULL, 1, NULL, NULL, NULL, NULL, 130, 140, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();  -- 8× 100m (distance-only, exactly one of duration/distance)
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_WedSwim, 1, 8, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 2 /*Work*/, N'100 @ threshold', NULL, 100, 4, NULL, NULL, NULL, NULL, 90, 100, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();  -- cool-down (duration)
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_WedSwim, 2, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 4 /*Cooldown*/, N'Cool-down', 300, NULL, 1, NULL, NULL, NULL, NULL, 130, 145, NULL, NULL, NULL, NULL, @Now, @Now);

-- Thu: Lower-body strength  (structured; sets/reps/load; computed ~47 TSS) ----
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_ThuStr, @AthleteId, @Plan, 5, DATEADD(DAY, 3, @WeekStart), N'Strength — Lower Body', N'Squat / RDL / lunge / calf raise.', 50, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_ThuStr, 0, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (NEWID(), @AthleteId, @Blk, 0, 2 /*Work*/, N'Back Squat',      NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, 6,  100.00, 7.0, @Now, @Now),
       (NEWID(), @AthleteId, @Blk, 1, 2 /*Work*/, N'Romanian Deadlift', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3, 8,  80.00, 7.0, @Now, @Now),
       (NEWID(), @AthleteId, @Blk, 2, 2 /*Work*/, N'Walking Lunge',    NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3, 10, 30.00, 6.0, @Now, @Now),
       (NEWID(), @AthleteId, @Blk, 3, 2 /*Work*/, N'Calf Raise',       NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3, 12, 40.00, 6.0, @Now, @Now);

-- Fri: Endurance ride (simple; explicit PlannedLoad) -------------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, @Plan, 2, DATEADD(DAY, 4, @WeekStart), N'Bike — Endurance 90''', N'Steady Z2 endurance ride.', 90, 75.00, @Now, @Now);

-- Sat: Long run (simple; explicit PlannedLoad) -------------------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, @Plan, 3, DATEADD(DAY, 5, @WeekStart), N'Run — Long 90''', N'Long aerobic run.', 90, 90.00, @Now, @Now);

-- ---- WEEK -2 (has completed actuals) ---------------------------------------

-- Bike threshold (structured; step ids held for actuals) ---------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_W2Bike, @AthleteId, @Plan, 2, DATEADD(DAY, -14, @WeekStart), N'Bike — Threshold 4×8''', N'Threshold intervals.', 70, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_W2Bike, 0, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (@P2_Warm, @AthleteId, @Blk, 0, 1 /*Warmup*/, N'Warm-up', 600, NULL, 2, 140, 170, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_W2Bike, 1, 4, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (@P2_Work, @AthleteId, @Blk, 0, 2 /*Work*/,     N'Threshold', 480, NULL, 4, 250, 270, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now),
       (@P2_Rec,  @AthleteId, @Blk, 1, 3 /*Recovery*/, N'Easy',      120, NULL, 1, 120, 140, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_W2Bike, 2, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (@P2_Cool, @AthleteId, @Blk, 0, 4 /*Cooldown*/, N'Cool-down', 600, NULL, 1, 120, 150, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

-- Long run (simple) ----------------------------------------------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_W2Run, @AthleteId, @Plan, 3, DATEADD(DAY, -9, @WeekStart), N'Run — Long 90''', N'Long aerobic run.', 90, 90.00, @Now, @Now);

-- ---- WEEK -1 (has completed actuals) ---------------------------------------

-- Bike VO2 5×3' (structured; step ids held for actuals) ----------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_W1Bike, @AthleteId, @Plan, 2, DATEADD(DAY, -7, @WeekStart), N'Bike — VO2 5×3''', N'5×3 min VO2max, 2 min easy.', 60, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_W1Bike, 0, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (@P3_Warm, @AthleteId, @Blk, 0, 1 /*Warmup*/, N'Warm-up', 600, NULL, 2, 140, 170, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_W1Bike, 1, 5, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (@P3_Work, @AthleteId, @Blk, 0, 2 /*Work*/,     N'VO2max', 180, NULL, 5, 270, 300, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now),
       (@P3_Rec,  @AthleteId, @Blk, 1, 3 /*Recovery*/, N'Easy',   120, NULL, 1, 120, 150, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

SET @Blk = NEWID();
INSERT INTO WorkoutBlocks (Id, AthleteId, PlannedWorkoutId, OrderIndex, Repeats, CreatedAt, UpdatedAt)
VALUES (@Blk, @AthleteId, @PW_W1Bike, 2, 1, @Now, @Now);
INSERT INTO WorkoutSteps (Id, AthleteId, WorkoutBlockId, OrderIndex, Intent, Title, DurationSeconds, DistanceMeters, TargetZone, TargetPowerLow, TargetPowerHigh, TargetHrLow, TargetHrHigh, TargetPaceLow, TargetPaceHigh, Sets, Reps, LoadKg, Rpe, CreatedAt, UpdatedAt)
VALUES (@P3_Cool, @AthleteId, @Blk, 0, 4 /*Cooldown*/, N'Cool-down', 600, NULL, 1, 120, 150, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, @Now, @Now);

-- Tempo run / Swim / Strength / Long ride (simple) ---------------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (@PW_W1Run,  @AthleteId, @Plan, 3, DATEADD(DAY, -5, @WeekStart), N'Run — Tempo 45''',       N'Tempo run.',            45, 60.00, @Now, @Now),
    (@PW_W1Swim, @AthleteId, @Plan, 1, DATEADD(DAY, -4, @WeekStart), N'Swim — Endurance 50''',  N'Aerobic swim.',         50, 52.00, @Now, @Now),
    (@PW_W1Str,  @AthleteId, @Plan, 5, DATEADD(DAY, -3, @WeekStart), N'Strength — Full Body',   N'Full-body strength.',   50, 50.00, @Now, @Now),
    (@PW_W1Ride, @AthleteId, @Plan, 2, DATEADD(DAY, -2, @WeekStart), N'Bike — Long Endurance',  N'Long endurance ride.', 150, 120.00, @Now, @Now);

-- ---- WEEK +1 (future; simple) ----------------------------------------------
INSERT INTO PlannedWorkouts
    (Id, AthleteId, TrainingPlanId, Sport, ScheduledDate, Title, Description, PlannedDurationMinutes, PlannedLoad, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @AthleteId, @Plan, 2, DATEADD(DAY, 7, @WeekStart), N'Bike — Sweet Spot 3×15''', N'Sweet-spot intervals.',  75, 80.00, @Now, @Now),
    (NEWID(), @AthleteId, @Plan, 3, DATEADD(DAY, 9, @WeekStart), N'Run — Intervals 6×800',    N'6×800 at 5k effort.',     60, 70.00, @Now, @Now);

----------------------------------------------------------------------------
-- 2.9 Completed workouts (actuals -> Recent Activity + actual-load reads)
--     ComputedLoad is the stored actual TSS (the app computes it at log time and
--     persists it; reads don't recompute). EffectiveLoad = LoadOverride ?? ComputedLoad.
----------------------------------------------------------------------------
INSERT INTO Workouts
    (Id, AthleteId, PlannedWorkoutId, Sport, CompletedDate, ActualDurationSeconds, ActualDistanceMeters, AvgHr, MaxHr, ComputedLoad, LoadOverride, Rpe, Notes, CreatedAt, UpdatedAt)
VALUES
    -- linked to planned (planned-vs-actual)
    (@W_A,    @AthleteId, @PW_W2Bike, 2, DATEADD(DAY, -14, @WeekStart), 3600, NULL,  158, 176, 74.20, NULL,   6.0, N'Held all four intervals.',     @Now, @Now),
    (NEWID(), @AthleteId, @PW_W2Run,  3, DATEADD(DAY,  -9, @WeekStart), 5400, 16000, 150, 168, 92.00, 100.00, 7.0, N'Long run, legs heavy late.',   @Now, @Now), -- LoadOverride demo
    (@W_C,    @AthleteId, @PW_W1Bike, 2, DATEADD(DAY,  -7, @WeekStart), 3300, NULL,  162, 184, 84.50, NULL,   8.0, N'VO2 set, last rep hurt.',      @Now, @Now),
    (NEWID(), @AthleteId, @PW_W1Run,  3, DATEADD(DAY,  -5, @WeekStart), 2700, 9000,  165, 178, 61.30, NULL,   7.0, N'Tempo on the river path.',     @Now, @Now),
    (NEWID(), @AthleteId, @PW_W1Swim, 1, DATEADD(DAY,  -4, @WeekStart), 3000, 2200,  144, 158, 52.10, NULL,   5.0, N'Easy aerobic swim.',           @Now, @Now),
    (NEWID(), @AthleteId, @PW_W1Str,  5, DATEADD(DAY,  -3, @WeekStart), 3000, NULL,  NULL, NULL, 57.75, NULL,  7.0, N'Full-body, felt strong.',      @Now, @Now), -- strength sRPE load
    (NEWID(), @AthleteId, @PW_W1Ride, 2, DATEADD(DAY,  -2, @WeekStart), 9000, 65000, 142, 160, 118.40, NULL,  6.0, N'Long endurance ride.',         @Now, @Now),
    -- unlinked recent
    (NEWID(), @AthleteId, NULL,       3, DATEADD(DAY,  -2, @Today),     2400, 7000,  138, 150, 36.80, NULL,   4.0, N'Recovery jog.',                @Now, @Now),
    (NEWID(), @AthleteId, NULL,       2, DATEADD(DAY,  -1, @Today),     2700, NULL,  128, 142, 38.50, NULL,   3.0, N'Easy spin to flush legs.',     @Now, @Now);

----------------------------------------------------------------------------
-- 2.10 Per-step actuals on the two bike sessions (planned-vs-actual detail)
----------------------------------------------------------------------------
INSERT INTO WorkoutStepResults
    (Id, AthleteId, WorkoutId, WorkoutStepId, OrderIndex, ActualDurationSeconds, ActualDistanceMeters, AvgPower, AvgHr, AvgPace, Rpe, CreatedAt, UpdatedAt)
VALUES
    -- W_A: week -2 threshold
    (NEWID(), @AthleteId, @W_A, @P2_Warm, 0, 600, NULL, 152, 118, NULL, 3.0, @Now, @Now),
    (NEWID(), @AthleteId, @W_A, @P2_Work, 1, 480, NULL, 264, 170, NULL, 7.0, @Now, @Now),
    (NEWID(), @AthleteId, @W_A, @P2_Rec,  2, 120, NULL, 128, 138, NULL, 2.0, @Now, @Now),
    (NEWID(), @AthleteId, @W_A, @P2_Cool, 3, 600, NULL, 140, 130, NULL, 3.0, @Now, @Now),
    -- W_C: week -1 VO2
    (NEWID(), @AthleteId, @W_C, @P3_Warm, 0, 600, NULL, 155, 122, NULL, 3.0, @Now, @Now),
    (NEWID(), @AthleteId, @W_C, @P3_Work, 1, 180, NULL, 292, 178, NULL, 9.0, @Now, @Now),
    (NEWID(), @AthleteId, @W_C, @P3_Rec,  2, 120, NULL, 135, 150, NULL, 2.0, @Now, @Now),
    (NEWID(), @AthleteId, @W_C, @P3_Cool, 3, 600, NULL, 142, 132, NULL, 3.0, @Now, @Now);

COMMIT TRANSACTION;

PRINT 'Bryk dev seed complete for athlete ' + CONVERT(varchar(40), @AthleteId)
    + ' — week of ' + CONVERT(varchar(10), @WeekStart, 120) + '.';
