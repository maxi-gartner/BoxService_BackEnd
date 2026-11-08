namespace BoxService_BackEnd.DTOs;

public sealed record LoginRequest(string Username, string Password);

/// <summary>Requests a password reset email for the supplied account email.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Completes a password reset with a one-time token and new password.</summary>
public sealed record CompletePasswordResetRequest(string Token, string NewPassword);
