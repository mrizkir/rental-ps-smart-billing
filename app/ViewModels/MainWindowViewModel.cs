using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ISessionService _session;
    private readonly IUserService _userService;
    private readonly ISmartTvService _smartTvService;
    private readonly IBillingPackageService _packageService;
    private readonly IBillingService _billingService;
    private readonly DispatcherTimer _timer;
    private Window? _ownerWindow;
    private bool _isRefreshing;
    private bool _disposed;

    public MainWindowViewModel(
        ISessionService session,
        IUserService userService,
        ISmartTvService smartTvService,
        IBillingPackageService packageService,
        IBillingService billingService)
    {
        _session = session;
        _userService = userService;
        _smartTvService = smartTvService;
        _packageService = packageService;
        _billingService = billingService;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
    }

    public MainWindowViewModel() : this(new SessionService(), null!, null!, null!, null!)
    {
    }

    public ObservableCollection<UnitCardViewModel> Units { get; } = [];

    public string WelcomeMessage =>
        _session.CurrentUser is null
            ? "Selamat datang"
            : $"Selamat datang, {_session.CurrentUser.DisplayName}";

    public string RoleLabel =>
        _session.CurrentUser?.Roles.FirstOrDefault() ?? "-";

    public string ClockDisplay => DateTime.Now.ToString("dddd, dd MMMM yyyy  HH:mm:ss");

    public bool CanManageUsers => _session.HasPermission("users.manage");
    public bool CanManageSettings => _session.HasPermission("settings.manage");
    public bool CanStartSession => _session.HasPermission("billing.session.start");
    public bool CanEndSession => _session.HasPermission("billing.session.end");
    public bool IsSuperAdmin => _session.IsInRole("superadmin");

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public void SetOwnerWindow(Window ownerWindow)
    {
        _ownerWindow = ownerWindow;
        _timer.Start();
    }

    public async Task LoadDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (_billingService is null)
            return;

        IsBusy = true;
        try
        {
            await _billingService.AutoEndExpiredAsync(cancellationToken);
            var cards = await _billingService.GetDashboardAsync(cancellationToken);
            Units.Clear();
            foreach (var card in cards)
            {
                Units.Add(new UnitCardViewModel(
                    card,
                    CanStartSession,
                    CanEndSession,
                    StartUnitAsync,
                    ExtendUnitAsync,
                    PayUnitAsync));
            }

            StatusMessage = Units.Count == 0
                ? "Belum ada Smart TV aktif. Tambah TV lewat menu Smart TV."
                : string.Empty;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load billing dashboard", ex);
            StatusMessage = "Gagal memuat dashboard billing.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ClockDisplay));

        var now = DateTime.UtcNow;
        foreach (var unit in Units)
            unit.RefreshTimer(now);

        if (_isRefreshing || _billingService is null)
            return;

        var expired = Units.Where(u => u.IsExpired(now) && u.SessionId is not null).ToList();
        if (expired.Count == 0)
            return;

        _isRefreshing = true;
        try
        {
            foreach (var unit in expired)
                unit.MarkStopped();

            await _billingService.AutoEndExpiredAsync();
            await LoadDashboardAsync();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task StartUnitAsync(UnitCardViewModel unit)
    {
        if (_ownerWindow is null || !CanStartSession)
            return;

        try
        {
            var packages = await _billingService.GetPackagesAsync();
            if (packages.Count == 0)
            {
                StatusMessage = "Belum ada paket billing. Seed paket gagal atau kosong.";
                return;
            }

            var dialog = new StartSessionWindow();
            var vm = new StartSessionViewModel(
                unit.TvName,
                packages,
                "Mulai Sesi",
                _ => dialog.Close())
            {
                ShowCustomerField = true
            };
            dialog.DataContext = vm;
            await dialog.ShowDialog(_ownerWindow);

            if (!vm.Confirmed || vm.ResultPackage is null)
                return;

            IsBusy = true;
            var result = await _billingService.StartSessionAsync(
                unit.SmartTvId,
                vm.ResultPackage.Id,
                vm.ResultCustomerName,
                _session.CurrentUser?.Id);

            StatusMessage = result.Success
                ? (result.WarningMessage ?? $"Sesi dimulai: {unit.TvName}")
                : (result.ErrorMessage ?? "Gagal mulai sesi.");

            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Start session failed", ex);
            StatusMessage = "Gagal memulai sesi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExtendUnitAsync(UnitCardViewModel unit)
    {
        if (_ownerWindow is null || !CanEndSession || unit.SessionId is null)
            return;

        if (unit.IsOpenEnded)
        {
            StatusMessage = "Sesi Free Play tidak bisa ditambah waktu. Gunakan BAYAR.";
            return;
        }

        try
        {
            var packages = await _billingService.GetFixedPackagesAsync();
            if (packages.Count == 0)
            {
                StatusMessage = "Tidak ada paket tetap untuk menambah waktu.";
                return;
            }

            var dialog = new StartSessionWindow();
            var vm = new StartSessionViewModel(
                unit.TvName,
                packages,
                "Tambah Waktu",
                _ => dialog.Close())
            {
                ShowCustomerField = false
            };
            dialog.DataContext = vm;
            await dialog.ShowDialog(_ownerWindow);

            if (!vm.Confirmed || vm.ResultPackage is null)
                return;

            IsBusy = true;
            var result = await _billingService.ExtendSessionAsync(unit.SessionId.Value, vm.ResultPackage.Id);
            StatusMessage = result.Success
                ? (result.WarningMessage ?? $"Waktu ditambah: {unit.TvName}")
                : (result.ErrorMessage ?? "Gagal tambah waktu.");

            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Extend session failed", ex);
            StatusMessage = "Gagal menambah waktu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PayUnitAsync(UnitCardViewModel unit)
    {
        if (_ownerWindow is null || !CanEndSession || unit.SessionId is null)
            return;

        var confirmMessage = unit.IsOpenEnded
            ? $"Akhiri Free Play {unit.TvName}? Estimasi tagihan {unit.AmountDisplay} (dihitung ulang saat bayar). TV akan dimatikan."
            : $"Akhiri sesi {unit.TvName}? Total {unit.AmountDisplay}. TV akan dimatikan.";

        var confirmed = await DialogHelper.ConfirmAsync(
            _ownerWindow,
            "Bayar / Akhiri Sesi",
            confirmMessage);

        if (!confirmed)
            return;

        var sessionId = unit.SessionId.Value;
        var tvName = unit.TvName;
        // Stop timer/UI immediately — power-off TV can take tens of seconds.
        unit.MarkStopped();

        try
        {
            IsBusy = true;
            var result = await _billingService.EndSessionAsync(sessionId);
            if (result.Success)
            {
                var total = result.Amount is decimal amount
                    ? $"Rp {amount:N0}"
                    : unit.AmountDisplay;
                StatusMessage = result.WarningMessage
                    ?? $"Sesi selesai: {tvName} — {total}";
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Gagal mengakhiri sesi.";
            }

            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("End session failed", ex);
            StatusMessage = "Gagal mengakhiri sesi.";
            await LoadDashboardAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshDashboardAsync() => await LoadDashboardAsync();

    [RelayCommand(CanExecute = nameof(CanManageUsers))]
    private async Task ListUsersAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new UserListWindow();
            var viewModel = new UserListViewModel(_userService, _session, () => dialog.Close());
            viewModel.SetOwnerWindow(dialog);
            dialog.DataContext = viewModel;

            await viewModel.LoadAsync();
            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open user list dialog", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageUsers))]
    private async Task AddUserAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var roles = await _userService.GetRolesAsync();
            var dialog = new AddUserWindow();

            dialog.DataContext = new AddUserViewModel(
                _userService,
                roles,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add user dialog", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSettings))]
    private async Task ListSmartTvsAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new SmartTvListWindow();
            var viewModel = new SmartTvListViewModel(_smartTvService, () => dialog.Close());
            viewModel.SetOwnerWindow(dialog);
            dialog.DataContext = viewModel;

            await viewModel.LoadAsync();
            await dialog.ShowDialog(_ownerWindow);
            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open Smart TV list dialog", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSettings))]
    private async Task AddSmartTvAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new AddSmartTvWindow();
            dialog.DataContext = new AddSmartTvViewModel(
                _smartTvService,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add Smart TV dialog", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSettings))]
    private async Task ListPackagesAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new PackageListWindow();
            var viewModel = new PackageListViewModel(_packageService, () => dialog.Close());
            viewModel.SetOwnerWindow(dialog);
            dialog.DataContext = viewModel;

            await viewModel.LoadAsync();
            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open package list dialog", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSettings))]
    private async Task AddPackageAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new AddPackageWindow();
            dialog.DataContext = new AddPackageViewModel(
                _packageService,
                () => dialog.Close());

            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add package dialog", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
