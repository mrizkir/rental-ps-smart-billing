using Microsoft.Data.SqlClient;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Data;

public interface IBillingPackageRepository
{
    Task<IReadOnlyList<BillingPackage>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<BillingPackage?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
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
            SELECT Id, Name, DurationMinutes, Price, IsActive
            FROM BillingPackages
            WHERE IsActive = 1
            ORDER BY DurationMinutes
            """,
            connection);

        var items = new List<BillingPackage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingPackage
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DurationMinutes = reader.GetInt32(2),
                Price = reader.GetDecimal(3),
                IsActive = reader.GetBoolean(4)
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
            SELECT Id, Name, DurationMinutes, Price, IsActive
            FROM BillingPackages
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new BillingPackage
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            DurationMinutes = reader.GetInt32(2),
            Price = reader.GetDecimal(3),
            IsActive = reader.GetBoolean(4)
        };
    }
}
