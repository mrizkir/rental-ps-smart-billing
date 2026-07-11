namespace rental_ps_smart_billing.Models;

public sealed class CreateUserResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static CreateUserResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static CreateUserResult Succeeded() => new() { Success = true };
}
