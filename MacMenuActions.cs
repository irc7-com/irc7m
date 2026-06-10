namespace Irc7m;

/// <summary>
/// Bridges platform-native menu actions (Mac Catalyst AppDelegate)
/// to MAUI page handlers without creating a circular dependency.
/// </summary>
public static class MacMenuActions
{
    public static event Action? SettingsRequested;
    public static event Action? AboutRequested;

    internal static void FireSettings() => SettingsRequested?.Invoke();
    internal static void FireAbout()    => AboutRequested?.Invoke();
}

