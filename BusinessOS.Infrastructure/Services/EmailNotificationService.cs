using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BusinessOS.Infrastructure.Services;

/// <summary>
/// Sends transactional email through SMTP (MailKit). When delivery is disabled, the message is
/// not sent and the body is not written to logs.
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
        var result = await SendMessageAsync(
            new EmailMessage(toEmail, subject, body),
            cancellationToken);

        if (!result.Delivered && result.ProviderConfigured)
        {
            throw new InvalidOperationException("Email delivery failed.");
        }
    }

    public async Task<EmailDeliveryResult> SendMessageAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning(
                "Email delivery is disabled. Message was not sent. To={Email} Subject={Subject}",
                message.ToEmail,
                message.Subject);
            return new EmailDeliveryResult(false, false);
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogError("Email delivery is enabled but Email:FromAddress is empty.");
            throw new InvalidOperationException("Email sender address is not configured.");
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            mime.To.Add(MailboxAddress.Parse(message.ToEmail));
            mime.Subject = message.Subject;

            var builder = new BodyBuilder { TextBody = message.TextBody };
            if (!string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                builder.HtmlBody = message.HtmlBody;
            }

            mime.Body = builder.ToMessageBody();

            using var client = new SmtpClient
            {
                Timeout = Math.Clamp(_options.TimeoutSeconds, 5, 60) * 1000
            };

            var socket = !_options.UseSsl
                ? SecureSocketOptions.None
                : _options.SmtpPort == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socket, cancellationToken);
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "Email sent. To={Email} Subject={Subject}",
                message.ToEmail,
                message.Subject);
            return new EmailDeliveryResult(true, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Email delivery failed. To={Email} Subject={Subject} ErrorType={ErrorType}",
                message.ToEmail,
                message.Subject,
                ex.GetType().Name);
            throw;
        }
    }
}
