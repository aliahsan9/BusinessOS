using MediatR;

namespace BusinessOS.Application.Features.Expenses.Commands.CreateExpenseCategory;

public record CreateExpenseCategoryCommand(string Name, string? Description) : IRequest<Guid>;
