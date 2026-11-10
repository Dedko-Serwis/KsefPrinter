using KsefPrinter.Models;
using System.Windows.Documents;

namespace KsefPrinter.Services;

public interface IInvoiceDocumentBuilder
{
    FlowDocument Build(Faktura faktura, AppSettings settings);
}
