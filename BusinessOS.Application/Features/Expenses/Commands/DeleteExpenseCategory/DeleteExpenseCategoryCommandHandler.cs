using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Commands.DeleteExpenseCategory;

public sealed class DeleteExpenseCategoryCommandHandler : IRequestHandler<DeleteExpenseCategoryCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteExpenseCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.ExpenseCategories
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        var hasExpenses = await _context.Expenses
            .AsNoTracking()
            .AnyAsync(x => x.ExpenseCategoryId == request.Id, cancellationToken);

        if (hasExpenses)
            throw new ConflictException("Cannot delete category with existing expenses.");

        _context.ExpenseCategories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
