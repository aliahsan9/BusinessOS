using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Expenses.Queries.GetAllExpenses;

public sealed class GetAllExpensesQueryHandler
    : IRequestHandler<GetAllExpensesQuery, PagedResult<ExpenseSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<ExpenseSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = x => x.Title,
            ["amount"] = x => x.Amount,
            ["expensedate"] = x => x.ExpenseDate,
            ["status"] = x => x.Status,
            ["createdat"] = x => x.CreatedAt
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllExpensesQueryHandler> _logger;

    public GetAllExpensesQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllExpensesQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ExpenseSummaryResponse>> Handle(
        GetAllExpensesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Expenses.AsNoTracking();

        if (request.CategoryId.HasValue)
        {
            var categoryId = request.CategoryId.Value;
            query = query.Where(x => x.ExpenseCategoryId == categoryId);
        }

        var projectedQuery = query.Select(ExpenseProjections.ToSummary);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            projectedQuery = projectedQuery.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            projectedQuery = projectedQuery.Where(x =>
                x.Title.Contains(search) ||
                (x.Vendor != null && x.Vendor.Contains(search)));
        }

        if (request.DateFrom.HasValue)
        {
            var dateFrom = request.DateFrom.Value.ToUniversalTime();
            projectedQuery = projectedQuery.Where(x => x.ExpenseDate >= dateFrom);
        }

        if (request.DateTo.HasValue)
        {
            var dateTo = request.DateTo.Value.ToUniversalTime();
            projectedQuery = projectedQuery.Where(x => x.ExpenseDate <= dateTo);
        }

        var totalCount = await projectedQuery.CountAsync(cancellationToken);

        var items = await projectedQuery
            .ApplySort(request.SortBy, request.SortDirection, SortFields, x => x.ExpenseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} expenses (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<ExpenseSummaryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
