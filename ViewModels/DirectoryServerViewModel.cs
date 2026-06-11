using Irc7m.Models;
using Irc7m.Services;

namespace Irc7m.ViewModels;

/// <summary>
/// The permanent first tab. Connects to a Directory Server and resolves channels via FINDS.
/// </summary>
public class DirectoryServerViewModel : ChatWindowViewModel
{
    private readonly IrcClientSettings _settings;
    private readonly MainViewModel     _mainVm;

    private bool    _isLoggedOn;
    private string? _pendingChannel;
    private CancellationTokenSource? _keepAliveCts;

    // ── Public state ────────────────────────────────────────────────────────────
    public bool IsConnected
    {
        get => _isLoggedOn;
        private set
        {
            _isLoggedOn = value;
            OnPropertyChanged();
        }
    }

    // ── Reconnect state ────────────────────────────────────────────────────────
    private string? _lastHost;
    private int     _lastPort;
    private bool    _intentionalDisconnect;
    private int     _reconnectAttempt;

    /// <summary>The hostname of the most recently attempted connection (persisted across connect/disconnect).</summary>
    public string? LastHost => _lastHost;
    public int     LastPort => _lastPort;

    public override bool IsCloseable => false;

    // ── Constructor ────────────────────────────────────────────────────────────

    public DirectoryServerViewModel(IrcClientSettings settings, MainViewModel mainVm)
    {
        _settings = settings;
        _mainVm   = mainVm;
        Title     = "Directory Server";

        AppendLine("* Welcome to Irc7m.");
        AppendLine("* Use /irc7 to connect to the Irc7 Directory Server.");
        AppendLine("* Or use /connect <ip> <port> for a custom server.");
        AppendLine("* Right-click for debug options.");
    }

    // ─��� Public entry points ────────────────────────────────────────────────────

    /// <summary>
    /// Called externally (e.g. from a channel window) to perform a /join via this Directory Server.
    /// </summary>
    public void RequestJoin(string channel)
    {
        var normalized = NormalizeChannel(channel);

        if (!_isLoggedOn)
        {
            AppendLine($"* Cannot join {normalized}: not connected to Directory Server.");
            return;
        }

        _pendingChannel = normalized;
        AppendLine($"* Looking up {normalized}\u2026");
        _ = Connection!.SendRawAsync($"FINDS {normalized}");
    }

    /// <summary>Disconnects intentionally (no reconnect).</summary>
    public void IntentionalDisconnect()
    {
        _intentionalDisconnect = true;
        StopKeepAlive();
        Connection?.Disconnect();
        Connection  = null;
        IsConnected = false;
        AppendLine("* Disconnected.");
    }

    /// <summary>Programmatically connects to a server (called from menu Servers submenu).</summary>
    public void HandleConnectCommand(string host, int port)
    {
        _intentionalDisconnect = true;
        if (Connection is not null) { Connection.Disconnect(); Connection = null; IsConnected = false; }
        _ = ConnectToDirectoryAsync(host, port);
    }

    /// <summary>Sends LIST to retrieve available channels.</summary>
    public void RequestChannelList()
    {
        if (!IsConnected) { AppendLine("* Not connected."); return; }
        AppendLine("* Requesting channel list…");
        _ = Connection!.SendRawAsync("LIST");
    }

    // ── Command handling ───────────────────────────────────────────────────────

    protected override void HandleCommand(string verb, string[] args)
    {
        switch (verb)
        {
            case "irc7":
                _intentionalDisconnect = true;
                if (Connection is not null) { Connection.Disconnect(); Connection = null; IsConnected = false; }
                _ = ConnectToDirectoryAsync("dir.irc7.com", 6667);
                break;

            case "server":
            case "connect":
                if (args.Length < 2) { AppendLine("* Usage: /connect <ip> <port>"); return; }
                if (!int.TryParse(args[1], out var port)) { AppendLine("* Invalid port."); return; }

                _intentionalDisconnect = true;
                if (Connection is not null) { Connection.Disconnect(); Connection = null; IsConnected = false; }
                _ = ConnectToDirectoryAsync(args[0], port);
                break;

            case "join":
                if (!IsConnected)
                {
                    AppendLine("* Not connected. Use /connect <ip> <port> first.");
                    return;
                }
                if (args.Length < 1) { AppendLine("* Usage: /join <channel>"); return; }

                var channel = NormalizeChannel(args[0]);
                _pendingChannel = channel;
                AppendLine($"* Looking up {channel}\u2026");
                _ = Connection!.SendRawAsync($"FINDS {channel}");
                break;

            default:
                // If any argument looks like a channel name, treat it as the
                // pending channel so an incoming 613 knows what to open.
                var channelArg = args.FirstOrDefault(a =>
                    a.StartsWith("%#", StringComparison.Ordinal) ||
                    a.StartsWith("#",  StringComparison.Ordinal) ||
                    a.StartsWith("&",  StringComparison.Ordinal));

                if (channelArg is not null)
                    _pendingChannel = NormalizeChannel(channelArg);

                base.HandleCommand(verb, args);
                break;
        }
    }

    // ── Connection ─────────────────────────────────────────────────────────────

    private async Task ConnectToDirectoryAsync(string host, int port)
    {
        _lastHost              = host;
        _lastPort              = port;
        _intentionalDisconnect = false;
        ConnectionState        = IrcConnectionState.Connecting;
        try
        {
            AppendDebug($"⚡ Connecting to {host}:{port}\u2026");
            var conn = new IrcConnection();
            conn.MessageReceived += OnMessageReceived;
            conn.Disconnected    += OnDisconnected;
            conn.RawLineReceived += (_, raw) => AppendDebug(raw);
            Connection = conn;

            await conn.ConnectAsync(host, port);
            await conn.SendRawAsync($"NICK {_settings.Nick}");
            await conn.SendRawAsync($"USER {_settings.UserName} 0 * :{_settings.RealName}");
        }
        catch (Exception ex)
        {
            AppendDebug($"⚡ Connection failed: {ex.Message}");
            ConnectionState = IrcConnectionState.Disconnected;
            Connection      = null;

            // TCP connect itself failed – OnDisconnected won't fire, so re-schedule here
            if (!_intentionalDisconnect)
                _ = ScheduleReconnectAsync();
        }
    }

    // ── Inbound message handling ───────────────────────────────────────────────

    private void OnMessageReceived(object? sender, IrcMessage msg)
    {
        switch (msg.Command)
        {
            case "001":
                _reconnectAttempt = 0;
                IsConnected       = true;
                ConnectionState   = IrcConnectionState.Connected;
                AppendLine($"* Connected to Directory Server as {_settings.Nick}");
                AppendLine("* Use /join <channel> to look up a channel.");
                // Persist this server so Connect menu can reconnect to it
                if (_lastHost is not null)
                    _settings.AddRecentServer(_lastHost, _lastPort);
                StartKeepAlive();
                break;

            case "613":
                Handle613(msg);
                break;

            case "702":
                _pendingChannel = null;
                AppendLine($"* Error: {msg.Trailing ?? "Channel not found"}");
                break;

            case "433":
                AppendLine($"* Nick '{_settings.Nick}' is already in use.");
                break;

            case "NOTICE":
                AppendLine($"* {msg.PrefixNick ?? "Server"}: {msg.Trailing}");
                break;

            case "PRIVMSG":
            {
                var pmTarget = msg.Params.Length > 0 ? msg.Params[0] : "";
                var pmText   = msg.Trailing ?? "";
                var pmNick   = msg.PrefixNick ?? msg.Prefix ?? "Server";

                // Handle CTCP within the DS connection
                if (pmText.StartsWith("\x01") && pmText.EndsWith("\x01"))
                {
                    HandleCtcp(pmNick, pmText);
                    break;
                }

                // DMs addressed to us → Messages tab
                if (pmTarget.Equals(_settings.Nick, StringComparison.OrdinalIgnoreCase))
                    _mainVm.PrivateMessages.ReceiveMessage(pmNick, pmText, Connection);
                else
                    AppendLine($"<{pmNick}> {pmText}");
                break;
            }

            case "WHISPER":
            {
                var wNick = msg.PrefixNick ?? msg.Prefix ?? "Server";
                var wText = msg.Trailing ?? "";
                _mainVm.PrivateMessages.ReceiveMessage(wNick, wText, Connection, isWhisper: true);
                break;
            }

            case "372":
            case "375":
                if (!string.IsNullOrEmpty(msg.Trailing))
                    AppendLine(msg.Trailing);
                break;

            case "376":
                break;

            default:
                if (!string.IsNullOrEmpty(msg.Trailing))
                    AppendLine($"[{msg.Command}] {msg.Trailing}");
                break;
        }
    }

    private void Handle613(IrcMessage msg)
    {
        if (msg.Trailing is null)
        {
            AppendLine("* 613 received but no server address in reply.");
            return;
        }

        var parts = msg.Trailing.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var chatPort))
        {
            AppendLine($"* 613 parse error: '{msg.Trailing}'");
            return;
        }

        var ip      = parts[0];
        var channel = _pendingChannel ?? "#unknown";
        _pendingChannel = null;

        AppendLine($"* Found {channel} \u2192 {ip}:{chatPort}  Opening channel window\u2026");
        _mainVm.OpenChannel(channel, ip, chatPort);
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        IsConnected = false;
        StopKeepAlive();
        ConnectionState = IrcConnectionState.Disconnected;
        AppendDebug("⚡ Disconnected from Directory Server.");

        if (!_intentionalDisconnect && _lastHost is not null)
            _ = ScheduleReconnectAsync();
    }

    // ── Auto-reconnect ─────────────────────────────────────────────────────────

    private async Task ScheduleReconnectAsync()
    {
        _reconnectAttempt++;
        const int delay = 5;

        ConnectionState = IrcConnectionState.Reconnecting;
        AppendDebug($"⚡ Reconnect attempt {_reconnectAttempt} in {delay}s\u2026");

        await Task.Delay(TimeSpan.FromSeconds(delay));

        if (Connection?.IsConnected == true || _intentionalDisconnect) return;

        await ConnectToDirectoryAsync(_lastHost!, _lastPort);
        // ConnectToDirectoryAsync schedules the next attempt itself on TCP failure
    }

    // ── Keep-alive (VERSION every 30 s) ────────────────────────────────────────

    private void StartKeepAlive()
    {
        StopKeepAlive();
        _keepAliveCts = new CancellationTokenSource();
        _ = KeepAliveLoopAsync(_keepAliveCts.Token);
    }

    private void StopKeepAlive()
    {
        _keepAliveCts?.Cancel();
        _keepAliveCts = null;
    }

    private async Task KeepAliveLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (Connection?.IsConnected == true)
                {
                    AppendDebug(">> VERSION (keepalive)");
                    await Connection.SendRawAsync("VERSION");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string NormalizeChannel(string input)
    {
        if (input.StartsWith("%#")) return input;
        if (input.StartsWith('#'))  return "%" + input;
        return "%#" + input;
    }

    private void HandleCtcp(string fromNick, string rawCtcp)
    {
        var inner = rawCtcp[1..^1];  // strip \x01
        if (inner == "VERSION" && _settings.CtcpVersionReply)
        {
            _ = Connection?.SendRawAsync($"NOTICE {fromNick} :\x01VERSION Irc7m 1.0\x01");
            AppendDebug($"CTCP VERSION reply → {fromNick}");
        }
        else if (inner == "TIME" && _settings.CtcpTimeReply)
        {
            var t = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy");
            _ = Connection?.SendRawAsync($"NOTICE {fromNick} :\x01TIME {t}\x01");
            AppendDebug($"CTCP TIME reply → {fromNick}");
        }
        else if (inner.StartsWith("ACTION "))
        {
            AppendLine($"* {fromNick} {inner[7..]}");
        }
    }
}

