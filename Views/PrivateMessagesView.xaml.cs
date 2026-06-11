namespace Irc7m.Views;

public partial class PrivateMessagesView : ContentView
{
    public PrivateMessagesView()
    {
        InitializeComponent();
    }

    public void FocusInput() => ChatView.FocusInput();
}

