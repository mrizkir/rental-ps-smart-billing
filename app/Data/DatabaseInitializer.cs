using BCrypt.Net;
using Microsoft.Data.SqlClient;

namespace rental_ps_smart_billing.Data;

public sealed class DatabaseInitializer
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DatabaseInitializer(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var databaseName = new SqlConnectionStringBuilder(_connectionFactory.ConnectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Nama database tidak ditemukan di connection string.");

        await SqlConnectionHelper.EnsureDatabaseExistsAsync(
            _connectionFactory.ConnectionString,
            databaseName,
            cancellationToken);

        AppLog.Step("Opening SQL Server connection...");
        await using var connection = await SqlConnectionHelper.OpenAsync(
            _connectionFactory.ConnectionString,
            cancellationToken);

        AppLog.Step("Applying schema...");
        await ExecuteBatchAsync(connection, SchemaSql, cancellationToken);
        AppLog.Step("Schema ready");

        AppLog.Step("Checking seed data...");
        await SeedAsync(connection, cancellationToken);
        await SeedBillingPackagesAsync(connection, cancellationToken);
        AppLog.Step("Seed check completed");
    }

    private static async Task SeedBillingPackagesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var countCommand = new SqlCommand("SELECT COUNT(*) FROM BillingPackages", connection);
        var count = (int)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (count > 0)
        {
            AppLog.Step($"Billing package seed skipped, found {count} package(s)");
            return;
        }

        AppLog.Info("Seeding default billing packages...");
        await using var seedCommand = new SqlCommand(
            """
            INSERT INTO BillingPackages (Name, DurationMinutes, Price) VALUES
            (N'Paket 60 Menit', 60, 15000),
            (N'Paket 120 Menit', 120, 25000);
            """,
            connection);
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);
        AppLog.Info("Billing package seed completed");
    }

    private static async Task SeedAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var countCommand = new SqlCommand("SELECT COUNT(*) FROM Roles", connection);
        var roleCount = (int)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (roleCount > 0)
        {
            AppLog.Step($"Seed skipped, found {roleCount} role(s)");
            return;
        }

        AppLog.Info("Seeding default roles, permissions, and users...");
        var adminHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        var operatorHash = BCrypt.Net.BCrypt.HashPassword("Operator123!");

        await using var seedCommand = new SqlCommand(
            $"""
            INSERT INTO Roles (Name, Description) VALUES
            ('operator', N'Operator kasir'),
            ('superadmin', N'Administrator penuh');

            INSERT INTO Permissions (Code, Description) VALUES
            ('billing.session.start', N'Mulai sesi rental'),
            ('billing.session.end', N'Akhiri sesi rental'),
            ('billing.view', N'Lihat transaksi'),
            ('reports.view', N'Lihat laporan'),
            ('users.manage', N'Kelola user'),
            ('roles.manage', N'Kelola role'),
            ('settings.manage', N'Kelola pengaturan');

            INSERT INTO RolePermissions (RoleId, PermissionId)
            SELECT r.Id, p.Id
            FROM Roles r
            CROSS JOIN Permissions p
            WHERE r.Name = 'operator'
              AND p.Code IN ('billing.session.start', 'billing.session.end', 'billing.view');

            INSERT INTO RolePermissions (RoleId, PermissionId)
            SELECT r.Id, p.Id
            FROM Roles r
            CROSS JOIN Permissions p
            WHERE r.Name = 'superadmin';

            INSERT INTO Users (Username, PasswordHash, DisplayName) VALUES
            ('admin', @AdminHash, N'Administrator'),
            ('operator1', @OperatorHash, N'Operator 1');

            INSERT INTO UserRoles (UserId, RoleId)
            SELECT u.Id, r.Id FROM Users u, Roles r
            WHERE u.Username = 'admin' AND r.Name = 'superadmin';

            INSERT INTO UserRoles (UserId, RoleId)
            SELECT u.Id, r.Id FROM Users u, Roles r
            WHERE u.Username = 'operator1' AND r.Name = 'operator';
            """,
            connection);
        seedCommand.Parameters.AddWithValue("@AdminHash", adminHash);
        seedCommand.Parameters.AddWithValue("@OperatorHash", operatorHash);
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);
        AppLog.Info("Seed completed");
    }

    private static async Task ExecuteBatchAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        var batches = sql.Split(["GO"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;

            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private const string SchemaSql = """
        IF OBJECT_ID('dbo.Roles', 'U') IS NULL
        BEGIN
            CREATE TABLE Roles (
                Id          INT IDENTITY(1,1) PRIMARY KEY,
                Name        NVARCHAR(50)  NOT NULL UNIQUE,
                Description NVARCHAR(200) NULL
            );
        END

        IF OBJECT_ID('dbo.Permissions', 'U') IS NULL
        BEGIN
            CREATE TABLE Permissions (
                Id          INT IDENTITY(1,1) PRIMARY KEY,
                Code        NVARCHAR(100) NOT NULL UNIQUE,
                Description NVARCHAR(200) NULL
            );
        END

        IF OBJECT_ID('dbo.RolePermissions', 'U') IS NULL
        BEGIN
            CREATE TABLE RolePermissions (
                RoleId       INT NOT NULL REFERENCES Roles(Id),
                PermissionId INT NOT NULL REFERENCES Permissions(Id),
                PRIMARY KEY (RoleId, PermissionId)
            );
        END

        IF OBJECT_ID('dbo.Users', 'U') IS NULL
        BEGIN
            CREATE TABLE Users (
                Id           INT IDENTITY(1,1) PRIMARY KEY,
                Username     NVARCHAR(50)  NOT NULL UNIQUE,
                PasswordHash NVARCHAR(200) NOT NULL,
                DisplayName  NVARCHAR(100) NOT NULL,
                IsActive     BIT NOT NULL DEFAULT 1,
                CreatedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
            );
        END

        IF OBJECT_ID('dbo.UserRoles', 'U') IS NULL
        BEGIN
            CREATE TABLE UserRoles (
                UserId INT NOT NULL REFERENCES Users(Id),
                RoleId INT NOT NULL REFERENCES Roles(Id),
                PRIMARY KEY (UserId, RoleId)
            );
        END

        IF OBJECT_ID('dbo.SmartTvs', 'U') IS NULL
        BEGIN
            CREATE TABLE SmartTvs (
                Id              INT IDENTITY(1,1) PRIMARY KEY,
                Name            NVARCHAR(100) NOT NULL,
                Brand           NVARCHAR(50)  NOT NULL DEFAULT 'Samsung',
                IpAddress       NVARCHAR(45)  NOT NULL,
                MacAddress      NVARCHAR(17)  NOT NULL,
                WsPort          INT NOT NULL DEFAULT 8002,
                Token           NVARCHAR(200) NULL,
                LastTestAt      DATETIME2 NULL,
                LastTestStatus  NVARCHAR(20) NULL,
                LastTestMessage NVARCHAR(500) NULL,
                IsActive        BIT NOT NULL DEFAULT 1,
                CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                UpdatedAt       DATETIME2 NULL
            );
        END

        -- Migrasi dari TokenFilePath (path file) ke Token (nilai string)
        IF COL_LENGTH('dbo.SmartTvs', 'TokenFilePath') IS NOT NULL
           AND COL_LENGTH('dbo.SmartTvs', 'Token') IS NULL
        BEGIN
            EXEC sp_rename 'dbo.SmartTvs.TokenFilePath', 'Token', 'COLUMN';
        END

        IF COL_LENGTH('dbo.SmartTvs', 'Token') IS NOT NULL
        BEGIN
            ALTER TABLE SmartTvs ALTER COLUMN Token NVARCHAR(200) NULL;
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'UX_SmartTvs_IpAddress' AND object_id = OBJECT_ID('dbo.SmartTvs'))
        BEGIN
            CREATE UNIQUE INDEX UX_SmartTvs_IpAddress
                ON SmartTvs (IpAddress) WHERE IsActive = 1;
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'UX_SmartTvs_MacAddress' AND object_id = OBJECT_ID('dbo.SmartTvs'))
        BEGIN
            CREATE UNIQUE INDEX UX_SmartTvs_MacAddress
                ON SmartTvs (MacAddress) WHERE IsActive = 1;
        END

        IF OBJECT_ID('dbo.BillingPackages', 'U') IS NULL
        BEGIN
            CREATE TABLE BillingPackages (
                Id              INT IDENTITY(1,1) PRIMARY KEY,
                Name            NVARCHAR(50) NOT NULL,
                DurationMinutes INT NOT NULL,
                Price           DECIMAL(18,2) NOT NULL,
                BillingMode     NVARCHAR(20) NOT NULL DEFAULT 'Fixed',
                IsActive        BIT NOT NULL DEFAULT 1,
                CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
            );
        END

        IF COL_LENGTH('dbo.BillingPackages', 'BillingMode') IS NULL
        BEGIN
            ALTER TABLE BillingPackages
            ADD BillingMode NVARCHAR(20) NOT NULL
                CONSTRAINT DF_BillingPackages_BillingMode DEFAULT 'Fixed';
        END

        IF OBJECT_ID('dbo.RentalSessions', 'U') IS NULL
        BEGIN
            CREATE TABLE RentalSessions (
                Id              INT IDENTITY(1,1) PRIMARY KEY,
                SmartTvId       INT NOT NULL REFERENCES SmartTvs(Id),
                PackageId       INT NULL REFERENCES BillingPackages(Id),
                CustomerName    NVARCHAR(100) NULL,
                StartedAt       DATETIME2 NOT NULL,
                EndsAt          DATETIME2 NULL,
                EndedAt         DATETIME2 NULL,
                Status          NVARCHAR(20) NOT NULL,
                Amount          DECIMAL(18,2) NULL,
                StartedByUserId INT NULL REFERENCES Users(Id),
                CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                UpdatedAt       DATETIME2 NULL
            );
        END

        -- Free Play (open-ended) menyimpan EndsAt = NULL
        IF EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.RentalSessions')
              AND name = 'EndsAt'
              AND is_nullable = 0)
        BEGIN
            ALTER TABLE RentalSessions ALTER COLUMN EndsAt DATETIME2 NULL;
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'UX_RentalSessions_ActiveTv' AND object_id = OBJECT_ID('dbo.RentalSessions'))
        BEGIN
            CREATE UNIQUE INDEX UX_RentalSessions_ActiveTv
                ON RentalSessions (SmartTvId) WHERE Status = 'Active';
        END
        """;
}
