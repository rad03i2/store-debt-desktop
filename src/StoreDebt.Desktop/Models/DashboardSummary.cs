namespace StoreDebt.Desktop.Models;

public readonly record struct DashboardSummary(
    long TotalDebt,
    int CustomerCount,
    long TodayCollections
);
