namespace rental_ps_smart_billing.Models;

public sealed class SmartTv
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string IpAddress { get; init; }
    public required string MacAddress { get; init; }
    public int WsPort { get; init; }
    public string? Token { get; init; }
    public DateTime? LastTestAt { get; init; }
    public string? LastTestStatus { get; init; }
    public string? LastTestMessage { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
