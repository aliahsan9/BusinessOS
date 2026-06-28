using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using FluentAssertions;

namespace BusinessOS.UnitTests.Common;

public class PaginationParamsTests
{
    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(1, 20, 1, 20)]
    [InlineData(2, 50, 2, 50)]
    [InlineData(1, 500, 1, 100)]
    [InlineData(-1, -5, 1, 20)]
    public void Normalize_ReturnsExpectedValues(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var (normalizedPage, normalizedPageSize) = PaginationParams.Normalize(page, pageSize);

        normalizedPage.Should().Be(expectedPage);
        normalizedPageSize.Should().Be(expectedPageSize);
    }
}

public class SortDirectionTests
{
    [Theory]
    [InlineData("asc", SortDirection.Asc)]
    [InlineData("ASC", SortDirection.Asc)]
    [InlineData("desc", SortDirection.Desc)]
    [InlineData("DESC", SortDirection.Desc)]
    [InlineData(null, SortDirection.Asc)]
    [InlineData("", SortDirection.Asc)]
    public void ParseSortDirection_ReturnsExpectedDirection(string? value, SortDirection expected)
    {
        QueryableSortingExtensions.ParseSortDirection(value).Should().Be(expected);
    }
}

public class PagedResultTests
{
    [Fact]
    public void TotalPages_CalculatesCorrectly()
    {
        var result = new PagedResult<string>
        {
            Items = ["a", "b"],
            Page = 1,
            PageSize = 2,
            TotalCount = 5
        };

        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }
}
