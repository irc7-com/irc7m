namespace Irc7m.Models;

/// <summary>A single user entry in a channel nick list.</summary>
public class NickInfo
{
    public string Nick       { get; set; } = "";
    public char?  ModePrefix { get; set; }   // '@' op  '+' voice  '~' owner  '&' admin  '%' halfop

    public string DisplayName => ModePrefix.HasValue ? $"{ModePrefix}{Nick}" : Nick;

    public static NickInfo FromRaw(string raw)
    {
        if (raw.Length > 0 && raw[0] is '@' or '+' or '~' or '&' or '%')
            return new NickInfo { ModePrefix = raw[0], Nick = raw[1..] };
        return new NickInfo { Nick = raw };
    }
}

