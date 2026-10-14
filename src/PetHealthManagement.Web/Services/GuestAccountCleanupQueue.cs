using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace PetHealthManagement.Web.Services;

public sealed class GuestAccountCleanupQueue(
    TimeProvider timeProvider,
    IOptions<GuestLoginOptions> options)
{
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    private readonly object _gate = new();
    private DateTimeOffset? _lastRequestedAt;
    private bool _requestPending;

    public bool TryRequestCleanup()
    {
        if (!options.Value.CleanupEnabled)
        {
            return false;
        }

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (_requestPending
                || (_lastRequestedAt.HasValue && now - _lastRequestedAt.Value < GuestAccountCleanupWorker.Interval))
            {
                return false;
            }

            if (!_requests.Writer.TryWrite(true))
            {
                return false;
            }

            _lastRequestedAt = now;
            _requestPending = true;
            return true;
        }
    }

    public IAsyncEnumerable<bool> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _requests.Reader.ReadAllAsync(cancellationToken);
    }

    public void CompleteCleanup()
    {
        lock (_gate)
        {
            _lastRequestedAt = timeProvider.GetUtcNow();
            _requestPending = false;
        }
    }
}
