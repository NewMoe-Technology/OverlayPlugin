using System;
using System.Linq;
using Fleck;
using Newtonsoft.Json.Linq;
using NoOverlayPlugin.JsonRpc;
using RainbowMage.OverlayPlugin.DieMoe;

namespace RainbowMage.OverlayPlugin
{
    public partial class WSServer
    {
        internal class NOPConnectionHandler : IWSConnection
        {
            public string Name => $"Overlay:{_overlayId}";
            private ILogger _logger;
            private EventDispatcher _dispatcher;
            private IWebSocketConnection _conn;
            private string _overlayId;
            //private JsonRpcProcessor _rpc;

            public NOPConnectionHandler(string overlayId, ILogger logger, EventDispatcher dispatcher, IWebSocketConnection conn, WSServer server)
            {
                _logger = logger;
                _overlayId = overlayId;
                _dispatcher = dispatcher;
                _conn = conn;

                _logger.Log(LogLevel.Info, $"Overlay {_overlayId}: connected");
                DieMoe.Log.D($"Overlay {_overlayId}: connected");

                var open = true;
                _conn.OnMessage = OnMessage;
                _conn.OnClose = () =>
                {
                    if (!open) return;
                    open = false;

                    try
                    {
                        // 使用NOPOverlayForOP的OverlayBase才需要unsubscribe all
                        // 不过OverlayBase会在BrowserStartLoading时UnsubscribeAll
                        // 这里主要是用以保持和WSServer的实现的一致性。
                        _dispatcher.UnsubscribeAll(this);
                        server._connections.Remove(this);
                        NOPOverlays.Get(overlayId)?.Connection_OnDisconnected(this);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"Failed to unsubscribe WebSocket connection: {ex}");
                    }
                };
                _conn.OnError = (ex) =>
                {
                    // Fleck will close the connection; make sure we always clean up even if Fleck doesn't call OnClose().
                    _conn.OnClose();

                    _logger.Log(LogLevel.Info, $"WebSocket connection was closed with error: {ex}");
                };
            }

            public void Close() => _conn.Close();

            public void Notify(string method, params object[] args)
            {
                var request = new JsonRpcRequest(method, args.Select(arg => JToken.FromObject(arg)).ToList());
                _conn.Send(request.Serialize());
            }

            public void HandleEvent(JObject e)
            {
                if (!_conn.IsAvailable)
                {
                    _logger.Log(LogLevel.Error, "A closed WebSocket connection wasn't cleaned up properly; fixing.");
                    _conn.OnClose();
                    return;
                }

                var notification = new JsonRpcRequest("__OverlayCallback", new[] { (JToken)e });
                DieMoe.Log.D($"Overlay {_overlayId}: Sending event: {notification.Serialize()}");
                _conn.Send(notification.Serialize());
            }

            public void OnMessage(string message)
            {
                DieMoe.Log.D($"Overlay {_overlayId}: Received message: {message}");

                try
                {
                    var response = NOPOverlayForOP.JsonRpcProcessor.Process(message);
                    DieMoe.Log.D($"Overlay {_overlayId}: Responding: {response.Serialize()}");
                    _conn.Send(response.Serialize());
                }
                catch (Exception ex)
                {
                    DieMoe.Log.W($"Overlay {_overlayId}: Failed to process JSON RPC message: {message}", ex);
                    _logger.Log(LogLevel.Warning, $"JSON RPC error: {ex.Message}");
                }
            }

            internal static bool TryHandle(TinyIoCContainer container, IWebSocketConnection conn, WSServer server, out NOPConnectionHandler handler)
            {
                var path = conn.ConnectionInfo.Path;

                // /overlays/{id} — WebView2 overlay connections (NOPOverlay.exe)
                if (path.StartsWith("/overlays/") && path.Length > "/overlays/".Length)
                {
                    var overlayId = path.Substring("/overlays/".Length);
                    handler = new NOPConnectionHandler(overlayId, container.Resolve<ILogger>(), container.Resolve<EventDispatcher>(), conn, server);
                    if (NOPOverlays.TryGet(overlayId, out var overlay))
                    {
                        overlay.Connection_OnConnected(handler);
                    }
                    else
                    {
                        handler.Close();
                    }
                    return true;
                }
                handler = null;
                return false;
            }
        }
    }
}
