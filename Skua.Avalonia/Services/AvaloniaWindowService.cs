using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Skua.Core.Interfaces;
using Skua.Core.ViewModels;

namespace Skua.Avalonia.Services;

public class AvaloniaWindowService : IWindowService, IDisposable
{
    private readonly Dictionary<string, Window> _managedWindows = new();
    private readonly IServiceProvider _services;

    public AvaloniaWindowService(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowWindow<TViewModel>() where TViewModel : class
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var window = new HostWindow
            {
                DataContext = _services.GetService<TViewModel>()
            };
            window.Show();
        });
    }

    public void ShowWindow<TViewModel>(int width, int height) where TViewModel : class
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var window = new HostWindow
            {
                DataContext = _services.GetService<TViewModel>(),
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Show();
        });
    }

    public void ShowWindow<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var window = new HostWindow
            {
                DataContext = viewModel
            };
            window.Show();
        });
    }

    public void ShowManagedWindow(string key)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!_managedWindows.TryGetValue(key, out var window))
                return;

            if (window.IsVisible)
            {
                window.Activate();
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                return;
            }

            window.Show();
            window.Activate();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            if (window.DataContext is ObservableRecipient recipient)
                recipient.IsActive = true;
        });
    }

    public void RegisterManagedWindow<TViewModel>(string key, TViewModel viewModel) where TViewModel : class, IManagedWindow
    {
        if (_managedWindows.ContainsKey(key))
            return;

        Dispatcher.UIThread.Invoke(() =>
        {
            var window = new HostWindow
            {
                DataContext = viewModel,
                Width = viewModel.Width,
                Height = viewModel.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            window.Closed += (sender, e) =>
            {
                _managedWindows.Remove(key);
                if (window.DataContext is IDisposable disposable)
                    disposable.Dispose();
                window.DataContext = null;
            };

            _managedWindows.Add(key, window);
        });
    }

    public void Dispose()
    {
        foreach (var kvp in _managedWindows.ToList())
        {
            try
            {
                kvp.Value.Close();
                if (kvp.Value.DataContext is IDisposable disposable)
                    disposable.Dispose();
            }
            catch { }
        }
        _managedWindows.Clear();
        GC.SuppressFinalize(this);
    }
}
