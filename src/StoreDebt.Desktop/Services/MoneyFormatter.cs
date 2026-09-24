using System.Globalization;

namespace StoreDebt.Desktop.Services;

public static class MoneyFormatter
{
    public static string Format(long amount)
    {
        var formatted = amount.ToString("N0", CultureInfo.InvariantCulture);
        return $"{ArabicDigits(formatted)} د.ع";
    }

    private static string ArabicDigits(string value)
    {
        return value
            .Replace('0', '٠')
            .Replace('1', '١')
            .Replace('2', '٢')
            .Replace('3', '٣')
            .Replace('4', '٤')
            .Replace('5', '٥')
            .Replace('6', '٦')
            .Replace('7', '٧')
            .Replace('8', '٨')
            .Replace('9', '٩');
    }

    public static bool TryParse(string? text, out long amount)
    {
        var normalized = EnglishDigits.Normalize(text)
            .Replace(",", string.Empty)
            .Replace("٬", string.Empty)
            .Trim();

        return long.TryParse(
            normalized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }
}
