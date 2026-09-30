namespace Taproom.Api.Sources.Opnsense;

public interface IInterfaceCounterClient
{
    /// <summary>Cumulative (bytes received, bytes transmitted) for the given logical interface, e.g. "wan".</summary>
    Task<(long BytesReceived, long BytesTransmitted)> GetCountersAsync(string interfaceName, CancellationToken cancellationToken);
}
