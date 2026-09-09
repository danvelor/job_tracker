using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// Builds JWTs directly rather than through the API, because the API will not
/// issue an invalid one — which is exactly the point of the tests that use
/// these.
/// </summary>
internal static class TestTokens
{
    public static string Signed(Guid organizationId, string key, TimeSpan? lifetime = null)
    {
        var token = new JwtSecurityToken(
            issuer: ApiFactory.Issuer,
            audience: ApiFactory.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim("org", organizationId.ToString()),
            ],
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A token this API's key did not sign.</summary>
    public static string Forged(Guid organizationId) =>
        Signed(organizationId, "a-completely-different-key-also-32-bytes-long");

    public static string Expired(Guid organizationId) =>
        Signed(organizationId, ApiFactory.SigningKey, TimeSpan.FromMinutes(-5));
}
