using KsefPrinter.Services;
using Xunit;

namespace KsefPrinter.Tests;

/// <summary>
/// Test regresyjny wszystkich syntetycznych faktur XML dostarczanych z projektem.
/// Nie korzysta z prywatnych faktur ani katalogów roboczych.
/// </summary>
public class KsefInvoiceReaderAllRealFilesTests
{
    private static readonly string[] ExpectedFiles =
    [
        "synthetic-missing.xml",
        "synthetic-multi-vat.xml",
        "synthetic-rounding.xml",
        "synthetic-vat-np.xml",
        "synthetic-vat-zero.xml",
        "synthetic-vat-zw.xml"
    ];

    private static string TestDataDirectory =>
        Path.Combine(AppContext.BaseDirectory, "TestData");

    public static IEnumerable<object[]> SyntheticInvoiceFiles() =>
        ExpectedFiles.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(SyntheticInvoiceFiles))]
    public void Load_AllSyntheticInvoices_ParsesWithoutError(string fileName)
    {
        var path = Path.Combine(TestDataDirectory, fileName);
        Assert.True(File.Exists(path), $"Brak syntetycznego pliku testowego: {path}");

        var invoice = KsefInvoiceReader.Load(path);

        Assert.NotNull(invoice);
        Assert.False(string.IsNullOrWhiteSpace(invoice.NumerFaktury));
        Assert.Equal("PLN", invoice.KodWaluty);
        Assert.NotEmpty(invoice.Pozycje);
    }

    [Fact]
    public void TestDataDirectory_ContainsExactlyExpectedSyntheticInvoices()
    {
        Assert.True(Directory.Exists(TestDataDirectory));

        var actual = Directory.GetFiles(TestDataDirectory, "synthetic-*.xml")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExpectedFiles.OrderBy(name => name, StringComparer.Ordinal),
            actual);
    }
}
