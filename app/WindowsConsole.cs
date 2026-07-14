using System.Runtime.InteropServices;

namespace rental_ps_smart_billing;

/// <summary>
/// WinExe tidak punya console di Windows. Attach ke terminal induk
/// (atau AllocConsole) agar log --verbose / --test-db terlihat.
/// </summary>
internal static class WindowsConsole
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    public static void EnsureAttached(string[] args)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var needConsole = args.Any(a =>
            a.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--test-db", StringComparison.OrdinalIgnoreCase));

        var attached = AttachConsole(AttachParentProcess);
        if (!attached && needConsole)
            attached = AllocConsole();

        if (!attached)
            return;

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        Console.WriteLine();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
