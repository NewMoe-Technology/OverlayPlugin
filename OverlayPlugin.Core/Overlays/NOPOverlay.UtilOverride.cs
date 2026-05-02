using System.Drawing;
using System.Windows.Forms;
using RainbowMage.OverlayPlugin.DieMoe;

namespace RainbowMage.OverlayPlugin
{
    internal static partial class Util
    {
        // 原版只接受OverlayForm，我不想碰原版代码太多，所以重载放这了。
        public static bool IsOnScreen(NOPOverlayForOP overlay)
        {
            var screens = Screen.AllScreens;
            //var rect = overlay.GetRectangle();
            var rect = new Rectangle();
            foreach (Screen screen in screens)
            {
                var formRectangle = rect;
                if (screen.WorkingArea.IntersectsWith(formRectangle))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
