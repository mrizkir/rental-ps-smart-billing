using Microsoft.Data.SqlClient;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Data;

public interface ITvModelRepository
{
    Task<IReadOnlyList<TvModelListItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TvModelListItem>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<TvModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SleepTimerProfile?> GetSleepProfileBySmartTvIdAsync(
        int smartTvId,
        CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsUsedBySmartTvAsync(int modelId, CancellationToken cancellationToken = default);
    Task CreateAsync(
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(
        int id,
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class TvModelRepository : ITvModelRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TvModelRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<TvModelListItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Code, Name, Brand, SleepTimerMode, SleepTimerMinutes, SleepTimerConfirmKeys, IsActive
            FROM TvModels
            ORDER BY Code
            """,
            connection);

        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<TvModelListItem>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Code, Name, Brand, SleepTimerMode, SleepTimerMinutes, SleepTimerConfirmKeys, IsActive
            FROM TvModels
            WHERE IsActive = 1
            ORDER BY Code
            """,
            connection);

        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<TvModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                Id, Code, Name, Brand, SleepTimerMode, SleepTimerMinutes,
                SleepTimerKeyDelaySeconds, SleepTimerConfirmKeys, IsActive
            FROM TvModels
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new TvModel
        {
            Id = reader.GetInt32(0),
            Code = reader.GetString(1),
            Name = reader.GetString(2),
            Brand = reader.GetString(3),
            SleepTimerMode = reader.GetString(4),
            SleepTimerMinutes = reader.GetInt32(5),
            SleepTimerKeyDelaySeconds = (double)reader.GetDecimal(6),
            SleepTimerConfirmKeys = reader.GetString(7),
            IsActive = reader.GetBoolean(8)
        };
    }

    public async Task<SleepTimerProfile?> GetSleepProfileBySmartTvIdAsync(
        int smartTvId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                m.Code,
                m.SleepTimerMode,
                m.SleepTimerMinutes,
                m.SleepTimerKeyDelaySeconds,
                m.SleepTimerConfirmKeys
            FROM SmartTvs t
            INNER JOIN TvModels m ON m.Id = t.ModelId AND m.IsActive = 1
            WHERE t.Id = @SmartTvId
            """,
            connection);
        command.Parameters.AddWithValue("@SmartTvId", smartTvId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new SleepTimerProfile
        {
            ModelCode = reader.GetString(0),
            Mode = reader.GetString(1),
            Minutes = reader.GetInt32(2),
            KeyDelaySeconds = (double)reader.GetDecimal(3),
            ConfirmKeys = SleepTimerProfile.ParseConfirmKeys(reader.GetString(4))
        };
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM TvModels
            WHERE Code = @Code
              AND IsActive = 1
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """,
            connection);
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@ExcludeId", (object?)excludeId ?? DBNull.Value);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    public async Task<bool> IsUsedBySmartTvAsync(int modelId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM SmartTvs
            WHERE ModelId = @ModelId AND IsActive = 1
            """,
            connection);
        command.Parameters.AddWithValue("@ModelId", modelId);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    public async Task CreateAsync(
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            INSERT INTO TvModels
                (Code, Name, Brand, SleepTimerMode, SleepTimerMinutes, SleepTimerKeyDelaySeconds, SleepTimerConfirmKeys)
            VALUES
                (@Code, @Name, @Brand, @SleepTimerMode, @SleepTimerMinutes, @SleepTimerKeyDelaySeconds, @SleepTimerConfirmKeys)
            """,
            connection);
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Brand", brand);
        command.Parameters.AddWithValue("@SleepTimerMode", sleepTimerMode);
        command.Parameters.AddWithValue("@SleepTimerMinutes", sleepTimerMinutes);
        command.Parameters.AddWithValue("@SleepTimerKeyDelaySeconds", (decimal)sleepTimerKeyDelaySeconds);
        command.Parameters.AddWithValue("@SleepTimerConfirmKeys", sleepTimerConfirmKeys);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE TvModels
            SET Code = @Code,
                Name = @Name,
                Brand = @Brand,
                SleepTimerMode = @SleepTimerMode,
                SleepTimerMinutes = @SleepTimerMinutes,
                SleepTimerKeyDelaySeconds = @SleepTimerKeyDelaySeconds,
                SleepTimerConfirmKeys = @SleepTimerConfirmKeys,
                IsActive = @IsActive,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Brand", brand);
        command.Parameters.AddWithValue("@SleepTimerMode", sleepTimerMode);
        command.Parameters.AddWithValue("@SleepTimerMinutes", sleepTimerMinutes);
        command.Parameters.AddWithValue("@SleepTimerKeyDelaySeconds", (decimal)sleepTimerKeyDelaySeconds);
        command.Parameters.AddWithValue("@SleepTimerConfirmKeys", sleepTimerConfirmKeys);
        command.Parameters.AddWithValue("@IsActive", isActive);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Model TV tidak ditemukan.");
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            UPDATE TvModels
            SET IsActive = 0,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            connection);
        command.Parameters.AddWithValue("@Id", id);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("Model TV tidak ditemukan.");
    }

    private static async Task<IReadOnlyList<TvModelListItem>> ReadListAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<TvModelListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TvModelListItem
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                Name = reader.GetString(2),
                Brand = reader.GetString(3),
                SleepTimerMode = reader.GetString(4),
                SleepTimerMinutes = reader.GetInt32(5),
                SleepTimerConfirmKeys = reader.GetString(6),
                IsActive = reader.GetBoolean(7)
            });
        }

        return items;
    }
}
