using System.Diagnostics;

namespace rental_ps_smart_billing;

public static class AppLog
{
    private static bool _verbose;

    public static void Configure(string[] args)
    {
#if DEBUG
        _verbose = true;
#endif
        if (args.Contains("--verbose", StringComparer.OrdinalIgnoreCase))
            _verbose = true;
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex)
    {
        Write("ERROR", message);
        Write("ERROR", $"{ex.GetType().Name}: {ex.Message}");
        if (_verbose && ex.StackTrace is not null)
            Write("ERROR", ex.StackTrace);
    }

    public static void Step(string message)
    {
        if (_verbose)
            Write("STEP", message);
    }

    private static void Write(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [{level}] {message}");
        Debug.WriteLine($"[{level}] {message}");
    }

    public static string MaskConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                parts[i] = "Password=***";
        }

        return string.Join(';', parts);
    }
}
