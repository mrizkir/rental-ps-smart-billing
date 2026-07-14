using Microsoft.Data.SqlClient;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Data;

public interface ISmartTvRepository
{
    Task<IReadOnlyList<SmartTvListItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SmartTvEditDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> IpExistsAsync(string ipAddress, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> MacExistsAsync(string macAddress, int? excludeId = null, CancellationToken cancellationToken = default);
    Task CreateAsync(
        string name,
        string brand,
        int? modelId,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(
        int id,
        string name,
        string brand,
        int? modelId,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateTestResultAsync(
        int id,
        string status,
        string message,
        CancellationToken cancellationToken = default);
    Task UpdateTokenAsync(int id, string token, CancellationToken cancellationToken = default);
}

public sealed class SmartTvRepository : ISmartTvRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SmartTvRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SmartTvListItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                t.Id,
                t.Name,
                t.Brand,
                m.Code,
                t.IpAddress,
                t.MacAddress,
                t.WsPort,
                t.IsActive,
                t.LastTestStatus,
                t.LastTestAt
            FROM SmartTvs t
            LEFT JOIN TvModels m ON m.Id = t.ModelId
            ORDER BY t.Name
            """,
            connection);

        var items = new List<SmartTvListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SmartTvListItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Brand = reader.GetString(2),
                ModelCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                IpAddress = reader.GetString(4),
                MacAddress = reader.GetString(5),
                WsPort = reader.GetInt32(6),
                IsActive = reader.GetBoolean(7),
                LastTestStatus = reader.IsDBNull(8) ? null : reader.GetString(8),
                LastTestAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return items;
    }

    public async Task<SmartTvEditDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                t.Id,
                t.Name,
                t.Brand,
                t.ModelId,
                m.Code,
                t.IpAddress,
                t.MacAddress,
                t.WsPort,
                t.Token,
                t.IsActive
            FROM SmartTvs t
            LEFT JOIN TvModels m ON m.Id = t.ModelId
            WHERE t.Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var tokenOrdinal = reader.GetOrdinal("Token");
        var tokenValue = reader.IsDBNull(tokenOrdinal)
            ? null
            : reader.GetString(tokenOrdinal).Trim();

        return new SmartTvEditDetails
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Brand = reader.GetString(reader.GetOrdinal("Brand")),
            ModelId = reader.IsDBNull(reader.GetOrdinal("ModelId"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("ModelId")),
            ModelCode = reader.IsDBNull(reader.GetOrdinal("Code"))
                ? null
                : reader.GetString(reader.GetOrdinal("Code")),
            IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
            MacAddress = reader.GetString(reader.GetOrdinal("MacAddress")),
            WsPort = reader.GetInt32(reader.GetOrdinal("WsPort")),
            Token = string.IsNullOrWhiteSpace(tokenValue) ? null : tokenValue,
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }

    public async Task<bool> IpExistsAsync(
        string ipAddress,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM SmartTvs
            WHERE IpAddress = @IpAddress
              AND IsActive = 1
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """,
            connection);
        command.Parameters.AddWithValue("@IpAddress", ipAddress);
        command.Parameters.AddWithValue("@ExcludeId", (object?)excludeId ?? DBNull.Value);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    public async Task<bool> MacExistsAsync(
        string macAddress,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM SmartTvs
            WHERE MacAddress = @MacAddress
              AND IsActive = 1
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """,
            connection);
        command.Parameters.AddWithValue("@MacAddress", macAddress);
        command.Parameters.AddWithValue("@ExcludeId", (object?)excludeId ?? DBNull.Value);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    public async Task CreateAsync(
        string name,
        string brand,
        int? modelId,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            INSERT INTO SmartTvs (Name, Brand, ModelId, IpAddress, MacAddress, WsPort, Token)
            VALUES (@Name, @Brand, @ModelId, @IpAddress, @MacAddress, @WsPort, @Token)
            """,
            connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Brand", brand);
        command.Parameters.AddWithValue("@ModelId", (object?)modelId ?? DBNull.Value);
        command.Parameters.AddWithValue("@IpAddress", ipAddress);
        command.Parameters.AddWithValue("@MacAddress", macAddress);
        command.Parameters.AddWithValue("@WsPort", wsPort);
        command.Parameters.AddWithValue("@Token", (object?)token ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string name,
        string brand,
        int? modelId,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE SmartTvs
            SET Name = @Name,
                Brand = @Brand,
                ModelId = @ModelId,
                IpAddress = @IpAddress,
                MacAddress = @MacAddress,
                WsPort = @WsPort,
                Token = @Token,
                IsActive = @IsActive,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Brand", brand);
        command.Parameters.AddWithValue("@ModelId", (object?)modelId ?? DBNull.Value);
        command.Parameters.AddWithValue("@IpAddress", ipAddress);
        command.Parameters.AddWithValue("@MacAddress", macAddress);
        command.Parameters.AddWithValue("@WsPort", wsPort);
        command.Parameters.AddWithValue("@Token", (object?)token ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", isActive);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Smart TV tidak ditemukan.");
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE SmartTvs
            SET IsActive = 0,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Smart TV tidak ditemukan.");
    }

    public async Task UpdateTestResultAsync(
        int id,
        string status,
        string message,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE SmartTvs
            SET LastTestAt = SYSUTCDATETIME(),
                LastTestStatus = @Status,
                LastTestMessage = @Message,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@Message", message);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Smart TV tidak ditemukan.");
    }

    public async Task UpdateTokenAsync(int id, string token, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE SmartTvs
            SET Token = @Token,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Token", token);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Smart TV tidak ditemukan.");
    }
}
