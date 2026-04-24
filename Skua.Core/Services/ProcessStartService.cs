using Skua.Core.Interfaces;
using Skua.Core.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Skua.Core.Services;

public class ProcessStartService : IProcessService
{
    public ProcessStartService(ISettingsService settingsService, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
    }

    private readonly string _vscPath = Path.Combine(AppContext.BaseDirectory, "VSCode", "code");
    private readonly string _scriptsPath = ClientFileSources.SkuaScriptsDIR;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    public void OpenLink(string link)
    {
        ProcessStartInfo ps = new(link)
        {
            UseShellExecute = true,
            Verb = "open"
        };
        Process.Start(ps);
    }

    public void OpenVSC()
    {
        if (_settingsService.Get("UseLocalVSC", false) && File.Exists(_vscPath))
        {
            Process.Start(_vscPath, _scriptsPath);
            return;
        }
        try
        {
            VSCode(string.Empty);
        }
        catch
        {
            _dialogService.ShowMessageBox("Could not open a code editor. Install VS Code or set a default editor for .cs files.", "Editor not found");
        }
    }

    public void OpenVSC(string path)
    {
        if (_settingsService.Get("UseLocalVSC", false) && File.Exists(_vscPath))
        {
            Process.Start(_vscPath, new[] { _scriptsPath, path, "--reuse-window" });
            return;
        }
        try
        {
            VSCode(path);
        }
        catch
        {
            try
            {
                // Fallback: open the file with the OS default editor
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", path);
                else
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void VSCode(string path)
    {
        // On macOS, "code" may not be in PATH. Try the standard VS Code CLI location first.
        string codeCmd = "code";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string macVscPath = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
            if (File.Exists(macVscPath))
                codeCmd = macVscPath;
        }

        string args = string.IsNullOrEmpty(path)
            ? _scriptsPath
            : $"\"{_scriptsPath}\" \"{path}\" --reuse-window";

        ProcessStartInfo psi = new(codeCmd, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi);
    }
}