using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Infrastructure;

internal sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"integration-tests-{Guid.NewGuid():N}";
    private readonly TemporaryStorageRoot _storageRoot = new("PetHealthManagement.IntegrationTests");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.PostConfigure<StorageOptions>(options =>
            {
                options.RootPath = _storageRoot.RootPath;
            });

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthenticationDefaults.CompositeScheme;
                    options.DefaultAuthenticateScheme = TestAuthenticationDefaults.CompositeScheme;
                    options.DefaultForbidScheme = TestAuthenticationDefaults.CompositeScheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                })
                .AddPolicyScheme(TestAuthenticationDefaults.CompositeScheme, displayName: null, options =>
                {
                    options.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey(TestAuthenticationDefaults.UserIdHeaderName)
                            ? TestAuthenticationDefaults.HeaderScheme
                            : IdentityConstants.ApplicationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, HeaderTestAuthenticationHandler>(
                    TestAuthenticationDefaults.HeaderScheme,
                    _ => { });
        });
    }

    public HttpClient CreateAnonymousClient(bool allowAutoRedirect = false)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public HttpClient CreateAuthenticatedClient(
        string userId,
        string? userName = null,
        IEnumerable<string>? roles = null,
        bool allowAutoRedirect = false)
    {
        var client = CreateAnonymousClient(allowAutoRedirect);
        client.DefaultRequestHeaders.Add(TestAuthenticationDefaults.UserIdHeaderName, userId);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationDefaults.UserNameHeaderName, userName);
        }

        var normalizedRoles = roles?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .ToArray();

        if (normalizedRoles is { Length: > 0 })
        {
            client.DefaultRequestHeaders.Add(
                TestAuthenticationDefaults.RolesHeaderName,
                string.Join(',', normalizedRoles));
        }

        return client;
    }

    public async Task ResetDatabaseAsync(Func<ApplicationDbContext, Task> seedAsync)
    {
        ArgumentNullException.ThrowIfNull(seedAsync);

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        await seedAsync(dbContext);
        await dbContext.SaveChangesAsync();
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(dbContext);
    }

    public async Task<AntiforgeryRequestData> CreateAntiforgeryRequestDataAsync(
        string userId,
        string? userName = null,
        IEnumerable<string>? roles = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = CreatePrincipal(userId, userName, roles)
        };
        httpContext.Request.Scheme = Uri.UriSchemeHttps;

        var tokenSet = antiforgery.GetAndStoreTokens(httpContext);
        var cookieHeaderValue = httpContext.Response.Headers.SetCookie.ToString().Split(';', 2)[0];

        return new AntiforgeryRequestData(
            cookieHeaderValue,
            tokenSet.FormFieldName!,
            tokenSet.RequestToken!);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            _storageRoot.Dispose();
        }
        catch
        {
            // Best effort cleanup for test storage.
        }
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, string? userName, IEnumerable<string>? roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(userName) ? userId : userName)
        };

        if (roles is not null)
        {
            foreach (var role in roles.Where(role => !string.IsNullOrWhiteSpace(role)))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
    }
}

internal sealed record AntiforgeryRequestData(
    string CookieHeaderValue,
    string FormFieldName,
    string RequestToken);
