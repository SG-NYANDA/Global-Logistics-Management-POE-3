using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GLMS.Contracts;
using GLMS.Web.Models;
using Microsoft.IdentityModel.Tokens;

namespace GLMS.Api.Services;

public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    public AccountTokenResponse Create(ApplicationUser user)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "GLMS.Api";
        var audience = configuration["Jwt:Audience"] ?? "GLMS.Web";
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(issuer)
            || string.IsNullOrWhiteSpace(audience)
            || string.IsNullOrWhiteSpace(signingKey)
            || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience and a Jwt:SigningKey of at least 32 bytes.");
        }

        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!)
            ],
            DateTime.UtcNow,
            expires.UtcDateTime,
            credentials);

        return new AccountTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Email!);
    }
}
