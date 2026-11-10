using KsefPrinter.Services;
using Xunit;

namespace KsefPrinter.Tests;

public class InvoiceValueResolverEdgeCaseTests
{
    private static string File(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", name);

    [Fact]
    public void MultipleVatRates_AreSummedAtInvoiceLevel()
    {
        var i=KsefInvoiceReader.Load(File("synthetic-multi-vat.xml"));
        var v=InvoiceValueResolver.Resolve(i);
        Assert.Equal(200m,v.Netto); Assert.Equal(31m,v.Vat); Assert.Equal(231m,v.Brutto);
    }

    [Fact]
    public void ZeroPercentVat_CalculatesZeroVatAndGross()
    {
        var i=KsefInvoiceReader.Load(File("synthetic-vat-zero.xml"));
        var v=InvoiceValueResolver.Resolve(Assert.Single(i.Pozycje));
        Assert.Equal(50m,v.WartoscNetto); Assert.Equal(0m,v.Vat); Assert.Equal(50m,v.WartoscBrutto);
        Assert.True(v.VatWyliczony); Assert.True(v.WartoscBruttoWyliczona);
    }

    [Theory]
    [InlineData("synthetic-vat-zw.xml")]
    [InlineData("synthetic-vat-np.xml")]
    public void NonNumericVatRate_IsNotInvented(string name)
    {
        var i=KsefInvoiceReader.Load(File(name));
        var v=InvoiceValueResolver.Resolve(Assert.Single(i.Pozycje));
        Assert.Equal(50m,v.WartoscNetto); Assert.Null(v.Vat); Assert.Null(v.WartoscBrutto);
        Assert.False(v.VatWyliczony); Assert.False(v.WartoscBruttoWyliczona);
    }

    [Fact]
    public void MoneyCalculation_RoundsToTwoDecimals()
    {
        var i=KsefInvoiceReader.Load(File("synthetic-rounding.xml"));
        var v=InvoiceValueResolver.Resolve(Assert.Single(i.Pozycje));
        Assert.Equal(10m,v.WartoscNetto); Assert.Equal(2.30m,v.Vat); Assert.Equal(12.30m,v.WartoscBrutto);
    }

    [Fact]
    public void MissingOptionalFields_DoNotBreakInterpretation()
    {
        var i=KsefInvoiceReader.Load(File("synthetic-missing.xml"));
        var v=InvoiceValueResolver.Resolve(Assert.Single(i.Pozycje));
        Assert.Equal(10m,v.WartoscNetto); Assert.Null(v.CenaNetto); Assert.Null(v.CenaBrutto);
        Assert.Null(v.Vat); Assert.Null(v.WartoscBrutto);
    }
}
