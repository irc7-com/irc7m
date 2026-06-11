using Irc7m.Services;

namespace Irc7m.Views;

public partial class PreferencesPage : ContentPage
{
    private readonly IrcClientSettings _settings;

    public PreferencesPage(IrcClientSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        NickEntry.Text          = _settings.Nick;
        AltNickEntry.Text       = _settings.AltNick;
        UserNameEntry.Text      = _settings.UserName;
        RealNameEntry.Text      = _settings.RealName;
        EmailEntry.Text         = _settings.Email;
        PasswordEntry.Text      = _settings.Password;

        RetrySwitch.IsToggled          = _settings.RetryOnDisconnect;
        RetryIntervalEntry.Text        = _settings.RetryIntervalSeconds.ToString();

        FontSizeEntry.Text             = _settings.FontSize.ToString("0.#");
        TimestampsSwitch.IsToggled     = _settings.ShowTimestamps;
        ShowModeSwitch.IsToggled       = _settings.ShowModeInNick;
        DebugModeSwitch.IsToggled      = _settings.DefaultDebugMode;

        CtcpVersionSwitch.IsToggled    = _settings.CtcpVersionReply;
        CtcpTimeSwitch.IsToggled       = _settings.CtcpTimeReply;
    }

    private void ApplySettings()
    {
        if (!string.IsNullOrWhiteSpace(NickEntry.Text))
            _settings.Nick      = NickEntry.Text.Trim();
        if (!string.IsNullOrWhiteSpace(AltNickEntry.Text))
            _settings.AltNick   = AltNickEntry.Text.Trim();
        if (!string.IsNullOrWhiteSpace(UserNameEntry.Text))
            _settings.UserName  = UserNameEntry.Text.Trim();
        if (!string.IsNullOrWhiteSpace(RealNameEntry.Text))
            _settings.RealName  = RealNameEntry.Text.Trim();

        _settings.Email    = EmailEntry.Text?.Trim()    ?? "";
        _settings.Password = PasswordEntry.Text?.Trim() ?? "";

        _settings.RetryOnDisconnect = RetrySwitch.IsToggled;
        if (int.TryParse(RetryIntervalEntry.Text, out var ri) && ri > 0)
            _settings.RetryIntervalSeconds = ri;

        if (double.TryParse(FontSizeEntry.Text, out var fs) && fs > 0)
            _settings.FontSize = fs;

        _settings.ShowTimestamps   = TimestampsSwitch.IsToggled;
        _settings.ShowModeInNick   = ShowModeSwitch.IsToggled;
        _settings.DefaultDebugMode = DebugModeSwitch.IsToggled;

        _settings.CtcpVersionReply = CtcpVersionSwitch.IsToggled;
        _settings.CtcpTimeReply    = CtcpTimeSwitch.IsToggled;

        _settings.Save();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ApplySettings();
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();
}

