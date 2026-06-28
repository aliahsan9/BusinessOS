using MediatR;

namespace BusinessOS.Application.Features.Expenses.Commands.DeleteExpense;

public record DeleteExpenseCommand(Guid Id) : IRequest<Unit>;
