using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class AddTvModelViewModel : ViewModelBase
{
    private readonly ITvModelService _tvModelService;
    private readonly Action _closeDialog;

    public AddTvModelViewModel(ITvModelService tvModelService, Action closeDialog)
    {
        _tvModelService = tvModelService;
        _closeDialog = closeDialog;
        Brand = "Samsung";
    }

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _brand = "Samsung";
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
            var result = await _tvModelService.CreateAsync(Code, Name, Brand, cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menambah model TV.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Add TV model failed", ex);
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
