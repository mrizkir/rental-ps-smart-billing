using Microsoft.Data.SqlClient;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Data;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserListItem>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserEditDetails?> GetUserForEditAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task CreateUserAsync(
        string username,
        string passwordHash,
        string displayName,
        string roleName,
        CancellationToken cancellationToken = default);
    Task UpdateUserAsync(
        int userId,
        string displayName,
        string roleName,
        string? passwordHash,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task DeactivateUserAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UserRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Username, PasswordHash, DisplayName, IsActive
            FROM Users
            WHERE Username = @Username AND IsActive = 1
            """,
            connection);
        command.Parameters.AddWithValue("@Username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            DisplayName = reader.GetString(3),
            IsActive = reader.GetBoolean(4)
        };
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT r.Name
            FROM UserRoles ur
            INNER JOIN Roles r ON r.Id = ur.RoleId
            WHERE ur.UserId = @UserId
            ORDER BY r.Name
            """,
            connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            roles.Add(reader.GetString(0));

        return roles;
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT DISTINCT p.Code
            FROM UserRoles ur
            INNER JOIN RolePermissions rp ON rp.RoleId = ur.RoleId
            INNER JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE ur.UserId = @UserId
            ORDER BY p.Code
            """,
            connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var permissions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            permissions.Add(reader.GetString(0));

        return permissions;
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT Id, Name, Description
            FROM Roles
            ORDER BY Name
            """,
            connection);

        var roles = new List<Role>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new Role
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return roles;
    }

    public async Task<IReadOnlyList<UserListItem>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                u.Id,
                u.Username,
                u.DisplayName,
                u.IsActive,
                ISNULL(STRING_AGG(r.Name, ', ') WITHIN GROUP (ORDER BY r.Name), '') AS Roles
            FROM Users u
            LEFT JOIN UserRoles ur ON ur.UserId = u.Id
            LEFT JOIN Roles r ON r.Id = ur.RoleId
            GROUP BY u.Id, u.Username, u.DisplayName, u.IsActive
            ORDER BY u.Username
            """,
            connection);

        var users = new List<UserListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new UserListItem
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(2),
                IsActive = reader.GetBoolean(3),
                Roles = reader.GetString(4)
            });
        }

        return users;
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM Users WHERE Username = @Username",
            connection);
        command.Parameters.AddWithValue("@Username", username);

        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    public async Task CreateUserAsync(
        string username,
        string passwordHash,
        string displayName,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var insertUser = new SqlCommand(
                """
                INSERT INTO Users (Username, PasswordHash, DisplayName)
                OUTPUT INSERTED.Id
                VALUES (@Username, @PasswordHash, @DisplayName)
                """,
                connection,
                transaction);
            insertUser.Parameters.AddWithValue("@Username", username);
            insertUser.Parameters.AddWithValue("@PasswordHash", passwordHash);
            insertUser.Parameters.AddWithValue("@DisplayName", displayName);

            var userId = (int)(await insertUser.ExecuteScalarAsync(cancellationToken) ?? 0);

            await using var insertRole = new SqlCommand(
                """
                INSERT INTO UserRoles (UserId, RoleId)
                SELECT @UserId, r.Id
                FROM Roles r
                WHERE r.Name = @RoleName
                """,
                connection,
                transaction);
            insertRole.Parameters.AddWithValue("@UserId", userId);
            insertRole.Parameters.AddWithValue("@RoleName", roleName);

            var rows = await insertRole.ExecuteNonQueryAsync(cancellationToken);
            if (rows == 0)
                throw new InvalidOperationException($"Role '{roleName}' tidak ditemukan.");

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<UserEditDetails?> GetUserForEditAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            """
            SELECT
                u.Id,
                u.Username,
                u.DisplayName,
                u.IsActive,
                ISNULL(MIN(r.Name), '') AS RoleName
            FROM Users u
            LEFT JOIN UserRoles ur ON ur.UserId = u.Id
            LEFT JOIN Roles r ON r.Id = ur.RoleId
            WHERE u.Id = @UserId
            GROUP BY u.Id, u.Username, u.DisplayName, u.IsActive
            """,
            connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new UserEditDetails
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2),
            IsActive = reader.GetBoolean(3),
            RoleName = reader.GetString(4)
        };
    }

    public async Task UpdateUserAsync(
        int userId,
        string displayName,
        string roleName,
        string? passwordHash,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var updateSql = passwordHash is null
                ? """
                  UPDATE Users
                  SET DisplayName = @DisplayName, IsActive = @IsActive
                  WHERE Id = @UserId
                  """
                : """
                  UPDATE Users
                  SET DisplayName = @DisplayName, IsActive = @IsActive, PasswordHash = @PasswordHash
                  WHERE Id = @UserId
                  """;

            await using var updateUser = new SqlCommand(updateSql, connection, transaction);
            updateUser.Parameters.AddWithValue("@UserId", userId);
            updateUser.Parameters.AddWithValue("@DisplayName", displayName);
            updateUser.Parameters.AddWithValue("@IsActive", isActive);
            if (passwordHash is not null)
                updateUser.Parameters.AddWithValue("@PasswordHash", passwordHash);

            var rows = await updateUser.ExecuteNonQueryAsync(cancellationToken);
            if (rows == 0)
                throw new InvalidOperationException("User tidak ditemukan.");

            await using var deleteRoles = new SqlCommand(
                "DELETE FROM UserRoles WHERE UserId = @UserId",
                connection,
                transaction);
            deleteRoles.Parameters.AddWithValue("@UserId", userId);
            await deleteRoles.ExecuteNonQueryAsync(cancellationToken);

            await using var insertRole = new SqlCommand(
                """
                INSERT INTO UserRoles (UserId, RoleId)
                SELECT @UserId, r.Id
                FROM Roles r
                WHERE r.Name = @RoleName
                """,
                connection,
                transaction);
            insertRole.Parameters.AddWithValue("@UserId", userId);
            insertRole.Parameters.AddWithValue("@RoleName", roleName);

            var roleRows = await insertRole.ExecuteNonQueryAsync(cancellationToken);
            if (roleRows == 0)
                throw new InvalidOperationException($"Role '{roleName}' tidak ditemukan.");

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeactivateUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "UPDATE Users SET IsActive = 0 WHERE Id = @UserId",
            connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException("User tidak ditemukan.");
    }
}
