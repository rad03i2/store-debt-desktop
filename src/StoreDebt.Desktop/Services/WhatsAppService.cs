using System.ComponentModel;
using System.Diagnostics;
using StoreDebt.Desktop.Models;

namespace StoreDebt.Desktop.Services;

public sealed class WhatsAppService
{
    private readonly StatementService _statementService;

    public WhatsAppService(StatementService statementService)
    {
        _statementService = statementService;
    }

    public string OpenStatementInDesktopApp(Customer customer, StatementReceipt receipt)
    {
        if (!IraqiPhoneService.TryToWhatsAppDigits(customer.Phone, out var phone))
        {
            throw new InvalidOperationException(
                "لا يوجد رقم هاتف عراقي صالح لهذا الزبون. أدخل رقماً مثل 07701234567.");
        }

        // WhatsApp's URI scheme can open the installed app and prefill text.
        // The documented URI does not provide a local-file attachment parameter,
        // so the receipt image is also copied to the clipboard ready to paste.
        _statementService.CopyImage(receipt.ImagePath);

        var encodedText = Uri.EscapeDataString(receipt.Text);
        var uri = $"whatsapp://send?phone={phone}&text={encodedText}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException(
                "تعذر فتح تطبيق واتساب على ويندوز. تأكد من تثبيت واتساب وتسجيل الدخول إليه.");
        }

        return "تم فتح تطبيق واتساب وتجهيز نص التنبيه وصورة كشف الحساب.";
    }
}
