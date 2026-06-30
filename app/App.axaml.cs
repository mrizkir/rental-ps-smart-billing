using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
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
#if ENABLE_XAML_HOT_RELOAD
        this.EnableHotReload();
#endif
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}