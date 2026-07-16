using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class UnitCardViewModel : ViewModelBase
{
    private readonly Func<UnitCardViewModel, Task> _startAsync;
    private readonly Func<UnitCardViewModel, Task> _extendAsync;
    private readonly Func<UnitCardViewModel, Task> _convertToFreePlayAsync;
    private readonly Func<UnitCardViewModel, Task> _payAsync;
    private readonly Func<UnitCardViewModel, Task> _powerOnAsync;
    private readonly Func<UnitCardViewModel, Task> _powerOffAsync;

    public UnitCardViewModel(
        UnitCardItem item,
        bool canStart,
        bool canEnd,
        bool canControlPower,
        Func<UnitCardViewModel, Task> startAsync,
        Func<UnitCardViewModel, Task> extendAsync,
        Func<UnitCardViewModel, Task> convertToFreePlayAsync,
        Func<UnitCardViewModel, Task> payAsync,
        Func<UnitCardViewModel, Task> powerOnAsync,
        Func<UnitCardViewModel, Task> powerOffAsync)
    {
        _startAsync = startAsync;
        _extendAsync = extendAsync;
        _convertToFreePlayAsync = convertToFreePlayAsync;
        _payAsync = payAsync;
        _powerOnAsync = powerOnAsync;
        _powerOffAsync = powerOffAsync;
        CanStart = canStart;
        CanEnd = canEnd;
        CanControlPower = canControlPower;
        Apply(item);
    }

    public int SmartTvId { get; private set; }
    public int? SessionId { get; private set; }
    public bool CanStart { get; }
    public bool CanEnd { get; }
    public bool CanControlPower { get; }

    private string _ipAddress = string.Empty;
    private string _macAddress = string.Empty;
    private int _wsPort;
    private string? _token;

    [ObservableProperty]
    private string _tvName = string.Empty;

    [ObservableProperty]
    private string _statusLabel = "AVAILABLE";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isOpenEnded;

    /// <summary>null = checking / unknown, true = online, false = offline.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTvOnline))]
    [NotifyPropertyChangedFor(nameof(ShowTvOffline))]
    [NotifyPropertyChangedFor(nameof(ShowTvStatusUnknown))]
    [NotifyPropertyChangedFor(nameof(TvOnlineToolTip))]
    private bool? _isTvOnline;

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
    private DateTime? _openEndedFrom;

    [ObservableProperty]
    private decimal _packagePrice;

    [ObservableProperty]
    private decimal _fixedAmount;

    public bool CanExtend => IsPlaying && !IsOpenEnded;
    public bool CanConvertToFreePlay => IsPlaying && !IsOpenEnded;
    public bool ShowOpenEndedPay => IsPlaying && IsOpenEnded;
    public bool ShowFixedPlayingActions => IsPlaying && !IsOpenEnded;
    public bool ShowTvOnline => IsTvOnline == true;
    public bool ShowTvOffline => IsTvOnline == false;
    public bool ShowTvStatusUnknown => IsTvOnline is null;
    public string TvOnlineToolTip => IsTvOnline switch
    {
        true => "TV online",
        false => "TV offline",
        _ => "Memeriksa koneksi TV…"
    };

    public SmartTvTestRequest ToTestRequest() => new()
    {
        IpAddress = _ipAddress,
        MacAddress = _macAddress,
        WsPort = _wsPort,
        Token = _token
    };

    public void Apply(UnitCardItem item)
    {
        SmartTvId = item.SmartTvId;
        SessionId = item.SessionId;
        _ipAddress = item.IpAddress;
        _macAddress = item.MacAddress;
        _wsPort = item.WsPort;
        _token = item.Token;
        TvName = item.TvName;
        StatusLabel = item.StatusLabel;
        IsPlaying = item.IsPlaying;
        IsOpenEnded = item.IsOpenEnded;
        IsTvOnline = null;
        CustomerDisplay = string.IsNullOrWhiteSpace(item.CustomerName) ? "-" : item.CustomerName;
        PackageDisplay = string.IsNullOrWhiteSpace(item.PackageName) ? "-" : item.PackageName;
        FixedAmount = item.Amount;
        PackagePrice = item.PackagePrice;
        StartedAt = item.StartedAt;
        EndsAt = item.EndsAt;
        OpenEndedFrom = item.OpenEndedFrom;
        StartedAtDisplay = FormatStartedAt(item.StartedAt);
        RefreshTimer(DateTime.UtcNow);
        NotifyPlayingLayout();
    }

    public void SetTvOnline(bool isOnline) => IsTvOnline = isOnline;

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
        OpenEndedFrom = null;
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
            var billFrom = OpenEndedFrom ?? StartedAt.Value;
            var openEndedPart = BillingCalculator.CalculateOpenEndedAmount(
                billFrom, utcNow, PackagePrice);
            AmountDisplay = $"Rp {(FixedAmount + openEndedPart):N0}";
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

    public bool NeedsSessionEndWarn(DateTime utcNow, int warnMinutesBeforeEnd)
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

    [RelayCommand(CanExecute = nameof(CanExecuteConvertToFreePlay))]
    private Task ConvertToFreePlayAsync() => _convertToFreePlayAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePlayingAction))]
    private Task PayAsync() => _payAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePower))]
    private Task PowerOnAsync() => _powerOnAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePower))]
    private Task PowerOffAsync() => _powerOffAsync(this);

    private bool CanExecuteStart() => CanStart && !IsPlaying;

    private bool CanExecuteExtend() => CanEnd && CanExtend;

    private bool CanExecuteConvertToFreePlay() => CanEnd && CanConvertToFreePlay;

    private bool CanExecutePlayingAction() => CanEnd && IsPlaying;

    private bool CanExecutePower() => CanControlPower;

    partial void OnIsPlayingChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        ExtendCommand.NotifyCanExecuteChanged();
        ConvertToFreePlayCommand.NotifyCanExecuteChanged();
        PayCommand.NotifyCanExecuteChanged();
        NotifyPlayingLayout();
    }

    partial void OnIsOpenEndedChanged(bool value)
    {
        ExtendCommand.NotifyCanExecuteChanged();
        ConvertToFreePlayCommand.NotifyCanExecuteChanged();
        NotifyPlayingLayout();
    }

    private void NotifyPlayingLayout()
    {
        OnPropertyChanged(nameof(CanExtend));
        OnPropertyChanged(nameof(CanConvertToFreePlay));
        OnPropertyChanged(nameof(ShowOpenEndedPay));
        OnPropertyChanged(nameof(ShowFixedPlayingActions));
    }
}
