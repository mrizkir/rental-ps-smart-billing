using Microsoft.Extensions.Configuration;
using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing;

public static class AppServices
{
    public static ISessionService Session { get; private set; } = null!;
    public static IAuthService Auth { get; private set; } = null!;
    public static IUserService Users { get; private set; } = null!;
    public static ISmartTvService SmartTvs { get; private set; } = null!;
    public static IBillingPackageService Packages { get; private set; } = null!;
    public static IBillingService Billing { get; private set; } = null!;
    public static bool IsInitialized { get; private set; }

    public static string LoadConnectionString()
    {
        var basePath = AppContext.BaseDirectory;
        AppLog.Step($"Base path: {basePath}");

        var localConfigPath = Path.Combine(basePath, "appsettings.local.json");
        AppLog.Step($"appsettings.local.json exists: {File.Exists(localConfigPath)}");

        var configuration = BuildConfiguration();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' tidak ditemukan.");

        connectionString = SqlConnectionHelper.Normalize(connectionString);

        if (connectionString.Contains("CHANGE_ME", StringComparison.Ordinal))
        {
            AppLog.Warn("Password masih CHANGE_ME. Isi app/appsettings.local.json atau set env RENTAL_PS_ConnectionStrings__Default.");
        }

        AppLog.Step($"Connection string: {AppLog.MaskConnectionString(connectionString)}");
        return connectionString;
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables(prefix: "RENTAL_PS_")
            .Build();

    public static void Initialize()
    {
        if (IsInitialized)
            return;

        AppLog.Info("AppServices.Initialize started");
        var configuration = BuildConfiguration();
        var connectionString = LoadConnectionString();
        var connectionFactory = new SqlConnectionFactory(connectionString);

        AppLog.Info("Initializing database...");
        var databaseInitializer = new DatabaseInitializer(connectionFactory);
        databaseInitializer.InitializeAsync().GetAwaiter().GetResult();
        AppLog.Info("Database initialized");

        var userRepository = new UserRepository(connectionFactory);
        var smartTvRepository = new SmartTvRepository(connectionFactory);
        var packageRepository = new BillingPackageRepository(connectionFactory);
        var sessionRepository = new RentalSessionRepository(connectionFactory);

        var tvBaseUrl = configuration["TvService:BaseUrl"] ?? "http://127.0.0.1:5001";
        var tvApiClient = new TvApiClient(tvBaseUrl);

        Session = new SessionService();
        Auth = new AuthService(userRepository);
        Users = new UserService(userRepository);
        SmartTvs = new SmartTvService(smartTvRepository, tvApiClient);
        Packages = new BillingPackageService(packageRepository);
        Billing = new BillingService(sessionRepository, packageRepository, smartTvRepository, tvApiClient);
        IsInitialized = true;

        AppLog.Info("AppServices.Initialize completed");
    }
}
