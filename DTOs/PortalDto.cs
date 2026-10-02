namespace BoxService_BackEnd.DTOs;

public sealed record PortalGoogleLoginRequest(string IdToken, string? InviteToken);
