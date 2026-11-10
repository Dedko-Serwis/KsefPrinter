namespace KsefPrinter.Services;

public class KsefInvoiceReadException : Exception
{
    public KsefInvoiceReadException(string message)
        : base(message)
    {
    }

    public KsefInvoiceReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
