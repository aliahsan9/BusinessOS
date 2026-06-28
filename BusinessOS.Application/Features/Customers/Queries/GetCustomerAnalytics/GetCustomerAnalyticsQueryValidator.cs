using FluentValidation;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerAnalytics;

public sealed class GetCustomerAnalyticsQueryValidator : AbstractValidator<GetCustomerAnalyticsQuery>
{
    public GetCustomerAnalyticsQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer id is required.");
    }
}
