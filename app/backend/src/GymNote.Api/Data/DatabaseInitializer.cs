using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymNote.Api.Data;

/// <summary>
/// Applies migrations (RUN_MIGRATIONS, default true) and the optional demo seed (SEED_DEMO_DATA, default false).
/// Safe to run on every startup and from several replicas: migrations are tracked by EF and the seed
/// is skipped when the demo user already exists.
/// </summary>
public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task InitializeAsync(WebApplication app)
    {
        var runMigrations = app.Configuration.GetValue("RUN_MIGRATIONS", true);
        var seedDemoData = app.Configuration.GetValue("SEED_DEMO_DATA", false);
        if (!runMigrations && !seedDemoData)
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        // The database may still be starting (or being recreated), so retry for a while before giving up.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (runMigrations)
                {
                    logger.LogInformation("Applying database migrations");
                    await db.Database.MigrateAsync();
                }

                if (seedDemoData)
                {
                    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
                    await DemoDataSeeder.SeedAsync(db, hasher, logger);
                }

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Database initialization failed (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}s",
                    attempt, MaxAttempts, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay);
            }
        }
    }
}
