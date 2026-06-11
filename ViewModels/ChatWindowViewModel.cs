using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using Irc7m.Services;

namespace Irc7m.ViewModels;

public enum IrcConnectionState { None, Connecting, Connected, Reconnecting, Disconnected }

/// <summary>
/// Base ViewModel for every IRC window tab.
/// </summary>
public class ChatWindowViewModel : INotifyPropertyChanged
{
    private string _title      = "Window";
    private string _inputText  = "";
    private string _outputText = "";
    private bool   _isSelected;
    private IrcConnectionState _connectionState = IrcConnectionState.None;
    private string _inputPlaceholder = "Type a message or /command…";

    private readonly StringBuilder _outputBuilder = new();

    // ── Input history ──────────────────────────────────────────────────────────
    private readonly List<string> _history    = new();
    private int                   _historyPos = -1;
    private string                _savedInput = "";

    // ── Debug panel ────────────────────────────────────────────────────────────
    private bool          _isDebugMode;
    private string        _debugText       = "";
    private double        _debugPanelWidth = 420;
    private readonly StringBuilder _debugBuilder = new();

    protected IrcConnection? Connection;

    // ── Properties ────────────────────────────────────────────────────────────

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public string OutputText
    {
        get => _outputText;
        private set { _outputText = value; OnPropertyChanged(); }
    }

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public virtual bool IsCloseable => true;

    public string InputPlaceholder
    {
        get => _inputPlaceholder;
        protected set { _inputPlaceholder = value; OnPropertyChanged(); }
    }

    // ── Connection state indicator ─────────────────────────────────────────────

    public IrcConnectionState ConnectionState
    {
        get => _connectionState;
        protected set
        {
            _connectionState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectionIndicator));
            OnPropertyChanged(nameof(ConnectionIndicatorColor));
        }
    }

    /// <summary>Unicode symbol shown in the tab (● or ⚡).</summary>
    public string ConnectionIndicator => _connectionState switch
    {
        IrcConnectionState.Connected    => "●",
        IrcConnectionState.Disconnected => "●",
        IrcConnectionState.Connecting   => "⚡",
        IrcConnectionState.Reconnecting => "⚡",
        _                               => ""
    };

    /// <summary>Colour for the indicator (green / red / orange / transparent).</summary>
    public Color ConnectionIndicatorColor => _connectionState switch
    {
        IrcConnectionState.Connected    => Color.FromArgb("#4caf50"),
        IrcConnectionState.Disconnected => Color.FromArgb("#f44336"),
        IrcConnectionState.Connecting   => Color.FromArgb("#ff9800"),
        IrcConnectionState.Reconnecting => Color.FromArgb("#ff9800"),
        _                               => Colors.Transparent
    };

    // ── Debug properties ───────────────────────────────────────────────────────

    public bool IsDebugMode
    {
        get => _isDebugMode;
        set { _isDebugMode = value; OnPropertyChanged(); }
    }

    public string DebugText
    {
        get => _debugText;
        private set { _debugText = value; OnPropertyChanged(); }
    }

    public double DebugPanelWidth
    {
        get => _debugPanelWidth;
        set { _debugPanelWidth = Math.Max(180, value); OnPropertyChanged(); }
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    public ICommand SubmitInputCommand { get; }
    public ICommand HistoryUpCommand   { get; }
    public ICommand HistoryDownCommand { get; }
    public ICommand ToggleDebugCommand { get; }
    public ICommand ClearDebugCommand  { get; }

    public ChatWindowViewModel()
    {
        SubmitInputCommand = new Command(OnSubmitInput);
        HistoryUpCommand   = new Command(HistoryUp);
        HistoryDownCommand = new Command(HistoryDown);
        ToggleDebugCommand = new Command(() => IsDebugMode = !IsDebugMode);
        ClearDebugCommand  = new Command(() => { _debugBuilder.Clear(); DebugText = ""; });
    }

    // ── Debug helpers ──────────────────────────────────────────────────────────

    /// <summary>Appends a raw socket line (prefixed "&gt;&gt; " or "&lt;&lt; ") to the debug log.</summary>
    protected void AppendDebug(string raw)
    {
        var stamp = $"[{DateTime.Now:HH:mm:ss.fff}] {raw}";
        if (_debugBuilder.Length > 0) _debugBuilder.AppendLine();
        _debugBuilder.Append(stamp);
        DebugText = _debugBuilder.ToString();
    }

    // ── Input handling ─────────────────────────────────────────────────────────

    private void OnSubmitInput()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Add to history (skip consecutive duplicates)
        if (_history.Count == 0 || _history[^1] != text)
            _history.Add(text);
        _historyPos = -1;
        _savedInput = "";

        InputText = "";

        if (text.StartsWith('/'))
        {
            var parts = text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb  = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
            var args  = parts.Length > 1 ? parts[1..] : [];
            HandleCommand(verb, args);
        }
        else
        {
            HandleMessage(text);
        }
    }

    // ── History navigation ─────────────────────────────────────────────────────

    private void HistoryUp()
    {
        if (_history.Count == 0) return;
        if (_historyPos == -1)
            _savedInput = InputText;              // save draft before navigating

        _historyPos = Math.Min(_historyPos + 1, _history.Count - 1);
        InputText   = _history[_history.Count - 1 - _historyPos];
    }

    private void HistoryDown()
    {
        if (_historyPos == -1) return;
        _historyPos--;
        InputText = _historyPos == -1 ? _savedInput : _history[_history.Count - 1 - _historyPos];
    }

    protected virtual void HandleCommand(string verb, string[] args)
    {
        switch (verb)
        {
            case "clear":
                _outputBuilder.Clear();
                OutputText = "";
                break;

            case "raw":
                if (Connection is not null && args.Length > 0)
                    _ = Connection.SendRawAsync(string.Join(" ", args));
                else
                    AppendLine("* Not connected or no command specified.");
                break;

            case "nick":
                if (args.Length > 0 && Connection is not null)
                    _ = Connection.SendRawAsync($"NICK {args[0]}");
                else
                    AppendLine("* Usage: /nick <newnick>");
                break;

            default:
                if (Connection?.IsConnected == true)
                {
                    // Unknown command + connected → send straight to server
                    var raw = args.Length > 0
                        ? $"{verb.ToUpperInvariant()} {string.Join(" ", args)}"
                        : verb.ToUpperInvariant();
                    _ = Connection.SendRawAsync(raw);
                }
                else
                {
                    AppendLine($"* Unknown command: /{verb}");
                }
                break;
        }
    }

    /// <summary>Called when user input is NOT a slash-command. Override to send PRIVMSG.</summary>
    protected virtual void HandleMessage(string text)
    {
        AppendLine("* Not connected to a channel.");
    }

    // ── Output ─────────────────────────────────────────────────────────────────

    public void AppendLine(string line)
    {
        var stamped = $"[{DateTime.Now:HH:mm:ss}] {line}";
        if (_outputBuilder.Length > 0)
            _outputBuilder.AppendLine();
        _outputBuilder.Append(stamped);
        OutputText = _outputBuilder.ToString();
    }

    // ── Output helpers (additional) ────────────────────────────────────────────

    /// <summary>Appends a line without adding a timestamp prefix.</summary>
    protected void AppendRawLine(string line)
    {
        if (_outputBuilder.Length > 0) _outputBuilder.AppendLine();
        _outputBuilder.Append(line);
        OutputText = _outputBuilder.ToString();
    }

    /// <summary>Replaces the entire output area with the supplied text.</summary>
    protected void ReplaceOutput(string text)
    {
        _outputBuilder.Clear();
        _outputBuilder.Append(text);
        OutputText = text;
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
