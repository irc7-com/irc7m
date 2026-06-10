using System.Collections.ObjectModel;
using Irc7m.Models;
using Irc7m.Services;

namespace Irc7m.ViewModels;

/// <summary>
/// ViewModel for an IRC channel window.
/// Connects to a chat server, sends JOIN, handles 353 nick list, PRIVMSG, NICK, PART etc.
/// </summary>
public class ChannelViewModel : ChatWindowViewModel
{
    private readonly IrcClientSettings _settings;
    private readonly MainViewModel     _mainVm;

    private bool _receivingNames;

    // ── Reconnect state ────────────────────────────────────────────────────────
    private bool _intentionalDisconnect;
    private int  _reconnectAttempt;

    public string Channel { get; }
    public string Host    { get; }
    public int    Port    { get; }

    public ObservableCollection<NickInfo> Nicks { get; } = new();

    // ── Constructor ────────────────────────────────────────────────────────────

    public ChannelViewModel(string channel, string host, int port,
                            IrcClientSettings settings, MainViewModel mainVm)
    {
        Channel   = channel;
        Host      = host;
        Port      = port;
        _settings = settings;
        _mainVm   = mainVm;
        Title     = channel;
    }

    // ── Connection lifecycle ───────────────────────────────────────────────────

    public async Task ConnectAsync()
    {
        _intentionalDisconnect = false;
        ConnectionState        = IrcConnectionState.Connecting;
        try
        {
            AppendDebug($"⚡ Connecting to {Host}:{Port}\u2026");
            var conn = new IrcConnection();
            conn.MessageReceived += OnMessageReceived;
            conn.Disconnected    += OnDisconnected;
            conn.RawLineReceived += (_, raw) => AppendDebug(raw);
            Connection = conn;

            await conn.ConnectAsync(Host, Port);
            await conn.SendRawAsync($"NICK {_settings.Nick}");
            await conn.SendRawAsync($"USER {_settings.UserName} 0 * :{_settings.RealName}");
        }
        catch (Exception ex)
        {
            AppendDebug($"⚡ Connection failed: {ex.Message}");
            ConnectionState = IrcConnectionState.Disconnected;
            Connection      = null;

            // TCP connect failed – OnDisconnected won't fire, reschedule here
            if (!_intentionalDisconnect)
                _ = ScheduleReconnectAsync();
        }
    }

    public void Disconnect()
    {
        _intentionalDisconnect = true;
        Connection?.Disconnect();
        Connection = null;
    }

    // ── Command handling ───────────────────────────────────────────────────────

    protected override void HandleCommand(string verb, string[] args)
    {
        switch (verb)
        {
            case "join":
                // Forward /join to the Directory Server tab (same as typing it there)
                if (args.Length < 1) { AppendLine("* Usage: /join <channel>"); return; }
                _mainVm.JoinChannel(args[0]);
                break;

            case "part":
                var partMsg = args.Length > 0 ? string.Join(" ", args) : "Leaving";
                _ = Connection?.SendRawAsync($"PART {Channel} :{partMsg}");
                break;

            case "msg":
                if (args.Length < 2) { AppendLine("* Usage: /msg <target> <message>"); return; }
                _ = Connection?.SendRawAsync($"PRIVMSG {args[0]} :{string.Join(" ", args[1..])}");
                break;

            case "topic":
                if (args.Length < 1) { AppendLine("* Usage: /topic <text>"); return; }
                _ = Connection?.SendRawAsync($"TOPIC {Channel} :{string.Join(" ", args)}");
                break;

            case "me":
                if (args.Length < 1) { AppendLine("* Usage: /me <action>"); return; }
                var action = string.Join(" ", args);
                _ = Connection?.SendRawAsync($"PRIVMSG {Channel} :\x01ACTION {action}\x01");
                AppendLine($"* {_settings.Nick} {action}");
                break;

            case "names":
                _ = Connection?.SendRawAsync($"NAMES {Channel}");
                break;

            case "kick":
                if (args.Length < 1) { AppendLine("* Usage: /kick <nick> [reason]"); return; }
                var reason = args.Length > 1 ? string.Join(" ", args[1..]) : "Kicked";
                _ = Connection?.SendRawAsync($"KICK {Channel} {args[0]} :{reason}");
                break;

            default:
                base.HandleCommand(verb, args);
                break;
        }
    }

    protected override void HandleMessage(string text)
    {
        if (Connection?.IsConnected == true)
        {
            _ = Connection.SendRawAsync($"PRIVMSG {Channel} :{text}");
            AppendLine($"<{_settings.Nick}> {text}");
        }
        else
        {
            AppendLine("* Not connected.");
        }
    }

    // ── Inbound message handling ───────────────────────────────────────────────

    private void OnMessageReceived(object? sender, IrcMessage msg)
    {
        var nick = msg.PrefixNick ?? msg.Prefix ?? "";

        switch (msg.Command)
        {
            case "001": // RPL_WELCOME – now send JOIN
                _reconnectAttempt = 0;
                ConnectionState   = IrcConnectionState.Connected;
                AppendLine($"* Connected to {Host} as {_settings.Nick}");
                _ = Connection!.SendRawAsync($"JOIN {Channel}");
                break;

            case "353": // RPL_NAMREPLY  :server 353 me = #channel :@nick1 nick2 …
                if (!_receivingNames)
                {
                    _receivingNames = true;
                    Nicks.Clear();
                }
                if (msg.Trailing is not null)
                    foreach (var raw in msg.Trailing.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        Nicks.Add(NickInfo.FromRaw(raw));
                break;

            case "366": // RPL_ENDOFNAMES – sort list
                _receivingNames = false;
                SortNicks();
                break;

            case "JOIN":
                if (nick == _settings.Nick)
                    AppendLine($"* You have joined {Channel}");
                else
                {
                    Nicks.Add(new NickInfo { Nick = nick });
                    AppendLine($"* {nick} has joined {Channel}");
                }
                break;

            case "PART":
                RemoveNick(nick);
                AppendLine($"* {nick} has left {Channel} ({msg.Trailing ?? ""})");
                break;

            case "QUIT":
                RemoveNick(nick);
                AppendLine($"* {nick} has quit ({msg.Trailing ?? ""})");
                break;

            case "KICK":
                var kicked = msg.Params.Length > 1 ? msg.Params[1] : "";
                RemoveNick(kicked);
                AppendLine($"* {nick} has kicked {kicked} ({msg.Trailing ?? ""})");
                if (kicked.Equals(_settings.Nick, StringComparison.OrdinalIgnoreCase))
                    AppendLine("* You have been kicked.");
                break;

            case "NICK":
                var newNick = msg.Trailing ?? (msg.Params.Length > 0 ? msg.Params[0] : "");
                RenameNick(nick, newNick);
                AppendLine($"* {nick} is now known as {newNick}");
                if (nick.Equals(_settings.Nick, StringComparison.OrdinalIgnoreCase))
                    _settings.Nick = newNick;
                break;

            case "PRIVMSG":
                var target = msg.Params.Length > 0 ? msg.Params[0] : "";
                var text   = msg.Trailing ?? "";

                // CTCP ACTION
                if (text.StartsWith("\x01ACTION") && text.EndsWith("\x01"))
                {
                    var act = text[8..^1];
                    AppendLine($"* {nick} {act}");
                    break;
                }

                if (target.Equals(Channel, StringComparison.OrdinalIgnoreCase))
                    AppendLine($"<{nick}> {text}");
                else if (target.Equals(_settings.Nick, StringComparison.OrdinalIgnoreCase))
                    AppendLine($"->{nick}<- {text}");
                break;

            case "332": // RPL_TOPIC on join
                Title = $"{Channel} | {msg.Trailing}";
                AppendLine($"* Topic: {msg.Trailing}");
                break;

            case "TOPIC": // topic change live
                Title = $"{Channel} | {msg.Trailing}";
                AppendLine($"* {nick} changed topic to: {msg.Trailing}");
                break;

            case "MODE":
                AppendLine($"* Mode change: {string.Join(" ", msg.Params)} {msg.Trailing ?? ""}".TrimEnd());
                break;

            case "NOTICE":
                AppendLine($"* Notice from {nick}: {msg.Trailing}");
                break;

            case "372": // MOTD
            case "375":
                AppendLine(msg.Trailing ?? "");
                break;

            case "433": // ERR_NICKNAMEINUSE
                AppendLine($"* Nick is already in use.");
                break;
        }
    }

    // ── Nick list helpers ──────────────────────────────────────────────────────

    private void RemoveNick(string nick)
    {
        var found = Nicks.FirstOrDefault(n =>
            n.Nick.Equals(nick, StringComparison.OrdinalIgnoreCase));
        if (found is not null) Nicks.Remove(found);
    }

    private void RenameNick(string oldNick, string newNick)
    {
        var found = Nicks.FirstOrDefault(n =>
            n.Nick.Equals(oldNick, StringComparison.OrdinalIgnoreCase));
        if (found is null) return;

        var idx = Nicks.IndexOf(found);
        Nicks.Remove(found);
        Nicks.Insert(idx, new NickInfo { ModePrefix = found.ModePrefix, Nick = newNick });
    }

    private void SortNicks()
    {
        var sorted = Nicks
            .OrderBy(n => n.ModePrefix switch { '@' => 0, '~' => 0, '&' => 1, '%' => 2, '+' => 3, _ => 4 })
            .ThenBy(n => n.Nick, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Nicks.Clear();
        foreach (var n in sorted) Nicks.Add(n);
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        ConnectionState = IrcConnectionState.Disconnected;
        AppendDebug("⚡ Disconnected from server.");

        if (!_intentionalDisconnect)
            _ = ScheduleReconnectAsync();
    }

    private static readonly int[] ReconnectDelays = { 5, 10, 20, 40, 60 };

    private async Task ScheduleReconnectAsync()
    {
        _reconnectAttempt++;
        const int delay = 5;

        ConnectionState = IrcConnectionState.Reconnecting;
        AppendDebug($"⚡ Reconnect attempt {_reconnectAttempt} in {delay}s\u2026");

        await Task.Delay(TimeSpan.FromSeconds(delay));

        if (Connection?.IsConnected == true || _intentionalDisconnect) return;

        Nicks.Clear();
        await ConnectAsync();
        // ConnectAsync schedules the next attempt itself on TCP failure
    }
}



