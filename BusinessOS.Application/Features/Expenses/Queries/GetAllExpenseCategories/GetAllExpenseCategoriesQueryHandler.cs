using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Queries.GetAllExpenseCategories;

public sealed class GetAllExpenseCategoriesQueryHandler
    : IRequestHandler<GetAllExpenseCategoriesQuery, IReadOnlyList<ExpenseCategoryResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAllExpenseCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExpenseCategoryResponse>> Handle(
        GetAllExpenseCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ExpenseCategories.AsNoTracking();

        if (request.ActiveOnly == true)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.Name)
            .Select(ExpenseProjections.ToCategoryResponse)
            .ToListAsync(cancellationToken);
    }
}
