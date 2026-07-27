using System.Diagnostics;
using BusinessOS.Application.Common.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Dictionary<string, string> BusinessEvents = new(StringComparer.Ordinal)
    {
        ["LoginCommand"] = "User login requested",
        ["RegisterCommand"] = "User registration requested",
        ["CreateProductCommand"] = "Product created",
        ["UpdateProductCommand"] = "Product updated",
        ["DeleteProductCommand"] = "Product deleted",
        ["CreateCategoryCommand"] = "Category created",
        ["UpdateCategoryCommand"] = "Category updated",
        ["DeleteCategoryCommand"] = "Category deleted",
        ["CreateOrderCommand"] = "Sale (order) created",
        ["UpdateOrderCommand"] = "Sale (order) updated",
        ["DeleteOrderCommand"] = "Sale (order) deleted",
        ["CreatePurchaseOrderCommand"] = "Purchase created",
        ["UpdatePurchaseOrderCommand"] = "Purchase updated",
        ["DeletePurchaseOrderCommand"] = "Purchase deleted",
        ["ReceivePurchaseOrderCommand"] = "Purchase received (stock updated)",
        ["CreateInvoiceFromOrderCommand"] = "Invoice generated",
        ["UpdateInvoiceCommand"] = "Invoice updated",
        ["DeleteInvoiceCommand"] = "Invoice deleted",
        ["UpdateInventoryCommand"] = "Stock levels updated",
        ["IncreaseStockCommand"] = "Stock increased",
        ["DecreaseStockCommand"] = "Stock decreased",
        ["AdjustStockCommand"] = "Stock adjusted"
    };

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly IOptionsMonitor<LoggingPerformanceOptions> _performanceOptions;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        IOptionsMonitor<LoggingPerformanceOptions> performanceOptions)
    {
        _logger = logger;
        _performanceOptions = performanceOptions;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var isBusinessEvent = BusinessEvents.TryGetValue(requestName, out var businessEvent);

        _logger.LogDebug("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var threshold = _performanceOptions.CurrentValue.MediatRWarningThresholdMs;

            if (threshold > 0 && elapsedMs >= threshold)
            {
                _logger.LogWarning(
                    "Slow MediatR handler {RequestName} completed in {ElapsedMilliseconds}ms (threshold {ThresholdMs}ms)",
                    requestName,
                    elapsedMs,
                    threshold);
            }
            else if (isBusinessEvent)
            {
                _logger.LogInformation(
                    "{BusinessEvent} via {RequestName} in {ElapsedMilliseconds}ms",
                    businessEvent,
                    requestName,
                    elapsedMs);
            }
            else
            {
                _logger.LogDebug(
                    "Handled {RequestName} in {ElapsedMilliseconds}ms",
                    requestName,
                    elapsedMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
