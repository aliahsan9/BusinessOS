using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseAnalytics;

public record GetExpenseAnalyticsQuery(
    DateTime? DateFrom,
    DateTime? DateTo
) : IRequest<ExpenseAnalyticsResponse>;
