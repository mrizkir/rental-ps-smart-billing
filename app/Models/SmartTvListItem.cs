namespace rental_ps_smart_billing.Models;

public sealed class SmartTvListItem
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string IpAddress { get; init; }
    public required string MacAddress { get; init; }
    public int WsPort { get; init; }
    public bool IsActive { get; init; }
    public string? LastTestStatus { get; init; }
    public DateTime? LastTestAt { get; init; }

    public string Status => IsActive ? "Aktif" : "Nonaktif";

    public string LastTestDisplay => LastTestStatus switch
    {
        "success" => "Sukses",
        "failed" => "Gagal",
        "pending" => "Pending",
        null or "" => "-",
        _ => LastTestStatus
    };
}
