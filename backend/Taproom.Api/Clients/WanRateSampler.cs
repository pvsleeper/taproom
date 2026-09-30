using Microsoft.Extensions.Options;
using Taproom.Api.Config;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Turns the WAN interface's cumulative byte counters into a live rate by comparing against the previous
/// sample. A sample younger than 2 seconds is reused; otherwise one new sample is taken and shared by
/// every concurrent caller (single-flight, same pattern as phase 4's InterfaceSampler). A counter that
/// goes down (router reboot) is treated as a new baseline, never a negative rate.
/// </summary>
public sealed class WanRateSampler
{
    private static readonly TimeSpan ReuseWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FallbackWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(5);

    private readonly IInterfaceCounterClient _client;
    private readonly string _interfaceName;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<WanRateSampler> _logger;

    private DateTimeOffset _lastSuccessAt = DateTimeOffset.MinValue;
    private (long Rx, long Tx, DateTimeOffset At)? _previousCounters;
    private (long DownBps, long UpBps) _lastRate;

    public WanRateSampler(IInterfaceCounterClient client, IOptions<TaproomOptions> options, ILogger<WanRateSampler> logger)
    {
        _client = client;
        _interfaceName = options.Value.Opnsense.WanInterface;
        _logger = logger;
    }

    public async Task<(long DownBps, long UpBps, bool Ok)> GetRateAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return (_lastRate.DownBps, _lastRate.UpBps, true);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return (_lastRate.DownBps, _lastRate.UpBps, true);
            }

            try
            {
                using var cts = new CancellationTokenSource(SampleTimeout);
                var (rx, tx) = await _client.GetCountersAsync(_interfaceName, cts.Token);
                var now = DateTimeOffset.UtcNow;

                if (_previousCounters is { } prev)
                {
                    var elapsed = (now - prev.At).TotalSeconds;
                    if (elapsed > 0 && rx >= prev.Rx && tx >= prev.Tx)
                    {
                        _lastRate = ((long)((rx - prev.Rx) * 8 / elapsed), (long)((tx - prev.Tx) * 8 / elapsed));
                    }
                    else
                    {
                        // First sample after a counter reset (rx/tx went down) — no valid rate yet.
                        _lastRate = (0, 0);
                    }
                }
                else
                {
                    _lastRate = (0, 0);
                }

                _previousCounters = (rx, tx, now);
                _lastSuccessAt = now;
                return (_lastRate.DownBps, _lastRate.UpBps, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WAN interface counter sample failed");
                if (DateTimeOffset.UtcNow - _lastSuccessAt < FallbackWindow)
                {
                    return (_lastRate.DownBps, _lastRate.UpBps, false);
                }
                _lastRate = (0, 0);
                return (0, 0, false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsFresh() => _lastSuccessAt != DateTimeOffset.MinValue && DateTimeOffset.UtcNow - _lastSuccessAt < ReuseWindow;
}
