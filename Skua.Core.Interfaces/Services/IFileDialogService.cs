namespace Skua.Core.Interfaces;

public interface IFileDialogService
{
    string? OpenFile();

    string? OpenFile(string filters);

    string? OpenFile(string initialDirectory, string filters);

    string? OpenFolder();

    string? OpenFolder(string initialDirectory);

    IEnumerable<string>? OpenText();

    string? Save();

    string? Save(string filters);

    string? Save(string initialDirectory, string filters);

    void SaveText(string contents);

    void SaveText(IEnumerable<string> contents);

    // Async overloads with default implementations for cross-platform support.
    // macOS native dialogs require the Cocoa event loop to pump, so sync
    // wrappers deadlock on the UI thread. Override these in Avalonia.
    Task<string?> OpenFileAsync() => Task.FromResult(OpenFile());
    Task<string?> OpenFileAsync(string filters) => Task.FromResult(OpenFile(filters));
    Task<string?> OpenFileAsync(string initialDirectory, string filters) => Task.FromResult(OpenFile(initialDirectory, filters));
    Task<string?> OpenFolderAsync() => Task.FromResult(OpenFolder());
    Task<string?> OpenFolderAsync(string initialDirectory) => Task.FromResult(OpenFolder(initialDirectory));
    Task<string?> SaveAsync() => Task.FromResult(Save());
    Task<string?> SaveAsync(string filters) => Task.FromResult(Save(filters));
    Task<string?> SaveAsync(string initialDirectory, string filters) => Task.FromResult(Save(initialDirectory, filters));
}