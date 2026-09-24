using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StoreDebt.Desktop.Models;

namespace StoreDebt.Desktop.Services;

public sealed class StatementService
{
    private const int ReceiptWidth = 1200;
    private const int MaxImageTransactions = 200;
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-IQ");

    public StatementReceipt Create(
        Customer customer,
        IReadOnlyList<DebtTransaction> transactions)
    {
        var generatedAt = DateTime.Now;
        var text = BuildShareText(customer, transactions, generatedAt);
        var imagePath = RenderReceipt(customer, transactions, generatedAt);

        return new StatementReceipt
        {
            Text = text,
            ImagePath = imagePath,
            GeneratedAtText = EnglishDigits.DateTime(generatedAt),
            TransactionCount = transactions.Count
        };
    }

    public void CopyText(string text)
    {
        Clipboard.SetText(text);
    }

    public void CopyImage(string imagePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        Clipboard.SetImage(image);
    }

    public void OpenImage(string imagePath)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = imagePath,
            UseShellExecute = true
        });
    }

    public void OpenFolder(string imagePath)
    {
        var folder = Path.GetDirectoryName(imagePath);
        if (string.IsNullOrWhiteSpace(folder)) return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private static string BuildShareText(
        Customer customer,
        IReadOnlyList<DebtTransaction> transactions,
        DateTime generatedAt)
    {
        var lines = new List<string>
        {
            "كشف حساب - دفتر المحل",
            $"الزبون: {customer.Name}",
            $"رقم الهاتف: {IraqiPhoneService.Display(customer.Phone)}",
            $"التاريخ: {EnglishDigits.DateTime(generatedAt)}",
            $"عدد الحركات: {EnglishDigits.Number(transactions.Count)}",
            $"الرصيد الحالي: {MoneyFormatter.Format(customer.TotalDebt)}",
            string.Empty,
            "أحدث الحركات:"
        };

        foreach (var tx in transactions.Take(10))
        {
            lines.Add(
                $"{tx.DateTimeText} | {tx.TypeText} | {tx.AmountText} | الرصيد {tx.BalanceText}");
        }

        if (transactions.Count > 10)
        {
            lines.Add($"... وباقي {EnglishDigits.Number(transactions.Count - 10)} حركة موضحة في صورة الكشف.");
        }

        lines.Add(string.Empty);
        lines.Add("صورة كشف الحساب جاهزة للإرفاق مع هذه الرسالة.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderReceipt(
        Customer customer,
        IReadOnlyList<DebtTransaction> transactions,
        DateTime generatedAt)
    {
        var visible = transactions.Take(MaxImageTransactions).Reverse().ToList();
        var rowHeight = 78;
        var baseHeight = 520;
        var footerHeight = transactions.Count > MaxImageTransactions ? 150 : 100;
        var height = baseHeight + (visible.Count * rowHeight) + footerHeight;

        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ReceiptWidth, height));

            var primary = new SolidColorBrush(Color.FromRgb(23, 107, 69));
            var dark = new SolidColorBrush(Color.FromRgb(21, 35, 28));
            var muted = new SolidColorBrush(Color.FromRgb(104, 117, 110));
            var border = new Pen(new SolidColorBrush(Color.FromRgb(221, 230, 225)), 1.5);
            var debt = new SolidColorBrush(Color.FromRgb(194, 59, 59));
            var payment = new SolidColorBrush(Color.FromRgb(17, 130, 75));

            DrawText(dc, "كشف حساب", 46, FontWeights.Bold, primary, 60, 54, ReceiptWidth - 120);
            DrawText(dc, "دفتر المحل - إدارة الديون والحسابات", 22, FontWeights.Normal, muted, 60, 118, ReceiptWidth - 120);

            dc.DrawLine(border, new Point(60, 170), new Point(ReceiptWidth - 60, 170));

            DrawText(dc, $"الزبون: {customer.Name}", 30, FontWeights.SemiBold, dark, 60, 198, ReceiptWidth - 120);
            DrawText(dc, $"الهاتف: {IraqiPhoneService.Display(customer.Phone)}", 22, FontWeights.Normal, muted, 60, 246, ReceiptWidth - 120);
            DrawText(dc, $"تاريخ الكشف: {EnglishDigits.DateTime(generatedAt)}", 22, FontWeights.Normal, muted, 60, 282, ReceiptWidth - 120);
            DrawText(dc, $"عدد الحركات: {EnglishDigits.Number(transactions.Count)}", 22, FontWeights.Normal, muted, 60, 318, ReceiptWidth - 120);

            var debtBox = new Rect(60, 365, ReceiptWidth - 120, 105);
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(255, 244, 244)),
                new Pen(new SolidColorBrush(Color.FromRgb(244, 210, 210)), 1.5),
                debtBox,
                18,
                18);

            DrawText(dc, "الرصيد الحالي", 20, FontWeights.SemiBold, muted, 82, 386, ReceiptWidth - 164);
            DrawText(dc, MoneyFormatter.Format(customer.TotalDebt), 38, FontWeights.Bold, debt, 82, 420, ReceiptWidth - 164);

            var y = 505d;
            DrawText(dc, "الحركات", 26, FontWeights.Bold, dark, 60, y, ReceiptWidth - 120);
            y += 48;

            foreach (var tx in visible)
            {
                dc.DrawLine(border, new Point(60, y), new Point(ReceiptWidth - 60, y));

                var amountBrush = tx.Type == TransactionType.Debt ? debt : payment;
                var summary = BuildTransactionSummary(tx);

                DrawText(dc, tx.TypeText, 20, FontWeights.SemiBold, dark, 70, y + 12, 180);
                DrawText(dc, tx.DateTimeText, 17, FontWeights.Normal, muted, 260, y + 13, 290);
                DrawText(dc, summary, 17, FontWeights.Normal, dark, 570, y + 13, 320);
                DrawText(dc, tx.AmountText, 20, FontWeights.Bold, amountBrush, 900, y + 10, 230);
                DrawText(dc, $"الرصيد: {tx.BalanceText}", 15, FontWeights.Normal, muted, 900, y + 42, 230);

                y += rowHeight;
            }

            dc.DrawLine(border, new Point(60, y), new Point(ReceiptWidth - 60, y));
            y += 24;

            if (transactions.Count > MaxImageTransactions)
            {
                DrawText(
                    dc,
                    $"يعرض الوصل أحدث {EnglishDigits.Number(MaxImageTransactions)} حركة من أصل {EnglishDigits.Number(transactions.Count)} حركة.",
                    17,
                    FontWeights.Normal,
                    muted,
                    60,
                    y,
                    ReceiptWidth - 120);
                y += 42;
            }

            DrawText(
                dc,
                "هذا الكشف صادر من برنامج دفتر المحل.",
                16,
                FontWeights.Normal,
                muted,
                60,
                y,
                ReceiptWidth - 120);
        }

        var bitmap = new RenderTargetBitmap(
            ReceiptWidth,
            height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StoreDebtDesktop",
            "Statements");
        Directory.CreateDirectory(folder);

        var stamp = generatedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, $"Statement_{customer.Id}_{stamp}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);

        return path;
    }

    private static string BuildTransactionSummary(DebtTransaction tx)
    {
        var source = !string.IsNullOrWhiteSpace(tx.ItemsSummary)
            ? tx.ItemsSummary
            : tx.Note;

        if (string.IsNullOrWhiteSpace(source))
            return "بدون تفاصيل";

        source = source.Replace(Environment.NewLine, " ").Trim();
        return source.Length <= 42 ? source : source[..39] + "...";
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        double fontSize,
        FontWeight weight,
        Brush brush,
        double x,
        double y,
        double width)
    {
        var formatted = new FormattedText(
            EnglishDigits.Normalize(text),
            ArabicCulture,
            FlowDirection.RightToLeft,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize,
            brush,
            1.0)
        {
            MaxTextWidth = Math.Max(1, width),
            TextAlignment = TextAlignment.Right,
            Trimming = TextTrimming.CharacterEllipsis
        };

        dc.DrawText(formatted, new Point(x, y));
    }
}
