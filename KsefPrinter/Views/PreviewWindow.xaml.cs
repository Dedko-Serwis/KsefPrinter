using KsefPrinter.Services;
using KsefPrinter.Models;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Documents;

namespace KsefPrinter.Views;

public partial class PreviewWindow : Window
{
    public event Action? PrintingStarted;
    public event Action<bool>? PrintingFinished;
    public bool PrintWasRequested { get; private set; }
    private readonly FlowDocument _document;
    private readonly SettingsService _settingsService;
    private PreviewFitMode _fitMode = PreviewFitMode.Height;
    private bool _initializing = true;
    private bool _applyingZoom;
    private double _customZoom = 100;

    public PreviewWindow(FlowDocument document, SettingsService? settingsService = null)
    {
        InitializeComponent();
        _document = document;
        _settingsService = settingsService ?? new SettingsService();
        Viewer.Document = document;
        Loaded += RestorePreview;
        Closing += SavePreview;
        DependencyPropertyDescriptor.FromProperty(FlowDocumentPageViewer.ZoomProperty, typeof(FlowDocumentPageViewer))
            .AddValueChanged(Viewer, Viewer_ZoomChanged);
    }

    private void RestorePreview(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Load();
        if (double.IsFinite(s.PreviewWidth) && s.PreviewWidth >= 300 && s.PreviewWidth < 10000) Width = s.PreviewWidth;
        if (double.IsFinite(s.PreviewHeight) && s.PreviewHeight >= 300 && s.PreviewHeight < 10000) Height = s.PreviewHeight;
        if (double.IsFinite(s.PreviewLeft) && double.IsFinite(s.PreviewTop) && s.PreviewLeft >= 0 && s.PreviewTop >= 0)
        {
            var bounds = new Rect(s.PreviewLeft, s.PreviewTop, Width, Height);
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (bounds.IntersectsWith(screen))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = s.PreviewLeft;
                Top = s.PreviewTop;
            }
        }
        _customZoom = s.RememberPreviewZoom && double.IsFinite(s.PreviewZoom)
            ? Math.Clamp(s.PreviewZoom, Viewer.MinZoom, Viewer.MaxZoom) : 100;
        _fitMode = Enum.IsDefined(s.PreviewFitMode) ? s.PreviewFitMode : PreviewFitMode.Height;
        if (s.PreviewMaximized) WindowState = WindowState.Maximized;
        _initializing = false;
        FitModeCombo.SelectedIndex = (int)_fitMode;
        Dispatcher.BeginInvoke(ApplyPreviewMode, DispatcherPriority.Loaded);
    }

    private void SavePreview(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var s = _settingsService.Load();
        var bounds = RestoreBounds;
        if (double.IsFinite(bounds.Width) && bounds.Width > 0) s.PreviewWidth = bounds.Width;
        if (double.IsFinite(bounds.Height) && bounds.Height > 0) s.PreviewHeight = bounds.Height;
        if (double.IsFinite(bounds.Left) && double.IsFinite(bounds.Top))
        {
            s.PreviewLeft = bounds.Left;
            s.PreviewTop = bounds.Top;
        }
        s.PreviewFitMode = _fitMode;
        if (s.RememberPreviewZoom) s.PreviewZoom = _customZoom;
        s.PreviewMaximized = WindowState == WindowState.Maximized;
        _settingsService.Save(s);
    }


    private void ApplyPreviewMode()
    {
        if (_initializing || Viewer.ActualWidth <= 0 || Viewer.ActualHeight <= 0) return;

        double zoom = _customZoom;
        if (_fitMode != PreviewFitMode.Custom)
        {
            double pageWidth = _document.PageWidth;
            double pageHeight = _document.PageHeight;
            if (!double.IsFinite(pageWidth) || pageWidth <= 0) pageWidth = 793.7;
            if (!double.IsFinite(pageHeight) || pageHeight <= 0) pageHeight = 1122.5;

            // Rezerwa na marginesy i pasek przewijania/sterowania PageViewer.
            double availableWidth = Math.Max(1, Viewer.ActualWidth - 70);
            double availableHeight = Math.Max(1, Viewer.ActualHeight - 80);
            zoom = 100 * (_fitMode == PreviewFitMode.Width
                ? availableWidth / pageWidth : availableHeight / pageHeight);
        }
        SetViewerZoom(zoom);
    }

    private void SetViewerZoom(double zoom)
    {
        if (!double.IsFinite(zoom)) return;
        _applyingZoom = true;
        try
        {
            Viewer.Zoom = Math.Clamp(zoom, Viewer.MinZoom, Viewer.MaxZoom);
            ZoomCombo.Text = $"{Viewer.Zoom:0.#}%";
        }
        finally { _applyingZoom = false; }
    }

    private void Viewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initializing && _fitMode != PreviewFitMode.Custom)
            Dispatcher.BeginInvoke(ApplyPreviewMode, DispatcherPriority.Loaded);
    }

    private void Viewer_ZoomChanged(object? sender, EventArgs e)
    {
        if (_initializing || _applyingZoom) return;
        // Zmiana przez wbudowany pasek PageViewer także oznacza tryb ręczny.
        _customZoom = Viewer.Zoom;
        _fitMode = PreviewFitMode.Custom;
        FitModeCombo.SelectedIndex = (int)_fitMode;
        ZoomCombo.Text = $"{Viewer.Zoom:0.#}%";
    }

    private void FitModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || FitModeCombo.SelectedIndex < 0) return;
        _fitMode = (PreviewFitMode)FitModeCombo.SelectedIndex;
        ApplyPreviewMode();
    }

    private void SetCustomZoom(double zoom)
    {
        _customZoom = Math.Clamp(zoom, Viewer.MinZoom, Viewer.MaxZoom);
        _fitMode = PreviewFitMode.Custom;
        FitModeCombo.SelectedIndex = (int)_fitMode;
        SetViewerZoom(_customZoom);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) =>
        SetCustomZoom(Viewer.Zoom - Viewer.ZoomIncrement);

    private void ZoomIn_Click(object sender, RoutedEventArgs e) =>
        SetCustomZoom(Viewer.Zoom + Viewer.ZoomIncrement);

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ZoomCombo.SelectedItem is not ComboBoxItem item) return;
        if (TryParseZoom(item.Content?.ToString(), out var zoom)) SetCustomZoom(zoom);
    }

    private void ZoomCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitZoomText();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void ZoomCombo_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CommitZoomText();

    private void CommitZoomText()
    {
        if (_initializing) return;
        if (TryParseZoom(ZoomCombo.Text, out var zoom)) SetCustomZoom(zoom);
        else ZoomCombo.Text = $"{Viewer.Zoom:0.#}%";
    }

    private static bool TryParseZoom(string? text, out double zoom)
    {
        string value = (text ?? "").Trim().TrimEnd('%').Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out zoom)
            && double.IsFinite(zoom);
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        PrintButton.IsEnabled = false;
        PrintWasRequested = true;
        PrintingStarted?.Invoke();
        try
        {
            bool success = await new InvoicePrinter(_settingsService).PrintWithResultAsync(_document);
            PrintingFinished?.Invoke(success);
        }
        catch (Exception ex)
        {
            PrintingFinished?.Invoke(false);
            MessageBox.Show(this, $"Błąd drukowania: {ex.Message}",
                "KsefPrinter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PrintButton.IsEnabled = true;
        }
    }
}
