using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseCategoryById;

public record GetExpenseCategoryByIdQuery(Guid Id) : IRequest<ExpenseCategoryResponse>;
