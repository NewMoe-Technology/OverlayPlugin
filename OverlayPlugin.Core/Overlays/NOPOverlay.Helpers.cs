using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.Internals;

namespace RainbowMage.OverlayPlugin.DieMoe
{
    public partial class NOPOverlayForOP
    {
        class CallbackAdapter : IJavascriptCallback
        {
            Func<object[], object> callback;

            public CallbackAdapter(Func<object[], object> callback)
            {
                this.callback = callback;
            }

            public long Id => throw new NotImplementedException();

            public bool CanExecute => true;

            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true; // Nothing to dispose.

            public Task<JavascriptResponse> ExecuteAsync(params object[] parms)
            {

                var response = new JavascriptResponse();
                try
                {
                    var result = callback(parms);
                    response.Success = true;
                    response.Result = result;
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Message = $"Callback execution failed: {ex}";
                }
                return Task.FromResult(response);
            }

            public Task<JavascriptResponse> ExecuteWithTimeoutAsync(TimeSpan? timeout, params object[] parms) => ExecuteAsync(parms);
        }
    }

    internal static class Log
    {
        static TextWriter Writer;
        static bool Persist;

        static Log()
        {
            try
            {
                var filename = "ACT.OverlayPlugin.NOP";
                Persist = true;
                Writer = TextWriter.Synchronized(new StreamWriter(filename, true));
                WriteLine("==========================");
                WriteLine("******  日志文件头  ******");
                WriteLine("==========================");
            }
            catch (Exception ex)
            {
                Persist = false;
                MessageBox.Show($"初始化启动日志时出错！\n为了确保正常使用，将不会保存启动日志。\n\n错误信息:\n{ex}", "日志记录已被关闭", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void WriteLine(string strLine)
        {
            var datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var str = $"[{datetime}] {strLine}";

            Debug.WriteLine(strLine);

            if (Persist && Writer != null)
                try
                {
                    Writer.WriteLine(str);
                    Writer.Flush();
                }
                catch (Exception ex)
                {
                    Persist = false;
                    MessageBox.Show($"在写日志行时出错！\n为了确保正常使用，日志记录将被关闭。\n\n试图写入的日志行:\n{str}\n\n错误信息:\n{ex}", "日志记录已被关闭", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
        }

        internal static void D(string logLine)
        {
            WriteLine($"[详细] {logLine}");
        }

        internal static void I(string logLine)
        {
            WriteLine($"[信息] {logLine}");
        }

        internal static void W(string logLine)
        {
            WriteLine($"[警告] {logLine}");
        }

        internal static void W(string text, Exception ex)
        {
            WriteLine($"[警告] {text}\n{ex}");
        }

        internal static void W(string text, string dump)
        {
            WriteLine($"[警告] {text}\n{dump}");
        }

        /// <summary>
        /// 日志错误
        /// </summary>
        /// <param name="text">日志消息</param>
        /// <param name="ex">异常对象</param>
        internal static void E(string text, Exception ex)
        {
            WriteLine($"[错误] {text}\n{ex}");
        }

        internal static void E(string text)
        {
            WriteLine($"[错误] {text}");
        }

        internal static void F(string message, Exception ex = null, bool shoWriteroubleShooting = false, int errorCode = 0)
        {
            var logline = ex == null ? message : $"{message}\n{ex}";
            WriteLine($"[rua!] {logline}");
        }
    }
}
