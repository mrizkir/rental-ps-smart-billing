using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace rental_ps_smart_billing.ViewModels;

public partial class ConfirmViewModel : ViewModelBase
{
    private readonly Action<bool> _closeDialog;

    public ConfirmViewModel(string title, string message, Action<bool> closeDialog)
    {
        Title = title;
        Message = message;
        _closeDialog = closeDialog;
    }

    public string Title { get; }
    public string Message { get; }

    [RelayCommand]
    private void Confirm() => _closeDialog(true);

    [RelayCommand]
    private void Cancel() => _closeDialog(false);
}
