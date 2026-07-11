using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing.ViewModels;

public partial class UserListViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly ISessionService _session;
    private readonly Action _closeDialog;
    private Window? _ownerWindow;

    public UserListViewModel(IUserService userService, ISessionService session, Action closeDialog)
    {
        _userService = userService;
        _session = session;
        _closeDialog = closeDialog;
    }

    public ObservableCollection<UserListItem> Users { get; } = [];

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private UserListItem? _selectedUser;

    public bool HasSelection => SelectedUser is not null;

    public void SetOwnerWindow(Window ownerWindow) => _ownerWindow = ownerWindow;

    partial void OnSelectedUserChanged(UserListItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        EditUserCommand.NotifyCanExecuteChanged();
        DeleteUserCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var users = await _userService.GetUsersAsync(cancellationToken);
            Users.Clear();
            foreach (var user in users)
                Users.Add(user);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load users", ex);
            ErrorMessage = "Gagal memuat daftar user.";
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
    private async Task AddUserAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var roles = await _userService.GetRolesAsync(cancellationToken);
            var dialog = new AddUserWindow();

            dialog.DataContext = new AddUserViewModel(
                _userService,
                roles,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add user dialog from list", ex);
            ErrorMessage = "Gagal membuka form tambah user.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditUserAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedUser is null)
            return;

        try
        {
            var user = await _userService.GetUserForEditAsync(SelectedUser.Id, cancellationToken);
            if (user is null)
            {
                ErrorMessage = "User tidak ditemukan.";
                return;
            }

            var roles = await _userService.GetRolesAsync(cancellationToken);
            var dialog = new EditUserWindow();
            var currentUserId = _session.CurrentUser?.Id ?? 0;

            dialog.DataContext = new EditUserViewModel(
                _userService,
                user,
                roles,
                currentUserId,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open edit user dialog", ex);
            ErrorMessage = "Gagal membuka form edit user.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteUserAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedUser is null)
            return;

        var confirmed = await DialogHelper.ConfirmAsync(
            _ownerWindow,
            "Hapus User",
            $"Nonaktifkan user '{SelectedUser.Username}'? User tidak bisa login setelah ini.");

        if (!confirmed)
            return;

        try
        {
            var currentUserId = _session.CurrentUser?.Id ?? 0;
            var result = await _userService.DeleteUserAsync(
                SelectedUser.Id,
                currentUserId,
                cancellationToken);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menghapus user.";
                return;
            }

            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to delete user", ex);
            ErrorMessage = "Gagal menghapus user.";
        }
    }

    [RelayCommand]
    private void Close() => _closeDialog();
}
