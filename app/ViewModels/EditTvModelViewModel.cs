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
        IsActive = model.IsActive;
    }

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _brand = "Samsung";
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
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

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();
}
