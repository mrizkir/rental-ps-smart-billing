using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;

namespace rental_ps_smart_billing.ViewModels;

public partial class RevenueReportViewModel : ViewModelBase
{
    private readonly IBillingService _billingService;
    private readonly Action _closeDialog;
    private Window? _ownerWindow;

    public RevenueReportViewModel(IBillingService billingService, Action closeDialog)
    {
        _billingService = billingService;
        _closeDialog = closeDialog;

        var today = DateTime.Today;
        FromDate = today;
        ToDate = today;
    }

    public ObservableCollection<RevenueReportItem> Items { get; } = [];

    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _periodDisplay = "-";
    [ObservableProperty] private string _sessionCountDisplay = "0";
    [ObservableProperty] private string _totalAmountDisplay = "Rp 0";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasData;

    private RevenueReportResult? _lastReport;

    public void SetOwnerWindow(Window ownerWindow) => _ownerWindow = ownerWindow;

    public async Task LoadTodayAsync(CancellationToken cancellationToken = default) =>
        await LoadAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (FromDate is null || ToDate is null)
        {
            ErrorMessage = "Tanggal dari/sampai wajib diisi.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        NotifyCommands();

        try
        {
            var report = await _billingService.GetRevenueReportAsync(
                FromDate.Value.Date,
                ToDate.Value.Date,
                cancellationToken);

            _lastReport = report;
            Items.Clear();
            foreach (var item in report.Items)
                Items.Add(item);

            PeriodDisplay = report.PeriodDisplay;
            SessionCountDisplay = report.SessionCount.ToString("N0");
            TotalAmountDisplay = report.TotalAmountDisplay;
            HasData = report.Items.Count > 0;
            StatusMessage = HasData
                ? $"{report.SessionCount} transaksi selesai."
                : "Tidak ada transaksi selesai pada periode ini.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Load revenue report failed", ex);
            ErrorMessage = "Gagal memuat laporan pendapatan.";
            HasData = false;
            _lastReport = null;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand]
    private async Task LoadTodayShortcutAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        FromDate = today;
        ToDate = today;
        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task LoadThisMonthAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        FromDate = new DateTime(today.Year, today.Month, 1);
        ToDate = today;
        await LoadAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportExcelAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || _lastReport is null || _lastReport.Items.Count == 0)
            return;

        try
        {
            var storage = _ownerWindow.StorageProvider;
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Simpan Laporan Pendapatan",
                SuggestedFileName = $"pendapatan_{_lastReport.FromLocalDate:yyyyMMdd}_{_lastReport.ToLocalDate:yyyyMMdd}.xlsx",
                FileTypeChoices =
                [
                    new FilePickerFileType("Excel")
                    {
                        Patterns = ["*.xlsx"]
                    }
                ],
                DefaultExtension = "xlsx"
            });

            if (file is null)
                return;

            var path = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                ErrorMessage = "Path file tidak valid.";
                return;
            }

            IsBusy = true;
            NotifyCommands();
            await Task.Run(() => RevenueReportExporter.ExportToExcel(_lastReport, path), cancellationToken);
            StatusMessage = $"Excel disimpan: {path}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Export revenue report failed", ex);
            ErrorMessage = "Gagal mengekspor Excel.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand]
    private void Close() => _closeDialog();

    private bool CanRun() => !IsBusy;
    private bool CanExport() => !IsBusy && HasData && _lastReport is not null;

    private void NotifyCommands()
    {
        LoadCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    partial void OnHasDataChanged(bool value) => ExportExcelCommand.NotifyCanExecuteChanged();
}
