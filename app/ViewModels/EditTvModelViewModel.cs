using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class EditTvModelViewModel : ViewModelBase
{
    private readonly ITvModelService _tvModelService;
    private readonly int _modelId;
    private readonly Action _closeDialog;

    public EditTvModelViewModel(ITvModelService tvModelService, TvModel model, Action closeDialog)
    {
        _tvModelService = tvModelService;
        _modelId = model.Id;
        _closeDialog = closeDialog;

        Code = model.Code;
        Name = model.Name;
        Brand = model.Brand;
        SelectedMode = model.SleepTimerMode;
        MinutesText = model.SleepTimerMinutes.ToString();
        KeyDelayText = model.SleepTimerKeyDelaySeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        ConfirmKeysText = model.SleepTimerConfirmKeys;
        IsActive = model.IsActive;
    }

    public string[] Modes { get; } = ["menu", "cycle"];

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _brand = "Samsung";
    [ObservableProperty] private string? _selectedMode;
    [ObservableProperty] private string _minutesText = "30";
    [ObservableProperty] private string _keyDelayText = "1.0";
    [ObservableProperty] private string _confirmKeysText = "KEY_DOWN,KEY_ENTER";
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public bool ShowConfirmKeys =>
        string.Equals(SelectedMode, "menu", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedModeChanged(string? value) =>
        OnPropertyChanged(nameof(ShowConfirmKeys));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!TryParseNumbers(out var minutes, out var delay))
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        SaveCommand.NotifyCanExecuteChanged();

        try
        {
            var result = await _tvModelService.UpdateAsync(
                _modelId,
                Code,
                Name,
                Brand,
                SelectedMode ?? "menu",
                minutes,
                delay,
                ConfirmKeysText,
                IsActive,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal memperbarui model TV.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Update TV model failed", ex);
            ErrorMessage = "Gagal menyimpan model TV.";
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

    private bool TryParseNumbers(out int minutes, out double delay)
    {
        minutes = 0;
        delay = 1;
        if (!int.TryParse(MinutesText.Trim(), out minutes))
        {
            ErrorMessage = "Durasi Sleep Timer harus angka.";
            return false;
        }

        if (!double.TryParse(KeyDelayText.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out delay))
        {
            ErrorMessage = "Delay harus angka (contoh 1.0).";
            return false;
        }

        return true;
    }

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();
}
