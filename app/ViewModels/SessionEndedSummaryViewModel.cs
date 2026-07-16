using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.ViewModels;

public partial class SessionEndedSummaryViewModel : ViewModelBase
{
    private readonly Action _closeDialog;

    public SessionEndedSummaryViewModel(IReadOnlyList<AutoEndedSessionItem> items, Action closeDialog)
    {
        _closeDialog = closeDialog;
        Items = new ObservableCollection<AutoEndedSessionItem>(items);
        SessionCount = items.Count;
        TotalAmount = items.Sum(i => i.Amount);
        Title = items.Count == 1
            ? "Sesi selesai — tagihan"
            : $"Sesi selesai ({items.Count} unit) — tagihan";
        Headline = items.Count == 1
            ? "Waktu sewa habis. Tagihan yang harus dibayar:"
            : "Waktu sewa habis pada beberapa unit. Tagihan yang harus dibayar:";
    }

    public string Title { get; }
    public string Headline { get; }
    public ObservableCollection<AutoEndedSessionItem> Items { get; }
    public int SessionCount { get; }
    public decimal TotalAmount { get; }

    public string TotalAmountDisplay => $"Rp {TotalAmount:N0}";

    public string FooterHint =>
        "Detail juga tersedia di Laporan Pendapatan.";

    [RelayCommand]
    private void Close() => _closeDialog();
}
