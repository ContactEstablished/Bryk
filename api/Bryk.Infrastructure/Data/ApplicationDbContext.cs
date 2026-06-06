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

            // Denormalized, indexed AthleteId with no FK to Athlete (ADR-0003).
            entity.HasIndex(e => e.AthleteId);
        });
    }

}
