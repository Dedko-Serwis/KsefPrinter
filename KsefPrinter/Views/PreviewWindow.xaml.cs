using KsefPrinter.Services;
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

    public PreviewWindow(FlowDocument document, SettingsService? settingsService = null)
    {
        InitializeComponent();
        _document = document;
        _settingsService = settingsService ?? new SettingsService();
        Viewer.Document = document;
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
        if (double.IsFinite(s.PreviewZoom))
            Viewer.Zoom = Math.Clamp(s.PreviewZoom, Viewer.MinZoom, Viewer.MaxZoom);
        if (s.PreviewMaximized) WindowState = WindowState.Maximized;
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
        s.PreviewZoom = Viewer.Zoom;
        s.PreviewMaximized = WindowState == WindowState.Maximized;
        _settingsService.Save(s);
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
