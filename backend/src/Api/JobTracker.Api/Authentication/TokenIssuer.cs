using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JobTracker.Api.Authentication;

/// <summary>
/// HS256 with a symmetric key, because the API is both issuer and validator
/// (architecture 7.1). A real deployment replaces this one class with an
/// identity provider and nothing else in the design moves — which is the point
/// of keeping token acquisition behind a single seam.
/// </summary>
internal sealed class TokenIssuer(IOptions<JwtOptions> options)
{
    public const string OrganizationClaim = "org";

    public string Issue(Guid organizationId, string subject, string name)
    {
        var settings = options.Value;

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(OrganizationClaim, organizationId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, name),
            ],
            expires: DateTime.UtcNow.AddMinutes(settings.LifetimeMinutes),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
