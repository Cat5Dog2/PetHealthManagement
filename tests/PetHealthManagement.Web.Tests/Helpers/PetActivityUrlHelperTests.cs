using PetHealthManagement.Web.Helpers;

namespace PetHealthManagement.Web.Tests.Helpers;

public class PetActivityUrlHelperTests
{
    [Theory]
    [InlineData(null, "/HealthLogs?petId=1")]
    [InlineData("2", "/HealthLogs?petId=1&page=2")]
    [InlineData("abc", "/HealthLogs?petId=1")]
    public void HealthLogList_PreservesExistingFallbackBehavior(string? page, string expected)
    {
        var actual = PetActivityUrlHelper.HealthLogList(1, page);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, "/Visits?petId=1")]
    [InlineData("2", "/Visits?petId=1&page=2")]
    [InlineData("abc", "/Visits?petId=1&page=1")]
    public void VisitList_PreservesExistingFallbackBehavior(string? page, string expected)
    {
        var actual = PetActivityUrlHelper.VisitList(1, page);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, "/ScheduleItems?petId=1&page=1")]
    [InlineData("2", "/ScheduleItems?petId=1&page=2")]
    [InlineData("abc", "/ScheduleItems?petId=1&page=1")]
    public void ScheduleItemList_PreservesExistingFallbackBehavior(string? page, string expected)
    {
        var actual = PetActivityUrlHelper.ScheduleItemList(1, page);

        Assert.Equal(expected, actual);
    }
}
