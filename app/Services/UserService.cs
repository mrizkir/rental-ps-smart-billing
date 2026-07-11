using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface IUserService
{
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserEditDetails?> GetUserForEditAsync(int userId, CancellationToken cancellationToken = default);
    Task<CreateUserResult> CreateUserAsync(
        string username,
        string password,
        string displayName,
        string roleName,
        CancellationToken cancellationToken = default);
    Task<CreateUserResult> UpdateUserAsync(
        int userId,
        string displayName,
        string roleName,
        string? password,
        bool isActive,
        int currentUserId,
        CancellationToken cancellationToken = default);
    Task<CreateUserResult> DeleteUserAsync(
        int userId,
        int currentUserId,
        CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        _userRepository.GetRolesAsync(cancellationToken);

    public Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        _userRepository.GetAllUsersAsync(cancellationToken);

    public Task<UserEditDetails?> GetUserForEditAsync(int userId, CancellationToken cancellationToken = default) =>
        _userRepository.GetUserForEditAsync(userId, cancellationToken);

    public async Task<CreateUserResult> CreateUserAsync(
        string username,
        string password,
        string displayName,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        username = username.Trim();
        displayName = displayName.Trim();
        roleName = roleName.Trim();

        if (string.IsNullOrWhiteSpace(username))
            return CreateUserResult.Failed("Username wajib diisi.");

        if (string.IsNullOrWhiteSpace(password))
            return CreateUserResult.Failed("Password wajib diisi.");

        if (password.Length < 6)
            return CreateUserResult.Failed("Password minimal 6 karakter.");

        if (string.IsNullOrWhiteSpace(displayName))
            return CreateUserResult.Failed("Nama tampilan wajib diisi.");

        if (string.IsNullOrWhiteSpace(roleName))
            return CreateUserResult.Failed("Role wajib dipilih.");

        if (await _userRepository.UsernameExistsAsync(username, cancellationToken))
            return CreateUserResult.Failed("Username sudah digunakan.");

        var roles = await _userRepository.GetRolesAsync(cancellationToken);
        if (roles.All(r => !r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            return CreateUserResult.Failed("Role tidak valid.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        await _userRepository.CreateUserAsync(username, passwordHash, displayName, roleName, cancellationToken);

        AppLog.Info($"User created: {username} ({roleName})");
        return CreateUserResult.Succeeded();
    }

    public async Task<CreateUserResult> UpdateUserAsync(
        int userId,
        string displayName,
        string roleName,
        string? password,
        bool isActive,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        roleName = roleName.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
            return CreateUserResult.Failed("Nama tampilan wajib diisi.");

        if (string.IsNullOrWhiteSpace(roleName))
            return CreateUserResult.Failed("Role wajib dipilih.");

        if (!string.IsNullOrWhiteSpace(password) && password.Length < 6)
            return CreateUserResult.Failed("Password minimal 6 karakter.");

        if (userId == currentUserId && !isActive)
            return CreateUserResult.Failed("Tidak dapat menonaktifkan akun yang sedang login.");

        var user = await _userRepository.GetUserForEditAsync(userId, cancellationToken);
        if (user is null)
            return CreateUserResult.Failed("User tidak ditemukan.");

        var roles = await _userRepository.GetRolesAsync(cancellationToken);
        if (roles.All(r => !r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            return CreateUserResult.Failed("Role tidak valid.");

        string? passwordHash = string.IsNullOrWhiteSpace(password)
            ? null
            : BCrypt.Net.BCrypt.HashPassword(password);

        await _userRepository.UpdateUserAsync(userId, displayName, roleName, passwordHash, isActive, cancellationToken);

        AppLog.Info($"User updated: {user.Username} ({roleName})");
        return CreateUserResult.Succeeded();
    }

    public async Task<CreateUserResult> DeleteUserAsync(
        int userId,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == currentUserId)
            return CreateUserResult.Failed("Tidak dapat menghapus akun yang sedang login.");

        var user = await _userRepository.GetUserForEditAsync(userId, cancellationToken);
        if (user is null)
            return CreateUserResult.Failed("User tidak ditemukan.");

        if (!user.IsActive)
            return CreateUserResult.Failed("User sudah nonaktif.");

        await _userRepository.DeactivateUserAsync(userId, cancellationToken);

        AppLog.Info($"User deactivated: {user.Username}");
        return CreateUserResult.Succeeded();
    }
}
