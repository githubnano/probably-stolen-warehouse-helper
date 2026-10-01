using System;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>
    /// tooltip 末尾追加自定义行。
    /// 钩点与配色照抄 Kitchen mod:Harmony patch GameItemElement.GetTooltipBasic 后缀,
    /// 文字用 AddLine + ColorPalette.None(继承默认字色/无背景)。
    /// </summary>
    public static class TooltipTail
    {
        public static readonly RenderHandler.ColorPalette None = RenderHandler.ColorPalette.None;

        /// <summary>追加一行,使用游戏 tooltip 默认字色。</summary>
        public static void Line(RichTextBuilder builder, string text, bool bold = false, bool italic = false)
        {
            try { builder?.AddLine(text, false, None, bold, italic, false, false, None, None, None); }
            catch { }
        }

        [HarmonyLib.HarmonyPatch(typeof(GameItemElement), "GetTooltipBasic")]
        public static class TooltipBasicPatch
        {
            public static void Prefix(GameItemElement __instance)
            {
                // Refresh before the native description is read, including existing save
                // items and modifier settings changed since this helper was created.
                try { Items.RefreshLocalizedText(__instance); }
                catch { }
            }

            public static void Postfix(GameItemElement __instance, ref RichTextBuilder __result)
            {
                try
                {
                    if (__instance == null || __result == null) return;
                    var id = __instance.identifier;
                    if (id == null) return;
                    if (id == Config.IdExtractorBasic.Value || id == Config.IdExtractorAdv.Value)
                    {
                        Disassembly.AppendHint(__result); // 原版物品的提示,特殊走这里
                        return;
                    }
                    var def = Items.Find(id); // 自定义物品:各自定义文件里自带 tooltip
                    if (def != null) def.AppendTooltip(__result, __instance);
                    else HelperLogic.AppendItemBinding(__result, __instance); // 其余物品:显示绑定信息
                }
                catch { }
            }
        }
    }
}
