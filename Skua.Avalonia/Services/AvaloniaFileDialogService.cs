using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
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

    // Sync wrappers — these deadlock on macOS if called from the UI thread.
    // Use the async overloads instead from async command handlers.
    public string? OpenFile() => OpenFileAsync().GetAwaiter().GetResult();
    public string? OpenFile(string filter) => OpenFileAsync(filter).GetAwaiter().GetResult();
    public string? OpenFile(string initialDirectory, string filter) => OpenFileAsync(initialDirectory, filter).GetAwaiter().GetResult();
    public string? OpenFolder() => OpenFolderAsync().GetAwaiter().GetResult();
    public string? OpenFolder(string initialDirectory) => OpenFolderAsync(initialDirectory).GetAwaiter().GetResult();

    public async Task<string?> OpenFileAsync()
    {
        var window = GetMainWindow();
        if (window is null) return null;
        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(ClientFileSources.SkuaDIR)
        });
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> OpenFileAsync(string filter)
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
    }

    public async Task<string?> OpenFileAsync(string initialDirectory, string filter)
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
    }

    public async Task<string?> OpenFolderAsync()
    {
        var window = GetMainWindow();
        if (window is null) return null;
        var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> OpenFolderAsync(string initialDirectory)
    {
        var window = GetMainWindow();
        if (window is null) return null;
        var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory)
        });
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> SaveAsync()
    {
        return await SaveInternalAsync(ClientFileSources.SkuaDIR, "Text Files (*.txt)|*.txt");
    }

    public async Task<string?> SaveAsync(string filter)
    {
        return await SaveInternalAsync(ClientFileSources.SkuaDIR, filter);
    }

    public async Task<string?> SaveAsync(string initialDirectory, string filter)
    {
        return await SaveInternalAsync(initialDirectory, filter);
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
        return SaveInternalAsync(initialDirectory, filter).GetAwaiter().GetResult();
    }

    private async Task<string?> SaveInternalAsync(string initialDirectory, string filter)
    {
        var window = GetMainWindow();
        if (window is null) return null;
        var result = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
            FileTypeChoices = ParseFilter(filter)
        });
        return result?.Path.LocalPath;
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
