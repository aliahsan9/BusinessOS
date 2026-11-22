using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BusinessOS.Infrastructure.Integrations.Providers;

/// <summary>
/// Tenant SMTP send path. Encrypted SMTP JSON is stored in AccessTokenProtected and decrypted for ExecuteAsync.
/// </summary>
public sealed class SmtpIntegrationProvider : CatalogIntegrationProvider
{
    public SmtpIntegrationProvider() : base("smtp-email")
    {
    }

    public override async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadSecret(accessToken, out var secret))
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "configuration",
                Message = "SMTP credentials are missing or invalid. Reconfigure SMTP."
            };
        }

        if (string.Equals(operation, "status", StringComparison.OrdinalIgnoreCase))
        {
            return new IntegrationCallResult
            {
                Performed = true,
                Status = "completed",
                Message = "SMTP credentials are stored for " + secret.FromAddress + ".",
                Data = new JsonObject
                {
                    ["host"] = secret.Host,
                    ["port"] = secret.Port,
                    ["fromAddress"] = secret.FromAddress,
                    ["hasPassword"] = true
                }
            };
        }

        if (!string.Equals(operation, "send", StringComparison.OrdinalIgnoreCase))
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "not_enabled",
                Message = "That SMTP operation is not enabled."
            };
        }

        var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
        var to = args["to"]?.GetValue<string>() ?? "";
        var subject = args["subject"]?.GetValue<string>() ?? "Message";
        var body = args["body"]?.GetValue<string>() ?? args["text"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@', StringComparison.Ordinal))
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "validation",
                Message = "A valid recipient address is required."
            };
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(secret.FromDisplayName ?? secret.FromAddress, secret.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = NalvaGuard.Clip(subject, 200);
            message.Body = new TextPart("plain") { Text = NalvaGuard.Clip(body, 20000) };

            using var client = new SmtpClient();
            var secure = secret.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
            await client.ConnectAsync(secret.Host, secret.Port, secure, cancellationToken);
            await client.AuthenticateAsync(secret.Username, secret.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return new IntegrationCallResult
            {
                Performed = true,
                Status = "completed",
                Message = "The SMTP message was accepted by the mail server.",
                Data = new JsonObject { ["to"] = to, ["provider"] = "smtp-email" }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "provider_error",
                Message = "SMTP send failed: " + NalvaGuard.Redact(NalvaGuard.Clip(ex.Message, 200))
            };
        }
    }

    public override bool VerifyWebhookSignature(string secret, string body, string? signature) => false;

    private static bool TryReadSecret(string payload, out SmtpSecret secret)
    {
        secret = new SmtpSecret();
        try
        {
            var parsed = JsonSerializer.Deserialize<SmtpSecret>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Host) || string.IsNullOrWhiteSpace(parsed.Password))
                return false;
            secret = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class SmtpSecret
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public string? FromDisplayName { get; set; }
    }
}
