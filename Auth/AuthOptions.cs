namespace BoxService_BackEnd.Auth;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "BoxService";
    public string Audience { get; set; } = "BoxService.FrontEnd";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60;
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "BoxService";
    public bool EnableSsl { get; set; } = true;
}

public sealed class PasswordResetOptions
{
    public string FrontendUrl { get; set; } = string.Empty;
}

/// <summary>
/// Ya no se usa para autenticar (eso lee la tabla "usuarios" ahora, ver
/// AuthService) — se mantiene solo como tipo genérico para
/// PasswordHasher&lt;T&gt; en el comando "dotnet run -- hash-password".
/// </summary>
public sealed class AuthUser
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
