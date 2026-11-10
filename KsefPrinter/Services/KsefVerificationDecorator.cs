using KsefPrinter.Models;
using QRCoder;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KsefPrinter.Services;

/// <summary>Numer KSeF i oficjalny kod QR (tryb ONLINE) z oryginalnych bajtów FA(3).</summary>
public static class KsefVerificationDecorator
{
    private static readonly Regex NumberPattern = new(
        @"^(?<nip>\d{10})-(?<date>\d{8})-[0-9A-Fa-f]{12}-[0-9A-Fa-f]{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Apply(FlowDocument document, Faktura invoice, string xmlPath,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(invoice);
        string number = Path.GetFileNameWithoutExtension(xmlPath);
        // Kopie Windows mogą mieć sufiks (1), (2) itp. Nie jest on częścią numeru KSeF.
        number = Regex.Replace(number, @"\s*\(\d+\)$", "");
        var match = NumberPattern.Match(number);
        if (!match.Success)
        {
            log?.Invoke("Brak numeru KSeF: nazwa pliku nie odpowiada formatowi KSeF. Pominięto QR.");
            return;
        }
        var sellerNip = invoice.Sprzedawca?.NIP?.Trim();
        if (!string.Equals(match.Groups["nip"].Value, sellerNip, StringComparison.Ordinal))
        {
            log?.Invoke("UWAGA: NIP w numerze KSeF nie odpowiada NIP sprzedawcy. Pominięto QR.");
            return;
        }
        if (!DateTime.TryParseExact(match.Groups["date"].Value, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var ksefDate))
        {
            log?.Invoke("Nieprawidłowa data w numerze KSeF. Pominięto QR.");
            return;
        }
        // Data w numerze KSeF to data nadania, niekoniecznie data wystawienia faktury.
        if (invoice.DataWystawienia != default && ksefDate.Date < invoice.DataWystawienia.Date)
            log?.Invoke("UWAGA: data nadania numeru KSeF jest wcześniejsza niż data wystawienia.");

        invoice.NumerKSeF = number;
        AddHeaderNumber(document, number);
        byte[] originalBytes = File.ReadAllBytes(xmlPath);
        string digest = Convert.ToBase64String(SHA256.HashData(originalBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string url = $"https://qr.ksef.mf.gov.pl/invoice/{match.Groups["nip"].Value}/{ksefDate:dd-MM-yyyy}/{digest}";
        AddQrFooter(document, number, url);
        log?.Invoke($"Numer KSeF: {number}");
        log?.Invoke($"Link weryfikacyjny: {url}");
    }

    private static void AddHeaderNumber(FlowDocument doc, string number)
    {
        // Wszystkie cztery typy faktur korzystają z nagłówka StandardInvoiceDocumentBuilder.
        var firstTable = doc.Blocks.OfType<Table>().FirstOrDefault();
        var firstCell = firstTable?.RowGroups.FirstOrDefault()?.Rows.FirstOrDefault()?.Cells.FirstOrDefault();
        var section = firstCell?.Blocks.OfType<Section>().FirstOrDefault();
        if (section is null) return;
        var invoiceNumber = section.Blocks.OfType<Paragraph>().FirstOrDefault(p =>
            new TextRange(p.ContentStart, p.ContentEnd).Text.Contains("Nr faktury"));
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 2), FontSize = 9 };
        paragraph.Inlines.Add(new Bold(new Run("Nr KSeF: ")));
        paragraph.Inlines.Add(new Run(number));
        section.Blocks.InsertAfter(invoiceNumber ?? section.Blocks.FirstBlock!, paragraph);
    }

    // Prawa połowa wiersza pozostaje bez zmian. QR zajmuje 60% tej
    // połowy (czyli 30% szerokości całego wiersza) i jest dosunięty do prawej.
    private static Table CreateRightAlignedQrBlock(Section qrSection)
    {
        var layout = new Table { CellSpacing = 0, Margin = new Thickness(0) };
        layout.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
        layout.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });

        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph())
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        });
        row.Cells.Add(new TableCell(qrSection)
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            TextAlignment = TextAlignment.Center
        });
        group.Rows.Add(row);
        layout.RowGroups.Add(group);
        return layout;
    }

    private static void AddQrFooter(FlowDocument doc, string number, string url)
    {
        using var qr = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
        byte[] png = new PngByteQRCode(qr).GetGraphic(5);
        var bitmap = new BitmapImage();
        using (var ms = new MemoryStream(png))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
        }
        bitmap.Freeze();
        var section = new Section { Margin = new Thickness(0, 8, 0, 0), TextAlignment = TextAlignment.Center };
        section.Blocks.Add(new Paragraph(new Bold(new Run("Sprawdź fakturę w KSeF")))
            { FontSize = 10, Margin = new Thickness(0, 0, 0, 4), KeepWithNext = true });
        var image = new Image { Source = bitmap, Width = 132, Height = 132, Stretch = Stretch.Uniform };
        section.Blocks.Add(new BlockUIContainer(image) { Margin = new Thickness(0, 0, 0, 2), TextAlignment = TextAlignment.Center });
        section.Blocks.Add(new Paragraph(new Run(number))
            { FontSize = 8, Margin = new Thickness(0, 0, 0, 3) });
        var link = new Hyperlink(new Run("Otwórz weryfikację faktury")) { NavigateUri = new Uri(url) };
        link.RequestNavigate += (_, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { /* Link można również skopiować z logu. */ }
        };
        section.Blocks.Add(new Paragraph(link) { FontSize = 9, Margin = new Thickness(0) });

        // VAT i rozliczenie tworzą dwukolumnową tabelę. Kod QR umieszczamy
        // pod VAT (prawa kolumna), a płatność pod rozliczeniem (lewa kolumna).
        Table? vatTable = doc.Blocks.OfType<Table>().FirstOrDefault(t =>
            new TextRange(t.ContentStart, t.ContentEnd).Text.Contains("Podsumowanie VAT"));
        if (vatTable is not null && vatTable.RowGroups.Count > 0 &&
            vatTable.RowGroups[0].Rows.Count > 0 &&
            vatTable.RowGroups[0].Rows[0].Cells.Count >= 2)
        {
            var cells = vatTable.RowGroups[0].Rows[0].Cells;
            var right = cells[1];
            right.Blocks.Add(CreateRightAlignedQrBlock(section));

            // Standardowy szablon umieszcza płatność bezpośrednio po tabeli VAT.
            // Przenosimy tylko bloki sekcji płatności, nie dodatkowe informacje.
            Block? cursor = vatTable.NextBlock;
            bool paymentStarted = false;
            while (cursor is not null)
            {
                Block? next = cursor.NextBlock;
                string text = new TextRange(cursor.ContentStart, cursor.ContentEnd).Text.Trim();
                if (!paymentStarted)
                {
                    if (!text.StartsWith("Płatność", StringComparison.Ordinal)) break;
                    paymentStarted = true;
                }
                else if (text.StartsWith("Dodatkowe informacje", StringComparison.Ordinal) ||
                         text.StartsWith("Informacje", StringComparison.Ordinal) ||
                         text.StartsWith("Stopka", StringComparison.Ordinal))
                    break;
                doc.Blocks.Remove(cursor);
                cells[0].Blocks.Add(cursor);
                cursor = next;
            }
            return;
        }

        // Bez tabeli VAT: QR w osobnej prawej kolumnie obok płatności.
        var grid = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 0) };
        grid.Columns.Add(new TableColumn { Width = new GridLength(356) });
        grid.Columns.Add(new TableColumn { Width = new GridLength(356) });
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph()) { BorderThickness = new Thickness(0) });
        row.Cells.Add(new TableCell(CreateRightAlignedQrBlock(section)) { BorderThickness = new Thickness(0) });
        group.Rows.Add(row);
        grid.RowGroups.Add(group);
        doc.Blocks.Add(grid);
    }
}
