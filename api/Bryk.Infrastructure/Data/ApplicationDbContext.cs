using Microsoft.EntityFrameworkCore;
using Bryk.Domain.Entities;

namespace Bryk.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Mesocycle> Mesocycles => Set<Mesocycle>();
    public DbSet<Week> Weeks => Set<Week>();
    public DbSet<Day> Days => Set<Day>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<DayExercise> DayExercises => Set<DayExercise>();
    public DbSet<Athlete> Athletes => Set<Athlete>();
    public DbSet<AthleteSportProfile> AthleteSportProfiles => Set<AthleteSportProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Mesocycle configuration
        modelBuilder.Entity<Mesocycle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WeeklyIncreasePercentage).HasPrecision(5, 2);
            entity.Property(e => e.BuildRecoveryRatio).HasMaxLength(10);
            entity.Property(e => e.WeeklyPatternType).HasMaxLength(50);
            
            entity.HasMany(e => e.Weeks)
                .WithOne(w => w.Mesocycle)
                .HasForeignKey(w => w.MesocycleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Week configuration
        modelBuilder.Entity<Week>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            
            entity.HasMany(e => e.Days)
                .WithOne(d => d.Week)
                .HasForeignKey(d => d.WeekId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.MesocycleId, e.WeekNumber }).IsUnique();
        });

        // Day configuration
        modelBuilder.Entity<Day>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            
            entity.HasMany(e => e.DayExercises)
                .WithOne(de => de.Day)
                .HasForeignKey(de => de.DayId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.WeekId, e.Date }).IsUnique();
        });

        // Exercise configuration
        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IntensityZone).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            
            entity.HasMany(e => e.DayExercises)
                .WithOne(de => de.Exercise)
                .HasForeignKey(de => de.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete exercises
        });

        // DayExercise configuration
        modelBuilder.Entity<DayExercise>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AveragePace).HasMaxLength(50);
            entity.Property(e => e.MaxPace).HasMaxLength(50);
            entity.Property(e => e.AdditionalPaceMetric).HasMaxLength(100);
            entity.Property(e => e.PerformanceComparisonPercent).HasPrecision(5, 2);
            entity.Property(e => e.ComparisonNotes).HasMaxLength(500);
            entity.Property(e => e.WeatherCondition).HasMaxLength(100);
            entity.Property(e => e.WindSpeed).HasMaxLength(50);
            entity.Property(e => e.WorkoutNotes).HasMaxLength(2000);
            
            entity.HasIndex(e => new { e.DayId, e.OrderIndex });
        });

        // Athlete configuration
        modelBuilder.Entity<Athlete>(entity =>
        {
            entity.HasKey(e => e.Id);
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
    }

}
