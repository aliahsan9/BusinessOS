namespace BusinessOS.Application.Features.Operations.DTOs;

public record DepartmentRequest(string Name, string? Description, Guid? ManagerEmployeeId);
public record DepartmentDto(Guid Id, string Name, string? Description, Guid? ManagerEmployeeId, bool IsActive);

public record TeamRequest(string Name, string? Description, Guid? DepartmentId, Guid? LeaderEmployeeId);
public record TeamDto(Guid Id, string Name, string? Description, Guid? DepartmentId, Guid? LeaderEmployeeId, bool IsActive, int MemberCount);

public record EmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string JobTitle,
    DateTime JoiningDate,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerEmployeeId,
    string? EmploymentType,
    string? Skills,
    string? WorkLocation,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    decimal? Salary,
    string? UserId,
    string? ProfilePictureUrl);

public record EmployeeDto(
    Guid Id,
    string? EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string JobTitle,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    Guid? ManagerEmployeeId,
    DateTime JoiningDate,
    string EmploymentStatus,
    string EmploymentType,
    bool IsActive,
    string? Skills,
    string? WorkLocation,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? UserId,
    string? ProfilePictureUrl,
    decimal? Salary);

public record AttendanceDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly WorkDate,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    decimal WorkedHours,
    bool IsLate,
    bool IsEarlyDeparture,
    string Status,
    string? CorrectionReason);

public record CorrectAttendanceRequest(DateTime? CheckInAt, DateTime? CheckOutAt, string Reason);

public record LeaveTypeRequest(string Name, decimal DefaultDays, bool IsPaid);
public record LeaveTypeDto(Guid Id, string Name, decimal DefaultDays, bool IsPaid, bool IsActive);
public record LeaveRequestCreate(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string? Reason, Guid? EmployeeId);
public record LeaveDecisionRequest(string? Reason);
public record LeaveRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days,
    string Status,
    string? Reason,
    string? DecisionReason,
    decimal? RemainingBalance);

public record AnnouncementRequest(string Title, string Body, string Audience, Guid? DepartmentId, Guid? CompanyTeamId, string Priority, DateTime? ExpiresAt);
public record AnnouncementDto(Guid Id, string Title, string Body, string Audience, string Priority, DateTime? ExpiresAt, bool IsRead, DateTime CreatedAt);

public record GoalRequest(Guid EmployeeId, string Title, string? Description, string? KpiName, decimal TargetValue, decimal CurrentValue, DateOnly? PeriodStart, DateOnly? PeriodEnd);
public record GoalDto(Guid Id, Guid EmployeeId, string Title, string? KpiName, decimal TargetValue, decimal CurrentValue, string Status, DateOnly? PeriodStart, DateOnly? PeriodEnd);
public record ReviewRequest(Guid EmployeeId, string PeriodLabel, int Rating, string? Feedback, string Status);
public record ReviewDto(Guid Id, Guid EmployeeId, string PeriodLabel, int Rating, string? Feedback, string Status, DateTime? CompletedAt);

public record StageRequest(string Name, int SortOrder, bool IsWon, bool IsLost);
public record StageDto(Guid Id, string Name, int SortOrder, bool IsWon, bool IsLost);

public record LeadRequest(
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    string Source,
    string Priority,
    Guid? StageId,
    string? AssignedUserId,
    decimal EstimatedValue,
    DateTime? ExpectedCloseDate,
    string? Notes);

public record LeadDto(
    Guid Id,
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    string Source,
    string Status,
    string Priority,
    Guid? StageId,
    string? StageName,
    string? AssignedUserId,
    decimal EstimatedValue,
    DateTime? ExpectedCloseDate,
    bool IsQualified,
    Guid? CustomerId,
    Guid? DealId,
    string? Notes);

public record MoveStageRequest(Guid StageId);
public record ConvertLeadResult(Guid LeadId, Guid CustomerId, Guid? DealId, bool CustomerCreated);

public record DealRequest(
    string Title,
    Guid? CustomerId,
    Guid? LeadId,
    Guid? StageId,
    string? AssignedUserId,
    decimal Value,
    int Probability,
    DateTime? ExpectedCloseDate,
    string? Notes);

public record DealDto(
    Guid Id,
    string Title,
    Guid? CustomerId,
    Guid? LeadId,
    Guid? StageId,
    string? StageName,
    string? AssignedUserId,
    decimal Value,
    int Probability,
    DateTime? ExpectedCloseDate,
    string Status,
    Guid? ProjectId,
    string? Notes);

public record FollowUpRequest(string Title, DateTime DueAt, string Priority, string? AssignedUserId, Guid? LeadId, Guid? CustomerId, Guid? DealId, string? Notes);
public record FollowUpDto(Guid Id, string Title, DateTime DueAt, string Priority, string Status, string? AssignedUserId, Guid? LeadId, Guid? CustomerId, Guid? DealId, bool IsOverdue);

public record SegmentRequest(string Name, string? CustomerType, string? Industry, string? Country, string? Tag, decimal? MinSpend, string? Notes);
public record SegmentDto(Guid Id, string Name, string? CustomerType, string? Industry, string? Country, string? Tag, decimal? MinSpend, int MatchCount);

public record InteractionRequest(Guid? CustomerId, Guid? LeadId, Guid? DealId, string Channel, string Summary, DateTime? OccurredAt);
public record InteractionDto(Guid Id, Guid? CustomerId, Guid? LeadId, string Channel, string Summary, DateTime OccurredAt);

public record CrmAnalyticsDto(
    int TotalLeads,
    int QualifiedLeads,
    int ConvertedLeads,
    int LostLeads,
    int ActiveDeals,
    int WonDeals,
    decimal PipelineValue,
    decimal ConversionRate);

public record WarehouseRequest(string Name, string? Code, string? Address, bool IsDefault);
public record WarehouseDto(Guid Id, string Name, string? Code, string? Address, bool IsDefault, bool IsActive);
public record WarehouseStockDto(Guid ProductId, string ProductName, decimal OnHand, decimal Reserved, decimal Available);
public record StockAdjustRequest(Guid WarehouseId, Guid ProductId, decimal QuantityDelta, string? Notes);
public record TransferLineRequest(Guid ProductId, decimal Quantity);
public record TransferRequest(Guid FromWarehouseId, Guid ToWarehouseId, string? Notes, IReadOnlyList<TransferLineRequest> Lines);
public record TransferDto(Guid Id, string TransferNumber, string Status, Guid FromWarehouseId, Guid ToWarehouseId);

public record CatalogItemRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    string? Description,
    decimal CostPrice,
    decimal SalePrice,
    string Kind,
    string? Barcode,
    string? UnitOfMeasure,
    decimal? ReorderLevel,
    string? PricingModel,
    string? Duration,
    string? DeliveryDetails,
    Guid? ParentCategoryId);

public record FulfillmentRequest(Guid OrderId, string? Carrier, string? TrackingNumber, IReadOnlyList<TransferLineRequest>? Lines);
public record FulfillmentDto(Guid Id, Guid OrderId, string Status, string? Carrier, string? TrackingNumber, DateTime? ShippedAt, DateTime? DeliveredAt);

public record ReturnLineRequest(Guid ProductId, decimal Quantity);
public record ReturnRequest(Guid OrderId, string Reason, IReadOnlyList<ReturnLineRequest> Lines);
public record ReturnDecisionRequest(bool Approve, bool Restock, decimal RefundAmount, Guid? PaymentId, string? Notes);
public record ReturnDto(Guid Id, string ReturnNumber, Guid OrderId, string Status, bool Restock, decimal RefundAmount);

public record IncomeRequest(string Category, decimal Amount, DateTime OccurredOn, string PaymentMethod, string? Reference, string? Notes, bool IsRecurring);
public record IncomeDto(Guid Id, string Category, decimal Amount, DateTime OccurredOn, string PaymentMethod, string Status, string? Reference);

public record PurchaseInvoiceRequest(string InvoiceNumber, Guid SupplierId, Guid? PurchaseId, DateTime IssueDate, DateTime DueDate, decimal SubTotal, decimal Tax, string? Notes, string? AttachmentUrl);
public record PurchaseInvoiceDto(Guid Id, string InvoiceNumber, Guid SupplierId, decimal Total, decimal AmountPaid, string Status, DateTime DueDate);
public record SupplierPaymentRequest(decimal Amount, string PaymentMethod, string? Reference);

public record TaxRateRequest(string Name, decimal RatePercent, bool IsInclusive, bool IsDefault);
public record TaxRateDto(Guid Id, string Name, decimal RatePercent, bool IsInclusive, bool IsDefault, bool IsActive);

public record SubscriptionExpenseRequest(string Provider, string Name, string BillingCycle, decimal Cost, string Currency, DateTime RenewalDate, string? ExpenseCategory);
public record SubscriptionExpenseDto(Guid Id, string Provider, string Name, string BillingCycle, decimal Cost, string Currency, DateTime RenewalDate, string PaymentStatus);

public record CashFlowDto(decimal OpeningBalance, decimal Inflows, decimal Outflows, decimal ClosingBalance, decimal OutstandingReceivables, decimal OutstandingPayables);
public record ProfitAndLossDto(decimal RecognizedRevenue, decimal InvoicedAmount, decimal CashReceived, decimal CostOfGoodsSold, decimal GrossProfit, decimal OperatingExpenses, decimal NetProfit);
public record InventoryAlertDto(Guid ProductId, string ProductName, decimal OnHand, decimal ReorderLevel, string Alert);

public record ProjectRequest(
    string Name,
    string? Description,
    string? Code,
    Guid? CustomerId,
    string? ManagerUserId,
    DateTime? StartDate,
    DateTime? Deadline,
    decimal Budget,
    int Priority,
    string? Workflow,
    string? Visibility,
    Guid? DealId);

public record ProjectDto(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    Guid? CustomerId,
    string Status,
    string? ManagerUserId,
    DateTime? StartDate,
    DateTime? Deadline,
    decimal? Budget,
    int Priority,
    string Workflow,
    string Visibility,
    Guid? DealId);

public record ProjectMemberRequest(string UserId, string RoleName);
public record TaskRequest(Guid ProjectId, string Title, string? Description, string? AssignedUserId, int Priority, DateTime? DueDate, string? Stage, string TaskKind, Guid? ParentTaskId, Guid? SprintId, bool IsClientVisible);
public record TaskDto(Guid Id, Guid ProjectId, string Title, string Status, string? Stage, string TaskKind, int Priority, DateTime? DueDate, string? AssignedUserId, Guid? ParentTaskId, bool IsClientVisible);
public record TaskCommentRequest(string Body, bool IsClientVisible);
public record MilestoneRequest(Guid ProjectId, string Name, DateTime? DueDate, bool IsClientVisible);
public record MilestoneDto(Guid Id, Guid ProjectId, string Name, string Status, DateTime? DueDate, bool IsClientVisible);
public record SprintRequest(Guid ProjectId, string Name, DateTime? StartDate, DateTime? EndDate);
public record SprintDto(Guid Id, Guid ProjectId, string Name, string Status, DateTime? StartDate, DateTime? EndDate);
public record TimeEntryRequest(Guid? ProjectId, Guid? WorkTaskId, decimal Hours, bool IsBillable, decimal? HourlyRate, string? Notes, DateTime? StartedAt);
public record TimeEntryDto(Guid Id, Guid? ProjectId, Guid? WorkTaskId, string UserId, decimal Hours, bool IsBillable, decimal? HourlyRate, string Status, DateTime? StartedAt, DateTime? EndedAt);
public record ProjectProgressDto(int TotalTasks, int CompletedTasks, int PendingTasks, int OverdueTasks, int MilestonesOpen, int MilestonesDone, decimal HoursLogged, decimal? Budget, decimal BillableAmount, decimal RemainingBudget, decimal ProgressPercent);
public record ProjectFileDto(Guid Id, string FileName, string ContentType, long SizeBytes, bool IsClientVisible, int Version, DateTime CreatedAt);
public record ClientAccessDto(Guid Id, string Email, DateTime ExpiresAt, string Token);
public record PortalProjectDto(Guid Id, string Name, string Status, DateTime? Deadline, IReadOnlyList<MilestoneDto> Milestones, IReadOnlyList<TaskDto> Tasks, IReadOnlyList<ProjectFileDto> Files);
public record PortalFeedbackRequest(Guid ProjectId, string Message);

public record QuotationPaymentRequest(Guid QuotationId, DateTime DueDate, bool RecordPayment, decimal? PaymentAmount, string? PaymentMethod, string? Reference);
public record WorkflowIdsDto(Guid? LeadId, Guid? CustomerId, Guid? DealId, Guid? QuotationId, Guid? OrderId, Guid? InvoiceId, Guid? PaymentId, Guid? ProjectId, Guid? PurchaseInvoiceId, Guid? ReturnId);
public record ProjectBillingRequest(Guid ProjectId, DateTime DueDate, Guid? ServiceProductId);

public record BusinessSnapshotDto(
    int Employees,
    int OpenLeads,
    int OpenDeals,
    decimal PipelineValue,
    int OpenOrders,
    decimal OutstandingReceivables,
    int ActiveProjects,
    int OpenTasks);
