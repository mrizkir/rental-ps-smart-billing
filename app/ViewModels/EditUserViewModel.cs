using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class EditUserViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly int _userId;
    private readonly int _currentUserId;
    private readonly Action _closeDialog;

    public EditUserViewModel(
        IUserService userService,
        UserEditDetails user,
        IEnumerable<Role> roles,
        int currentUserId,
        Action closeDialog)
    {
        _userService = userService;
        _userId = user.Id;
        _currentUserId = currentUserId;
        _closeDialog = closeDialog;

        Username = user.Username;
        DisplayName = user.DisplayName;
        IsActive = user.IsActive;

        foreach (var role in roles)
            Roles.Add(role);

        SelectedRole = Roles.FirstOrDefault(r =>
            r.Name.Equals(user.RoleName, StringComparison.OrdinalIgnoreCase))
            ?? Roles.FirstOrDefault();
    }

    public ObservableCollection<Role> Roles { get; } = [];

    public string Username { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private Role? _selectedRole;

    [ObservableProperty]
    private bool _isActive = true;

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
            if (!string.IsNullOrWhiteSpace(Password) && Password != ConfirmPassword)
            {
                ErrorMessage = "Konfirmasi password tidak cocok.";
                return;
            }

            var result = await _userService.UpdateUserAsync(
                _userId,
                DisplayName,
                SelectedRole?.Name ?? string.Empty,
                string.IsNullOrWhiteSpace(Password) ? null : Password,
                IsActive,
                _currentUserId,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal memperbarui user.";
                return;
            }

            _closeDialog();
        }
        catch (Exception ex)
        {
            AppLog.Error("Update user failed", ex);
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
}
