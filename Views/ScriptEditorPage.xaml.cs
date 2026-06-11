using Irc7m.Services;
using Irc7m.ViewModels;

namespace Irc7m.Views;

public partial class ScriptEditorPage : ContentPage
{
    private readonly ScriptEngine   _engine;
    private readonly ScriptGlobals  _globals;
    private double                  _outputHeight = 200;
    private double                  _panStartHeight;
    private bool                    _running;

    public ScriptEditorPage(ScriptEngine engine, MainViewModel mainVm, IrcClientSettings settings)
    {
        _engine  = engine;
        _globals = new ScriptGlobals { Main = mainVm, Settings = settings };

        _engine.OutputReceived += (_, line) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OutputEditor.Text += (OutputEditor.Text.Length > 0 ? "\n" : "") + line;
            });

        InitializeComponent();
        CodeEditor.Text = ScriptEngine.SampleScript;
    }

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        if (_running) return;
        _running = true;

        OutputEditor.Text += $"\n[{DateTime.Now:HH:mm:ss}] ▶ Running script…\n";
        await _engine.RunAsync(CodeEditor.Text ?? "", _globals);

        _running = false;
    }

    private void OnClearOutputClicked(object? sender, EventArgs e)
        => OutputEditor.Text = "";

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();

    // ── Resizable output panel ─────────────────────────────────────────────────

    private void OnDividerPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartHeight = OutputPanel.HeightRequest > 0
                    ? OutputPanel.HeightRequest
                    : _outputHeight;
                break;

            case GestureStatus.Running:
                var newH = Math.Max(80, _panStartHeight - e.TotalY);
                _outputHeight              = newH;
                OutputPanel.HeightRequest  = newH;
                break;
        }
    }
}

