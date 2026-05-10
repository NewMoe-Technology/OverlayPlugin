using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NoOverlayPlugin.JsonRpc;
using RainbowMage.HtmlRenderer;

namespace RainbowMage.OverlayPlugin.DieMoe
{
    public partial class NOPOverlayForOP
    {
        public static JsonRpcProcessor JsonRpcProcessor = new JsonRpcProcessor();

        private string name; // 真的就只是名字而已
        private string id; // 一个没什么用的uuid，每次启动都是重新生成的，用来关联连接和实例还算够用。
        private string url; // 设置窗口加载的URL，NOP -s 参数
        private OverlayApi overlayApi; // 原版里被传入CefSharp绑定成为OverlayPluginApi的变量。

        internal WSServer.NOPConnectionHandler Connection { get; set; }

        internal void Connection_OnConnected(WSServer.NOPConnectionHandler conn)
        {
            if (Connection != null)
            {
                Log.W($"{this}.Connection_OnConnected: 上一个链接尚未断开，却收到了新的连接，已自动放弃上一个连接");
            }
            Connection = conn;
        }

        internal void Connection_OnDisconnected(WSServer.NOPConnectionHandler conn)
        {
            if (Connection == null)
            {
                Log.W($"{this}.Connection_OnDisconnected: 收到了连接断开的消息，但当前没有连接，已忽略");
            }
            Connection = null;
        }

        public NOPOverlayForOP(string name, string id, string url, int maxFrameRate, object overlayApi)
        {
            NOPOverlays.Add(id, this);
            this.name = name; // 设置窗口标题，NOP -n 参数，以及提供NOP -i 参数，因为OverlayPlugin的id是加载时生成的，不可靠。
            this.id = id; // 悬浮窗插件会为每个悬浮窗分配一个uuid并保存到文件，但他自己的设置保存代码太烂，ACT退出时可能无法正常保存导致状态丢失。
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

                // 赋值给自己来触发Notify，确保窗口状态符合保存的设置。
                Visible = Visible;
                IsClickThru = IsClickThru;
                Locked = Locked;

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

        public string Url
        {
            get => url;
            internal set
            {
                Log.D($"{this}.set_Url({value})");
                url = value;
                // TODO: file路径和http路径处理逻辑不一样，需要甄别，如果不一样则需要重启渲染进程。
                Connection?.Notify("Overlay.OpenUrl", url);
            }
        }
        public bool Visible
        {
            get => isVisible;
            internal set
            {
                if (isVisible != value)
                {
                    // 只在发生变化时打日志，因为ActiveWindowChangedHandler会在每次焦点窗口改变时设置所有悬浮窗的Visible。
                    Log.D($"{this}.set_Visible({value})");
                }
                isVisible = value;
                Connection?.Notify("Overlay.SetVisible", isVisible);
            }
        }
        public bool IsClickThru
        {
            get => isClickThru;
            internal set
            {
                Log.D($"{this}.set_IsClickThru({value})");
                isClickThru = value;
                Connection?.Notify("Overlay.SetClickthrough", isClickThru);
            }
        }
        public bool Locked
        {
            get => isLocked;
            internal set
            {
                Log.D($"{this}.set_Locked({value})");
                isLocked = value;
                Connection?.Notify("Overlay.SetWindowLocked", isLocked);
            }
        }

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
            Connection?.Notify("Overlay.Reload");
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
            bool _stopped; // 渲染进程意外退出后会自动重启，需要一个变量知道何时应该自动重启。
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
                Log.D($"{Overlay}.NOPRenderer.BeginRender() 开始渲染");
                _stopped = false;
                StartProcess();
            }

            void StartProcess()
            {
                Log.D($"{Overlay}.NOPRenderer.BeginRender() 启动渲染进程");

                // 拼命令行
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NOP", "Renderer")); // 确保目录存在
                var overlayConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NOP", "Renderer", $"{Id}.oss");
                var args = (Url.StartsWith("file:///") ? $"-d \"{Url.Substring(8)}\"" : $"-s \"{Url}\"") +
                    $" -n \"{Name}\"" +
                    $" -p {Process.GetCurrentProcess().Id}" +
                    $" -c \"{overlayConfigPath}\"" +
                    $" -i {Id} -h 127.0.0.1:10501";

#if DEBUG
                args += " --devtools --esc --show-task-icon --log console";
#endif

                Log.D($"{Overlay}.NOPRenderer.BeginRender() 启动渲染进程 {args}");
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

            void OnProcessExited(object sender, EventArgs e)
            {
                Log.D($"{Overlay}.NOPRenderer.OnProcessExited() 渲染进程已退出");
                Handle = IntPtr.Zero;
                _process?.Dispose();
                _process = null;

                if (!_stopped)
                {
                    Log.W($"{Overlay}.NOPRenderer.OnProcessExited() 渲染进程意外退出，正在重启...");
                    Task.Run(() =>
                    {
                        try
                        {
                            StartProcess();
                        }
                        catch (Exception ex)
                        {
                            Log.E($"{Overlay}.NOPRenderer.OnProcessExited() 重启渲染进程失败: {ex}");
                        }
                    });
                }
            }

            public IntPtr Handle { get; private set; }

            internal void EndRender()
            {
                Log.D($"{Overlay}.NOPRenderer.BeginRender() 停止渲染");

                _stopped = true;
                if (_process?.HasExited != true)
                {
                    _process?.Kill();
                }
            }

            internal void ExecuteScript(string script)
            {
                Log.D($"{Overlay}.NOPRenderer.执行JS(script={script.Substring(0, Math.Min(script?.Length ?? 0, 100))}...) called from:\n{new StackTrace(true)}");
                if (Overlay.Connection != null)
                {
                    Overlay.Connection.Notify("Overlay.ExecuteScript", script);
                }
                else
                {
                    // TODO: 连接还没有建立好，先把脚本放到队列里等连接建立好了再发。不过因为是本地连接，应该不会断掉吧。
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
                // 需要给渲染进程发消息设置静音
                Log.D($"{Overlay}.NOPRenderer.SetMuted(v={v})");
                Overlay.Connection?.Notify("Overlay.SetMuted", v);
            }

            internal void SetZoomLevel(double level)
            {
                // 不需要实现，以后可以考虑，数值的含义还没搞明白。
                Log.D($"{Overlay}.NOPRenderer.SetZoomLevel(v={level}) called from:\n{new StackTrace(true)}");
                Overlay.Connection?.Notify("Overlay.SetZoomLevel", level);
            }

            internal void showDevTools(bool open = true)
            {
                // 不需要实现，以后再看看，Aardio的WebView2封装刚好有一个showDevtoolsWindow函数可以用。
                Log.D($"{Overlay}.NOPRenderer.showDevTools(open={open})");
                Overlay.Connection?.Notify("Overlay.ShowDevTools");
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

    internal static class NOPOverlays
    {
        static Dictionary<string, NOPOverlayForOP> OverlayById = new Dictionary<string, NOPOverlayForOP>();
        static object lockOverlayById = new object();

        internal static bool Add(string key, NOPOverlayForOP overlay)
        {
            lock (lockOverlayById)
            {
                Log.D($"NOPOverlays.TryAdd({key}) 添加悬浮窗");
                if (!OverlayById.ContainsKey(key))
                {
                    Log.D($"NOPOverlays.TryAdd({key}) 添加成功");
                    OverlayById[key] = overlay;
                    return true;
                }
            }
            Log.W($"NOPOverlays.TryAdd({key}) 冲突！悬浮窗已存在。");
            return false;
        }

        internal static NOPOverlayForOP Get(string key)
        {
            lock (lockOverlayById)
            {
                if (OverlayById.TryGetValue(key, out var overlay))
                {
                    return overlay;
                }
                else
                {
                    Log.D($"NOPOverlays.Get({key}) 没有找到悬浮窗");
                    return null;
                }
            }
        }

        internal static bool TryGet(string key, out NOPOverlayForOP overlay)
        {
            lock (lockOverlayById)
            {
                if (OverlayById.TryGetValue(key, out overlay))
                {
                    return true;
                }
                else
                {
                    Log.D($"NOPOverlays.Get({key}) 没有找到悬浮窗");
                    overlay = null;
                    return false;
                }
            }
        }
    }
}
