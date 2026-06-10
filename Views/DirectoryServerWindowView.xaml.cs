using System.ComponentModel;
using Irc7m.ViewModels;

namespace Irc7m.Views;

public partial class DirectoryServerWindowView : ContentView
{
    private DirectoryServerViewModel? _vm;
    private double _panStartWidth;

    public DirectoryServerWindowView()
    {
        InitializeComponent();
    }

    /// <summary>Delegates focus to the embedded chat input.</summary>
    public void FocusInput() => ChatView.FocusInput();

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = BindingContext as DirectoryServerViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DirectoryServerViewModel.DebugText))
        {
            await MainThread.InvokeOnMainThreadAsync(ScrollDebugToBottom);
        }
        else if (e.PropertyName == nameof(DirectoryServerViewModel.IsDebugMode)
                 && _vm?.IsDebugMode == true)
        {
            // Default the panel to half the current view width each time it opens
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

    // ── Resize handle drag ─────────────────────────────────────────────────────

    private void OnDividerPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_vm is null) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartWidth = _vm.DebugPanelWidth;
                break;

            case GestureStatus.Running:
                // Drag left (negative TotalX) = expand; drag right = shrink
                _vm.DebugPanelWidth = _panStartWidth - e.TotalX;
                break;
        }
    }
}

