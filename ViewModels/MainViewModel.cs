using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Irc7m.Models;
using Irc7m.Services;

namespace Irc7m.ViewModels;

/// <summary>
/// Root ViewModel: owns the tab collection and drives the content area.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private ChatWindowViewModel? _selectedTab;
    private readonly IrcClientSettings _settings;

    public ObservableCollection<ChatWindowViewModel> Tabs { get; } = new();

    /// <summary>The permanent private-messages tab.</summary>
    public PrivateMessagesViewModel PrivateMessages { get; private set; } = null!;

    public ChatWindowViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab is not null) _selectedTab.IsSelected = false;
            _selectedTab = value;
            if (_selectedTab is not null) _selectedTab.IsSelected = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDirectoryServerConnected));
            OnPropertyChanged(nameof(ConnectDisconnectLabel));
        }
    }

    /// <summary>True when the Directory Server tab has an active connection.</summary>
    public bool IsDirectoryServerConnected =>
        Tabs.OfType<DirectoryServerViewModel>().FirstOrDefault()?.IsConnected ?? false;

    /// <summary>Dynamic label for the File > Connect/Disconnect menu item.</summary>
    public string ConnectDisconnectLabel =>
        IsDirectoryServerConnected ? "Disconnect" : "Connect…";

    // ── Commands ───────────────────────────────────────────────────────────────

    public ICommand SelectTabCommand        { get; }
    public ICommand CloseTabCommand         { get; }
    public ICommand ConnectDisconnectCommand{ get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    public MainViewModel(IrcClientSettings settings)
    {
        _settings               = settings;
        SelectTabCommand        = new Command<ChatWindowViewModel>(vm => SelectedTab = vm);
        CloseTabCommand         = new Command<ChatWindowViewModel>(CloseTab);
        ConnectDisconnectCommand= new Command(OnConnectDisconnect);
    }

    // ── Initialise ─────────────────────────────────────────────────────────────

    public void Initialize()
    {
        var dirServer = new DirectoryServerViewModel(_settings, this);
        dirServer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DirectoryServerViewModel.IsConnected))
            {
                OnPropertyChanged(nameof(IsDirectoryServerConnected));
                OnPropertyChanged(nameof(ConnectDisconnectLabel));
            }
        };
        AddTab(dirServer);

        PrivateMessages = new PrivateMessagesViewModel(_settings);
        AddTab(PrivateMessages);

        // Go back to DS as default selected
        SelectedTab = dirServer;
    }

    // ── Tab management ─────────────────────────────────────────────────────────

    public void AddTab(ChatWindowViewModel vm)
    {
        Tabs.Add(vm);
        SelectedTab = vm;
    }

    public void CloseTab(ChatWindowViewModel vm)
    {
        if (vm is DirectoryServerViewModel) return;   // permanent tab
        if (vm is PrivateMessagesViewModel)  return;   // permanent tab
        if (!Tabs.Contains(vm)) return;

        var idx = Tabs.IndexOf(vm);
        Tabs.Remove(vm);

        if (SelectedTab == vm)
            SelectedTab = Tabs.Count > 0 ? Tabs[Math.Max(0, idx - 1)] : null;

        // Disconnect on close
        (vm as ChannelViewModel)?.Disconnect();
    }

    /// <summary>
    /// Called by channel windows when the user types /join – forwards to the Directory Server.
    /// </summary>
    public void JoinChannel(string channel)
    {
        var ds = Tabs.OfType<DirectoryServerViewModel>().FirstOrDefault();
        if (ds is null) return;
        ds.RequestJoin(channel);
        SelectedTab = ds;
    }

    /// <summary>Opens (or focuses) a ChannelViewModel for the given server + channel.</summary>
    public void OpenChannel(string channel, string host, int port)
    {
        var existing = Tabs
            .OfType<ChannelViewModel>()
            .FirstOrDefault(c =>
                c.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase) &&
                c.Host == host && c.Port == port);

        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var vm = new ChannelViewModel(channel, host, port, _settings, this);
        AddTab(vm);
        _ = vm.ConnectAsync();

        _settings.AddRecentChannel(channel);
    }

    // ── Connect / Disconnect ───────────────────────────────────────────────────

    private void OnConnectDisconnect()
    {
        var ds = Tabs.OfType<DirectoryServerViewModel>().FirstOrDefault();
        if (ds is null) return;

        if (ds.IsConnected)
        {
            ds.IntentionalDisconnect();
            return;
        }

        // Determine which server to connect to (priority order):
        //  1. Last host used this session
        //  2. Most recently saved server in settings
        //  3. First registered server (e.g. dir.irc7.com)
        //  4. Fall back – just focus the DS tab so user can type /connect
        var host = ds.LastHost
                   ?? _settings.RecentServers.FirstOrDefault()?.Host
                   ?? _settings.RegisteredServers.FirstOrDefault()?.Host;

        var port = (ds.LastPort > 0 ? (int?)ds.LastPort : null)
                   ?? _settings.RecentServers.FirstOrDefault()?.Port
                   ?? _settings.RegisteredServers.FirstOrDefault()?.Port
                   ?? 6667;

        SelectedTab = ds;

        if (host is not null)
            ds.HandleConnectCommand(host, port);
        // else: user sees the DS tab and can type /connect manually
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

