using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Skua.Core.Interfaces;
using Skua.Core.Models;

namespace Skua.Avalonia.Services;

public class AvaloniaFileDialogService : IFileDialogService
{
    private Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private T? InvokeOnUI<T>(Func<Task<T?>> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return func().GetAwaiter().GetResult();
        return Dispatcher.UIThread.InvokeAsync(func).GetAwaiter().GetResult();
    }

    public string? OpenFile()
    {
        return InvokeOnUI(async () =>
        {
            var window = GetMainWindow();
            if (window is null) return null;
            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(ClientFileSources.SkuaDIR)
            });
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        });
    }

    public string? OpenFile(string filter)
    {
        return InvokeOnUI(async () =>
        {
            var window = GetMainWindow();
            if (window is null) return null;
            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(ClientFileSources.SkuaDIR),
                FileTypeFilter = ParseFilter(filter)
            });
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        });
    }

    public string? OpenFile(string initialDirectory, string filter)
    {
        return InvokeOnUI(async () =>
        {
            var window = GetMainWindow();
            if (window is null) return null;
            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
                FileTypeFilter = ParseFilter(filter)
            });
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        });
    }

    public string? OpenFolder(string initialDirectory)
    {
        return InvokeOnUI(async () =>
        {
            var window = GetMainWindow();
            if (window is null) return null;
            var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory)
            });
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        });
    }

    public string? OpenFolder()
    {
        return InvokeOnUI(async () =>
        {
            var window = GetMainWindow();
            if (window is null) return null;
            var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        });
    }

    public IEnumerable<string>? OpenText()
    {
        var file = OpenFile("Text Files (*.txt)|*.txt");
        return file is not null ? File.ReadAllLines(file) : null;
    }

    public string? Save()
    {
        return SaveInternal(ClientFileSources.SkuaDIR, "Text Files (*.txt)|*.txt");
    }

    public string? Save(string filter)
    {
        return SaveInternal(ClientFileSources.SkuaDIR, filter);
    }

    public string? Save(string initialDirectory, string filter)
    {
        return SaveInternal(initialDirectory, filter);
    }

    public void SaveText(string contents)
    {
        var file = Save();
        if (!string.IsNullOrEmpty(file))
            File.WriteAllText(file, contents);
    }

    public void SaveText(IEnumerable<string> contents)
    {
        var file = Save();
        if (!string.IsNullOrEmpty(file))
            File.WriteAllLines(file, contents);
    }

    private string? SaveInternal(string initialDirectory, string filter)
    {
        return InvokeOnUI(async () =>
        {
            var window = GetMainWindow();
            if (window is null) return null;
            var result = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
                FileTypeChoices = ParseFilter(filter)
            });
            return result?.Path.LocalPath;
        });
    }

    private static List<FilePickerFileType> ParseFilter(string filter)
    {
        var types = new List<FilePickerFileType>();
        var parts = filter.Split('|');
        for (int i = 0; i < parts.Length - 1; i += 2)
        {
            var name = parts[i];
            var patterns = parts[i + 1].Split(';').Select(p => p.Trim()).ToList();
            types.Add(new FilePickerFileType(name) { Patterns = patterns });
        }
        return types;
    }
}
