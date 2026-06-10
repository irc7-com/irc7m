using System.ComponentModel;
using Irc7m.ViewModels;

namespace Irc7m.Views;

public partial class ChatWindowView : ContentView
{
    private ChatWindowViewModel? _currentVm;

    public ChatWindowView()
    {
        InitializeComponent();

        // Re-focus after submit
        InputEntry.Completed += (_, _) => InputEntry.Focus();

        // Highlight the border on focus
        InputEntry.Focused   += (_, _) => InputBorder.Stroke = Color.FromArgb("#0078d4");
        InputEntry.Unfocused += (_, _) => InputBorder.Stroke = Color.FromArgb("#3c3c3c");

        // Focus the input as soon as this view appears
        this.Loaded += (_, _) => FocusInput();
    }

    /// <summary>Focuses the text input field.</summary>
    public void FocusInput()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Yield();
            InputEntry.Focus();
        });
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_currentVm is not null)
            _currentVm.PropertyChanged -= OnVmPropertyChanged;

        _currentVm = BindingContext as ChatWindowViewModel;

        if (_currentVm is not null)
            _currentVm.PropertyChanged += OnVmPropertyChanged;
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatWindowViewModel.OutputText))
            await MainThread.InvokeOnMainThreadAsync(
                () => EditorScrollHelper.ScrollToEndAsync(OutputEditor));
    }
}

