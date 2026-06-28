using System.Linq.Expressions;
using BusinessOS.Application.Common.Models;

namespace BusinessOS.Application.Common.Extensions;

public static class QueryableSortingExtensions
{
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        SortDirection direction,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> sortFields,
        Expression<Func<T, object>> defaultSort)
    {
        var key = string.IsNullOrWhiteSpace(sortBy)
            ? null
            : sortBy.Trim();

        if (key is null || !sortFields.TryGetValue(key, out var sortExpression))
        {
            return direction == SortDirection.Desc
                ? query.OrderByDescending(defaultSort)
                : query.OrderBy(defaultSort);
        }

        return direction == SortDirection.Desc
            ? query.OrderByDescending(sortExpression)
            : query.OrderBy(sortExpression);
    }

    public static SortDirection ParseSortDirection(string? value) =>
        string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Desc
            : SortDirection.Asc;
}
