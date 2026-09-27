using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PetHealthManagement.Web.Tests.Infrastructure;

// 次のコミットで指定した例外を投げる。コミット前に投げるため、トランザクションは確定しない。
internal sealed class CommitFailureInterceptor : DbTransactionInterceptor
{
    private readonly Queue<Exception> _pendingFailures = new();

    public int FailedCommitCount { get; private set; }

    public void FailNextCommitWith(Exception exception)
    {
        _pendingFailures.Enqueue(exception);
    }

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result)
    {
        ThrowIfFailurePending();
        return base.TransactionCommitting(transaction, eventData, result);
    }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfFailurePending();
        return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    private void ThrowIfFailurePending()
    {
        if (_pendingFailures.TryDequeue(out var exception))
        {
            FailedCommitCount++;
            throw exception;
        }
    }
}
