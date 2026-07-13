using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class EditPackageViewModel : ViewModelBase
{
    private readonly IBillingPackageService _packageService;
    private readonly int _packageId;
    private readonly Action _closeDialog;

    public EditPackageViewModel(
        IBillingPackageService packageService,
        BillingPackage package,
        Action closeDialog)
    {
        _packageService = packageService;
        _packageId = package.Id;
        _closeDialog = closeDialog;

        Name = package.Name;
        DurationMinutesText = package.DurationMinutes.ToString();
        PriceText = ((long)package.Price).ToString();
        IsActive = package.IsActive;
        SelectedBillingMode = BillingModeOptions.FirstOrDefault(o => o.Mode == package.BillingMode)
            ?? BillingModeOptions[0];
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
    private string _durationMinutesText = string.Empty;

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsFixedPackage =>
        SelectedBillingMode?.Mode == BillingModes.Fixed;

    public string PriceLabel => IsFixedPackage
        ? "Harga (Rp)"
        : "Tarif per menit (Rp)";

    partial void OnSelectedBillingModeChanged(PackageBillingModeOption? value)
    {
        OnPropertyChanged(nameof(IsFixedPackage));
        OnPropertyChanged(nameof(PriceLabel));
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

            var result = await _packageService.UpdatePackageAsync(
                _packageId,
                Name,
                durationMinutes,
                price,
                mode,
                IsActive,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal memperbarui paket.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Update package failed", ex);
            ErrorMessage = "Gagal menyimpan perubahan.";
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
