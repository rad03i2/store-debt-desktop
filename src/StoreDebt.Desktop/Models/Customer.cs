namespace StoreDebt.Desktop.Models;

public sealed class Customer
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public long TotalDebt { get; init; }
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; init; }

    public string DebtText => $"{TotalDebt:N0} د.ع";
    public string ContactText => string.IsNullOrWhiteSpace(Phone) ? "بدون رقم هاتف" : Phone;
}
