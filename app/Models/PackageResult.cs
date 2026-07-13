namespace rental_ps_smart_billing.Models;

public sealed class PackageResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static PackageResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static PackageResult Succeeded() => new() { Success = true };
}
