namespace StoreDebt.Desktop.Models;

public sealed class StatementReceipt
{
    public required string Text { get; init; }
    public required string ImagePath { get; init; }
    public required string GeneratedAtText { get; init; }
    public int TransactionCount { get; init; }
}
