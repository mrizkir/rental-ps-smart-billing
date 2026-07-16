using Avalonia.Controls;
using rental_ps_smart_billing.Models;
using rental_ps_smart_billing.ViewModels;
using rental_ps_smart_billing.Views;

namespace rental_ps_smart_billing;

public static class DialogHelper
{
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var dialog = new ConfirmWindow();
        var tcs = new TaskCompletionSource<bool>();

        dialog.DataContext = new ConfirmViewModel(title, message, confirmed =>
        {
            tcs.TrySetResult(confirmed);
            dialog.Close();
        });

        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }

    public static async Task ShowSessionEndedSummaryAsync(
        Window owner,
        IReadOnlyList<AutoEndedSessionItem> items)
    {
        if (items.Count == 0)
            return;

        var dialog = new SessionEndedSummaryWindow();
        var tcs = new TaskCompletionSource();

        dialog.DataContext = new SessionEndedSummaryViewModel(items, () =>
        {
            tcs.TrySetResult();
            dialog.Close();
        });

        dialog.Closed += (_, _) => tcs.TrySetResult();

        await dialog.ShowDialog(owner);
        await tcs.Task;
    }
}
