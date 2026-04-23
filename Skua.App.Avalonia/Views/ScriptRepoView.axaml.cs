using Avalonia.Controls;

namespace Skua.App.Avalonia.Views;

public partial class ScriptRepoView : UserControl
{
    public ScriptRepoView()
    {
        InitializeComponent();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // TODO: implement filtering on Scripts collection
    }
}
