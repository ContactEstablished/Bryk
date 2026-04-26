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
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property("CreatedAt").CurrentValue == null || 
                    (DateTime)entry.Property("CreatedAt").CurrentValue == default)
                {
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                }
            }
            
            if (entry.State == EntityState.Modified)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
