using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Integrations;

/// <summary>
/// Retries open integration dead-letter rows with exponential backoff.
/// Retries call the real platform execute path — never invents successful provider responses.
/// </summary>
public sealed class IntegrationDeadLetterProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<IntegrationDeadLetterProcessor> _logger;

    public IntegrationDeadLetterProcessor(IServiceScopeFactory scopes, ILogger<IntegrationDeadLetterProcessor> logger)
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
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Integration dead-letter processor pass failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var now = DateTime.UtcNow;
        var due = await db.IntegrationDeadLetters.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && x.Status == "Open"
                        && x.Attempts < 8
                        && x.NextAttemptAt != null
                        && x.NextAttemptAt <= now
                        && x.ConnectionId != null)
            .OrderBy(x => x.NextAttemptAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var row in due)
        {
            if (!IntegrationProviderCatalog.TryGet(row.ProviderKey, out var definition) || definition is null)
                continue;
            var operation = definition.ReadOperations.FirstOrDefault(op => op is not ("get" or "payment" or "order" or "report" or "status" or "messages"))
                            ?? definition.ReadOperations.FirstOrDefault();
            if (operation is null)
            {
                await MarkExhaustedAsync(row.Id, "No safe read operation available for retry.", cancellationToken);
                continue;
            }

            using var scope = _scopes.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenant.SetTenantId(row.TenantId);
            var platform = scope.ServiceProvider.GetRequiredService<IIntegrationPlatform>();
            var scopedDb = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var tracked = await scopedDb.IntegrationDeadLetters.FirstOrDefaultAsync(x => x.Id == row.Id, cancellationToken);
            if (tracked is null)
                continue;

            try
            {
                var result = await platform.ExecuteAsync(tracked.ProviderKey, operation, "{}", false, cancellationToken);
                tracked.Attempts += 1;
                if (result.Performed)
                {
                    tracked.Status = "Resolved";
                    tracked.NextAttemptAt = default(DateTime?);
                    tracked.ErrorRedacted = string.Empty;
                }
                else
                {
                    var delayMinutes = Math.Min(120, (int)Math.Pow(2, Math.Max(1, tracked.Attempts)));
                    tracked.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                    tracked.ErrorRedacted = result.Message.Length > 300 ? result.Message[..300] : result.Message;
                    if (tracked.Attempts >= 8)
                        tracked.Status = "Exhausted";
                }

                await scopedDb.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                tracked.Attempts += 1;
                tracked.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(120, (int)Math.Pow(2, Math.Max(1, tracked.Attempts))));
                tracked.ErrorRedacted = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                if (tracked.Attempts >= 8)
                    tracked.Status = "Exhausted";
                await scopedDb.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex, "Dead-letter retry failed for {Provider} / {Operation}", tracked.ProviderKey, tracked.Operation);
            }
        }
    }

    private async Task MarkExhaustedAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var tracked = await db.IntegrationDeadLetters.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tracked is null)
            return;
        tracked.Status = "Exhausted";
        tracked.NextAttemptAt = null;
        tracked.ErrorRedacted = reason;
        await db.SaveChangesAsync(cancellationToken);
    }
}
