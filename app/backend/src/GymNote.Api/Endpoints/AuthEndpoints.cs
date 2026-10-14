using GymNote.Api.Auth;
using GymNote.Api.Contracts;
using GymNote.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymNote.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").AllowAnonymous();
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, AppDbContext db, IPasswordHasher<User> hasher, CancellationToken ct)
    {
        if (RequestValidation.Validate(request) is { } invalid)
        {
            return invalid;
        }

        var email = NormalizeEmail(request.Email!);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return EmailTaken();
        }

        var user = new User { Id = Guid.NewGuid(), Email = email };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return EmailTaken();
        }

        return Results.Created((string?)null, new RegisterResponse(user.Id, user.Email));
    }

    private static async Task<IResult> Login(
        LoginRequest request, AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return InvalidCredentials();
        }

        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new LoginResponse(tokens.CreateToken(user)));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static IResult EmailTaken() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.");

    private static IResult InvalidCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
}
