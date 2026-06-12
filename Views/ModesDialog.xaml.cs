using Irc7m.ViewModels;

namespace Irc7m.Views;

public partial class ModesDialog : ContentPage
{
    public ModesDialog(ModesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        this.FindByName("PageRoot");
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}

