
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KsefPrinter.Services;

public static class KsefFileNameValidator
{
    private static readonly Regex Pattern = new(
        @"^(?<nip>[0-9]{10})-(?<date>[0-9]{8})-(?<technical>[0-9A-F]{12})-(?<crc>[0-9A-F]{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryValidate(
        string path,
        string? sellerNip,
        out string error)
    {
        string number = Path.GetFileNameWithoutExtension(path);

        // Obsługa nazw plików pobranych wielokrotnie,
        // np. 1132567365-20260901-5B39C9000021-22(1).xml
        number = Regex.Replace(number, @"\s*\(\d+\)$", "");

        var match = Pattern.Match(number);

        if (!match.Success)
        {
            error = $"Niepoprawny format numeru KSeF: {number}";
            return false;
        }

        string nip = match.Groups["nip"].Value;
        string date = match.Groups["date"].Value;
        string suppliedCrc = match.Groups["crc"].Value;

        if (!string.Equals(
                nip,
                sellerNip?.Trim(),
                StringComparison.Ordinal))
        {
            error = $"NIP w numerze KSeF ({nip}) " +
                    $"nie zgadza się z NIP sprzedawcy ({sellerNip}).";
            return false;
        }

        if (!DateTime.TryParseExact(
                date,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            error = $"Nieprawidłowa data w numerze KSeF: {date}";
            return false;
        }

        // CRC-8 obliczamy z pierwszych 32 znaków,
        // uwzględniając trzy pierwsze sekcje i myślniki.
        string dataToCheck = number[..^3];
        string expectedCrc = CalculateCrc8(dataToCheck);

        if (!string.Equals(
                suppliedCrc,
                expectedCrc,
                StringComparison.Ordinal))
        {
            error = $"Nieprawidłowa suma kontrolna numeru KSeF. " +
                    $"Odczytano: {suppliedCrc}, oczekiwano: {expectedCrc}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string CalculateCrc8(string value)
    {
        byte crc = 0x00;

        foreach (byte b in Encoding.ASCII.GetBytes(value))
        {
            crc ^= b;

            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x80) != 0
                    ? (byte)((crc << 1) ^ 0x07)
                    : (byte)(crc << 1);
            }
        }

        return crc.ToString("X2", CultureInfo.InvariantCulture);
    }
}
