using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Skua.Avalonia;
using Skua.Core.ViewModels;

namespace Skua.App.Avalonia.Views.Dialogs;

public partial class AssignHotKeyDialogView : UserControl
{
    private const string WaitingInputText = "Waiting input...";
    private const string CaptureHintText = "Press a non-modifier key (Esc to cancel).";
    private const string ModifierOnlyHintText = "Modifier keys cannot be used alone. Press another key.";
    private const string SaveWithoutKeyHintText = "Press a non-modifier key before saving.";

    private string _backupKey = string.Empty;
    private bool _capturing;

    public AssignHotKeyDialogView()
    {
        InitializeComponent();
    }

    private AssignHotKeyDialogViewModel? VM => DataContext as AssignHotKeyDialogViewModel;

    private void AssignKey_Click(object? sender, RoutedEventArgs e)
    {
        if (VM is null) return;
        _backupKey = VM.KeyInput;
        VM.KeyInput = WaitingInputText;
        VM.InputHint = CaptureHintText;
        _capturing = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_capturing || VM is null)
        {
            base.OnKeyDown(e);
            return;
        }

        var key = e.Key;

        if (key == Key.Escape)
        {
            VM.KeyInput = _backupKey;
            VM.InputHint = string.Empty;
            _capturing = false;
            e.Handled = true;
            return;
        }

        if (IsModifierKey(key))
        {
            VM.InputHint = ModifierOnlyHintText;
            e.Handled = true;
            return;
        }

        VM.KeyInput = key.ToString();
        VM.InputHint = string.Empty;
        _capturing = false;
        e.Handled = true;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (VM is null) return;

        if (string.Equals(VM.KeyInput, WaitingInputText))
        {
            VM.KeyInput = _backupKey;
            VM.InputHint = SaveWithoutKeyHintText;
            _capturing = false;
            return;
        }

        if (IsModifierKeyInput(VM.KeyInput))
        {
            VM.InputHint = ModifierOnlyHintText;
            return;
        }

        VM.InputHint = string.Empty;
        _capturing = false;

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

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt;

    private static bool IsModifierKeyInput(string keyInput) =>
        keyInput is "LeftCtrl" or "RightCtrl" or "LeftShift" or "RightShift" or "LeftAlt" or "RightAlt";
}
