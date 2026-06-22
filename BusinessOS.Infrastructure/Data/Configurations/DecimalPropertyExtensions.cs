using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

internal static class DecimalPropertyExtensions
{
    public const int MoneyPrecision = 18;
    public const int MoneyScale = 2;

    public static PropertyBuilder<decimal> AsMoney(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(MoneyPrecision, MoneyScale);
}
