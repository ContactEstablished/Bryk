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
