using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PetHealthManagement.Web.Data;

namespace PetHealthManagement.Web.Tests.Infrastructure;

internal static class TestDbContextFactory
{
    public static ApplicationDbContext CreateInMemoryDbContext(string databaseNamePrefix)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{databaseNamePrefix}-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    public static async Task<SqliteInMemoryTestContext> CreateSqliteInMemoryContextAsync(
        params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var dbContext = new ApplicationDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync();

        return new SqliteInMemoryTestContext(connection, dbContext);
    }
}

internal sealed class SqliteInMemoryTestContext(
    SqliteConnection connection,
    ApplicationDbContext dbContext) : IAsyncDisposable
{
    public SqliteConnection Connection { get; } = connection;

    public ApplicationDbContext DbContext { get; } = dbContext;

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
