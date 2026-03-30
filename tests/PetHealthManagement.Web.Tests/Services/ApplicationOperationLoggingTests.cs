using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Services;

public class ApplicationOperationLoggingTests
{
    [Fact]
    public void LogUnhandledRequestException_WritesStructuredRequestFields()
    {
        var logger = new TestLogger<object>();
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123"
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/Visits/Create";
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-a")],
                "TestAuth"));

        ApplicationOperationLogging.LogUnhandledRequestException(
            logger,
            new InvalidOperationException("boom"),
            httpContext);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.LogLevel);
        Assert.Equal(HttpMethods.Post, entry.Properties["Method"]);
        Assert.Equal("/Visits/Create", entry.Properties["Path"]?.ToString());
        Assert.Equal("trace-123", entry.Properties["TraceId"]);
        Assert.Equal("user-a", entry.Properties["UserId"]);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }
}
