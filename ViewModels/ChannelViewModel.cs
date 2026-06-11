using System.Collections.ObjectModel;
using System.Windows.Input;
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

    // ── WHOIS state ────────────────────────────────────────────────────────────
    private string? _pendingWhoisNick;
    private readonly Dictionary<string, (string user, string host, string server)>
        _whoisCache = new(StringComparer.OrdinalIgnoreCase);

    public string Channel { get; }
    public string Host    { get; }
    public int    Port    { get; }

    public ObservableCollection<NickInfo> Nicks { get; } = new();

    private NickInfo? _selectedNick;
    public NickInfo? SelectedNick
    {
        get => _selectedNick;
        set
        {
            if (_selectedNick != value)
            {
                // Clear previous selection
                if (_selectedNick != null)
                    _selectedNick.IsSelected = false;
                
                _selectedNick = value;
                
                // Set new selection
                if (_selectedNick != null)
                    _selectedNick.IsSelected = true;
                
                OnPropertyChanged();
            }
        }
    }

    // ── Nicklist context-menu commands ─────────────────────────────────────────

    public ICommand SelectNickCommand     { get; }
    public ICommand ViewProfileCommand    { get; }
    public ICommand WhisperNickCommand    { get; }
    public ICommand IdentNickCommand      { get; }
    public ICommand KickProfanityCommand  { get; }
    public ICommand KickFloodingCommand   { get; }
    public ICommand KickOtherCommand      { get; }
    public ICommand SetOwnerCommand       { get; }
    public ICommand SetHostCommand        { get; }
    public ICommand SetParticipantCommand { get; }

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

        // ── Wire nicklist commands ─────────────────────────────────────────────

        SelectNickCommand = new Command<NickInfo>(nick =>
        {
            SelectedNick = nick;
        });

        ViewProfileCommand = new Command<NickInfo>(_ =>
            AppendLine("* View Profile: coming soon."));

        WhisperNickCommand = new Command<NickInfo>(nick =>
        {
            _mainVm.PrivateMessages.StartConversation(CleanNick(nick.Nick), Connection);
            _mainVm.SelectedTab = _mainVm.PrivateMessages;
        });

        IdentNickCommand = new Command<NickInfo>(nick =>
        {
            if (_whoisCache.TryGetValue(nick.Nick, out var cached))
            {
                AppendLine($"* Ident: {nick.Nick}!{cached.user}@{cached.host}${cached.server}");
                return;
            }
            var bare = CleanNick(nick.Nick);
            _pendingWhoisNick = bare;
            _ = Connection?.SendRawAsync($"WHOIS {bare}");
            AppendLine($"* Requesting WHOIS for {bare}…");
        });

        KickProfanityCommand = new Command<NickInfo>(nick =>
            _ = Connection?.SendRawAsync($"KICK {Channel} {CleanNick(nick.Nick)} :Profanity"));

        KickFloodingCommand = new Command<NickInfo>(nick =>
            _ = Connection?.SendRawAsync($"KICK {Channel} {CleanNick(nick.Nick)} :Flooding"));

        KickOtherCommand = new Command<NickInfo>(async nick =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null) return;
            var reason = await page.DisplayPromptAsync(
                "Kick", $"Reason for kicking {nick.Nick}:", placeholder: "reason");
            if (!string.IsNullOrWhiteSpace(reason))
                _ = Connection?.SendRawAsync($"KICK {Channel} {CleanNick(nick.Nick)} :{reason.Trim()}");
        });

        SetOwnerCommand = new Command<NickInfo>(nick =>
            _ = Connection?.SendRawAsync($"MODE {Channel} +q {CleanNick(nick.Nick)}"));

        SetHostCommand = new Command<NickInfo>(nick =>
            _ = Connection?.SendRawAsync($"MODE {Channel} +o {CleanNick(nick.Nick)}"));

        SetParticipantCommand = new Command<NickInfo>(nick =>
        {
            // Strip all elevated modes: -qa (owner+admin), then -o (op), then -v (voice)
            var b = CleanNick(nick.Nick);
            _ = Connection?.SendRawAsync($"MODE {Channel} -qaov {b} {b} {b} {b}");
        });
    }

    private static string CleanNick(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw ?? "";
        int i = 0;
        // Remove known mode/status prefixes and leading dots
        while (i < raw.Length && (raw[i] == '@' || raw[i] == '+' || raw[i] == '~' || raw[i] == '&' || raw[i] == '%' || raw[i] == '.'))
            i++;
        return i > 0 ? raw[i..] : raw;
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

    /// <summary>Parts the channel (called from Rooms > Part menu).</summary>
    public void HandlePartCommand(string reason = "Leaving")
    {
        _ = Connection?.SendRawAsync($"PART {Channel} :{reason}");
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

        // If this is a numeric server reply (three-digit), mirror the Directory Server
        // formatting by showing a bracketed code line before any further handling.
        // Skip certain replies that have their own human-friendly handlers (WHOIS
        // replies, NAMES end markers, etc.) to avoid duplication.
        if (!string.IsNullOrEmpty(msg.Command) && msg.Command.Length == 3 && int.TryParse(msg.Command, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var _code))
        {
            // Codes handled elsewhere that we don't want duplicated here
            var skip = msg.Command is "311" or "312" or "314" or "318" or "353" or "366" or "613";
            if (!skip && !string.IsNullOrEmpty(msg.Trailing))
            {
                AppendLine($"[{msg.Command}] {msg.Trailing}");
            }
        }

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

                // CTCP handling
                if (text.StartsWith("\x01") && text.EndsWith("\x01"))
                {
                    HandleCtcp(nick, text);
                    break;
                }

                if (target.Equals(Channel, StringComparison.OrdinalIgnoreCase))
                    AppendLine($"<{nick}> {text}");
                else if (target.Equals(_settings.Nick, StringComparison.OrdinalIgnoreCase))
                    _mainVm.PrivateMessages.ReceiveMessage(nick, text, Connection);
                break;

            case "WHISPER":
            {
                var wNick = msg.PrefixNick ?? msg.Prefix ?? "";
                var wText = msg.Trailing ?? "";
                _mainVm.PrivateMessages.ReceiveMessage(wNick, wText, Connection, isWhisper: true);
                break;
            }

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

            case "401": // ERR_NOSUCHNICK
            {
                // params: [me, target]
                var tgt = msg.Params.Length > 1 ? msg.Params[1] : msg.Trailing ?? "";
                AppendLine($"* No such nick/channel: {tgt}");
                break;
            }

            case "403": // ERR_NOSUCHCHANNEL
            {
                var ch = msg.Params.Length > 1 ? msg.Params[1] : msg.Trailing ?? "";
                AppendLine($"* No such channel: {ch}");
                break;
            }

            case "404": // ERR_CANNOTSENDTOCHAN
            {
                var ch = msg.Params.Length > 1 ? msg.Params[1] : msg.Trailing ?? "";
                AppendLine($"* Cannot send to channel: {ch}");
                break;
            }

            case "482": // ERR_CHANOPRIVSNEEDED
            {
                var ch = msg.Params.Length > 1 ? msg.Params[1] : (msg.Params.Length > 0 ? msg.Params[0] : "");
                AppendLine($"* You're not channel operator{(string.IsNullOrEmpty(ch) ? "" : $" for {ch}")}");
                break;
            }

            // ── WHOIS replies ──────────────────────────────────────────────────

            case "311": // RPL_WHOISUSER  :server 311 me nick user host * :realname
                if (msg.Params.Length >= 4)
                {
                    var wn = msg.Params[1];
                    _whoisCache[wn] = (msg.Params[2], msg.Params[3], "");
                }
                break;

            case "312": // RPL_WHOISSERVER  :server 312 me nick server :info
                if (msg.Params.Length >= 3 &&
                    _whoisCache.TryGetValue(msg.Params[1], out var wds))
                    _whoisCache[msg.Params[1]] = (wds.user, wds.host, msg.Params[2]);
                break;

            case "314": // RPL_WHOWASUSER – treat same as 311
                if (msg.Params.Length >= 4)
                    _whoisCache[msg.Params[1]] = (msg.Params[2], msg.Params[3], "");
                break;

            case "318": // RPL_ENDOFWHOIS
            {
                var wNick = msg.Params.Length > 1 ? msg.Params[1] : _pendingWhoisNick ?? "";
                if (_pendingWhoisNick is not null &&
                    _whoisCache.TryGetValue(wNick, out var wi))
                {
                    var identStr = $"{wNick}!{wi.user}@{wi.host}" +
                                   (wi.server.Length > 0 ? $"${wi.server}" : "");
                    AppendLine($"* Ident: {identStr}");

                    // Update the NickInfo entry so IdentString works in bindings too
                    var ni = Nicks.FirstOrDefault(n =>
                        n.Nick.Equals(wNick, StringComparison.OrdinalIgnoreCase));
                    if (ni is not null)
                    {
                        ni.UserName = wi.user;
                        ni.HostName = wi.host;
                        ni.Server   = wi.server;
                    }
                    _pendingWhoisNick = null;
                }
                break;
            }
        }
    }

    // ── Nick list helpers ──────────────────────────────────────────────────────

    private void RemoveNick(string nick)
    {
        var found = Nicks.FirstOrDefault(n =>
            n.Nick.Equals(nick, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
        {
            // Clear selection if removing the selected nick
            if (SelectedNick == found)
                SelectedNick = null;
            Nicks.Remove(found);
        }
    }

    private void RenameNick(string oldNick, string newNick)
    {
        var found = Nicks.FirstOrDefault(n =>
            n.Nick.Equals(oldNick, StringComparison.OrdinalIgnoreCase));
        if (found is null) return;

        var idx = Nicks.IndexOf(found);
        var wasSelected = SelectedNick == found;
        
        Nicks.Remove(found);
        var newNickInfo = new NickInfo { ModePrefix = found.ModePrefix, Nick = newNick };
        Nicks.Insert(idx, newNickInfo);
        
        // Preserve selection if the renamed nick was selected
        if (wasSelected)
            SelectedNick = newNickInfo;
    }

    private void SortNicks()
    {
        var currentSelected = SelectedNick;
        var sorted = Nicks
            .OrderBy(n => n.ModePrefix switch { '@' => 0, '~' => 0, '&' => 1, '%' => 2, '+' => 3, _ => 4 })
            .ThenBy(n => n.Nick, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Nicks.Clear();
        foreach (var n in sorted) Nicks.Add(n);
        
        // Restore selection if it still exists in the list
        if (currentSelected is not null && sorted.Contains(currentSelected))
            SelectedNick = currentSelected;
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

    // ── CTCP ──────────────────────────────────────────────────────────────────

    private void HandleCtcp(string fromNick, string rawCtcp)
    {
        var inner = rawCtcp[1..^1];  // strip \x01 delimiters

        if (inner.StartsWith("ACTION "))
        {
            AppendLine($"* {fromNick} {inner[7..]}");
            return;
        }

        if (inner == "VERSION" && _settings.CtcpVersionReply)
        {
            _ = Connection?.SendRawAsync($"NOTICE {fromNick} :\x01VERSION Irc7m 1.0\x01");
            AppendDebug($"CTCP VERSION reply → {fromNick}");
            return;
        }

        if (inner == "TIME" && _settings.CtcpTimeReply)
        {
            var t = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy");
            _ = Connection?.SendRawAsync($"NOTICE {fromNick} :\x01TIME {t}\x01");
            AppendDebug($"CTCP TIME reply → {fromNick}");
        }
    }
}



