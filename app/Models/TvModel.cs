namespace rental_ps_smart_billing.Models;

public sealed class SleepTimerProfile
{
    public string Mode { get; init; } = "menu";
    public int Minutes { get; init; } = 30;
    public double KeyDelaySeconds { get; init; } = 1.0;
    public IReadOnlyList<string> ConfirmKeys { get; init; } = ["KEY_DOWN", "KEY_ENTER"];
    public string? ModelCode { get; init; }

    public static SleepTimerProfile FromDefaults(int minutes) => new()
    {
        Mode = "menu",
        Minutes = minutes < 1 ? 30 : minutes,
        KeyDelaySeconds = 1.0,
        ConfirmKeys = ["KEY_DOWN", "KEY_ENTER"],
        ModelCode = null
    };

    public static IReadOnlyList<string> ParseConfirmKeys(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ["KEY_DOWN", "KEY_ENTER"];

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length > 0)
            .ToArray();
    }
}

public sealed class TvModelListItem
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string SleepTimerMode { get; init; }
    public int SleepTimerMinutes { get; init; }
    public string SleepTimerConfirmKeys { get; init; } = "KEY_DOWN,KEY_ENTER";
    public bool IsActive { get; init; }

    public string Status => IsActive ? "Aktif" : "Nonaktif";
    public string SleepDisplay => $"{SleepTimerMode} / {SleepTimerMinutes}m";
    public string DisplayLabel => $"{Code} — {Name}";
}

public sealed class TvModel
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string SleepTimerMode { get; init; }
    public int SleepTimerMinutes { get; init; }
    public double SleepTimerKeyDelaySeconds { get; init; }
    public required string SleepTimerConfirmKeys { get; init; }
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
