using Microsoft.Extensions.Options;
using Taproom.Api.Clients;
using Taproom.Api.Config;
using Taproom.Api.Sources.Omada;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Polling;

/// <summary>
/// Polls Omada and OPNsense in parallel on an interval, merges the results, and publishes the
/// snapshot to <see cref="ClientSnapshotStore"/>. If a source fails, its last good data is reused
/// and the source is marked unhealthy rather than emptying the table.
/// </summary>
public sealed class PollerService : BackgroundService
{
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(10);

    private readonly IOmadaClient _omada;
    private readonly IOpnsenseClient _opnsense;
    private readonly ClientSnapshotStore _store;
    private readonly TaproomOptions _options;
    private readonly ILogger<PollerService> _logger;

    private IReadOnlyList<OmadaClientRecord> _lastOmada = [];
    private IReadOnlyList<ArpEntry> _lastArp = [];
    private IReadOnlyList<DhcpLease> _lastDhcp = [];
    private SourceStatus _omadaStatus = new() { Ok = false };
    private SourceStatus _opnsenseStatus = new() { Ok = false };

    public PollerService(
        IOmadaClient omada,
        IOpnsenseClient opnsense,
        ClientSnapshotStore store,
        IOptions<TaproomOptions> options,
        ILogger<PollerService> logger)
    {
        _omada = omada;
        _opnsense = opnsense;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        var omadaTask = FetchOmadaAsync(stoppingToken);
        var opnsenseTask = FetchOpnsenseAsync(stoppingToken);
        await Task.WhenAll(omadaTask, opnsenseTask);

        var clients = ClientMerger.Merge(_lastOmada, _lastArp, _lastDhcp);
        _store.Update(new ClientSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Sources = new Dictionary<string, SourceStatus>
            {
                ["omada"] = _omadaStatus,
                ["opnsense"] = _opnsenseStatus,
            },
            Clients = clients,
        });
    }

    private async Task FetchOmadaAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(SourceTimeout);
        try
        {
            _lastOmada = await _omada.GetClientsAsync(cts.Token);
            _omadaStatus = new SourceStatus { Ok = true, LastSuccess = DateTimeOffset.UtcNow };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Omada poll failed");
            _omadaStatus = _omadaStatus with { Ok = false, Error = ex.Message };
        }
    }

    private async Task FetchOpnsenseAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(SourceTimeout);
        try
        {
            var arpTask = _opnsense.GetArpTableAsync(cts.Token);
            var dhcpTask = _opnsense.GetDhcpLeasesAsync(cts.Token);
            await Task.WhenAll(arpTask, dhcpTask);
            _lastArp = arpTask.Result;
            _lastDhcp = dhcpTask.Result;
            _opnsenseStatus = new SourceStatus { Ok = true, LastSuccess = DateTimeOffset.UtcNow };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "OPNsense poll failed");
            _opnsenseStatus = _opnsenseStatus with { Ok = false, Error = ex.Message };
        }
    }
}
