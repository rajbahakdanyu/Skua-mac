using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Flash;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Utils;
using System.Dynamic;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Skua.Avalonia.Services;

/// <summary>
/// Cross-platform Flash replacement using Ruffle (WebAssembly Flash Player).
/// Communicates with the SWF via a local HTTP bridge instead of COM ActiveX.
/// </summary>
public class RuffleFlashUtil : IFlashUtil
{
    private readonly IMessenger _messenger;
    private readonly Lazy<IScriptManager> _lazyManager;
    private RuffleBridge? _bridge;

    public int BridgePort => _bridge?.Port ?? 35921;

    public RuffleFlashUtil(IMessenger messenger, Lazy<IScriptManager> manager)
    {
        _messenger = messenger;
        _lazyManager = manager;
    }

    public event FlashCallHandler? FlashCall;

    public void InitializeFlash()
    {
        try
        {
            _bridge = new RuffleBridge();
            _bridge.FlashCall += OnFlashCall;
            _bridge.Start();

            _messenger.Send(new FlashChangedMessage<RuffleBridge>(_bridge));
        }
        catch (Exception ex)
        {
            _messenger.Send(new FlashErrorMessage(ex, "InitializeFlash", Array.Empty<object>()));
        }
    }

    private void OnFlashCall(string function, object[] args)
    {
        FlashCall?.Invoke(function, args);

        // Always respond to requestLoadGame with loadClient.
        // SkuaStartupHandler unsubscribes after the first call, so on browser
        // page refreshes the handler is gone. This ensures the game always loads.
        if (function == "requestLoadGame")
        {
            Task.Run(() =>
            {
                try { Call("loadClient"); }
                catch { }
            });
        }
    }

    public string? Call(string function, params object[] args)
    {
        return Call<string>(function, args);
    }

    public T? Call<T>(string function, params object[] args)
    {
        try
        {
            object? o = Call(function, typeof(T), args);
            return o is not null ? (T)o : (T)DefaultProvider.GetDefault<T>(typeof(T));
        }
        catch
        {
            return (T)DefaultProvider.GetDefault<T>(typeof(T));
        }
    }

    public object? Call(string function, Type type, params object[] args)
    {
        if (_lazyManager.Value.ShouldExit && Thread.CurrentThread.Name == "Script Thread")
            _lazyManager.Value.ScriptCts?.Token.ThrowIfCancellationRequested();
        try
        {
            StringBuilder req = new StringBuilder().Append($"<invoke name=\"{function}\" returntype=\"xml\">");
            if (args.Length > 0)
            {
                req.Append("<arguments>");
                args.ForEach(o => req.Append(ToFlashXml(o)));
                req.Append("</arguments>");
            }
            req.Append("</invoke>");

            string? result = _bridge?.CallFunction(req.ToString());
            if (string.IsNullOrEmpty(result)) return default;
            XElement el = XElement.Parse(result);
            return el.FirstNode is null ? default : Convert.ChangeType(el.FirstNode.ToString(), type);
        }
        catch (Exception e)
        {
            _messenger.Send(new FlashErrorMessage(e, function, args));
            return default;
        }
    }

    public static string ToFlashXml(object o)
    {
        switch (o)
        {
            case null:
                return "<null/>";
            case bool _:
                return $"<{o.ToString()!.ToLower()}/>";
            case double _:
            case float _:
            case long _:
            case int _:
                return $"<number>{o}</number>";
            case ExpandoObject _:
                StringBuilder sb = new StringBuilder().Append("<object>");
                foreach (var kvp in (o as IDictionary<string, object>)!)
                    sb.Append($"<property id=\"{kvp.Key}\">{ToFlashXml(kvp.Value)}</property>");
                return sb.Append("</object>").ToString();
            default:
                if (o is Array)
                {
                    StringBuilder _sb = new StringBuilder().Append("<array>");
                    int k = 0;
                    foreach (object el in (o as Array)!)
                        _sb.Append($"<property id=\"{k++}\">{ToFlashXml(el)}</property>");
                    return _sb.Append("</array>").ToString();
                }
                return $"<string>{SecurityElement.Escape(o.ToString())}</string>";
        }
    }

    public IFlashObject<T> CreateFlashObject<T>(string path)
    {
        return new FlashObject<T>(Call<int>("lnkCreate", path), this);
    }

    public object FromFlashXml(XElement el) => el.Name.ToString() switch
    {
        "number" => int.TryParse(el.Value, out int i) ? i : float.TryParse(el.Value, out float f) ? f : 0,
        "true" => true,
        "false" => false,
        "null" => null!,
        "array" => el.Elements().Select(e => FromFlashXml(e)).ToArray(),
        "object" => BuildExpandoFromXml(el),
        _ => el.Value
    };

    private object BuildExpandoFromXml(XElement el)
    {
        dynamic d = new ExpandoObject();
        foreach (var e in el.Elements())
        {
            var id = e.Attribute("id");
            if (id is not null && e.Elements().Any())
                ((IDictionary<string, object>)d)[id.Value] = FromFlashXml(e.Elements().First());
        }
        return d;
    }

    public void Dispose()
    {
        if (_bridge != null)
        {
            _bridge.FlashCall -= OnFlashCall;
            _bridge.Dispose();
            _bridge = null;
        }
        GC.SuppressFinalize(this);
    }
}
