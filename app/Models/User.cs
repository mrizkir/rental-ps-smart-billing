namespace rental_ps_smart_billing.Models;

public sealed class User
{
    public int Id { get; init; }
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required string DisplayName { get; init; }
    public bool IsActive { get; init; }
}
