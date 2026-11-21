using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Tenant-scoped automation toggles for the Inventory &amp; Procurement agent.
/// </summary>
public class ProcurementAgentSettings : AuditableEntity
{
    public Guid TenantId { get; set; }
    public bool DailyInventoryMonitoring { get; set; } = true;
    public bool LowStockDetection { get; set; } = true;
    public bool DemandAnalysis { get; set; } = true;
    public bool ProcurementRecommendations { get; set; } = true;
    public bool PurchaseOrderStatusMonitoring { get; set; } = true;
    public bool DeliveryFollowUps { get; set; } = true;
    public bool InventoryDiscrepancyAlerts { get; set; } = true;
    public int SlowMovingDays { get; set; } = 90;
    public decimal OverstockMultiplier { get; set; } = 2.5m;
    public int MinimumHistoryDays { get; set; } = 14;
    /// <summary>lowest-total | fastest-delivery | preferred-history</summary>
    public string PreferredSupplierCriterion { get; set; } = "lowest-total";
}

/// <summary>
/// Durable procurement recommendation prepared by the agent. Approval is bound to ApprovalFingerprint.
/// </summary>
public class ProcurementRecommendation : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? SupplierId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string Reason { get; set; } = default!;
    public string Confidence { get; set; } = "low";
    public string Status { get; set; } = "Prepared";
    public string ApprovalFingerprint { get; set; } = default!;
    public string? IdempotencyKey { get; set; }
    public Guid? PurchaseId { get; set; }
    public Guid? ApprovalId { get; set; }
    public string Source { get; set; } = "agent";
    public string? AssumptionsJson { get; set; }

    public Product? Product { get; set; }
    public Supplier? Supplier { get; set; }
    public Purchase? Purchase { get; set; }
}

/// <summary>
/// Verified receiving event. Stock only changes when this record is created successfully.
/// </summary>
public class PurchaseReceivingEvent : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseId { get; set; }
    public string ReceivedByUserId { get; set; } = default!;
    public string? Notes { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public decimal StockIncreasedBy { get; set; }
    public string ResultStatus { get; set; } = default!;

    public Purchase? Purchase { get; set; }
}
