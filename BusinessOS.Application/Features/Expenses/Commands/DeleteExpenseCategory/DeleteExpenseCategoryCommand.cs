using MediatR;

namespace BusinessOS.Application.Features.Expenses.Commands.DeleteExpenseCategory;

public record DeleteExpenseCategoryCommand(Guid Id) : IRequest<Unit>;
