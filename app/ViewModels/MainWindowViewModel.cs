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
    private readonly ITvModelService _tvModelService;
    private readonly IBillingPackageService _packageService;
    private readonly IBillingService _billingService;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<int> _sleepWarnedSessionIds = [];
    private Window? _ownerWindow;
    private bool _isRefreshing;
    private bool _disposed;
    private bool _isSendingSleepWarn;
    private CancellationTokenSource? _tvStatusCts;

    public MainWindowViewModel(
        ISessionService session,
        IUserService userService,
        ISmartTvService smartTvService,
        ITvModelService tvModelService,
        IBillingPackageService packageService,
        IBillingService billingService)
    {
        _session = session;
        _userService = userService;
        _smartTvService = smartTvService;
        _tvModelService = tvModelService;
        _packageService = packageService;
        _billingService = billingService;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
    }

    public MainWindowViewModel() : this(new SessionService(), null!, null!, null!, null!, null!)
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
    public bool CanViewBilling => _session.HasPermission("billing.view");
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
                    ConvertToFreePlayUnitAsync,
                    PayUnitAsync,
                    SleepTimerUnitAsync));
            }

            StatusMessage = Units.Count == 0
                ? "Belum ada Smart TV aktif. Tambah TV lewat menu Smart TV."
                : string.Empty;

            _ = RefreshTvOnlineStatusesAsync();
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

    private async Task RefreshTvOnlineStatusesAsync()
    {
        if (_smartTvService is null)
            return;

        _tvStatusCts?.Cancel();
        _tvStatusCts?.Dispose();
        _tvStatusCts = new CancellationTokenSource();
        var cancellationToken = _tvStatusCts.Token;
        var units = Units.ToList();

        var tasks = units.Select(async unit =>
        {
            try
            {
                var result = await _smartTvService.TestConnectionAsync(
                    unit.ToTestRequest(),
                    cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                    unit.SetTvOnline(result.Success);
            }
            catch (OperationCanceledException)
            {
                // Refresh berikutnya membatalkan pengecekan sebelumnya.
            }
            catch (Exception ex)
            {
                AppLog.Error($"TV online check failed for {unit.TvName}", ex);
                if (!cancellationToken.IsCancellationRequested)
                    unit.SetTvOnline(false);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ClockDisplay));

        var now = DateTime.UtcNow;
        foreach (var unit in Units)
            unit.RefreshTimer(now);

        if (_isRefreshing || _billingService is null)
            return;

        await TryAutoSleepWarnAsync(now);

        var expired = Units.Where(u => u.IsExpired(now) && u.SessionId is not null).ToList();
        if (expired.Count == 0)
            return;

        _isRefreshing = true;
        try
        {
            foreach (var unit in expired)
            {
                if (unit.SessionId is int sessionId)
                    _sleepWarnedSessionIds.Remove(sessionId);
                unit.MarkStopped();
            }

            await _billingService.AutoEndExpiredAsync();
            await LoadDashboardAsync();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task TryAutoSleepWarnAsync(DateTime utcNow)
    {
        var warnMinutes = BillingCalculator.SleepTimerWarnMinutesBeforeEnd;
        if (warnMinutes <= 0 || _isSendingSleepWarn || !CanEndSession)
            return;

        // Reset warn flag if session diperpanjang melewati jendela peringatan.
        foreach (var unit in Units)
        {
            if (unit.SessionId is not int sessionId || unit.EndsAt is null)
                continue;

            if (unit.EndsAt.Value - utcNow > TimeSpan.FromMinutes(warnMinutes))
                _sleepWarnedSessionIds.Remove(sessionId);
        }

        var toWarn = Units
            .Where(u => u.SessionId is not null
                        && u.NeedsSleepTimerWarn(utcNow, warnMinutes)
                        && !_sleepWarnedSessionIds.Contains(u.SessionId.Value))
            .ToList();

        if (toWarn.Count == 0)
            return;

        _isSendingSleepWarn = true;
        try
        {
            foreach (var unit in toWarn)
            {
                var sessionId = unit.SessionId!.Value;
                _sleepWarnedSessionIds.Add(sessionId);
                try
                {
                    var result = await _billingService.ShowSleepTimerAsync(unit.SmartTvId);
                    StatusMessage = result.Success
                        ? (result.WarningMessage ?? $"Sleep Timer otomatis: {unit.TvName}")
                        : (result.ErrorMessage ?? $"Sleep Timer gagal: {unit.TvName}");
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Auto sleep timer failed for TV {unit.TvName}", ex);
                }
            }
        }
        finally
        {
            _isSendingSleepWarn = false;
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

            var dialog = new ExtendSessionWindow();
            var vm = new ExtendSessionViewModel(
                unit.TvName,
                packages,
                _ => dialog.Close());
            dialog.DataContext = vm;
            await dialog.ShowDialog(_ownerWindow);

            if (!vm.Confirmed)
                return;

            IsBusy = true;
            BillingResult result;
            if (vm.IsCustomResult)
            {
                result = await _billingService.ExtendSessionByCustomAsync(
                    unit.SessionId.Value,
                    vm.ResultMinutes,
                    vm.ResultPrice);
            }
            else
            {
                if (vm.ResultPackage is null)
                    return;

                result = await _billingService.ExtendSessionAsync(
                    unit.SessionId.Value,
                    vm.ResultPackage.Id);
            }

            StatusMessage = result.Success
                ? (result.WarningMessage ?? $"Waktu ditambah: {unit.TvName} (+{vm.ResultMinutes} menit)")
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

    private async Task ConvertToFreePlayUnitAsync(UnitCardViewModel unit)
    {
        if (_ownerWindow is null || !CanEndSession || unit.SessionId is null)
            return;

        if (unit.IsOpenEnded)
        {
            StatusMessage = "Sesi sudah Free Play.";
            return;
        }

        try
        {
            var packages = await _billingService.GetOpenEndedPackagesAsync();
            if (packages.Count == 0)
            {
                StatusMessage = "Belum ada paket Free Play aktif. Tambah dulu di menu Paket.";
                return;
            }

            BillingPackage? selectedPackage;
            if (packages.Count == 1)
            {
                var only = packages[0];
                var confirmed = await DialogHelper.ConfirmAsync(
                    _ownerWindow,
                    "Ubah ke Free Play",
                    $"Ubah sesi {unit.TvName} ke {only.DisplayLabel}?\n\n" +
                    $"Biaya paket tetap ({unit.AmountDisplay}) tetap ditagih.\n" +
                    "Free Play dihitung mulai sekarang. TV tetap menyala.");
                if (!confirmed)
                    return;
                selectedPackage = only;
            }
            else
            {
                var dialog = new StartSessionWindow();
                var vm = new StartSessionViewModel(
                    unit.TvName,
                    packages,
                    "Ubah ke Free Play",
                    _ => dialog.Close())
                {
                    ShowCustomerField = false
                };
                dialog.DataContext = vm;
                await dialog.ShowDialog(_ownerWindow);

                if (!vm.Confirmed || vm.ResultPackage is null)
                    return;
                selectedPackage = vm.ResultPackage;
            }

            IsBusy = true;
            var result = await _billingService.ConvertToFreePlayAsync(
                unit.SessionId.Value,
                selectedPackage.Id);

            StatusMessage = result.Success
                ? (result.WarningMessage ?? $"Diubah ke Free Play: {unit.TvName}")
                : (result.ErrorMessage ?? "Gagal ubah ke Free Play.");

            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Convert to Free Play failed", ex);
            StatusMessage = "Gagal mengubah ke Free Play.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SleepTimerUnitAsync(UnitCardViewModel unit)
    {
        if (!CanEndSession || !unit.IsPlaying)
            return;

        try
        {
            IsBusy = true;
            var result = await _billingService.ShowSleepTimerAsync(unit.SmartTvId);
            if (unit.SessionId is int sessionId)
                _sleepWarnedSessionIds.Add(sessionId);

            StatusMessage = result.Success
                ? (result.WarningMessage ?? $"Sleep Timer dikirim: {unit.TvName}")
                : (result.ErrorMessage ?? "Gagal mengirim Sleep Timer.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Manual sleep timer failed", ex);
            StatusMessage = "Gagal mengirim Sleep Timer.";
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
            var viewModel = new SmartTvListViewModel(
                _smartTvService,
                _tvModelService,
                () => dialog.Close());
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
                _tvModelService,
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
    private async Task ListTvModelsAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new TvModelListWindow();
            var viewModel = new TvModelListViewModel(_tvModelService, () => dialog.Close());
            viewModel.SetOwnerWindow(dialog);
            dialog.DataContext = viewModel;
            await viewModel.LoadAsync();
            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open TV model list dialog", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSettings))]
    private async Task AddTvModelAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new AddTvModelWindow();
            dialog.DataContext = new AddTvModelViewModel(_tvModelService, () => dialog.Close());
            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add TV model dialog", ex);
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

    [RelayCommand(CanExecute = nameof(CanViewBilling))]
    private async Task OpenRevenueReportAsync()
    {
        if (_ownerWindow is null)
            return;

        try
        {
            var dialog = new RevenueReportWindow();
            var viewModel = new RevenueReportViewModel(_billingService, () => dialog.Close());
            viewModel.SetOwnerWindow(dialog);
            dialog.DataContext = viewModel;
            await viewModel.LoadTodayAsync();
            await dialog.ShowDialog(_ownerWindow);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open revenue report dialog", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tvStatusCts?.Cancel();
        _tvStatusCts?.Dispose();
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}