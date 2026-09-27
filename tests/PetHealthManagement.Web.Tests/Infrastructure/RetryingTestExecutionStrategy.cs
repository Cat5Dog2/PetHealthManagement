using Microsoft.EntityFrameworkCore.Storage;

namespace PetHealthManagement.Web.Tests.Infrastructure;

// 本番の EnableRetryOnFailure と同じく「再試行する」実行戦略。
// InMemory / SQLite の既定（再試行なし）では、実行戦略の外で開始したトランザクションの誤用を検出できないため、テストで再現する。
internal sealed class RetryingTestExecutionStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
{
    protected override bool ShouldRetryOn(Exception exception)
    {
        return exception is TransientTestException;
    }
}

internal sealed class TransientTestException(string message) : Exception(message);
