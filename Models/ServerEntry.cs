namespace Irc7m.Models;

/// <summary>Represents a saved IRC server connection.</summary>
public class ServerEntry
{
    public string  Name     { get; set; } = "";
    public string  Host     { get; set; } = "";
    public int     Port     { get; set; } = 6667;
    public string? Password { get; set; }
    public DateTime? LastUsed { get; set; }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name) ? $"{Host}:{Port}" : $"{Name} ({Host}:{Port})";
}

