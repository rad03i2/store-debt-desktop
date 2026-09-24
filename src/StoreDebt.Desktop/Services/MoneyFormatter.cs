using System.Globalization;

namespace StoreDebt.Desktop.Services;

public static class MoneyFormatter
{
    public static string Format(long amount) =>
        $"{amount.ToString("N0", CultureInfo.InvariantCulture)} د.ع";

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
