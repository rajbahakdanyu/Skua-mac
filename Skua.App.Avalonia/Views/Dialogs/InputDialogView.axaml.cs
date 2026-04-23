using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Skua.Avalonia;

namespace Skua.App.Avalonia.Views.Dialogs;

public partial class InputDialogView : UserControl
{
    public InputDialogView()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.FindAncestorOfType<Window>() is HostDialog dialog)
        {
            dialog.DialogResult = true;
            dialog.Close();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.FindAncestorOfType<Window>() is HostDialog dialog)
        {
            dialog.DialogResult = false;
            dialog.Close();
        }
    }
}
