using System.Net;
using System.Text;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Agents;
using BusinessOS.Infrastructure.AI.Copilot;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.UnitTests.AI;

public class OpenAiConfigurationSecurityTests
{
    private const string FakeKey = "sk-test-not-a-real-key";

    [Fact]
    public void Configuration_BindsAiOpenAiApiKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:OpenAiApiKey"] = FakeKey,
                ["Ai:OpenAiModel"] = "gpt-4o-mini",
                ["Ai:OpenAiEmbeddingModel"] = "text-embedding-3-small",
                ["Ai:Tts:OpenAiModel"] = "gpt-4o-mini-tts",
                ["Ai:Tts:DefaultVoice"] = "coral"
            })
            .Build();

        var ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>();
        var speech = configuration.GetSection(TtsOptions.SectionName).Get<TtsOptions>();

        ai!.OpenAiApiKey.Should().Be(FakeKey);
        ai.OpenAiModel.Should().Be("gpt-4o-mini");
        ai.OpenAiEmbeddingModel.Should().Be("text-embedding-3-small");
        speech!.OpenAiModel.Should().Be("gpt-4o-mini-tts");
        speech.DefaultVoice.Should().Be("coral");
    }

    [Fact]
    public void EnvironmentVariable_Ai__OpenAiApiKey_OverridesTheEmptySetting()
    {
        const string name = "BUSINESSOS_TEST_Ai__OpenAiApiKey";
        Environment.SetEnvironmentVariable(name, FakeKey);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:OpenAiApiKey"] = "" })
                .AddEnvironmentVariables("BUSINESSOS_TEST_")
                .Build();

            configuration["Ai:OpenAiApiKey"].Should().Be(FakeKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void CommittedSettings_DoNotContainAnOpenAiKey()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
        {
            Path.Combine("BusinessOS.API", "appsettings.json"),
            Path.Combine("BusinessOS.API", "appsettings.Development.json"),
            Path.Combine("BusinessOS.API", "appsettings.Production.json")
        })
        {
            var json = File.ReadAllText(Path.Combine(root, relative));
            json.Should().NotMatchRegex(@"sk-[A-Za-z0-9_\-]{20,}");
        }

        var appsettings = File.ReadAllText(Path.Combine(root, "BusinessOS.API", "appsettings.json"));
        appsettings.Should().Contain("\"OpenAiApiKey\": \"\"");
    }

    [Fact]
    public void DiagnosticsContracts_DoNotExposeTheOpenAiKey()
    {
        var names = typeof(AiDiagnosticsSummaryDto).GetProperties().Select(property => property.Name)
            .Concat(typeof(AiCopilotChatResponse).GetProperties().Select(property => property.Name))
            .Concat(typeof(AiCopilotDiagnosticsDto).GetProperties().Select(property => property.Name));

        names.Should().NotContain(name => name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, OpenAiFailureKind.Unauthorized, "rejected the API key")]
    [InlineData(HttpStatusCode.Forbidden, OpenAiFailureKind.Forbidden, "refused this request")]
    [InlineData(HttpStatusCode.TooManyRequests, OpenAiFailureKind.RateLimited, "rate limiting")]
    [InlineData(HttpStatusCode.InternalServerError, OpenAiFailureKind.Unavailable, "temporarily unavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, OpenAiFailureKind.Unavailable, "temporarily unavailable")]
    public void ProviderStatus_BecomesASafeClientMessage(HttpStatusCode status, OpenAiFailureKind kind, string excerpt)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent($"{{\"error\":{{\"message\":\"{FakeKey} leaked\"}}}}", Encoding.UTF8, "application/json")
        };
        response.Headers.TryAddWithoutValidation("x-request-id", "req_" + (int)status);
        response.Headers.TryAddWithoutValidation("openai-organization", FakeKey);

        var failure = OpenAiHttp.Describe(response);

        failure.Kind.Should().Be(kind);
        failure.StatusCode.Should().Be((int)status);
        failure.ClientMessage.Should().Contain(excerpt);
        failure.ClientMessage.Should().NotContain(FakeKey);
        failure.CorrelationId.Should().Be("req_" + (int)status);
        failure.ToString().Should().NotContain(FakeKey);
        OpenAiHttp.Redact($"prefix {FakeKey} bearer {FakeKey}").Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task ChatClient_InvalidKey_DoesNotLogOrReturnTheSecret()
    {
        var logger = new CaptureLogger<OpenAiChatClient>();
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent($"{{\"error\":{{\"message\":\"Incorrect API key {FakeKey}\"}}}}", Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req_chat_401");
            return response;
        });
        var client = new OpenAiChatClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") },
            Options.Create(new AiOptions { OpenAiApiKey = FakeKey, OpenAiModel = "gpt-4o-mini" }),
            logger);

        var reply = await client.GenerateReplyAsync(Guid.NewGuid(), "user", "system", "hello");

        reply.Should().BeNull();
        client.LastFailure.Should().NotBeNull();
        client.LastFailure!.Kind.Should().Be(OpenAiFailureKind.Unauthorized);
        client.LastFailure.CorrelationId.Should().Be("req_chat_401");
        client.LastFailure.ClientMessage.Should().NotContain(FakeKey);
        handler.Authorization.Should().Be(FakeKey);
        var logged = string.Join('\n', logger.Messages);
        logged.Should().Contain("401");
        logged.Should().Contain("req_chat_401");
        logged.Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task ToolLoop_DoesNotRetryAnInvalidKey()
    {
        var calls = 0;
        var client = new OpenAiChatClient(
            new HttpClient(new RecordingHandler(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent($"{{\"error\":\"{FakeKey}\"}}", Encoding.UTF8, "application/json")
                };
            }))
            { BaseAddress = new Uri("https://api.openai.com/") },
            Options.Create(new AiOptions { OpenAiApiKey = FakeKey, OpenAiModel = "gpt-4o-mini" }),
            new CaptureLogger<OpenAiChatClient>());

        var result = await client.RunToolLoopAsync(
            Guid.NewGuid(),
            "user",
            "system",
            "hello",
            [new ModelToolDefinition("GetRevenue", "Revenue")],
            (_, _) => Task.FromResult("unused"));

        calls.Should().Be(1);
        result!.Completed.Should().BeFalse();
        client.LastFailure!.Kind.Should().Be(OpenAiFailureKind.Unauthorized);
        client.LastFailure.ClientMessage.Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task Embeddings_MissingKey_UsesTheLocalFallbackWithoutCallingOpenAi()
    {
        var calls = 0;
        var client = new OpenAiEmbeddingClient(
            new HttpClient(new RecordingHandler(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            })),
            Options.Create(new AiOptions { OpenAiApiKey = "  " }),
            new CaptureLogger<OpenAiEmbeddingClient>());

        var vector = await client.GenerateEmbeddingAsync("invoice total", vectorSize: 16);

        calls.Should().Be(0);
        vector.Should().HaveCount(16);
    }

    [Fact]
    public async Task Embeddings_InvalidKey_ThrowsASafeError_InsteadOfAFallbackVector()
    {
        var logger = new CaptureLogger<OpenAiEmbeddingClient>();
        var client = new OpenAiEmbeddingClient(
            new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(FakeKey, Encoding.UTF8, "application/json")
            }))
            { BaseAddress = new Uri("https://api.openai.com/") },
            Options.Create(new AiOptions { OpenAiApiKey = FakeKey, OpenAiEmbeddingModel = "text-embedding-3-small" }),
            logger);

        var act = () => client.GenerateEmbeddingAsync("invoice total", vectorSize: 16);

        var error = await act.Should().ThrowAsync<OpenAiProviderException>();
        error.Which.Message.Should().NotContain(FakeKey);
        string.Join('\n', logger.Messages).Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task StartupCheck_MissingKey_WarnsAndDoesNotCallOpenAi()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var logger = new CaptureLogger<OpenAiStartupCheck>();
        var check = CreateCheck(handler, logger, apiKey: "");

        await check.StartAsync(CancellationToken.None);

        handler.Calls.Should().Be(0);
        var logged = string.Join('\n', logger.Messages);
        logged.Should().Contain("missing");
        logged.Should().Contain("dotnet user-secrets set");
        logged.Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task StartupCheck_ConfiguredKey_ProbesModelsWithoutLoggingTheKey()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"data\":[{{\"id\":\"{FakeKey}\"}}]}}", Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req_models");
            return response;
        });
        var logger = new CaptureLogger<OpenAiStartupCheck>();
        var check = CreateCheck(handler, logger, FakeKey);

        await check.StartAsync(CancellationToken.None);

        handler.Calls.Should().Be(1);
        handler.LastPath.Should().Be("/v1/models");
        handler.Authorization.Should().Be(FakeKey);
        var logged = string.Join('\n', logger.Messages);
        logged.Should().Contain("accepted");
        logged.Should().Contain("req_models");
        logged.Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task StartupCheck_RejectedKey_LogsTheStatusWithoutTheBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(FakeKey, Encoding.UTF8, "application/json")
        });
        var logger = new CaptureLogger<OpenAiStartupCheck>();
        var check = CreateCheck(handler, logger, FakeKey);

        await check.StartAsync(CancellationToken.None);

        var logged = string.Join('\n', logger.Messages);
        logged.Should().Contain("401");
        logged.Should().NotContain(FakeKey);
    }

    [Fact]
    public async Task StartupCheck_TestingEnvironment_DoesNotProbe()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var logger = new CaptureLogger<OpenAiStartupCheck>();
        var check = CreateCheck(handler, logger, FakeKey, environmentName: "Testing");

        await check.StartAsync(CancellationToken.None);

        handler.Calls.Should().Be(0);
        logger.Messages.Should().BeEmpty();
    }

    private static OpenAiStartupCheck CreateCheck(
        RecordingHandler handler,
        CaptureLogger<OpenAiStartupCheck> logger,
        string apiKey,
        string environmentName = "Development")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        return new OpenAiStartupCheck(
            new StubHttpClientFactory(http),
            Options.Create(new AiOptions
            {
                OpenAiApiKey = apiKey,
                OpenAiModel = "gpt-4o-mini",
                OpenAiEmbeddingModel = "text-embedding-3-small",
                OpenAiBaseUrl = "https://api.openai.com/"
            }),
            Options.Create(new TtsOptions { OpenAiModel = "gpt-4o-mini-tts", DefaultVoice = "coral" }),
            new TestHostEnvironment { EnvironmentName = environmentName },
            logger);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BusinessOS.API", "appsettings.json")))
                return dir.FullName;

            var nested = Path.Combine(dir.FullName, "BusinessOS");
            if (File.Exists(Path.Combine(nested, "BusinessOS.API", "appsettings.json")))
                return nested;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate BusinessOS.API/appsettings.json.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "BusinessOS.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorization = request.Headers.Authorization?.Parameter;
            LastPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception) + (exception is null ? "" : exception.ToString()));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
