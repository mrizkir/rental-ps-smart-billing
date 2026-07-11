using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class AddUserViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly Action _closeDialog;

    public AddUserViewModel(IUserService userService, IEnumerable<Role> roles, Action closeDialog)
    {
        _userService = userService;
        _closeDialog = closeDialog;

        foreach (var role in roles)
            Roles.Add(role);

        SelectedRole = Roles.FirstOrDefault();
    }

    public ObservableCollection<Role> Roles { get; } = [];

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private Role? _selectedRole;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        SaveCommand.NotifyCanExecuteChanged();

        try
        {
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Konfirmasi password tidak cocok.";
                return;
            }

            var result = await _userService.CreateUserAsync(
                Username,
                Password,
                DisplayName,
                SelectedRole?.Name ?? string.Empty,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menambah user.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Add user failed", ex);
            ErrorMessage = "Gagal menyimpan user. Periksa koneksi database.";
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
