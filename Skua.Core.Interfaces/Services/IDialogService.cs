using Skua.Core.Models;

namespace Skua.Core.Interfaces;

public interface IDialogService
{
    bool? ShowDialog<TViewModel>(TViewModel viewModel)
        where TViewModel : class;

    bool? ShowDialog<TViewModel>(TViewModel viewModel, string Title)
        where TViewModel : class;

    bool? ShowDialog<TViewModel>(TViewModel viewModel, Action<TViewModel> callback)
        where TViewModel : class;

    void ShowMessageBox(string message, string caption);

    bool? ShowMessageBox(string message, string caption, bool yesAndNo);

    DialogResult ShowMessageBox(string message, string caption, params string[] buttons);

    // Async overloads for platforms (macOS) where modal dialogs need the native event loop.
    Task ShowMessageBoxAsync(string message, string caption)
    {
        ShowMessageBox(message, caption);
        return Task.CompletedTask;
    }

    Task<bool?> ShowMessageBoxAsync(string message, string caption, bool yesAndNo)
        => Task.FromResult(ShowMessageBox(message, caption, yesAndNo));

    Task<DialogResult> ShowMessageBoxAsync(string message, string caption, params string[] buttons)
        => Task.FromResult(ShowMessageBox(message, caption, buttons));

    Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel) where TViewModel : class
        => Task.FromResult(ShowDialog(viewModel));

    Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel, string title) where TViewModel : class
        => Task.FromResult(ShowDialog(viewModel, title));

    Task<bool?> ShowDialogAsync<TViewModel>(TViewModel viewModel, Action<TViewModel> callback) where TViewModel : class
        => Task.FromResult(ShowDialog(viewModel, callback));
}