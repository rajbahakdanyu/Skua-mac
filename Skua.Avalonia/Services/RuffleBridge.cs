using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Skua.Avalonia.Services;

/// <summary>
/// Bridge between .NET and Ruffle-hosted SWF via a local HTTPS server (Kestrel).
/// SWF → .NET: JS sends FlashCall XML over WebSocket command channel
/// .NET → SWF: .NET pushes calls over WebSocket, browser JS executes and sends result back
/// HTTPS is required so Ruffle allows SharedObject (game's charCount needs it).
/// </summary>
public class RuffleBridge : IDisposable, IComponent
{
    private WebApplication? _app;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingCalls = new();
    private static readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = true });
    private int _callId;
    private WebSocket? _commandSocket;
    private readonly SemaphoreSlim _wsSendLock = new(1, 1);

    public event Action<string, object[]>? FlashCall;
    public event EventHandler? Disposed;

    public ISite? Site { get; set; }

    public string GameUrl => $"https://localhost:{Port}/game.html";
    public int Port { get; private set; } = 35921;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        var cert = GenerateSelfSignedCert();

        // Find an available port
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                var builder = WebApplication.CreateSlimBuilder();
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(Port, listenOptions =>
                    {
                        listenOptions.UseHttps(cert);
                    });
                });
                builder.Logging.ClearProviders();

                _app = builder.Build();

                // CORS middleware
                _app.Use(async (context, next) =>
                {
                    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                    context.Response.Headers["Access-Control-Allow-Headers"] = "*";
                    if (context.Request.Method == "OPTIONS")
                    {
                        context.Response.StatusCode = 204;
                        return;
                    }
                    await next();
                });

                // WebSocket support for socket proxy
                _app.UseWebSockets();

                // WebSocket-to-TCP proxy for SmartFox game server connections
                // and WebSocket command channel for .NET↔SWF bridge
                _app.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        if (context.Request.Path == "/socket")
                        {
                            await HandleSocketProxy(context);
                            return;
                        }
                        if (context.Request.Path == "/bridge")
                        {
                            await HandleCommandChannel(context);
                            return;
                        }
                    }
                    await next(context);
                });

                MapEndpoints(_app);
                _app.StartAsync().Wait();
                break;
            }
            catch
            {
                Port++;
                _app?.DisposeAsync().AsTask().Wait();
                _app = null;
            }
        }

        if (_app is null)
            throw new InvalidOperationException("Could not find an available port for the Ruffle bridge.");
    }

    private void MapEndpoints(WebApplication app)
    {
        app.MapGet("/game.html", () => Results.Content(GetGameHtml(), "text/html; charset=utf-8"));

        app.MapGet("/skua.swf", async () =>
        {
            string swfPath = Path.Combine(AppContext.BaseDirectory, "skua.swf");
            if (File.Exists(swfPath))
            {
                byte[] swf = await File.ReadAllBytesAsync(swfPath);
                return Results.File(swf, "application/x-shockwave-flash");
            }
            return Results.NotFound();
        });

        app.Map("/proxy/{**url}", async (HttpContext ctx, string url) =>
        {
            // Reconstruct full URL
            string targetUrl = url;
            if (!string.IsNullOrEmpty(ctx.Request.QueryString.Value))
                targetUrl += ctx.Request.QueryString.Value;

            // Serve skua.swf locally (it doesn't exist on game.aq.com)
            if (targetUrl.Contains("skua.swf"))
            {
                string swfPath = Path.Combine(AppContext.BaseDirectory, "skua.swf");
                if (File.Exists(swfPath))
                {
                    byte[] swf = await File.ReadAllBytesAsync(swfPath);
                    return Results.File(swf, "application/x-shockwave-flash");
                }
                return Results.NotFound();
            }

            try
            {
                using var proxyReq = new HttpRequestMessage(
                    ctx.Request.Method == "POST" ? HttpMethod.Post : HttpMethod.Get,
                    targetUrl);
                proxyReq.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                proxyReq.Headers.Add("Accept", "*/*");
                proxyReq.Headers.Add("Accept-Language", "en-US,en;q=0.9");

                if (ctx.Request.Method == "POST")
                {
                    using var ms = new MemoryStream();
                    await ctx.Request.Body.CopyToAsync(ms);
                    if (ms.Length > 0)
                    {
                        proxyReq.Content = new ByteArrayContent(ms.ToArray());
                        if (ctx.Request.ContentType is not null)
                            proxyReq.Content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
                    }
                }

                using var proxyResp = await _httpClient.SendAsync(proxyReq);
                // Only log non-200 responses to reduce noise


                byte[] body = await proxyResp.Content.ReadAsByteArrayAsync();
                string contentType = proxyResp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                ctx.Response.StatusCode = (int)proxyResp.StatusCode;
                ctx.Response.ContentType = contentType;
                ctx.Response.ContentLength = body.Length;
                await ctx.Response.Body.WriteAsync(body);
                return Results.Empty;
            }
            catch
            {
                return Results.StatusCode(502);
            }
        });
    }

    private string GetGameHtml()
    {
        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8"/>
            <title>Skua - AQW</title>
            <style>
                html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: #000; }
                #container { width: 100%; height: 100%; }
            </style>
        </head>
        <body>
            <div id="container"></div>
            <script>
                function log(msg) {
                    console.log('[Skua]', msg);
                }

                // Prevent browser from throttling this tab when it's in the background.
                // A silent AudioContext keeps the event loop at full speed.
                try {
                    const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
                    const osc = audioCtx.createOscillator();
                    const gain = audioCtx.createGain();
                    gain.gain.value = 0;
                    osc.connect(gain);
                    gain.connect(audioCtx.destination);
                    osc.start();
                    document.addEventListener('click', () => audioCtx.resume(), { once: true });
                } catch(e) { }

                // Proxy override: intercept ALL external fetch/XHR through our local server
                const cdnHosts = ['unpkg.com', 'cdn.jsdelivr.net', 'cdnjs.cloudflare.com'];
                function shouldProxy(url) {
                    try {
                        const parsed = new URL(url, location.origin);
                        if (parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1') return false;
                        if (cdnHosts.some(h => parsed.hostname.endsWith(h))) return false;
                        return parsed.protocol === 'https:' || parsed.protocol === 'http:';
                    } catch(e) { return false; }
                }

                const _origFetch = window.fetch.bind(window);
                window.fetch = function(input, init) {
                    let url = (input instanceof Request) ? input.url : String(input);
                    if (shouldProxy(url)) {
                        const proxied = '/proxy/' + url;
                        if (input instanceof Request) {
                            const opts = {
                                method: input.method,
                                headers: input.headers,
                                body: input.body,
                                mode: 'cors',
                                credentials: 'omit'
                            };
                            if (input.body instanceof ReadableStream) {
                                opts.duplex = 'half';
                            }
                            input = new Request(proxied, opts);
                        } else {
                            input = proxied;
                        }
                    }
                    return _origFetch(input, init);
                };

                const _origXHROpen = XMLHttpRequest.prototype.open;
                XMLHttpRequest.prototype.open = function(method, url, ...rest) {
                    if (typeof url === 'string' && shouldProxy(url)) {
                        url = '/proxy/' + url;
                    }
                    return _origXHROpen.call(this, method, url, ...rest);
                };
            </script>
            <script src="https://unpkg.com/@ruffle-rs/ruffle"></script>
            <script>
                function argsToXml(val) {
                    if (val === null || val === undefined) return '<null/>';
                    if (val === true) return '<true/>';
                    if (val === false) return '<false/>';
                    if (typeof val === 'number') return '<number>' + val + '</number>';
                    if (typeof val === 'string') return '<string>' + escapeXml(val) + '</string>';
                    if (Array.isArray(val)) return val.map(v => argsToXml(v)).join('');
                    return '<string>' + escapeXml(String(val)) + '</string>';
                }

                function escapeXml(s) {
                    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
                }

                // WebSocket command channel (replaces HTTP polling)
                let bridgeWs = null;
                let ruffleInstance = null;

                function connectBridge() {
                    bridgeWs = new WebSocket('wss://localhost:' + location.port + '/bridge');
                    bridgeWs.onopen = () => console.log('[Skua] Bridge WS connected');
                    bridgeWs.onclose = () => { console.log('[Skua] Bridge WS closed'); setTimeout(connectBridge, 500); };
                    bridgeWs.onerror = () => {};

                    // Process calls directly in onmessage — no setTimeout (throttled in background tabs).
                    // With .NET serialization removed, calls arrive one at a time or in small bursts.
                    bridgeWs.onmessage = (evt) => {
                        try {
                            const call = JSON.parse(evt.data);
                            let result = undefined;
                            try {
                                const args = call.args || [];
                                result = ruffleInstance.callExternalInterface(call.name, ...args);
                            } catch(e) {
                                console.warn('.NET->SWF error:', call.name, e);
                            }
                            let xmlResult = '<null/>';
                            if (result !== undefined && result !== null) {
                                if (typeof result === 'string') xmlResult = '<string>' + escapeXml(result) + '</string>';
                                else if (typeof result === 'number') xmlResult = '<number>' + result + '</number>';
                                else if (typeof result === 'boolean') xmlResult = result ? '<true/>' : '<false/>';
                                else xmlResult = '<string>' + escapeXml(String(result)) + '</string>';
                            }
                            bridgeWs.send(JSON.stringify({ id: call.id, result: xmlResult }));
                        } catch(e) { console.warn('[Skua] Bridge msg error:', e); }
                    };
                }
                connectBridge();

                function makeFlashCallForwarder(name) {
                    return function() {
                        const args = Array.from(arguments);
                        let flatArgs = args;
                        if (args.length === 1 && Array.isArray(args[0])) flatArgs = args[0];
                        const xmlArgs = flatArgs.map(a => argsToXml(a)).join('');
                        const xml = '<invoke name="' + name + '" returntype="xml"><arguments>' + xmlArgs + '</arguments></invoke>';
                        if (bridgeWs && bridgeWs.readyState === 1) {
                            bridgeWs.send(JSON.stringify({ type: 'flashcall', xml: xml }));
                        }
                    };
                }

                const knownCalls = [
                    'requestLoadGame', 'debug', 'pre-load', 'loaded', 'pext',
                    'openWebsite', 'cycleComplete', 'cycleCompleteS'
                ];
                knownCalls.forEach(name => { window[name] = makeFlashCallForwarder(name); });

                const ruffle = window.RufflePlayer.newest();
                const player = ruffle.createPlayer();
                document.getElementById('container').appendChild(player);
                player.style.width = '100%';
                player.style.height = '100%';

                ruffleInstance = player.ruffle();

                // Fetch server list and build socketProxy config before loading SWF
                async function initGame() {
                    let socketProxy = [];
                    try {
                        const resp = await fetch('/proxy/https://game.aq.com/game/api/data/servers');
                        if (resp.ok) {
                            const servers = await resp.json();
                            const seen = new Set();
                            for (const s of servers) {
                                const ip = s.sIP;
                                const port = s.iPort || 5588;
                                const key = ip + ':' + port;
                                if (!seen.has(key)) {
                                    seen.add(key);
                                    socketProxy.push({
                                        host: ip,
                                        port: port,
                                        proxyUrl: 'wss://localhost:' + location.port + '/socket?host=' + encodeURIComponent(ip) + '&port=' + port
                                    });
                                }
                                // Also add common SmartFox port 5588 if different
                                if (port !== 5588) {
                                    const key2 = ip + ':5588';
                                    if (!seen.has(key2)) {
                                        seen.add(key2);
                                        socketProxy.push({
                                            host: ip,
                                            port: 5588,
                                            proxyUrl: 'wss://localhost:' + location.port + '/socket?host=' + encodeURIComponent(ip) + '&port=5588'
                                        });
                                    }
                                }
                            }
                        }
                    } catch(e) {
                        console.warn('[Skua] Server list error:', e);
                    }

                    // Load skua.swf with socket proxy configured
                    try {
                        await ruffleInstance.load({
                            url: '/skua.swf',
                            allowScriptAccess: true,
                            allowNetworking: 'all',
                            autoplay: 'on',
                            logLevel: 'warn',
                            parameters: {},
                            socketProxy: socketProxy,
                            defaultFonts: {
                                sansSerif: { name: 'Arial' },
                                serif: { name: 'Georgia' },
                                typewriter: { name: 'Courier New' },
                            }
                        });
                        log('SWF loaded OK');
                    } catch(e) {
                        console.error('[Skua] SWF LOAD ERROR:', e);
                    }
                }
                initGame();

                player.addEventListener('loadeddata', () => {
                    console.log('[Skua] SWF fully loaded');
                });
            </script>
        </body>
        </html>
        """;
    }

    private object ParseFlashArg(XElement el)
    {
        return el.Name.ToString() switch
        {
            "number" => int.TryParse(el.Value, out int i) ? i : float.TryParse(el.Value, out float f) ? f : 0,
            "true" => true,
            "false" => false,
            "null" => null!,
            "string" => el.Value,
            _ => el.Value
        };
    }

    public string? CallFunction(string invokeXml)
    {
        string name;
        object[] args;
        try
        {
            XElement el = XElement.Parse(invokeXml);
            name = el.Attribute("name")!.Value;
            var argsElement = el.Element("arguments");
            args = argsElement?.Elements().Select(x => ParseFlashArg(x)).ToArray() ?? Array.Empty<object>();
        }
        catch
        {
            return null;
        }

        string id = Interlocked.Increment(ref _callId).ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCalls[id] = tcs;

        // Push command over WebSocket
        try
        {
            var msg = JsonSerializer.Serialize(new { id, name, args });
            var bytes = Encoding.UTF8.GetBytes(msg);
            if (_commandSocket is { State: WebSocketState.Open } ws)
            {
                _wsSendLock.Wait();
                try
                {
                    ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                       .Wait(TimeSpan.FromSeconds(2));
                }
                finally { _wsSendLock.Release(); }
            }
            else
            {
                _pendingCalls.TryRemove(id, out _);
                return null;
            }
        }
        catch
        {
            _pendingCalls.TryRemove(id, out _);
            return null;
        }

        try
        {
            if (tcs.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                return tcs.Task.Result;
            }
        }
        catch { }
        finally
        {
            _pendingCalls.TryRemove(id, out _);
        }

        return null;
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(System.Net.IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());

        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));

        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
#pragma warning disable SYSLIB0057
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
#pragma warning restore SYSLIB0057
    }

    private async Task HandleCommandChannel(HttpContext context)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        _commandSocket = ws;

        // Run the receive loop on a dedicated thread so it is never starved
        // by thread-pool threads blocked in CallFunction .Wait() calls.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                ReceiveLoop(ws);
            }
            finally
            {
                if (_commandSocket == ws) _commandSocket = null;
                done.TrySetResult();
            }
        })
        {
            IsBackground = true,
            Name = "BridgeReceive"
        };
        thread.Start();
        await done.Task;
    }

    private void ReceiveLoop(WebSocket ws)
    {
        var buffer = new byte[65536];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                // Accumulate full message (may be fragmented across multiple frames)
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                        .GetAwaiter().GetResult();
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (ms.Length == 0)
                    continue;

                var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                try
                {
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "flashcall")
                    {
                        // SWF → .NET flash call via WebSocket — dispatch to thread pool
                        string xml = root.GetProperty("xml").GetString()!;
                        var el = XElement.Parse(xml);
                        string function = el.Attribute("name")!.Value;
                        var argsElement = el.Element("arguments");
                        object[] args = argsElement?.Elements().Select(x => ParseFlashArg(x)).ToArray() ?? Array.Empty<object>();
                        ThreadPool.QueueUserWorkItem(_ => FlashCall?.Invoke(function, args));
                    }
                    else if (root.TryGetProperty("id", out var idEl))
                    {
                        // Call result from browser — complete the pending call
                        string id = idEl.GetString()!;
                        string callResult = root.GetProperty("result").GetString()!;
                        if (_pendingCalls.TryRemove(id, out var tcs))
                            tcs.TrySetResult(callResult);
                    }
                }
                catch { }
            }
        }
        catch (WebSocketException) { }
    }

    private static async Task HandleSocketProxy(HttpContext context)
    {
        var host = context.Request.Query["host"].ToString();
        if (string.IsNullOrEmpty(host) || !int.TryParse(context.Request.Query["port"], out int port))
        {
            context.Response.StatusCode = 400;
            return;
        }



        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        using var tcp = new TcpClient();

        try
        {
            await tcp.ConnectAsync(host, port);
            var stream = tcp.GetStream();

            using var cts = new CancellationTokenSource();

            // WebSocket → TCP
            var wsToTcp = Task.Run(async () =>
            {
                var buffer = new byte[8192];
                try
                {
                    while (!cts.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                            break;
                        if (result.Count > 0)
                        {
                            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cts.Token);
                            await stream.FlushAsync(cts.Token);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (WebSocketException) { }
                catch { }
                finally
                {
                    await cts.CancelAsync();
                }
            });

            // TCP → WebSocket
            var tcpToWs = Task.Run(async () =>
            {
                var buffer = new byte[8192];
                try
                {
                    while (!cts.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
                    {
                        int read = await stream.ReadAsync(buffer.AsMemory(), cts.Token);
                        if (read <= 0) break;
                        await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read),
                            WebSocketMessageType.Binary, true, cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (WebSocketException) { }
                catch { }
                finally
                {
                    await cts.CancelAsync();
                }
            });

            await Task.WhenAny(wsToTcp, tcpToWs);
            await cts.CancelAsync();
        }
        catch { }

        if (ws.State == WebSocketState.Open)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "connection closed", CancellationToken.None);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _app?.StopAsync().Wait(TimeSpan.FromSeconds(5));
        _app?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        _cts?.Dispose();
        Disposed?.Invoke(this, EventArgs.Empty);
        GC.SuppressFinalize(this);
    }
}
