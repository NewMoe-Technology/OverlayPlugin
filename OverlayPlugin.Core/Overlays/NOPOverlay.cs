using System;
using System.Drawing;
using System.Windows.Forms;
using RainbowMage.HtmlRenderer;

namespace RainbowMage.OverlayPlugin.DieMoe
{
    public partial class NOPOverlayForOP
    {
        private string name;
        private string v;
        private string url;
        private int maxFrameRate;
        private object overlayApi;

        public NOPOverlayForOP(string name, string v, string url, int maxFrameRate, object overlayApi)
        {
            this.name = name;
            this.v = v;
            this.url = url;
            this.maxFrameRate = maxFrameRate;
            this.overlayApi = overlayApi;
        }

        public NOPRenderer Renderer { get; internal set; }
        public bool Visible { get; internal set; }
        public IntPtr Handle { get; internal set; }
        public FormStartPosition StartPosition { get; internal set; }
        public Point Location { get; internal set; }
        public string Text { get; internal set; }
        public Size Size { get; internal set; }
        public bool IsClickThru { get; internal set; }
        public bool Locked { get; internal set; }
        public int MaxFrameRate { get; internal set; }
        public string Url { get; internal set; }

        internal void ClearFrame()
        {
            throw new NotImplementedException();
        }

        internal void Close()
        {
            throw new NotImplementedException();
        }

        internal void Dispose()
        {
            throw new NotImplementedException();
        }

        internal void Reload()
        {
            throw new NotImplementedException();
        }

        internal void SetAcceptFocus(bool accept)
        {
            throw new NotImplementedException();
        }

        internal void Show()
        {
            throw new NotImplementedException();
        }

        public class NOPRenderer
        {
            public event EventHandler<BrowserErrorEventArgs> BrowserError;
            public event EventHandler<BrowserLoadEventArgs> BrowserStartLoading;
            public event EventHandler<BrowserLoadEventArgs> BrowserLoad;
            public event EventHandler<BrowserConsoleLogEventArgs> BrowserConsoleLog;

            internal void BeginRender()
            {
                throw new NotImplementedException();
            }

            internal void EndRender()
            {
                throw new NotImplementedException();
            }

            internal void ExecuteScript(string script)
            {
                throw new NotImplementedException();
            }

            internal Bitmap Screenshot()
            {
                throw new NotImplementedException();
            }

            internal void SetMuted(bool v)
            {
                throw new NotImplementedException();
            }

            internal void SetZoomLevel(double v)
            {
                throw new NotImplementedException();
            }

            internal void showDevTools(bool open = true)
            {
                throw new NotImplementedException();
            }
        }
    }
}
