using BusinessOS.Application.Common.Models;
using FluentValidation;

namespace BusinessOS.Application.Common.Validators;

public static class PaginationValidator
{
    public static IRuleBuilderOptions<T, int> ValidPage<T>(IRuleBuilder<T, int> ruleBuilder) =>
        ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page must be zero or greater.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(IRuleBuilder<T, int> ruleBuilder) =>
        ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage("PageSize must be zero or greater.")
            .LessThanOrEqualTo(PaginationParams.MaxPageSize)
            .WithMessage($"PageSize cannot exceed {PaginationParams.MaxPageSize}.");
}
