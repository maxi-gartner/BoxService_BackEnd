using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services;

public sealed class PasswordRecoveryService(
    UserRepository userRepository,
    PasswordResetRepository resetRepository,
    AuthAuditRepository auditRepository,
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> resetOptions,
    ILogger<PasswordRecoveryService> logger)
{
    private const int TokenLifetimeMinutes = 30;
    private readonly PasswordResetOptions _resetOptions = resetOptions.Value;
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();

    public async Task RequestResetAsync(string? email, string? sourceIp, CancellationToken cancellationToken)
    {
        var users = string.IsNullOrWhiteSpace(email)
            ? []
            : userRepository.GetActiveByEmail(email.Trim());

        if (users.Count != 1)
        {
            auditRepository.Record(
                "password_reset_requested",
                users.Count > 1 ? "ambiguous_email" : "accepted",
                null,
                null,
                null,
                sourceIp);
            return;
        }

        var user = users[0];
        auditRepository.Record(
            "password_reset_requested",
            "accepted",
            null,
            user.UserId,
            user.TenantId,
            sourceIp);

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(token);
        resetRepository.CreateToken(
            user.UserId,
            tokenHash,
            DateTimeOffset.UtcNow.AddMinutes(TokenLifetimeMinutes));

        try
        {
            var resetUrl = BuildResetUrl(token);
            await emailSender.SendPasswordResetAsync(user.Email!, resetUrl, cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.Net.Mail.SmtpException
                or FormatException)
        {
            resetRepository.RevokeToken(tokenHash);
            auditRepository.Record(
                "password_reset_email",
                "failed",
                null,
                user.UserId,
                user.TenantId,
                sourceIp);
            logger.LogError(exception, "Password reset email could not be sent for user {UserId}.", user.UserId);
            return;
        }

        auditRepository.Record(
            "password_reset_email",
            "sent",
            null,
            user.UserId,
            user.TenantId,
            sourceIp);
    }

    public bool CompleteReset(string token, string newPassword, string? sourceIp)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            auditRepository.Record(
                "password_reset_completed",
                "invalid_or_expired_token",
                null,
                null,
                null,
                sourceIp);
            return false;
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            auditRepository.Record("password_reset_completed", "invalid_password", null, null, null, sourceIp);
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        }

        var tokenHash = HashToken(token);
        var passwordHash = _passwordHasher.HashPassword(new UserAccount(), newPassword);
        var completion = resetRepository.CompleteReset(tokenHash, passwordHash);

        if (completion is null)
        {
            auditRepository.Record(
                "password_reset_completed",
                "invalid_or_expired_token",
                null,
                null,
                null,
                sourceIp);
            return false;
        }

        auditRepository.Record(
            "password_reset_completed",
            "succeeded",
            completion.UserId,
            completion.UserId,
            completion.TenantId,
            sourceIp);
        return true;
    }

    private string BuildResetUrl(string token)
    {
        if (!Uri.TryCreate(_resetOptions.FrontendUrl, UriKind.Absolute, out var frontendUri)
            || (frontendUri.Scheme != Uri.UriSchemeHttps
                && !(frontendUri.Scheme == Uri.UriSchemeHttp && frontendUri.IsLoopback)))
        {
            throw new InvalidOperationException(
                "PasswordReset:FrontendUrl debe ser una URL HTTPS (HTTP solo se permite para localhost).");
        }

        if (!string.IsNullOrEmpty(frontendUri.Fragment))
        {
            throw new InvalidOperationException("PasswordReset:FrontendUrl no puede contener un fragmento.");
        }

        var builder = new UriBuilder(frontendUri);
        var existingQuery = builder.Query.TrimStart('?');
        var tokenQuery = $"token={Uri.EscapeDataString(token)}";
        builder.Query = string.IsNullOrEmpty(existingQuery)
            ? tokenQuery
            : $"{existingQuery}&{tokenQuery}";
        return builder.Uri.ToString();
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
