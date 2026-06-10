using Irc7m.Services;
using Irc7m.ViewModels;
using Irc7m.Views;

namespace Irc7m;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel      _mainVm;
    private readonly IrcClientSettings  _settings;

    // Cache of VM → View so we don't recreate on every tab switch
    private readonly Dictionary<ChatWindowViewModel, View> _viewCache = new();

    public MainPage(MainViewModel mainVm, IrcClientSettings settings)
    {
        _mainVm   = mainVm;
        _settings = settings;

        InitializeComponent();
        BindingContext = mainVm;

        mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
                UpdateContent(mainVm.SelectedTab);
        };

        mainVm.Tabs.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is null) return;
            foreach (ChatWindowViewModel vm in e.OldItems)
                _viewCache.Remove(vm);
        };

        mainVm.Initialize();
    }

    // ── Content switching ──────────────────────────────────────────────────────

    private void UpdateContent(ChatWindowViewModel? vm)
    {
        if (vm is null) { ContentArea.Content = null; return; }

        if (!_viewCache.TryGetValue(vm, out var view))
        {
            view = vm switch
            {
                DirectoryServerViewModel dsVm => new DirectoryServerWindowView { BindingContext = dsVm },
                ChannelViewModel         cVm  => new ChannelWindowView         { BindingContext = cVm  },
                _                             => new ChatWindowView            { BindingContext = vm   }
            };

            _viewCache[vm] = view;
        }

        ContentArea.Content = view;
    
        // Focus the input of the newly active window
        switch (view)
        {
            case DirectoryServerWindowView dsv: dsv.FocusInput(); break;
            case ChannelWindowView         cwv: cwv.FocusInput(); break;
            case ChatWindowView            cv:  cv.FocusInput();  break;
        }
    }

    // ── Menu handlers ──────────────────────────────────────────────────────────

    private async void OnSettingsClicked(object? sender, EventArgs e) => await OnSettingsAsync();
    private void      OnQuitClicked(object? sender, EventArgs e)      => Application.Current?.Quit();
    private async void OnAboutClicked(object? sender, EventArgs e)    => await OnAboutAsync();

    private async Task OnSettingsAsync()
    {
        var nick = await DisplayPromptAsync("Settings", "Nick name:",
            initialValue: _settings.Nick, maxLength: 30);
        if (!string.IsNullOrWhiteSpace(nick)) _settings.Nick = nick.Trim();

        var user = await DisplayPromptAsync("Settings", "Username:",
            initialValue: _settings.UserName, maxLength: 30);
        if (!string.IsNullOrWhiteSpace(user)) _settings.UserName = user.Trim();
    }

    private async Task OnAboutAsync()
        => await DisplayAlertAsync("About Irc7m",
            "Irc7m — IRC Client\nBuilt with .NET MAUI\n\n" +
            "Supports directory-server channel lookup\nvia the custom FINDS / 613 protocol.",
            "OK");
}