using System;
using System.Collections;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;

namespace WarehouseHelper
{
    /// <summary>
    /// 零件堆改装:把材料(垃圾/废金属/电子元件)拖到零件堆上,凑齐后变成仓库助手。
    /// 消耗与变形都延一帧执行(避免拖拽管道里销毁物品卡死拖拽)。
    /// 进度通过原生 tooltip 管线(TooltipTail)显示,像冰箱升级一样。
    /// </summary>
    public static class Conversion
    {
        public static void SetupPile(GameItem pile)
        {
            // 不再挂任何 Il2Cpp 委托(原生 InvokeAllReduce 会因 interop 委托抛 TargetException)。
            // 交互走 Interaction.cs 的 GameItem.May/Can/Target patch。
        }

        public static bool MayAcceptPublic(GameItem pile, GameItem other) => MayAccept(pile, other);
        public static void AcceptPublic(GameItem pile, GameItem material) => Accept(pile, material);

        public static bool IsPile(GameItem i)
            => i != null && (i.identifier == Items.PartsBasicId || i.identifier == Items.PartsAdvId);

        private static bool IsAdv(GameItem pile) => pile.identifier == Items.PartsAdvId;

        private static string TagFor(string materialId)
        {
            if (materialId == Config.IdJunk.Value) return "wh_m_junk";
            if (materialId == Config.IdScrap.Value) return "wh_m_scrap";
            if (materialId == Config.IdCircuit.Value) return "wh_m_circuit";
            return null;
        }

        private static int NeedFor(GameItem pile, string materialId)
        {
            bool adv = IsAdv(pile);
            if (materialId == Config.IdJunk.Value) return adv ? 0 : Config.BasicNeedJunk.Value;
            if (materialId == Config.IdScrap.Value) return adv ? Config.AdvNeedScrap.Value : 0;
            if (materialId == Config.IdCircuit.Value) return adv ? Config.AdvNeedCircuit.Value : Config.BasicNeedCircuit.Value;
            return 0;
        }

        private static bool MayAccept(GameItem pile, GameItem other)
        {
            if (pile == null || other == null) return false;
            var tag = TagFor(other.identifier);
            if (tag == null) return false;
            int need = NeedFor(pile, other.identifier);
            if (need <= 0) return false;
            return GetTagInt(pile, tag) < need;
        }

        private static void Accept(GameItem pile, GameItem material)
        {
            var matId = material?.identifier;
            var tag = matId == null ? null : TagFor(matId);
            if (tag == null) return;
            MelonCoroutines.Start(AcceptNextFrame(pile, material, tag));
        }

        private static IEnumerator AcceptNextFrame(GameItem pile, GameItem material, string tag)
        {
            yield return null;

            try
            {
                if (pile == null || material == null) yield break;
                string matId = material.identifier;
                int need = NeedFor(pile, matId);
                int got = GetTagInt(pile, tag);
                if (need <= 0 || got >= need) yield break;

                // 消耗 1 个
                int n = material.unitCount;
                if (n > 1) material.SetAmount(n - 1);
                else material.Destroy();

                got++;
                SetTagInt(pile, tag, got);


                if (IsComplete(pile)) Transform(pile);
            }
            catch (Exception e) { WarehouseHelperMod.Err("Accept: " + e); }
        }

        private static bool IsComplete(GameItem pile)
        {
            if (IsAdv(pile))
            {
                return GetTagInt(pile, "wh_m_scrap") >= Config.AdvNeedScrap.Value
                    && GetTagInt(pile, "wh_m_circuit") >= Config.AdvNeedCircuit.Value;
            }
            return GetTagInt(pile, "wh_m_junk") >= Config.BasicNeedJunk.Value
                && GetTagInt(pile, "wh_m_circuit") >= Config.BasicNeedCircuit.Value;
        }

        private static void Transform(GameItem pile)
        {
            string helperId = IsAdv(pile) ? Items.HelperAdvId : Items.HelperBasicId;
            MelonCoroutines.Start(TransformNextFrame(pile, helperId));
        }

        private static IEnumerator TransformNextFrame(GameItem pile, string helperId)
        {
            yield return null;

            try
            {


                GameItem helper = null;
                try { helper = DirectoryMaster.Item(helperId, true); }
                catch (Exception e) { WarehouseHelperMod.Err("创建仓库助手失败: " + e); yield break; }
                if (helper == null) { WarehouseHelperMod.Err("创建仓库助手失败: null"); yield break; }

                if (TryPlaceHelper(pile, helper))
                {
                    // TryAccept 已确认新助手能放入，再销毁零件堆。
                    try { pile.Destroy(); } catch { }
                    yield break;
                }

                // 没地方放:材料不白扣,每 2 秒重试直到放下为止
                WarehouseHelperMod.Warn("仓库助手无处可放,进入自动重试");
                try { helper.Destroy(); } catch { }
                MelonCoroutines.Start(TransformRetryLoop(pile, helperId));
            }
            catch (Exception e) { WarehouseHelperMod.Err("改装协程异常: " + e); }
        }

        /// <summary>空间不足时的重试:直到助手放下(零件堆销毁)或零件堆不存在为止。</summary>
        private static IEnumerator TransformRetryLoop(GameItem pile, string helperId)
        {
            while (true)
            {
                yield return new UnityEngine.WaitForSeconds(2f);
                if (pile == null) yield break;

                GameItem helper = null;
                try { helper = DirectoryMaster.Item(helperId, true); } catch { }
                if (helper == null) yield break;

                if (TryPlaceHelper(pile, helper))
                {
                    try { pile.Destroy(); } catch { }

                    yield break;
                }
                try { helper.Destroy(); } catch { }
            }
        }

        /// <summary>优先放零件堆所在容器,满了兜底背包。</summary>
        private static bool TryPlaceHelper(GameItem pile, GameItem helper)
        {
            bool placed = false;
            try
            {
                var parent = pile.parentInventory;
                if (parent != null)
                    placed = GraphUtils.TryAccept(parent.Cast<GraphNodeStorage>(), helper) > 0;
            }
            catch (Exception e) { WarehouseHelperMod.Warn("助手入位异常: " + e.Message); }

            if (!placed)
            {
                try
                {
                    var entries = UnityEngine.Object.FindObjectsOfType<EmporiumEntry>();
                    if (entries != null && entries.Length > 0 && entries[0].backInvinvElement != null)
                        placed = GraphUtils.TryAccept(entries[0].backInvinvElement.Cast<GraphNodeStorage>(), helper) > 0;
                }
                catch (Exception e) { WarehouseHelperMod.Warn("助手入位(背包)异常: " + e.Message); }
            }
            return placed;
        }

        /// <summary>原生 tooltip 追加改装进度(冰箱式)。</summary>
        public static void AppendProgress(RichTextBuilder builder, GameItem pile)
        {
            try
            {
                TooltipTail.Line(builder, I18n.T("conversion.progress"), bold: true);
                if (IsAdv(pile))
                {
                    TooltipTail.Line(builder, MatLine(I18n.T("material.scrap"), GetTagInt(pile, "wh_m_scrap"), Config.AdvNeedScrap.Value));
                    TooltipTail.Line(builder, MatLine(I18n.T("material.circuit"), GetTagInt(pile, "wh_m_circuit"), Config.AdvNeedCircuit.Value));
                }
                else
                {
                    TooltipTail.Line(builder, MatLine(I18n.T("material.junk"), GetTagInt(pile, "wh_m_junk"), Config.BasicNeedJunk.Value));
                    TooltipTail.Line(builder, MatLine(I18n.T("material.circuit"), GetTagInt(pile, "wh_m_circuit"), Config.BasicNeedCircuit.Value));
                }
                TooltipTail.Line(builder, I18n.T("conversion.hint"), italic: true);
            }
            catch { }
        }

        private static string MatLine(string name, int got, int need)
            => $"{name} {got}/{need}{(got >= need ? " ✓" : "")}";

        // ---- tag 工具 ----

        public static int GetTagInt(GameItem item, string name)
        {
            try
            {
                if (!item.IsTag(name)) return 0;
                var t = item.GetTagReadonly(name);
                if (t == null) return 0;
                return t.GetInt();
            }
            catch { return 0; }
        }

        // Kitchen 实测可用的写法:直接写 state + modifiedState,不经 ModifyTag 的 Il2Cpp 委托
        // (ModifyTag 要走 Action<TagState> 委托编组,写不进去时静默失败,tooltip 永远 0/1——"喂材料不涨"的疑凶)
        public static void SetTagInt(GameItem item, string name, int v)
        {
            try
            {
                var st = item.state;
                if (st != null) { st.EnableTag(name); var t = st.GetTag(name); if (t != null) t.SetInt(v); }
                var ms = item.modifiedState;
                if (ms != null) { ms.EnableTag(name); var t = ms.GetTag(name); if (t != null) t.SetInt(v); }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("SetTagInt: " + e.Message); }
        }

        public static void SetTagString(GameItem item, string name, string v)
        {
            try
            {
                var st = item.state;
                if (st != null) { st.EnableTag(name); var t = st.GetTag(name); if (t != null) t.SetString(v); }
                var ms = item.modifiedState;
                if (ms != null) { ms.EnableTag(name); var t = ms.GetTag(name); if (t != null) t.SetString(v); }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("SetTagString: " + e.Message); }
        }

        public static string GetTagString(GameItem item, string name)
        {
            try
            {
                if (!item.IsTag(name)) return null;
                var t = item.GetTagReadonly(name);
                if (t == null) return null;
                return t.GetString();
            }
            catch { return null; }
        }
    }
}
