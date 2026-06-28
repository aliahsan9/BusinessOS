using MediatR;

namespace BusinessOS.Application.Features.Expenses.Commands.UpdateExpenseCategory;

public record UpdateExpenseCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive
) : IRequest<Unit>;
