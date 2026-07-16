namespace rental_ps_smart_billing.Models;

public sealed class TvModelListItem
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public bool IsActive { get; init; }

    public string Status => IsActive ? "Aktif" : "Nonaktif";
    public string DisplayLabel => $"{Code} — {Name}";
}

public sealed class TvModel
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public bool IsActive { get; init; }
}

public sealed class TvModelResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static TvModelResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static TvModelResult Succeeded() => new() { Success = true };
}
