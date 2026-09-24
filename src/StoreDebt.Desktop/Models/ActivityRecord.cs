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
    public string AmountText => Type == TransactionType.Debt ? $"+{Amount:N0} د.ع" : $"-{Amount:N0} د.ع";
    public string BalanceText => $"{BalanceAfter:N0} د.ع";
    public string DateTimeText => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp)
        .LocalDateTime.ToString("yyyy/MM/dd - hh:mm tt");
}
