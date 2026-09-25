namespace StoreDebt.Desktop.Services;

public static class IraqiPhoneService
{
    public const string CountryCode = "964";

    public static bool TryNormalizeForStorage(string? input, out string localNumber)
    {
        localNumber = string.Empty;
        var raw = EnglishDigits.Normalize(input).Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("00964", StringComparison.Ordinal))
            digits = digits[2..];

        if (digits.StartsWith("964", StringComparison.Ordinal))
            digits = "0" + digits[3..];

        // Iraqi mobile numbers are stored locally as 07XXXXXXXXX.
        if (digits.Length == 11 &&
            digits.StartsWith("07", StringComparison.Ordinal) &&
            digits.All(char.IsDigit))
        {
            localNumber = digits;
            return true;
        }

        return false;
    }

    public static bool TryToWhatsAppDigits(string? input, out string internationalDigits)
    {
        internationalDigits = string.Empty;

        if (!TryNormalizeForStorage(input, out var local) || string.IsNullOrEmpty(local))
            return false;

        internationalDigits = CountryCode + local[1..];
        return true;
    }

    public static string Display(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "بدون رقم هاتف";

        var display = TryNormalizeForStorage(input, out var local) && !string.IsNullOrEmpty(local)
            ? local
            : EnglishDigits.Normalize(input);

        return EnglishDigits.ToArabicDigits(display);
    }
}
