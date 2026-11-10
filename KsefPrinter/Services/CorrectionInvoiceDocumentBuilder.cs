using KsefPrinter.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace KsefPrinter.Services;

/// <summary>
/// Widok korekty FA(3). Wspólny układ części formalnej jest pobierany
/// ze sprawdzonego szablonu VAT, natomiast tabela korekty jest osobna.
/// </summary>
public sealed class CorrectionInvoiceDocumentBuilder : IInvoiceDocumentBuilder
{
    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    public FlowDocument Build(Faktura faktura, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(faktura);
        if (faktura.RodzajFaktury != "KOR")
            throw new ArgumentException("Oczekiwano faktury KOR.", nameof(faktura));

        // Zachowujemy wszystkie istniejące sekcje (VAT, rozliczenie,
        // płatność, dodatkowe informacje) i ich dotychczasowy wygląd.
        FlowDocument doc = new StandardInvoiceDocumentBuilder().Build(faktura, settings);

        // Sekcja z fakturą pierwotną bezpośrednio pod nagłówkiem.
        Section references = new() { Margin = new Thickness(0, 2, 0, 8) };
        references.Blocks.Add(new Paragraph(new Bold(new Run("Dane korekty")))
        { FontSize = 11, Margin = new Thickness(0, 0, 0, 3), KeepWithNext = true });
        AddLine(references, "Przyczyna korekty", faktura.PrzyczynaKorekty);
        AddLine(references, "Typ korekty", faktura.TypKorekty);
        foreach (var original in faktura.FakturyKorygowane)
        {
            AddIndentedValue(references, "Nr faktury korygowanej", original.NumerFaktury);
            if (original.DataWystawienia.HasValue)
                AddLine(references, "Data faktury korygowanej",
                    original.DataWystawienia.Value.ToString("dd.MM.yyyy", Pl));
            AddIndentedValue(references, "Nr faktury KSeF korygowanej", original.NumerKSeF);
        }
        // Dane korekty i nabywca mają wspólną górną krawędź.
        // W szablonie Standard druga tabela zawiera podmioty.
        var partiesTable = doc.Blocks.OfType<Table>().Skip(1).FirstOrDefault();
        if (partiesTable is not null && partiesTable.RowGroups.Count > 0
            && partiesTable.RowGroups[0].Rows.Count > 0)
        {
            var rows = partiesTable.RowGroups[0].Rows;
            var firstRow = rows[0];
            // Przenosimy nabywcę do nowego wiersza razem z danymi korekty.
            // Pozostałe podmioty zachowujemy w kolejnych wierszach.
            TableCell buyer = firstRow.Cells[1];
            firstRow.Cells.Remove(buyer);
            bool hasThirdParty = firstRow.Cells[0].Blocks
                .OfType<Paragraph>().Any(p =>
                    !string.IsNullOrWhiteSpace(new TextRange(p.ContentStart, p.ContentEnd).Text));
            if (!hasThirdParty)
                rows.Remove(firstRow);
            else
                firstRow.Cells.Add(new TableCell(new Paragraph()));

            TableRow correctionRow = new();
            correctionRow.Cells.Add(new TableCell(references)
            {
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            });
            correctionRow.Cells.Add(buyer);
            rows.Insert(0, correctionRow);
        }
        else
        {
            doc.Blocks.Add(references);
        }

        // Zamień tabelę pozycji, nie zmieniając układu sprzedawcy i nabywcy.
        var blocks = doc.Blocks.Cast<Block>().ToList();
        var total = blocks.OfType<Paragraph>().FirstOrDefault(p =>
            new TextRange(p.ContentStart, p.ContentEnd).Text.Contains("Kwota należności ogółem"));
        if (total is not null)
        {
            int totalIndex = blocks.IndexOf(total);
            Table? oldItems = blocks.Take(totalIndex).OfType<Table>().LastOrDefault();
            if (oldItems is not null)
            {
                doc.Blocks.InsertBefore(oldItems, new Paragraph(
                    new Bold(new Run("Pozycje faktury korygującej")))
                {
                    Margin = new Thickness(0, 7, 0, 3),
                    FontSize = 11,
                    KeepWithNext = true
                });
                doc.Blocks.InsertBefore(oldItems, BuildCorrectionTable(faktura.Pozycje, settings));
                doc.Blocks.Remove(oldItems);
            }
            var oldTitle = doc.Blocks.OfType<Paragraph>().FirstOrDefault(p =>
                new TextRange(p.ContentStart, p.ContentEnd).Text.Trim() == "Pozycje faktury");
            if (oldTitle is not null) doc.Blocks.Remove(oldTitle);
        }

        // Usuń pusty nagłówek stopki (gdy nie ma żadnej treści).
        var infoTitle = doc.Blocks.OfType<Paragraph>().FirstOrDefault(p =>
            new TextRange(p.ContentStart, p.ContentEnd).Text.Trim() == "Informacje");
        if (infoTitle is not null && infoTitle.NextBlock is null)
            doc.Blocks.Remove(infoTitle);
        foreach (var section in doc.Blocks.OfType<Section>().ToList())
        {
            var paragraphs = section.Blocks.OfType<Paragraph>().ToList();
            if (section.Blocks.Count == 1 && paragraphs.Count == 1 &&
                new TextRange(paragraphs[0].ContentStart, paragraphs[0].ContentEnd)
                    .Text.Trim() == "Informacje")
                doc.Blocks.Remove(section);
        }
        return doc;
    }

    private static void AddIndentedValue(Section section, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        section.Blocks.Add(new Paragraph(new Bold(new Run(label + ":")))
        { Margin = new Thickness(0, 2, 0, 0), KeepWithNext = true });
        section.Blocks.Add(new Paragraph(new Run(value))
        { Margin = new Thickness(22, 0, 0, 3) });
    }

    private static void AddLine(Section section, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Paragraph p = new() { Margin = new Thickness(0, 1, 0, 1) };
        p.Inlines.Add(new Bold(new Run(label + ": ")));
        p.Inlines.Add(new Run(value));
        section.Blocks.Add(p);
    }

    private static Table BuildCorrectionTable(List<PozycjaFaktury> items, AppSettings settings)
    {
        Table table = new() { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 8) };
        foreach (double width in new[] { 30d, 192d, 58d, 58d, 67d, 45d, 83d, 83d, 83d })
            table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        TableRowGroup group = new();
        table.RowGroups.Add(group);
        TableRow header = new();
        foreach (string h in new[] { "Lp.", "Nazwa", "Ilość\nprzed", "Ilość\npo",
            "Różnica\nilości", "J.m.", "Brutto\nprzed", "Brutto\npo", "Różnica\nbrutto" })
            Cell(header, h, true);
        group.Rows.Add(header);

        // UU_ID jest najlepszym kluczem parowania, NrWierszaFa stanowi
        // rezerwę. Nie sumujemy wierszy bez wiarygodnego identyfikatora.
        var indexed = items.Select((item, index) => new { item, index }).ToList();
        var groups = indexed.GroupBy(x =>
            !string.IsNullOrWhiteSpace(x.item.UU_ID) ? "U:" + x.item.UU_ID :
            !string.IsNullOrWhiteSpace(x.item.NrWiersza) ? "N:" + x.item.NrWiersza :
            "I:" + x.index.ToString(CultureInfo.InvariantCulture));

        foreach (var pair in groups)
        {
            var before = pair.Where(x => x.item.StanPrzed).Select(x => x.item).ToList();
            var after = pair.Where(x => !x.item.StanPrzed).Select(x => x.item).ToList();
            // Wielokrotne wiersze o tym samym kluczu zachowujemy oddzielnie.
            int count = Math.Max(before.Count, after.Count);
            for (int i = 0; i < count; i++)
            {
                var b = i < before.Count ? before[i] : null;
                var a = i < after.Count ? after[i] : null;
                var item = a ?? b!;
                TableRow row = new();
                Cell(row, item.NrWiersza);
                var name = item.NazwaTowaru ?? "";
                if (settings.ItemNameDisplay == ItemNameDisplayMode.Truncate)
                {
                    int limit = Math.Clamp(settings.ItemNameMaxLength, 5, 500);
                    if (name.Length > limit) name = name[..(limit - 1)] + "…";
                }
                Cell(row, name);
                Cell(row, Quantity(b?.Ilosc), numeric: true);
                Cell(row, Quantity(a?.Ilosc), numeric: true);
                decimal? qtyBefore = Parse(b?.Ilosc), qtyAfter = Parse(a?.Ilosc);
                Cell(row, qtyBefore.HasValue && qtyAfter.HasValue
                    ? (qtyAfter.Value - qtyBefore.Value).ToString("0.######", Pl) : null, numeric: true);
                Cell(row, item.Miara);
                Cell(row, Money(b?.WartoscBrutto), numeric: true);
                Cell(row, Money(a?.WartoscBrutto), numeric: true);
                decimal? grossBefore = Parse(b?.WartoscBrutto);
                decimal? grossAfter = Parse(a?.WartoscBrutto);
                Cell(row, grossBefore.HasValue && grossAfter.HasValue
                    ? (grossAfter.Value - grossBefore.Value).ToString("N2", Pl) : null, numeric: true);
                group.Rows.Add(row);
            }
        }
        return table;
    }

    private static decimal? Parse(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string? Quantity(string? s) =>
        Parse(s) is decimal value ? value.ToString("0.############################", Pl) : s;

    private static string? Money(string? s) =>
        Parse(s) is decimal value ? value.ToString("N2", Pl) : null;

    private static void Cell(TableRow row, string? text, bool heading = false, bool numeric = false)
    {
        Paragraph p = new() { Margin = new Thickness(2),
            TextAlignment = heading ? TextAlignment.Center : numeric ? TextAlignment.Right : TextAlignment.Left };
        p.Inlines.Add(heading ? new Bold(new Run(text ?? "")) : new Run(text ?? ""));
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0.5) });
    }
}
