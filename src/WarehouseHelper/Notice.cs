using System;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>游戏原生飘字，自动淡出，不使用声望对话气泡。</summary>
    public static class Notice
    {
        public static void Show(string text)
        {
            try
            {
                var renderer = RenderHandler.current;
                if (renderer == null || renderer.overlayRoot == null)
                {
                    WarehouseHelperMod.Warn("提示界面未初始化: " + text);
                    return;
                }
                SplashTextManager.Instance.DisplayFloatText(text, SplashTextManager.TextMode.Normal);
            }
            catch (Exception e) { WarehouseHelperMod.Warn("Notice.Show: " + e.Message + " | " + text); }
        }
    }
}
