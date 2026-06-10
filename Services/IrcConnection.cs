using System.Net.Sockets;
using Irc7m.Models;

namespace Irc7m.Services;

/// <summary>
/// TCP connection wrapper for the IRC protocol.
/// Reads lines asynchronously on a background task, fires MessageReceived on the UI thread.
/// Handles PING → PONG transparently.
/// RawLineReceived fires for every line in/out (useful for debug panels).
/// </summary>
public class IrcConnection : IDisposable
{
    private TcpClient?               _client;
    private StreamReader?            _reader;
    private StreamWriter?            _writer;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim   _writeLock = new(1, 1);

    public bool IsConnected { get; private set; }

    /// <summary>Fired for every fully-parsed inbound message (UI thread).</summary>
    public event EventHandler<IrcMessage>? MessageReceived;

    /// <summary>Fired for every raw line received or sent – prefixed with "&lt;&lt; " or "&gt;&gt; " (UI thread).</summary>
    public event EventHandler<string>? RawLineReceived;

    public event EventHandler? Disconnected;

    public async Task ConnectAsync(string host, int port)
    {
        var resolvedHost = await ResolveHostAsync(host);
        _client = new TcpClient();
        await _client.ConnectAsync(resolvedHost, port);

        var stream = _client.GetStream();
        _reader = new StreamReader(stream, System.Text.Encoding.Latin1);
        _writer = new StreamWriter(stream, System.Text.Encoding.Latin1)
        {
            AutoFlush = true,
            NewLine   = "\r\n"
        };

        IsConnected = true;
        _cts = new CancellationTokenSource();
        _ = ReadLoopAsync(_cts.Token);
    }

    public async Task SendRawAsync(string line)
    {
        if (_writer is null || !IsConnected) return;
        await _writeLock.WaitAsync();
        try
        {
            // Fire raw event before the actual write so order is preserved
            MainThread.BeginInvokeOnMainThread(() => RawLineReceived?.Invoke(this, $">> {line}"));
            await _writer.WriteLineAsync(line);
        }
        finally { _writeLock.Release(); }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line is null) break;

                // Fire raw received event for every line (including PING)
                MainThread.BeginInvokeOnMainThread(() => RawLineReceived?.Invoke(this, $"<< {line}"));

                var msg = IrcMessage.Parse(line);

                // Handle PING transparently – PONG is sent via SendRawAsync so >> event fires there
                if (msg.Command == "PING")
                {
                    var token = msg.Trailing ?? (msg.Params.Length > 0 ? msg.Params[0] : "");
                    await SendRawAsync($"PONG :{token}");
                    continue;
                }

                MainThread.BeginInvokeOnMainThread(() => MessageReceived?.Invoke(this, msg));
            }
        }
        catch (OperationCanceledException) { }
        catch { /* swallow IO/socket errors; Disconnected fires below */ }
        finally
        {
            IsConnected = false;
            MainThread.BeginInvokeOnMainThread(() => Disconnected?.Invoke(this, EventArgs.Empty));
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        IsConnected = false;
        try { _client?.Close(); } catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves <paramref name="host"/> to a connectable address.
    /// If normal DNS lookup fails, checks whether the name matches the local
    /// machine's hostname and returns "127.0.0.1" so LAN servers advertising
    /// their short hostname are still reachable.
    /// </summary>
    private static async Task<string> ResolveHostAsync(string host)
    {
        // Happy path – host is already an IP or resolves cleanly
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
            if (addresses.Length > 0) return host;
        }
        catch { }

        // Fallback – if the name matches this machine, use loopback
        try
        {
            var localName = System.Net.Dns.GetHostName();
            if (string.Equals(host, localName, StringComparison.OrdinalIgnoreCase))
                return "127.0.0.1";
        }
        catch { }

        return host; // return original; ConnectAsync will surface the error
    }

    public void Dispose()
    {
        Disconnect();
        _writeLock.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
