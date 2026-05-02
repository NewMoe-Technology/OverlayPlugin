using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using RainbowMage.HtmlRenderer;

namespace RainbowMage.OverlayPlugin.DieMoe
{
    public partial class NOPOverlayForOP
    {
        private string name; // ？？？得看看OverlayForm拿这个做了什么
        private string id; // ？？？得看看OverlayForm拿这个做了什么
        private string url; // ？？？得看看OverlayForm拿这个做了什么
        private object overlayApi; // ？？？得看看OverlayForm拿这个做了什么

        public NOPOverlayForOP(string name, string id, string url, int maxFrameRate, object overlayApi)
        {
            this.name = name; // 设置窗口标题，NOP -n 参数
            this.id = id; // 设置窗口ID，NOP -i 参数
            this.url = url; // 设置窗口加载的URL，NOP -s 参数
            this.overlayApi = overlayApi; // 原版里的处理API操作的部分，需要想办法让NOP悬浮窗也能调用这个实例里的函数，暂定思路是通过WebSocket服务器和JSON RPC来桥接
        }

        public NOPRenderer Renderer { get; internal set; } = new NOPRenderer();

        public bool Visible { get; internal set; } // TODO: set时需要给渲染进程发消息
        public bool IsClickThru { get; internal set; } // TODO: set时需要给渲染进程发消息
        public bool Locked { get; internal set; } // TODO: set时需要给渲染进程发消息
        public string Url { get; internal set; } // TODO: set时需要给渲染进程发消息

        // 我感觉EnsureOverlaysAreOverGame坏掉了，得验证一下，如果坏掉了那就不用支持了，干脆把那段代码注释掉。
        // 也有可能是我理解错了，可能那段代码在检查位于FFXIV游戏窗口后面的窗口。
        public IntPtr Handle { get; internal set; } // TODO: get时需要从渲染进程获取，只能set一次。会被EnsureOverlaysAreOverGame用于强制悬浮窗置顶，所以需要支持这个功能。
        public FormStartPosition StartPosition { get; internal set; } // .NET FormStartPosition 枚举，原版 OverlayBase.cs:136 用来让系统选初始位置。NOP 不需要——Aardio 自己管定位，或以后再实现，例如传参什么的。
        public Point Location { get; internal set; } // 这个与Size合在一起才是完整的窗口Rectangle，TODO: set时需要给渲染进程发消息（应该是设置窗口位置）
        public Size Size { get; internal set; } // 这个与Location合在一起才是完整的窗口Rectangle，TODO: set时需要给渲染进程发消息（应该是设置窗口大小）
        public string Text { get; internal set; } // TODO: set时需要给渲染进程发消息（应该是设置标题），或者只传参也可以？到时候看看
        public int MaxFrameRate { get; internal set; } // 无动作

        internal void ClearFrame()
        {
            // 应该不需要实现，这个是原来用Form显示时用来清除窗口内容的。
            Debug.WriteLine($"!!! NOPOverlayForOP.ClearFrame() called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void Close()
        {
            // TODO: 需要给渲染进程发消息关闭窗口
            // 调用时是和Close一起的，不过可以考虑把关闭进程的逻辑放在这里，destructor里也调用一次以防万一。
            Debug.WriteLine($"!!! NOPOverlayForOP.Close() called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void Dispose()
        {
            // 释放资源，应该没什么资源需要释放吧？调用时是和Close一起的，应该算多余的，因为原版的OverlayForm真的是一个控件。
            Debug.WriteLine($"!!! NOPOverlayForOP.Dispose() called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void Reload()
        {
            // TODO: 需要给渲染进程发消息重新加载页面
            Debug.WriteLine($"!!! NOPOverlayForOP.Reload() called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void SetAcceptFocus(bool accept)
        {
            // TODO: 需要给渲染进程发消息设置是否接受焦点
            Debug.WriteLine($"!!! NOPOverlayForOP.SetAcceptFocus(accept={accept}) called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void Show()
        {
            Debug.WriteLine("!!! NOPOverlayForOP.Show");
            Visible = true;
        }

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

            // 思路
            // BrowserStartLoading 应该在 Aardio 导航时触发。
            // BrowserLoad 在 WebView2 加载完成时触发。


            internal void BeginRender()
            {
                // TODO: 启动渲染进程
                Debug.WriteLine("!!! NOPRenderer.BeginRender() 启动渲染进程");
            }

            internal void EndRender()
            {
                // TODO: 停止渲染进程
                Debug.WriteLine($"!!! NOPRenderer.EndRender() 停止渲染进程, called from:\n{new StackTrace(true)}");
                throw new NotImplementedException();
            }

            internal void ExecuteScript(string script)
            {
                // TODO: 需要给渲染进程发消息执行脚本
                Debug.WriteLine($"!!! NOPRenderer.ExecuteScript(script={script.Substring(0, Math.Min(script?.Length ?? 0, 100))}...) called from:\n{new StackTrace(true)}");
                throw new NotImplementedException();
            }

            internal Bitmap Screenshot()
            {
                // 不需要实现，很老的悬浮窗才会用，不打算支持。
                Debug.WriteLine($"!!! NOPRenderer.Screenshot() called from:\n{new StackTrace(true)}");
                throw new NotImplementedException();
            }

            internal void SetMuted(bool v)
            {
                // TODO: 需要给渲染进程发消息设置静音
                Debug.WriteLine($"!!! NOPRenderer.SetMuted(v={v}) called from:\n{new StackTrace(true)}");
                throw new NotImplementedException();
            }

            internal void SetZoomLevel(double v)
            {
                // 不需要实现，以后可以考虑
                Debug.WriteLine($"!!! NOPRenderer.SetZoomLevel(v={v}) called from:\n{new StackTrace(true)}");
                throw new NotImplementedException();
            }

            internal void showDevTools(bool open = true)
            {
                // 不需要实现，虽然很想要但似乎有点难度，以后再看看。
                Debug.WriteLine($"!!! NOPRenderer.showDevTools(open={open}) called from:\n{new StackTrace(true)}");
                throw new NotImplementedException();
            }
        }
    }
}
