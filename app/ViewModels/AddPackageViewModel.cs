using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public sealed class PackageBillingModeOption
{
    public required string Mode { get; init; }
    public required string Label { get; init; }
}

public partial class AddPackageViewModel : ViewModelBase
{
    private readonly IBillingPackageService _packageService;
    private readonly Action _closeDialog;

    public AddPackageViewModel(IBillingPackageService packageService, Action closeDialog)
    {
        _packageService = packageService;
        _closeDialog = closeDialog;
        DurationMinutesText = "60";
        PriceText = "15000";
        SelectedBillingMode = BillingModeOptions[0];
    }

    public ObservableCollection<PackageBillingModeOption> BillingModeOptions { get; } =
    [
        new() { Mode = BillingModes.Fixed, Label = "Paket tetap (durasi tetap)" },
        new() { Mode = BillingModes.OpenEnded, Label = "Free Play (per menit)" }
    ];

    [ObservableProperty]
    private PackageBillingModeOption? _selectedBillingMode;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _durationMinutesText = "60";

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsFixedPackage =>
        SelectedBillingMode?.Mode == BillingModes.Fixed;

    public string PriceLabel => IsFixedPackage
        ? "Harga (Rp)"
        : "Tarif per menit (Rp)";

    public string PricePlaceholder => IsFixedPackage
        ? "contoh: 15000"
        : "contoh: 200";

    partial void OnSelectedBillingModeChanged(PackageBillingModeOption? value)
    {
        OnPropertyChanged(nameof(IsFixedPackage));
        OnPropertyChanged(nameof(PriceLabel));
        OnPropertyChanged(nameof(PricePlaceholder));
        if (value?.Mode == BillingModes.OpenEnded && string.IsNullOrWhiteSpace(Name))
            Name = "Free Play";
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        SaveCommand.NotifyCanExecuteChanged();

        try
        {
            var mode = SelectedBillingMode?.Mode ?? BillingModes.Fixed;
            var durationMinutes = 0;
            if (mode == BillingModes.Fixed && !TryParseDuration(out durationMinutes))
                return;

            if (!TryParsePrice(out var price))
                return;

            var result = await _packageService.CreatePackageAsync(
                Name,
                durationMinutes,
                price,
                mode,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menambah paket.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Add package failed", ex);
            ErrorMessage = "Gagal menyimpan paket. Periksa koneksi database.";
        }
        finally
        {
            IsBusy = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void Cancel() => _closeDialog();

    private bool CanSave() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private bool TryParseDuration(out int durationMinutes)
    {
        durationMinutes = 0;
        if (!int.TryParse(DurationMinutesText.Trim(), out durationMinutes))
        {
            ErrorMessage = "Durasi harus berupa angka (menit).";
            return false;
        }

        return true;
    }

    private bool TryParsePrice(out decimal price)
    {
        price = 0;
        var text = PriceText.Trim().Replace(".", "", StringComparison.Ordinal);
        if (!decimal.TryParse(text, out price))
        {
            ErrorMessage = "Harga harus berupa angka.";
            return false;
        }

        return true;
    }
}
