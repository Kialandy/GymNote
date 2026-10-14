using System.Security.Claims;
using GymNote.Api.Auth;
using GymNote.Api.Contracts;
using GymNote.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GymNote.Api.Endpoints;

public static class ExerciseEndpoints
{
    public static IEndpointRouteBuilder MapExerciseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exercises")
            .RequireAuthorization()
            .AddEndpointFilter(RequireExistingUser);

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapDelete("/{id:guid}", Delete);
        return app;
    }

    /// <summary>
    /// The database is recreated often, so a still-valid token may reference a user that no longer exists.
    /// Treat that as unauthenticated so the client sends the user back to the login screen.
    /// </summary>
    private static async ValueTask<object?> RequireExistingUser(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var userId = http.User.GetUserId();
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        if (userId == Guid.Empty || !await db.Users.AnyAsync(u => u.Id == userId, http.RequestAborted))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session is no longer valid.");
        }

        return await next(context);
    }

    private static async Task<IResult> List(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var exercises = await db.Exercises
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Name)
            .Select(e => new ExerciseResponse(e.Id, e.Name, e.SuggestedSets, e.SuggestedRepsPerSet, e.SuggestedRestSeconds))
            .ToListAsync(ct);

        return Results.Ok(exercises);
    }

    private static async Task<IResult> Create(
        CreateExerciseRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (RequestValidation.Validate(request) is { } invalid)
        {
            return invalid;
        }

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            UserId = principal.GetUserId(),
            Name = request.Name!.Trim(),
            SuggestedSets = request.SuggestedSets!.Value,
            SuggestedRepsPerSet = request.SuggestedRepsPerSet!.Value,
            SuggestedRestSeconds = request.SuggestedRestSeconds!.Value,
        };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/exercises/{exercise.Id}", new ExerciseResponse(
            exercise.Id, exercise.Name, exercise.SuggestedSets, exercise.SuggestedRepsPerSet, exercise.SuggestedRestSeconds));
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        // Filtering by owner means another user's exercise is indistinguishable from a missing one.
        var exercise = await db.Exercises.SingleOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (exercise is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Exercise not found.");
        }

        db.Exercises.Remove(exercise);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
