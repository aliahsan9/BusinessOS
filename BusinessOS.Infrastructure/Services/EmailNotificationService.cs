using System.Net;
using System.Net.Mail;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

/// <summary>
/// Sends email via SMTP when <see cref="EmailOptions.Enabled"/> is true; otherwise logs the message
/// (including password-reset links) so local development still works.
/// </summary>
public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogInformation(
                "Email delivery disabled. To={Email} Subject={Subject} Body={Body}",
                toEmail,
                subject,
                body);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);

        _logger.LogInformation("Email sent to {Email} with subject {Subject}", toEmail, subject);
    }
}
