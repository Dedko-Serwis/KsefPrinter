namespace KsefPrinter.Models;

public enum ItemNameDisplayMode
{
    Wrap,
    Truncate
}

public enum PreferredPriceMode
{
    Net,
    Gross
}

public class AppSettings
{
    public int UpdateCheckDays { get; set; } = 7;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public double PreviewLeft { get; set; } = -1;
    public double PreviewTop { get; set; } = -1;
    public double PreviewWidth { get; set; } = 900;
    public double PreviewHeight { get; set; } = 1000;
    public double PreviewZoom { get; set; } = 100;
    public bool PreviewMaximized { get; set; }

    public int ItemNameMaxLength { get; set; } = 34;
    public bool ShowPrintPreview { get; set; } = true;

    public string PrinterName { get; set; } = "";

    // Domyślnie pełna nazwa pozycji z zawijaniem.
    public ItemNameDisplayMode ItemNameDisplay { get; set; } = ItemNameDisplayMode.Wrap;

    // Używane tylko, gdy XML zawiera jednocześnie P_9A i P_9B.
    public PreferredPriceMode PreferredPrice { get; set; } = PreferredPriceMode.Net;
}
