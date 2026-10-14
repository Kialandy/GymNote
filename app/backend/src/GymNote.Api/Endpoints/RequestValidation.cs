using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GymNote.Api.Endpoints;

public static class RequestValidation
{
    /// <summary>
    /// Validates DataAnnotations on <paramref name="model"/>. Returns a 400 ValidationProblem result
    /// (errors keyed by camelCase property name) or null when the model is valid.
    /// </summary>
    public static IResult? Validate<T>(T model) where T : class
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            return null;
        }

        var errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (Member: JsonNamingPolicy.CamelCase.ConvertName(member), Message: r.ErrorMessage ?? "Invalid value.")))
            .GroupBy(e => e.Member)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

        return Results.ValidationProblem(errors);
    }
}
