using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymNote.Api.Contracts;
using GymNote.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymNote.Api.Tests;

/// <summary>
/// Hosts the real API in memory. PostgreSQL is swapped for the EF Core in-memory provider so the
/// suite runs in CI without a database; migrations and seeding are disabled.
/// </summary>
public class GymNoteApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"gymnote-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Host=unused");
        builder.UseSetting("Jwt:Secret", "test-only-secret-key-that-is-long-enough-1234567890");
        builder.UseSetting("Jwt:Issuer", "gymnote-tests");
        builder.UseSetting("Jwt:Audience", "gymnote-tests");
        builder.UseSetting("RUN_MIGRATIONS", "false");
        builder.UseSetting("SEED_DEMO_DATA", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string? email = null, string password = "Password123!")
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }
}
