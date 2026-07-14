using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class AddSmartTvViewModel : ViewModelBase
{
    private readonly ISmartTvService _smartTvService;
    private readonly ITvModelService _tvModelService;
    private readonly Action _closeDialog;

    public AddSmartTvViewModel(
        ISmartTvService smartTvService,
        ITvModelService tvModelService,
        Action closeDialog)
    {
        _smartTvService = smartTvService;
        _tvModelService = tvModelService;
        _closeDialog = closeDialog;

        foreach (var brand in smartTvService.GetBrands())
            Brands.Add(brand);

        SelectedBrand = Brands.FirstOrDefault();
        WsPortText = "8002";
        _ = LoadModelsAsync();
    }

    public ObservableCollection<string> Brands { get; } = [];
    public ObservableCollection<TvModelListItem> Models { get; } = [];

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _selectedBrand;
    [ObservableProperty] private TvModelListItem? _selectedModel;
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private string _macAddress = string.Empty;
    [ObservableProperty] private string _wsPortText = "8002";
    [ObservableProperty] private string _pairingToken = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _testMessage = string.Empty;
    [ObservableProperty] private string _copyFeedback = string.Empty;
    [ObservableProperty] private bool _isBusy;

    private async Task LoadModelsAsync()
    {
        try
        {
            var models = await _tvModelService.GetActiveModelsAsync();
            Models.Clear();
            foreach (var model in models)
                Models.Add(model);
            SelectedModel = Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            AppLog.Error("Load TV models for Add Smart TV failed", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        if (!TryParseWsPort(out var wsPort))
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        TestMessage = string.Empty;
        NotifyCommands();

        try
        {
            var result = await _smartTvService.TestConnectionAsync(
                new SmartTvTestRequest
                {
                    IpAddress = IpAddress,
                    MacAddress = MacAddress,
                    WsPort = wsPort,
                    Token = string.IsNullOrWhiteSpace(PairingToken) ? null : PairingToken
                },
                cancellationToken);

            TestMessage = result.Message;
            if (!string.IsNullOrWhiteSpace(result.Token))
                PairingToken = result.Token;
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
            var result = await _smartTvService.CreateAsync(
                Name,
                SelectedBrand ?? string.Empty,
                SelectedModel?.Id,
                IpAddress,
                MacAddress,
                wsPort,
                string.IsNullOrWhiteSpace(PairingToken) ? null : PairingToken,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menambah Smart TV.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Add Smart TV failed", ex);
            ErrorMessage = "Gagal menyimpan Smart TV. Periksa koneksi database.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand]
    private void Cancel() => _closeDialog();

    public bool HasTestMessage => !string.IsNullOrWhiteSpace(TestMessage);

    partial void OnTestMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasTestMessage));
        CopyTestMessageCommand.NotifyCanExecuteChanged();
        CopyFeedback = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasTestMessage))]
    private async Task CopyTestMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(TestMessage))
            return;

        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.Windows.Count > 0)
        {
            var window = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
            var clipboard = window is null ? null : TopLevel.GetTopLevel(window)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(TestMessage);
                CopyFeedback = "Info TV disalin ke clipboard.";
                return;
            }
        }

        CopyFeedback = "Clipboard tidak tersedia.";
    }

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
