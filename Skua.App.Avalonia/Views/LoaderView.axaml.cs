using Avalonia.Controls;

namespace Skua.App.Avalonia.Views;

public partial class LoaderView : UserControl
{
    public LoaderView()
    {
        InitializeComponent();
        UpdateAllBtn.CommandParameter = true;
    }
}
