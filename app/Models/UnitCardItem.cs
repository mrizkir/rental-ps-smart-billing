namespace rental_ps_smart_billing.Models;

public sealed class UnitCardItem
{
    public int SmartTvId { get; init; }
    public required string TvName { get; init; }
    public required string IpAddress { get; init; }
    public required string MacAddress { get; init; }
    public int WsPort { get; init; }
    public string? Token { get; init; }

    public int? SessionId { get; init; }
    public string? CustomerName { get; init; }
    public string? PackageName { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public decimal Amount { get; init; }

    public bool IsPlaying => SessionId is not null;

    public string StatusLabel => IsPlaying ? "PLAYING" : "AVAILABLE";
}
