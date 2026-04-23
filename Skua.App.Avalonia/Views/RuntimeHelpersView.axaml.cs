using Avalonia.Controls;

namespace Skua.App.Avalonia.Views;

public partial class RuntimeHelpersView : UserControl
{
    public RuntimeHelpersView()
    {
        InitializeComponent();
        SetIdsInvBtn.CommandParameter = false;
        SetIdsBankBtn.CommandParameter = true;
    }
}
