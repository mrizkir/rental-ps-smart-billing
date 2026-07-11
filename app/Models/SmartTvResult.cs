namespace rental_ps_smart_billing.Models;

public sealed class SmartTvResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static SmartTvResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static SmartTvResult Succeeded() => new() { Success = true };
}
