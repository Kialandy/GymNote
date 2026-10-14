using System.Security.Claims;
using System.Text;
using GymNote.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GymNote.Api.Auth;

public class TokenService(IOptions<JwtSettings> options, TimeProvider timeProvider)
{
    public string CreateToken(User user)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
            ]),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(settings.TokenLifetime),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;
}
