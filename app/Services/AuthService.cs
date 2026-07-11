using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        AppLog.Step($"Login attempt for user: {username}");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Failed("Username dan password wajib diisi.");

        var user = await _userRepository.GetByUsernameAsync(username.Trim(), cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            AppLog.Warn($"Login failed for user: {username}");
            return AuthResult.Failed("Username atau password salah.");
        }

        var roles = await _userRepository.GetRoleNamesAsync(user.Id, cancellationToken);
        var permissions = await _userRepository.GetPermissionCodesAsync(user.Id, cancellationToken);

        AppLog.Info($"Login succeeded for user: {user.Username} (roles: {string.Join(", ", roles)})");

        return AuthResult.Succeeded(new AuthenticatedUser
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Roles = roles,
            Permissions = permissions
        });
    }
}
