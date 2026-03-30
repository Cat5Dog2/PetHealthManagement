using System.Data.Common;
using System.Threading;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PetHealthManagement.Web.Tests.Infrastructure;

internal sealed class QueryCountingDbCommandInterceptor : DbCommandInterceptor
{
    private int _executedCommandCount;

    public int ExecutedCommandCount => Volatile.Read(ref _executedCommandCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _executedCommandCount, 0);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Increment();
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Increment();
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Increment();
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void Increment()
    {
        Interlocked.Increment(ref _executedCommandCount);
    }
}
