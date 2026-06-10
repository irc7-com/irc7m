using Microsoft.Extensions.Logging;
using Irc7m.Controls;
using Irc7m.Services;
using Irc7m.ViewModels;

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
        builder.Services.AddSingleton<IrcClientSettings>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

