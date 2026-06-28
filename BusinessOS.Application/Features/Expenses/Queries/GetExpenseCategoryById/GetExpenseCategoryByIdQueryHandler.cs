using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseCategoryById;

public sealed class GetExpenseCategoryByIdQueryHandler
    : IRequestHandler<GetExpenseCategoryByIdQuery, ExpenseCategoryResponse>
{
    private readonly IApplicationDbContext _context;

    public GetExpenseCategoryByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExpenseCategoryResponse> Handle(
        GetExpenseCategoryByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.ExpenseCategories
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(ExpenseProjections.ToCategoryResponse)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");
    }
}
