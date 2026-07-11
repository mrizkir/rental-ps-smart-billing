using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.ViewModels;

public partial class UnitCardViewModel : ViewModelBase
{
    private readonly Func<UnitCardViewModel, Task> _startAsync;
    private readonly Func<UnitCardViewModel, Task> _extendAsync;
    private readonly Func<UnitCardViewModel, Task> _payAsync;

    public UnitCardViewModel(
        UnitCardItem item,
        bool canStart,
        bool canEnd,
        Func<UnitCardViewModel, Task> startAsync,
        Func<UnitCardViewModel, Task> extendAsync,
        Func<UnitCardViewModel, Task> payAsync)
    {
        _startAsync = startAsync;
        _extendAsync = extendAsync;
        _payAsync = payAsync;
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
    private string _timerDisplay = "--:--:--";

    [ObservableProperty]
    private string _customerDisplay = "-";

    [ObservableProperty]
    private string _packageDisplay = "-";

    [ObservableProperty]
    private string _amountDisplay = "Rp 0";

    [ObservableProperty]
    private DateTime? _endsAt;

    public void Apply(UnitCardItem item)
    {
        SmartTvId = item.SmartTvId;
        SessionId = item.SessionId;
        TvName = item.TvName;
        StatusLabel = item.StatusLabel;
        IsPlaying = item.IsPlaying;
        CustomerDisplay = string.IsNullOrWhiteSpace(item.CustomerName) ? "-" : item.CustomerName;
        PackageDisplay = string.IsNullOrWhiteSpace(item.PackageName) ? "-" : item.PackageName;
        AmountDisplay = $"Rp {item.Amount:N0}";
        EndsAt = item.EndsAt;
        RefreshTimer(DateTime.UtcNow);
    }

    public void RefreshTimer(DateTime utcNow)
    {
        if (!IsPlaying || EndsAt is null)
        {
            TimerDisplay = "--:--:--";
            return;
        }

        var remaining = EndsAt.Value - utcNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        TimerDisplay = remaining.ToString(@"hh\:mm\:ss");
    }

    public bool IsExpired(DateTime utcNow) =>
        IsPlaying && EndsAt is not null && EndsAt.Value <= utcNow;

    [RelayCommand(CanExecute = nameof(CanExecuteStart))]
    private Task StartAsync() => _startAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePlayingAction))]
    private Task ExtendAsync() => _extendAsync(this);

    [RelayCommand(CanExecute = nameof(CanExecutePlayingAction))]
    private Task PayAsync() => _payAsync(this);

    private bool CanExecuteStart() => CanStart && !IsPlaying;

    private bool CanExecutePlayingAction() => CanEnd && IsPlaying;

    partial void OnIsPlayingChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        ExtendCommand.NotifyCanExecuteChanged();
        PayCommand.NotifyCanExecuteChanged();
    }
}
