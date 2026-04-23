using Avalonia.Controls;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.ViewModels;

namespace Skua.App.Avalonia;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
        DataContext = Ioc.Default.GetRequiredService<MainMenuViewModel>();
    }
}
