using Avalonia.Controls;

namespace rental_ps_smart_billing.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
                viewModel.SetOwnerWindow(this);
        };
    }
}
