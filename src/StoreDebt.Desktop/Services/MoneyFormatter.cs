namespace StoreDebt.Desktop.Services;

public static class MoneyFormatter
{
    public static string Format(long amount) => $"{amount:N0} د.ع";

    public static bool TryParse(string? text, out long amount)
    {
        var normalized = (text ?? string.Empty)
            .Replace(",", string.Empty)
            .Replace("٬", string.Empty)
            .Trim();

        return long.TryParse(normalized, out amount) && amount > 0;
    }
}
