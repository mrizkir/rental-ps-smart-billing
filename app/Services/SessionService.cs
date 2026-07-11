using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface ISessionService
{
    AuthenticatedUser? CurrentUser { get; }
    bool IsAuthenticated { get; }
    void SetCurrentUser(AuthenticatedUser user);
    void Clear();
    bool HasPermission(string permissionCode);
    bool IsInRole(string roleName);
}

public sealed class SessionService : ISessionService
{
    private HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);

    public AuthenticatedUser? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public void SetCurrentUser(AuthenticatedUser user)
    {
        CurrentUser = user;
        _roles = user.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _permissions = user.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void Clear()
    {
        CurrentUser = null;
        _roles.Clear();
        _permissions.Clear();
    }

    public bool HasPermission(string permissionCode) =>
        _permissions.Contains(permissionCode);

    public bool IsInRole(string roleName) =>
        _roles.Contains(roleName);
}
