namespace GymNote.Api.Data;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public List<Exercise> Exercises { get; set; } = [];
}

public class Exercise
{
    public const int NameMaxLength = 100;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SuggestedSets { get; set; }
    public int SuggestedRepsPerSet { get; set; }
    public int SuggestedRestSeconds { get; set; }
}
