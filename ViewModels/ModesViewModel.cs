using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Irc7m.Services;
using Microsoft.Maui.ApplicationModel;

namespace Irc7m.ViewModels;

public class ModeEntry : INotifyPropertyChanged
{
    public char ModeChar { get; }
    public string Label { get; }

    private bool _isSet;
    public bool IsSet
    {
        get => _isSet;
        set { if (_isSet == value) return; _isSet = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet))); }
    }

    public ModeEntry(char ch, string label, bool isSet = false)
    {
        ModeChar = ch; Label = label; _isSet = isSet;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class PropertyEntry : INotifyPropertyChanged
{
    private string _value = string.Empty;
    public string Key { get; }
    public string Value
    {
        get => _value;
        set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    }

    public PropertyEntry(string key, string? value = null)
    {
        Key = key;
        _value = value ?? string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ModesViewModel : INotifyPropertyChanged
{
    private readonly ChannelViewModel _channelVm;
    public string ChannelName => _channelVm.Channel;

    public ObservableCollection<ModeEntry> ModeEntries { get; } = new();
    public ObservableCollection<PropertyEntry> PropertyEntries { get; } = new();

    public ICommand ApplyCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand RefreshPropertiesCommand { get; }
    public ICommand ApplyPropertiesCommand { get; }

    public bool CanManageModes => _channelVm.CanManageModes;

    public ModesViewModel(ChannelViewModel channelVm)
    {
        _channelVm = channelVm;

        // Build the list of human-friendly channel modes to display.
        var known = new (char ch, string label)[]
        {
            ('a', "Auth only"),
            ('d', "Cloneable"),
            ('e', "Clone"),
            ('f', "Profanity filter"),
            ('r', "Registered only"),
            ('w', "No whisper"),
            ('W', "No guest whisper"),
            ('z', "Service channel"),
            ('u', "Knock-only"),
            ('x', "Auditorium (moderated)"),
            ('S', "Subscriber"),
            ('g', "On stage")
        };

        var cm = ChannelModesStore.GetModes(channelVm.Channel);
        foreach (var k in known)
            ModeEntries.Add(new ModeEntry(k.ch, k.label, cm.ActiveModes.Contains(k.ch)));

        // Seed property entries (topic, onjoin, onpart) – values will be populated when server returns PROP list
        PropertyEntries.Add(new PropertyEntry("topic"));
        PropertyEntries.Add(new PropertyEntry("onjoin"));
        PropertyEntries.Add(new PropertyEntry("onpart"));

        ApplyCommand = new Command(OnApply);
        CloseCommand = new Command(OnClose);
        RefreshPropertiesCommand = new Command(OnRefreshProperties);
        ApplyPropertiesCommand = new Command(OnApplyProperties);

        ChannelModesStore.ModesChanged += OnStoreModesChanged;
    }

    private void OnStoreModesChanged(string channel, ChannelModes modes)
    {
        if (!string.Equals(channel, _channelVm.Channel, StringComparison.OrdinalIgnoreCase)) return;
        // update entries on UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var e in ModeEntries)
                e.IsSet = modes.ActiveModes.Contains(e.ModeChar);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModeEntries)));
        });
    }

    private async void OnApply()
    {
        if (!CanManageModes) return;

        var adds = ModeEntries.Where(e => e.IsSet).Select(e => e.ModeChar).ToList();
        var removes = ModeEntries.Where(e => !e.IsSet).Select(e => e.ModeChar).ToList();

        string modespec = null!;
        if (adds.Count > 0 && removes.Count > 0)
            modespec = "+" + new string(adds.ToArray()) + "-" + new string(removes.ToArray());
        else if (adds.Count > 0)
            modespec = "+" + new string(adds.ToArray());
        else if (removes.Count > 0)
            modespec = "-" + new string(removes.ToArray());

        if (!string.IsNullOrEmpty(modespec))
        {
            // Send MODE command to server; server is source of truth
            await _channelVm.SendRawAsync($"MODE {_channelVm.Channel} {modespec}");
        }
    }

    private async void OnRefreshProperties()
    {
        // Request property enumeration from server. Server-specific command: PROP %<channel> *
        await _channelVm.SendRawAsync($"PROP {_channelVm.Channel} *");
    }

    private async void OnApplyProperties()
    {
        if (!CanManageModes) return; // reuse permission model for now

        foreach (var p in PropertyEntries)
        {
            // If value is empty skip
            if (string.IsNullOrWhiteSpace(p.Value)) continue;

            // If value contains spaces, prefix with ':' to send as trailing parameter
            var val = p.Value.Contains(' ') ? ":" + p.Value : p.Value;
            await _channelVm.SendRawAsync($"PROP %{_channelVm.Channel} {p.Key} {val}");
        }
    }

    private async void OnClose()
    {
        // Close is handled by the view as modal dismiss
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}



