namespace BoxService_BackEnd.Auth;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "BoxService";
    public string Audience { get; set; } = "BoxService.FrontEnd";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60;
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
