using System.ComponentModel.DataAnnotations;

namespace GymNote.Api.Auth;

/// <summary>Bound from the Jwt__Secret, Jwt__Issuer and Jwt__Audience environment variables.</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32, ErrorMessage = "Jwt:Secret must be at least 32 characters.")]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
}
