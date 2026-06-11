using System.Collections.ObjectModel;
using System.Windows.Input;
using Irc7m.Models;
using Irc7m.Services;

namespace Irc7m.ViewModels;

/// <summary>
/// Holds a single conversation with one remote nick via private message.
/// </summary>
public class PrivateConversation
{
    public string           Nick       { get; }
    public IrcConnection?   Connection { get; set; }
    public List<string>     Lines      { get; } = new();
    public int              Unread     { get; set; }

    public PrivateConversation(string nick) => Nick = nick;
}

/// <summary>
/// Permanent "Messages" tab. Collects all PRIVMSG / WHISPER messages sent directly to the
/// local user across all connections and groups them by sender.
/// </summary>
public class PrivateMessagesViewModel : ChatWindowViewModel
{
    private readonly IrcClientSettings _settings;

    private readonly Dictionary<string, PrivateConversation>
        _conversations = new(StringComparer.OrdinalIgnoreCase);

    private NickInfo? _selectedPeer;
    private int       _totalUnread;

    public ObservableCollection<NickInfo> Nicks { get; } = new();

    public ICommand SelectPeerCommand { get; }

    public override bool IsCloseable => false;

    // ── Selected peer ──────────────────────────────────────────────────────────

    public NickInfo? SelectedPeer
    {
        get => _selectedPeer;
        set
        {
            _selectedPeer = value;
            OnPropertyChanged();
            UpdatePlaceholder();

            if (value is null)
            {
                ShowAll();
                return;
            }

            // Clear unread for this peer
            if (_conversations.TryGetValue(value.Nick, out var conv))
            {
                _totalUnread   -= conv.Unread;
                conv.Unread     = 0;
                UpdateTitle();
            }
            ShowPeer(value.Nick);
        }
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    public PrivateMessagesViewModel(IrcClientSettings settings)
    {
        _settings          = settings;
        Title              = "Messages";
        SelectPeerCommand  = new Command<NickInfo>(n => SelectedPeer = n);
        UpdatePlaceholder();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens (or focuses) a conversation with a nick without sending a message.
    /// Called from the Whisper context-menu item.
    /// </summary>
    public void StartConversation(string nick, IrcConnection? connection)
    {
        if (!_conversations.TryGetValue(nick, out var conv))
        {
            conv = new PrivateConversation(nick) { Connection = connection };
            _conversations[nick] = conv;
            Nicks.Add(new NickInfo { Nick = nick });
        }
        else if (connection is not null)
        {
            conv.Connection = connection;
        }

        // Select the peer so the user can start typing immediately
        SelectedPeer = Nicks.FirstOrDefault(n =>
            n.Nick.Equals(nick, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Called by channel/DS view-models when a PRIVMSG or WHISPER addressed to us arrives.</summary>
    public void ReceiveMessage(string fromNick, string text,
                               IrcConnection? connection, bool isWhisper = false)
    {
        if (!_conversations.TryGetValue(fromNick, out var conv))
        {
            conv = new PrivateConversation(fromNick) { Connection = connection };
            _conversations[fromNick] = conv;
            Nicks.Add(new NickInfo { Nick = fromNick });
        }
        else if (connection is not null)
        {
            conv.Connection = connection;
        }

        var prefix = isWhisper ? "~" : "";
        var line   = $"[{DateTime.Now:HH:mm:ss}] {prefix}<{fromNick}> {text}";
        conv.Lines.Add(line);

        // If this peer is currently selected, append immediately; otherwise increment badge
        if (_selectedPeer?.Nick.Equals(fromNick, StringComparison.OrdinalIgnoreCase) == true)
        {
            AppendRawLine(line);
        }
        else
        {
            conv.Unread++;
            _totalUnread++;
            UpdateTitle();
        }
    }

    // ── Outbound message ───────────────────────────────────────────────────────

    protected override void HandleMessage(string text)
    {
        if (_selectedPeer is null)
        {
            AppendLine("* Select a user from the right panel to send a message.");
            return;
        }

        var nick = _selectedPeer.Nick;
        if (!_conversations.TryGetValue(nick, out var conv) ||
            conv.Connection?.IsConnected != true)
        {
            AppendLine($"* Not connected – cannot send message to {nick}.");
            return;
        }

        _ = conv.Connection.SendRawAsync($"PRIVMSG {nick} :{text}");
        var line = $"[{DateTime.Now:HH:mm:ss}] <{_settings.Nick}> {text}";
        conv.Lines.Add(line);
        AppendRawLine(line);
    }

    // ── Display helpers ────────────────────────────────────────────────────────

    private void ShowPeer(string nick)
    {
        if (!_conversations.TryGetValue(nick, out var conv)) return;
        ReplaceOutput(string.Join(Environment.NewLine, conv.Lines));
    }

    private void ShowAll()
    {
        var all = _conversations.Values
            .SelectMany(c => c.Lines)
            .ToList();
        ReplaceOutput(string.Join(Environment.NewLine, all));
    }

    private void UpdateTitle()
        => Title = _totalUnread > 0 ? $"Messages ({_totalUnread})" : "Messages";

    private void UpdatePlaceholder()
        => InputPlaceholder = _selectedPeer is null
            ? "Select a user from the list to reply…"
            : $"Message {_selectedPeer.Nick}…";
}


