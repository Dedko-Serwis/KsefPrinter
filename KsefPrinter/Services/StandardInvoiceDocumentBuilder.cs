using KsefPrinter.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace KsefPrinter.Services;

/// <summary>
/// Standardowy, czytelny wydruk zwykłej faktury VAT.
/// Builder korzysta wyłącznie z danych odczytanych z XML; wyjątkiem jest
/// wartość brutto w podsumowaniu VAT (netto + VAT), zgodnie z ustaloną prezentacją.
/// </summary>
public sealed class StandardInvoiceDocumentBuilder : IInvoiceDocumentBuilder
{
    public FlowDocument Build(Faktura faktura, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(faktura);
        settings ??= new AppSettings();

        FlowDocument doc = new()
        {
            PageWidth = 793.7,
            PageHeight = 1122.5,
            MinPageWidth = 793.7,
            MaxPageWidth = 793.7,
            ColumnWidth = 10000,
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.Normal,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        AddHeaderWithSeller(doc, faktura);
        AddPartiesGrid(doc, faktura);
        AddItems(doc, faktura, settings);
        AddTotalAmount(doc, faktura);
        AddVatAndSettlement(doc, faktura);
        AddPayment(doc, faktura);
        AddAdditionalInformation(doc, faktura);
        AddFooter(doc, faktura);

        return doc;
    }

    // Dwa pola w nagłówku; nie rezerwujemy miejsca dla nieistniejących danych.
    private static void AddHeaderWithSeller(FlowDocument doc, Faktura f)
    {
        Section details = NewSection();
        Paragraph title = new(new Bold(new Run(GetDocumentTitle(f.RodzajFaktury))))
        {
            FontSize = 15, Margin = new Thickness(0, 0, 0, 5)
        };
        details.Blocks.Add(title);
        AddLabelValue(details, "Nr faktury", f.NumerFaktury, boldValue: true);
        AddLabelValue(details, "Data wystawienia", FormatDate(f.DataWystawienia));
        AddLabelValue(details, "Data sprzedaży/dostawy", FormatDate(f.DataDostawy));
        if (f.DataDostawyOd.HasValue || f.DataDostawyDo.HasValue)
        {
            string period = $"{FormatDate(f.DataDostawyOd)} – {FormatDate(f.DataDostawyDo)}".Trim(' ', '–');
            AddLabelValue(details, "Okres faktury", period);
        }
        AddLabelValue(details, "Miejsce wystawienia", f.MiejsceWystawienia);
        AddLabelValue(details, "Waluta", f.KodWaluty);

        Table t = TwoColumnTable();
        TableRow row = new();
        row.Cells.Add(new TableCell(details) { BorderThickness = new Thickness(0), Padding = new Thickness(0) });
        row.Cells.Add(CreatePartyCell("Sprzedawca", f.Sprzedawca?.Nazwa, f.Sprzedawca?.NIP,
            f.Sprzedawca?.PełnyAdres, f.Sprzedawca?.KodKraju));
        t.RowGroups[0].Rows.Add(row);
        doc.Blocks.Add(t);
    }

    private static Table TwoColumnTable()
    {
        Table t = new() { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 6) };
        AddColumn(t, 356);
        AddColumn(t, 356);
        t.RowGroups.Add(new TableRowGroup());
        return t;
    }

    private static void AddPartiesGrid(FlowDocument doc, Faktura f)
    {
        Table table = TwoColumnTable();
        var thirdParties = f.PodmiotyTrzecie.Where(p =>
            Has(p.Nazwa) || Has(p.Adres) || Has(p.OpisRoli)).ToList();

        TableCell buyer = CreatePartyCell("Nabywca", f.Nabywca?.Nazwa,
            f.Nabywca?.PelnyNIP, f.Nabywca?.PełnyAdres, f.Nabywca?.KodKraju);

        TableCell ThirdPartyCell(int index)
        {
            var p = thirdParties[index];
            string label = Has(p.OpisRoli) ? p.OpisRoli! : ThirdPartyRoleLabel(p.Rola);
            return CreatePartyCell(label, p.Nazwa, p.BrakID == "1" ? null : p.NIP, p.Adres, null);
        }

        // Nabywca zawsze pod sprzedawcą (prawa kolumna).
        TableRow firstRow = new();
        firstRow.Cells.Add(thirdParties.Count > 0 ? ThirdPartyCell(0) : EmptyCell());
        firstRow.Cells.Add(buyer);
        table.RowGroups[0].Rows.Add(firstRow);

        for (int i = 1; i < thirdParties.Count; i += 2)
        {
            TableRow row = new();
            row.Cells.Add(ThirdPartyCell(i));
            row.Cells.Add(i + 1 < thirdParties.Count ? ThirdPartyCell(i + 1) : EmptyCell());
            table.RowGroups[0].Rows.Add(row);
        }
        doc.Blocks.Add(table);
    }

    private static TableCell EmptyCell() =>
        new(new Paragraph()) { BorderThickness = new Thickness(0), Padding = new Thickness(0) };

    private static void AddItems(FlowDocument doc, Faktura f, AppSettings settings)
    {
        var items = f.Pozycje ?? [];
        if (items.Count == 0) return;

        bool hasNetPrice = items.Any(x => Has(x.CenaNetto));
        bool hasGrossPrice = items.Any(x => Has(x.CenaBrutto));
        bool hasDiscount = items.Any(x => Has(x.Rabat));
        bool hasNetValue = items.Any(x => Has(x.WartoscNetto));
        bool hasGrossValue = items.Any(x => Has(x.WartoscBrutto));
        bool showPrice = hasNetPrice || hasGrossPrice;
        bool useGrossPrice = hasGrossPrice && (!hasNetPrice || settings.PreferredPrice == PreferredPriceMode.Gross);

        AddSectionTitle(doc, "Pozycje faktury");
        Table table = new() { CellSpacing = 0 };

        // Maksymalny wariant ma 9 kolumn i mieści się w szerokości strony.
        AddColumn(table, 28);   // Lp.
        AddColumn(table, 212);  // Nazwa - ok. 1 cm mniej niż w Etapie 7
        AddColumn(table, 52);   // Ilość
        AddColumn(table, 40);   // J.m.
        if (showPrice) AddColumn(table, 68);
        if (hasDiscount) AddColumn(table, 52);
        AddColumn(table, 45);   // VAT
        if (hasNetValue) AddColumn(table, 95);
        if (hasGrossValue) AddColumn(table, 95);

        TableRowGroup group = new();
        table.RowGroups.Add(group);
        TableRow header = new();
        AddHeaderCell(header, "Lp.");
        AddHeaderCell(header, "Nazwa");
        AddHeaderCell(header, "Ilość");
        AddHeaderCell(header, "J.m.");
        if (showPrice) AddHeaderCell(header, useGrossPrice ? "Cena\nbrutto" : "Cena\nnetto");
        if (hasDiscount) AddHeaderCell(header, "Rabat");
        AddHeaderCell(header, "VAT");
        if (hasNetValue) AddHeaderCell(header, "Wartość\nnetto");
        if (hasGrossValue) AddHeaderCell(header, "Wartość\nbrutto");
        group.Rows.Add(header);

        foreach (var item in items)
        {
            TableRow row = new();
            AddTextCell(row, item.NrWiersza);
            row.Cells.Add(CreateItemNameCell(item, settings.ItemNameDisplay, settings.ItemNameMaxLength));
            AddCenteredTextCell(row, item.Ilosc);
            AddCenteredTextCell(row, item.Miara);
            if (showPrice) AddMoneyCell(row, useGrossPrice ? item.CenaBrutto : item.CenaNetto);
            if (hasDiscount) AddMoneyCell(row, item.Rabat);
            AddCenteredTextCell(row, item.StawkaVAT);
            if (hasNetValue) AddMoneyCell(row, item.WartoscNetto);
            if (hasGrossValue) AddMoneyCell(row, item.WartoscBrutto);
            group.Rows.Add(row);
        }

        doc.Blocks.Add(table);
    }

    private static TableCell CreateItemNameCell(PozycjaFaktury item, ItemNameDisplayMode mode, int maxLength)
    {
        Paragraph p = new(new Run(FormatItemName(item.NazwaTowaru, mode, maxLength))) { Margin = new Thickness(2) };
        var details = new List<string>();
        AddDetail(details, "Indeks", item.Indeks);
        AddDetail(details, "GTIN", item.GTIN);
        AddDetail(details, "PKWiU", item.PKWiU);
        AddDetail(details, "GTU", item.GTU);
        // UU_ID celowo pomijamy w wydruku Standard.
        if (details.Count > 0)
        {
            p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(string.Join(" | ", details)) { FontSize = 8 });
        }
        return new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
    }

    private static void AddTotalAmount(FlowDocument doc, Faktura f)
    {
        Paragraph p = new()
        {
            Margin = new Thickness(0, 4, 0, 3),
            TextAlignment = TextAlignment.Right,
            FontSize = 12,
            FontWeight = FontWeights.Bold
        };
        p.Inlines.Add(new Run($"Kwota należności ogółem: {Money(f.KwotaNaleznosci)} {f.KodWaluty}".Trim()));
        doc.Blocks.Add(p);
    }

    private static void AddVatAndSettlement(FlowDocument doc, Faktura f)
    {
        FlowDocument vat = new();
        FlowDocument settlement = new();
        AddVatSummary(vat, f);
        AddSettlement(settlement, f);
        if (vat.Blocks.Count == 0 && settlement.Blocks.Count == 0) return;

        Table table = TwoColumnTable();
        TableRow row = new();
        row.Cells.Add(SectionCell(settlement));
        row.Cells.Add(SectionCell(vat));
        table.RowGroups[0].Rows.Add(row);
        doc.Blocks.Add(table);
    }

    private static void MoveBlocks(FlowDocument source, BlockCollection target)
    {
        while (source.Blocks.FirstBlock is Block block)
        {
            source.Blocks.Remove(block);
            target.Add(block);
        }
    }

    private static TableCell SectionCell(FlowDocument source)
    {
        Section section = NewSection();
        while (source.Blocks.FirstBlock is Block block)
        {
            source.Blocks.Remove(block);
            section.Blocks.Add(block);
        }
        return new TableCell(section)
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0)
        };
    }

    private static void AddVatSummary(FlowDocument doc, Faktura f)
    {
        if (f.PodsumowanieVat.Count == 0) return;

        AddSectionTitle(doc, "Podsumowanie VAT");
        Table table = new() { CellSpacing = 0 };
        AddColumn(table, 80); AddColumn(table, 80); AddColumn(table, 78); AddColumn(table, 80);
        TableRowGroup group = new(); table.RowGroups.Add(group);
        TableRow header = new();
        AddHeaderCell(header, "Stawka VAT"); AddHeaderCell(header, "Netto"); AddHeaderCell(header, "VAT"); AddHeaderCell(header, "Brutto");
        group.Rows.Add(header);

        decimal totalNet = 0, totalVat = 0;
        foreach (var v in f.PodsumowanieVat)
        {
            decimal net = v.PodstawaOpodatkowania ?? 0;
            decimal vat = v.KwotaVat ?? 0;
            totalNet += net; totalVat += vat;
            TableRow row = new();
            AddTextCell(row, VatLabel(v.Kod));
            AddMoneyCell(row, v.PodstawaOpodatkowania);
            AddMoneyCell(row, v.KwotaVat);
            AddMoneyCell(row, net + vat);
            group.Rows.Add(row);
        }

        TableRow total = new();
        AddBoldCell(total, "Razem"); AddBoldMoneyCell(total, totalNet); AddBoldMoneyCell(total, totalVat); AddBoldMoneyCell(total, totalNet + totalVat);
        group.Rows.Add(total);
        doc.Blocks.Add(table);
    }

    private static void AddSettlement(FlowDocument doc, Faktura f)
    {
        var r = f.Rozliczenie;
        if (r == null) return;
        bool hasAny = r.Obciazenia.Count > 0 || r.SumaObciazen.HasValue || r.Odliczenia.Count > 0 ||
                      r.SumaOdliczen.HasValue || r.DoZaplaty.HasValue || r.DoRozliczenia.HasValue;
        if (!hasAny) return;

        AddSectionTitle(doc, "Rozliczenie");
        foreach (var x in r.Obciazenia) AddLabelValue(doc, "Obciążenie", JoinMoneyReason(x.Kwota, x.Powod, f.KodWaluty));
        if (r.SumaObciazen.HasValue) AddLabelValue(doc, "Suma obciążeń", WithCurrency(r.SumaObciazen, f.KodWaluty));
        foreach (var x in r.Odliczenia) AddLabelValue(doc, "Odliczenie", JoinMoneyReason(x.Kwota, x.Powod, f.KodWaluty));
        if (r.SumaOdliczen.HasValue) AddLabelValue(doc, "Suma odliczeń", WithCurrency(r.SumaOdliczen, f.KodWaluty));
        if (r.DoZaplaty.HasValue) AddLabelValue(doc, "Do zapłaty", WithCurrency(r.DoZaplaty, f.KodWaluty), boldValue: true);
        if (r.DoRozliczenia.HasValue) AddLabelValue(doc, "Do rozliczenia", WithCurrency(r.DoRozliczenia, f.KodWaluty), boldValue: true);
    }

    private static void AddPayment(FlowDocument doc, Faktura f)
    {
        var p = f.Platnosc;
        if (p == null) return;

        FlowDocument left = new();
        FlowDocument right = new();
        AddSectionTitle(left, "Płatność");
        if (p.Zaplacono) AddLabelValue(left, "Informacja o płatności", "Zapłacono");
        if (p.DataZaplaty.HasValue) AddLabelValue(left, "Data zapłaty", FormatDate(p.DataZaplaty));

        foreach (var t in p.Terminy)
        {
            if (t.Termin.HasValue) AddLabelValue(left, "Termin płatności", FormatDate(t.Termin));
            if (Has(t.Ilosc) || Has(t.Jednostka) || Has(t.ZdarzeniePoczatkowe))
                AddLabelValue(left, "Opis terminu", string.Join(" ", new[] { t.Ilosc, t.Jednostka, t.ZdarzeniePoczatkowe }.Where(Has)));
        }

        string? paymentForm = Has(p.OpisPlatnosci) ? p.OpisPlatnosci : PaymentFormLabel(p.FormaPlatnosci);
        AddLabelValue(left, "Forma płatności", paymentForm);

        foreach (var partial in p.PlatnosciCzesciowe)
        {
            string value = WithCurrency(ParseDecimal(partial.Kwota), f.KodWaluty);
            string date = partial.DataDateTime.HasValue ? FormatDate(partial.DataDateTime) : partial.Data ?? string.Empty;
            string form = Has(partial.OpisFormy) ? partial.OpisFormy! : PaymentFormLabel(partial.Forma) ?? string.Empty;
            AddLabelValue(left, "Płatność częściowa", string.Join(" | ", new[] { value, date, form }.Where(Has)));
        }

        // Pierwszy rachunek pozostaje pod informacjami o płatności.
        // Dalsze rachunki trafiają do drugiej kolumny, bez sztucznego pustego pola.
        bool first = true;
        foreach (var account in p.Rachunki)
        {
            AddBankAccount(first ? left : right, "Rachunek", account);
            first = false;
        }
        foreach (var account in p.RachunkiFaktora)
            AddBankAccount(right, "Rachunek faktora", account);

        if (right.Blocks.Count == 0)
        {
            MoveBlocks(left, doc.Blocks);
            return;
        }

        Table table = TwoColumnTable();
        TableRow row = new();
        row.Cells.Add(SectionCell(left));
        row.Cells.Add(SectionCell(right));
        table.RowGroups[0].Rows.Add(row);
        doc.Blocks.Add(table);
    }

    private static void AddAdditionalInformation(FlowDocument doc, Faktura f)
    {
        bool hasAny = f.DodatkoweOpisy.Any(x => Has(x.Klucz) || Has(x.Wartosc)) ||
                      f.NumeryWZ.Any(Has) || f.Umowy.Count > 0 || f.Zamowienia.Count > 0 || Has(f.WarunkiDostawy);
        if (!hasAny) return;

        AddSectionTitle(doc, "Dodatkowe informacje");
        foreach (var x in f.DodatkoweOpisy)
        {
            if (!Has(x.Klucz) && !Has(x.Wartosc)) continue;
            AddLabelValue(doc, Has(x.Klucz) ? x.Klucz! : "Informacja", x.Wartosc);
        }
        foreach (var wz in f.NumeryWZ.Where(Has)) AddLabelValue(doc, "Dokument WZ", wz);
        foreach (var u in f.Umowy) AddRelatedDocument(doc, "Umowa", u);
        foreach (var z in f.Zamowienia) AddRelatedDocument(doc, "Zamówienie", z);
        AddLabelValue(doc, "Warunki dostawy", f.WarunkiDostawy);
    }

    private static void AddFooter(FlowDocument doc, Faktura f)
    {
        bool hasAny = f.StopkaFaktury.Any(Has) || f.Rejestry.Any(r => Has(r.PelnaNazwa) || Has(r.KRS) || Has(r.REGON) || Has(r.BDO));
        if (!hasAny) return;

        Section section = new() { Margin = new Thickness(0) };
        section.Blocks.Add(new Paragraph(new Bold(new Run("Informacje")))
        {
            FontSize = 11,
            Margin = new Thickness(0, 7, 0, 3),
            KeepWithNext = true
        });

        foreach (var info in f.StopkaFaktury.Where(Has))
            section.Blocks.Add(new Paragraph(new Run(info)) { Margin = new Thickness(0, 0, 0, 3), FontSize = 9 });

        foreach (var r in f.Rejestry)
        {
            var parts = new List<string>();
            if (Has(r.PelnaNazwa) && !SameText(r.PelnaNazwa, f.Sprzedawca?.Nazwa)) parts.Add(r.PelnaNazwa!);
            AddDetail(parts, "KRS", r.KRS); AddDetail(parts, "REGON", r.REGON); AddDetail(parts, "BDO", r.BDO);
            if (parts.Count > 0)
                section.Blocks.Add(new Paragraph(new Run(string.Join(" | ", parts))) { Margin = new Thickness(0), FontSize = 9 });
        }

        doc.Blocks.Add(section);
    }

    private static TableCell CreatePartyCell(string title, string? name, string? nip, string? address, string? country)
    {
        Paragraph p = new() { Margin = new Thickness(4) };
        p.Inlines.Add(new Bold(new Run(title)));
        if (Has(name)) p.Inlines.Add(new Run($"\n{name}"));
        if (Has(nip)) p.Inlines.Add(new Run($"\nNIP: {nip}"));
        if (Has(address)) p.Inlines.Add(new Run($"\n{address}"));
        if (Has(country) && !string.Equals(country, "PL", StringComparison.OrdinalIgnoreCase)) p.Inlines.Add(new Run($"\nKraj: {country}"));
        return new TableCell(p) { BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    }

    private static void AddBankAccount(FlowDocument doc, string label, RachunekBankowy r)
    {
        AddLabelValue(doc, label, r.NrRB);
        AddLabelValue(doc, "Bank", r.NazwaBanku);
        AddLabelValue(doc, "SWIFT", r.SWIFT);
        AddLabelValue(doc, "Opis rachunku", r.OpisRachunku);
        if (Has(r.RachunekWlasnyBanku)) AddLabelValue(doc, "Rachunek własny banku", r.RachunekWlasnyBanku);
    }

    private static void AddRelatedDocument(FlowDocument doc, string label, DokumentPowiazany d)
    {
        string value = string.Join(" | ", new[] { d.Numer, d.Data.HasValue ? FormatDate(d.Data) : null }.Where(Has));
        AddLabelValue(doc, label, value);
    }

    private static Section NewSection(bool border = false) => new()
    {
        BorderBrush = Brushes.Black,
        BorderThickness = border ? new Thickness(1) : new Thickness(0),
        Padding = new Thickness(5),
        Margin = new Thickness(0, 0, 0, 5)
    };

    private static void AddSectionTitle(FlowDocument doc, string text) =>
        doc.Blocks.Add(new Paragraph(new Bold(new Run(text)))
        {
            FontSize = 11,
            Margin = new Thickness(0, 7, 0, 3),
            KeepWithNext = true
        });

    private static void AddLabelValue(Section section, string label, string? value, bool boldValue = false)
    {
        if (!Has(value)) return;
        Paragraph p = new() { Margin = new Thickness(0, 1, 0, 1) };
        p.Inlines.Add(new Bold(new Run(label + ": ")));
        p.Inlines.Add(boldValue ? new Bold(new Run(value)) : new Run(value));
        section.Blocks.Add(p);
    }

    private static void AddLabelValue(FlowDocument doc, string label, string? value, bool boldValue = false)
    {
        if (!Has(value)) return;
        Paragraph p = new() { Margin = new Thickness(0, 1, 0, 1) };
        p.Inlines.Add(new Bold(new Run(label + ": ")));
        p.Inlines.Add(boldValue ? new Bold(new Run(value)) : new Run(value));
        doc.Blocks.Add(p);
    }

    private static void AddColumn(Table table, double width) => table.Columns.Add(new TableColumn { Width = new GridLength(width) });

    private static void AddHeaderCell(TableRow row, string text)
    {
        Paragraph p = new(new Bold(new Run(text))) { Margin = new Thickness(2), TextAlignment = TextAlignment.Center };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static void AddTextCell(TableRow row, string? text)
    {
        Paragraph p = new(new Run(text ?? string.Empty)) { Margin = new Thickness(2) };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static void AddCenteredTextCell(TableRow row, string? text)
    {
        Paragraph p = new(new Run(text ?? string.Empty))
        {
            Margin = new Thickness(2),
            TextAlignment = TextAlignment.Center
        };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static void AddCenteredCell(TableRow row, string? text)
    {
        Paragraph p = new(new Run(text ?? string.Empty))
        {
            Margin = new Thickness(2),
            TextAlignment = TextAlignment.Center
        };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static void AddMoneyCell(TableRow row, string? value)
    {
        AddMoneyCell(row, ParseDecimal(value));
    }

    private static void AddMoneyCell(TableRow row, decimal? value)
    {
        Paragraph p = new(new Run(Money(value)))
        {
            Margin = new Thickness(2),
            TextAlignment = TextAlignment.Right
        };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static void AddBoldMoneyCell(TableRow row, decimal value)
    {
        Paragraph p = new(new Bold(new Run(Money(value))))
        {
            Margin = new Thickness(2),
            TextAlignment = TextAlignment.Right
        };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static void AddBoldCell(TableRow row, string text)
    {
        Paragraph p = new(new Bold(new Run(text))) { Margin = new Thickness(2) };
        row.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) });
    }

    private static string FormatItemName(string? name, ItemNameDisplayMode mode, int maxLength)
    {
        if (!Has(name)) return string.Empty;
        if (mode == ItemNameDisplayMode.Wrap) return name!;
        maxLength = Math.Clamp(maxLength, 5, 500);
        return name!.Length <= maxLength ? name : name[..(maxLength - 1)] + "…";
    }

    private static string GetDocumentTitle(string? type) => type switch
    {
        "KOR" => "FAKTURA KORYGUJĄCA",
        "ZAL" => "FAKTURA ZALICZKOWA",
        _ => "FAKTURA VAT"
    };

    private static string VatLabel(string code) => code switch
    {
        "1" => "23% / 22%",
        "2" => "8% / 7%",
        "3" => "5%",
        "4" => "ryczałt taxi",
        "5" => "procedura szczególna",
        "6_1" => "0%",
        "6_2" => "0% WDT",
        "6_3" => "0% eksport",
        "7" => "zw.",
        "8" => "poza terytorium kraju",
        "9" => "usługi art. 100",
        "10" => "odwrotne obciążenie",
        "11" => "marża",
        _ => code
    };

    private static string ThirdPartyRoleLabel(string? code) => code switch
    {
        "1" => "Faktor",
        "2" => "Odbiorca",
        "3" => "Podmiot pierwotny",
        "4" => "Dodatkowy nabywca",
        "5" => "Wystawca faktury",
        "6" => "Dokonujący płatności",
        "7" => "Jednostka samorządu terytorialnego - wystawca",
        "8" => "Jednostka samorządu terytorialnego - odbiorca",
        "9" => "Członek grupy VAT - wystawca",
        "10" => "Członek grupy VAT - odbiorca",
        "11" => "Pracownik",
        _ => "Podmiot trzeci"
    };

    private static string? PaymentFormLabel(string? code) => code switch
    {
        "1" => "Gotówka",
        "2" => "Karta",
        "3" => "Bon",
        "4" => "Czek",
        "5" => "Kredyt",
        "6" => "Przelew",
        "7" => "Mobilna",
        null or "" => null,
        _ => code
    };

    private static string FormatDate(DateTime date) => date == DateTime.MinValue ? string.Empty : date.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("pl-PL"));
    private static string FormatDate(DateTime? date) => date.HasValue ? FormatDate(date.Value) : string.Empty;
    private static string Money(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("pl-PL"));
    private static string Money(decimal? value) => value.HasValue ? Money(value.Value) : string.Empty;
    private static string WithCurrency(decimal? value, string? currency) => value.HasValue ? $"{Money(value)} {currency}".Trim() : string.Empty;
    private static string JoinMoneyReason(decimal? value, string? reason, string? currency) => string.Join(" | ", new[] { WithCurrency(value, currency), reason }.Where(Has));
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool SameText(string? a, string? b) => Has(a) && Has(b) && string.Equals(a!.Trim(), b!.Trim(), StringComparison.OrdinalIgnoreCase);
    private static void AddDetail(List<string> details, string label, string? value) { if (Has(value)) details.Add($"{label}: {value}"); }
}
