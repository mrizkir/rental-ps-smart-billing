using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing.ViewModels;

public partial class SmartTvListViewModel : ViewModelBase
{
    private readonly ISmartTvService _smartTvService;
    private readonly ITvModelService _tvModelService;
    private readonly Action _closeDialog;
    private Window? _ownerWindow;

    public SmartTvListViewModel(
        ISmartTvService smartTvService,
        ITvModelService tvModelService,
        Action closeDialog)
    {
        _smartTvService = smartTvService;
        _tvModelService = tvModelService;
        _closeDialog = closeDialog;
    }

    public ObservableCollection<SmartTvListItem> SmartTvs { get; } = [];

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _testMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _copyFeedback = string.Empty;

    [ObservableProperty]
    private SmartTvListItem? _selectedSmartTv;

    public bool HasTestMessage => !string.IsNullOrWhiteSpace(TestMessage);

    partial void OnTestMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasTestMessage));
        CopyTestMessageCommand.NotifyCanExecuteChanged();
        CopyFeedback = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasTestMessage))]
    private async Task CopyTestMessageAsync()
    {
        if (_ownerWindow is null || string.IsNullOrWhiteSpace(TestMessage))
            return;

        var clipboard = TopLevel.GetTopLevel(_ownerWindow)?.Clipboard;
        if (clipboard is null)
        {
            CopyFeedback = "Clipboard tidak tersedia.";
            return;
        }

        await clipboard.SetTextAsync(TestMessage);
        CopyFeedback = "Info TV disalin ke clipboard.";
    }

    public bool HasSelection => SelectedSmartTv is not null;

    public void SetOwnerWindow(Window ownerWindow) => _ownerWindow = ownerWindow;

    partial void OnSelectedSmartTvChanged(SmartTvListItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        EditSmartTvCommand.NotifyCanExecuteChanged();
        DeleteSmartTvCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        TestMessage = string.Empty;

        try
        {
            var items = await _smartTvService.GetSmartTvsAsync(cancellationToken);
            SmartTvs.Clear();
            foreach (var item in items)
                SmartTvs.Add(item);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load Smart TVs", ex);
            ErrorMessage = "Gagal memuat daftar Smart TV.";
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
    private async Task AddSmartTvAsync(CancellationToken cancellationToken)
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
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open add Smart TV dialog from list", ex);
            ErrorMessage = "Gagal membuka form tambah Smart TV.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditSmartTvAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedSmartTv is null)
            return;

        try
        {
            var smartTv = await _smartTvService.GetForEditAsync(SelectedSmartTv.Id, cancellationToken);
            if (smartTv is null)
            {
                ErrorMessage = "Smart TV tidak ditemukan.";
                return;
            }

            var dialog = new EditSmartTvWindow();
            var viewModel = new EditSmartTvViewModel(
                _smartTvService,
                _tvModelService,
                smartTv,
                () => dialog.Close());
            dialog.DataContext = viewModel;

            // Pastikan token terbaru dari DB tampil (mis. setelah Test dari list)
            await viewModel.ReloadFromDbAsync(cancellationToken);

            await dialog.ShowDialog(_ownerWindow);
            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open edit Smart TV dialog", ex);
            ErrorMessage = "Gagal membuka form edit Smart TV.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSmartTvAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedSmartTv is null)
            return;

        var confirmed = await DialogHelper.ConfirmAsync(
            _ownerWindow,
            "Hapus Smart TV",
            $"Nonaktifkan Smart TV '{SelectedSmartTv.Name}'?");

        if (!confirmed)
            return;

        try
        {
            var result = await _smartTvService.DeactivateAsync(SelectedSmartTv.Id, cancellationToken);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Gagal menghapus Smart TV.";
                return;
            }

            await LoadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to delete Smart TV", ex);
            ErrorMessage = "Gagal menghapus Smart TV.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        if (SelectedSmartTv is null)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        TestMessage = string.Empty;

        try
        {
            var result = await _smartTvService.TestConnectionAsync(SelectedSmartTv.Id, cancellationToken);
            await LoadAsync(cancellationToken);
            // Set setelah LoadAsync agar tidak ter-clear.
            TestMessage = result.Message;
            if (!result.Success)
                ErrorMessage = result.Message;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to test Smart TV connection from list", ex);
            ErrorMessage = "Gagal melakukan test koneksi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _closeDialog();
}
