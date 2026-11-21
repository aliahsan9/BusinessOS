using System.Text;

namespace BusinessOS.Infrastructure.Integrations;

/// <summary>
/// Gmail API expects RFC 4648 base64url (no padding) for the raw MIME message.
/// </summary>
public static class GmailMime
{
    public static string EncodeRawMessage(string to, string subject, string body, string? from = null, string? inReplyTo = null, string? references = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(from))
            sb.Append("From: ").Append(from.Trim()).Append("\r\n");
        sb.Append("To: ").Append(to.Trim()).Append("\r\n");
        sb.Append("Subject: ").Append(EncodeHeader(subject)).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append("Content-Type: text/plain; charset=\"UTF-8\"\r\n");
        if (!string.IsNullOrWhiteSpace(inReplyTo))
            sb.Append("In-Reply-To: ").Append(inReplyTo.Trim()).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(references))
            sb.Append("References: ").Append(references.Trim()).Append("\r\n");
        sb.Append("\r\n");
        sb.Append(body ?? "");
        return ToBase64Url(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string EncodeHeader(string value)
    {
        if (string.IsNullOrEmpty(value) || value.All(c => c < 128))
            return value;
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return "=?UTF-8?B?" + encoded + "?=";
    }
}
