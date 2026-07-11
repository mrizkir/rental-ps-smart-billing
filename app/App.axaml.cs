using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
#if ENABLE_XAML_HOT_RELOAD
using HotAvalonia;
#endif
using rental_ps_smart_billing.ViewModels;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing;

public partial class App : Application
{
    public override void Initialize()
    {
        AppLog.Step("App.Initialize started");
#if ENABLE_XAML_HOT_RELOAD
        this.EnableHotReload();
#endif
        AvaloniaXamlLoader.Load(this);
        AppLog.Step("App.Initialize completed");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppLog.Step("OnFrameworkInitializationCompleted started");

        if (!AppServices.IsInitialized)
            throw new InvalidOperationException("AppServices belum diinisialisasi.");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShowLogin(desktop);
        }
        else
        {
            AppLog.Warn("ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime");
        }

        base.OnFrameworkInitializationCompleted();
        AppLog.Info("Application ready");
    }

    private static void ShowLogin(IClassicDesktopStyleApplicationLifetime desktop)
    {
        AppLog.Step("Creating LoginWindow...");
        var loginWindow = new LoginWindow
        {
            DataContext = new LoginViewModel(
                AppServices.Auth,
                AppServices.Session,
                () => OnLoginSuccess(desktop))
        };

        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        AppLog.Info("LoginWindow shown");
    }

    private static void OnLoginSuccess(IClassicDesktopStyleApplicationLifetime desktop)
    {
        AppLog.Info("Login successful, opening MainWindow...");
        var previousWindow = desktop.MainWindow;
        var mainWindow = new MainWindow
        {
            DataContext = new MainWindowViewModel(
                AppServices.Session,
                AppServices.Users,
                AppServices.SmartTvs,
                AppServices.Billing)
        };

        if (mainWindow.DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetOwnerWindow(mainWindow);
            _ = viewModel.LoadDashboardAsync();
        }

        desktop.MainWindow = mainWindow;
        mainWindow.WindowState = Avalonia.Controls.WindowState.Maximized;
        mainWindow.Show();
        previousWindow?.Close();
        AppLog.Info("MainWindow shown");
    }
}
