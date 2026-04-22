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

            // Launch Chrome with --allow-insecure-localhost to accept self-signed cert
            try
            {
                string chromePath = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
                if (System.IO.File.Exists(chromePath))
                {
                    System.Diagnostics.Process.Start(chromePath,
                        $"--allow-insecure-localhost \"{url}\"");
                }
                else
                {
                    // Fallback to default browser (user may need to accept cert warning)
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
}
