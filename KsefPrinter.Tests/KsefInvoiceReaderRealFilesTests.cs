using KsefPrinter.Services;
using Xunit;

namespace KsefPrinter.Tests;

/// <summary>
/// Sprawdza odczyt danych z syntetycznych faktur FA(3).
/// Wszystkie oczekiwane wartości pochodzą z plików synthetic-*.xml.
/// </summary>
public class KsefInvoiceReaderRealFilesTests
{
    private static string TestFile(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    public static IEnumerable<object[]> SyntheticInvoices()
    {
        yield return ["synthetic-missing.xml", "SYN/MIN/1", 10m, "PLN", 1];
        yield return ["synthetic-multi-vat.xml", "SYN/MULTI/1", 231m, "PLN", 2];
        yield return ["synthetic-rounding.xml", "SYN/ROUND/1", 12.30m, "PLN", 1];
        yield return ["synthetic-vat-np.xml", "SYN/NP/1", 50m, "PLN", 1];
        yield return ["synthetic-vat-zero.xml", "SYN/ZERO/1", 50m, "PLN", 1];
        yield return ["synthetic-vat-zw.xml", "SYN/ZW/1", 50m, "PLN", 1];
    }

    [Theory]
    [MemberData(nameof(SyntheticInvoices))]
    public void Load_SyntheticInvoices_ReadsBasicData(
        string fileName,
        string expectedInvoiceNumber,
        decimal expectedAmount,
        string expectedCurrency,
        int expectedRows)
    {
        var invoice = KsefInvoiceReader.Load(TestFile(fileName));

        Assert.Equal(expectedInvoiceNumber, invoice.NumerFaktury);
        Assert.Equal(expectedCurrency, invoice.KodWaluty);
        Assert.Equal(expectedAmount, invoice.KwotaNaleznosci);
        Assert.Equal(expectedRows, invoice.Pozycje.Count);
    }

    [Fact]
    public void Load_MultiVat_ReadsInvoiceVatSummary()
    {
        var invoice = KsefInvoiceReader.Load(TestFile("synthetic-multi-vat.xml"));

        Assert.Equal(231m, invoice.KwotaNaleznosci);
        Assert.Contains(invoice.PodsumowanieVat,
            item => item.PodstawaOpodatkowania == 100m && item.KwotaVat == 23m);
        Assert.Contains(invoice.PodsumowanieVat,
            item => item.PodstawaOpodatkowania == 100m && item.KwotaVat == 8m);
    }

    [Fact]
    public void Load_SyntheticInvoice_DoesNotRequireRowVat()
    {
        var invoice = KsefInvoiceReader.Load(TestFile("synthetic-multi-vat.xml"));

        Assert.Equal(2, invoice.Pozycje.Count);
        Assert.All(invoice.Pozycje, row => Assert.Null(row.KwotaVAT));
        Assert.NotEmpty(invoice.PodsumowanieVat);
    }
}
