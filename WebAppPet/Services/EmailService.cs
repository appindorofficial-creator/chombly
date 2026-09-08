using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace WebAppPet.Services;

public interface IEmailService
{
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public class EmailService : IEmailService
{
    private readonly SmtpOptions _opt;
    private readonly ILogger<EmailService> _log;

    public EmailService(IOptions<SmtpOptions> opt, ILogger<EmailService> log)
    {
        _opt = opt.Value;
        _log = log;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opt.Host)
        && !string.IsNullOrWhiteSpace(_opt.User)
        && !string.IsNullOrWhiteSpace(_opt.Password)
        && !string.IsNullOrWhiteSpace(_opt.From);

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _log.LogWarning("SMTP no configurado. No se envió correo a {To}: {Subject}", to, subject);
            return false;
        }

        if (string.IsNullOrWhiteSpace(to))
            return false;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(
                    string.IsNullOrWhiteSpace(_opt.From) ? _opt.User : _opt.From,
                    _opt.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to.Trim());

            using var client = new SmtpClient(_opt.Host, _opt.Port)
            {
                EnableSsl = _opt.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_opt.User, _opt.Password.Replace(" ", ""))
            };

            await client.SendMailAsync(message, ct);
            _log.LogInformation("Correo enviado a {To}: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error enviando correo a {To}: {Subject}", to, subject);
            return false;
        }
    }
}
