using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Auth;

// Login del portal del cliente: nunca confía en lo que mande el browser
// sobre quién es — el ID token de Google se valida acá, server-side,
// contra las claves públicas de Google (Google.Apis.Auth). El vínculo
// cuenta-de-Google -> cliente sale de client_portal_access
// (PortalAccessRepository), nunca de hacer matching por email.
public sealed class PortalAuthService(
    IOptions<AuthOptions> jwtOptions,
    IConfiguration configuration,
    PortalAccessRepository access,
    ClientRepository clients,
    ILogger<PortalAuthService> logger)
{
    private readonly AuthOptions _jwt = jwtOptions.Value;

    public async Task<PortalAuthOutcome> AuthenticateAsync(string idToken, string? inviteToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var googleClientId = configuration["Google:ClientId"]
                ?? throw new InvalidOperationException("No hay Google:ClientId configurada.");
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [googleClientId],
            });
        }
        catch (InvalidJwtException ex)
        {
            // Mensaje genérico al cliente a propósito (no revelar detalle de
            // validación) — pero el motivo real queda en los logs del
            // servicio para poder diagnosticar un mismatch de Client ID.
            logger.LogWarning(ex, "Token de Google rechazado.");
            return PortalAuthOutcome.Invalid("Token de Google inválido.");
        }

        // 1) ¿Ya hay un cliente vinculado a esta cuenta de Google? (login de vuelta)
        var clientId = access.GetClientIdByGoogleSub(payload.Subject);

        // 2) Si no, ¿vino un link de invitación válido? (primera activación)
        if (clientId is null && !string.IsNullOrWhiteSpace(inviteToken))
        {
            try
            {
                clientId = access.TryConsumeInvite(inviteToken, payload.Subject);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
            {
                return PortalAuthOutcome.Invalid("Esa cuenta de Google ya está vinculada a otro cliente.");
            }
        }

        if (clientId is null)
        {
            return PortalAuthOutcome.Invalid("Necesitás una invitación de tu taller para entrar acá.");
        }

        var client = clients.GetById(clientId.Value);
        if (client is null)
        {
            return PortalAuthOutcome.Invalid("Ese cliente ya no existe.");
        }

        var expiresAt = DateTime.UtcNow.AddDays(7);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, client.ClientId.ToString()),
            new Claim("name", client.Name),
            new Claim("role", "customer"),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return PortalAuthOutcome.Ok(new PortalAuthResult(
            new JwtSecurityTokenHandler().WriteToken(token), expiresAt, client.ClientId, client.Name));
    }
}

public sealed record PortalAuthResult(string Token, DateTime ExpiresAt, int ClientId, string Name);

public sealed class PortalAuthOutcome
{
    public PortalAuthResult? Result { get; private init; }
    public string? Error { get; private init; }
    public bool Success => Result is not null;

    public static PortalAuthOutcome Ok(PortalAuthResult result) => new() { Result = result };
    public static PortalAuthOutcome Invalid(string error) => new() { Error = error };
}
