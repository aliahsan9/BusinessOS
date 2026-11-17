using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Operations.DTOs;

namespace BusinessOS.Application.Features.Operations.Services;

public interface IWorkforceService
{
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<DepartmentDto> SaveDepartmentAsync(Guid? id, DepartmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default);
    Task<TeamDto> SaveTeamAsync(Guid? id, TeamRequest request, CancellationToken cancellationToken = default);
    Task AssignTeamMemberAsync(Guid teamId, Guid employeeId, CancellationToken cancellationToken = default);
    Task<PagedResult<EmployeeDto>> GetEmployeesAsync(string? search, string? status, Guid? departmentId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<EmployeeDto> GetEmployeeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeDto> SaveEmployeeAsync(Guid? id, EmployeeRequest request, CancellationToken cancellationToken = default);
    Task SetEmployeeActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);
    Task<AttendanceDto> CheckInAsync(Guid? employeeId, CancellationToken cancellationToken = default);
    Task<AttendanceDto> CheckOutAsync(Guid? employeeId, CancellationToken cancellationToken = default);
    Task<PagedResult<AttendanceDto>> GetAttendanceAsync(Guid? employeeId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AttendanceDto> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> SaveLeaveTypeAsync(LeaveTypeRequest request, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> ApplyLeaveAsync(LeaveRequestCreate request, CancellationToken cancellationToken = default);
    Task<PagedResult<LeaveRequestDto>> GetLeaveAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> DecideLeaveAsync(Guid id, bool approve, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken cancellationToken = default);
    Task<AnnouncementDto> SaveAnnouncementAsync(AnnouncementRequest request, CancellationToken cancellationToken = default);
    Task MarkAnnouncementReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoalDto>> GetGoalsAsync(Guid? employeeId, CancellationToken cancellationToken = default);
    Task<GoalDto> SaveGoalAsync(Guid? id, GoalRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewDto>> GetReviewsAsync(Guid? employeeId, CancellationToken cancellationToken = default);
    Task<ReviewDto> SaveReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default);
}

public interface ICrmOperationsService
{
    Task<IReadOnlyList<StageDto>> GetStagesAsync(CancellationToken cancellationToken = default);
    Task<StageDto> SaveStageAsync(Guid? id, StageRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<LeadDto>> GetLeadsAsync(string? search, Guid? stageId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<LeadDto> SaveLeadAsync(Guid? id, LeadRequest request, CancellationToken cancellationToken = default);
    Task<LeadDto> MoveLeadAsync(Guid id, Guid stageId, CancellationToken cancellationToken = default);
    Task<ConvertLeadResult> ConvertLeadAsync(Guid id, bool createDeal, CancellationToken cancellationToken = default);
    Task<PagedResult<DealDto>> GetDealsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<DealDto> SaveDealAsync(Guid? id, DealRequest request, CancellationToken cancellationToken = default);
    Task<DealDto> WinDealAsync(Guid id, bool createProject, CancellationToken cancellationToken = default);
    Task<PagedResult<FollowUpDto>> GetFollowUpsAsync(bool overdueOnly, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FollowUpDto> SaveFollowUpAsync(FollowUpRequest request, CancellationToken cancellationToken = default);
    Task<FollowUpDto> CompleteFollowUpAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken cancellationToken = default);
    Task<SegmentDto> SaveSegmentAsync(SegmentRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerMatchDto>> GetSegmentCustomersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<InteractionDto> AddInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InteractionDto>> GetInteractionsAsync(Guid? customerId, Guid? leadId, CancellationToken cancellationToken = default);
    Task<CrmAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);
}

public record CustomerMatchDto(Guid Id, string Name, string Email, string? Company, decimal TotalSpend);

public interface ICommerceOperationsService
{
    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<WarehouseDto> SaveWarehouseAsync(Guid? id, WarehouseRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WarehouseStockDto>> GetWarehouseStockAsync(Guid warehouseId, CancellationToken cancellationToken = default);
    Task TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default);
    Task<Guid> SaveCatalogItemAsync(CatalogItemRequest request, CancellationToken cancellationToken = default);
    Task<FulfillmentDto> SaveFulfillmentAsync(FulfillmentRequest request, CancellationToken cancellationToken = default);
    Task<FulfillmentDto> AdvanceFulfillmentAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<ReturnDto> CreateReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReturnDto>> GetReturnsAsync(CancellationToken cancellationToken = default);
    Task<ReturnDto> DecideReturnAsync(Guid id, ReturnDecisionRequest request, CancellationToken cancellationToken = default);
    Task<IncomeDto> SaveIncomeAsync(IncomeRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<IncomeDto>> GetIncomeAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> SavePurchaseInvoiceAsync(PurchaseInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceDto>> GetPurchaseInvoicesAsync(CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> PayPurchaseInvoiceAsync(Guid id, SupplierPaymentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaxRateDto>> GetTaxRatesAsync(CancellationToken cancellationToken = default);
    Task<TaxRateDto> SaveTaxRateAsync(TaxRateRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionExpenseDto> SaveSubscriptionAsync(SubscriptionExpenseRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionExpenseDto>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task<CashFlowDto> GetCashFlowAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

public interface IProjectOperationsService
{
    Task<PagedResult<ProjectDto>> GetProjectsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ProjectDto> SaveProjectAsync(Guid? id, ProjectRequest request, CancellationToken cancellationToken = default);
    Task AddProjectMemberAsync(Guid projectId, ProjectMemberRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<TaskDto>> GetTasksAsync(Guid? projectId, string? stage, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TaskDto> SaveTaskAsync(Guid? id, TaskRequest request, CancellationToken cancellationToken = default);
    Task AddCommentAsync(Guid taskId, TaskCommentRequest request, CancellationToken cancellationToken = default);
    Task<MilestoneDto> SaveMilestoneAsync(MilestoneRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MilestoneDto>> GetMilestonesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<MilestoneDto> UpdateMilestoneStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<SprintDto> SaveSprintAsync(SprintRequest request, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> StartTimerAsync(Guid? projectId, Guid? taskId, bool billable, decimal? hourlyRate, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> StopTimerAsync(CancellationToken cancellationToken = default);
    Task<TimeEntryDto> SaveTimeAsync(TimeEntryRequest request, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> ApproveTimeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TimeEntryDto>> GetTimeAsync(Guid? projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ProjectProgressDto> GetProgressAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectFileDto> SaveFileAsync(Guid projectId, string fileName, string contentType, Stream content, bool clientVisible, CancellationToken cancellationToken = default);
    Task<(ProjectFileDto Meta, Stream Content)> OpenFileAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task<ClientAccessDto> CreateClientAccessAsync(Guid projectId, string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortalProjectDto>> GetPortalAsync(string token, CancellationToken cancellationToken = default);
    Task AddPortalFeedbackAsync(string token, PortalFeedbackRequest request, CancellationToken cancellationToken = default);
    Task<(ProjectFileDto Meta, Stream Content)> OpenPortalFileAsync(string token, Guid fileId, CancellationToken cancellationToken = default);
}

public interface IBusinessWorkflowService
{
    Task<ConvertLeadResult> LeadToCustomerAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<WorkflowIdsDto> QuotationToPaymentAsync(QuotationPaymentRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> PurchaseToInvoiceAsync(Guid purchaseId, CancellationToken cancellationToken = default);
    Task<ReturnDto> SettleReturnAsync(Guid returnId, ReturnDecisionRequest request, CancellationToken cancellationToken = default);
    Task<ProjectDto> DealToProjectAsync(Guid dealId, CancellationToken cancellationToken = default);
    Task<WorkflowIdsDto> BillProjectAsync(ProjectBillingRequest request, CancellationToken cancellationToken = default);
}

public interface IBusinessAgentGateway
{
    Task<BusinessSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadDto>> GetLeadsAsync(CancellationToken cancellationToken = default);
    Task<ProfitAndLossDto> GetFinanceAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
}
