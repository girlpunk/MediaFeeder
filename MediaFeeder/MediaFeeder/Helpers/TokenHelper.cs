namespace MediaFeeder.Helpers;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Data.db;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

public class TokenHelper(IConfiguration configuration)
{
    public string GenerateAPIJwt(AuthUser user)
    {
        var userClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Role, "API"),
        };

        var authSettings = configuration.GetSection("Auth");
        var selfIssuer = authSettings.GetValue<string>("issuer", "MediaFeeder");

        var issuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.GetValue<string>("secret") ?? throw new InvalidOperationException()));
        var signinCredentials = new SigningCredentials(issuerSigningKey, SecurityAlgorithms.HmacSha256);

        var tokenOptions = new JwtSecurityToken(
            selfIssuer,
            selfIssuer,
            userClaims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: signinCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
    }

    public string GeneratePlaybackJwt(AuthUser user, int videoId)
    {
        var userClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Role, "Download"),
            new(JwtRegisteredClaimNames.Acr, videoId.ToString())
        };

        var authSettings = configuration.GetSection("Auth");
        var selfIssuer = authSettings.GetValue<string>("issuer", "MediaFeeder");

        var issuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.GetValue<string>("secret") ?? throw new InvalidOperationException()));
        var signinCredentials = new SigningCredentials(issuerSigningKey, SecurityAlgorithms.HmacSha256);

        var tokenOptions = new JwtSecurityToken(
            selfIssuer,
            selfIssuer,
            userClaims,
            expires: DateTime.Now.AddYears(1),
            signingCredentials: signinCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
    }
}
