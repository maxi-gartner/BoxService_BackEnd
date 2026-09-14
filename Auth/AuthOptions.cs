namespace BoxService_BackEnd.Auth;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "BoxService";
    public string Audience { get; set; } = "BoxService.FrontEnd";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60;
    public List<AuthUser> Users { get; set; } = [];
}

public sealed class AuthUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
