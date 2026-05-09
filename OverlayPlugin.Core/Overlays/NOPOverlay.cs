using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using Newtonsoft.Json.Linq;
using NoOverlayPlugin.JsonRpc;
using RainbowMage.HtmlRenderer;
using static Transitions.Transition;

namespace RainbowMage.OverlayPlugin.DieMoe
{
    public partial class NOPOverlayForOP
    {
        public static JsonRpcProcessor JsonRpcProcessor = new JsonRpcProcessor();

        private string name; // ？？？得看看OverlayForm拿这个做了什么
        private string id; // ？？？得看看OverlayForm拿这个做了什么
        private string url; // ？？？得看看OverlayForm拿这个做了什么
        private OverlayApi overlayApi; // ？？？得看看OverlayForm拿这个做了什么

        public NOPOverlayForOP(string name, string id, string url, int maxFrameRate, object overlayApi)
        {
            this.name = name; // 设置窗口标题，NOP -n 参数，以及提供NOP -i 参数，因为OverlayPlugin的id是加载时生成的，不可靠。
            this.id = id; // 虽然id仅在本次加载中一致，但可用于将悬浮窗实例与WS连接关联。
            this.url = url; // 设置窗口加载的URL，NOP -s 参数
            this.overlayApi = (OverlayApi)overlayApi; // 原版里的处理API操作的部分，需要想办法让NOP悬浮窗也能调用这个实例里的函数，暂定思路是通过WebSocket服务器和JSON RPC来桥接

            Renderer = new NOPRenderer(this, this.id, this.name, this.url);

            JsonRpcProcessor.AddMethod($"{this.id}:OverlayApi.callHandler", new Func<string, string>(data =>
            {
                Log.D($"{this}.JsonRpcProcessor received call to OverlayApi.callHandler with data: {data}");
                try
                {
                    var json = this.overlayApi.callHandler(data, null).GetAwaiter().GetResult();
                    return json;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"悬浮窗调用OverlayApi.callHandler时意外出错: {ex}");
                    return "null";
                }
            }));
            // 需要将实际抵达的URL传回去，因为原版悬浮窗支持通过重定向批量修改用户的旧URL。
            JsonRpcProcessor.AddMethod($"{this.id}:Renderer.FireOnceBrowserStartLoading", new Func<string, string>(overlayUrl =>
            {
                if (overlayUrl.Contains("://127.0.0.1") && overlayUrl != Url)
                {
                    Log.D($"{this}.BrowserStartLoading: 渲染进程错误返回了本地服务器的URL，已自动恢复为正确URL");
                    Renderer.FireOnceBrowserStartLoading(Url);
                }
                else
                {
                    Renderer.FireOnceBrowserStartLoading(overlayUrl);
                }
                return "null";
            }));
            JsonRpcProcessor.AddMethod($"{this.id}:Renderer.FireOnceBrowserLoad", new Func<string, string>(overlayUrl =>
            {
                if (overlayUrl.Contains("://127.0.0.1") && overlayUrl != Url)
                {
                    Log.D($"{this}.BrowserLoad: 渲染进程错误返回了本地服务器的URL，已自动恢复为正确URL.");
                    Renderer.FireOnceBrowserLoad(Url);
                }
                else
                {
                    Renderer.FireOnceBrowserLoad(overlayUrl);
                }
                return "null";
            }));
        }

        public NOPRenderer Renderer { get; internal set; }

        bool isVisible;
        bool isClickThru;
        bool isLocked;
        FormStartPosition startPosition;
        Point location;
        Size size;

        // ActiveWindowChangedHandler会在每次焦点窗口改变时设置所有悬浮窗的Visible
        public bool Visible { get => isVisible; internal set { if (isVisible != value) { Log.D($"{this}.set_Visible({value})"); } isVisible = value; } } // TODO: set时需要给渲染进程发消息
        public bool IsClickThru { get => isClickThru; internal set { Log.D($"{this}.set_IsClickThru({value})"); isClickThru = value; } } // TODO: set时需要给渲染进程发消息
        public bool Locked { get => isLocked; internal set { Log.D($"{this}.set_Locked({value})"); isLocked = value; } } // TODO: set时需要给渲染进程发消息
        public string Url { get => url; internal set { Log.D($"{this}.set_Url({value})"); url = value; } } // TODO: set时需要给渲染进程发消息

        public FormStartPosition StartPosition { get => startPosition; internal set { Log.D($"{this}.set_StartPosition({value})"); startPosition = value; } } // .NET FormStartPosition 枚举，原版 OverlayBase.cs:136 用来让系统选初始位置。NOP 不需要——Aardio 自己管定位，或以后再实现，例如传参什么的。
        public Point Location { get => location; internal set { Log.D($"{this}.set_Location({value})"); location = value; } } // 这个与Size合在一起才是完整的窗口Rectangle，TODO: set时需要给渲染进程发消息（应该是设置窗口位置）
        public Size Size { get => size; internal set { Log.D($"{this}.set_Size({value})"); size = value; } } // 这个与Location合在一起才是完整的窗口Rectangle，TODO: set时需要给渲染进程发消息（应该是设置窗口大小）
        public string Text { get => name; internal set => name = value; } // Form的Text属性，原版里用来设置窗口标题的，NOP的话应该也是设置窗口标题。目前不打算做太多功能，传参够用了。
        public int MaxFrameRate { get; internal set; } // 无动作，单纯为了兼容而存在

        // 我感觉EnsureOverlaysAreOverGame坏掉了，得验证一下，如果坏掉了那就不用支持了，干脆把那段代码注释掉。
        // 也有可能是我理解错了，可能那段代码在检查位于FFXIV游戏窗口后面的窗口。
        // TODO: get时需要从渲染进程获取，只能set一次。会被EnsureOverlaysAreOverGame用于强制悬浮窗置顶，所以需要支持这个功能。
        public IntPtr Handle => Renderer.Handle;

        internal void ClearFrame()
        {
            // 应该不需要实现，这个是原来用Form显示时用来清除窗口内容的。
            Log.D($"{this}.ClearFrame() called from:\n{new StackTrace(true)}");
        }

        internal void Close()
        {
            // 调用时是和Close一起的，不过可以考虑把关闭进程的逻辑放在这里，destructor里也调用一次以防万一。
            Log.D($"{this}.Close() called from:\n{new StackTrace(true)}");
            Renderer.EndRender();
        }

        internal void Dispose()
        {
            // 释放资源，应该没什么资源需要释放吧？调用时是和Close一起的，应该算多余的，因为原版的OverlayForm真的是一个控件。
            Log.D($"{this}.Dispose() called from:\n{new StackTrace(true)}");
        }

        internal void Reload()
        {
            // TODO: 需要给渲染进程发消息重新加载页面
            Log.D($"{this}.Reload() called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void SetAcceptFocus(bool accept)
        {
            if (Handle == IntPtr.Zero)
            {
                Log.D($"{this}.SetAcceptFocus(accept={accept}) called but Handle is not ready yet, called from:\n{new StackTrace(true)}");
                return;
            }

            // TODO: 需要给渲染进程发消息设置是否接受焦点
            // 先试试直接修改窗口样式，看看能不能实现不接受焦点的效果。
            const int WS_EX_NOACTIVATE = 0x08000000;
            Log.D($"{this}.SetAcceptFocus(accept={accept}) called from:\n{new StackTrace(true)}");
            int ex = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
            if (accept)
            {
                ex &= ~WS_EX_NOACTIVATE;
            }
            else
            {
                ex |= WS_EX_NOACTIVATE;
            }
            NativeMethods.SetWindowLongA(Handle, NativeMethods.GWL_EXSTYLE, (IntPtr)ex);
        }

        internal void Show()
        {
            Log.D($"{this}.Show");
            Visible = true;
        }

        public override string ToString() => $"NOP悬浮窗[{name}|Id={id}]";

        // 负责管理NOP悬浮窗进程
        public class NOPRenderer
        {
            // 打错误日志 
            public event EventHandler<BrowserErrorEventArgs> BrowserError;
            // 调 UnsubscribeAll() — 关键！ 页面切换时丢掉旧订阅，不然打开新页面后依然会收到旧的订阅的数据。OverlayBase.cs:L152
            // 还有PrepareWebsite，保存URL、重置zoom、清除ModernApi标志。MiniParseOverlay.cs:L95
            public event EventHandler<BrowserLoadEventArgs> BrowserStartLoading; // ？？？需要检查这些被用来做什么了
            // 调 NotifyOverlayState() — 发送窗口锁定状态给 JS。OverlayBase.cs:L157和L292
            public event EventHandler<BrowserLoadEventArgs> BrowserLoad; // ？？？需要检查这些被用来做什么了
            // 如果启用则在产生 JS console 日志时触发
            public event EventHandler<BrowserConsoleLogEventArgs> BrowserConsoleLog; // ？？？需要检查这些被用来做什么了

            Process _process;
            NOPOverlayForOP Overlay { get; }
            string Id { get; }
            string Name { get; }
            string Url { get; }

            public NOPRenderer(NOPOverlayForOP overlay, string id, string name, string url)
            {
                Overlay = overlay;
                Id = id;
                Name = name;
                Url = url;
            }

            internal void BeginRender()
            {
                Log.D($"{Overlay}.NOPRenderer.BeginRender() 启动渲染进程");

                // 拼命令行
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NOP", "Renderer")); // 确保目录存在
                var overlayConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NOP", "Renderer", $"{Id}.oss");
                var args = (Url.StartsWith("file:///") ? $"-d \"{Url.Substring(8)}\"" : $"-s \"{Url}\"") +
                    $" -n \"{Name}\"" +
                    $" -p {Process.GetCurrentProcess().Id}" +
                    $" -c \"{overlayConfigPath}\"" +
                    $" -i {Id} -h 127.0.0.1:10501" +
                    $" --devtools --esc --show-task-icon";

                Log.D($"{Overlay}.NOPRenderer.BeginRender() 启动渲染进程 {args}");
                NOPConnections.Reserve(Overlay.id);
                _process = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine("Plugins", "ACT.OverlayPlugin", "NOPOverlay.exe"),
                    Arguments = args,
                    UseShellExecute = false,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                });

                // 获取窗口句柄（等 MainWindowHandle 就绪）
                _process.WaitForInputIdle();
                Handle = _process.MainWindowHandle;

                // 监听进程退出
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;
            }

            private void OnProcessExited(object sender, EventArgs e)
            {
                Log.D($"{Overlay}.NOPRenderer.OnProcessExited() 渲染进程已退出");
                Handle = IntPtr.Zero;
                _process?.Dispose();
                _process = null;
            }

            public IntPtr Handle { get; private set; }

            internal void EndRender()
            {
                Log.D($"{Overlay}.NOPRenderer.EndRender() 停止渲染进程, called from:\n{new StackTrace(true)}");

                if (_process?.HasExited != true)
                {
                    _process?.Kill();
                }
            }

            internal void ExecuteScript(string script)
            {
                Log.D($"{Overlay}.NOPRenderer.执行JS(script={script.Substring(0, Math.Min(script?.Length ?? 0, 100))}...) called from:\n{new StackTrace(true)}");
                if (NOPConnections.TryGet(Overlay.id, out var conn) && conn != null)
                {
                    conn.Notify("Overlay.ExecuteScript", script);
                }
                else
                {
                    //this.scriptQueue.Add(script);
                }
            }

            internal Bitmap Screenshot()
            {
                // 不需要实现，很老的悬浮窗才会用，不打算支持。
                Log.D($"{Overlay}.NOPRenderer.Screenshot() called from:\n{new StackTrace(true)}");
                return new Bitmap(1, 1);
            }

            internal void SetMuted(bool v)
            {
                // TODO: 需要给渲染进程发消息设置静音
                Log.D($"{Overlay}.NOPRenderer.SetMuted(v={v}) called from:\n{new StackTrace(true)}");
            }

            internal void SetZoomLevel(double level)
            {
                // 不需要实现，以后可以考虑，数值的含义还没搞明白。
                Log.D($"{Overlay}.NOPRenderer.SetZoomLevel(v={level}) called from:\n{new StackTrace(true)}");
            }

            internal void showDevTools(bool open = true)
            {
                // 不需要实现，以后再看看，Aardio的WebView2封装刚好有一个showDevtoolsWindow函数可以用。
                Log.D($"{Overlay}.NOPRenderer.showDevTools(open={open}) called from:\n{new StackTrace(true)}");
            }

            bool firedBrowserStartLoading = false;
            bool firedBrowserLoad = false;
            internal void FireOnceBrowserStartLoading(string url)
            {
                if (firedBrowserStartLoading)
                {
                    Log.D($"{Overlay}.NOPRenderer.FireOnceBrowserStartLoading(url={url}) called but already fired once");
                    return;
                }
                Log.D($"{Overlay}.NOPRenderer.FireOnceBrowserStartLoading(url={url}) firing BrowserStartLoading event");
                BrowserStartLoading?.Invoke(this, new BrowserLoadEventArgs(0, url));
            }

            internal void FireOnceBrowserLoad(string url)
            {
                if (firedBrowserLoad)
                {
                    Log.D($"{Overlay}.NOPRenderer.FireOnceBrowserLoad(url={url}) called but already fired once");
                    return;
                }
                Log.D($"{Overlay}.NOPRenderer.FireOnceBrowserLoad(url={url}) firing BrowserLoad event");
                BrowserLoad?.Invoke(this, new BrowserLoadEventArgs(200, url));
            }
        }
    }

    internal static class NOPConnections
    {
        static Dictionary<string, WSServer.NOPConnectionHandler> ConnectionByName = new Dictionary<string, WSServer.NOPConnectionHandler>();
        static object lockConnectionByName = new object();

        internal static bool Reserve(string key)
        {
            lock (lockConnectionByName)
            {
                Log.D($"NOPConnections.Reserve({key}) 预定连接位置");
                if (!ConnectionByName.ContainsKey(key))
                {
                    Log.D($"NOPConnections.Reserve({key}) 预定成功");
                    ConnectionByName[key] = null;
                    return true;
                }
            }
            Log.D($"NOPConnections.Reserve({key}) 已经被预定了");
            return false;
        }

        internal static bool Unreserve(string key)
        {
            lock (lockConnectionByName)
            {
                Log.D($"NOPConnections.Unreserve({key}) 取消预定连接位置");
                if (ConnectionByName.ContainsKey(key))
                {
                    Log.D($"NOPConnections.Unreserve({key}) 取消预定成功");
                    ConnectionByName[key]?.Close();
                    ConnectionByName.Remove(key);
                    return true;
                }
            }
            Log.D($"NOPConnections.Unreserve({key}) 没有找到预定的连接");
            return false;
        }

        internal static bool TryAdd(string key, WSServer.NOPConnectionHandler conn)
        {
            lock (lockConnectionByName)
            {
                Log.D($"NOPConnections.TryAdd({key}) 尝试添加连接");
                if (ConnectionByName.ContainsKey(key) && ConnectionByName[key] is null)
                {
                    Log.D($"NOPConnections.TryAdd({key}) 添加成功");
                    ConnectionByName[key] = conn;
                    return true;
                }
            }
            Log.D($"NOPConnections.TryAdd({key}) 没有预定位置或者已经有连接了");
            return false;
        }

        internal static void Remove(string id, WSServer.NOPConnectionHandler conn)
        {
            lock (lockConnectionByName)
            {
                Log.D($"NOPConnections.Remove({id}) 尝试移除连接");
                if (ConnectionByName.TryGetValue(id, out var existing) && existing == conn)
                {
                    Log.D($"NOPConnections.Remove({id}) 移除成功");
                    ConnectionByName[id] = null;
                }
                else
                {
                    Log.D($"NOPConnections.Remove({id}) 没有找到匹配的连接");
                }
            }
        }

        internal static bool TryGet(string id, out WSServer.NOPConnectionHandler conn)
        {
            lock (lockConnectionByName)
            {
                return ConnectionByName.TryGetValue(id, out conn);
            }
        }
    }
}
