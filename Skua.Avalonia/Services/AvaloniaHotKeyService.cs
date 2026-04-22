using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;
using Skua.Core.Utils;
using Avalonia.Input;
using System.Collections.Specialized;
using System.Globalization;

namespace Skua.Avalonia.Services;

public class AvaloniaHotKeyService : IHotKeyService, IDisposable
{
    private readonly Dictionary<string, IRelayCommand> _hotKeys;
    private readonly ISettingsService _settingsService;
    private readonly IDecamelizer _decamelizer;
    private readonly List<KeyBinding> _registeredBindings = new();

    public AvaloniaHotKeyService(Dictionary<string, IRelayCommand> hotKeys, ISettingsService settingsService, IDecamelizer decamelizer)
    {
        _hotKeys = hotKeys;
        _settingsService = settingsService;
        _decamelizer = decamelizer;
    }

    public void Reload()
    {
        _registeredBindings.Clear();

        StringCollection? hotkeys = _settingsService.Get<StringCollection>("HotKeys");
        hotkeys ??= new StringCollection();

        EnsureAllBindingsExist(hotkeys);
        _settingsService.Set("HotKeys", hotkeys);

        foreach (string? hk in hotkeys)
        {
            if (string.IsNullOrEmpty(hk))
                continue;

            string[] split = hk.Split('|');
            if (_hotKeys.ContainsKey(split[0]))
            {
                if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
                    continue;

                var parsed = ParseGesture(split[1]);
                if (parsed is null)
                {
                    StrongReferenceMessenger.Default.Send<HotKeyErrorMessage>(new(split[0]));
                    continue;
                }

                var kb = new KeyBinding
                {
                    Gesture = parsed,
                    Command = _hotKeys[split[0]]
                };
                _registeredBindings.Add(kb);
            }
        }
    }

    public List<T> GetHotKeys<T>() where T : IHotKey, new()
    {
        StringCollection hotkeys = _settingsService.Get<StringCollection>("HotKeys") ?? new StringCollection();
        EnsureAllBindingsExist(hotkeys);
        _settingsService.Set("HotKeys", hotkeys);

        List<T> parsed = new();
        foreach (string hk in hotkeys)
        {
            if (string.IsNullOrEmpty(hk)) continue;
            string[] split = hk.Split('|');
            string gesture = split.Length > 1 ? split[1] : string.Empty;
            parsed.Add(new() { Binding = split[0], Title = _decamelizer.Decamelize(split[0], null), KeyGesture = gesture });
        }
        return parsed;
    }

    public HotKey? ParseToHotKey(string keyGesture)
    {
        var gesture = ParseGesture(keyGesture);
        if (gesture is null) return null;
        return new HotKey(
            gesture.Key.ToString(),
            gesture.KeyModifiers.HasFlag(KeyModifiers.Control),
            gesture.KeyModifiers.HasFlag(KeyModifiers.Alt),
            gesture.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    private KeyGesture? ParseGesture(string keyGesture)
    {
        string ksc = keyGesture.ToLower();
        KeyModifiers modifiers = KeyModifiers.None;

        if (ksc.Contains("alt")) modifiers |= KeyModifiers.Alt;
        if (ksc.Contains("shift")) modifiers |= KeyModifiers.Shift;
        if (ksc.Contains("ctrl") || ksc.Contains("ctl")) modifiers |= KeyModifiers.Control;

        string keyStr = ksc
            .Replace("+", "")
            .Replace("alt", "")
            .Replace("shift", "")
            .Replace("ctrl", "")
            .Replace("ctl", "")
            .Trim();

        keyStr = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(keyStr);

        if (string.IsNullOrEmpty(keyStr))
            return null;

        if (Enum.TryParse<Key>(keyStr, true, out var key) && key != Key.None)
            return new KeyGesture(key, modifiers);

        return null;
    }

    private void EnsureAllBindingsExist(StringCollection hotkeys)
    {
        HashSet<string> existing = new();
        HashSet<string> usedGestures = new(StringComparer.OrdinalIgnoreCase);
        foreach (string hk in hotkeys)
        {
            if (string.IsNullOrWhiteSpace(hk)) continue;
            string[] split = hk.Split('|');
            if (split.Length > 0 && !string.IsNullOrWhiteSpace(split[0]))
                existing.Add(split[0]);
            if (split.Length > 1 && !string.IsNullOrWhiteSpace(split[1]))
                usedGestures.Add(split[1]);
        }

        foreach (string key in _hotKeys.Keys)
        {
            if (existing.Contains(key)) continue;
            string gesture = string.Empty;
            if (string.Equals(key, "ToggleLagKiller", StringComparison.Ordinal) && !usedGestures.Contains("F6"))
                gesture = "F6";
            hotkeys.Add($"{key}|{gesture}");
        }
    }

    public void Dispose()
    {
        _registeredBindings.Clear();
        _hotKeys.Clear();
        GC.SuppressFinalize(this);
    }
}
