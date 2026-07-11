using Avalonia;
using rental_ps_smart_billing.Data;

namespace rental_ps_smart_billing;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLog.Configure(args);
        AppLog.Info("Program.Main started");
        AppLog.Info("Tip: dotnet run -- --verbose   atau   dotnet run -- --test-db");

        if (args.Contains("--test-db", StringComparer.OrdinalIgnoreCase))
        {
            RunDatabaseTest();
            return;
        }

        try
        {
            AppServices.Initialize();
        }
        catch (Exception ex)
        {
            AppLog.Error("Startup gagal sebelum UI dibuka", ex);

            if (ex is Microsoft.Data.SqlClient.SqlException { Number: 18456 })
            {
                Console.WriteLine();
                Console.WriteLine("Autentikasi SQL Server gagal untuk user 'sa'.");
                Console.WriteLine("Password di appsettings.local.json harus SAMA PERSIS dengan DBeaver.");
                Console.WriteLine("Uji koneksi: dotnet run -- --test-db");
            }

            Environment.ExitCode = 1;
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void RunDatabaseTest()
    {
        try
        {
            var connectionString = AppServices.LoadConnectionString();
            SqlConnectionHelper.TestConnectionAsync(connectionString).GetAwaiter().GetResult();
            AppLog.Info("Database test passed.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Database test failed", ex);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
