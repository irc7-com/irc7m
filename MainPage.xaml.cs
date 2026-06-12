using Irc7m.Services;
using Irc7m.ViewModels;
using Irc7m.Views;

namespace Irc7m;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel      _mainVm;
    private readonly IrcClientSettings  _settings;
    private readonly ScriptEngine       _scriptEngine;

    // Cache of VM → View so we don't recreate on every tab switch
    private readonly Dictionary<ChatWindowViewModel, View> _viewCache = new();

    // Track which dropdown is open (null = closed)
    private Label? _activeMenuLabel;

    public MainPage(MainViewModel mainVm, IrcClientSettings settings, ScriptEngine scriptEngine)
    {
        _mainVm       = mainVm;
        _settings     = settings;
        _scriptEngine = scriptEngine;

        InitializeComponent();
        BindingContext = mainVm;

        mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
                UpdateContent(mainVm.SelectedTab);
            if (e.PropertyName == nameof(MainViewModel.ConnectDisconnectLabel))
                ConnectMenuItem.Text = mainVm.ConnectDisconnectLabel;
        };

        mainVm.Tabs.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is null) return;
            foreach (ChatWindowViewModel vm in e.OldItems)
                _viewCache.Remove(vm);
        };

        mainVm.Initialize();
        ConnectMenuItem.Text  = mainVm.ConnectDisconnectLabel;
        CtcpTimeMenuItem.Text = CtcpTimeLabel();

        // Dismiss dropdown on content area tap
        ContentArea.GestureRecognizers.Add(
            new TapGestureRecognizer { Command = new Command(CloseDropdown) });
    }

    // ── Content switching ──────────────────────────────────────────────────────

    private void UpdateContent(ChatWindowViewModel? vm)
    {
        CloseDropdown();
        if (vm is null) { ContentArea.Content = null; return; }

        if (!_viewCache.TryGetValue(vm, out var view))
        {
            view = vm switch
            {
                DirectoryServerViewModel dsVm  => new DirectoryServerWindowView { BindingContext = dsVm  },
                ChannelViewModel         cVm   => new ChannelWindowView         { BindingContext = cVm   },
                PrivateMessagesViewModel pmVm  => new PrivateMessagesView       { BindingContext = pmVm  },
                _                             => new ChatWindowView             { BindingContext = vm    }
            };
            _viewCache[vm] = view;
        }

        ContentArea.Content = view;

        switch (view)
        {
            case DirectoryServerWindowView dsv: dsv.FocusInput(); break;
            case ChannelWindowView         cwv: cwv.FocusInput(); break;
            case PrivateMessagesView        pmv: pmv.FocusInput(); break;
            case ChatWindowView             cv:  cv.FocusInput();  break;
        }
    }

    // ── In-window dropdown menu system ─────────────────────────────────────────

    private void OnFileMenuTapped(object? sender, TappedEventArgs e)
        => ToggleDropdown(FileMenuLabel, BuildFileMenu());

    private void OnEditMenuTapped(object? sender, TappedEventArgs e)
        => ToggleDropdown(EditMenuLabel, BuildEditMenu());

    private void OnViewMenuTapped(object? sender, TappedEventArgs e)
        => ToggleDropdown(ViewMenuLabel, BuildViewMenu());

    private void OnRoomsMenuTapped(object? sender, TappedEventArgs e)
        => ToggleDropdown(RoomsMenuLabel, BuildRoomsMenu());

    private void OnHelpMenuTapped(object? sender, TappedEventArgs e)
        => ToggleDropdown(HelpMenuLabel, BuildHelpMenu());

    private void ToggleDropdown(Label menuLabel, IEnumerable<(string text, bool isSep, Action? action)> items)
    {
        // Clicking the same label a second time closes
        if (_activeMenuLabel == menuLabel)
        {
            CloseDropdown();
            return;
        }

        _activeMenuLabel          = menuLabel;
        menuLabel.BackgroundColor = Color.FromArgb("#3c3c3c");

        // menuLabel.X is relative to the HorizontalStackLayout (its parent).
        // The HStackLayout has Padding="4,0", so content starts 4px in.
        // MenuBarGrid and the HStackLayout are both at X=0 in the outer Grid.
        DropdownPanel.TranslationX = menuLabel.X + 4;
        DropdownPanel.TranslationY = 30;   // below the 30px menu bar row

        DropdownItems.Children.Clear();
        foreach (var (text, isSep, action) in items)
        {
            if (isSep)
            {
                DropdownItems.Children.Add(new BoxView
                {
                    Color         = Color.FromArgb("#555555"),
                    HeightRequest = 1,
                    Margin        = new Thickness(0, 2)
                });
            }
            else
            {
                var captured = action;
                var row      = new Label
                {
                    Text            = text,
                    TextColor       = Color.FromArgb("#e0e0e0"),
                    FontSize        = 13,
                    Padding         = new Thickness(16, 7),
                    BackgroundColor = Colors.Transparent,
                    MinimumWidthRequest = 200
                };
                row.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(() =>
                    {
                        CloseDropdown();
                        captured?.Invoke();
                    })
                });
                DropdownItems.Children.Add(row);
            }
        }

        DropdownPanel.IsVisible = true;
    }

    private void CloseDropdown()
    {
        DropdownPanel.IsVisible = false;
        if (_activeMenuLabel is not null)
        {
            _activeMenuLabel.BackgroundColor = Colors.Transparent;
            _activeMenuLabel = null;
        }
    }

    // ── Menu item builders ──────────────────────────────────────────────────────

    private IEnumerable<(string, bool, Action?)> BuildFileMenu()
    {
        var label = _mainVm.ConnectDisconnectLabel;
        yield return (label,         false, () => OnConnectDisconnectClicked(null, EventArgs.Empty));
        yield return ("---",         true,  null);
        yield return ("Preferences…",false, () => OnPreferencesClicked(null, EventArgs.Empty));
        yield return ("---",         true,  null);
        yield return ("Servers…",    false, () => OnServersClicked(null, EventArgs.Empty));
        yield return ("---",         true,  null);
        yield return ("Script Editor…",false,() => OnScriptEditorClicked(null, EventArgs.Empty));
        yield return ("---",         true,  null);
        yield return ("Quit",        false, () => Application.Current?.Quit());
    }

    private IEnumerable<(string, bool, Action?)> BuildEditMenu()
    {
        yield return ("Cut",        false, () => OnCutClicked(null, EventArgs.Empty));
        yield return ("Copy",       false, () => OnCopyClicked(null, EventArgs.Empty));
        yield return ("Paste",      false, () => OnPasteClicked(null, EventArgs.Empty));
        yield return ("---",        true,  null);
        yield return ("Select All", false, () => OnSelectAllClicked(null, EventArgs.Empty));
    }

    private IEnumerable<(string, bool, Action?)> BuildViewMenu()
    {
        yield return ("Horizontal Tabs",  false, () => OnHorizontalTabsClicked(null, EventArgs.Empty));
        yield return ("Vertical Tabs",    false, () => OnVerticalTabsClicked(null, EventArgs.Empty));
        yield return ("---",              true,  null);
        var ctcpLabel = _settings.CtcpTimeReply ? "CTCP Time Reply  ✓" : "CTCP Time Reply";
        yield return (ctcpLabel,          false, () => OnToggleCtcpTimeClicked(null, EventArgs.Empty));
    }

    private IEnumerable<(string, bool, Action?)> BuildRoomsMenu()
    {
        yield return ("Join…",         false, () => OnJoinClicked(null, EventArgs.Empty));
        yield return ("Part",          false, () => OnPartClicked(null, EventArgs.Empty));
        yield return ("---",           true,  null);
        yield return ("Recent Rooms…", false, () => OnRecentRoomsClicked(null, EventArgs.Empty));
        yield return ("---",           true,  null);
        yield return ("Channel List",  false, () => OnChannelListClicked(null, EventArgs.Empty));
    }

    private IEnumerable<(string, bool, Action?)> BuildHelpMenu()
    {
        yield return ("About Irc7m", false, () => OnAboutClicked(null, EventArgs.Empty));
    }

    // ── File menu actions ──────────────────────────────────────────────────────

    private void OnConnectDisconnectClicked(object? sender, EventArgs e)
        => _mainVm.ConnectDisconnectCommand.Execute(null);

    private async void OnPreferencesClicked(object? sender, EventArgs e)
    {
        var page = new PreferencesPage(_settings);
        await Navigation.PushModalAsync(new NavigationPage(page));
        CtcpTimeMenuItem.Text = CtcpTimeLabel();
    }

    private void OnQuitClicked(object? sender, EventArgs e)
        => Application.Current?.Quit();

    private async void OnScriptEditorClicked(object? sender, EventArgs e)
    {
        var page = new ScriptEditorPage(_scriptEngine, _mainVm, _settings);
        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    // ── Servers dialog ─────────────────────────────────────────────────────────

    private async void OnServersClicked(object? sender, EventArgs e)
    {
        var servers = _settings.RegisteredServers
            .Concat(_settings.RecentServers)
            .DistinctBy(s => $"{s.Host}:{s.Port}")
            .ToList();

        if (!servers.Any())
        {
            await DisplayAlertAsync("Servers", "No saved servers. Add servers in Preferences.", "OK");
            return;
        }

        var choices = servers.Select(s => s.ToString()).ToArray();
        var chosen  = await DisplayActionSheetAsync("Connect to server…", "Cancel", null, choices);
        if (chosen is null || chosen == "Cancel") return;

        var server = servers[Array.IndexOf(choices, chosen)];
        ConnectToServer(server);
    }

    private void ConnectToServer(Models.ServerEntry server)
    {
        var ds = _mainVm.Tabs.OfType<DirectoryServerViewModel>().FirstOrDefault();
        if (ds is null) return;
        _mainVm.SelectedTab = ds;
        ds.HandleConnectCommand(server.Host, server.Port);
    }

    // ── Edit menu actions ──────────────────────────────────────────────────────

    private void OnCutClicked(object? sender, EventArgs e)   { /* handled by native platform */ }
    private void OnCopyClicked(object? sender, EventArgs e)  { /* handled by native platform */ }
    private void OnPasteClicked(object? sender, EventArgs e) { /* handled by native platform */ }
    private void OnSelectAllClicked(object? sender, EventArgs e) { /* handled by native platform */ }

    // ── View menu actions ──────────────────────────────────────────────────────

    private void OnHorizontalTabsClicked(object? sender, EventArgs e) { /* current default */ }

    private void OnVerticalTabsClicked(object? sender, EventArgs e)
        => _ = DisplayAlertAsync("View", "Vertical tab layout coming soon.", "OK");

    private void OnToggleCtcpTimeClicked(object? sender, EventArgs e)
    {
        _settings.CtcpTimeReply   = !_settings.CtcpTimeReply;
        CtcpTimeMenuItem.Text     = CtcpTimeLabel();
        _settings.Save();
    }

    // Open Modes dialog from the View menu for the currently selected channel
    private async void OnOpenModesClicked(object? sender, EventArgs e)
    {
        if (_mainVm.SelectedTab is Irc7m.ViewModels.ChannelViewModel cv)
        {
            var vm = new ModesViewModel(cv);
            var page = new ModesDialog(vm);
            await Navigation.PushModalAsync(page);
        }
        else
        {
            await DisplayAlertAsync("Modes", "No channel selected.", "OK");
        }
    }

    // Open Modes dialog from a tab's context menu. The MenuFlyoutItem's BindingContext
    // is the ChatWindowViewModel for that tab, so extract the ChannelViewModel if present.
    private async void OnOpenModesFromTabClicked(object? sender, EventArgs e)
    {
        if (sender is MenuFlyoutItem mfi && mfi.BindingContext is Irc7m.ViewModels.ChannelViewModel cv)
        {
            var vm = new ModesViewModel(cv);
            var page = new ModesDialog(vm);
            await Navigation.PushModalAsync(page);
            return;
        }

        // Fallback: if not directly bound, try the currently selected tab
        if (_mainVm.SelectedTab is Irc7m.ViewModels.ChannelViewModel selected)
        {
            var vm = new ModesViewModel(selected);
            var page = new ModesDialog(vm);
            await Navigation.PushModalAsync(page);
            return;
        }

        await DisplayAlertAsync("Modes", "No channel available to manage modes.", "OK");
    }

    private string CtcpTimeLabel()
        => _settings.CtcpTimeReply ? "CTCP Time Reply  ✓" : "CTCP Time Reply";

    // ── Rooms menu actions ─────────────────────────────────────────────────────

    private async void OnJoinClicked(object? sender, EventArgs e)
    {
        var ch = await DisplayPromptAsync("Join Channel",
            "Enter channel name (e.g. #general):",
            placeholder: "#channel");
        if (string.IsNullOrWhiteSpace(ch)) return;
        _mainVm.JoinChannel(ch.Trim());
        _settings.AddRecentChannel(ch.Trim());
    }

    private void OnPartClicked(object? sender, EventArgs e)
    {
        if (_mainVm.SelectedTab is ChannelViewModel cv)
            cv.HandlePartCommand();
    }

    private async void OnRecentRoomsClicked(object? sender, EventArgs e)
    {
        if (!_settings.RecentChannels.Any())
        {
            await DisplayAlertAsync("Recent Rooms", "No recently visited channels.", "OK");
            return;
        }

        var choices = _settings.RecentChannels.Take(15).ToArray();
        var chosen  = await DisplayActionSheetAsync("Join recent channel…", "Cancel", null, choices);
        if (chosen is null || chosen == "Cancel") return;
        _mainVm.JoinChannel(chosen);
    }

    private void OnChannelListClicked(object? sender, EventArgs e)
    {
        var ds = _mainVm.Tabs.OfType<DirectoryServerViewModel>().FirstOrDefault();
        if (ds?.IsConnected == true)
        {
            _mainVm.SelectedTab = ds;
            ds.RequestChannelList();
        }
        else
            _ = DisplayAlertAsync("Channel List", "Connect to the Directory Server first.", "OK");
    }

    // ── Help menu actions ──────────────────────────────────────────────────────

    private async void OnAboutClicked(object? sender, EventArgs e)
        => await DisplayAlertAsync("About Irc7m",
            "Irc7m — IRC Client v1.0\nBuilt with .NET MAUI\n\n" +
            "Supports directory-server channel lookup\nvia the custom FINDS / 613 protocol.\n\n" +
            "CTCP VERSION / TIME auto-reply supported.\n" +
            "Runtime C# scripting on macOS.",
            "OK");
}




