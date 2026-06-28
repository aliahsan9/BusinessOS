using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Commands.UpdateExpenseCategory;

public sealed class UpdateExpenseCategoryCommandHandler : IRequestHandler<UpdateExpenseCategoryCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateExpenseCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.ExpenseCategories
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        var name = request.Name.Trim();
        var duplicate = await _context.ExpenseCategories
            .AsNoTracking()
            .AnyAsync(x => x.Name == name && x.Id != request.Id, cancellationToken);

        if (duplicate)
            throw new ConflictException($"Expense category '{name}' already exists.");

        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
