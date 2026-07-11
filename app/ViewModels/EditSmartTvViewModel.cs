using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class EditSmartTvViewModel : ViewModelBase
{
    private readonly ISmartTvService _smartTvService;
    private readonly int _smartTvId;
    private readonly Action _closeDialog;

    public EditSmartTvViewModel(
        ISmartTvService smartTvService,
        SmartTvEditDetails smartTv,
        Action closeDialog)
    {
        _smartTvService = smartTvService;
        _smartTvId = smartTv.Id;
        _closeDialog = closeDialog;

        ApplyDetails(smartTv);

        foreach (var brand in smartTvService.GetBrands())
            Brands.Add(brand);

        SelectedBrand = Brands.FirstOrDefault(b =>
            b.Equals(smartTv.Brand, StringComparison.OrdinalIgnoreCase))
            ?? Brands.FirstOrDefault();
    }

    public ObservableCollection<string> Brands { get; } = [];

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _selectedBrand;

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private string _wsPortText = "8002";

    /// <summary>Token pairing TV (nilai string dari DB), bukan path file.</summary>
    [ObservableProperty]
    private string _pairingToken = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _testMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public async Task ReloadFromDbAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _smartTvService.GetForEditAsync(_smartTvId, cancellationToken);
        if (latest is null)
            return;

        ApplyDetails(latest);

        SelectedBrand = Brands.FirstOrDefault(b =>
            b.Equals(latest.Brand, StringComparison.OrdinalIgnoreCase))
            ?? SelectedBrand;
    }

    private void ApplyDetails(SmartTvEditDetails smartTv)
    {
        Name = smartTv.Name;
        IpAddress = smartTv.IpAddress;
        MacAddress = smartTv.MacAddress;
        WsPortText = smartTv.WsPort.ToString();
        PairingToken = smartTv.Token?.Trim() ?? string.Empty;
        IsActive = smartTv.IsActive;
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        TestMessage = string.Empty;
        NotifyCommands();

        try
        {
            // Simpan dulu perubahan form (IP/MAC/port/token) agar test memakai data terbaru
            if (!TryParseWsPort(out var wsPort))
                return;

            var saveResult = await _smartTvService.UpdateAsync(
                _smartTvId,
                Name,
                SelectedBrand ?? string.Empty,
                IpAddress,
                MacAddress,
                wsPort,
                string.IsNullOrWhiteSpace(PairingToken) ? null : PairingToken.Trim(),
                IsActive,
                cancellationToken);

            if (!saveResult.Success)
            {
                ErrorMessage = saveResult.ErrorMessage ?? "Gagal menyimpan data sebelum test.";
                return;
            }

            // Test by id: update LastTest* + Token di DB jika Python mengembalikan token
            var result = await _smartTvService.TestConnectionAsync(_smartTvId, cancellationToken);
            TestMessage = result.Message;

            // Selalu muat ulang dari DB agar token yang tersimpan tampil di form
            await ReloadFromDbAsync(cancellationToken);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Token))
                PairingToken = result.Token.Trim();
        }
        catch (Exception ex)
        {
            AppLog.Error("Test Smart TV connection failed", ex);
            TestMessage = "Gagal melakukan test koneksi.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!TryParseWsPort(out var wsPort))
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        TestMessage = string.Empty;
        NotifyCommands();

        try
        {
            var result = await _smartTvService.UpdateAsync(
                _smartTvId,
                Name,
                SelectedBrand ?? string.Empty,
                IpAddress,
                MacAddress,
                wsPort,
                string.IsNullOrWhiteSpace(PairingToken) ? null : PairingToken.Trim(),
                IsActive,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal memperbarui Smart TV.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Update Smart TV failed", ex);
            ErrorMessage = "Gagal menyimpan perubahan.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand]
    private void Cancel() => _closeDialog();

    private bool CanSave() => !IsBusy;

    private bool CanTest() => !IsBusy;

    private bool TryParseWsPort(out int wsPort)
    {
        if (int.TryParse(WsPortText.Trim(), out wsPort))
            return true;

        ErrorMessage = "Port WebSocket harus berupa angka.";
        return false;
    }

    private void NotifyCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommands();
}
