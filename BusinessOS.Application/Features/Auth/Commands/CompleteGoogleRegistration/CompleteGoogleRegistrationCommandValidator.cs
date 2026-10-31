using FluentValidation;

namespace BusinessOS.Application.Features.Auth.Commands.CompleteGoogleRegistration;

public sealed class CompleteGoogleRegistrationCommandValidator : AbstractValidator<CompleteGoogleRegistrationCommand>
{
    public CompleteGoogleRegistrationCommandValidator()
    {
        RuleFor(x => x.ContinuationToken)
            .NotEmpty().WithMessage("Google sign-up could not be completed. Start again with Google.")
            .MaximumLength(8192);

        RuleFor(x => x.BusinessName)
            .NotEmpty().WithMessage("Business name is required")
            .MaximumLength(200);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);
    }
}
