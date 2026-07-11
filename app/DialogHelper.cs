using Avalonia.Controls;
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
}
