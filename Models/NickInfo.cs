using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Irc7m.Models;

/// <summary>A single user entry in a channel nick list.</summary>
public class NickInfo : INotifyPropertyChanged
{
    public string Nick { get; set; } = "";

    private char? _modePrefix;
    /// <summary>Visual prefix character shown before the nick ('.' '@' '+' etc).</summary>
    public char? ModePrefix
    {
        get => _modePrefix;
        set { if (_modePrefix == value) return; _modePrefix = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    // Populated on demand by WHOIS
    public string? UserName { get; set; }
    public string? HostName { get; set; }
    public string? Server { get; set; }
    public string? RealName { get; set; }

    // Track user-mode letters (e.g. 'q','o','v') for accurate state handling
    private readonly HashSet<char> _userModes = new();
    public IEnumerable<char> UserModes => _userModes;

    public void AddUserMode(char m)
    {
        if (_userModes.Add(m))
        {
            UpdateModePrefixFromUserModes();
            OnPropertyChanged(nameof(UserModes));
        }
    }

    public void RemoveUserMode(char m)
    {
        if (_userModes.Remove(m))
        {
            UpdateModePrefixFromUserModes();
            OnPropertyChanged(nameof(UserModes));
        }
    }

    private void UpdateModePrefixFromUserModes()
    {
        // Priority: q (owner) > o (op) > v (voice)
        if (_userModes.Contains('q')) ModePrefix = '.'; // owner prefix per project convention
        else if (_userModes.Contains('o')) ModePrefix = '@';
        else if (_userModes.Contains('v')) ModePrefix = '+';
        else ModePrefix = null;
    }

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

    public bool HasWhoisData => UserName is not null;
    public string DisplayName => ModePrefix.HasValue ? $"{ModePrefix}{Nick}" : Nick;

    /// <summary>Returns Nick!user@host$server when WHOIS data is available, otherwise just Nick.</summary>
    public string IdentString => HasWhoisData ? $"{Nick}!{UserName}@{HostName}${Server}" : Nick;

    public static NickInfo FromRaw(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new NickInfo();
        if (raw.Length > 0 && (raw[0] == '@' || raw[0] == '+' || raw[0] == '~' || raw[0] == '&' || raw[0] == '%' || raw[0] == '.'))
        {
            var ni = new NickInfo { Nick = raw[1..] };
            ni.ModePrefix = raw[0];
            // Also map common prefixes to user-modes for initial state
            if (raw[0] == '+') ni.AddUserMode('v');
            if (raw[0] == '@' || raw[0] == '~') ni.AddUserMode('o');
            if (raw[0] == '.') ni.AddUserMode('q');
            return ni;
        }
        return new NickInfo { Nick = raw };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
