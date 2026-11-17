using KsefPrinter.Models;
using KsefPrinter.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KsefPrinter.Views;

public partial class PreviewWindow : Window
{
    private const double MinZoom = 25;
    private const double MaxZoom = 300;
    private const double ZoomStep = 10;
    private const double PageMargin = 24;

    public event Action? PrintingStarted;
    public event Action<bool>? PrintingFinished;
    public bool PrintWasRequested { get; private set; }

    private readonly FlowDocument _document;
    private readonly SettingsService _settingsService;
    private readonly DocumentPaginator _paginator;
    private PreviewFitMode _fitMode = PreviewFitMode.Height;
    private bool _initializing = true;
    private bool _settingZoomText;
    private double _customZoom = 100;
    private double _currentZoom = 100;
    private int _pageNumber;
    private int _pageCount = 1;

    public PreviewWindow(FlowDocument document, SettingsService? settingsService = null)
    {
        InitializeComponent();
        _document = document;
        _settingsService = settingsService ?? new SettingsService();

        _paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        double pageWidth = double.IsFinite(document.PageWidth) && document.PageWidth > 0
            ? document.PageWidth : 793.7;
        double pageHeight = double.IsFinite(document.PageHeight) && document.PageHeight > 0
            ? document.PageHeight : 1122.5;
        _paginator.PageSize = new Size(pageWidth, pageHeight);
        PageContainer.Width = pageWidth;
        PageContainer.Height = pageHeight;
        PageView.Width = pageWidth;
        PageView.Height = pageHeight;
        PageView.DocumentPaginator = _paginator;
        _paginator.ComputePageCount();
        _pageCount = Math.Max(1, _paginator.PageCount);
        UpdatePageNavigation();

        Loaded += RestorePreview;
        Closing += SavePreview;
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
            ? Math.Clamp(s.PreviewZoom, MinZoom, MaxZoom) : 100;
        _fitMode = Enum.IsDefined(s.PreviewFitMode) ? s.PreviewFitMode : PreviewFitMode.Height;
        if (s.PreviewMaximized) WindowState = WindowState.Maximized;

        _initializing = false;
        FitModeCombo.SelectedIndex = (int)_fitMode;
        Dispatcher.BeginInvoke(new Action(ApplyPreviewMode), DispatcherPriority.Loaded);
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

        // Skala dotyczy wyłącznie widoku, nie rozmiaru strony dokumentu.
        double availableWidth = Math.Max(1, PreviewScroll.ActualWidth - PageMargin - 18);
        double availableHeight = Math.Max(1, PreviewScroll.ActualHeight - PageMargin - 18);
        double zoom = 100 * (_fitMode == PreviewFitMode.Width
            ? availableWidth / PageContainer.Width
            : availableHeight / PageContainer.Height);
        SetViewerZoom(zoom);
    }

    private void SetViewerZoom(double zoom)
    {
        if (!double.IsFinite(zoom)) return;
        _currentZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        PageScale.ScaleX = _currentZoom / 100;
        PageScale.ScaleY = _currentZoom / 100;
        _settingZoomText = true;
        try
        {
            ZoomCombo.SelectedIndex = -1;
            ZoomCombo.Text = $"{_currentZoom:0.#}%";
        }
        finally { _settingZoomText = false; }
    }

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initializing && _fitMode != PreviewFitMode.Custom)
            Dispatcher.BeginInvoke(new Action(ApplyPreviewMode), DispatcherPriority.Loaded);
    }

    private void FitModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || FitModeCombo.SelectedIndex < 0) return;
        _fitMode = (PreviewFitMode)FitModeCombo.SelectedIndex;
        ApplyPreviewMode();
    }

    private void SetCustomZoom(double zoom)
    {
        if (!double.IsFinite(zoom)) return;
        _customZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _fitMode = PreviewFitMode.Custom;
        FitModeCombo.SelectedIndex = (int)_fitMode;
        SetViewerZoom(_customZoom);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetCustomZoom(_currentZoom - ZoomStep);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetCustomZoom(_currentZoom + ZoomStep);

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _settingZoomText || ZoomCombo.SelectedItem is not ComboBoxItem item) return;
        if (TryParseZoom(item.Content?.ToString(), out var zoom)) SetCustomZoom(zoom);
    }

    private void ZoomCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitZoomText();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void ZoomCombo_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitZoomText();

    private void CommitZoomText()
    {
        if (_initializing || _settingZoomText) return;
        if (TryParseZoom(ZoomCombo.Text, out var zoom)) SetCustomZoom(zoom);
        else SetViewerZoom(_currentZoom);
    }

    private static bool TryParseZoom(string? text, out double zoom)
    {
        string value = (text ?? "").Trim().TrimEnd('%').Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out zoom)
            && double.IsFinite(zoom);
    }

    private void UpdatePageNavigation()
    {
        PageView.PageNumber = _pageNumber;
        PageCounter.Text = $"{_pageNumber + 1} z {_pageCount}";
        PreviousPageButton.IsEnabled = _pageNumber > 0;
        NextPageButton.IsEnabled = _pageNumber + 1 < _pageCount;
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageNumber <= 0) return;
        _pageNumber--;
        UpdatePageNavigation();
        PreviewScroll.ScrollToTop();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageNumber + 1 >= _pageCount) return;
        _pageNumber++;
        UpdatePageNavigation();
        PreviewScroll.ScrollToTop();
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
        finally { PrintButton.IsEnabled = true; }
    }
}
