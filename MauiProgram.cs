using Microsoft.Extensions.Logging;
using Irc7m.Controls;
using Irc7m.Services;
using Irc7m.ViewModels;
using Irc7m.Views;

namespace Irc7m;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",   "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf",  "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(h =>
            {
#if MACCATALYST || IOS
                h.AddHandler<HistoryEntry, HistoryEntryHandler>();
#endif
            });

        // ── Services ────────────────────────────────────────────────────────
        builder.Services.AddSingleton<IrcClientSettings>(sp =>
        {
            var s = new IrcClientSettings();
            s.Load();
            return s;
        });
        builder.Services.AddSingleton<ScriptEngine>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

        // ── Transient pages (opened as modals) ──────────────────────────────
        builder.Services.AddTransient<PreferencesPage>();
        builder.Services.AddTransient<ScriptEditorPage>(sp =>
            new ScriptEditorPage(
                sp.GetRequiredService<ScriptEngine>(),
                sp.GetRequiredService<MainViewModel>(),
                sp.GetRequiredService<IrcClientSettings>()));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

