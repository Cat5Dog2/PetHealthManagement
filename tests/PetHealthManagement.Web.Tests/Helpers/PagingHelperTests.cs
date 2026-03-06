using PetHealthManagement.Web.Helpers;

namespace PetHealthManagement.Web.Tests.Helpers;

public class PagingHelperTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    public void NormalizePage_ReturnsExpectedPage(int? page, int expected)
    {
        var normalized = PagingHelper.NormalizePage(page);

        Assert.Equal(expected, normalized);
    }
}
