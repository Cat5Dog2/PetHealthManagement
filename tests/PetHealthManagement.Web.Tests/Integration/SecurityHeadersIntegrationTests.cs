using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public class SecurityHeadersIntegrationTests
{
    [Fact]
    public async Task HomePage_ReturnsConfiguredSecurityHeaders()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertHeader(response, "Content-Security-Policy", SecurityHeaders.ContentSecurityPolicy);
        AssertHeader(response, "Permissions-Policy", SecurityHeaders.PermissionsPolicy);
        AssertHeader(response, "Referrer-Policy", SecurityHeaders.ReferrerPolicy);
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
    }

    [Fact]
    public async Task VisitsCreate_SetsAntiforgeryCookie_WithSecureAttributes()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            dbContext.Pets.Add(new Pet
            {
                Id = 1,
                OwnerId = "owner-user",
                Name = "Mugi",
                SpeciesCode = "DOG",
                IsPublic = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");
        using var response = await client.GetAsync("/Visits/Create?petId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieValues));

        var antiforgeryCookie = Assert.Single(
            setCookieValues,
            value => value.StartsWith("__Host-PetHealthManagement.AntiForgery=", StringComparison.Ordinal));

        Assert.Contains("path=/", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Services_ConfigureSecureCookieAndHstsDefaults()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();

        var authCookieOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var antiforgeryOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<AntiforgeryOptions>>()
            .Value;
        var hstsOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<HstsOptions>>()
            .Value;

        Assert.Equal("__Host-PetHealthManagement.Auth", authCookieOptions.Cookie.Name);
        Assert.Equal("/", authCookieOptions.Cookie.Path);
        Assert.True(authCookieOptions.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, authCookieOptions.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, authCookieOptions.Cookie.SameSite);

        Assert.Equal("__Host-PetHealthManagement.AntiForgery", antiforgeryOptions.Cookie.Name);
        Assert.Equal("/", antiforgeryOptions.Cookie.Path);
        Assert.True(antiforgeryOptions.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, antiforgeryOptions.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, antiforgeryOptions.Cookie.SameSite);

        Assert.Equal(TimeSpan.FromDays(180), hstsOptions.MaxAge);
        Assert.False(hstsOptions.IncludeSubDomains);
        Assert.False(hstsOptions.Preload);
    }

    private static void AssertHeader(HttpResponseMessage response, string headerName, string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var values));
        Assert.Equal(expectedValue, values.Single());
    }
}
