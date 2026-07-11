namespace rental_ps_smart_billing.Models;

public sealed class TvConnectionTestResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public DateTime TestedAt { get; init; }
    public string? Token { get; init; }

    public static TvConnectionTestResult Succeeded(string message, string? token = null) => new()
    {
        Success = true,
        Message = message,
        TestedAt = DateTime.UtcNow,
        Token = token
    };

    public static TvConnectionTestResult Failed(string message) => new()
    {
        Success = false,
        Message = message,
        TestedAt = DateTime.UtcNow
    };
}
