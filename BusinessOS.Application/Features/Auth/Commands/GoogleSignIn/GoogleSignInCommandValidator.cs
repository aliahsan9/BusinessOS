using FluentValidation;

namespace BusinessOS.Application.Features.Auth.Commands.GoogleSignIn;

public sealed class GoogleSignInCommandValidator : AbstractValidator<GoogleSignInCommand>
{
    public GoogleSignInCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google sign-in could not be completed.")
            .MaximumLength(8192);
    }
}
