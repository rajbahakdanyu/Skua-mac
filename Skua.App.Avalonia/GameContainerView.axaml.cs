using Avalonia.Controls;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.Interfaces;
using Skua.Avalonia.Services;

namespace Skua.App.Avalonia;

public partial class GameContainerView : UserControl
{
    public GameContainerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        InitializeGame();
    }

    private void InitializeGame()
    {
        var flashUtil = Ioc.Default.GetRequiredService<IFlashUtil>();
        flashUtil.InitializeFlash();

        // The RuffleBridge starts a local HTTPS server
        if (flashUtil is RuffleFlashUtil ruffleUtil)
        {
            int port = ruffleUtil.BridgePort;
            string url = $"https://localhost:{port}/game.html";
            StatusText.Text = $"Game running at: {url}";
            LoadingBar.IsVisible = false;

            // Launch a Chromium browser with --allow-insecure-localhost for self-signed cert
            try
            {
                string? browserPath = FindChromiumBrowser();
                if (browserPath != null)
                {
                    System.Diagnostics.Process.Start(browserPath,
                        $"--allow-insecure-localhost \"{url}\"");
                }
                else
                {
                    var processService = Ioc.Default.GetRequiredService<IProcessService>();
                    processService.OpenLink(url);
                }
            }
            catch
            {
                var processService = Ioc.Default.GetRequiredService<IProcessService>();
                processService.OpenLink(url);
            }
        }
    }

    private static string? FindChromiumBrowser()
    {
        // macOS browser paths (ordered by preference)
        string[] candidates =
        {
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/Applications/Chromium.app/Contents/MacOS/Chromium",
            "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
        };

        foreach (var path in candidates)
        {
            if (System.IO.File.Exists(path))
                return path;
        }

        // Linux: check PATH
        if (OperatingSystem.IsLinux())
        {
            string[] cmds = { "google-chrome", "chromium-browser", "chromium", "microsoft-edge" };
            foreach (var cmd in cmds)
            {
                try
                {
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("which", cmd) { RedirectStandardOutput = true, UseShellExecute = false });
                    string? result = p?.StandardOutput.ReadToEnd().Trim();
                    p?.WaitForExit();
                    if (!string.IsNullOrEmpty(result) && System.IO.File.Exists(result))
                        return result;
                }
                catch { }
            }
        }

        return null;
    }
}
