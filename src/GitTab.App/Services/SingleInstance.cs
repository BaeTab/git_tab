using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace GitTab.App.Services;

/// <summary>
/// Ensures a single running Git Tab per user. The first process becomes the <b>primary</b> and
/// listens on a named pipe; any later process (e.g. one launched from an Explorer right-click)
/// forwards its command line to the primary and exits, so shell actions land in the existing
/// window instead of spawning a second app.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\GitTab.SingleInstance.v1";
    private static string PipeName => "GitTab.ipc." + Environment.UserName;

    private Mutex? _mutex;
    private CancellationTokenSource? _cts;

    /// <summary>Returns true if this process is the first (primary) instance.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        return createdNew;
    }

    /// <summary>
    /// Begin listening for forwarded command lines. <paramref name="onMessage"/> is invoked on a
    /// background thread; marshal to the UI thread inside the handler.
    /// </summary>
    public void StartServer(Action<string> onMessage)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var msg = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(msg)) onMessage(msg);
                }
                catch (OperationCanceledException) { break; }
                catch { /* keep the listener alive across malformed / aborted connections */ }
            }
        }, ct);
    }

    /// <summary>Forward a message to the primary instance. Returns false if none is listening.</summary>
    public static bool TrySend(string message, int timeoutMs = 2000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            writer.Write(message);
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _mutex?.ReleaseMutex(); } catch { /* not owner / already released */ }
        _mutex?.Dispose();
    }
}
