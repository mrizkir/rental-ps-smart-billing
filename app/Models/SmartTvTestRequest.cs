namespace rental_ps_smart_billing.Models;

public sealed class SmartTvTestRequest
{
    public required string IpAddress { get; init; }
    public required string MacAddress { get; init; }
    public int WsPort { get; init; }
    public string? Token { get; init; }
}
