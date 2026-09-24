using Microsoft.Data.Sqlite;
using StoreDebt.Desktop.Models;

namespace StoreDebt.Desktop.Data;

public sealed class StoreDatabase
{
    private readonly string _connectionString;

    public StoreDatabase()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDirectory = Path.Combine(appData, "StoreDebtDesktop");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "store-debt.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = """
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                """;
            await pragma.ExecuteNonQueryAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT NOT NULL DEFAULT '',
                Address TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                TotalDebt INTEGER NOT NULL DEFAULT 0,
                CreatedAt INTEGER NOT NULL,
                UpdatedAt INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                Type INTEGER NOT NULL,
                Amount INTEGER NOT NULL,
                BalanceAfter INTEGER NOT NULL,
                ItemsSummary TEXT NOT NULL DEFAULT '',
                Note TEXT NOT NULL DEFAULT '',
                Timestamp INTEGER NOT NULL,
                FOREIGN KEY(CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Customers_Name ON Customers(Name);
            CREATE INDEX IF NOT EXISTS IX_Customers_UpdatedAt ON Customers(UpdatedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_Transactions_CustomerId_Timestamp
                ON Transactions(CustomerId, Timestamp DESC);
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync()
    {
        var items = new List<Customer>();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Phone, Address, Notes, TotalDebt, CreatedAt, UpdatedAt
            FROM Customers
            ORDER BY TotalDebt DESC, UpdatedAt DESC, Name COLLATE NOCASE;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new Customer
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Phone = reader.GetString(2),
                Address = reader.GetString(3),
                Notes = reader.GetString(4),
                TotalDebt = reader.GetInt64(5),
                CreatedAt = reader.GetInt64(6),
                UpdatedAt = reader.GetInt64(7)
            });
        }

        return items;
    }

    public async Task<long> AddCustomerAsync(
        string name,
        string phone,
        string address,
        string notes)
    {
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Customers (Name, Phone, Address, Notes, TotalDebt, CreatedAt, UpdatedAt)
            VALUES ($name, $phone, $address, $notes, 0, $now, $now);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$phone", phone.Trim());
        command.Parameters.AddWithValue("$address", address.Trim());
        command.Parameters.AddWithValue("$notes", notes.Trim());
        command.Parameters.AddWithValue("$now", now);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    public async Task<IReadOnlyList<DebtTransaction>> GetTransactionsAsync(long customerId)
    {
        var items = new List<DebtTransaction>();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CustomerId, Type, Amount, BalanceAfter, ItemsSummary, Note, Timestamp
            FROM Transactions
            WHERE CustomerId = $customerId
            ORDER BY Timestamp DESC, Id DESC
            LIMIT 250;
            """;
        command.Parameters.AddWithValue("$customerId", customerId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DebtTransaction
            {
                Id = reader.GetInt64(0),
                CustomerId = reader.GetInt64(1),
                Type = (TransactionType)reader.GetInt32(2),
                Amount = reader.GetInt64(3),
                BalanceAfter = reader.GetInt64(4),
                ItemsSummary = reader.GetString(5),
                Note = reader.GetString(6),
                Timestamp = reader.GetInt64(7)
            });
        }

        return items;
    }

    public async Task RecordDebtAsync(
        long customerId,
        long amount,
        string itemsSummary,
        string note)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var currentDebt = await GetCurrentDebtAsync(connection, transaction, customerId);
        var newDebt = checked(currentDebt + amount);
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        await InsertTransactionAsync(
            connection,
            transaction,
            customerId,
            TransactionType.Debt,
            amount,
            newDebt,
            itemsSummary,
            note,
            now);

        await UpdateCustomerDebtAsync(connection, transaction, customerId, newDebt, now);
        transaction.Commit();
    }

    public async Task RecordPaymentAsync(long customerId, long amount, string note)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var currentDebt = await GetCurrentDebtAsync(connection, transaction, customerId);
        if (amount > currentDebt)
        {
            throw new InvalidOperationException("مبلغ التسديد أكبر من الدين الحالي.");
        }

        var newDebt = currentDebt - amount;
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        await InsertTransactionAsync(
            connection,
            transaction,
            customerId,
            TransactionType.Payment,
            amount,
            newDebt,
            string.Empty,
            note,
            now);

        await UpdateCustomerDebtAsync(connection, transaction, customerId, newDebt, now);
        transaction.Commit();
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        long totalDebt;
        int customerCount;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COALESCE(SUM(TotalDebt), 0), COUNT(*)
                FROM Customers;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            totalDebt = reader.GetInt64(0);
            customerCount = reader.GetInt32(1);
        }

        var now = DateTimeOffset.Now;
        var startOfToday = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            0,
            0,
            0,
            now.Offset).ToUnixTimeMilliseconds();

        long todayCollections;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COALESCE(SUM(Amount), 0)
                FROM Transactions
                WHERE Type = $paymentType AND Timestamp >= $startOfToday;
                """;
            command.Parameters.AddWithValue("$paymentType", (int)TransactionType.Payment);
            command.Parameters.AddWithValue("$startOfToday", startOfToday);

            todayCollections = Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
        }

        return new DashboardSummary(totalDebt, customerCount, todayCollections);
    }

    private static async Task<long> GetCurrentDebtAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long customerId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TotalDebt FROM Customers WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", customerId);

        var value = await command.ExecuteScalarAsync();
        if (value is null)
        {
            throw new InvalidOperationException("الزبون غير موجود.");
        }

        return Convert.ToInt64(value);
    }

    private static async Task InsertTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long customerId,
        TransactionType type,
        long amount,
        long balanceAfter,
        string itemsSummary,
        string note,
        long timestamp)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Transactions
                (CustomerId, Type, Amount, BalanceAfter, ItemsSummary, Note, Timestamp)
            VALUES
                ($customerId, $type, $amount, $balanceAfter, $itemsSummary, $note, $timestamp);
            """;
        command.Parameters.AddWithValue("$customerId", customerId);
        command.Parameters.AddWithValue("$type", (int)type);
        command.Parameters.AddWithValue("$amount", amount);
        command.Parameters.AddWithValue("$balanceAfter", balanceAfter);
        command.Parameters.AddWithValue("$itemsSummary", itemsSummary.Trim());
        command.Parameters.AddWithValue("$note", note.Trim());
        command.Parameters.AddWithValue("$timestamp", timestamp);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateCustomerDebtAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long customerId,
        long newDebt,
        long updatedAt)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Customers
            SET TotalDebt = $newDebt, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$newDebt", newDebt);
        command.Parameters.AddWithValue("$updatedAt", updatedAt);
        command.Parameters.AddWithValue("$id", customerId);

        if (await command.ExecuteNonQueryAsync() != 1)
        {
            throw new InvalidOperationException("تعذر تحديث رصيد الزبون.");
        }
    }
}
