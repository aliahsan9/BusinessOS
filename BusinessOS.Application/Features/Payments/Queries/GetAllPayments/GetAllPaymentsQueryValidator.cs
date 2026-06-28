using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Payments.Queries.GetAllPayments;

public sealed class GetAllPaymentsQueryValidator : AbstractValidator<GetAllPaymentsQuery>
{
    public GetAllPaymentsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.PaymentMethod)
            .Must(method => method is null || PaymentMethodNames.IsValid(method))
            .WithMessage("PaymentMethod must be a valid payment method.");

        RuleFor(x => x)
            .Must(x => x.DateFrom is null || x.DateTo is null || x.DateFrom <= x.DateTo)
            .WithMessage("DateFrom must be before or equal to DateTo.");
    }
}
