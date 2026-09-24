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

    public string OpenStatementChat(Customer customer, StatementReceipt receipt)
    {
        if (!IraqiPhoneService.TryToWhatsAppDigits(customer.Phone, out var phone))
        {
            throw new InvalidOperationException(
                "لا يوجد رقم هاتف عراقي صالح لهذا الزبون. أدخل رقماً مثل 07701234567.");
        }

        // Official wa.me links can prefill text, but they do not attach a local image.
        // Copy the receipt image to the clipboard so the user only needs Ctrl+V in the chat.
        _statementService.CopyImage(receipt.ImagePath);

        var encodedText = Uri.EscapeDataString(receipt.Text);
        var url = $"https://wa.me/{phone}?text={encodedText}";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        return "تم فتح محادثة واتساب بالنص الجاهز، وصورة الوصل منسوخة. اضغط Ctrl+V داخل المحادثة لإرفاقها ثم أرسل.";
    }
}
