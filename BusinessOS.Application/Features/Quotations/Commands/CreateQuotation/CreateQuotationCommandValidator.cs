using BusinessOS.Application.Features.Quotations.Constants;
using BusinessOS.Application.Features.Quotations.Queries;
using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Quotations.Commands.CreateQuotation;

public sealed class CreateQuotationCommandValidator : AbstractValidator<CreateQuotationCommand>
{
    public CreateQuotationCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.QuotationDate)
            .NotEmpty().WithMessage("QuotationDate is required.");

        RuleFor(x => x.ExpiryDate)
            .NotEmpty().WithMessage("ExpiryDate is required.")
            .GreaterThanOrEqualTo(x => x.QuotationDate)
            .WithMessage("ExpiryDate must be on or after QuotationDate.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(QuotationStatusNames.IsValid)
            .WithMessage("Status must be a valid quotation status.");

        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0).WithMessage("Discount cannot be negative.");

        RuleFor(x => x.Tax)
            .GreaterThanOrEqualTo(0).WithMessage("Tax cannot be negative.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one line item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            item.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
                .LessThanOrEqualTo(QuotationConstants.MaxItemQuantity)
                .WithMessage($"Quantity cannot exceed {QuotationConstants.MaxItemQuantity}.");

            item.RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
        });
    }
}
