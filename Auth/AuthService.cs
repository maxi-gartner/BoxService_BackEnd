using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Auth;

public sealed class AuthService(
    IOptions<AuthOptions> options,
    UserRepository userRepository,
    TenantRepository tenants,
    AuthAuditRepository auditRepository)
{
    private readonly AuthOptions _options = options.Value;
    private readonly UserRepository _userRepository = userRepository;
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();

    public AuthResult? Authenticate(string? username, string? password, string? sourceIp)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            auditRepository.Record("login", "failed", null, null, null, sourceIp);
            return null;
        }

        var user = _userRepository.GetByUsername(username);
        if (user is null || !user.Active || user.Role is not ("superadmin" or "dueno" or "empleado")
            || (user.Role == "superadmin" && user.TenantId is not null)
            || (user.Role != "superadmin" && (user.TenantId is not int activeTenantId
                || tenants.GetById(activeTenantId) is not { Active: true })))
        {
            auditRepository.Record("login", "failed", null, user?.UserId, user?.TenantId, sourceIp);
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification is not (PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded))
        {
            auditRepository.Record("login", "failed", null, user.UserId, user.TenantId, sourceIp);
            return null;
        }

        auditRepository.Record("login", "succeeded", user.UserId, user.UserId, user.TenantId, sourceIp);
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new("name", user.Username),
            new("role", user.Role),
            new(SessionSecurity.VersionClaim, SessionSecurity.Version(user.PasswordHash)),
        };

        // Ausente del todo para superadmin (no pertenece a ningún
        // tenant) — más limpio que mandar un claim vacío que cada
        // lector tendría que tratar como "string vacío = sin tenant".
        if (user.TenantId is int tenantId)
        {
            claims.Add(new Claim("tenantId", tenantId.ToString()));
        }

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
