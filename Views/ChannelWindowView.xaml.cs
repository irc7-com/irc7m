using System.ComponentModel;
using Irc7m.ViewModels;

namespace Irc7m.Views;

public partial class ChannelWindowView : ContentView
{
    private ChannelViewModel? _vm;
    private double _panStartWidth;

    public ChannelWindowView()
    {
        InitializeComponent();
    }

    public void FocusInput() => ChatView.FocusInput();

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = BindingContext as ChannelViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChannelViewModel.DebugText))
        {
            await MainThread.InvokeOnMainThreadAsync(ScrollDebugToBottom);
        }
        else if (e.PropertyName == nameof(ChannelViewModel.IsDebugMode)
                 && _vm?.IsDebugMode == true)
        {
            SetDebugPanelHalfWidth();
        }
    }

    private void SetDebugPanelHalfWidth()
    {
        if (_vm is null) return;
        var w = Width > 0 ? Width : RootGrid.Width;
        if (w > 0)
            _vm.DebugPanelWidth = Math.Max(200, w / 2);
    }

    private async Task ScrollDebugToBottom()
    {
        await EditorScrollHelper.ScrollToEndAsync(DebugEditor);
    }

    private void OnDividerPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_vm is null) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartWidth = _vm.DebugPanelWidth;
                break;
            case GestureStatus.Running:
                _vm.DebugPanelWidth = _panStartWidth - e.TotalX;
                break;
        }
    }
}

