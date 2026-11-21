namespace BusinessOS.Application.Features.ProcurementAgent;

public interface IInventoryProcurementAgentService
{
    Task<ProcurementOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryRiskDto>> GetInventoryRisksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandInsightDto>> AnalyzeDemandAsync(Guid? productId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierOptionDto>> CompareSuppliersAsync(Guid productId, decimal quantity, CancellationToken cancellationToken = default);
    Task<PreparedPurchaseOrderDto> PreparePurchaseOrderAsync(PreparePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PreparedPurchaseOrderDto> SubmitPreparedPurchaseAsync(Guid recommendationId, string? idempotencyKey, bool approved, string? approvalFingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeliveryTrackingDto>> GetDeliveriesAsync(CancellationToken cancellationToken = default);
    Task<ReceiveVerificationResultDto> ReceiveVerifiedAsync(ReceiveVerificationRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementAutomationSettingsDto> GetAutomationSettingsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementAutomationSettingsDto> SaveAutomationSettingsAsync(ProcurementAutomationSettingsWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementActivityDto>> GetActivityAsync(CancellationToken cancellationToken = default);
    Task RunScheduledMonitorAsync(CancellationToken cancellationToken = default);
}

public interface ISupplierResearchProvider
{
    string ProviderKey { get; }
    string Classification { get; } // REAL | SIMULATED | UNAVAILABLE | BLOCKED
    bool IsAvailable { get; }
    Task<IReadOnlyList<SupplierResearchQuoteDto>> ResearchAsync(
        Guid tenantId,
        string productName,
        string? sku,
        decimal quantity,
        CancellationToken cancellationToken = default);
}

public sealed class PreparePurchaseOrderRequest
{
    public Guid ProductId { get; set; }
    public Guid SupplierId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Reason { get; set; }
    public string? IdempotencyKey { get; set; }
    public string Source { get; set; } = "agent";
}

public sealed class ReceiveVerificationRequest
{
    public Guid PurchaseOrderId { get; set; }
    public IReadOnlyList<ReceiveVerificationLineRequest> Lines { get; set; } = [];
    public string? Notes { get; set; }
}

public sealed class ReceiveVerificationLineRequest
{
    public Guid PurchaseItemId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
    public string? DiscrepancyNotes { get; set; }
}

public sealed record ProcurementOverviewDto(
    int ActiveProducts,
    int LowStockCount,
    int OutOfStockCount,
    int SlowMovingCount,
    int OverstockCount,
    int OpenPurchaseOrders,
    int DelayedDeliveries,
    decimal EstimatedProcurementNeed,
    string DataClassification);

public sealed record InventoryRiskDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal CurrentStock,
    decimal ReorderLevel,
    decimal MaximumStockLevel,
    decimal IncomingQuantity,
    decimal EffectiveAvailable,
    string RiskKind,
    decimal? AverageDailyDemand,
    decimal? DaysOfCover,
    DateTime? EstimatedStockoutDate,
    decimal SuggestedReorderQuantity,
    string Confidence,
    string Explanation);

public sealed record DemandInsightDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitsLast30Days,
    decimal UnitsLast90Days,
    decimal? AverageDailyDemand,
    decimal? AverageMonthlyDemand,
    decimal CurrentStock,
    decimal? DaysOfCover,
    DateTime? EstimatedStockoutDate,
    decimal SuggestedReorderQuantity,
    string Confidence,
    string Assumptions,
    bool ForecastAvailable);

public sealed record SupplierOptionDto(
    Guid? SupplierId,
    string SupplierName,
    string Source,
    string Classification,
    decimal UnitPrice,
    decimal Quantity,
    decimal MinimumOrderQuantity,
    decimal TotalCost,
    int? EstimatedDeliveryDays,
    string? PaymentTerms,
    string? Availability,
    string? Notes);

public sealed record ProcurementRecommendationDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal CurrentStock,
    decimal ReorderLevel,
    decimal IncomingQuantity,
    decimal SuggestedQuantity,
    Guid? PreferredSupplierId,
    string? PreferredSupplierName,
    decimal? PreferredUnitPrice,
    decimal? EstimatedTotalCost,
    string Confidence,
    string Reason,
    string Status,
    string ApprovalFingerprint,
    DateTime CreatedAt);

public sealed record PreparedPurchaseOrderDto(
    Guid RecommendationId,
    Guid? PurchaseOrderId,
    string Status,
    string ApprovalFingerprint,
    bool RequiresApproval,
    bool Created,
    bool DuplicatePrevented,
    string Summary,
    Guid ProductId,
    Guid SupplierId,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTime? ExpectedDeliveryDate);

public sealed record DeliveryTrackingDto(
    Guid PurchaseOrderId,
    string ReferenceNumber,
    Guid SupplierId,
    string SupplierName,
    string Status,
    DateTime PurchaseDate,
    DateTime? ExpectedDeliveryDate,
    bool IsDelayed,
    int DaysLate,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal RemainingQuantity);

public sealed record ReceiveVerificationResultDto(
    Guid PurchaseOrderId,
    string Status,
    decimal StockIncreasedBy,
    IReadOnlyList<string> Discrepancies,
    string Summary);

public sealed record ProcurementAutomationSettingsDto(
    bool DailyInventoryMonitoring,
    bool LowStockDetection,
    bool DemandAnalysis,
    bool ProcurementRecommendations,
    bool PurchaseOrderStatusMonitoring,
    bool DeliveryFollowUps,
    bool InventoryDiscrepancyAlerts,
    int SlowMovingDays,
    decimal OverstockMultiplier,
    int MinimumHistoryDays,
    string PreferredSupplierCriterion,
    DateTime? UpdatedAt);

public sealed class ProcurementAutomationSettingsWriteRequest
{
    public bool? DailyInventoryMonitoring { get; set; }
    public bool? LowStockDetection { get; set; }
    public bool? DemandAnalysis { get; set; }
    public bool? ProcurementRecommendations { get; set; }
    public bool? PurchaseOrderStatusMonitoring { get; set; }
    public bool? DeliveryFollowUps { get; set; }
    public bool? InventoryDiscrepancyAlerts { get; set; }
    public int? SlowMovingDays { get; set; }
    public decimal? OverstockMultiplier { get; set; }
    public int? MinimumHistoryDays { get; set; }
    public string? PreferredSupplierCriterion { get; set; }
}

public sealed record ProcurementActivityDto(
    Guid Id,
    string Kind,
    string Title,
    string Summary,
    string Status,
    DateTime CreatedAt,
    Guid? RelatedPurchaseOrderId,
    Guid? RelatedProductId);

public sealed record SupplierResearchQuoteDto(
    string SupplierName,
    string? ExternalSupplierKey,
    decimal UnitPrice,
    decimal MinimumOrderQuantity,
    int? EstimatedDeliveryDays,
    string? PaymentTerms,
    string? Availability,
    string Classification,
    string? Notes);
