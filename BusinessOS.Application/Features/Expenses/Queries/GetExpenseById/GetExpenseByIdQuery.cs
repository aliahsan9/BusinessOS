using MediatR;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseById;

public record GetExpenseByIdQuery(Guid Id) : IRequest<ExpenseResponse>;
