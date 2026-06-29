using BusinessOS.Application.Features.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

/// <summary>
/// Email notification delivery stub. Wire up SMTP/SendGrid when ready.
/// </summary>
public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(ILogger<EmailNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Email notification disabled. Would send to {Email} with subject {Subject}",
            toEmail,
            subject);

        return Task.CompletedTask;
    }
}
