using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Flash;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Utils;
using System.Collections.Concurrent;
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

    /// <summary>
    /// Short-lived cache for read-only Flash object reads (getGameObject / getGameObjectS).
    /// Multiple threads polling the same path within 50ms share one WebSocket round-trip.
    /// This prevents bridge saturation when the skill timer, script thread, and event
    /// handlers all read world.myAvatar.items / world.questTree / etc. simultaneously.
    /// </summary>
    private readonly ConcurrentDictionary<string, (string? value, long tick)> _readCache = new();
    private const long CacheTtlMs = 50;

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
            return o is not null ? (T)o : (T)DefaultProvider.GetDefault<T>(typeof(T))!;
        }
        catch
        {
            return (T)DefaultProvider.GetDefault<T>(typeof(T))!;
        }
    }

    /// <summary>
    /// Game function paths known to be read-only (safe to cache for 50ms).
    /// Side-effectful functions (acceptQuest, sendEquipItemRequest, etc.) must NOT be listed here.
    /// </summary>
    private static readonly HashSet<string> _readOnlyGameFunctions = new(StringComparer.Ordinal)
    {
        "world.isQuestInProgress",
        "world.getQuestValue",
        "world.getAchievement",
        "world.maximumQuestTurnIns",
        "world.myAvatar.getCPByID",
        "world.myAvatar.getRep",
        "world.myAvatar.pMC.artLoaded",
    };

    /// <summary>
    /// Direct call bindings known to be read-only.
    /// </summary>
    private static readonly HashSet<string> _readOnlyDirectCalls = new(StringComparer.Ordinal)
    {
        "canUseSkill",
        "isLoggedIn",
        "isTrue",
        "availableMonsters",
        "getMonsters",
        "getTargetMonster",
        "HasAnyActiveAura",
        "GetAurasValue",
        "GetPlayerAura",
        "GetMonsterAuraByName",
        "GetMonsterAuraByID",
        "gender",
    };

    public object? Call(string function, Type type, params object[] args)
    {
        if (_lazyManager.Value.ShouldExit && Thread.CurrentThread.Name == "Script Thread")
            _lazyManager.Value.ScriptCts?.Token.ThrowIfCancellationRequested();

        // Cache read-only Flash calls to reduce WebSocket round-trips.
        // Covers getGameObject/getGameObjectS/isNull (single path arg),
        // callGameFunction/callGameFunction0 with whitelisted read-only paths,
        // and direct bindings like canUseSkill.
        bool isCacheable = IsCacheableCall(function, args);

        if (isCacheable)
        {
            string cacheKey = BuildCacheKey(function, args);
            long now = Environment.TickCount64;
            if (_readCache.TryGetValue(cacheKey, out var cached) && now - cached.tick < CacheTtlMs)
            {
                // Return cached result
                try
                {
                    if (string.IsNullOrEmpty(cached.value)) return default;
                    XElement el = XElement.Parse(cached.value);
                    return el.FirstNode is null ? default : Convert.ChangeType(el.FirstNode.ToString(), type);
                }
                catch { return default; }
            }

            // Never block the Avalonia UI thread on Flash calls.
            // ObjectBinding properties (Health, Mana, Gold, etc.) are evaluated by data
            // bindings on the UI thread. With the WebSocket bridge these calls can take
            // 10-50ms (or 15s on timeout), freezing the entire UI. Instead return stale
            // cache or default; the script thread / timers keep the cache populated.
            if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                // Return stale cache of any age — stale data is better than frozen UI
                if (_readCache.TryGetValue(cacheKey, out var stale))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(stale.value)) return default;
                        XElement el = XElement.Parse(stale.value);
                        return el.FirstNode is null ? default : Convert.ChangeType(el.FirstNode.ToString(), type);
                    }
                    catch { return default; }
                }
                return default; // Cold cache — return default until background thread populates it
            }
        }

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

            // Pass the script CTS token so CallFunction unblocks immediately on script stop
            var token = _lazyManager.Value.ScriptCts?.Token ?? CancellationToken.None;
            string? result = _bridge?.CallFunction(req.ToString(), token);

            // Update cache for cacheable reads
            if (isCacheable)
            {
                string cacheKey = BuildCacheKey(function, args);
                _readCache[cacheKey] = (result, Environment.TickCount64);
            }

            if (string.IsNullOrEmpty(result)) return default;
            XElement el = XElement.Parse(result);
            return el.FirstNode is null ? default : Convert.ChangeType(el.FirstNode.ToString(), type);
        }
        catch (OperationCanceledException)
        {
            return default;
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

    /// <summary>
    /// Determines whether a Flash call is a read-only query that can be cached.
    /// </summary>
    private static bool IsCacheableCall(string function, object[] args)
    {
        // getGameObject / getGameObjectS / isNull with a single path arg
        if (args.Length == 1 && args[0] is string
            && (function == "getGameObject" || function == "getGameObjectS" || function == "isNull"))
            return true;

        // getGameObjectKey(path, key) / getArrayObject(path, index) — read-only with 2 args
        if (args.Length == 2 && args[0] is string
            && (function == "getGameObjectKey" || function == "getArrayObject"))
            return true;

        // selectArrayObjects(path, selector) — read-only with 2 string args
        if (args.Length == 2 && args[0] is string && args[1] is string
            && function == "selectArrayObjects")
            return true;

        // callGameFunction with a whitelisted read-only path (first arg is the path)
        if (function == "callGameFunction" && args.Length >= 1 && args[0] is string path
            && _readOnlyGameFunctions.Contains(path))
            return true;

        // callGameFunction0 with a whitelisted read-only path
        if (function == "callGameFunction0" && args.Length >= 1 && args[0] is string path0
            && _readOnlyGameFunctions.Contains(path0))
            return true;

        // Direct bindings like canUseSkill(index)
        if (_readOnlyDirectCalls.Contains(function))
            return true;

        return false;
    }

    /// <summary>
    /// Builds a cache key from function name and all arguments.
    /// </summary>
    private static string BuildCacheKey(string function, object[] args)
    {
        if (args.Length == 0) return function;
        if (args.Length == 1) return $"{function}|{args[0]}";
        // For multi-arg calls (e.g., callGameFunction("world.isQuestInProgress", 1234))
        var sb = new StringBuilder(function);
        for (int i = 0; i < args.Length; i++)
            sb.Append('|').Append(args[i]);
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_bridge != null)
        {
            _bridge.FlashCall -= OnFlashCall;
            _bridge.Dispose();
            _bridge = null;
        }
        _readCache.Clear();
        GC.SuppressFinalize(this);
    }
}
