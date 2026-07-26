using Microsoft.EntityFrameworkCore;
using Bryk.Domain.Entities;

namespace Bryk.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Athlete> Athletes => Set<Athlete>();
    public DbSet<AthleteSportProfile> AthleteSportProfiles => Set<AthleteSportProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<PlannedWorkout> PlannedWorkouts => Set<PlannedWorkout>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<AthleteSportZone> AthleteSportZones => Set<AthleteSportZone>();
    public DbSet<WorkoutBlock> WorkoutBlocks => Set<WorkoutBlock>();
    public DbSet<WorkoutStep> WorkoutSteps => Set<WorkoutStep>();
    public DbSet<WorkoutStepResult> WorkoutStepResults => Set<WorkoutStepResult>();
    public DbSet<ActivityFile> ActivityFiles => Set<ActivityFile>();
    public DbSet<DailyWellness> DailyWellness => Set<DailyWellness>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Athlete configuration
        modelBuilder.Entity<Athlete>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Id is supplied by the application (today via ICurrentUserService, eventually
            // from the auth system). Disable EF's default Guid value generation so that
            // Guid.Empty and other caller-supplied values are persisted as-is.
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.HeightCm).HasPrecision(5, 2);
            entity.Property(e => e.WeightKg).HasPrecision(5, 2);
            entity.Property(e => e.TypicalWeeklyHours).HasPrecision(4, 1);
            entity.Property(e => e.YearsTraining).HasDefaultValue(0);
            entity.Property(e => e.TypicalWeeklyHours).HasDefaultValue(0m);

            entity.HasMany(e => e.SportProfiles)
                .WithOne(p => p.Athlete)
                .HasForeignKey(p => p.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Events)
                .WithOne(ev => ev.Athlete)
                .HasForeignKey(ev => ev.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Goals)
                .WithOne(g => g.Athlete)
                .HasForeignKey(g => g.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Equipment)
                .WithOne(eq => eq.Athlete)
                .HasForeignKey(eq => eq.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TrainingPlans)
                .WithOne(tp => tp.Athlete)
                .HasForeignKey(tp => tp.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AthleteSportProfile configuration
        modelBuilder.Entity<AthleteSportProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ThresholdValue).HasPrecision(7, 2);
            entity.Property(e => e.Lt1).HasPrecision(5, 1);
            entity.Property(e => e.Lt2).HasPrecision(5, 1);

            entity.HasIndex(e => new { e.AthleteId, e.Sport }).IsUnique();
        });

        // Event configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
        });

        // Goal configuration
        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        });

        // Equipment configuration
        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
        });

        // TrainingPlan configuration
        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RecoveryWeekPercentage).HasPrecision(5, 2);

            // Optional target event. ClientSetNull: EF nulls EventId client-side when an event is
            // deleted through the tracked context (plan goes standalone), but the FK is NO ACTION at
            // the DB. A DB-level SET NULL here would create a second Athlete -> Event -> TrainingPlan
            // delete-action path alongside the Athlete -> TrainingPlan cascade, which SQL Server rejects
            // (multiple cascade paths, error 1785).
            entity.HasOne(e => e.Event)
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasMany(e => e.PlannedWorkouts)
                .WithOne(pw => pw.TrainingPlan)
                .HasForeignKey(pw => pw.TrainingPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlannedWorkout configuration
        modelBuilder.Entity<PlannedWorkout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.PlannedLoad).HasPrecision(6, 2);

            // AthleteId is a denormalized, indexed column with no FK to Athlete (ADR-0003):
            // ownership cascades through TrainingPlan; this composite index serves the "This Week" range query.
            entity.HasIndex(e => new { e.AthleteId, e.ScheduledDate });

            // Structured-workout payload (ADR-0004 §2): blocks cascade with the planned workout.
            entity.HasMany(e => e.Blocks)
                .WithOne(b => b.PlannedWorkout)
                .HasForeignKey(b => b.PlannedWorkoutId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Workout configuration
        modelBuilder.Entity<Workout>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Nullable link to the planned session; unplanned executions are first-class (ADR-0001 §16).
            entity.HasOne(e => e.PlannedWorkout)
                .WithMany()
                .HasForeignKey(e => e.PlannedWorkoutId)
                .OnDelete(DeleteBehavior.SetNull);

            // Execution actuals (ADR-0005 §4).
            entity.Property(e => e.ComputedLoad).HasPrecision(7, 2);
            entity.Property(e => e.LoadOverride).HasPrecision(7, 2);
            entity.Property(e => e.Rpe).HasPrecision(3, 1);
            entity.Property(e => e.Notes).HasMaxLength(2000);

            // Per-step actuals cascade with the workout (ADR-0005 §5).
            entity.HasMany(e => e.StepResults)
                .WithOne(r => r.Workout)
                .HasForeignKey(r => r.WorkoutId)
                .OnDelete(DeleteBehavior.Cascade);

            // Denormalized, indexed AthleteId with no FK to Athlete (ADR-0003).
            entity.HasIndex(e => e.AthleteId);
        });

        // AthleteSportZone configuration
        modelBuilder.Entity<AthleteSportZone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LowerBound).HasPrecision(7, 2);
            entity.Property(e => e.UpperBound).HasPrecision(7, 2);

            // Denormalized AthleteId, no FK to Athlete (ADR-0004 §1). One override row per
            // athlete/sport/zone/metric.
            entity.HasIndex(e => new { e.AthleteId, e.Sport, e.ZoneNumber, e.Metric }).IsUnique();
        });

        // WorkoutBlock configuration (ADR-0004 §2)
        modelBuilder.Entity<WorkoutBlock>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Ordered child steps cascade with the block.
            entity.HasMany(e => e.Steps)
                .WithOne(s => s.WorkoutBlock)
                .HasForeignKey(s => s.WorkoutBlockId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PlannedWorkoutId, e.OrderIndex });
            // Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).
            entity.HasIndex(e => e.AthleteId);
        });

        // WorkoutStep configuration (ADR-0004 §2)
        modelBuilder.Entity<WorkoutStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.LoadKg).HasPrecision(6, 2);
            entity.Property(e => e.Rpe).HasPrecision(3, 1);

            entity.HasIndex(e => new { e.WorkoutBlockId, e.OrderIndex });
            // Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).
            entity.HasIndex(e => e.AthleteId);
        });

        // WorkoutStepResult configuration (ADR-0005 §5)
        modelBuilder.Entity<WorkoutStepResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Rpe).HasPrecision(3, 1);

            // Optional reference to the planned step — NoAction so a plan edit never touches completed
            // execution history (ADR-0005 §5). One-directional (no inverse nav).
            entity.HasOne<WorkoutStep>()
                .WithMany()
                .HasForeignKey(e => e.WorkoutStepId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.WorkoutId, e.OrderIndex });
            // Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).
            entity.HasIndex(e => e.AthleteId);
        });

        // ActivityFile configuration (ADR-0010 §2/§4/§5)
        modelBuilder.Entity<ActivityFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
            entity.Property(e => e.Content).IsRequired();

            // ParsedWorkoutId is a plain indexed Guid? with NO FK to Workout (ADR-0010 §4): the link is
            // one-directional (reverse lookup only) and deleting a workout must not cascade away the
            // uploaded file. Indexed because analytics reads it once per range query.
            entity.HasIndex(e => e.ParsedWorkoutId);
            // Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).
            entity.HasIndex(e => e.AthleteId);
        });

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
    }

}
