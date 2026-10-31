namespace BusinessOS.Application.Features.Notifications.DTOs;

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string TextBody,
    string? HtmlBody = null);

public sealed record EmailDeliveryResult(bool Delivered, bool ProviderConfigured);
