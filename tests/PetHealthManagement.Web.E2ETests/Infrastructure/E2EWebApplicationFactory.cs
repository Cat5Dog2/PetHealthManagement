using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.E2ETests.Infrastructure;

public sealed class E2EWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"e2e-tests-{Guid.NewGuid():N}";
    private readonly TemporaryStorageRoot _storageRoot = new("PetHealthManagement.E2ETests");
    private readonly SemaphoreSlim _proxyLock = new(1, 1);
    private TestServerProxy<Program>? _proxy;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

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
            services.AddDataProtection()
                .SetApplicationName(DataProtectionKeyManagementOptions.DefaultApplicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(_storageRoot.RootPath, "data-protection-keys")));
            services.Configure<HttpsRedirectionOptions>(options =>
            {
                options.HttpsPort = null;
            });
            services.PostConfigure<AntiforgeryOptions>(options =>
            {
                // The local E2E proxy uses HTTP, so browsers reject __Host- cookies.
                options.Cookie.Name = "PetHealthManagement.E2E.AntiForgery";
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
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

    public async Task<Uri> GetServerAddressAsync()
    {
        if (_proxy is not null)
        {
            return _proxy.BaseAddress;
        }

        await _proxyLock.WaitAsync();
        try
        {
            if (_proxy is null)
            {
                _proxy = new TestServerProxy<Program>(this);
                await _proxy.StartAsync();
            }

            return _proxy.BaseAddress;
        }
        finally
        {
            _proxyLock.Release();
        }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _proxy?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _proxyLock.Dispose();
            }
            catch
            {
                // Best effort cleanup for the local E2E proxy.
            }
        }

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
}
