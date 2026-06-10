using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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

    public ChatWindowViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab is not null) _selectedTab.IsSelected = false;
            _selectedTab = value;
            if (_selectedTab is not null) _selectedTab.IsSelected = true;
            OnPropertyChanged();
        }
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    public ICommand SelectTabCommand { get; }
    public ICommand CloseTabCommand  { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    public MainViewModel(IrcClientSettings settings)
    {
        _settings = settings;
        SelectTabCommand = new Command<ChatWindowViewModel>(vm => SelectedTab = vm);
        CloseTabCommand  = new Command<ChatWindowViewModel>(CloseTab);
    }

    // ── Initialise ─────────────────────────────────────────────────────────────

    public void Initialize()
    {
        var dirServer = new DirectoryServerViewModel(_settings, this);
        AddTab(dirServer);
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
        if (ds is null)
        {
            // Fallback: shouldn't happen, but select first tab
            return;
        }

        ds.RequestJoin(channel);
        SelectedTab = ds;   // bring the Directory Server tab to focus
    }
    /// Opens (or focuses) a ChannelViewModel for the given server + channel.
    /// </summary>
    public void OpenChannel(string channel, string host, int port)
    {
        // Bring existing tab to front if already open
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
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

