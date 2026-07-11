using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.ViewModels;

public partial class StartSessionViewModel : ViewModelBase
{
    private readonly Action<bool> _close;

    public StartSessionViewModel(
        string tvName,
        IEnumerable<BillingPackage> packages,
        string title,
        Action<bool> close)
    {
        TvName = tvName;
        Title = title;
        _close = close;

        foreach (var package in packages)
            Packages.Add(package);

        SelectedPackage = Packages.FirstOrDefault();
        CustomerName = "Guest";
        Confirmed = false;
    }

    public string Title { get; }
    public string TvName { get; }
    public ObservableCollection<BillingPackage> Packages { get; } = [];
    public bool Confirmed { get; private set; }
    public BillingPackage? ResultPackage { get; private set; }
    public string ResultCustomerName { get; private set; } = "Guest";

    [ObservableProperty]
    private BillingPackage? _selectedPackage;

    [ObservableProperty]
    private string _customerName = "Guest";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool ShowCustomerField { get; set; } = true;

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedPackage is null)
        {
            ErrorMessage = "Pilih paket terlebih dahulu.";
            return;
        }

        Confirmed = true;
        ResultPackage = SelectedPackage;
        ResultCustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "Guest" : CustomerName.Trim();
        _close(true);
    }

    [RelayCommand]
    private void Cancel() => _close(false);
}
