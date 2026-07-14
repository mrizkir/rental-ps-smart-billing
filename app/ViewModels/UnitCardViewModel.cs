using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class UnitCardViewModel : ViewModelBase
{
    private readonly Func<UnitCardViewModel, Task> _startAsync;
    private readonly Func<UnitCardViewModel, Task> _extendAsync;
    private readonly Func<UnitCardViewModel, Task> _payAsync;
    private readonly Func<UnitCardViewModel, Task> _sleepTimerAsync;

    public UnitCardViewModel(
        UnitCardItem item,
        bool canStart,
        bool canEnd,
        Func<UnitCardViewModel, Task> startAsync,
        Func<UnitCardViewModel, Task> extendAsync,
        Func<UnitCardViewModel, Task> payAsync,
        Func<UnitCardViewModel, Task> sleepTimerAsync)
    {
        _startAsync = startAsync;
        _extendAsync = extendAsync;
        _payAsync = payAsync;
        _sleepTimerAsync = sleepTimerAsync;
        CanStart = canStart;
        CanEnd = canEnd;
        Apply(item);
    }

    public int SmartTvId { get; private set; }
    public int? SessionId { get; private set; }
    public bool CanStart { get; }
    public bool CanEnd { get; }

    [ObservableProperty]
    private string _tvName = string.Empty;

    [ObservableProperty]
    private string _statusLabel = "AVAILABLE";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isOpenEnded;

    [ObservableProperty]
    private string _timerDisplay = "--:--:--";

    [ObservableProperty]
    private string _customerDisplay = "-";

    [ObservableProperty]
    private string _packageDisplay = "-";

    [ObservableProperty]
    private string _startedAtDisplay = "-";

    [ObservableProperty]
    private string _amountDisplay = "Rp 0";

    [ObservableProperty]
    private DateTime? _endsAt;

    [ObservableProperty]
    private DateTime? _startedAt;

    [ObservableProperty]
    private decimal _packagePrice;

    [ObservableProperty]
    private decimal _fixedAmount;

    public bool CanExtend => IsPlaying && !IsOpenEnded;
    public bool ShowOpenEndedPay => IsPlaying && IsOpenEnded;
    public bool ShowFixedPlayingActions => IsPlaying && !IsOpenEnded;
    public bool ShowSleepTimer => IsPlaying;

    public void Apply(UnitCardItem item)
    {
        SmartTvId = item.SmartTvId;
        SessionId = item.SessionId;
        TvName = item.TvName;
        StatusLabel = item.StatusLabel;
        IsPlaying = item.IsPlaying;
        IsOpenEnded = item.IsOpenEnded;
        CustomerDisplay = string.IsNullOrWhiteSpace(item.CustomerName) ? "-" : item.CustomerName;
        PackageDisplay = string.IsNullOrWhiteSpace(item.PackageName) ? "-" : item.PackageName;
        FixedAmount = item.Amount;
        PackagePrice = item.PackagePrice;
        StartedAt = item.StartedAt;
        EndsAt = item.EndsAt;
        StartedAtDisplay = FormatStartedAt(item.StartedAt);
        RefreshTimer(DateTime.UtcNow);
        NotifyPlayingLayout();
    }

    /// <summary>
    /// Hentikan tampilan sesi di kartu segera (sebelum power-off TV selesai).
    /// </summary>
    public void MarkStopped()
    {
        SessionId = null;
        IsPlaying = false;
        IsOpenEnded = false;
        StartedAt = null;
        EndsAt = null;
        CustomerDisplay = "-";
        PackageDisplay = "-";
        StartedAtDisplay = "-";
        FixedAmount = 0;
        PackagePrice = 0;
        StatusLabel = "AVAILABLE";
        TimerDisplay = "--:--:--";
        AmountDisplay = "Rp 0";
        NotifyPlayingLayout();
    }

    public void RefreshTimer(DateTime utcNow)
    {
        if (!IsPlaying)
        {
            TimerDisplay = "--:--:--";
            AmountDisplay = "Rp 0";
            return;
        }

        if (IsOpenEnded)
        {
            if (StartedAt is null)
            {
                TimerDisplay = "00:00:00";
                AmountDisplay = "Rp 0";
                return;
            }

            var elapsed = utcNow - StartedAt.Value;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            TimerDisplay = elapsed.ToString(@"hh\:mm\:ss");
            var amount = BillingCalculator.CalculateOpenEndedAmount(
                StartedAt.Value, utcNow, PackagePrice);
            AmountDisplay = $"Rp {amount:N0}";
            return;
        }

        if (EndsAt is null)
        {
            TimerDisplay = "--:--:--";
            AmountDisplay = $"Rp {FixedAmount:N0}";
            return;
        }

        var remaining = EndsAt.Value - utcNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        TimerDisplay = remaining.ToString(@"hh\:mm\:ss");
        AmountDisplay = $"Rp {FixedAmount:N0}";
    }

    public bool IsExpired(DateTime utcNow) =>
        IsPlaying && !IsOpenEnded && EndsAt is not null && EndsAt.Value <= utcNow;

    public bool NeedsSleepTimerWarn(DateTime utcNow, int warnMinutesBeforeEnd)
    {
        if (warnMinutesBeforeEnd <= 0 || !IsPlaying || IsOpenEnded || EndsAt is null || SessionId is null)
            return false;

        var remaining = EndsAt.Value - utcNow;
        return remaining > TimeSpan.Zero
               && remaining <= TimeSpan.FromMinutes(warnMinutesBeforeEnd);
    }

    private static string FormatStartedAt(DateTime? startedAtUtc) =>
        startedAtUtc is null
            ? "-"
            : startedAtUtc.Value.ToLocalTime().ToString("HH:mm");

    [RelayCommand(CanExecute = nameof(CanExecuteStart))]
    private Task StartAsync() => _startAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecuteExtend))]
    private Task ExtendAsync() => _extendAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePlayingAction))]
    private Task PayAsync() => _payAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePlayingAction))]
    private Task SleepTimerAsync() => _sleepTimerAsync(this);

    private bool CanExecuteStart() => CanStart && !IsPlaying;

    private bool CanExecuteExtend() => CanEnd && CanExtend;

    private bool CanExecutePlayingAction() => CanEnd && IsPlaying;

    partial void OnIsPlayingChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        ExtendCommand.NotifyCanExecuteChanged();
        PayCommand.NotifyCanExecuteChanged();
        SleepTimerCommand.NotifyCanExecuteChanged();
        NotifyPlayingLayout();
    }

    partial void OnIsOpenEndedChanged(bool value)
    {
        ExtendCommand.NotifyCanExecuteChanged();
        NotifyPlayingLayout();
    }

    private void NotifyPlayingLayout()
    {
        OnPropertyChanged(nameof(CanExtend));
        OnPropertyChanged(nameof(ShowOpenEndedPay));
        OnPropertyChanged(nameof(ShowFixedPlayingActions));
        OnPropertyChanged(nameof(ShowSleepTimer));
    }
}
