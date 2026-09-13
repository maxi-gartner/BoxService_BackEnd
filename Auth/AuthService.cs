using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BoxService_BackEnd.Auth;

public sealed class AuthService(IOptions<AuthOptions> options)
{
    private readonly AuthOptions _options = options.Value;

    public AuthResult? Authenticate(string username, string password)
    {
        var user = _options.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase) &&
            candidate.Password == password);

        if (user is null)
        {
            return null;
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, user.Username, user.Role);
    }
}

public sealed record AuthResult(string Token, DateTime ExpiresAt, string Username, string Role);
