using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Validators;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Inventory.Queries.GetStockTransactions;

public sealed record GetStockTransactionsQuery(
    Guid? ProductId,
    string? TransactionType,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<StockTransactionResponse>>;

public sealed class GetStockTransactionsQueryHandler
    : IRequestHandler<GetStockTransactionsQuery, PagedResult<StockTransactionResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<StockTransactionResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["createdat"] = x => x.CreatedAt,
            ["quantity"] = x => x.Quantity,
            ["transactiontype"] = x => x.TransactionType
        };

    private readonly IStockTransactionRepository _transactionRepository;

    public GetStockTransactionsQueryHandler(IStockTransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<PagedResult<StockTransactionResponse>> Handle(
        GetStockTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _transactionRepository.Query().Select(InventoryProjections.ToTransactionResponse);

        if (request.ProductId.HasValue)
            query = query.Where(x => x.ProductId == request.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(request.TransactionType))
            query = query.Where(x => x.TransactionType == request.TransactionType);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)) ||
                (x.Notes != null && x.Notes.Contains(search)) ||
                x.ProductName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockTransactionResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

public sealed class GetStockTransactionsQueryValidator : AbstractValidator<GetStockTransactionsQuery>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdat", "quantity", "transactiontype"
    };

    public GetStockTransactionsQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.TransactionType)
            .Must(x => x is null || StockTransactionTypeNames.IsValid(x))
            .WithMessage("Invalid transaction type.");

        RuleFor(x => x.SortBy)
            .Must(x => x is null || AllowedSortFields.Contains(x))
            .WithMessage("Invalid sort field.");
    }
}
