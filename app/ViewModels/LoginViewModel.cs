using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly Action _onLoginSuccess;

    public LoginViewModel(
        IAuthService authService,
        ISessionService sessionService,
        Action onLoginSuccess)
    {
        _authService = authService;
        _sessionService = sessionService;
        _onLoginSuccess = onLoginSuccess;
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        LoginCommand.NotifyCanExecuteChanged();

        try
        {
            var result = await _authService.LoginAsync(Username, Password, cancellationToken);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Login gagal.";
                return;
            }

            _sessionService.SetCurrentUser(result.User!);
            _onLoginSuccess();
        }
        catch (Exception ex)
        {
            AppLog.Error("Login error", ex);
            ErrorMessage = "Tidak dapat terhubung ke database. Periksa SQL Server.";
        }
        finally
        {
            IsBusy = false;
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanLogin() => !IsBusy;

    partial void OnIsBusyChanged(bool value) =>
        LoginCommand.NotifyCanExecuteChanged();
}
