using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Tests.Models;

public class ScheduleItemTypeCatalogTests
{
    [Theory]
    [InlineData("medicine", ScheduleItemTypeCatalog.Medicine)]
    [InlineData(" Medicine ", ScheduleItemTypeCatalog.Medicine)]
    [InlineData("Visit", ScheduleItemTypeCatalog.Visit)]
    public void TryNormalizeCode_ReturnsCanonicalCode_ForKnownCodes(string code, string expected)
    {
        var isKnown = ScheduleItemTypeCatalog.TryNormalizeCode(code, out var normalizedCode);

        Assert.True(isKnown);
        Assert.Equal(expected, normalizedCode);
    }

    [Fact]
    public void NormalizeFilterCode_ReturnsUppercaseSentinel_ForUnknownCode()
    {
        var normalizedCode = ScheduleItemTypeCatalog.NormalizeFilterCode(" custom ");

        Assert.Equal("CUSTOM", normalizedCode);
    }
}
