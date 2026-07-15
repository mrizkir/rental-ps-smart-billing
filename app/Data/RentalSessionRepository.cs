using Microsoft.Data.SqlClient;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Data;

public interface IRentalSessionRepository
{
    Task<IReadOnlyList<UnitCardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<RentalSession?> GetActiveByTvIdAsync(int smartTvId, CancellationToken cancellationToken = default);
    Task<RentalSession?> GetByIdAsync(int sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalSession>> GetExpiredActiveAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(
        int smartTvId,
        int packageId,
        string? customerName,
        DateTime startedAt,
        DateTime? endsAt,
        decimal amount,
        int? startedByUserId,
        CancellationToken cancellationToken = default);
    Task ExtendAsync(
        int sessionId,
        DateTime newEndsAt,
        decimal newAmount,
        CancellationToken cancellationToken = default);
    Task ConvertToFreePlayAsync(
        int sessionId,
        int freePlayPackageId,
        DateTime openEndedFrom,
        CancellationToken cancellationToken = default);
    Task CompleteAsync(
        int sessionId,
        DateTime endedAt,
        decimal amount,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevenueReportItem>> GetCompletedRevenueAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default);
}

public sealed class RentalSessionRepository : IRentalSessionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public RentalSessionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<UnitCardItem>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                t.Id,
                t.Name,
                t.IpAddress,
                t.MacAddress,
                t.WsPort,
                t.Token,
                s.Id,
                s.CustomerName,
                p.Name,
                s.StartedAt,
                s.EndsAt,
                ISNULL(s.Amount, 0),
                ISNULL(p.Price, 0),
                ISNULL(p.BillingMode, 'Fixed'),
                s.OpenEndedFrom
            FROM SmartTvs t
            LEFT JOIN RentalSessions s
                ON s.SmartTvId = t.Id AND s.Status = 'Active'
            LEFT JOIN BillingPackages p ON p.Id = s.PackageId
            WHERE t.IsActive = 1
            ORDER BY t.Name
            """,
            connection);

        var items = new List<UnitCardItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new UnitCardItem
            {
                SmartTvId = reader.GetInt32(0),
                TvName = reader.GetString(1),
                IpAddress = reader.GetString(2),
                MacAddress = reader.GetString(3),
                WsPort = reader.GetInt32(4),
                Token = reader.IsDBNull(5) ? null : reader.GetString(5),
                SessionId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                CustomerName = reader.IsDBNull(7) ? null : reader.GetString(7),
                PackageName = reader.IsDBNull(8) ? null : reader.GetString(8),
                StartedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                EndsAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                Amount = reader.GetDecimal(11),
                PackagePrice = reader.GetDecimal(12),
                BillingMode = reader.GetString(13),
                OpenEndedFrom = reader.IsDBNull(14) ? null : reader.GetDateTime(14)
            });
        }

        return items;
    }

    public async Task<RentalSession?> GetActiveByTvIdAsync(int smartTvId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                s.Id, s.SmartTvId, s.PackageId, s.CustomerName,
                s.StartedAt, s.EndsAt, s.EndedAt, s.Status, s.Amount, s.StartedByUserId,
                p.Name, p.Price, ISNULL(p.BillingMode, 'Fixed'), s.OpenEndedFrom
            FROM RentalSessions s
            LEFT JOIN BillingPackages p ON p.Id = s.PackageId
            WHERE s.SmartTvId = @SmartTvId AND s.Status = 'Active'
            """,
            connection);
        command.Parameters.AddWithValue("@SmartTvId", smartTvId);

        return await ReadSessionAsync(command, cancellationToken);
    }

    public async Task<RentalSession?> GetByIdAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                s.Id, s.SmartTvId, s.PackageId, s.CustomerName,
                s.StartedAt, s.EndsAt, s.EndedAt, s.Status, s.Amount, s.StartedByUserId,
                p.Name, p.Price, ISNULL(p.BillingMode, 'Fixed'), s.OpenEndedFrom
            FROM RentalSessions s
            LEFT JOIN BillingPackages p ON p.Id = s.PackageId
            WHERE s.Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", sessionId);

        return await ReadSessionAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<RentalSession>> GetExpiredActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                s.Id, s.SmartTvId, s.PackageId, s.CustomerName,
                s.StartedAt, s.EndsAt, s.EndedAt, s.Status, s.Amount, s.StartedByUserId,
                p.Name, p.Price, ISNULL(p.BillingMode, 'Fixed'), s.OpenEndedFrom
            FROM RentalSessions s
            LEFT JOIN BillingPackages p ON p.Id = s.PackageId
            WHERE s.Status = 'Active'
              AND s.EndsAt IS NOT NULL
              AND s.EndsAt <= SYSUTCDATETIME()
            """,
            connection);

        var items = new List<RentalSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(MapSession(reader));

        return items;
    }

    public async Task<int> CreateAsync(
        int smartTvId,
        int packageId,
        string? customerName,
        DateTime startedAt,
        DateTime? endsAt,
        decimal amount,
        int? startedByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            INSERT INTO RentalSessions
                (SmartTvId, PackageId, CustomerName, StartedAt, EndsAt, Status, Amount, StartedByUserId)
            OUTPUT INSERTED.Id
            VALUES
                (@SmartTvId, @PackageId, @CustomerName, @StartedAt, @EndsAt, 'Active', @Amount, @StartedByUserId)
            """,
            connection);
        command.Parameters.AddWithValue("@SmartTvId", smartTvId);
        command.Parameters.AddWithValue("@PackageId", packageId);
        command.Parameters.AddWithValue("@CustomerName", (object?)customerName ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartedAt", startedAt);
        command.Parameters.AddWithValue("@EndsAt", (object?)endsAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@Amount", amount);
        command.Parameters.AddWithValue("@StartedByUserId", (object?)startedByUserId ?? DBNull.Value);

        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    public async Task ExtendAsync(
        int sessionId,
        DateTime newEndsAt,
        decimal newAmount,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE RentalSessions
            SET EndsAt = @EndsAt,
                Amount = @Amount,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND Status = 'Active' AND EndsAt IS NOT NULL
            """,
            connection);
        command.Parameters.AddWithValue("@Id", sessionId);
        command.Parameters.AddWithValue("@EndsAt", newEndsAt);
        command.Parameters.AddWithValue("@Amount", newAmount);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Sesi aktif tidak ditemukan atau Free Play tidak bisa ditambah waktu.");
    }

    public async Task ConvertToFreePlayAsync(
        int sessionId,
        int freePlayPackageId,
        DateTime openEndedFrom,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE RentalSessions
            SET PackageId = @PackageId,
                EndsAt = NULL,
                OpenEndedFrom = @OpenEndedFrom,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
              AND Status = 'Active'
              AND EndsAt IS NOT NULL
            """,
            connection);
        command.Parameters.AddWithValue("@Id", sessionId);
        command.Parameters.AddWithValue("@PackageId", freePlayPackageId);
        command.Parameters.AddWithValue("@OpenEndedFrom", openEndedFrom);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Sesi paket tetap aktif tidak ditemukan.");
    }

    public async Task CompleteAsync(
        int sessionId,
        DateTime endedAt,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE RentalSessions
            SET EndedAt = @EndedAt,
                EndsAt = ISNULL(EndsAt, @EndedAt),
                Status = 'Completed',
                Amount = @Amount,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND Status = 'Active'
            """,
            connection);
        command.Parameters.AddWithValue("@Id", sessionId);
        command.Parameters.AddWithValue("@EndedAt", endedAt);
        command.Parameters.AddWithValue("@Amount", amount);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Sesi aktif tidak ditemukan.");
    }

    public async Task<IReadOnlyList<RevenueReportItem>> GetCompletedRevenueAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                s.Id,
                t.Name,
                s.CustomerName,
                p.Name,
                ISNULL(p.BillingMode, 'Fixed'),
                s.StartedAt,
                s.EndedAt,
                ISNULL(s.Amount, 0)
            FROM RentalSessions s
            INNER JOIN SmartTvs t ON t.Id = s.SmartTvId
            LEFT JOIN BillingPackages p ON p.Id = s.PackageId
            WHERE s.Status = 'Completed'
              AND s.EndedAt IS NOT NULL
              AND s.EndedAt >= @FromUtc
              AND s.EndedAt < @ToUtc
            ORDER BY s.EndedAt DESC, s.Id DESC
            """,
            connection);
        command.Parameters.AddWithValue("@FromUtc", fromUtcInclusive);
        command.Parameters.AddWithValue("@ToUtc", toUtcExclusive);

        var items = new List<RevenueReportItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new RevenueReportItem
            {
                SessionId = reader.GetInt32(0),
                TvName = reader.GetString(1),
                CustomerName = reader.IsDBNull(2) ? null : reader.GetString(2),
                PackageName = reader.IsDBNull(3) ? null : reader.GetString(3),
                BillingMode = reader.GetString(4),
                StartedAt = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                EndedAt = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
                Amount = reader.GetDecimal(7)
            });
        }

        return items;
    }

    private static async Task<RentalSession?> ReadSessionAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return MapSession(reader);
    }

    private static RentalSession MapSession(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        SmartTvId = reader.GetInt32(1),
        PackageId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
        CustomerName = reader.IsDBNull(3) ? null : reader.GetString(3),
        StartedAt = reader.GetDateTime(4),
        EndsAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        EndedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
        Status = reader.GetString(7),
        Amount = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
        StartedByUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        PackageName = reader.IsDBNull(10) ? null : reader.GetString(10),
        PackagePrice = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
        BillingMode = reader.FieldCount > 12 && !reader.IsDBNull(12)
            ? reader.GetString(12)
            : BillingModes.Fixed,
        OpenEndedFrom = reader.FieldCount > 13 && !reader.IsDBNull(13)
            ? reader.GetDateTime(13)
            : null
    };
}
