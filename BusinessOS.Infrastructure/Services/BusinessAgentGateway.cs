using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class BusinessAgentGateway : IBusinessAgentGateway
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IWorkforceService _workforce;
    private readonly ICrmOperationsService _crm;
    private readonly ICommerceOperationsService _commerce;
    private readonly IProjectOperationsService _projects;

    public BusinessAgentGateway(
        IApplicationDbContext db,
        ICurrentUserService user,
        IWorkforceService workforce,
        ICrmOperationsService crm,
        ICommerceOperationsService commerce,
        IProjectOperationsService projects)
    {
        _db = db;
        _user = user;
        _workforce = workforce;
        _crm = crm;
        _commerce = commerce;
        _projects = projects;
    }

    public async Task<BusinessSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.AnalyticsView, PermissionCodes.ReportView, PermissionCodes.FinanceView);
        var employees = _user.HasPermission(PermissionCodes.EmployeeView)
            ? await _db.Employees.CountAsync(x => x.IsActive, cancellationToken) : 0;
        var leads = _user.HasPermission(PermissionCodes.LeadView)
            ? await _db.Leads.CountAsync(x => x.Status != "Converted" && x.Status != "Lost", cancellationToken) : 0;
        var deals = _user.HasPermission(PermissionCodes.DealView)
            ? await _db.Deals.Where(x => x.Status == "Open").Select(x => x.Value).ToListAsync(cancellationToken)
            : [];
        var orders = _user.HasPermission(PermissionCodes.OrderView)
            ? await _db.Orders.CountAsync(x => x.Status != "Completed" && x.Status != "Cancelled", cancellationToken) : 0;
        var receivables = _user.HasPermission(PermissionCodes.FinanceView)
            ? await _db.Invoices.Where(x => x.Status != "Cancelled" && x.Status != "Draft").SumAsync(x => (decimal?)x.OutstandingAmount, cancellationToken) ?? 0
            : 0;
        var projects = _user.HasPermission(PermissionCodes.ProjectView)
            ? await _db.Projects.CountAsync(x => x.Status == Domain.Enums.ProjectStatus.Active || x.Status == Domain.Enums.ProjectStatus.Planning, cancellationToken) : 0;
        var tasks = _user.HasPermission(PermissionCodes.TaskView)
            ? await _db.WorkTasks.CountAsync(x => x.Status != Domain.Enums.WorkTaskStatus.Done && x.Status != Domain.Enums.WorkTaskStatus.Cancelled, cancellationToken) : 0;
        return new BusinessSnapshotDto(employees, leads, deals.Count, deals.Sum(), orders, receivables, projects, tasks);
    }

    public Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(CancellationToken cancellationToken = default) =>
        Employees(cancellationToken);

    private async Task<IReadOnlyList<EmployeeDto>> Employees(CancellationToken cancellationToken)
    {
        var page = await _workforce.GetEmployeesAsync(null, null, null, 1, 50, cancellationToken);
        return page.Items;
    }

    public async Task<IReadOnlyList<LeadDto>> GetLeadsAsync(CancellationToken cancellationToken = default)
    {
        var page = await _crm.GetLeadsAsync(null, null, 1, 50, cancellationToken);
        return page.Items;
    }

    public Task<ProfitAndLossDto> GetFinanceAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
        _commerce.GetProfitAndLossAsync(from, to, cancellationToken);

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var page = await _projects.GetProjectsAsync(1, 50, cancellationToken);
        return page.Items;
    }
}
