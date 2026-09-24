using StoreDebt.Desktop.Services;

namespace StoreDebt.Desktop.Models;

public sealed class ActivityRecord
{
    public long TransactionId { get; init; }
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public TransactionType Type { get; init; }
    public long Amount { get; init; }
    public long BalanceAfter { get; init; }
    public string ItemsSummary { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public long Timestamp { get; init; }

    public string TypeText => Type == TransactionType.Debt ? "دين جديد" : "تسديد";
    public string AmountText => Type == TransactionType.Debt
        ? $"+{EnglishDigits.Number(Amount)} د.ع"
        : $"-{EnglishDigits.Number(Amount)} د.ع";
    public string BalanceText => MoneyFormatter.Format(BalanceAfter);
    public string DateTimeText => EnglishDigits.DateTime(
        DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).LocalDateTime);
}
