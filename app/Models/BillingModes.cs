namespace rental_ps_smart_billing.Models;

public static class BillingModes
{
    public const string Fixed = "Fixed";
    public const string OpenEnded = "OpenEnded";

    public static bool IsOpenEnded(string? mode) =>
        string.Equals(mode, OpenEnded, StringComparison.OrdinalIgnoreCase);
}
