using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class BusinessOperationsEndpoints
{
    public static void MapBusinessOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        MapWorkforce(app);
        MapCrm(app);
        MapCommerce(app);
        MapProjects(app);
        MapWorkflows(app);
        MapAgent(app);
    }

    private static void MapWorkforce(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workforce").WithTags("Workforce").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/departments", async (IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetDepartmentsAsync(ct))).RequirePermission(PermissionCodes.EmployeeView);
        group.MapPost("/departments", async (DepartmentRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveDepartmentAsync(null, request, ct))).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPut("/departments/{id:guid}", async (Guid id, DepartmentRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveDepartmentAsync(id, request, ct))).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapGet("/teams", async (IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetTeamsAsync(ct))).RequirePermission(PermissionCodes.EmployeeView);
        group.MapPost("/teams", async (TeamRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveTeamAsync(null, request, ct))).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPut("/teams/{id:guid}", async (Guid id, TeamRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveTeamAsync(id, request, ct))).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPost("/teams/{id:guid}/members/{employeeId:guid}", async (Guid id, Guid employeeId, IWorkforceService service, CancellationToken ct) => { await service.AssignTeamMemberAsync(id, employeeId, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapGet("/employees", async (string? search, string? status, Guid? departmentId, int? page, int? pageSize, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetEmployeesAsync(search, status, departmentId, page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.EmployeeView);
        group.MapGet("/employees/{id:guid}", async (Guid id, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetEmployeeAsync(id, ct)));
        group.MapPost("/employees", async (EmployeeRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveEmployeeAsync(null, request, ct))).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPut("/employees/{id:guid}", async (Guid id, EmployeeRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveEmployeeAsync(id, request, ct))).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPost("/employees/{id:guid}/activate", async (Guid id, IWorkforceService service, CancellationToken ct) => { await service.SetEmployeeActiveAsync(id, true, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPost("/employees/{id:guid}/deactivate", async (Guid id, IWorkforceService service, CancellationToken ct) => { await service.SetEmployeeActiveAsync(id, false, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.EmployeeManage);
        group.MapPost("/attendance/check-in", async (Guid? employeeId, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.CheckInAsync(employeeId, ct)));
        group.MapPost("/attendance/check-out", async (Guid? employeeId, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.CheckOutAsync(employeeId, ct)));
        group.MapGet("/attendance", async (Guid? employeeId, DateOnly? from, DateOnly? to, int? page, int? pageSize, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetAttendanceAsync(employeeId, from, to, page ?? 1, pageSize ?? 31, ct)));
        group.MapPost("/attendance/{id:guid}/correct", async (Guid id, CorrectAttendanceRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.CorrectAttendanceAsync(id, request, ct))).RequirePermission(PermissionCodes.AttendanceManage);
        group.MapGet("/leave-types", async (IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetLeaveTypesAsync(ct)));
        group.MapPost("/leave-types", async (LeaveTypeRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveLeaveTypeAsync(request, ct))).RequirePermission(PermissionCodes.LeaveManage);
        group.MapGet("/leave-requests", async (string? status, int? page, int? pageSize, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetLeaveAsync(status, page ?? 1, pageSize ?? 20, ct)));
        group.MapPost("/leave-requests", async (LeaveRequestCreate request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.ApplyLeaveAsync(request, ct)));
        group.MapPost("/leave-requests/{id:guid}/approve", async (Guid id, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.DecideLeaveAsync(id, true, null, ct))).RequirePermission(PermissionCodes.LeaveManage);
        group.MapPost("/leave-requests/{id:guid}/reject", async (Guid id, LeaveDecisionRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.DecideLeaveAsync(id, false, request.Reason, ct))).RequirePermission(PermissionCodes.LeaveManage);
        group.MapGet("/announcements", async (IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetAnnouncementsAsync(ct)));
        group.MapPost("/announcements", async (AnnouncementRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveAnnouncementAsync(request, ct))).RequirePermission(PermissionCodes.AnnouncementManage);
        group.MapPost("/announcements/{id:guid}/read", async (Guid id, IWorkforceService service, CancellationToken ct) => { await service.MarkAnnouncementReadAsync(id, ct); return Results.NoContent(); });
        group.MapGet("/goals", async (Guid? employeeId, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetGoalsAsync(employeeId, ct)));
        group.MapPost("/goals", async (GoalRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveGoalAsync(null, request, ct))).RequirePermission(PermissionCodes.PerformanceManage);
        group.MapPut("/goals/{id:guid}", async (Guid id, GoalRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveGoalAsync(id, request, ct))).RequirePermission(PermissionCodes.PerformanceManage);
        group.MapGet("/reviews", async (Guid? employeeId, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetReviewsAsync(employeeId, ct)));
        group.MapPost("/reviews", async (ReviewRequest request, IWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveReviewAsync(request, ct))).RequirePermission(PermissionCodes.PerformanceManage);
    }

    private static void MapCrm(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/crm").WithTags("CRM").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/stages", async (ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetStagesAsync(ct))).RequirePermission(PermissionCodes.LeadView);
        group.MapPost("/stages", async (StageRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveStageAsync(null, request, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapPut("/stages/{id:guid}", async (Guid id, StageRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveStageAsync(id, request, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapGet("/leads", async (string? search, Guid? stageId, int? page, int? pageSize, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetLeadsAsync(search, stageId, page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.LeadView);
        group.MapPost("/leads", async (LeadRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveLeadAsync(null, request, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapPut("/leads/{id:guid}", async (Guid id, LeadRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveLeadAsync(id, request, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapPost("/leads/{id:guid}/stage", async (Guid id, MoveStageRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.MoveLeadAsync(id, request.StageId, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapPost("/leads/{id:guid}/convert", async (Guid id, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.ConvertLeadAsync(id, true, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapGet("/deals", async (string? status, int? page, int? pageSize, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetDealsAsync(status, page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.DealView);
        group.MapPost("/deals", async (DealRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveDealAsync(null, request, ct))).RequirePermission(PermissionCodes.DealManage);
        group.MapPut("/deals/{id:guid}", async (Guid id, DealRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveDealAsync(id, request, ct))).RequirePermission(PermissionCodes.DealManage);
        group.MapPost("/deals/{id:guid}/win", async (Guid id, bool? createProject, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.WinDealAsync(id, createProject ?? true, ct))).RequirePermission(PermissionCodes.DealManage);
        group.MapGet("/follow-ups", async (bool? overdue, int? page, int? pageSize, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetFollowUpsAsync(overdue ?? false, page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.LeadView);
        group.MapPost("/follow-ups", async (FollowUpRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveFollowUpAsync(request, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapPost("/follow-ups/{id:guid}/complete", async (Guid id, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.CompleteFollowUpAsync(id, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapGet("/segments", async (ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetSegmentsAsync(ct))).RequirePermission(PermissionCodes.CustomerView);
        group.MapPost("/segments", async (SegmentRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveSegmentAsync(request, ct))).RequirePermission(PermissionCodes.CustomerUpdate);
        group.MapGet("/segments/{id:guid}/customers", async (Guid id, int? page, int? pageSize, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetSegmentCustomersAsync(id, page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.CustomerView);
        group.MapGet("/interactions", async (Guid? customerId, Guid? leadId, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetInteractionsAsync(customerId, leadId, ct))).RequirePermission(PermissionCodes.CustomerView);
        group.MapPost("/interactions", async (InteractionRequest request, ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.AddInteractionAsync(request, ct))).RequirePermission(PermissionCodes.CustomerUpdate);
        group.MapGet("/analytics", async (ICrmOperationsService service, CancellationToken ct) => Results.Ok(await service.GetAnalyticsAsync(ct))).RequirePermission(PermissionCodes.LeadView);
    }

    private static void MapCommerce(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/operations").WithTags("Operations").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/warehouses", async (ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetWarehousesAsync(ct))).RequirePermission(PermissionCodes.WarehouseView);
        group.MapPost("/warehouses", async (WarehouseRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveWarehouseAsync(null, request, ct))).RequirePermission(PermissionCodes.WarehouseManage);
        group.MapGet("/warehouses/{id:guid}/stock", async (Guid id, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetWarehouseStockAsync(id, ct))).RequirePermission(PermissionCodes.WarehouseView);
        group.MapPost("/transfers", async (TransferRequest request, ICommerceOperationsService service, CancellationToken ct) => { await service.TransferAsync(request, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.WarehouseManage);
        group.MapGet("/alerts", async (ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetAlertsAsync(ct))).RequirePermission(PermissionCodes.InventoryView);
        group.MapPost("/catalog", async (CatalogItemRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveCatalogItemAsync(request, ct))).RequirePermission(PermissionCodes.ProductCreate);
        group.MapPost("/fulfillments", async (FulfillmentRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveFulfillmentAsync(request, ct))).RequirePermission(PermissionCodes.FulfillmentManage);
        group.MapPost("/fulfillments/{id:guid}/advance", async (Guid id, string status, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.AdvanceFulfillmentAsync(id, status, ct))).RequirePermission(PermissionCodes.FulfillmentManage);
        group.MapGet("/returns", async (ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetReturnsAsync(ct))).RequirePermission(PermissionCodes.OrderView);
        group.MapPost("/returns", async (ReturnRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.CreateReturnAsync(request, ct))).RequirePermission(PermissionCodes.ReturnManage);
        group.MapPost("/returns/{id:guid}/decide", async (Guid id, ReturnDecisionRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.DecideReturnAsync(id, request, ct))).RequirePermission(PermissionCodes.ReturnManage);
        group.MapGet("/income", async (int? page, int? pageSize, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetIncomeAsync(page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapPost("/income", async (IncomeRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveIncomeAsync(request, ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapGet("/purchase-invoices", async (ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetPurchaseInvoicesAsync(ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapPost("/purchase-invoices", async (PurchaseInvoiceRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SavePurchaseInvoiceAsync(request, ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapPost("/purchase-invoices/{id:guid}/pay", async (Guid id, SupplierPaymentRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.PayPurchaseInvoiceAsync(id, request, ct))).RequirePermission(PermissionCodes.ExpenseCreate);
        group.MapGet("/tax-rates", async (ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetTaxRatesAsync(ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapPost("/tax-rates", async (TaxRateRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveTaxRateAsync(request, ct))).RequirePermission(PermissionCodes.SettingsUpdate);
        group.MapGet("/subscriptions", async (ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetSubscriptionsAsync(ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapPost("/subscriptions", async (SubscriptionExpenseRequest request, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveSubscriptionAsync(request, ct))).RequirePermission(PermissionCodes.ExpenseCreate);
        group.MapGet("/reports/cash-flow", async (DateTime from, DateTime to, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetCashFlowAsync(from, to, ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapGet("/reports/profit-and-loss", async (DateTime from, DateTime to, ICommerceOperationsService service, CancellationToken ct) => Results.Ok(await service.GetProfitAndLossAsync(from, to, ct))).RequirePermission(PermissionCodes.FinanceView);
    }

    private static void MapProjects(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/project-ops").WithTags("Projects").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/projects", async (int? page, int? pageSize, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.GetProjectsAsync(page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.ProjectView);
        group.MapPost("/projects", async (ProjectRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveProjectAsync(null, request, ct))).RequirePermission(PermissionCodes.ProjectCreate);
        group.MapPut("/projects/{id:guid}", async (Guid id, ProjectRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveProjectAsync(id, request, ct))).RequirePermission(PermissionCodes.ProjectUpdate);
        group.MapPost("/projects/{id:guid}/members", async (Guid id, ProjectMemberRequest request, IProjectOperationsService service, CancellationToken ct) => { await service.AddProjectMemberAsync(id, request, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.ProjectUpdate);
        group.MapGet("/projects/{id:guid}/progress", async (Guid id, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.GetProgressAsync(id, ct))).RequirePermission(PermissionCodes.ProjectView);
        group.MapPost("/projects/{id:guid}/files", async (Guid id, IFormFile file, bool? clientVisible, IProjectOperationsService service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return Results.Ok(await service.SaveFileAsync(id, file.FileName, file.ContentType, stream, clientVisible ?? false, ct));
        }).RequirePermission(PermissionCodes.ProjectUpdate).DisableAntiforgery();
        group.MapGet("/files/{id:guid}", async (Guid id, IProjectOperationsService service, CancellationToken ct) =>
        {
            var (meta, stream) = await service.OpenFileAsync(id, ct);
            return Results.File(stream, meta.ContentType, meta.FileName);
        }).RequirePermission(PermissionCodes.ProjectView);
        group.MapGet("/tasks", async (Guid? projectId, string? stage, int? page, int? pageSize, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.GetTasksAsync(projectId, stage, page ?? 1, pageSize ?? 50, ct))).RequirePermission(PermissionCodes.TaskView);
        group.MapPost("/tasks", async (TaskRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveTaskAsync(null, request, ct))).RequirePermission(PermissionCodes.TaskCreate);
        group.MapPut("/tasks/{id:guid}", async (Guid id, TaskRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveTaskAsync(id, request, ct))).RequirePermission(PermissionCodes.TaskUpdate);
        group.MapPost("/tasks/{id:guid}/comments", async (Guid id, TaskCommentRequest request, IProjectOperationsService service, CancellationToken ct) => { await service.AddCommentAsync(id, request, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.TaskUpdate);
        group.MapPost("/milestones", async (MilestoneRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveMilestoneAsync(request, ct))).RequirePermission(PermissionCodes.ProjectUpdate);
        group.MapGet("/projects/{id:guid}/milestones", async (Guid id, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.GetMilestonesAsync(id, ct))).RequirePermission(PermissionCodes.ProjectView);
        group.MapPost("/milestones/{id:guid}/status", async (Guid id, string status, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.UpdateMilestoneStatusAsync(id, status, ct))).RequirePermission(PermissionCodes.ProjectUpdate);
        group.MapPost("/sprints", async (SprintRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveSprintAsync(request, ct))).RequirePermission(PermissionCodes.ProjectUpdate);
        group.MapGet("/time", async (Guid? projectId, int? page, int? pageSize, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.GetTimeAsync(projectId, page ?? 1, pageSize ?? 20, ct))).RequirePermission(PermissionCodes.TimeView);
        group.MapPost("/time", async (TimeEntryRequest request, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.SaveTimeAsync(request, ct))).RequirePermission(PermissionCodes.TimeView);
        group.MapPost("/time/start", async (Guid? projectId, Guid? taskId, bool? billable, decimal? hourlyRate, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.StartTimerAsync(projectId, taskId, billable ?? true, hourlyRate, ct))).RequirePermission(PermissionCodes.TimeView);
        group.MapPost("/time/stop", async (IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.StopTimerAsync(ct))).RequirePermission(PermissionCodes.TimeView);
        group.MapPost("/time/{id:guid}/approve", async (Guid id, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.ApproveTimeAsync(id, ct))).RequirePermission(PermissionCodes.TimeManage);
        group.MapPost("/projects/{id:guid}/client-access", async (Guid id, string email, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.CreateClientAccessAsync(id, email, ct))).RequirePermission(PermissionCodes.PortalManage);

        var portal = app.MapGroup("/portal").WithTags("Client Portal").AllowAnonymous().RequireRateLimiting("api");
        portal.MapGet("/{token}", async (string token, IProjectOperationsService service, CancellationToken ct) => Results.Ok(await service.GetPortalAsync(token, ct)));
        portal.MapPost("/{token}/feedback", async (string token, PortalFeedbackRequest request, IProjectOperationsService service, CancellationToken ct) => { await service.AddPortalFeedbackAsync(token, request, ct); return Results.NoContent(); });
        portal.MapGet("/{token}/files/{id:guid}", async (string token, Guid id, IProjectOperationsService service, CancellationToken ct) =>
        {
            var (meta, stream) = await service.OpenPortalFileAsync(token, id, ct);
            return Results.File(stream, meta.ContentType, meta.FileName);
        });
    }

    private static void MapWorkflows(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workflows").WithTags("Workflows").RequireAuthorization().RequireRateLimiting("api");
        group.MapPost("/leads/{id:guid}/convert", async (Guid id, IBusinessWorkflowService service, CancellationToken ct) => Results.Ok(await service.LeadToCustomerAsync(id, ct))).RequirePermission(PermissionCodes.LeadManage);
        group.MapPost("/quotation-to-payment", async (QuotationPaymentRequest request, IBusinessWorkflowService service, CancellationToken ct) => Results.Ok(await service.QuotationToPaymentAsync(request, ct))).RequirePermission(PermissionCodes.QuotationUpdate);
        group.MapPost("/purchases/{id:guid}/invoice", async (Guid id, IBusinessWorkflowService service, CancellationToken ct) => Results.Ok(await service.PurchaseToInvoiceAsync(id, ct))).RequirePermission(PermissionCodes.PurchaseOrderView);
        group.MapPost("/returns/{id:guid}/settle", async (Guid id, ReturnDecisionRequest request, IBusinessWorkflowService service, CancellationToken ct) => Results.Ok(await service.SettleReturnAsync(id, request, ct))).RequirePermission(PermissionCodes.ReturnManage);
        group.MapPost("/deals/{id:guid}/project", async (Guid id, IBusinessWorkflowService service, CancellationToken ct) => Results.Ok(await service.DealToProjectAsync(id, ct))).RequirePermission(PermissionCodes.DealManage);
        group.MapPost("/projects/bill", async (ProjectBillingRequest request, IBusinessWorkflowService service, CancellationToken ct) => Results.Ok(await service.BillProjectAsync(request, ct))).RequirePermission(PermissionCodes.InvoiceCreate);
    }

    private static void MapAgent(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/agent/business").WithTags("Business Agent").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/snapshot", async (IBusinessAgentGateway service, CancellationToken ct) => Results.Ok(await service.GetSnapshotAsync(ct))).RequirePermission(PermissionCodes.AnalyticsView);
        group.MapGet("/employees", async (IBusinessAgentGateway service, CancellationToken ct) => Results.Ok(await service.GetEmployeesAsync(ct))).RequirePermission(PermissionCodes.EmployeeView);
        group.MapGet("/leads", async (IBusinessAgentGateway service, CancellationToken ct) => Results.Ok(await service.GetLeadsAsync(ct))).RequirePermission(PermissionCodes.LeadView);
        group.MapGet("/finance", async (DateTime from, DateTime to, IBusinessAgentGateway service, CancellationToken ct) => Results.Ok(await service.GetFinanceAsync(from, to, ct))).RequirePermission(PermissionCodes.FinanceView);
        group.MapGet("/projects", async (IBusinessAgentGateway service, CancellationToken ct) => Results.Ok(await service.GetProjectsAsync(ct))).RequirePermission(PermissionCodes.ProjectView);
    }
}
