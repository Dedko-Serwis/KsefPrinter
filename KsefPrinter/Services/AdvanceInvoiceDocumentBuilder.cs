using KsefPrinter.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace KsefPrinter.Services;

public sealed class AdvanceInvoiceDocumentBuilder : IInvoiceDocumentBuilder
{
    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    public FlowDocument Build(Faktura faktura, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(faktura);
        if (faktura.RodzajFaktury != "ZAL")
            throw new ArgumentException("Oczekiwano faktury ZAL.", nameof(faktura));
        FlowDocument doc = new StandardInvoiceDocumentBuilder().Build(faktura, settings);
        // Standard nie tworzy sekcji pozycji, jeśli nie ma FaWiersz.
        // Faktura ZAL ma ZamowienieWiersz, dlatego wstawiamy sekcję przed kwotą ogółem.
        var total = doc.Blocks.OfType<Paragraph>().FirstOrDefault(p =>
            new TextRange(p.ContentStart, p.ContentEnd).Text.Contains("Kwota należności ogółem"));
        if (total is null)
            throw new InvalidOperationException("Brak kwoty należności w szablonie faktury.");

        doc.Blocks.InsertBefore(total, Heading("Pozycje zamówienia"));
        if (faktura.PozycjeZamowienia.Count > 0)
            doc.Blocks.InsertBefore(total, BuildOrderTable(faktura.PozycjeZamowienia, settings));
        else
            doc.Blocks.InsertBefore(total, new Paragraph(new Run("Brak pozycji zamówienia w XML.")));

        if (faktura.WartoscZamowienia is decimal orderValue)
        {
            doc.Blocks.InsertBefore(total, new Paragraph(new Run(
                    $"Wartość zamówienia (XML): {orderValue.ToString("N2", Pl)} {faktura.KodWaluty}"))
                { Margin = new Thickness(0, 3, 0, 4), TextAlignment = TextAlignment.Right });
        }
        return doc;
    }

    internal static Paragraph Heading(string text) => new(new Bold(new Run(text)))
    { FontSize = 11, Margin = new Thickness(0, 7, 0, 3), KeepWithNext = true };

    private static Table BuildOrderTable(List<PozycjaZamowienia> items, AppSettings settings)
    {
        bool hasGrossPrice = items.Any(x => !string.IsNullOrWhiteSpace(x.CenaBrutto));
        Table t = new() { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 6) };
        foreach (double w in new[] { 32d, 230d, 65d, 80d, 80d, 58d, 80d, 80d })
            t.Columns.Add(new TableColumn { Width = new GridLength(w) });
        TableRowGroup group = new(); t.RowGroups.Add(group);
        TableRow header = new();
        foreach (string label in new[] { "Lp.", "Nazwa", "Ilość", "Opis j.m.",
            hasGrossPrice ? "Cena brutto" : "Cena netto", "VAT", "Netto", "Kwota VAT" })
            Cell(header, label, true);
        group.Rows.Add(header);
        foreach (var item in items)
        {
            TableRow row = new();
            Cell(row, item.Numer);
            Cell(row, FormatOrderName(item, settings.ItemNameDisplay, settings.ItemNameMaxLength));
            Cell(row, Quantity(item.Ilosc));
            Cell(row, item.OpisJednostki);
            Cell(row, Money(hasGrossPrice ? item.CenaBrutto : item.CenaNetto));
            Cell(row, item.StawkaVat);
            Cell(row, Money(item.WartoscNetto));
            Cell(row, Money(item.WartoscVat));
            group.Rows.Add(row);
        }
        return t;
    }
    private static string FormatOrderName(PozycjaZamowienia item, ItemNameDisplayMode mode, int maxChars)
    {
        string name = item.Nazwa ?? string.Empty;
        if (mode == ItemNameDisplayMode.Wrap)
            return string.IsNullOrWhiteSpace(item.Indeks) ? name : $"{name}\nIndeks: {item.Indeks}";

        // W FlowDocument samo skrócenie do 60 znaków nadal powoduje
        // zawijanie w wąskiej kolumnie. 34 znaki mieszczą się w 230 DIP
        // przy domyślnej czcionce Consolas 10.
        maxChars = Math.Clamp(maxChars, 5, 500);
        return name.Length > maxChars ? name[..(maxChars - 1)] + "…" : name;
    }

    private static string? Quantity(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("0.############################", Pl) : value;
    private static string? Money(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("N2", Pl) : value;
    private static void Cell(TableRow row, string? value, bool header = false)
    {
        Paragraph p = new() { Margin = new Thickness(2),
            TextAlignment = header ? TextAlignment.Center : TextAlignment.Left };
        if (header) p.Inlines.Add(new Bold(new Run(value ?? "")));
        else p.Inlines.Add(new Run(value ?? ""));
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0.5) });
    }
}
