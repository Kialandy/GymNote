using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymNote.Api.Data;

public static class DemoDataSeeder
{
    // Demo-only credentials, documented in the README. Only created when SEED_DEMO_DATA=true.
    public const string DemoEmail = "demo@gymnote.local";
    public const string DemoPassword = "DemoPass123!";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher, ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Email == DemoEmail))
        {
            logger.LogInformation("Demo data already present; skipping seed");
            return;
        }

        var user = new User { Id = Guid.NewGuid(), Email = DemoEmail };
        user.PasswordHash = hasher.HashPassword(user, DemoPassword);
        user.Exercises =
        [
            new() { Id = Guid.NewGuid(), Name = "Barbell Back Squat", SuggestedSets = 4, SuggestedRepsPerSet = 8, SuggestedRestSeconds = 120 },
            new() { Id = Guid.NewGuid(), Name = "Bench Press", SuggestedSets = 4, SuggestedRepsPerSet = 8, SuggestedRestSeconds = 120 },
            new() { Id = Guid.NewGuid(), Name = "Pull-up", SuggestedSets = 3, SuggestedRepsPerSet = 10, SuggestedRestSeconds = 90 },
            new() { Id = Guid.NewGuid(), Name = "Plank (seconds as reps)", SuggestedSets = 3, SuggestedRepsPerSet = 45, SuggestedRestSeconds = 60 },
        ];
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo user {Email}", DemoEmail);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Another replica seeded concurrently.
            logger.LogInformation("Demo data was seeded concurrently; skipping");
        }
    }
}
