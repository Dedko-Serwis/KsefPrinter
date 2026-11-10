using KsefPrinter.Models;
using System.Windows;
using System.Windows.Documents;

namespace KsefPrinter.Services;

public sealed class SettlementInvoiceDocumentBuilder : IInvoiceDocumentBuilder
{
    public FlowDocument Build(Faktura faktura, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(faktura);
        if (faktura.RodzajFaktury != "ROZ")
            throw new ArgumentException("Oczekiwano faktury ROZ.", nameof(faktura));
        FlowDocument doc = new StandardInvoiceDocumentBuilder().Build(faktura, settings);
        // Zmiana tytułu w pierwszej komórce nagłówka, bez przebudowy danych podmiotów.
        var headerTable = doc.Blocks.OfType<Table>().FirstOrDefault();
        var firstCell = headerTable?.RowGroups[0].Rows[0].Cells[0];
        var title = firstCell?.Blocks.OfType<Section>().FirstOrDefault()?.Blocks
            .OfType<Paragraph>().FirstOrDefault();
        if (title is not null)
        {
            title.Inlines.Clear();
            title.Inlines.Add(new Bold(new Run("FAKTURA ROZLICZENIOWA")));
        }
        if (faktura.FakturyZaliczkowe.Count == 0) return doc;
        Section refs = new() { Margin = new Thickness(0, 4, 0, 7) };
        refs.Blocks.Add(AdvanceInvoiceDocumentBuilder.Heading("Rozliczenie zaliczek"));
        foreach (var reference in faktura.FakturyZaliczkowe)
        {
            if (!string.IsNullOrWhiteSpace(reference.NumerFaktury))
                AddReference(refs, "Numer faktury zaliczkowej", reference.NumerFaktury);
            if (!string.IsNullOrWhiteSpace(reference.NumerKSeF))
                AddReference(refs, "Numer KSeF faktury zaliczkowej", reference.NumerKSeF);
        }
        // Sekcja przed tabelą pozycji; kwoty należności i VAT pozostają z XML.
        var itemsTitle = doc.Blocks.OfType<Paragraph>().FirstOrDefault(p =>
            new TextRange(p.ContentStart, p.ContentEnd).Text.Trim() == "Pozycje faktury");
        if (itemsTitle is not null) doc.Blocks.InsertBefore(itemsTitle, refs);
        else doc.Blocks.Add(refs);
        return doc;
    }

    private static void AddReference(Section section, string label, string value)
    {
        section.Blocks.Add(new Paragraph(new Bold(new Run(label)))
        { Margin = new Thickness(0, 3, 0, 0), KeepWithNext = true });
        section.Blocks.Add(new Paragraph(new Run(value))
        { Margin = new Thickness(20, 0, 0, 3) });
    }
}
