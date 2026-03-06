using PetHealthManagement.Web.Helpers;

namespace PetHealthManagement.Web.Tests.Helpers;

public class ReturnUrlHelperTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/pets", true)]
    [InlineData("~/healthlogs", true)]
    [InlineData("https://example.com", false)]
    [InlineData("//evil.example.com", false)]
    [InlineData("/\\evil", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLocalUrl_ReturnsExpected(string? url, bool expected)
    {
        var isLocal = ReturnUrlHelper.IsLocalUrl(url);

        Assert.Equal(expected, isLocal);
    }

    [Fact]
    public void ResolveLocalReturnUrl_UsesReturnUrl_WhenReturnUrlIsLocal()
    {
        var actual = ReturnUrlHelper.ResolveLocalReturnUrl("/pets?page=2", "/mypage");

        Assert.Equal("/pets?page=2", actual);
    }

    [Fact]
    public void ResolveLocalReturnUrl_UsesFallback_WhenReturnUrlIsNotLocal()
    {
        var actual = ReturnUrlHelper.ResolveLocalReturnUrl("https://example.com", "/mypage");

        Assert.Equal("/mypage", actual);
    }

    [Fact]
    public void ResolveLocalReturnUrl_Throws_WhenFallbackIsNotLocal()
    {
        Assert.Throws<ArgumentException>(() => ReturnUrlHelper.ResolveLocalReturnUrl("/mypage", "https://example.com"));
    }
}
