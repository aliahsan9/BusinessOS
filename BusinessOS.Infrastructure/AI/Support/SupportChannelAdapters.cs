using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Support;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Support;

public sealed class SandboxSupportChannelAdapter : ISupportChannelAdapter
{
    public string Channel => SupportChannels.Sandbox;
    public string DisplayName => "Sandbox inbox";
    public string Implementation => "REAL";

    public Task<SupportChannelDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupportChannelDto(
            Channel,
            DisplayName,
            SupportChannelConnectionStatuses.Sandbox,
            true,
            true,
            Implementation,
            "sandbox",
            null,
            null,
            null));

    public Task<(bool Performed, string DeliveryStatus, string? ExternalMessageId, string Message, string? Error)> SendAsync(
        SupportTicketDetailDto ticket,
        string body,
        CancellationToken cancellationToken = default)
    {
        var id = $"sandbox-{Guid.NewGuid():N}";
        return Task.FromResult<(bool, string, string?, string, string?)>((
            true,
            SupportDeliveryStatuses.Simulated,
            id,
            "Message recorded in the sandbox channel. No external customer message was sent.",
            null));
    }
}

public sealed class WebsiteChatSupportChannelAdapter : ISupportChannelAdapter
{
    private readonly BusinessOSDbContext _db;

    public WebsiteChatSupportChannelAdapter(BusinessOSDbContext db) => _db = db;

    public string Channel => SupportChannels.WebsiteChat;
    public string DisplayName => "Business website chat";
    public string Implementation => "REAL";

    public async Task<SupportChannelDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var account = await EnsureAccountAsync(cancellationToken);
        return new SupportChannelDto(
            Channel,
            DisplayName,
            account.Status,
            account.AllowsInbound,
            account.AllowsOutbound,
            Implementation,
            account.ExternalAccountId,
            account.LastInboundAt,
            account.LastOutboundAt,
            account.LastError);
    }

    public async Task<(bool Performed, string DeliveryStatus, string? ExternalMessageId, string Message, string? Error)> SendAsync(
        SupportTicketDetailDto ticket,
        string body,
        CancellationToken cancellationToken = default)
    {
        var account = await EnsureAccountAsync(cancellationToken);
        if (account.Status is SupportChannelConnectionStatuses.Disconnected or SupportChannelConnectionStatuses.Unavailable)
            return (false, SupportDeliveryStatuses.Failed, null, "Website chat is not connected for this company.", "not_connected");

        account.LastOutboundAt = DateTime.UtcNow;
        account.LastError = null;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, SupportDeliveryStatuses.Delivered, $"webchat-{Guid.NewGuid():N}", "Reply delivered to the website chat thread.", null);
    }

    private async Task<SupportChannelAccount> EnsureAccountAsync(CancellationToken ct)
    {
        var account = await _db.SupportChannelAccounts.FirstOrDefaultAsync(x => x.Channel == Channel, ct);
        if (account is not null)
            return account;

        account = new SupportChannelAccount
        {
            Channel = Channel,
            DisplayName = DisplayName,
            Status = SupportChannelConnectionStatuses.Connected,
            AllowsInbound = true,
            AllowsOutbound = true,
            ExternalAccountId = "website-chat"
        };
        _db.SupportChannelAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }
}

public sealed class EmailSupportChannelAdapter : ISupportChannelAdapter
{
    private readonly IIntegrationPlatform? _integrations;

    public EmailSupportChannelAdapter(IIntegrationPlatform? integrations = null) => _integrations = integrations;

    public string Channel => SupportChannels.Email;
    public string DisplayName => "Email support (Gmail)";
    public string Implementation => "PARTIAL";

    public async Task<SupportChannelDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_integrations is null)
        {
            return new SupportChannelDto(Channel, DisplayName, SupportChannelConnectionStatuses.Unavailable, false, false, Implementation, null, null, null, "Integration platform unavailable.");
        }

        var connections = await _integrations.GetConnectionsAsync(cancellationToken);
        var gmail = connections.FirstOrDefault(x => string.Equals(x.ProviderKey, "gmail", StringComparison.OrdinalIgnoreCase));
        if (gmail is null || !string.Equals(gmail.Status, "Connected", StringComparison.OrdinalIgnoreCase))
        {
            return new SupportChannelDto(Channel, DisplayName, SupportChannelConnectionStatuses.Disconnected, true, false, Implementation, null, null, null, "Connect Gmail under Integrations to send support email.");
        }

        return new SupportChannelDto(Channel, DisplayName, SupportChannelConnectionStatuses.Connected, true, true, Implementation, gmail.ExternalAccountLabel, null, null, null);
    }

    public async Task<(bool Performed, string DeliveryStatus, string? ExternalMessageId, string Message, string? Error)> SendAsync(
        SupportTicketDetailDto ticket,
        string body,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.Status != SupportChannelConnectionStatuses.Connected || _integrations is null)
            return (false, SupportDeliveryStatuses.Failed, null, "Email support is disconnected. No message was sent.", "not_connected");

        if (string.IsNullOrWhiteSpace(ticket.CustomerEmail))
            return (false, SupportDeliveryStatuses.Failed, null, "Customer email is missing, so nothing was sent.", "validation");

        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"To: {ticket.CustomerEmail}\r\nSubject: Re: {ticket.Subject}\r\n\r\n{body}"));
        var result = await _integrations.ExecuteAsync(
            "gmail",
            "send",
            $"{{\"raw\":\"{raw}\",\"to\":\"{ticket.CustomerEmail}\",\"body\":{System.Text.Json.JsonSerializer.Serialize(body)}}}",
            true,
            cancellationToken);

        if (!result.Performed)
            return (false, SupportDeliveryStatuses.Failed, null, result.Message, result.Status);

        return (true, SupportDeliveryStatuses.Sent, result.Data["id"]?.ToString(), result.Message, null);
    }
}

public sealed class WhatsAppSupportChannelAdapter : ISupportChannelAdapter
{
    private readonly IIntegrationPlatform? _integrations;

    public WhatsAppSupportChannelAdapter(IIntegrationPlatform? integrations = null) => _integrations = integrations;

    public string Channel => SupportChannels.WhatsApp;
    public string DisplayName => "WhatsApp Business";
    public string Implementation => "PARTIAL";

    public async Task<SupportChannelDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_integrations is null)
            return Unavailable("Integration platform unavailable.");

        var connections = await _integrations.GetConnectionsAsync(cancellationToken);
        var row = connections.FirstOrDefault(x => string.Equals(x.ProviderKey, "whatsapp", StringComparison.OrdinalIgnoreCase));
        if (row is null || !string.Equals(row.Status, "Connected", StringComparison.OrdinalIgnoreCase))
            return new SupportChannelDto(Channel, DisplayName, SupportChannelConnectionStatuses.Disconnected, true, false, Implementation, null, null, null, "WhatsApp Business is not connected.");

        return new SupportChannelDto(Channel, DisplayName, SupportChannelConnectionStatuses.Connected, true, true, Implementation, row.ExternalAccountLabel, null, null, null);
    }

    public async Task<(bool Performed, string DeliveryStatus, string? ExternalMessageId, string Message, string? Error)> SendAsync(
        SupportTicketDetailDto ticket,
        string body,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.Status != SupportChannelConnectionStatuses.Connected || _integrations is null)
            return (false, SupportDeliveryStatuses.Failed, null, "WhatsApp is disconnected. No message was sent.", "not_connected");

        var result = await _integrations.ExecuteAsync(
            "whatsapp",
            "sendTemplate",
            $"{{\"body\":{System.Text.Json.JsonSerializer.Serialize(body)},\"ticketNumber\":{System.Text.Json.JsonSerializer.Serialize(ticket.TicketNumber)}}}",
            true,
            cancellationToken);

        if (!result.Performed)
            return (false, SupportDeliveryStatuses.Failed, null, result.Message, result.Status);

        return (true, SupportDeliveryStatuses.Sent, result.Data["id"]?.ToString(), result.Message, null);
    }

    private SupportChannelDto Unavailable(string error) =>
        new(Channel, DisplayName, SupportChannelConnectionStatuses.Unavailable, false, false, Implementation, null, null, null, error);
}

public sealed class MessengerSupportChannelAdapter : ISupportChannelAdapter
{
    public string Channel => SupportChannels.Messenger;
    public string DisplayName => "Facebook Messenger";
    public string Implementation => "UNAVAILABLE";

    public Task<SupportChannelDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupportChannelDto(
            Channel,
            DisplayName,
            SupportChannelConnectionStatuses.Unavailable,
            false,
            false,
            Implementation,
            null,
            null,
            null,
            "Messenger customer messaging is not implemented. Meta Page publish exists separately under Integrations."));

    public Task<(bool Performed, string DeliveryStatus, string? ExternalMessageId, string Message, string? Error)> SendAsync(
        SupportTicketDetailDto ticket,
        string body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(bool, string, string?, string, string?)>((
            false,
            SupportDeliveryStatuses.Failed,
            null,
            "Messenger is unavailable. No message was sent.",
            "unavailable"));
}

public sealed class SupportChannelRouter
{
    private readonly IReadOnlyDictionary<string, ISupportChannelAdapter> _adapters;

    public SupportChannelRouter(IEnumerable<ISupportChannelAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(x => x.Channel, StringComparer.OrdinalIgnoreCase);
    }

    public ISupportChannelAdapter Resolve(string? channel)
    {
        var key = string.IsNullOrWhiteSpace(channel) ? SupportChannels.Sandbox : channel.Trim();
        if (_adapters.TryGetValue(key, out var adapter))
            return adapter;
        return _adapters[SupportChannels.Sandbox];
    }

    public IReadOnlyList<ISupportChannelAdapter> All => _adapters.Values.ToList();
}
