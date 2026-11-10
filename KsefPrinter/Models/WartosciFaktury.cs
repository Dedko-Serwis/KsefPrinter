namespace KsefPrinter.Models;

public sealed class WartoscFaktury
{
    public decimal? Netto { get; init; }
    public decimal? Vat { get; init; }
    public decimal? Brutto { get; init; }
    public decimal? DoZaplaty { get; init; }
}

public sealed class WartoscPozycji
{
    public decimal? CenaNetto { get; init; }
    public decimal? CenaBrutto { get; init; }
    public decimal? WartoscNetto { get; init; }
    public decimal? Vat { get; init; }
    public decimal? WartoscBrutto { get; init; }

    public bool CenaNettoWyliczona { get; init; }
    public bool CenaBruttoWyliczona { get; init; }
    public bool WartoscNettoWyliczona { get; init; }
    public bool VatWyliczony { get; init; }
    public bool WartoscBruttoWyliczona { get; init; }
}
