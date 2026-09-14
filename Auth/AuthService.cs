using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BoxService_BackEnd.Auth;

public sealed class AuthService(IOptions<AuthOptions> options)
{
    private readonly AuthOptions _options = options.Value;

    // PasswordHasher<object> en vez de comparar contraseñas en texto plano
    // (== no es constant-time y appsettings.json las guardaba sin hashear).
    // El "object" es solo el tipo genérico que pide la API de Identity —
    // no hay entidad de usuario en EF acá, no se usa para nada más.
    private static readonly PasswordHasher<object> Hasher = new();

    public AuthResult? Authenticate(string username, string password)
    {
        var user = _options.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));

        if (user is null || !VerifyPassword(user.Password, password))
        {
            return null;
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
        // Claims con nombre corto ("role", no ClaimTypes.Role) a propósito:
        // ClaimTypes.Role/Name SON las URIs largas de WS-Federation
        // (http://schemas.../role) — JwtSecurityTokenHandler las escribe
        // tal cual en el token, y el front (que decodifica el JWT con
        // `jose`, sin pasar por el mapeo de claims de .NET) esperaba
        // simplemente "role". Program.cs configura RoleClaimType/
        // NameClaimType a juego, así que del lado del servidor
        // (User.FindFirst, [Authorize(Roles=...)]) sigue andando igual.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim("name", user.Username),
            new Claim("role", user.Role),
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

    // AuthOptions.Users guarda el hash (ver appsettings.example.json /
    // AuthPasswordHasher). Si alguien pega ahí una contraseña en texto
    // plano por error, VerifyHashedPassword tira FormatException en vez
    // de autenticar con eso — se trata como credencial inválida en vez
    // de romper el login.
    private static bool VerifyPassword(string storedHash, string providedPassword)
    {
        try
        {
            return Hasher.VerifyHashedPassword(new object(), storedHash, providedPassword)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record AuthResult(string Token, DateTime ExpiresAt, string Username, string Role);
