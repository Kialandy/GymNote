using System.Net;
using System.Net.Http.Json;
using GymNote.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GymNote.Api.Tests;

public class AuthTests(GymNoteApiFactory factory) : IClassFixture<GymNoteApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Health_returns_200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_valid_data_returns_201()
    {
        var email = NewEmail();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password123!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.Equal(email, body!.Email);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password123!" });

        // Email comparison is case-insensitive.
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = "Password123!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "Password123!", "email")]
    [InlineData("valid@example.com", "short", "password")]
    [InlineData("", "Password123!", "email")]
    public async Task Register_with_invalid_data_returns_validation_problem(string email, string password, string field)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password123!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_failures_return_the_same_generic_401()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password123!" });

        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword!" });
        var unknownUser = await _client.PostAsJsonAsync("/api/auth/login", new { email = NewEmail(), password = "Password123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        var first = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var second = await unknownUser.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(first!.Title, second!.Title);
    }
}
