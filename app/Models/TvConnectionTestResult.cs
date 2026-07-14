namespace rental_ps_smart_billing.Models;

public sealed class TvConnectionTestResult
{
    public bool Success { get; init; }
        public required string Message { get; init; }
    public DateTime TestedAt { get; init; }
    public string? Token { get; init; }

    /// <summary>Ringkasan info perangkat dari rest_device_info (jika ada).</summary>
    public string? DeviceSummary { get; init; }

    public static TvConnectionTestResult Succeeded(
        string message,
        string? token = null,
        string? deviceSummary = null) => new()
    {
        Success = true,
        Message = message,
        TestedAt = DateTime.UtcNow,
        Token = token,
        DeviceSummary = deviceSummary
    };

    public static TvConnectionTestResult Failed(string message) => new()
    {
        Success = false,
        Message = message,
        TestedAt = DateTime.UtcNow
    };
}
