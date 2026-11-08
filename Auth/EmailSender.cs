using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace BoxService_BackEnd.Auth;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendPasswordResetAsync(
        string recipient,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || _options.Port is < 1 or > 65535
            || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("La configuración SMTP está incompleta.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = "Recuperación de contraseña de BoxService",
            Body = $"Para definir una nueva contraseña, abrí este enlace dentro de los próximos 30 minutos:\n\n{resetUrl}\n\nSi no solicitaste este cambio, ignorá este correo.",
            IsBodyHtml = false,
        };
        message.To.Add(new MailAddress(recipient));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Timeout = 20000,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
