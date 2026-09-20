using System;
using System.Collections;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;

namespace WarehouseHelper
{
    /// <summary>
    /// 螺丝刀拆解模组提取器(机械臂)→ 特殊零件堆。
    /// 交互入口在 Interaction.cs(GameItem.May/Can/Target patch,Kitchen 同款)。
    /// 这里只剩判定 + 执行(协程延一帧动手,不碰拖拽中的物品)。
    /// </summary>
    public static class Disassembly
    {
        public static bool IsScrewdriver(GameItem i)
            => i != null && i.identifier == Config.IdScrewdriver.Value;

        public static bool IsExtractor(GameItem i)
            => i != null && (i.identifier == Config.IdExtractorBasic.Value || i.identifier == Config.IdExtractorAdv.Value);

        /// <summary>在原版 Target 分发中被调用;延一帧再动手,不碰拖拽中的物品。</summary>
        public static void DoDisassemble(GameItem extractor)
        {
            string pileId = extractor.identifier == Config.IdExtractorAdv.Value ? Items.PartsAdvId : Items.PartsBasicId;

            MelonCoroutines.Start(DoDisassembleNextFrame(extractor, pileId));
        }

        private static IEnumerator DoDisassembleNextFrame(GameItem extractor, string pileId)
        {
            yield return null;

            try
            {
                if (extractor == null) { WarehouseHelperMod.Warn("拆解取消:提取器已失效"); yield break; }

                GameItem pile = null;
                try { pile = DirectoryMaster.Item(pileId, true); }
                catch (Exception e) { WarehouseHelperMod.Err("创建零件堆失败: " + e); yield break; }
                if (pile == null) { WarehouseHelperMod.Err("创建零件堆失败: null"); yield break; }

                GameInventory parent = null;
                try { parent = extractor.parentInventory; } catch { }

                bool placed = false;
                if (parent != null)
                {
                    try { placed = GraphUtils.TryAccept(parent.Cast<GraphNodeStorage>(), pile) > 0; }
                    catch (Exception e) { WarehouseHelperMod.Warn("零件堆入位异常: " + e.Message); }
                }

                if (placed)
                {
                    // 不走 SwapItemsInPlace:新旧物品占格不同(2x3 → 3x3),原地换会压到邻居。
                    // 照原版切肉的做法:新物品由 TryAccept 放在空格,旧物品销毁。
                    try { extractor.Destroy(); } catch (Exception e) { WarehouseHelperMod.Warn("Destroy 提取器异常: " + e.Message); }

                    yield break;
                }

                // 原容器不可用:背包兜底
                try
                {
                    var entries = UnityEngine.Object.FindObjectsOfType<EmporiumEntry>();
                    if (entries != null && entries.Length > 0 && entries[0].backInvinvElement != null)
                        placed = GraphUtils.TryAccept(entries[0].backInvinvElement.Cast<GraphNodeStorage>(), pile) > 0;
                }
                catch (Exception e) { WarehouseHelperMod.Warn("零件堆入位(背包)异常: " + e.Message); }

                if (placed)
                {
                    try { extractor.Destroy(); } catch { }

                }
                else
                {
                    WarehouseHelperMod.Warn("零件堆无处可放,取消拆解(提取器保留)");
                    try { pile.Destroy(); } catch { }
                    Notice.Show(I18n.T("disassembly.no_space"));
                }
            }
            catch (Exception e) { WarehouseHelperMod.Err("拆解协程异常: " + e); }
        }

        public static void AppendHint(Il2Cpp.RichTextBuilder builder)
        {
            TooltipTail.Line(builder, I18n.T("disassembly.hint"));
        }
    }
}
