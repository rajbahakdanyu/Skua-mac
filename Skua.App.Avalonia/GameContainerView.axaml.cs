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

        if (flashUtil is RuffleFlashUtil ruffleUtil)
        {
            string url = $"https://localhost:{ruffleUtil.BridgePort}/game.html";
            StatusText.Text = $"Game running at: {url}";
            LoadingBar.IsVisible = false;
            LaunchBrowser(url);
        }
    }

    private static void LaunchBrowser(string url)
    {
        string? browser = FindChromiumBrowser();
        if (browser != null)
        {
            using var p = System.Diagnostics.Process.Start(browser, $"--allow-insecure-localhost \"{url}\"");
            return;
        }
        Ioc.Default.GetRequiredService<IProcessService>().OpenLink(url);
    }

    private static string? FindChromiumBrowser()
    {
        if (OperatingSystem.IsMacOS())
        {
            string[] candidates =
            {
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
                "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
            };
            foreach (var path in candidates)
                if (System.IO.File.Exists(path)) return path;
        }
        else if (OperatingSystem.IsLinux())
        {
            string[] cmds = { "google-chrome", "chromium-browser", "chromium", "microsoft-edge" };
            foreach (var cmd in cmds)
            {
                try
                {
                    using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("which", cmd)
                        { RedirectStandardOutput = true, UseShellExecute = false });
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
