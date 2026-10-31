using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BusinessOS.UnitTests.AI;

public class SophiaTtsServiceTests
{
    [Fact]
    public async Task IdenticalText_IsSynthesizedOnce_AndTheSecondReplyComesFromCache()
    {
        var provider = new FakeTtsProvider { Key = "openai", DefaultVoice = "coral" };
        var service = CreateService(provider);

        var first = await service.SynthesizeAsync("Hello   there", "en", 1, "coral");
        var second = await service.SynthesizeAsync("Hello there", "en", 1, "coral");

        provider.Calls.Should().Be(1);
        first.ServedFromCache.Should().BeFalse();
        second.ServedFromCache.Should().BeTrue();
        second.AudioBytes.Should().Equal(first.AudioBytes);
        second.Provider.Should().Be("openai");
    }

    [Fact]
    public async Task DifferentVoices_AreNotTreatedAsTheSameAudio()
    {
        var provider = new FakeTtsProvider
        {
            Key = "openai",
            DefaultVoice = "coral",
            Voices =
            [
                new TtsVoiceOption { Id = "coral", Label = "Coral" },
                new TtsVoiceOption { Id = "nova", Label = "Nova" }
            ]
        };
        var service = CreateService(provider);

        await service.SynthesizeAsync("Hello there", "en", 1, "coral");
        await service.SynthesizeAsync("Hello there", "en", 1, "nova");

        provider.Calls.Should().Be(2);
        provider.RequestedVoices.Should().Equal("coral", "nova");
    }

    [Fact]
    public async Task WhenOpenAiFails_TheFallbackProviderSpeaks()
    {
        var openai = new FakeTtsProvider
        {
            Key = "openai",
            DefaultVoice = "coral",
            Fail = true
        };
        var edge = new FakeTtsProvider
        {
            Key = "edge",
            DefaultVoice = "en-US-JennyNeural",
            Voices = [new TtsVoiceOption { Id = "en-US-JennyNeural", Label = "Jenny" }]
        };
        var service = CreateService(openai, edge);

        var result = await service.SynthesizeAsync("Hello there", "en", 1, "coral");

        openai.Calls.Should().Be(1);
        edge.Calls.Should().Be(1);
        result.Provider.Should().Be("edge");
        result.VoiceName.Should().Be("en-US-JennyNeural");
    }

    [Fact]
    public async Task WhenOpenAiIsNotConfigured_ItIsSkipped()
    {
        var openai = new FakeTtsProvider { Key = "openai", IsAvailable = false };
        var edge = new FakeTtsProvider
        {
            Key = "edge",
            DefaultVoice = "en-US-JennyNeural",
            Voices = [new TtsVoiceOption { Id = "en-US-JennyNeural", Label = "Jenny" }]
        };
        var service = CreateService(openai, edge);

        service.ActiveProvider.Should().Be("edge");
        service.ListVoices().Should().ContainSingle(v => v.Id == "en-US-JennyNeural");

        await service.SynthesizeAsync("Hello there", "en");

        openai.Calls.Should().Be(0);
        edge.Calls.Should().Be(1);
    }

    [Fact]
    public async Task WhenEveryProviderFails_TheErrorDoesNotExposeProviderInternals()
    {
        var openai = new FakeTtsProvider { Key = "openai", Fail = true, FailureMessage = "sk-secret-should-not-leak" };
        var service = CreateService(openai, fallback: "none");

        var act = () => service.SynthesizeAsync("Hello there", "en");

        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Be("Speech synthesis is unavailable.");
        error.Which.Message.Should().NotContain("sk-secret");
    }

    private static SophiaTtsService CreateService(FakeTtsProvider primary, FakeTtsProvider? secondary = null, string? fallback = null)
    {
        var providers = secondary is null
            ? new ITtsProvider[] { primary }
            : new ITtsProvider[] { primary, secondary };

        var options = Options.Create(new TtsOptions
        {
            Provider = primary.Key,
            FallbackProvider = fallback ?? secondary?.Key ?? "",
            CacheEnabled = true,
            CacheMinutes = 30,
            CacheMaxEntries = 8
        });

        return new SophiaTtsService(
            providers,
            new TtsAudioCache(options),
            options,
            NullLogger<SophiaTtsService>.Instance);
    }

    private sealed class FakeTtsProvider : ITtsProvider
    {
        public string Key { get; init; } = "openai";
        public bool IsAvailable { get; init; } = true;
        public string ModelId => "fake-model";
        public string DefaultVoice { get; init; } = "coral";
        public IReadOnlyList<TtsVoiceOption> Voices { get; init; } =
            [new TtsVoiceOption { Id = "coral", Label = "Coral" }, new TtsVoiceOption { Id = "nova", Label = "Nova" }];

        public bool Fail { get; init; }
        public string FailureMessage { get; init; } = "provider failed";
        public int Calls { get; private set; }
        public List<string> RequestedVoices { get; } = [];

        public Task<SophiaTtsResult> SynthesizeAsync(TtsSynthesisRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            RequestedVoices.Add(request.VoiceName ?? "");
            if (Fail)
                throw new InvalidOperationException(FailureMessage);

            var audio = new byte[256];
            audio[0] = (byte)Calls;
            return Task.FromResult(new SophiaTtsResult
            {
                AudioBytes = audio,
                ContentType = "audio/mpeg",
                VoiceName = request.VoiceName ?? DefaultVoice,
                Provider = Key
            });
        }
    }
}

public class OpenAiTtsProviderTests
{
    private const string ApiKey = "sk-test-voice-key";

    [Fact]
    public async Task Synthesize_PostsSpeechRequest_WithoutPuttingTheKeyInTheBody()
    {
        var handler = new RecordingHandler(_ => AudioResponse());
        var provider = CreateProvider(handler, apiKey: ApiKey, model: "gpt-4o-mini-tts");

        var result = await provider.SynthesizeAsync(new TtsSynthesisRequest
        {
            Text = "Hello there",
            VoiceName = "ash",
            SpeechRate = 1
        });

        handler.Calls.Should().Be(1);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/v1/audio/speech");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be(ApiKey);
        handler.Body.Should().Contain("\"model\":\"gpt-4o-mini-tts\"");
        handler.Body.Should().Contain("\"voice\":\"ash\"");
        handler.Body.Should().Contain("\"response_format\":\"mp3\"");
        handler.Body.Should().Contain("\"input\":\"Hello there\"");
        handler.Body.Should().Contain("instructions");
        handler.Body.Should().NotContain(ApiKey);
        result.ContentType.Should().Be("audio/mpeg");
        result.VoiceName.Should().Be("ash");
        result.Provider.Should().Be("openai");
        result.AudioBytes.Length.Should().BeGreaterThan(32);
    }

    [Fact]
    public async Task Synthesize_UsesTheDefaultVoice_WhenTheRequestedVoiceIsUnknown()
    {
        var handler = new RecordingHandler(_ => AudioResponse());
        var provider = CreateProvider(handler, apiKey: ApiKey, model: "tts-1");

        var result = await provider.SynthesizeAsync(new TtsSynthesisRequest
        {
            Text = "Hello there",
            VoiceName = "not-a-voice"
        });

        result.VoiceName.Should().Be("coral");
        handler.Body.Should().Contain("\"voice\":\"coral\"");
        handler.Body.Should().NotContain("instructions");
    }

    [Fact]
    public async Task Synthesize_FailsCleanly_WhenOpenAiReturnsAnError()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent($"{{\"error\":{{\"message\":\"{ApiKey} leaked\"}}}}", Encoding.UTF8, "application/json")
        });
        var provider = CreateProvider(handler, apiKey: ApiKey, model: "gpt-4o-mini-tts");

        var act = () => provider.SynthesizeAsync(new TtsSynthesisRequest { Text = "Hello there", VoiceName = "coral" });

        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Be("OpenAI speech synthesis failed (401).");
        error.Which.Message.Should().NotContain(ApiKey);
    }

    [Fact]
    public void IsAvailable_IsFalse_WhenTheKeyIsMissing()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("should not be called"));
        var provider = CreateProvider(handler, apiKey: "  ", model: "gpt-4o-mini-tts");

        provider.IsAvailable.Should().BeFalse();
        provider.Voices.Should().NotBeEmpty();
    }

    private static OpenAiTtsProvider CreateProvider(RecordingHandler handler, string apiKey, string model)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        return new OpenAiTtsProvider(
            new StubHttpClientFactory(client),
            Options.Create(new AiOptions { OpenAiApiKey = apiKey, OpenAiBaseUrl = "https://api.openai.com/" }),
            Options.Create(new TtsOptions { OpenAiModel = model, DefaultVoice = "coral" }),
            NullLogger<OpenAiTtsProvider>.Instance);
    }

    private static HttpResponseMessage AudioResponse()
    {
        var audio = new byte[220];
        audio[0] = (byte)'I';
        audio[1] = (byte)'D';
        audio[2] = (byte)'3';
        var content = new ByteArrayContent(audio);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
