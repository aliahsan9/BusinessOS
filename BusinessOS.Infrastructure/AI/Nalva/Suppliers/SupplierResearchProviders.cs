using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Application.Features.Nalva;

namespace BusinessOS.Infrastructure.AI.Nalva.Suppliers;

/// <summary>
/// Default production provider. No live supplier marketplace is connected.
/// Classification: UNAVAILABLE.
/// </summary>
public sealed class UnavailableSupplierResearchProvider : ISupplierResearchProvider
{
    public string ProviderKey => "none";
    public string Classification => "UNAVAILABLE";
    public bool IsAvailable => false;

    public Task<IReadOnlyList<SupplierResearchQuoteDto>> ResearchAsync(
        Guid tenantId,
        string productName,
        string? sku,
        decimal quantity,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierResearchQuoteDto>>([]);
}

/// <summary>
/// Deterministic test/demo provider. Never present as live supplier research.
/// Classification: SIMULATED.
/// </summary>
public sealed class TestSupplierResearchProvider : ISupplierResearchProvider
{
    public string ProviderKey => "test";
    public string Classification => "SIMULATED";
    public bool IsAvailable => true;

    public Task<IReadOnlyList<SupplierResearchQuoteDto>> ResearchAsync(
        Guid tenantId,
        string productName,
        string? sku,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        // Deterministic quotes derived from product name hash — never claimed as real market data.
        var seed = Math.Abs((productName + (sku ?? "")).GetHashCode(StringComparison.Ordinal));
        var basePrice = 10m + (seed % 40);
        var quotes = new List<SupplierResearchQuoteDto>
        {
            new(
                SupplierName: $"Simulated Supplier A ({NalvaGuard.Clip(productName, 40)})",
                ExternalSupplierKey: $"sim-a-{tenantId:N}"[..20],
                UnitPrice: decimal.Round(basePrice, 2),
                MinimumOrderQuantity: Math.Max(1, quantity > 50 ? 10 : 1),
                EstimatedDeliveryDays: 5 + (seed % 5),
                PaymentTerms: "Net 30 (simulated)",
                Availability: "SIMULATED availability",
                Classification: Classification,
                Notes: "SIMULATED test quote. Not a real supplier offer."),
            new(
                SupplierName: $"Simulated Supplier B ({NalvaGuard.Clip(productName, 40)})",
                ExternalSupplierKey: $"sim-b-{tenantId:N}"[..20],
                UnitPrice: decimal.Round(basePrice * 1.08m, 2),
                MinimumOrderQuantity: 1,
                EstimatedDeliveryDays: 3 + (seed % 3),
                PaymentTerms: "Prepaid (simulated)",
                Availability: "SIMULATED availability",
                Classification: Classification,
                Notes: "SIMULATED test quote. Not a real supplier offer.")
        };
        return Task.FromResult<IReadOnlyList<SupplierResearchQuoteDto>>(quotes);
    }
}
