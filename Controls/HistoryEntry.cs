using System.Windows.Input;

namespace Irc7m.Controls;

/// <summary>
/// Entry subclass that exposes HistoryUp/DownCommand bindable properties.
/// Platform handlers fire these when the user presses ↑ / ↓ in the focused field.
/// </summary>
public class HistoryEntry : Entry
{
    public static readonly BindableProperty HistoryUpCommandProperty =
        BindableProperty.Create(nameof(HistoryUpCommand), typeof(ICommand), typeof(HistoryEntry));

    public static readonly BindableProperty HistoryDownCommandProperty =
        BindableProperty.Create(nameof(HistoryDownCommand), typeof(ICommand), typeof(HistoryEntry));

    public ICommand? HistoryUpCommand
    {
        get => (ICommand?)GetValue(HistoryUpCommandProperty);
        set => SetValue(HistoryUpCommandProperty, value);
    }

    public ICommand? HistoryDownCommand
    {
        get => (ICommand?)GetValue(HistoryDownCommandProperty);
        set => SetValue(HistoryDownCommandProperty, value);
    }

    /// <summary>Called by the platform handler when ↑ is pressed.</summary>
    public void TriggerHistoryUp()   => HistoryUpCommand?.Execute(null);

    /// <summary>Called by the platform handler when ↓ is pressed.</summary>
    public void TriggerHistoryDown() => HistoryDownCommand?.Execute(null);
}

