using System.Xml.Linq;
using KsefPrinter.Services;
using Xunit;

namespace KsefPrinter.Tests;

/// <summary>
/// Testy obliczeń finansowych na syntetycznych fakturach FA(3).
/// Nie wymagają zewnętrznych ani rzeczywistych plików faktur.
/// </summary>
public class InvoiceValueResolverTests
{
    private static readonly XNamespace Ns = "http://crd.gov.pl/wzor/2025/06/25/13775/";

    private static KsefPrinter.Models.Faktura Invoice(
        decimal amount,
        string vatSummary,
        string row,
        string extra = "")
    {
        var document = new XDocument(
            new XElement(Ns + "Faktura",
                new XElement(Ns + "Fa",
                    new XElement(Ns + "KodWaluty", "PLN"),
                    new XElement(Ns + "P_1", "2026-10-07"),
                    new XElement(Ns + "P_2", "SYN/RESOLVER/1"),
                    XElement.Parse("<root>" + vatSummary + "</root>").Elements(),
                    new XElement(Ns + "P_15", amount),
                    XElement.Parse(row)),
                string.IsNullOrWhiteSpace(extra) ? null : XElement.Parse(extra)));
        return KsefInvoiceReader.Parse(document, "SYNTHETIC");
    }

    private static string Tag(string name, string value) =>
        $"<{name} xmlns='http://crd.gov.pl/wzor/2025/06/25/13775/'>{value}</{name}>";

    private static string Row(string content) =>
        Tag("FaWiersz", Tag("NrWierszaFa", "1") + content);

    [Fact]
    public void Rewident_UsesInvoiceSummary_ForNetVatGross()
    {
        var invoice = Invoice(184.50m,
            Tag("P_13_1", "150.00") + Tag("P_14_1", "34.50"),
            Row(Tag("P_11", "150.00") + Tag("P_12", "23")));

        var value = InvoiceValueResolver.Resolve(invoice);

        Assert.Equal(150.00m, value.Netto);
        Assert.Equal(34.50m, value.Vat);
        Assert.Equal(184.50m, value.Brutto);
        Assert.Equal(184.50m, value.DoZaplaty);
    }

    [Fact]
    public void PaidInvoice_HasZeroAmountDue()
    {
        var invoice = Invoice(95.48m,
            Tag("P_13_1", "77.63") + Tag("P_14_1", "17.85"),
            Row(Tag("P_11", "77.63") + Tag("P_12", "23")),
            Tag("Platnosc", Tag("Zaplacono", "1")));

        var value = InvoiceValueResolver.Resolve(invoice);

        Assert.Equal(95.48m, value.Brutto);
        Assert.Equal(0m, value.DoZaplaty);
    }

    [Fact]
    public void P4_UsesRozliczenieDoZaplaty()
    {
        var invoice = Invoice(253.15m,
            Tag("P_13_1", "205.81") + Tag("P_14_1", "47.34"),
            Row(Tag("P_11", "205.81") + Tag("P_12", "23")),
            Tag("Rozliczenie", Tag("DoZaplaty", "253.15")));

        var value = InvoiceValueResolver.Resolve(invoice);

        Assert.Equal(205.81m, value.Netto);
        Assert.Equal(47.34m, value.Vat);
        Assert.Equal(253.15m, value.Brutto);
        Assert.Equal(253.15m, value.DoZaplaty);
    }

    [Fact]
    public void Rewident_Row_CalculatesMissingGrossAndVat_ButMarksThemCalculated()
    {
        var invoice = Invoice(184.50m,
            Tag("P_13_1", "150.00") + Tag("P_14_1", "34.50"),
            Row(Tag("P_11", "150.00") + Tag("P_12", "23")));

        var row = InvoiceValueResolver.Resolve(Assert.Single(invoice.Pozycje));

        Assert.Equal(150.00m, row.WartoscNetto);
        Assert.Equal(34.50m, row.Vat);
        Assert.Equal(184.50m, row.WartoscBrutto);
        Assert.True(row.VatWyliczony);
        Assert.True(row.WartoscBruttoWyliczona);
    }

    [Fact]
    public void Sharks_Row_CalculatesNetFromGross_WhenOnlyGrossValuesExist()
    {
        var invoice = Invoice(145.00m,
            Tag("P_13_1", "117.89") + Tag("P_14_1", "27.11"),
            Row(Tag("P_9B", "145.00") + Tag("P_11A", "145.00") + Tag("P_12", "23")));

        var row = InvoiceValueResolver.Resolve(Assert.Single(invoice.Pozycje));

        Assert.Equal(145.00m, row.CenaBrutto);
        Assert.Equal(117.89m, row.CenaNetto);
        Assert.Equal(145.00m, row.WartoscBrutto);
        Assert.Equal(117.89m, row.WartoscNetto);
        Assert.Equal(27.11m, row.Vat);
        Assert.True(row.CenaNettoWyliczona);
        Assert.True(row.WartoscNettoWyliczona);
        Assert.True(row.VatWyliczony);
    }

    [Fact]
    public void Orlen_ExplicitValues_AreNotMarkedAsCalculated()
    {
        var invoice = Invoice(134.83m,
            Tag("P_13_1", "109.62") + Tag("P_14_1", "25.21"),
            Row(Tag("P_11", "109.62") + Tag("P_11Vat", "25.21") +
                Tag("P_11A", "134.83") + Tag("P_12", "23")));

        var row = InvoiceValueResolver.Resolve(Assert.Single(invoice.Pozycje));

        Assert.Equal(109.62m, row.WartoscNetto);
        Assert.Equal(25.21m, row.Vat);
        Assert.Equal(134.83m, row.WartoscBrutto);
        Assert.False(row.WartoscNettoWyliczona);
        Assert.False(row.VatWyliczony);
        Assert.False(row.WartoscBruttoWyliczona);
    }
}
