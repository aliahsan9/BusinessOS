using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Invoices.Queries.GetAllInvoices;

public sealed class GetAllInvoicesQueryValidator : AbstractValidator<GetAllInvoicesQuery>
{
    public GetAllInvoicesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(status => status is null || InvoiceStatusNames.IsValid(status))
            .WithMessage("Status must be a valid invoice status.");
    }
}
