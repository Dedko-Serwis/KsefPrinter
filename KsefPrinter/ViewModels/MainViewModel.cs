using KsefPrinter.Infrastructure;
using KsefPrinter.Models;
using KsefPrinter.Services;
using KsefPrinter.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Printing;
using System.Reflection;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace KsefPrinter.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly LoggerService _logger;
    private readonly SettingsService _settingsService;
    private readonly IInvoiceDocumentBuilder _invoiceDocumentBuilder;
    private string _status = "Gotowy";
    private bool _isDragging = false;
    private int _printedInBatch;
    private int _batchTotal;
    private int _loadedCount, _validCount, _invalidCount;
    private bool _currentFileValidated;
    private bool _isProcessingBatch;
    public string LoadedStatus => $"Wczytano {_loadedCount}/{_batchTotal}";
    public string ValidStatus => $"Poprawne {_validCount}/{_batchTotal}";
    public string InvalidStatus => $"Błędne {_invalidCount}/{_batchTotal}";
    public string PrintedStatus => $"Wydrukowane {_printedInBatch}/{_batchTotal}";
    private void RefreshCounters()
    {
        OnPropertyChanged(nameof(LoadedStatus));
        OnPropertyChanged(nameof(ValidStatus));
        OnPropertyChanged(nameof(InvalidStatus));
        OnPropertyChanged(nameof(PrintedStatus));
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }

    public ObservableCollection<string> LogItems { get; } = [];
    public SettingsViewModel Settings { get; } = new();
    public UpdateViewModel Update { get; } = new();
    public ICommand DropFilesCommand { get; private set; } = null!;

    public MainViewModel()
    {
        _logger = new LoggerService();
        _settingsService = new SettingsService();
        _invoiceDocumentBuilder = new StandardInvoiceDocumentBuilder();

        InitializeCommands();
        ShowPrinterInfo();
        Settings.Saved += (_, _) => ShowPrinterInfo();
    }

    private void InitializeCommands()
    {

        DropFilesCommand = new RelayCommand(async files =>
        {
            if (files is not string[] paths || paths.Length == 0)
                return;

            // Zapobiega uruchomieniu drugiej kolejki w trakcie obsługi pierwszej.
            if (_isProcessingBatch)
            {
                AddLog("Trwa przetwarzanie poprzedniej partii plików.");
                return;
            }

            _isProcessingBatch = true;
            try
            {
                _batchTotal = paths.Length;
                _printedInBatch = 0;
                _loadedCount = _validCount = _invalidCount = 0;
                RefreshCounters();

                for (int i = 0; i < paths.Length; i++)
                    await ProcessFile(paths[i], i + 1, paths.Length);

                Status = "Gotowy";
            }
            catch (Exception ex)
            {
                AddLog($"BŁĄD kolejki: {ex.Message}");
                Status = "Błąd kolejki";
            }
            finally
            {
                _isProcessingBatch = false;
            }
        });
    }

    private void ShowPrinterInfo()
    {
        var settings = _settingsService.Load();

        if (string.IsNullOrWhiteSpace(settings.PrinterName))
        {
            AddLog("Nie wybrano drukarki. Otwórz Ustawienia i wybierz drukarkę.");
            return;
        }

        var server = new LocalPrintServer();

        bool exists = server
            .GetPrintQueues()
            .Any(q => q.Name == settings.PrinterName);
    }

    public async Task ProcessFile(string path) => await ProcessFile(path, 1, 1);

    private async Task ProcessFile(string path, int current, int total)
    {
        _currentFileValidated = false;
        Status = $"Wczytuję {current}/{total}";
        AddLog($"Wczytuję: {Path.GetFileName(path)}");
        try
        {
            // Oddaj sterowanie UI, aby pasek stanu odświeżył się przed operacją.
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            var faktura = KsefInvoiceReader.Load(path);
            _loadedCount++;
            RefreshCounters();
            AddLog($"\t\t Faktura: {faktura.NumerFaktury}");
            // Nazwa pliku jest jedynym źródłem numeru KSeF w tym trybie.
            // Bez poprawnego numeru nie wolno tworzyć pozornie wiarygodnego QR.
            if (!KsefFileNameValidator.TryValidate(path, faktura.Sprzedawca?.NIP, out string validationError))
                throw new InvalidDataException(validationError);
            _validCount++;
            _currentFileValidated = true;
            RefreshCounters();

            Status = $"Generuję {current}/{total}";
            AddLog($"Generuję wydruk: {faktura.NumerFaktury}");
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            var settings = _settingsService.Load();
            var document = (faktura.RodzajFaktury switch
            {
                "KOR" => new CorrectionInvoiceDocumentBuilder(),
                "ZAL" => new AdvanceInvoiceDocumentBuilder(),
                "ROZ" => new SettlementInvoiceDocumentBuilder(),
                _ => _invoiceDocumentBuilder
            }).Build(faktura, settings);

            KsefVerificationDecorator.Apply(document, faktura, path, message => AddLog(message));

            if (settings.ShowPrintPreview)
            {
                Status = $"Podgląd {current}/{total}";
                AddLog($"Otwieram podgląd: {faktura.NumerFaktury}");
                var mainWindow = Application.Current.MainWindow;
                var preview = new PreviewWindow(document, _settingsService);
                if (mainWindow != null && mainWindow != preview)
                {
                    preview.Owner = mainWindow;
                    preview.ShowInTaskbar = false;
                }
                preview.PrintingStarted += () =>
                {
                    Status = $"Drukuję {current}/{total}";
                    AddLog($"Drukuję: {faktura.NumerFaktury}");
                };
                preview.PrintingFinished += success =>
                {
                    if (success)
                    {
                        _printedInBatch++;
                        RefreshCounters();
                        Status = $"Wysłano {_printedInBatch}/{total}";
                        AddLog($"Wysłano do drukarki: {faktura.NumerFaktury}");
                    }
                    else
                    {
                        Status = $"Błąd druku {current}/{total}";
                        AddLog($"BŁĄD drukowania: {faktura.NumerFaktury}");
                    }
                };
                // Okno główne pozostaje widoczne, ale nie zasłania podglądu.
                // Przywrócenie Topmost następuje również po wyjątku z ShowDialog.
                bool previousTopmost = mainWindow?.Topmost ?? false;
                try
                {
                    mainWindow?.Topmost = false;
                    preview.Topmost = true;
                    preview.ShowDialog();
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.Topmost = previousTopmost;
                        mainWindow.Activate();
                    }
                }
                if (!preview.PrintWasRequested)
                {
                    Status = $"Zamknięto {current}/{total}";
                    AddLog($"Zamknięto podgląd bez drukowania: {faktura.NumerFaktury}");
                }
            }
            else
            {
                Status = $"Drukuję {current}/{total}";
                AddLog($"Drukuję: {faktura.NumerFaktury}");
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                bool success = await new InvoicePrinter(_settingsService).PrintWithResultAsync(document);
                if (success)
                {
                    _printedInBatch++;
                    RefreshCounters();
                    Status = $"Wysłano {_printedInBatch}/{total}";
                    AddLog($"Wysłano do drukarki: {faktura.NumerFaktury}");
                }
                else
                    Status = $"Błąd druku {current}/{total}";
            }
        }
        catch (KsefInvoiceReadException ex)
        {
            _invalidCount++;
            RefreshCounters();
            AddLog($"BŁĄD XML: {ex.Message}");
            Status = $"Błąd odczytu {current}/{total}";
        }
        catch (Exception ex)
        {
            if (!_currentFileValidated)
            {
                _invalidCount++;
                RefreshCounters();
            }
            AddLog($"BŁĄD: {ex.Message}");
            Status = $"Błąd {current}/{total}";
        }
    }

    public void AddLog(string text)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (LogItems.Count > 500)
            {
                LogItems.Clear();
            }
            LogItems.Add($"{DateTime.Now:HH:mm:ss} {text}");
        });
        _logger.Log(text);
    }

}