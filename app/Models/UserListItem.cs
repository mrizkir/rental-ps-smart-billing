namespace rental_ps_smart_billing.Models;

public sealed class UserListItem
{
    public int Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Roles { get; init; }
    public bool IsActive { get; init; }
    public string Status => IsActive ? "Aktif" : "Nonaktif";
}
