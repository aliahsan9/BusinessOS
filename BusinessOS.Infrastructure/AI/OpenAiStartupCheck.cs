using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// Warns when Ai:OpenAiApiKey is missing, and confirms a configured key with GET /v1/models.
/// That call lists models and does not generate tokens.
/// </summary>
public sealed class OpenAiStartupCheck : IHostedService
{
    public const string HttpClientName = "OpenAiProbe";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly Agents.TtsOptions _speech;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<OpenAiStartupCheck> _logger;

    public OpenAiStartupCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        IOptions<Agents.TtsOptions> speech,
        IHostEnvironment environment,
        ILogger<OpenAiStartupCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _speech = speech.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsEnvironment("Testing"))
            return;

        if (string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
        {
            _logger.LogWarning(
                "OpenAI API key is missing (Ai:OpenAiApiKey / Ai__OpenAiApiKey). Chat stays on built-in replies, embeddings stay on the local fallback, and speech uses the backup voice. Local .NET: dotnet user-secrets set \"Ai:OpenAiApiKey\" \"<key>\" --project BusinessOS.API. Docker: set OPENAI_API_KEY in deploy/env/.env.dev. Do not put the key in appsettings or source control.");
            return;
        }

        var speechModel = string.IsNullOrWhiteSpace(_speech.OpenAiModel) ? "gpt-4o-mini-tts" : _speech.OpenAiModel;
        _logger.LogInformation(
            "OpenAI API key is configured. Chat model {ChatModel}. Embedding model {EmbeddingModel}. Speech model {SpeechModel}.",
            _options.OpenAiModel,
            _options.OpenAiEmbeddingModel,
            speechModel);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, "v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var correlation = OpenAiHttp.Describe(response).CorrelationId;
                _logger.LogInformation(
                    "OpenAI accepted the configured API key. Status {StatusCode}. CorrelationId={CorrelationId}",
                    (int)response.StatusCode,
                    string.IsNullOrWhiteSpace(correlation) ? "none" : correlation);
                return;
            }

            var failure = OpenAiHttp.Describe(response);
            OpenAiHttp.LogFailure(_logger, "startup authentication", failure);
            _logger.LogWarning(
                "OpenAI is configured but did not accept the API key. Chat and speech will report the provider error instead of using a successful model reply.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "OpenAI authentication check could not reach the provider ({ExceptionType}).",
                ex.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
