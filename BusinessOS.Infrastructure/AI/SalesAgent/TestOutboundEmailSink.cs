using System.Collections.Concurrent;
using BusinessOS.Application.Features.SalesAgent;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

/// <summary>
/// In-process email sink for automated tests. Explicitly labeled as a test sink — not a live mailbox integration.
/// </summary>
public sealed class TestOutboundEmailSink : IOutboundEmailSink
{
    private readonly ConcurrentDictionary<string, SentMail> _sent = new ConcurrentDictionary<string, SentMail>(StringComparer.OrdinalIgnoreCase);

    public bool IsTestSink => true;

    public IReadOnlyCollection<SentMail> Sent => _sent.Values.ToList();

    public Task<(bool Performed, string Status, string Message, string? ProviderMessageId)> SendAsync(
        string toEmail,
        string subject,
        string body,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return Task.FromResult<(bool, string, string, string?)>((false, "validation", "Recipient email is required.", null));

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey!;
        if (_sent.TryGetValue(key, out var existing))
            return Task.FromResult<(bool, string, string, string?)>((true, "idempotent", "Previous test send reused.", existing.ProviderMessageId));

        var messageId = "test-" + Guid.NewGuid().ToString("N");
        var mail = new SentMail(key, toEmail.Trim(), subject, body, messageId, DateTime.UtcNow);
        _sent[key] = mail;
        return Task.FromResult<(bool, string, string, string?)>((true, "sent", "Delivered through the test email sink (not a live provider).", messageId));
    }

    public sealed record SentMail(string IdempotencyKey, string ToEmail, string Subject, string Body, string ProviderMessageId, DateTime SentAt);
}
