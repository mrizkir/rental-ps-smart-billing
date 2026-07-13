namespace rental_ps_smart_billing.Models;

public sealed class BillingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }
    public decimal? Amount { get; init; }

    public static BillingResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static BillingResult Succeeded(string? warning = null, decimal? amount = null) => new()
    {
        Success = true,
        WarningMessage = warning,
        Amount = amount
    };
}
