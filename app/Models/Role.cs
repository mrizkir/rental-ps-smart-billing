namespace rental_ps_smart_billing.Models;

public sealed class Role
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
}
