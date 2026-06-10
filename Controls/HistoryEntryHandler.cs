#if MACCATALYST || IOS
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace Irc7m.Controls;

/// <summary>
/// Custom EntryHandler that swaps in a UITextField subclass which intercepts
/// ↑ / ↓ arrow keys via PressesBegan – more reliable than UIKeyCommand for text fields.
/// </summary>
public class HistoryEntryHandler : EntryHandler
{
    protected override MauiTextField CreatePlatformView() => new HistoryTextField();

    protected override void ConnectHandler(MauiTextField platformView)
    {
        base.ConnectHandler(platformView);

        // Suppress the native Mac Catalyst blue focus ring – our Border provides it
        platformView.Layer.BorderWidth  = 0;
        platformView.Layer.BorderColor  = null;
        platformView.BorderStyle        = UITextBorderStyle.None;

        if (platformView is HistoryTextField ht && VirtualView is HistoryEntry he)
        {
            ht.OnHistoryUp   = () => he.TriggerHistoryUp();
            ht.OnHistoryDown = () => he.TriggerHistoryDown();
        }
    }
}

/// <summary>
/// UITextField subclass that intercepts physical Up/Down arrow key presses
/// and routes them to the history callbacks instead of moving the cursor.
/// UIKey.Characters for arrow keys: ↑ = U+F700, ↓ = U+F701 (Apple private-use).
/// </summary>
sealed class HistoryTextField : MauiTextField
{
    public Action? OnHistoryUp   { get; set; }
    public Action? OnHistoryDown { get; set; }

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (TryHandleArrow(presses)) return;   // navigate + swallow
        base.PressesBegan(presses, evt);
    }

    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (IsArrowPress(presses)) return;     // swallow only – don't navigate again
        base.PressesEnded(presses, evt);
    }

    public override void PressesCancelled(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (IsArrowPress(presses)) return;
        base.PressesCancelled(presses, evt);
    }

    /// <summary>Fires the callback and returns true if an arrow key was pressed.</summary>
    private bool TryHandleArrow(NSSet<UIPress> presses)
    {
        foreach (var press in presses)
        {
            var ch = press.Key?.Characters;
            if (ch == "\uF700") { OnHistoryUp?.Invoke();   return true; }
            if (ch == "\uF701") { OnHistoryDown?.Invoke(); return true; }
        }
        return false;
    }

    /// <summary>Swallows arrow key releases without firing callbacks.</summary>
    private static bool IsArrowPress(NSSet<UIPress> presses)
    {
        foreach (var press in presses)
        {
            var ch = press.Key?.Characters;
            if (ch == "\uF700" || ch == "\uF701") return true;
        }
        return false;
    }
}
#endif
