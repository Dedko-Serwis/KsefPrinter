using System;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace KsefPrinter.Services
{
    public class InvoicePrinter(SettingsService settingsService)
    {
        private readonly SettingsService _settingsService = settingsService;

        // Zachowanie zgodności z istniejącymi wywołaniami.
        public async Task PrintAsync(FlowDocument document)
        {
            await PrintWithResultAsync(document);
        }

        // true = zadanie zostało przekazane do systemu drukowania.
        public async Task<bool> PrintWithResultAsync(FlowDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            var settings = _settingsService.Load();
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                MessageBox.Show("Nie wybrano drukarki w ustawieniach", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // FlowDocument należy obsługiwać na wątku UI (STA).
            return await Application.Current.Dispatcher.InvokeAsync(
                () => PrintOnPrinter(document, settings.PrinterName),
                DispatcherPriority.Background).Task;
        }

        private static bool PrintOnPrinter(FlowDocument document, string printerName)
        {
            try
            {
                using var printServer = new LocalPrintServer();
                PrintQueue? selectedQueue = null;
                foreach (PrintQueue queue in printServer.GetPrintQueues())
                {
                    if (string.Equals(queue.Name, printerName, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedQueue = queue;
                        break;
                    }
                }

                if (selectedQueue is null)
                    throw new InvalidOperationException($"Drukarka '{printerName}' nie została znaleziona");

                var dialog = new PrintDialog { PrintQueue = selectedQueue };
                dialog.PrintDocument(
                    ((IDocumentPaginatorSource)document).DocumentPaginator, "Faktura");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd drukowania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
