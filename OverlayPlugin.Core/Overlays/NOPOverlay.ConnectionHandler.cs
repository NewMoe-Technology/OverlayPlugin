using System;
using Fleck;
using Newtonsoft.Json.Linq;
using NoOverlayPlugin.JsonRpc;
using RainbowMage.OverlayPlugin.DieMoe;

namespace RainbowMage.OverlayPlugin
{
    public partial class WSServer
    {
        class NOPConnectionHandler : IWSConnection
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
                        _dispatcher.UnsubscribeAll(this);
                        server._connections.Remove(this);
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
                if (path.StartsWith("/overlays/"))
                {
                    var overlayId = path.Substring("/overlays/".Length);
                    if (overlayId.Length > 0)
                    {
                        handler = new NOPConnectionHandler(overlayId, container.Resolve<ILogger>(), container.Resolve<EventDispatcher>(), conn, server);
                        return true;
                    }
                }
                handler = null;
                return false;
            }
        }
    }
}
