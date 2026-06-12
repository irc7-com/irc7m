using System.Collections.Concurrent;
using Irc7m.Models;

namespace Irc7m.Services;

public class ChannelModes
{
    public HashSet<char> ActiveModes { get; } = new();
    // Additional parameters (mode -> parameters list), if needed later
    public Dictionary<char, List<string>> Parameters { get; } = new();

    public ChannelModes Clone()
    {
        var c = new ChannelModes();
        foreach (var m in ActiveModes) c.ActiveModes.Add(m);
        foreach (var kv in Parameters)
            c.Parameters[kv.Key] = new List<string>(kv.Value);
        return c;
    }

    public override string ToString()
    {
        if (ActiveModes.Count == 0) return string.Empty;
        return string.Join(',', ActiveModes);
    }
}

public static class ChannelModesStore
{
    private static readonly ConcurrentDictionary<string, ChannelModes> _modes = new(StringComparer.OrdinalIgnoreCase);

    public static event Action<string, ChannelModes>? ModesChanged;

    public static ChannelModes GetModes(string channel)
    {
        if (channel is null) throw new ArgumentNullException(nameof(channel));
        return _modes.GetOrAdd(channel, _ => new ChannelModes());
    }

    public static void SetModes(string channel, ChannelModes modes)
    {
        if (channel is null) throw new ArgumentNullException(nameof(channel));
        _modes[channel] = modes ?? new ChannelModes();
        ModesChanged?.Invoke(channel, modes);
    }

    public static void UpdateFromModeLine(string channel, string modespec, IEnumerable<string>? parameters = null)
    {
        var cm = GetModes(channel);

        // modespec may be like "+nt" or "-o" etc. We will apply sequentially.
        var sign = 1; // +1 add, -1 remove
        var paramsList = parameters?.ToList() ?? new List<string>();
        int pidx = 0;
        foreach (var ch in modespec)
        {
            if (ch == '+') { sign = 1; continue; }
            if (ch == '-') { sign = -1; continue; }

            // For channel-level modes that take a parameter (b,k, etc.) we'd handle param consumption here.
            // For now treat all modes as boolean flags in the ActiveModes set.
            if (sign == 1)
                cm.ActiveModes.Add(ch);
            else
                cm.ActiveModes.Remove(ch);
        }

        // Push notification
        ModesChanged?.Invoke(channel, cm.Clone());
    }
}

