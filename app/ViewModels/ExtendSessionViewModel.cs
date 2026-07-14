using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.ViewModels;

public partial class ExtendSessionViewModel : ViewModelBase
{
    private readonly Action<bool> _close;

    public ExtendSessionViewModel(
        string tvName,
        IEnumerable<BillingPackage> packages,
        Action<bool> close)
    {
        TvName = tvName;
        _close = close;

        foreach (var package in packages)
            Packages.Add(package);

        SelectedPackage = Packages.FirstOrDefault();
        UseCustomMinutes = Packages.Count == 0;
        MinutesText = "10";
        PriceText = "0";
        Confirmed = false;
    }

    public string TvName { get; }
    public ObservableCollection<BillingPackage> Packages { get; } = [];
    public bool HasPackages => Packages.Count > 0;
    public bool Confirmed { get; private set; }
    public bool IsCustomResult { get; private set; }
    public BillingPackage? ResultPackage { get; private set; }
    public int ResultMinutes { get; private set; }
    public decimal ResultPrice { get; private set; }

    [ObservableProperty]
    private bool _useCustomMinutes;

    [ObservableProperty]
    private BillingPackage? _selectedPackage;

    [ObservableProperty]
    private string _minutesText = "10";

    [ObservableProperty]
    private string _priceText = "0";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool UsePackage => !UseCustomMinutes;

    partial void OnUseCustomMinutesChanged(bool value)
    {
        OnPropertyChanged(nameof(UsePackage));
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void SelectPackageMode()
    {
        if (!HasPackages)
            return;
        UseCustomMinutes = false;
    }

    [RelayCommand]
    private void SelectCustomMode() => UseCustomMinutes = true;

    [RelayCommand]
    private void Confirm()
    {
        if (UseCustomMinutes)
        {
            if (!int.TryParse(MinutesText.Trim(), out var minutes) || minutes < 1)
            {
                ErrorMessage = "Durasi harus angka minimal 1 menit.";
                return;
            }

            if (minutes > 1440)
            {
                ErrorMessage = "Durasi maksimal 1440 menit (24 jam).";
                return;
            }

            if (!decimal.TryParse(
                    PriceText.Trim().Replace(".", "", StringComparison.Ordinal),
                    out var price)
                || price < 0)
            {
                ErrorMessage = "Harga harus angka 0 atau lebih.";
                return;
            }

            Confirmed = true;
            IsCustomResult = true;
            ResultMinutes = minutes;
            ResultPrice = price;
            ResultPackage = null;
            _close(true);
            return;
        }

        if (SelectedPackage is null)
        {
            ErrorMessage = "Pilih paket terlebih dahulu.";
            return;
        }

        Confirmed = true;
        IsCustomResult = false;
        ResultPackage = SelectedPackage;
        ResultMinutes = SelectedPackage.DurationMinutes;
        ResultPrice = SelectedPackage.Price;
        _close(true);
    }

    [RelayCommand]
    private void Cancel() => _close(false);
}
