using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Interfaces.Security;

namespace StudioFlow.API.Authentication;

/// <summary>
/// Concrete <see cref="IJwtTokenGenerator"/> for the API host (the abstraction
/// lives in Core; the implementation belongs to the composition layer). Signs an
/// HMAC-SHA256 JWT carrying the user's id, email and role (spec §19).
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateToken(User user)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_options.ExpiryMinutes),
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [ClaimTypes.NameIdentifier] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [ClaimTypes.Role] = user.Role.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
