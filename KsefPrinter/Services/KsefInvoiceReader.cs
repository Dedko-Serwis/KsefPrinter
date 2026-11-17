using KsefPrinter.Models;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace KsefPrinter.Services;

/// <summary>
/// Odczytuje dokument XML KSeF i mapuje go na model Faktura.
/// </summary>
public static class KsefInvoiceReader
{
    public static Faktura Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Ścieżka pliku nie może być pusta.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Plik {filePath} nie istnieje.", filePath);

        try
        {
            var document = XDocument.Load(filePath);
            return Parse(document, Path.GetFileNameWithoutExtension(filePath));
        }
        catch (KsefInvoiceReadException)
        {
            throw;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            throw new KsefInvoiceReadException($"Nie można odczytać pliku XML: {ex.Message}", ex);
        }
    }

    public static Faktura Parse(XDocument document, string? ksefNumber = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Root
            ?? throw new KsefInvoiceReadException("Dokument XML nie zawiera elementu głównego.");

        if (root.Name.LocalName != "Faktura")
            throw new KsefInvoiceReadException($"Nieobsługiwany element główny XML: {root.Name.LocalName}.");

        // Namespace pobieramy z nazwy elementu, a nie z deklaracji domyślnej.
        // Dzięki temu działa zarówno <Faktura xmlns=...>, jak i <tns:Faktura ...>.
        XNamespace ns = root.Name.Namespace;

        var faktura = new Faktura
        {
            NumerKSeF = string.IsNullOrWhiteSpace(ksefNumber) ? "brak danych" : ksefNumber
        };

        ParseNaglowek(faktura, root, ns);
        ParseFa(faktura, root, ns);
        ParseSprzedawca(faktura, root, ns);
        ParseNabywca(faktura, root, ns);
        ParsePodmiotyTrzecie(faktura, root, ns);
        ParsePozycje(faktura, root, ns);
        ParsePozycjeZamowienia(faktura, root, ns);
        ParsePodsumowanieVat(faktura, root, ns);
        ParsePlatnosci(faktura, root, ns);
        ParseRozliczenie(faktura, root, ns);
        ParseDodatkoweDane(faktura, root, ns);
        ParsePowiazania(faktura, root, ns);
        ParseStopka(faktura, root, ns);

        return faktura;
    }

    private static void ParseNaglowek(Faktura faktura, XElement? root, XNamespace ns)
    {
        var naglowek = root?.Element(ns + "Naglowek");
        if (naglowek == null) return;

        faktura.KodFormularza = naglowek.Element(ns + "KodFormularza")?.Value;
        faktura.WariantFormularza = naglowek.Element(ns + "WariantFormularza")?.Value;

        var data = naglowek.Element(ns + "DataWytworzeniaFa")?.Value;
        if (!string.IsNullOrEmpty(data))
            faktura.DataWytworzenia = DateTime.TryParse(data, out var dt) ? dt : DateTime.MinValue;

        faktura.SystemInfo = naglowek.Element(ns + "SystemInfo")?.Value;
    }

    private static void ParseFa(Faktura faktura, XElement? root, XNamespace ns)
    {
        var fa = root?.Element(ns + "Fa");
        if (fa == null) return;

        faktura.KodWaluty = fa.Element(ns + "KodWaluty")?.Value;

        var p1 = fa.Element(ns + "P_1")?.Value;
        if (!string.IsNullOrEmpty(p1))
            faktura.DataWystawienia = DateTime.TryParse(p1, out var dt) ? dt : DateTime.MinValue;

        faktura.MiejsceWystawienia = fa.Element(ns + "P_1M")?.Value;
        faktura.NumerFaktury = fa.Element(ns + "P_2")?.Value;

        var p6 = fa.Element(ns + "P_6")?.Value;
        if (!string.IsNullOrEmpty(p6))
            faktura.DataDostawy = DateTime.TryParse(p6, out var dt) ? dt : null;

        var okres = fa.Element(ns + "OkresFa");
        if (okres != null)
        {
            var od = okres.Element(ns + "P_6_Od")?.Value;
            var do_ = okres.Element(ns + "P_6_Do")?.Value;
            if (!string.IsNullOrEmpty(od)) faktura.DataDostawyOd = DateTime.TryParse(od, out var dt) ? dt : null;
            if (!string.IsNullOrEmpty(do_)) faktura.DataDostawyDo = DateTime.TryParse(do_, out var dt) ? dt : null;
        }

        faktura.RodzajFaktury = fa.Element(ns + "RodzajFaktury")?.Value;
        faktura.PrzyczynaKorekty = fa.Element(ns + "PrzyczynaKorekty")?.Value;
        faktura.TypKorekty = fa.Element(ns + "TypKorekty")?.Value;

        var p15 = fa.Element(ns + "P_15")?.Value;
        if (!string.IsNullOrEmpty(p15))
            faktura.KwotaNaleznosci = ParseDecimal(p15) ?? 0;
    }

    private static void ParseSprzedawca(Faktura faktura, XElement? root, XNamespace ns)
    {
        var podmiot = root?.Element(ns + "Podmiot1");
        if (podmiot == null) return;

        faktura.Sprzedawca = new Sprzedawca();

        var dane = podmiot.Element(ns + "DaneIdentyfikacyjne");
        if (dane != null)
        {
            faktura.Sprzedawca.NIP = dane.Element(ns + "NIP")?.Value;
            faktura.Sprzedawca.Nazwa = dane.Element(ns + "Nazwa")?.Value;
        }

        var adres = podmiot.Element(ns + "Adres");
        if (adres != null)
        {
            faktura.Sprzedawca.KodKraju = adres.Element(ns + "KodKraju")?.Value;
            faktura.Sprzedawca.AdresLinia1 = adres.Element(ns + "AdresL1")?.Value;
            faktura.Sprzedawca.AdresLinia2 = adres.Element(ns + "AdresL2")?.Value;
            faktura.Sprzedawca.GLN = adres.Element(ns + "GLN")?.Value;
        }

        var kontakty = podmiot.Elements(ns + "DaneKontaktowe");
        foreach (var kontakt in kontakty)
        {
            faktura.Sprzedawca.Email = kontakt.Element(ns + "Email")?.Value ?? faktura.Sprzedawca.Email;
            faktura.Sprzedawca.Telefon = kontakt.Element(ns + "Telefon")?.Value ?? faktura.Sprzedawca.Telefon;
        }
    }

    private static void ParseNabywca(Faktura faktura, XElement? root, XNamespace ns)
    {
        var podmiot = root?.Element(ns + "Podmiot2");
        if (podmiot == null) return;

        faktura.Nabywca = new Nabywca();

        var dane = podmiot.Element(ns + "DaneIdentyfikacyjne");
        if (dane != null)
        {
            faktura.Nabywca.NIP = dane.Element(ns + "NIP")?.Value;
            faktura.Nabywca.KodUE = dane.Element(ns + "KodUE")?.Value;
            faktura.Nabywca.NrVatUE = dane.Element(ns + "NrVatUE")?.Value;
            faktura.Nabywca.Nazwa = dane.Element(ns + "Nazwa")?.Value;
            faktura.Nabywca.BrakID = dane.Element(ns + "BrakID")?.Value;
        }

        var adres = podmiot.Element(ns + "Adres");
        if (adres != null)
        {
            faktura.Nabywca.KodKraju = adres.Element(ns + "KodKraju")?.Value;
            faktura.Nabywca.AdresLinia1 = adres.Element(ns + "AdresL1")?.Value;
            faktura.Nabywca.AdresLinia2 = adres.Element(ns + "AdresL2")?.Value;
            faktura.Nabywca.GLN = adres.Element(ns + "GLN")?.Value;
        }

        var kontakty = podmiot.Elements(ns + "DaneKontaktowe");
        foreach (var kontakt in kontakty)
        {
            faktura.Nabywca.Email = kontakt.Element(ns + "Email")?.Value ?? faktura.Nabywca.Email;
            faktura.Nabywca.Telefon = kontakt.Element(ns + "Telefon")?.Value ?? faktura.Nabywca.Telefon;
        }

        faktura.Nabywca.JST = podmiot.Element(ns + "JST")?.Value;
        faktura.Nabywca.GV = podmiot.Element(ns + "GV")?.Value;
    }

    private static void ParsePodmiotyTrzecie(Faktura faktura, XElement? root, XNamespace ns)
    {
        var podmioty = root?.Elements(ns + "Podmiot3");
        if (podmioty == null) return;

        foreach (var podmiot in podmioty)
        {
            var p3 = new PodmiotTrzeci();

            var dane = podmiot.Element(ns + "DaneIdentyfikacyjne");
            if (dane != null)
            {
                p3.NIP = dane.Element(ns + "NIP")?.Value;
                p3.Nazwa = dane.Element(ns + "Nazwa")?.Value;
                p3.BrakID = dane.Element(ns + "BrakID")?.Value;
            }

            p3.Rola = podmiot.Element(ns + "Rola")?.Value;
            p3.OpisRoli = podmiot.Element(ns + "OpisRoli")?.Value;

            var adres = podmiot.Element(ns + "Adres");
            if (adres != null)
            {
                var adresL1 = adres.Element(ns + "AdresL1")?.Value;
                var adresL2 = adres.Element(ns + "AdresL2")?.Value;
                p3.Adres = $"{adresL1} {adresL2}".Trim();
            }

            faktura.PodmiotyTrzecie.Add(p3);
        }
    }

    private static void ParsePozycje(Faktura faktura, XElement? root, XNamespace ns)
    {
        var wiersze = root?.Descendants(ns + "FaWiersz");
        if (wiersze == null) return;

        foreach (var wiersz in wiersze)
        {
            var pozycja = new PozycjaFaktury
            {
                NrWiersza = wiersz?.Element(ns + "NrWierszaFa")?.Value,
                UU_ID = wiersz?.Element(ns + "UU_ID")?.Value,
                StanPrzed = wiersz?.Element(ns + "StanPrzed")?.Value == "1",
                DataDostawy = ParseDate(wiersz?.Element(ns + "P_6A")?.Value),
                NazwaTowaru = wiersz?.Element(ns + "P_7")?.Value,
                Miara = wiersz?.Element(ns + "P_8A")?.Value,
                Ilosc = wiersz?.Element(ns + "P_8B")?.Value,
                CenaNetto = wiersz?.Element(ns + "P_9A")?.Value,
                CenaBrutto = wiersz?.Element(ns + "P_9B")?.Value,
                Rabat = wiersz?.Element(ns + "P_10")?.Value,
                WartoscNetto = wiersz?.Element(ns + "P_11")?.Value,
                WartoscBrutto = wiersz?.Element(ns + "P_11A")?.Value,
                KwotaVAT = wiersz?.Element(ns + "P_11Vat")?.Value,
                StawkaVAT = wiersz?.Element(ns + "P_12")?.Value,
                Indeks = wiersz?.Element(ns + "Indeks")?.Value,
                GTIN = wiersz?.Element(ns + "GTIN")?.Value,
                GTU = wiersz?.Element(ns + "GTU")?.Value,
                Procedura = wiersz?.Element(ns + "Procedura")?.Value,
                PKWiU = wiersz?.Element(ns + "PKWiU")?.Value,
                CN = wiersz?.Element(ns + "CN")?.Value,
                PKOB = wiersz?.Element(ns + "PKOB")?.Value,
                KwotaAkcyzy = wiersz?.Element(ns + "KwotaAkcyzy")?.Value
            };

            faktura.Pozycje.Add(pozycja);
        }
    }

    private static void ParsePozycjeZamowienia(Faktura faktura, XElement root, XNamespace ns)
    {
        var zamowienie = root.Element(ns + "Fa")?.Element(ns + "Zamowienie");
        if (zamowienie == null) return;
        faktura.WartoscZamowienia = ParseDecimal(zamowienie.Element(ns + "WartoscZamowienia")?.Value);
        foreach (var row in zamowienie.Elements(ns + "ZamowienieWiersz"))
        {
            faktura.PozycjeZamowienia.Add(new PozycjaZamowienia
            {
                Numer = row.Element(ns + "NrWierszaZam")?.Value,
                Nazwa = row.Element(ns + "P_7Z")?.Value,
                Indeks = row.Element(ns + "IndeksZ")?.Value,
                OpisJednostki = row.Element(ns + "P_8AZ")?.Value,
                Ilosc = row.Element(ns + "P_8BZ")?.Value,
                CenaNetto = row.Element(ns + "P_9AZ")?.Value,
                CenaBrutto = row.Element(ns + "P_9BZ")?.Value,
                WartoscNetto = row.Element(ns + "P_11NettoZ")?.Value,
                WartoscVat = row.Element(ns + "P_11VatZ")?.Value,
                StawkaVat = row.Element(ns + "P_12Z")?.Value
            });
        }
    }

    private static void ParsePodsumowanieVat(Faktura faktura, XElement root, XNamespace ns)
    {
        var fa = root.Element(ns + "Fa");
        if (fa == null) return;
        string[] codes = ["1", "2", "3", "4", "5", "6_1", "6_2", "6_3", "7", "8", "9", "10", "11"];
        foreach (var code in codes)
        {
            var podstawa = ParseDecimal(fa.Element(ns + $"P_13_{code}")?.Value);
            var vat = ParseDecimal(fa.Element(ns + $"P_14_{code}")?.Value);
            var vatPln = ParseDecimal(fa.Element(ns + $"P_14_{code}W")?.Value);
            if (podstawa.HasValue || vat.HasValue || vatPln.HasValue)
                faktura.PodsumowanieVat.Add(new PodsumowanieVat { Kod = code, PodstawaOpodatkowania = podstawa, KwotaVat = vat, KwotaVatPln = vatPln });
        }
    }

    private static void ParsePlatnosci(Faktura faktura, XElement root, XNamespace ns)
    {
        var x = root.Descendants(ns + "Platnosc").FirstOrDefault();
        if (x == null) return;
        var p = new WarunkiPlatnosci
        {
            Zaplacono = x.Element(ns + "Zaplacono")?.Value == "1",
            DataZaplaty = ParseDate(x.Element(ns + "DataZaplaty")?.Value),
            ZnacznikZaplatyCzesciowej = x.Element(ns + "ZnacznikZaplatyCzesciowej")?.Value,
            FormaPlatnosci = x.Element(ns + "FormaPlatnosci")?.Value,
            OpisPlatnosci = x.Element(ns + "OpisPlatnosci")?.Value,
            LinkDoPlatnosci = x.Element(ns + "LinkDoPlatnosci")?.Value,
            IPKSeF = x.Element(ns + "IPKSeF")?.Value
        };
        foreach (var t in x.Elements(ns + "TerminPlatnosci"))
        {
            var opis = t.Element(ns + "TerminOpis");
            p.Terminy.Add(new TerminPlatnosci { Termin = ParseDate(t.Element(ns + "Termin")?.Value), Ilosc = opis?.Element(ns + "Ilosc")?.Value, Jednostka = opis?.Element(ns + "Jednostka")?.Value, ZdarzeniePoczatkowe = opis?.Element(ns + "ZdarzeniePoczatkowe")?.Value });
        }
        foreach (var r in x.Elements(ns + "RachunekBankowy")) p.Rachunki.Add(ParseRachunek(r, ns));
        foreach (var r in x.Elements(ns + "RachunekBankowyFaktora")) p.RachunkiFaktora.Add(ParseRachunek(r, ns));
        foreach (var z in x.Elements(ns + "ZaplataCzesciowa"))
        {
            var cz = new Platnosc { Kwota = z.Element(ns + "KwotaZaplatyCzesciowej")?.Value, Data = z.Element(ns + "DataZaplatyCzesciowej")?.Value, Forma = z.Element(ns + "FormaPlatnosci")?.Value, OpisFormy = z.Element(ns + "OpisPlatnosci")?.Value };
            p.PlatnosciCzesciowe.Add(cz); faktura.Platnosci.Add(cz);
        }
        if (p.Zaplacono && p.DataZaplaty.HasValue) faktura.Platnosci.Add(new Platnosc { Kwota = "całość", Data = p.DataZaplaty.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Forma = p.FormaPlatnosci ?? "pełna" });
        faktura.Platnosc = p;
    }

    private static RachunekBankowy ParseRachunek(XElement r, XNamespace ns) => new() { NrRB = r.Element(ns + "NrRB")?.Value, SWIFT = r.Element(ns + "SWIFT")?.Value, RachunekWlasnyBanku = r.Element(ns + "RachunekWlasnyBanku")?.Value, NazwaBanku = r.Element(ns + "NazwaBanku")?.Value, OpisRachunku = r.Element(ns + "OpisRachunku")?.Value };

    private static void ParseRozliczenie(Faktura faktura, XElement root, XNamespace ns)
    {
        var x = root.Descendants(ns + "Rozliczenie").FirstOrDefault(); if (x == null) return;
        var r = new RozliczenieFaktury { SumaObciazen = ParseDecimal(x.Element(ns + "SumaObciazen")?.Value), SumaOdliczen = ParseDecimal(x.Element(ns + "SumaOdliczen")?.Value), DoZaplaty = ParseDecimal(x.Element(ns + "DoZaplaty")?.Value), DoRozliczenia = ParseDecimal(x.Element(ns + "DoRozliczenia")?.Value) };
        foreach (var e in x.Elements(ns + "Obciazenia")) r.Obciazenia.Add(new KorektaRozliczenia { Kwota = ParseDecimal(e.Element(ns + "Kwota")?.Value), Powod = e.Element(ns + "Powod")?.Value });
        foreach (var e in x.Elements(ns + "Odliczenia")) r.Odliczenia.Add(new KorektaRozliczenia { Kwota = ParseDecimal(e.Element(ns + "Kwota")?.Value), Powod = e.Element(ns + "Powod")?.Value });
        faktura.Rozliczenie = r;
    }

    private static void ParseDodatkoweDane(Faktura faktura, XElement root, XNamespace ns)
    {
        var fa = root.Element(ns + "Fa"); if (fa == null) return;
        foreach (var e in fa.Elements(ns + "DodatkowyOpis")) faktura.DodatkoweOpisy.Add(new DodatkowyOpis { Klucz = e.Element(ns + "Klucz")?.Value, Wartosc = e.Element(ns + "Wartosc")?.Value });
        foreach (var e in fa.Elements(ns + "WZ")) if (!string.IsNullOrWhiteSpace(e.Value)) faktura.NumeryWZ.Add(e.Value);
        var wt = fa.Element(ns + "WarunkiTransakcji"); if (wt == null) return;
        faktura.WarunkiDostawy = wt.Element(ns + "WarunkiDostawy")?.Value;
        foreach (var e in wt.Elements(ns + "Umowy")) faktura.Umowy.Add(new DokumentPowiazany { Data = ParseDate(e.Element(ns + "DataUmowy")?.Value), Numer = e.Element(ns + "NrUmowy")?.Value });
        foreach (var e in wt.Elements(ns + "Zamowienia")) faktura.Zamowienia.Add(new DokumentPowiazany { Data = ParseDate(e.Element(ns + "DataZamowienia")?.Value), Numer = e.Element(ns + "NrZamowienia")?.Value });
    }

    private static void ParsePowiazania(Faktura faktura, XElement root, XNamespace ns)
    {
        var fa = root.Element(ns + "Fa"); if (fa == null) return;
        foreach (var e in fa.Elements(ns + "DaneFaKorygowanej")) faktura.FakturyKorygowane.Add(new FakturaPowiazana { NumerKSeF = e.Element(ns + "NrKSeFFaKorygowanej")?.Value, NumerFaktury = e.Element(ns + "NrFaKorygowanej")?.Value, DataWystawienia = ParseDate(e.Element(ns + "DataWystFaKorygowanej")?.Value) });
        foreach (var e in fa.Elements(ns + "FakturaZaliczkowa")) faktura.FakturyZaliczkowe.Add(new FakturaPowiazana { NumerKSeF = e.Element(ns + "NrKSeFFaZaliczkowej")?.Value, NumerFaktury = e.Element(ns + "NrFaZaliczkowej")?.Value });
    }

    private static void ParseStopka(Faktura faktura, XElement root, XNamespace ns)
    {
        var stopka = root.Element(ns + "Stopka");
        if (stopka == null) return;

        foreach (var info in stopka.Elements(ns + "Informacje"))
        {
            var text = info.Element(ns + "StopkaFaktury")?.Value;
            if (!string.IsNullOrWhiteSpace(text)) faktura.StopkaFaktury.Add(text);
        }

        foreach (var r in stopka.Elements(ns + "Rejestry"))
        {
            faktura.Rejestry.Add(new DaneRejestrowe
            {
                PelnaNazwa = r.Element(ns + "PelnaNazwa")?.Value,
                KRS = r.Element(ns + "KRS")?.Value,
                REGON = r.Element(ns + "REGON")?.Value,
                BDO = r.Element(ns + "BDO")?.Value
            });
        }
    }

    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
}
