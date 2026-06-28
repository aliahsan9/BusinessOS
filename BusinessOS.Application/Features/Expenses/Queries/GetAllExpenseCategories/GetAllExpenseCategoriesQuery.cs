using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Expenses.Queries.GetAllExpenseCategories;

public record GetAllExpenseCategoriesQuery(bool? ActiveOnly) : IRequest<IReadOnlyList<ExpenseCategoryResponse>>;
