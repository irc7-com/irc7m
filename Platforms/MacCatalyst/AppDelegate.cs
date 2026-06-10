using Foundation;
using UIKit;

namespace Irc7m;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void BuildMenu(IUIMenuBuilder builder)
    {
        base.BuildMenu(builder);

        // Remove system menus that don't apply to a chat client
        // or that would duplicate our own MAUI MenuBarItems
        builder.RemoveMenu("com.apple.menu.format");    // no text formatting
        builder.RemoveMenu("com.apple.menu.services");  // no system services
        builder.RemoveMenu("com.apple.menu.help");      // we provide our own Help menu
    }
}

