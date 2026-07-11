namespace rental_ps_smart_billing.Models;

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public AuthenticatedUser? User { get; init; }

    public static AuthResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static AuthResult Succeeded(AuthenticatedUser user) => new()
    {
        Success = true,
        User = user
    };
}
