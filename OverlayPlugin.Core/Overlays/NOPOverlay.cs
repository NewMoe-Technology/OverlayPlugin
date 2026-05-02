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
            this.name = name;
            this.id = id;
            this.url = url;
            this.overlayApi = overlayApi;
        }

        public NOPRenderer Renderer { get; internal set; } = new NOPRenderer();

        public bool Visible { get; internal set; } // TODO: set时需要给渲染进程发消息
        public bool IsClickThru { get; internal set; } // TODO: set时需要给渲染进程发消息
        public bool Locked { get; internal set; } // TODO: set时需要给渲染进程发消息
        public string Url { get; internal set; } // TODO: set时需要给渲染进程发消息

        public IntPtr Handle { get; internal set; } // ？？？得看看这个被用来做什么了，我记得好像只用于检测窗口是否完成了初始化。
        public FormStartPosition StartPosition { get; internal set; } // ？？？需要检查OverlayForm里有没有特殊处理，怀疑是.NET Form里的东西。
        public Point Location { get; internal set; } // ？？？这个和Size有什么区别？难道得和Size一起才能得出Rectangle？
        public string Text { get; internal set; } // TODO: set时需要给渲染进程发消息（应该是设置标题）
        public Size Size { get; internal set; } // TODO: set时需要给渲染进程发消息，get也需要从渲染进程获取
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
            Debug.WriteLine($"!!! NOPOverlayForOP.Close() called from:\n{new StackTrace(true)}");
            throw new NotImplementedException();
        }

        internal void Dispose()
        {
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

        public class NOPRenderer
        {
            public event EventHandler<BrowserErrorEventArgs> BrowserError; // ？？？需要检查这些被用来做什么了
            public event EventHandler<BrowserLoadEventArgs> BrowserStartLoading; // ？？？需要检查这些被用来做什么了
            public event EventHandler<BrowserLoadEventArgs> BrowserLoad; // ？？？需要检查这些被用来做什么了
            public event EventHandler<BrowserConsoleLogEventArgs> BrowserConsoleLog; // ？？？需要检查这些被用来做什么了

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
