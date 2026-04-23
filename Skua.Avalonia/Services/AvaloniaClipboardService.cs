using Avalonia;
using Avalonia.Input;
using Skua.Core.Interfaces;

namespace Skua.Avalonia.Services;

public class AvaloniaClipboardService : IClipboardService
{
    private global::Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        return Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow?.Clipboard
            : null;
    }

    public void SetText(string text)
    {
        var clipboard = GetClipboard();
        clipboard?.SetTextAsync(text).GetAwaiter().GetResult();
    }

    public void SetData(string format, object data)
    {
        var clipboard = GetClipboard();
        if (clipboard is null) return;
        var dataObject = new DataObject();
        dataObject.Set(format, data);
        clipboard.SetDataObjectAsync(dataObject).GetAwaiter().GetResult();
    }

    public object GetData(string format)
    {
        var clipboard = GetClipboard();
        if (clipboard is null) return string.Empty;
        var formats = clipboard.GetFormatsAsync().GetAwaiter().GetResult();
        if (formats?.Contains(format) == true)
        {
            return clipboard.GetDataAsync(format).GetAwaiter().GetResult() ?? string.Empty;
        }
        return string.Empty;
    }

    public string GetText()
    {
        var clipboard = GetClipboard();
        return clipboard?.GetTextAsync().GetAwaiter().GetResult() ?? string.Empty;
    }
}
