#if MACCATALYST || IOS
using CoreGraphics;
using UIKit;
#endif

namespace Irc7m.Views;

/// <summary>Shared helpers for Editor-based output views.</summary>
internal static class EditorScrollHelper
{
    /// <summary>Scrolls an Editor to its last line using the native UITextView API.</summary>
    public static async Task ScrollToEndAsync(Editor editor)
    {
        await Task.Yield(); // let layout settle before measuring

#if MACCATALYST || IOS
        if (editor.Handler?.PlatformView is UITextView tv)
        {
            var maxY = Math.Max(0, tv.ContentSize.Height - tv.Bounds.Height);
            tv.SetContentOffset(new CGPoint(0, maxY), animated: false);
        }
#endif
    }
}

