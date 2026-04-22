using Avalonia.Controls;

namespace Skua.Avalonia;

public partial class HostDialog : Window
{
    public bool? DialogResult { get; set; }

    public HostDialog()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
    }
}
