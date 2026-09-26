namespace Taproom.Api.Clients;

/// <summary>Holds the latest merged snapshot in memory. Thread-safe singleton; no database.</summary>
public sealed class ClientSnapshotStore
{
    private readonly object _lock = new();
    private ClientSnapshot? _snapshot;

    public ClientSnapshot? Current
    {
        get
        {
            lock (_lock)
            {
                return _snapshot;
            }
        }
    }

    public void Update(ClientSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
        }
    }
}
