using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Expenses.Queries.GetAllExpenses;

public record GetAllExpensesQuery(
    Guid? CategoryId,
    string? Status,
    string? Search,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<ExpenseSummaryResponse>>;
