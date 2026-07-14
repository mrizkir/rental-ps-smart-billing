using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.Services;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing.ViewModels;

public partial class TvModelListViewModel : ViewModelBase
{
    private readonly ITvModelService _tvModelService;
    private readonly Action _closeDialog;
    private Window? _ownerWindow;

    public TvModelListViewModel(ITvModelService tvModelService, Action closeDialog)
    {
        _tvModelService = tvModelService;
        _closeDialog = closeDialog;
    }

    public ObservableCollection<TvModelListItem> Models { get; } = [];

    [ObservableProperty] private TvModelListItem? _selectedModel;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public bool HasSelection => SelectedModel is not null;

    public void SetOwnerWindow(Window ownerWindow) => _ownerWindow = ownerWindow;

    partial void OnSelectedModelChanged(TvModelListItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        EditModelCommand.NotifyCanExecuteChanged();
        DeleteModelCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var models = await _tvModelService.GetModelsAsync(cancellationToken);
            Models.Clear();
            foreach (var model in models)
                Models.Add(model);
        }
        catch (Exception ex)
        {
            AppLog.Error("Load TV models failed", ex);
            ErrorMessage = "Gagal memuat daftar model TV.";
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
    private async Task AddModelAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null)
            return;

        var dialog = new AddTvModelWindow();
        dialog.DataContext = new AddTvModelViewModel(_tvModelService, () => dialog.Close());
        await dialog.ShowDialog(_ownerWindow);
        await LoadAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditModelAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedModel is null)
            return;

        var model = await _tvModelService.GetByIdAsync(SelectedModel.Id, cancellationToken);
        if (model is null)
        {
            ErrorMessage = "Model TV tidak ditemukan.";
            return;
        }

        var dialog = new EditTvModelWindow();
        dialog.DataContext = new EditTvModelViewModel(_tvModelService, model, () => dialog.Close());
        await dialog.ShowDialog(_ownerWindow);
        await LoadAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteModelAsync(CancellationToken cancellationToken)
    {
        if (_ownerWindow is null || SelectedModel is null)
            return;

        var confirmed = await DialogHelper.ConfirmAsync(
            _ownerWindow,
            "Nonaktifkan Model TV",
            $"Nonaktifkan model '{SelectedModel.Code}'?");
        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            var result = await _tvModelService.DeactivateAsync(SelectedModel.Id, cancellationToken);
            if (!result.Success)
                ErrorMessage = result.ErrorMessage ?? "Gagal menonaktifkan model.";
            await LoadAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _closeDialog();
}
