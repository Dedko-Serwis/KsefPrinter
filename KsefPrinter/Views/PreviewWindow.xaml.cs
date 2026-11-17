using KsefPrinter.Services;
using KsefPrinter.Models;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
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
    private bool _fitScheduled;
    private bool _autoFitting;

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
        if (_initializing) return;
        if (_fitMode == PreviewFitMode.Custom)
        {
            SetViewerZoom(_customZoom);
            return;
        }
        ScheduleAutoFit();
    }

    private void ScheduleAutoFit()
    {
        if (_initializing || _fitMode == PreviewFitMode.Custom || _fitScheduled) return;
        _fitScheduled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _fitScheduled = false;
            FitToRenderedPage();
        }), DispatcherPriority.ContextIdle);
    }

    private void FitToRenderedPage()
    {
        if (_initializing || _fitMode == PreviewFitMode.Custom || _autoFitting) return;
        if (Viewer.ActualWidth <= 0 || Viewer.ActualHeight <= 0) return;

        // FlowDocumentPageViewer może wyświetlać stronę w innej skali niż
        // wynikałoby to z FlowDocument.PageWidth/PageHeight.
        // Mierzymy faktycznie wyrenderowaną stronę, a nie rozmiar dokumentu.
        var page = FindVisualChild<DocumentPageView>(Viewer);
        if (page is null || page.ActualWidth <= 0 || page.ActualHeight <= 0) return;

        Rect bounds = page.TransformToAncestor(Viewer)
            .TransformBounds(new Rect(0, 0, page.ActualWidth, page.ActualHeight));
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        double availableWidth = Math.Max(1, Viewer.ActualWidth - 50);
        double availableHeight = Math.Max(1, Viewer.ActualHeight - 65);
        double ratio = _fitMode == PreviewFitMode.Width
            ? availableWidth / bounds.Width
            : availableHeight / bounds.Height;
        if (!double.IsFinite(ratio) || Math.Abs(1 - ratio) < 0.025) return;

        double targetZoom = Math.Clamp(Viewer.Zoom * ratio, Viewer.MinZoom, Viewer.MaxZoom);
        if (Math.Abs(targetZoom - Viewer.Zoom) < 0.2) return;

        _autoFitting = true;
        try { SetViewerZoom(targetZoom); }
        finally { _autoFitting = false; }

        // Po zmianie Zoom układ strony aktualizuje się asynchronicznie.
        // Kolejny pomiar koryguje ewentualny błąd (maks. kilka iteracji).
        if (Math.Abs(targetZoom - Viewer.MinZoom) > 0.1 &&
            Math.Abs(targetZoom - Viewer.MaxZoom) > 0.1)
            ScheduleAutoFit();
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
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
            ScheduleAutoFit();
    }

    private void Viewer_ZoomChanged(object? sender, EventArgs e)
    {
        if (_initializing || _applyingZoom || _autoFitting) return;
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
