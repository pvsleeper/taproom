namespace Taproom.Api.Sources.Opnsense;

/// <summary>
/// OPNsense's web backend occasionally corrupts the chunked-transfer framing on larger JSON responses
/// (HttpIOException: "Received an invalid chunk terminator"), seemingly under load — most often observed
/// while an admin is also browsing the web UI at the same time. It's transient: a prompt retry succeeds.
/// </summary>
public static class TransientHttpRetry
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await Task.Delay(Delay, cancellationToken);
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                await Task.Delay(Delay, cancellationToken);
            }
        }
    }
}
