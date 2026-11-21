using BusinessOS.Application.Features.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Support;

/// <summary>
/// Polls due support follow-up jobs and sends them through connected/sandbox channels.
/// </summary>
public sealed class SupportFollowUpProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SupportFollowUpProcessor> _logger;

    public SupportFollowUpProcessor(IServiceScopeFactory scopes, ILogger<SupportFollowUpProcessor> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var support = scope.ServiceProvider.GetRequiredService<ISupportAgentService>();
                var processed = await support.ProcessDueFollowUpsAsync(stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("Processed {Count} support follow-up jobs.", processed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Support follow-up processor pass failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
