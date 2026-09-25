using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StoreDebt.Desktop.Models;

namespace StoreDebt.Desktop.Services;

public sealed class StatementService
{
    private const int ReceiptWidth = 1280;
    private const int MaxImageTransactions = 200;
    private static readonly CultureInfo DrawingCulture = CultureInfo.GetCultureInfo("ar-IQ");

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

    public void CopyText(string text) => Clipboard.SetText(text);

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
            "دفتر المحل - إدارة الديون والحسابات",
            $"الاسم: {customer.Name}",
            $"الهاتف: {IraqiPhoneService.Display(customer.Phone)}",
            $"تاريخ الكشف: {EnglishDigits.DateTime(generatedAt)}",
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

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderReceipt(
        Customer customer,
        IReadOnlyList<DebtTransaction> transactions,
        DateTime generatedAt)
    {
        var visible = transactions.Take(MaxImageTransactions).Reverse().ToList();
        const int rowHeight = 82;
        const int headerHeight = 690;
        var footerHeight = transactions.Count > MaxImageTransactions ? 150 : 105;
        var height = headerHeight + (visible.Count * rowHeight) + footerHeight;

        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ReceiptWidth, height));

            var primary = new SolidColorBrush(Color.FromRgb(23, 107, 69));
            var dark = new SolidColorBrush(Color.FromRgb(21, 35, 28));
            var muted = new SolidColorBrush(Color.FromRgb(104, 117, 110));
            var soft = new SolidColorBrush(Color.FromRgb(247, 249, 248));
            var borderBrush = new SolidColorBrush(Color.FromRgb(221, 230, 225));
            var border = new Pen(borderBrush, 1.4);
            var debt = new SolidColorBrush(Color.FromRgb(194, 59, 59));
            var payment = new SolidColorBrush(Color.FromRgb(17, 130, 75));

            DrawText(dc, "كشف حساب", 48, FontWeights.Bold, dark, 70, 58, ReceiptWidth - 140);
            DrawText(dc, "دفتر المحل - إدارة الديون والحسابات", 22, FontWeights.Normal, muted, 70, 122, ReceiptWidth - 140);
            dc.DrawLine(new Pen(primary, 4), new Point(70, 175), new Point(ReceiptWidth - 70, 175));

            var infoBox = new Rect(70, 205, ReceiptWidth - 140, 190);
            dc.DrawRoundedRectangle(soft, border, infoBox, 18, 18);

            DrawLabelValue(dc, "الاسم", customer.Name, 92, 230, 500, dark, muted);
            DrawLabelValue(dc, "الهاتف", IraqiPhoneService.Display(customer.Phone), 670, 230, 480, dark, muted);
            DrawLabelValue(dc, "تاريخ الكشف", EnglishDigits.DateTime(generatedAt), 92, 305, 500, dark, muted);
            DrawLabelValue(dc, "عدد الحركات", EnglishDigits.Number(transactions.Count), 670, 305, 480, dark, muted);

            var balanceBox = new Rect(70, 425, ReceiptWidth - 140, 145);
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(255, 245, 245)),
                new Pen(new SolidColorBrush(Color.FromRgb(244, 210, 210)), 1.5),
                balanceBox,
                20,
                20);
            DrawText(dc, "الرصيد الحالي", 22, FontWeights.SemiBold, muted, 92, 447, ReceiptWidth - 184);
            DrawText(dc, MoneyFormatter.Format(customer.TotalDebt), 48, FontWeights.Bold, debt, 92, 490, ReceiptWidth - 184);

            DrawText(dc, "الحركات", 28, FontWeights.Bold, dark, 70, 610, ReceiptWidth - 140);

            var tableTop = 656d;
            var tableWidth = ReceiptWidth - 140d;
            var x = 70d;

            dc.DrawRoundedRectangle(soft, border, new Rect(x, tableTop, tableWidth, 58), 12, 12);

            // RTL visual order: amount, details, date/time, type.
            DrawText(dc, "المبلغ", 18, FontWeights.SemiBold, dark, x + 20, tableTop + 16, 210);
            DrawText(dc, "التفاصيل", 18, FontWeights.SemiBold, dark, x + 245, tableTop + 16, 330);
            DrawText(dc, "التاريخ والوقت", 18, FontWeights.SemiBold, dark, x + 590, tableTop + 16, 330);
            DrawText(dc, "نوع الحركة", 18, FontWeights.SemiBold, dark, x + 935, tableTop + 16, 205);

            var y = tableTop + 58;

            foreach (var tx in visible)
            {
                dc.DrawLine(border, new Point(x, y + rowHeight), new Point(x + tableWidth, y + rowHeight));

                var amountBrush = tx.Type == TransactionType.Debt ? debt : payment;
                var summary = BuildTransactionSummary(tx);

                DrawText(dc, tx.AmountText, 20, FontWeights.Bold, amountBrush, x + 20, y + 18, 210);
                DrawText(dc, summary, 17, FontWeights.Normal, dark, x + 245, y + 17, 330);
                DrawText(dc, tx.DateTimeText, 17, FontWeights.Normal, muted, x + 590, y + 17, 330);
                DrawText(dc, tx.TypeText, 18, FontWeights.SemiBold, dark, x + 935, y + 17, 205);
                DrawText(dc, $"الرصيد بعد الحركة: {tx.BalanceText}", 14, FontWeights.Normal, muted, x + 20, y + 48, 555);

                y += rowHeight;
            }

            y += 28;

            if (transactions.Count > MaxImageTransactions)
            {
                DrawText(
                    dc,
                    $"يعرض الكشف أحدث {EnglishDigits.Number(MaxImageTransactions)} حركة من أصل {EnglishDigits.Number(transactions.Count)} حركة.",
                    16,
                    FontWeights.Normal,
                    muted,
                    70,
                    y,
                    ReceiptWidth - 140);
                y += 42;
            }

            DrawText(
                dc,
                "هذا الكشف صادر من برنامج دفتر المحل.",
                15,
                FontWeights.Normal,
                muted,
                70,
                y,
                ReceiptWidth - 140);
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

    private static void DrawLabelValue(
        DrawingContext dc,
        string label,
        string value,
        double x,
        double y,
        double width,
        Brush valueBrush,
        Brush labelBrush)
    {
        DrawText(dc, label, 16, FontWeights.Normal, labelBrush, x, y, width);
        DrawText(dc, value, 23, FontWeights.SemiBold, valueBrush, x, y + 28, width);
    }

    private static string BuildTransactionSummary(DebtTransaction tx)
    {
        var source = !string.IsNullOrWhiteSpace(tx.ItemsSummary)
            ? tx.ItemsSummary
            : tx.Note;

        if (string.IsNullOrWhiteSpace(source))
            return "بدون تفاصيل";

        source = source.Replace(Environment.NewLine, " ").Trim();
        return source.Length <= 48 ? source : source[..45] + "...";
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
            EnglishDigits.ToArabicDigits(text),
            DrawingCulture,
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
