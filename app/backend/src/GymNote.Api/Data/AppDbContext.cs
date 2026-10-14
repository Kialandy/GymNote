using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GymNote.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Exercise> Exercises => Set<Exercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).IsRequired().HasMaxLength(256);
            user.Property(u => u.PasswordHash).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
            user.HasMany(u => u.Exercises)
                .WithOne()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Exercise>(exercise =>
        {
            exercise.ToTable("exercises", t =>
            {
                t.HasCheckConstraint("ck_exercises_sets", "\"SuggestedSets\" BETWEEN 1 AND 20");
                t.HasCheckConstraint("ck_exercises_reps", "\"SuggestedRepsPerSet\" BETWEEN 1 AND 100");
                t.HasCheckConstraint("ck_exercises_rest", "\"SuggestedRestSeconds\" BETWEEN 0 AND 600");
            });
            exercise.HasKey(e => e.Id);
            exercise.Property(e => e.Name).IsRequired().HasMaxLength(Exercise.NameMaxLength);
            exercise.HasIndex(e => e.UserId);
        });
    }
}

/// <summary>Used only by `dotnet ef` tooling; never connects to a database.</summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=gymnote")
            .Options;
        return new AppDbContext(options);
    }
}
