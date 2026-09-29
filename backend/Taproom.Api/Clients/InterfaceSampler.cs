using Microsoft.Extensions.Options;
using Taproom.Api.Config;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>
/// Holds the latest top-talkers sample for the LAN interface. A sample younger than 2 seconds is reused;
/// otherwise one new sample is taken and shared by every concurrent caller (single-flight), so N detail
/// pages open at once cause one upstream call, not N. No background timer — nothing is sampled unless a
/// request comes in.
/// </summary>
public sealed class InterfaceSampler
{
    private static readonly TimeSpan ReuseWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FallbackWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(5);

    private readonly ITopTalkersClient _client;
    private readonly string _interfaceName;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<InterfaceSampler> _logger;

    private IReadOnlyList<TopTalkerRecord> _lastSample = [];
    private DateTimeOffset _lastSuccessAt = DateTimeOffset.MinValue;

    public InterfaceSampler(ITopTalkersClient client, IOptions<TaproomOptions> options, ILogger<InterfaceSampler> logger)
    {
        _client = client;
        _interfaceName = options.Value.Opnsense.LanInterface;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<TopTalkerRecord> Records, bool Ok)> GetSampleAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return (_lastSample, true);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return (_lastSample, true);
            }

            try
            {
                // Not linked to the caller's token: this fetch is shared by every waiter above, and one
                // caller disconnecting shouldn't abort it for the rest.
                using var cts = new CancellationTokenSource(SampleTimeout);
                var records = await _client.GetTopTalkersAsync(_interfaceName, cts.Token);
                _lastSample = records;
                _lastSuccessAt = DateTimeOffset.UtcNow;
                return (records, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Top-talkers sample failed");
                if (DateTimeOffset.UtcNow - _lastSuccessAt < FallbackWindow)
                {
                    return (_lastSample, false);
                }
                _lastSample = [];
                return ([], false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsFresh() => _lastSuccessAt != DateTimeOffset.MinValue && DateTimeOffset.UtcNow - _lastSuccessAt < ReuseWindow;
}
