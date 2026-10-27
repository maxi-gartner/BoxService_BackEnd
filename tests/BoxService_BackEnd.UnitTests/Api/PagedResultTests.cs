using BoxService_BackEnd.Api;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Api;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(50, 25, 2)]
    [InlineData(51, 25, 3)]
    [InlineData(1, 25, 1)]
    [InlineData(50, 0, 0)]
    [InlineData(50, -1, 0)]
    public void TotalPages_ForCountsAndPageSizes_ReturnsExpectedValue(
        int totalCount, int pageSize, int expected)
    {
        var result = new PagedResult<int>
        {
            Items = [],
            Page = 1,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        var totalPages = result.TotalPages;

        Assert.Equal(expected, totalPages);
    }
}
