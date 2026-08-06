using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BusinessOS.Application.Features.Agents.Services;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>
/// Free Microsoft Edge Read Aloud neural TTS (same engine as Edge browser).
/// Uses en-US-JennyNeural for natural English spoken replies.
/// </summary>
public sealed class EdgeNeuralTtsService : ISophiaTtsService
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string EdgeVersion = "1-130.0.2849.68";
    private const long WinEpochSeconds = 11_644_473_600L;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EdgeNeuralTtsService> _logger;

    public EdgeNeuralTtsService(
        IHttpClientFactory httpClientFactory,
        ILogger<EdgeNeuralTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SophiaTtsResult> SynthesizeAsync(
        string text,
        string language,
        double speechRate = 1.0,
        CancellationToken cancellationToken = default)
    {
        var cleaned = CleanForSpeech(text);
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new InvalidOperationException("Nothing to speak.");

        // Keep TTS snappy — long tool dumps should not be fully narrated.
        if (cleaned.Length > 900)
            cleaned = cleaned[..900].TrimEnd() + "…";

        const string voice = "en-US-JennyNeural";
        const string xmlLang = "en-US";

        try
        {
            var mp3 = await SynthesizeWithEdgeAsync(cleaned, voice, xmlLang, speechRate, cancellationToken);
            if (mp3.Length > 0)
            {
                return new SophiaTtsResult
                {
                    AudioBytes = mp3,
                    ContentType = "audio/mpeg",
                    VoiceName = voice
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Edge neural TTS failed for {Voice}", voice);
        }

        throw new InvalidOperationException("Speech synthesis failed.");
    }

    private async Task<byte[]> SynthesizeWithEdgeAsync(
        string text,
        string voice,
        string xmlLang,
        double speechRate,
        CancellationToken cancellationToken)
    {
        var clockSkewSeconds = await EstimateClockSkewSecondsAsync(cancellationToken);
        var connectionId = Guid.NewGuid().ToString("N");
        var requestId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec(clockSkewSeconds);
        var uri = new Uri(
            "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
            $"?TrustedClientToken={TrustedClientToken}" +
            $"&ConnectionId={connectionId}" +
            $"&Sec-MS-GEC={secMsGec}" +
            $"&Sec-MS-GEC-Version={EdgeVersion}");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
        try
        {
            ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckidbmigfz");
        }
        catch
        {
            // Some platforms disallow Origin header on ClientWebSocket.
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(45));

        await ws.ConnectAsync(uri, linked.Token);

        var ratePercent = RateToPercent(speechRate);
        var timestamp = DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);

        var configPayload =
            "X-Timestamp:" + timestamp + "\r\n" +
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            """{"context":{"synthesis":{"audio":{"metadataoptions":{"sentenceBoundaryEnabled":false,"wordBoundaryEnabled":false},"outputFormat":"audio-24khz-48kbitrate-mono-mp3"}}}}""";

        await ws.SendAsync(Encoding.UTF8.GetBytes(configPayload), WebSocketMessageType.Text, true, linked.Token);

        var ssml = BuildSsml(text, voice, xmlLang, ratePercent);
        var ssmlPayload =
            "X-RequestId:" + requestId + "\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            "X-Timestamp:" + timestamp + "\r\n" +
            "Path:ssml\r\n\r\n" +
            ssml;

        await ws.SendAsync(Encoding.UTF8.GetBytes(ssmlPayload), WebSocketMessageType.Text, true, linked.Token);

        await using var audio = new MemoryStream();
        var buffer = new byte[64 * 1024];

        while (ws.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, linked.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                    break;
                }

                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var bytes = message.ToArray();
            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Edge binary frames: 2-byte big-endian header length + headers + mp3 payload
                if (bytes.Length >= 2)
                {
                    var headerLen = (bytes[0] << 8) | bytes[1];
                    var audioStart = 2 + headerLen;
                    if (headerLen >= 0 && audioStart < bytes.Length)
                    {
                        audio.Write(bytes, audioStart, bytes.Length - audioStart);
                    }
                    else
                    {
                        // Legacy fallback: header\r\n\r\n + payload
                        var sep = IndexOfSeparator(bytes);
                        if (sep >= 0 && sep < bytes.Length)
                            audio.Write(bytes, sep, bytes.Length - sep);
                    }
                }

                continue;
            }

            var textMsg = Encoding.UTF8.GetString(bytes);
            if (textMsg.Contains("Path:turn.end", StringComparison.OrdinalIgnoreCase))
                break;
            if (textMsg.Contains("Path:audio", StringComparison.OrdinalIgnoreCase))
            {
                var sep = textMsg.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (sep >= 0)
                {
                    // Rare text-framed audio with base64 — ignore; Edge usually sends binary.
                }
            }
        }

        if (ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

        return audio.ToArray();
    }

    private static string BuildSsml(string text, string voice, string xmlLang, string ratePercent)
    {
        // Escape XML properly.
        var escaped = new XText(text).ToString();
        return
            $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{xmlLang}'>" +
            $"<voice name='{voice}'>" +
            $"<prosody rate='{ratePercent}'>{escaped}</prosody>" +
            "</voice></speak>";
    }

    private static string RateToPercent(double rate)
    {
        // 1.0 → +0%, 0.9 → -10%, 1.1 → +10%
        var pct = (int)Math.Round((rate - 1.0) * 100);
        pct = Math.Clamp(pct, -50, 50);
        return pct >= 0 ? $"+{pct}%" : $"{pct}%";
    }

    private async Task<double> EstimateClockSkewSecondsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SophiaTts");
            using var req = new HttpRequestMessage(HttpMethod.Head, "https://www.bing.com/");
            req.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (resp.Headers.Date is DateTimeOffset serverDate)
                return (serverDate - DateTimeOffset.UtcNow).TotalSeconds;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not estimate clock skew for Edge TTS");
        }

        return 0;
    }

    private static string GenerateSecMsGec(double clockSkewSeconds = 0)
    {
        var ticks = (long)Math.Floor(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + clockSkewSeconds + WinEpochSeconds);
        ticks -= ticks % 300;
        var payload = ticks.ToString(CultureInfo.InvariantCulture) + TrustedClientToken;
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static int IndexOfSeparator(byte[] data)
    {
        // Find \r\n\r\n and return index after it
        for (var i = 0; i < data.Length - 3; i++)
        {
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10)
                return i + 4;
        }

        return -1;
    }

    private static string CleanForSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var s = text;
        s = Regex.Replace(s, @"\*\*|__|`+|#+", " ");
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");
        s = Regex.Replace(s, @"https?://\S+", " ");
        s = Regex.Replace(s, @"\r\n?|\n", ". ");
        s = Regex.Replace(s, @"\s{2,}", " ");
        return s.Trim();
    }
}
