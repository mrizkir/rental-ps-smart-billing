namespace rental_ps_smart_billing.Models;

public sealed class SmartTvEditDetails
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string IpAddress { get; init; }
    public required string MacAddress { get; init; }
    public int WsPort { get; init; }
    public string? Token { get; init; }
    public bool IsActive { get; init; }
}
