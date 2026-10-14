using System.ComponentModel.DataAnnotations;
using GymNote.Api.Data;

namespace GymNote.Api.Contracts;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters."), MaxLength(128)]
    public string? Password { get; set; }
}

public record RegisterResponse(Guid Id, string Email);

public class LoginRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public record LoginResponse(string Token);

public class CreateExerciseRequest
{
    [Required, MaxLength(Exercise.NameMaxLength)]
    public string? Name { get; set; }

    [Required, Range(1, 20)]
    public int? SuggestedSets { get; set; }

    [Required, Range(1, 100)]
    public int? SuggestedRepsPerSet { get; set; }

    [Required, Range(0, 600)]
    public int? SuggestedRestSeconds { get; set; }
}

public record ExerciseResponse(Guid Id, string Name, int SuggestedSets, int SuggestedRepsPerSet, int SuggestedRestSeconds);
