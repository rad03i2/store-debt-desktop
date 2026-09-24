using System.Globalization;

namespace StoreDebt.Desktop.Services;

public static class EnglishDigits
{
    private static readonly char[] ArabicDigits = ['٠','١','٢','٣','٤','٥','٦','٧','٨','٩'];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return value
            .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
            .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9')
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9');
    }

    public static string Number(long value) => ToArabicDigits(value.ToString("N0", CultureInfo.InvariantCulture));

    public static string DateTime(DateTime value) =>
        ToArabicDigits(value.ToString("yyyy/MM/dd - h:mm tt", CultureInfo.InvariantCulture)
            .Replace("AM", "ص")
            .Replace("PM", "م"));

    public static string Date(DateTime value) =>
        ToArabicDigits(value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));

    public static string ToArabicDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= '0' && chars[i] <= '9')
                chars[i] = ArabicDigits[chars[i] - '0'];
        }
        return new string(chars);
    }
}
