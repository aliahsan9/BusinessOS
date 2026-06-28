using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Commands.CreateExpenseCategory;

public sealed class CreateExpenseCategoryCommandHandler : IRequestHandler<CreateExpenseCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateExpenseCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var duplicate = await _context.ExpenseCategories
            .AsNoTracking()
            .AnyAsync(x => x.Name == name, cancellationToken);

        if (duplicate)
            throw new ConflictException($"Expense category '{name}' already exists.");

        var category = new ExpenseCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        _context.ExpenseCategories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
