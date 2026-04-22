using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Skua.Core.Interfaces;
using Skua.Core.Models;
using Skua.Core.ViewModels;

namespace Skua.Avalonia.Services;

public class AvaloniaDialogService : IDialogService
{
    private Window? GetOwner()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private T InvokeOnUI<T>(Func<Task<T>> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return func().GetAwaiter().GetResult();
        return Dispatcher.UIThread.InvokeAsync(func).GetAwaiter().GetResult();
    }

    private void InvokeOnUI(Func<Task> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
            func().GetAwaiter().GetResult();
        else
            Dispatcher.UIThread.InvokeAsync(func).GetAwaiter().GetResult();
    }

    public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        return InvokeOnUI(async () =>
        {
            var dialog = new HostDialog { DataContext = viewModel };
            var owner = GetOwner();
            if (owner is not null)
                await dialog.ShowDialog<bool?>(owner);
            else
                dialog.Show();
            return dialog.DialogResult;
        });
    }

    public bool? ShowDialog<TViewModel>(TViewModel viewModel, string title) where TViewModel : class
    {
        return InvokeOnUI(async () =>
        {
            var dialog = new HostDialog { DataContext = viewModel, Title = title };
            var owner = GetOwner();
            if (owner is not null)
                await dialog.ShowDialog<bool?>(owner);
            else
                dialog.Show();
            return dialog.DialogResult;
        });
    }

    public bool? ShowDialog<TViewModel>(TViewModel viewModel, Action<TViewModel> callback) where TViewModel : class
    {
        return InvokeOnUI(async () =>
        {
            var dialog = new HostDialog { DataContext = viewModel };
            dialog.Closed += (s, e) =>
            {
                try { callback(viewModel); } catch { }
            };
            var owner = GetOwner();
            if (owner is not null)
                await dialog.ShowDialog<bool?>(owner);
            else
                dialog.Show();
            return dialog.DialogResult;
        });
    }

    public void ShowMessageBox(string message, string caption)
    {
        InvokeOnUI(async () =>
        {
            var vm = new MessageBoxDialogViewModel(message, caption);
            var dialog = new HostDialog { DataContext = vm };
            var owner = GetOwner();
            if (owner is not null)
                await dialog.ShowDialog<bool?>(owner);
            else
                dialog.Show();
        });
    }

    public bool? ShowMessageBox(string message, string caption, bool yesAndNo)
    {
        return InvokeOnUI(async () =>
        {
            var vm = new MessageBoxDialogViewModel(message, caption, yesAndNo);
            var dialog = new HostDialog { DataContext = vm };
            var owner = GetOwner();
            if (owner is not null)
                await dialog.ShowDialog<bool?>(owner);
            else
                dialog.Show();
            return dialog.DialogResult;
        });
    }

    public DialogResult ShowMessageBox(string message, string caption, params string[] buttons)
    {
        return InvokeOnUI(async () =>
        {
            var vm = new CustomDialogViewModel(message, caption, buttons);
            var dialog = new HostDialog { DataContext = vm };
            var owner = GetOwner();
            if (owner is not null)
                await dialog.ShowDialog<bool?>(owner);
            else
                dialog.Show();
            return vm.Result ?? DialogResult.Cancelled;
        });
    }
}
