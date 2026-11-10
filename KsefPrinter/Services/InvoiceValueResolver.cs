using KsefPrinter.Models;
using System.Globalization;

namespace KsefPrinter.Services;

/// <summary>
/// Interpretuje wartości finansowe już odczytane z XML.
/// Nie modyfikuje modelu źródłowego i nie udaje, że wartość wyliczona
/// była obecna w dokumencie KSeF.
/// </summary>
public static class InvoiceValueResolver
{
    public static WartoscFaktury Resolve(Faktura faktura)
    {
        ArgumentNullException.ThrowIfNull(faktura);

        decimal? netto = faktura.PodsumowanieVat.Count > 0
            ? SumNullable(faktura.PodsumowanieVat.Select(x => x.PodstawaOpodatkowania))
            : null;

        decimal? vat = faktura.PodsumowanieVat.Count > 0
            ? SumNullable(faktura.PodsumowanieVat.Select(x => x.KwotaVat))
            : null;

        decimal? brutto = faktura.KwotaNaleznosci;

        decimal? doZaplaty;
        if (faktura.Platnosc?.Zaplacono == true)
        {
            doZaplaty = 0m;
        }
        else if (faktura.Rozliczenie?.DoZaplaty is decimal rozliczenie)
        {
            doZaplaty = rozliczenie;
        }
        else if (faktura.Platnosc?.PlatnosciCzesciowe.Count > 0)
        {
            var zaplacono = faktura.Platnosc.PlatnosciCzesciowe
                .Select(x => Parse(x.Kwota))
                .Where(x => x.HasValue)
                .Sum(x => x!.Value);

            doZaplaty = Math.Max(0m, faktura.KwotaNaleznosci - zaplacono);
        }
        else
        {
            doZaplaty = faktura.KwotaNaleznosci;
        }

        return new WartoscFaktury
        {
            Netto = netto,
            Vat = vat,
            Brutto = brutto,
            DoZaplaty = doZaplaty
        };
    }

    public static WartoscPozycji Resolve(PozycjaFaktury pozycja)
    {
        ArgumentNullException.ThrowIfNull(pozycja);

        var cenaNetto = Parse(pozycja.CenaNetto);
        var cenaBrutto = Parse(pozycja.CenaBrutto);
        var wartoscNetto = Parse(pozycja.WartoscNetto);
        var vat = Parse(pozycja.KwotaVAT);
        var wartoscBrutto = Parse(pozycja.WartoscBrutto);
        var stawka = ParseVatRate(pozycja.StawkaVAT);

        bool cenaNettoWyliczona = false;
        bool cenaBruttoWyliczona = false;
        bool wartoscNettoWyliczona = false;
        bool vatWyliczony = false;
        bool wartoscBruttoWyliczona = false;

        // Najpierw korzystamy wyłącznie z wartości jawnie podanych w XML.
        // Brakujące wartości uzupełniamy tylko wtedy, gdy wynik jest jednoznaczny.

        if (!wartoscBrutto.HasValue && wartoscNetto.HasValue && vat.HasValue)
        {
            wartoscBrutto = wartoscNetto.Value + vat.Value;
            wartoscBruttoWyliczona = true;
        }

        if (!vat.HasValue && wartoscNetto.HasValue && wartoscBrutto.HasValue)
        {
            vat = wartoscBrutto.Value - wartoscNetto.Value;
            vatWyliczony = true;
        }

        if (!wartoscNetto.HasValue && wartoscBrutto.HasValue && vat.HasValue)
        {
            wartoscNetto = wartoscBrutto.Value - vat.Value;
            wartoscNettoWyliczona = true;
        }

        // Stawkę VAT wykorzystujemy dopiero, gdy brakuje drugiej wartości.
        // Wyniki pieniężne zaokrąglamy do 2 miejsc.
        if (stawka.HasValue)
        {
            if (!wartoscBrutto.HasValue && wartoscNetto.HasValue)
            {
                wartoscBrutto = Money(wartoscNetto.Value * (1m + stawka.Value));
                wartoscBruttoWyliczona = true;
            }

            if (!wartoscNetto.HasValue && wartoscBrutto.HasValue)
            {
                wartoscNetto = Money(wartoscBrutto.Value / (1m + stawka.Value));
                wartoscNettoWyliczona = true;
            }

            if (!vat.HasValue && wartoscNetto.HasValue && wartoscBrutto.HasValue)
            {
                vat = wartoscBrutto.Value - wartoscNetto.Value;
                vatWyliczony = true;
            }

            if (!cenaBrutto.HasValue && cenaNetto.HasValue)
            {
                cenaBrutto = Money(cenaNetto.Value * (1m + stawka.Value));
                cenaBruttoWyliczona = true;
            }

            if (!cenaNetto.HasValue && cenaBrutto.HasValue)
            {
                cenaNetto = Money(cenaBrutto.Value / (1m + stawka.Value));
                cenaNettoWyliczona = true;
            }
        }

        return new WartoscPozycji
        {
            CenaNetto = cenaNetto,
            CenaBrutto = cenaBrutto,
            WartoscNetto = wartoscNetto,
            Vat = vat,
            WartoscBrutto = wartoscBrutto,
            CenaNettoWyliczona = cenaNettoWyliczona,
            CenaBruttoWyliczona = cenaBruttoWyliczona,
            WartoscNettoWyliczona = wartoscNettoWyliczona,
            VatWyliczony = vatWyliczony,
            WartoscBruttoWyliczona = wartoscBruttoWyliczona
        };
    }

    private static decimal? SumNullable(IEnumerable<decimal?> values)
    {
        var present = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return present.Count == 0 ? null : present.Sum();
    }

    private static decimal? Parse(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static decimal? ParseVatRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Tylko stawki liczbowe. Oznaczeń typu "zw", "np", "oo" nie przeliczamy.
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var percent)
            ? percent / 100m
            : null;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
