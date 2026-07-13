using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing.ViewModels;

public partial class PackageListViewModel : ViewModelBase
{
    private readonly IBillingPackageService _packageService;
    private readonly Action _closeDialog;
    private Window? _ownerWindow;

    public PackageListViewModel(IBillingPackageService packageService, Action closeDialog)
    {
        _packageService = packageService;
        _closeDialog = closeDialog;
    }

    public ObservableCollection<BillingPackageListItem> Packages { get; } = [];

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private BillingPackageListItem? _selectedPackage;

    public bool HasSelection => SelectedPackage is not null;

    public void SetOwnerWindow(Window ownerWindow) => _ownerWindow = ownerWindow;

    partial void OnSelectedPackageChanged(BillingPackageListItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        EditPackageCommand.NotifyCanExecuteChanged();
        DeletePackageCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var packages = await _packageService.GetPackagesAsync(cancellationToken);
            Packages.Clear();
            foreach (var package in packages)
                Packages.Add(package);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load packages", ex);
            ErrorMessage = "Gagal memuat daftar paket.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    [RelayCommand]
    private async Task AddPackageAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new AddPackageWindow();
            dialog.DataContext = new AddPackageViewModel(
                _packageService,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add package dialog", ex);
            ErrorMessage = "Gagal membuka form tambah paket.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditPackageAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedPackage is null)
            return;

        try
        {
            var package = await _packageService.GetPackageByIdAsync(SelectedPackage.Id, cancellationToken);
            if (package is null)
            {
                ErrorMessage = "Paket tidak ditemukan.";
                return;
            }

            var dialog = new EditPackageWindow();
            dialog.DataContext = new EditPackageViewModel(
                _packageService,
                package,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open edit package dialog", ex);
            ErrorMessage = "Gagal membuka form edit paket.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeletePackageAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedPackage is null)
            return;

        var confirmed = await DialogHelper.ConfirmAsync(
            _ownerWindow,
            "Hapus Paket",
            $"Nonaktifkan paket '{SelectedPackage.Name}'? Paket tidak bisa dipilih untuk sesi baru.");

        if (!confirmed)
            return;

        try
        {
            var result = await _packageService.DeletePackageAsync(SelectedPackage.Id, cancellationToken);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menghapus paket.";
                return;
            }

            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to delete package", ex);
            ErrorMessage = "Gagal menghapus paket.";
        }
    }

    [RelayCommand]
    private void Close() => _closeDialog();
}
