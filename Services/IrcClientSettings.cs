using System.Text.Json;
using Irc7m.Models;

namespace Irc7m.Services;

/// <summary>Application-wide IRC identity and preference settings, persisted via MAUI Preferences.</summary>
public class IrcClientSettings
{
    // ── Identity ────────────────────────────────────────────────────────────────
    public string Nick     { get; set; } = "Irc7mUser";
    public string AltNick  { get; set; } = "Irc7mUser_";
    public string UserName { get; set; } = "irc7m";
    public string RealName { get; set; } = "Irc7m IRC Client";
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";

    // ── Connection ──────────────────────────────────────────────────────────────
    public bool RetryOnDisconnect    { get; set; } = true;
    public int  RetryIntervalSeconds { get; set; } = 5;

    // ── Display ─────────────────────────────────────────────────────────────────
    public double FontSize         { get; set; } = 12;
    public bool   ShowTimestamps   { get; set; } = true;
    public bool   ShowModeInNick   { get; set; } = true;
    public bool   DefaultDebugMode { get; set; } = false;

    // ── CTCP ────────────────────────────────────────────────────────────────────
    public bool CtcpVersionReply { get; set; } = true;
    public bool CtcpTimeReply    { get; set; } = true;

    // ── Servers ─────────────────────────────────────────────────────────────────
    public List<ServerEntry> RecentServers { get; set; } = new();

    public List<ServerEntry> RegisteredServers { get; set; } = new()
    {
        new ServerEntry { Name = "irc7", Host = "dir.irc7.com", Port = 6667 }
    };

    // ── Channels ────────────────────────────────────────────────────────────────
    public List<string> RecentChannels { get; set; } = new();

    // ── Persistence ─────────────────────────────────────────────────────────────
    private const string P = "irc7m_";

    public void Load()
    {
        Nick     = Preferences.Get(P + "nick",     Nick);
        AltNick  = Preferences.Get(P + "altnick",  AltNick);
        UserName = Preferences.Get(P + "username", UserName);
        RealName = Preferences.Get(P + "realname", RealName);
        Email    = Preferences.Get(P + "email",    Email);
        Password = Preferences.Get(P + "password", Password);

        RetryOnDisconnect    = Preferences.Get(P + "retry",          RetryOnDisconnect);
        RetryIntervalSeconds = Preferences.Get(P + "retry_interval", RetryIntervalSeconds);

        FontSize         = (double)Preferences.Get(P + "fontsize",   (float)FontSize);
        ShowTimestamps   = Preferences.Get(P + "timestamps",         ShowTimestamps);
        ShowModeInNick   = Preferences.Get(P + "showmode",           ShowModeInNick);
        DefaultDebugMode = Preferences.Get(P + "debugmode",          DefaultDebugMode);
        CtcpVersionReply = Preferences.Get(P + "ctcp_version",       CtcpVersionReply);
        CtcpTimeReply    = Preferences.Get(P + "ctcp_time",          CtcpTimeReply);

        try
        {
            var rSrv = Preferences.Get(P + "recent_servers",     "[]");
            var gSrv = Preferences.Get(P + "registered_servers", "");
            var rCh  = Preferences.Get(P + "recent_channels",    "[]");

            RecentServers  = JsonSerializer.Deserialize<List<ServerEntry>>(rSrv!)  ?? RecentServers;
            RecentChannels = JsonSerializer.Deserialize<List<string>>(rCh!)        ?? RecentChannels;
            if (!string.IsNullOrEmpty(gSrv))
                RegisteredServers = JsonSerializer.Deserialize<List<ServerEntry>>(gSrv!) ?? RegisteredServers;
        }
        catch { /* ignore */ }
    }

    public void Save()
    {
        Preferences.Set(P + "nick",     Nick);
        Preferences.Set(P + "altnick",  AltNick);
        Preferences.Set(P + "username", UserName);
        Preferences.Set(P + "realname", RealName);
        Preferences.Set(P + "email",    Email);
        Preferences.Set(P + "password", Password);

        Preferences.Set(P + "retry",          RetryOnDisconnect);
        Preferences.Set(P + "retry_interval", RetryIntervalSeconds);

        Preferences.Set(P + "fontsize",       (float)FontSize);
        Preferences.Set(P + "timestamps",     ShowTimestamps);
        Preferences.Set(P + "showmode",       ShowModeInNick);
        Preferences.Set(P + "debugmode",      DefaultDebugMode);
        Preferences.Set(P + "ctcp_version",   CtcpVersionReply);
        Preferences.Set(P + "ctcp_time",      CtcpTimeReply);

        try
        {
            Preferences.Set(P + "recent_servers",     JsonSerializer.Serialize(RecentServers));
            Preferences.Set(P + "registered_servers", JsonSerializer.Serialize(RegisteredServers));
            Preferences.Set(P + "recent_channels",    JsonSerializer.Serialize(RecentChannels));
        }
        catch { }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    public void AddRecentServer(string host, int port, string name = "")
    {
        RecentServers.RemoveAll(s =>
            s.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && s.Port == port);
        RecentServers.Insert(0, new ServerEntry
            { Name = name, Host = host, Port = port, LastUsed = DateTime.UtcNow });
        if (RecentServers.Count > 10)
            RecentServers.RemoveRange(10, RecentServers.Count - 10);
        Save();
    }

    public void AddRecentChannel(string channel)
    {
        RecentChannels.RemoveAll(c =>
            c.Equals(channel, StringComparison.OrdinalIgnoreCase));
        RecentChannels.Insert(0, channel);
        if (RecentChannels.Count > 20)
            RecentChannels.RemoveRange(20, RecentChannels.Count - 20);
        Save();
    }
}
