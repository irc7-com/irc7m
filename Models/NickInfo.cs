using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Irc7m.Models;

/// <summary>A single user entry in a channel nick list.</summary>
public class NickInfo : INotifyPropertyChanged
{
    public string Nick       { get; set; } = "";
    public char?  ModePrefix { get; set; }   // '@' op  '+' voice  '~' owner  '&' admin  '%' halfop

    // Populated on demand by WHOIS
    public string? UserName  { get; set; }
    public string? HostName  { get; set; }
    public string? Server    { get; set; }
    public string? RealName  { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool   HasWhoisData => UserName is not null;
    public string DisplayName  => ModePrefix.HasValue ? $"{ModePrefix}{Nick}" : Nick;

    /// <summary>Returns Nick!user@host$server when WHOIS data is available, otherwise just Nick.</summary>
    public string IdentString =>
        HasWhoisData ? $"{Nick}!{UserName}@{HostName}${Server}" : Nick;

    public static NickInfo FromRaw(string raw)
    {
        if (raw.Length > 0 && raw[0] is '@' or '+' or '~' or '&' or '%')
            return new NickInfo { ModePrefix = raw[0], Nick = raw[1..] };
        return new NickInfo { Nick = raw };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
