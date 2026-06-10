namespace Irc7m.Models;

/// <summary>
/// Represents a parsed IRC protocol message.
/// Grammar: [@tags] [:prefix] COMMAND [param ...] [:trailing]
/// </summary>
public class IrcMessage
{
    public string?   Prefix   { get; init; }
    public string    Command  { get; init; } = "";
    public string[]  Params   { get; init; } = [];
    public string?   Trailing { get; init; }

    /// <summary>Nick extracted from "nick!user@host" prefix.</summary>
    public string? PrefixNick =>
        Prefix is not null && Prefix.Contains('!')
            ? Prefix[..Prefix.IndexOf('!')]
            : Prefix;

    public static IrcMessage Parse(string line)
    {
        if (string.IsNullOrEmpty(line))
            return new IrcMessage();

        string? prefix   = null;
        string? trailing = null;

        // Strip @tags
        if (line.StartsWith('@'))
        {
            var sp = line.IndexOf(' ');
            if (sp < 0) return new IrcMessage();
            line = line[(sp + 1)..].TrimStart();
        }

        // Prefix
        if (line.StartsWith(':'))
        {
            var sp = line.IndexOf(' ');
            if (sp < 0) return new IrcMessage { Command = line[1..].ToUpperInvariant() };
            prefix = line[1..sp];
            line   = line[(sp + 1)..];
        }

        // Trailing
        var ti = line.IndexOf(" :");
        if (ti >= 0)
        {
            trailing = line[(ti + 2)..];
            line     = line[..ti];
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new IrcMessage
        {
            Prefix   = prefix,
            Command  = parts.Length > 0 ? parts[0].ToUpperInvariant() : "",
            Params   = parts.Length > 1 ? parts[1..] : [],
            Trailing = trailing
        };
    }

    public override string ToString() =>
        $"[{Command}] prefix={Prefix} params={string.Join(",", Params)} trailing={Trailing}";
}

