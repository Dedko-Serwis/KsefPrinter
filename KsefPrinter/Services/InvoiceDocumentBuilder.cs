using KsefPrinter.Models;
using System.Windows.Documents;

namespace KsefPrinter.Services;

/// <summary>
/// Warstwa zgodności z wcześniejszym API. Nowy kod powinien używać IInvoiceDocumentBuilder.
/// </summary>
public static class InvoiceDocumentBuilder
{
    public static FlowDocument Build(Faktura faktura, AppSettings? settings = null) =>
        new StandardInvoiceDocumentBuilder().Build(faktura, settings ?? new AppSettings());
}
