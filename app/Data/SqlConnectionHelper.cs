using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace rental_ps_smart_billing.Data;

public static class SqlConnectionHelper
{
    private const int DefaultTimeoutSeconds = 30;

    public static string Normalize(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (builder.ConnectTimeout <= 0)
            builder.ConnectTimeout = DefaultTimeoutSeconds;

        // SQL Server 2022 (Docker/Linux) umumnya butuh TLS; samakan dengan driver .NET modern.
        builder.Encrypt = true;
        builder.TrustServerCertificate = true;

        return builder.ConnectionString;
    }

    public static string ToMasterConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(Normalize(connectionString))
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    public static async Task<SqlConnection> OpenAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(connectionString);
        var builder = new SqlConnectionStringBuilder(normalized);
        var timeoutSeconds = builder.ConnectTimeout;
        var connection = new SqlConnection(normalized);
        var stopwatch = Stopwatch.StartNew();

        AppLog.Step($"Connecting to {builder.DataSource}, db={builder.InitialCatalog} (timeout {timeoutSeconds}s)...");
        AppLog.Step($"User={builder.UserID}, password length={builder.Password.Length}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 5));

        try
        {
            await connection.OpenAsync(timeoutCts.Token);
            AppLog.Step($"Connected in {stopwatch.ElapsedMilliseconds}ms (db={connection.Database})");
            return connection;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await connection.DisposeAsync();
            throw new TimeoutException(
                $"Koneksi SQL Server timeout setelah {timeoutSeconds}s. Periksa SQL Server, port 1433, dan kredensial.");
        }
        catch (SqlException ex)
        {
            await connection.DisposeAsync();
            AppLog.Error($"SQL error after {stopwatch.ElapsedMilliseconds}ms", ex);
            throw;
        }
        catch (Exception ex)
        {
            await connection.DisposeAsync();
            AppLog.Error($"Connection failed after {stopwatch.ElapsedMilliseconds}ms", ex);
            throw;
        }
    }

    public static async Task EnsureDatabaseExistsAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        var masterConnectionString = ToMasterConnectionString(connectionString);
        await using var connection = await OpenAsync(masterConnectionString, cancellationToken);

        AppLog.Step($"Ensuring database '{databaseName}' exists...");
        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, @"^[A-Za-z0-9_]+$"))
            throw new ArgumentException($"Nama database tidak valid: {databaseName}", nameof(databaseName));

        await using var command = new SqlCommand(
            $"""
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @DatabaseName)
                CREATE DATABASE [{databaseName}];
            """,
            connection);
        command.Parameters.AddWithValue("@DatabaseName", databaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
        AppLog.Step($"Database '{databaseName}' ready");
    }

    public static async Task TestConnectionAsync(string connectionString)
    {
        AppLog.Info("=== SQL Connection Test ===");

        var variants = new[]
        {
            ("config", connectionString),
            ("localhost+master", Override(connectionString, "localhost,1433", "master")),
            ("127.0.0.1+master", Override(connectionString, "127.0.0.1,1433", "master")),
        };

        foreach (var (name, variant) in variants)
        {
            AppLog.Info($"Trying variant: {name}");
            try
            {
                await using var connection = await OpenAsync(variant);
                await using var command = new SqlCommand("SELECT @@VERSION", connection);
                var version = (string?)await command.ExecuteScalarAsync();
                AppLog.Info($"SUCCESS [{name}]: {version?[..Math.Min(80, version.Length)]}...");
                return;
            }
            catch (Exception ex)
            {
                AppLog.Error($"FAILED [{name}]", ex);
            }
        }

        throw new InvalidOperationException("Semua variasi koneksi gagal. Pastikan password sama persis dengan DBeaver.");
    }

    private static string Override(string connectionString, string server, string database)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            DataSource = server,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = DefaultTimeoutSeconds
        };
        return builder.ConnectionString;
    }
}
