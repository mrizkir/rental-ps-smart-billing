namespace rental_ps_smart_billing.Models;

public sealed class RentalSession
{
    public int Id { get; init; }
    public int SmartTvId { get; init; }
    public int? PackageId { get; init; }
    public string? CustomerName { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndsAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public required string Status { get; init; }
    public decimal? Amount { get; init; }
    public int? StartedByUserId { get; init; }
    public string? PackageName { get; init; }
    public decimal? PackagePrice { get; init; }
}
