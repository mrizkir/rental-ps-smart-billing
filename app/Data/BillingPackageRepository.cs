using Microsoft.Data.SqlClient;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Data;

public interface IBillingPackageRepository
{
    Task<IReadOnlyList<BillingPackage>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPackageListItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BillingPackage?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(
        int id,
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveSessionsAsync(int packageId, CancellationToken cancellationToken = default);
}

public sealed class BillingPackageRepository : IBillingPackageRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BillingPackageRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<BillingPackage>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Name, DurationMinutes, Price, IsActive, BillingMode
            FROM BillingPackages
            WHERE IsActive = 1
            ORDER BY
                CASE WHEN BillingMode = 'OpenEnded' THEN 1 ELSE 0 END,
                DurationMinutes,
                Name
            """,
            connection);

        var items = new List<BillingPackage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadPackage(reader));

        return items;
    }

    public async Task<IReadOnlyList<BillingPackageListItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Name, DurationMinutes, Price, IsActive, BillingMode
            FROM BillingPackages
            ORDER BY
                CASE WHEN BillingMode = 'OpenEnded' THEN 1 ELSE 0 END,
                DurationMinutes,
                Name
            """,
            connection);

        var items = new List<BillingPackageListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingPackageListItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DurationMinutes = reader.GetInt32(2),
                Price = reader.GetDecimal(3),
                IsActive = reader.GetBoolean(4),
                BillingMode = reader.GetString(5)
            });
        }

        return items;
    }

    public async Task<BillingPackage?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Name, DurationMinutes, Price, IsActive, BillingMode
            FROM BillingPackages
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadPackage(reader);
    }

    public async Task CreateAsync(
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            INSERT INTO BillingPackages (Name, DurationMinutes, Price, BillingMode)
            VALUES (@Name, @DurationMinutes, @Price, @BillingMode)
            """,
            connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@DurationMinutes", durationMinutes);
        command.Parameters.AddWithValue("@Price", price);
        command.Parameters.AddWithValue("@BillingMode", billingMode);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE BillingPackages
            SET Name = @Name,
                DurationMinutes = @DurationMinutes,
                Price = @Price,
                BillingMode = @BillingMode,
                IsActive = @IsActive
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@DurationMinutes", durationMinutes);
        command.Parameters.AddWithValue("@Price", price);
        command.Parameters.AddWithValue("@BillingMode", billingMode);
        command.Parameters.AddWithValue("@IsActive", isActive);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Paket tidak ditemukan.");
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "UPDATE BillingPackages SET IsActive = 0 WHERE Id = @Id",
            connection);
        command.Parameters.AddWithValue("@Id", id);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Paket tidak ditemukan.");
    }

    public async Task<bool> HasActiveSessionsAsync(int packageId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM RentalSessions
            WHERE PackageId = @PackageId AND Status = 'Active'
            """,
            connection);
        command.Parameters.AddWithValue("@PackageId", packageId);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    private static BillingPackage ReadPackage(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        DurationMinutes = reader.GetInt32(2),
        Price = reader.GetDecimal(3),
        IsActive = reader.GetBoolean(4),
        BillingMode = reader.GetString(5)
    };
}
