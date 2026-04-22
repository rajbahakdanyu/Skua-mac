using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Skua.Avalonia;
using Skua.Avalonia.Services;
using Skua.Core.AppStartup;
using Skua.Core.Interfaces;
using Skua.Core.Models;
using Skua.Core.Utils;
using Skua.Core.ViewModels;
using System.Globalization;
using Westwind.Scripting;

namespace Skua.App.Avalonia;

public sealed partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;
    private IScriptInterface _bot = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        Services = ConfigureServices();
        Services.GetRequiredService<ISettingsService>().SetApplicationVersion();

        Services.GetRequiredService<IClientFilesService>().CreateDirectories();
        Services.GetRequiredService<IClientFilesService>().CreateFiles();
        Task.Factory.StartNew(async () => await Services.GetRequiredService<IScriptServers>().GetServers());

        _bot = Services.GetRequiredService<IScriptInterface>();
        _ = Services.GetRequiredService<ILogService>();

        string[] args = Environment.GetCommandLineArgs();
        var startup = new SkuaStartupHandler(args, _bot, Services.GetRequiredService<ISettingsService>(), Services.GetRequiredService<IThemeService>());
        startup.Execute();

        RoslynLifetimeManager.WarmupRoslyn();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += OnShutdownRequested;

            // Start auto-update checks
            StartUpdateChecks();

            Services.GetRequiredService<IPluginManager>().Initialize();
            Services.GetRequiredService<IHotKeyService>().Reload();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        Services.GetRequiredService<ICaptureProxy>().Stop();

        await ((IAsyncDisposable)Services.GetRequiredService<IScriptBoost>()).DisposeAsync();
        await ((IAsyncDisposable)Services.GetRequiredService<IScriptBotStats>()).DisposeAsync();
        await ((IAsyncDisposable)Services.GetRequiredService<IScriptDrop>()).DisposeAsync();
        await Ioc.Default.GetRequiredService<IScriptManager>().StopScript();
        await ((IScriptInterfaceManager)_bot).StopTimerAsync();

        Services.GetRequiredService<IFlashUtil>().Dispose();

        WeakReferenceMessenger.Default.Cleanup();
        WeakReferenceMessenger.Default.Reset();
        StrongReferenceMessenger.Default.Reset();

        RoslynLifetimeManager.ShutdownRoslyn();
    }

    private void StartUpdateChecks()
    {
        var settings = Services.GetRequiredService<ISettingsService>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var getScripts = Services.GetRequiredService<IGetScriptsService>();

        if (settings.Get<bool>("CheckBotScriptsUpdates"))
        {
            Task.Factory.StartNew(async () =>
            {
                await getScripts.GetScriptsAsync(null, default);
                int missingBefore = getScripts.Missing;
                int outdatedBefore = getScripts.Outdated;

                if ((missingBefore > 0 || outdatedBefore > 0)
                    && (settings.Get<bool>("AutoUpdateBotScripts") || dialogService.ShowMessageBox("Would you like to update your scripts?", "Script Update", true) == true))
                {
                    await getScripts.DownloadAllWhereAsync(s => !s.Downloaded || s.Outdated);
                    int missingAfter = getScripts.Missing;
                    int outdatedAfter = getScripts.Outdated;
                    int actuallyDownloaded = missingBefore - missingAfter + (outdatedBefore - outdatedAfter);
                    if (actuallyDownloaded > 0)
                        dialogService.ShowMessageBox($"Downloaded {actuallyDownloaded} script(s).\r\nYou can disable auto script updates in Options > Application.", "Script Update");
                }
            });
        }

        if (settings.Get<bool>("CheckAdvanceSkillSetsUpdates"))
        {
            Task.Factory.StartNew(async () =>
            {
                long remoteSize = await getScripts.CheckAdvanceSkillSetsUpdates();
                if (remoteSize > 0)
                {
                    if (settings.Get<bool>("AutoUpdateAdvanceSkillSetsUpdates") || dialogService.ShowMessageBox("Would you like to update your AdvanceSkill Sets?", "AdvanceSkill Sets Update", true) == true)
                    {
                        if (await getScripts.UpdateSkillSetsFile())
                        {
                            dialogService.ShowMessageBox("AdvanceSkill Sets has been updated.", "AdvanceSkill Sets Update");
                            Services.GetRequiredService<IAdvancedSkillContainer>().SyncSkills();
                        }
                    }
                }
            });
        }

        Task.Factory.StartNew(async () => await getScripts.UpdateQuestDataFile());

        if (settings.Get<bool>("CheckJunkItemsUpdates"))
        {
            Task.Factory.StartNew(async () =>
            {
                long remoteSize = await getScripts.CheckJunkItemsUpdates();
                if (remoteSize > 0)
                {
                    if (settings.Get<bool>("AutoUpdateJunkItems") || dialogService.ShowMessageBox("Would you like to update your Junk Items list?", "Junk Items Update", true) == true)
                    {
                        if (await getScripts.UpdateJunkItemsFile())
                        {
                            Services.GetRequiredService<IJunkService>().Load();
                            dialogService.ShowMessageBox("Junk Items list has been updated.", "Junk Items Update");
                        }
                    }
                }
            });
        }
    }

    private IServiceProvider ConfigureServices()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddAvaloniaServices();

        services.AddCommonServices();

        services.AddScriptableObjects();

        services.AddCompiler();

        services.AddSkuaMainAppViewModels();

        ServiceProvider provider = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(provider);

        return provider;
    }

    // --- Dialog button click handlers ---

    public void DialogOK_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.FindAncestorOfType<Window>() is HostDialog dialog)
        {
            dialog.DialogResult = true;
            dialog.Close();
        }
    }

    public void DialogYesNo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.FindAncestorOfType<Window>() is HostDialog dialog)
        {
            dialog.DialogResult = btn.Tag?.ToString() == "True";
            dialog.Close();
        }
    }

    public void CustomDialogButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.FindAncestorOfType<Window>() is HostDialog dialog)
        {
            string text = btn.Content?.ToString() ?? string.Empty;
            if (dialog.DataContext is CustomDialogViewModel vm)
            {
                int index = vm.Buttons.IndexOf(text);
                vm.Result = new DialogResult(text, index);
            }
            dialog.DialogResult = true;
            dialog.Close();
        }
    }
}
