using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>
/// Routes speech to the configured provider, then the fallback, and caches identical audio.
/// </summary>
public sealed class SophiaTtsService : ISophiaTtsService
{
    private readonly IReadOnlyList<ITtsProvider> _providers;
    private readonly TtsAudioCache _cache;
    private readonly TtsOptions _options;
    private readonly ILogger<SophiaTtsService> _logger;

    public SophiaTtsService(
        IEnumerable<ITtsProvider> providers,
        TtsAudioCache cache,
        IOptions<TtsOptions> options,
        ILogger<SophiaTtsService> logger)
    {
        _providers = providers.ToList();
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public string ActiveProvider => ResolveChain().FirstOrDefault()?.Key ?? "none";

    public string DefaultVoice => ResolveChain().FirstOrDefault()?.DefaultVoice ?? _options.DefaultVoice;

    public IReadOnlyList<TtsVoiceOption> ListVoices() =>
        ResolveChain().FirstOrDefault()?.Voices ?? [];

    public async Task<SophiaTtsResult> SynthesizeAsync(
        string text,
        string language,
        double speechRate = 1.0,
        string? voiceName = null,
        CancellationToken cancellationToken = default)
    {
        var spoken = TtsTextNormalizer.Normalize(text);
        if (string.IsNullOrWhiteSpace(spoken))
            throw new InvalidOperationException("Nothing to speak.");

        var rate = TtsTextNormalizer.ClampRate(speechRate);
        var chain = ResolveChain();
        if (chain.Count == 0)
            throw new InvalidOperationException("No speech provider is available.");

        Exception? lastError = null;
        foreach (var provider in chain)
        {
            var voice = ResolveVoice(provider, voiceName);
                var cacheKey = TtsTextNormalizer.CacheKey(
                    provider.Key,
                    provider.ModelId,
                    voice,
                    rate,
                    (string.IsNullOrWhiteSpace(language) ? "en" : language) + "\n" + spoken);
            try
            {
                var result = await _cache.GetOrCreateAsync(
                    cacheKey,
                    _options.CacheEnabled,
                    ct => provider.SynthesizeAsync(new TtsSynthesisRequest
                    {
                        Text = spoken,
                        Language = string.IsNullOrWhiteSpace(language) ? "en-US" : language,
                        VoiceName = voice,
                        SpeechRate = rate
                    }, ct),
                    cancellationToken);

                _logger.LogInformation(
                    "Speech {Cache} via {Provider} voice {Voice} ({Chars} chars)",
                    result.ServedFromCache ? "cache hit" : "synthesized",
                    string.IsNullOrWhiteSpace(result.Provider) ? provider.Key : result.Provider,
                    result.VoiceName,
                    spoken.Length);

                return result.Provider.Length == 0
                    ? new SophiaTtsResult
                    {
                        AudioBytes = result.AudioBytes,
                        ContentType = result.ContentType,
                        VoiceName = result.VoiceName,
                        Provider = provider.Key,
                        ServedFromCache = result.ServedFromCache
                    }
                    : result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Speech provider {Provider} failed", provider.Key);
            }
        }

        throw new InvalidOperationException("Speech synthesis is unavailable.", lastError);
    }

    private List<ITtsProvider> ResolveChain()
    {
        var chain = new List<ITtsProvider>();
        AddIfAvailable(chain, _options.Provider);
        if (!string.IsNullOrWhiteSpace(_options.FallbackProvider))
            AddIfAvailable(chain, _options.FallbackProvider);

        if (chain.Count == 0)
        {
            var any = _providers.FirstOrDefault(p => p.IsAvailable);
            if (any is not null)
                chain.Add(any);
        }

        return chain;
    }

    private void AddIfAvailable(List<ITtsProvider> chain, string key)
    {
        var provider = _providers.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (provider is null || !provider.IsAvailable)
            return;
        if (chain.Any(p => p.Key.Equals(provider.Key, StringComparison.OrdinalIgnoreCase)))
            return;
        chain.Add(provider);
    }

    private static string ResolveVoice(ITtsProvider provider, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) &&
            !requested.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            var match = provider.Voices.FirstOrDefault(v =>
                v.Id.Equals(requested.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Id;
        }

        return provider.DefaultVoice;
    }
}
