using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseById;

public sealed class GetExpenseByIdQueryHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseResponse>
{
    private readonly IApplicationDbContext _context;

    public GetExpenseByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExpenseResponse> Handle(
        GetExpenseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var expense = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(ExpenseProjections.ToDetail)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        return expense;
    }
}
