using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public sealed class HealthEndpointIntegrationTests
{
    [Fact]
    public async Task Health_ReturnsOk_WithoutAuthenticationOrDatabase()
    {
        using var baseFactory = new IntegrationTestWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthenticationService>();
            services.AddSingleton<IAuthenticationService>(new ThrowingAuthenticationService());
            services.RemoveAll<ApplicationDbContext>();
            services.AddScoped<ApplicationDbContext>(_ => throw new InvalidOperationException("Health probe accessed the database."));
        }));
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private sealed class ThrowingAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw UnexpectedAuthentication();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw UnexpectedAuthentication();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw UnexpectedAuthentication();
        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) => throw UnexpectedAuthentication();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw UnexpectedAuthentication();

        private static InvalidOperationException UnexpectedAuthentication() => new("Health probe executed authentication.");
    }
}
