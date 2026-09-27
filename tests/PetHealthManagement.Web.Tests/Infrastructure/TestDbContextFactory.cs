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

    public static Task<SqliteInMemoryTestContext> CreateSqliteInMemoryContextAsync(
        params IInterceptor[] interceptors)
    {
        return CreateSqliteInMemoryContextCoreAsync(retryOnFailure: false, interceptors);
    }

    // 本番（SQL Server + EnableRetryOnFailure）と同じく、再試行する実行戦略を設定した SQLite。
    public static Task<SqliteInMemoryTestContext> CreateRetryingSqliteInMemoryContextAsync(
        params IInterceptor[] interceptors)
    {
        return CreateSqliteInMemoryContextCoreAsync(retryOnFailure: true, interceptors);
    }

    // DI に登録する場合など、DbContext を自分で作らないときにも同じ設定を使えるようにする
    public static DbContextOptionsBuilder UseRetryingSqlite(
        DbContextOptionsBuilder optionsBuilder,
        SqliteConnection connection,
        params IInterceptor[] interceptors)
    {
        return UseSqlite(optionsBuilder, connection, retryOnFailure: true, interceptors);
    }

    private static DbContextOptionsBuilder UseSqlite(
        DbContextOptionsBuilder optionsBuilder,
        SqliteConnection connection,
        bool retryOnFailure,
        IInterceptor[] interceptors)
    {
        optionsBuilder.UseSqlite(connection, sqliteOptions =>
        {
            if (retryOnFailure)
            {
                sqliteOptions.ExecutionStrategy(dependencies => new RetryingTestExecutionStrategy(dependencies));
            }
        });

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        return optionsBuilder;
    }

    private static async Task<SqliteInMemoryTestContext> CreateSqliteInMemoryContextCoreAsync(
        bool retryOnFailure,
        IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        UseSqlite(optionsBuilder, connection, retryOnFailure, interceptors);

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
