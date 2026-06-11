#if MACCATALYST || WINDOWS
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
#endif

using Irc7m.ViewModels;

namespace Irc7m.Services;

/// <summary>
/// Globals object injected into every C# script.
/// Scripts can call Echo("text") to print to the output console,
/// access MainVm to interact with the IRC client, and call SendRaw to
/// send a raw IRC command on the current active channel.
/// </summary>
public class ScriptGlobals
{
    public MainViewModel? Main     { get; set; }
    public IrcClientSettings? Settings { get; set; }

    /// <summary>Print a line to the script output console.</summary>
    public Action<string> Echo { get; set; } = _ => { };

    /// <summary>Join a channel via the directory server.</summary>
    public void JoinChannel(string channel) => Main?.JoinChannel(channel);
}

/// <summary>
/// Runtime C# script engine. Compilation and execution via Roslyn on macCatalyst/Windows.
/// On iOS/Android a stub message is returned instead.
/// </summary>
public class ScriptEngine
{
    /// <summary>Raised for every line of output produced by the script (or engine errors).</summary>
    public event EventHandler<string>? OutputReceived;

    /// <summary>The sample starter script shown in the editor on first launch.</summary>
    public const string SampleScript = """
        // Irc7m Sample Script
        // Available globals:
        //   Echo(string)        – print to output
        //   Main                – MainViewModel (access Tabs, SelectedTab, etc.)
        //   Settings            – IrcClientSettings
        //   JoinChannel(string) – join a channel via the directory server
        //
        // Example: print the titles of all open tabs
        foreach (var tab in Main.Tabs)
            Echo($"Tab: {tab.Title}");

        Echo("Script completed.");
        """;

    public async Task<bool> RunAsync(string code, ScriptGlobals globals)
    {
#if MACCATALYST || WINDOWS
        try
        {
            globals.Echo = line => OutputReceived?.Invoke(this, line);

            var options = ScriptOptions.Default
                .WithReferences(
                    typeof(MainViewModel).Assembly,
                    typeof(object).Assembly)
                .WithImports(
                    "System",
                    "System.Linq",
                    "System.Threading.Tasks",
                    "Irc7m.ViewModels",
                    "Irc7m.Services",
                    "Irc7m.Models");

            await CSharpScript.RunAsync(code, options, globals);
            OutputReceived?.Invoke(this, "✓ Script finished.");
            return true;
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke(this, $"✗ {ex.Message}");
            return false;
        }
#else
        await Task.CompletedTask;
        OutputReceived?.Invoke(this, "Script execution is not supported on this platform.");
        return false;
#endif
    }
}

