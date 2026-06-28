using BusinessOS.Application.Features.Finance.DTOs;

namespace BusinessOS.Application.Features.Finance.Services;

public interface IFinanceService
{
    Task<FinancialDashboardResponse> GetDashboardAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<ProfitLossResponse> GetProfitLossAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        string? groupBy,
        CancellationToken cancellationToken = default);

    Task<RevenueBreakdown> GetRevenueBreakdownAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<ExpenseBreakdown> GetExpenseBreakdownAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);
}
