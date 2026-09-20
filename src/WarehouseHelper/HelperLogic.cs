using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// 仓库助手核心:绑定数据(tag)、数字键触发、目标查找、动作执行。
    /// 绑定存在【被绑物品】自己的 tag 里:wh_b_s = 槽位(1 起,0/无 = 未绑),wh_b_a = 动作 id。
    /// 用法:拖着【物品】放到仓库助手(绑定站)上 -> 弹窗选槽位+动作。
    /// 每次绑定替换该键的上一批物品;一次多选仍可把多个物品一起绑定。
    /// 助手型号只决定绑定时可选的槽位范围(基础 1-2 / 进阶 1-9),触发不需要助手在场。
    /// </summary>
    public static class HelperLogic
    {
        public const int SlotCount = 9; // 最大槽数(进阶款)

        /// <summary>基础款 2 槽(Alt+1-2),进阶款 9 槽(Alt+1-9)。</summary>
        public static int SlotCountOf(GameItem helper)
            => helper != null && helper.identifier == Items.HelperAdvId ? 9 : 2;

        public const int ACT_USE = 0;      // 使用
        public const int ACT_ACTIVATE = 1; // 激活
        public const int ACT_TOGGLE = 2;   // 切换
        public const int ACT_OPEN = 3;     // 打开
        public const int ACT_UNLOAD = 4;   // 卸载
        public const int ACT_MOVE = 5;     // Persisted IDs 0-5 must never be renumbered.
        public const int ACT_EQUIP = 6;    // 装备

        public const int ActionCount = 7;

        public static bool IsHelper(GameItem i)
            => i != null && (i.identifier == Items.HelperBasicId || i.identifier == Items.HelperAdvId);

        public static void Init() { }

        private static Il2CppSystem.Func<GameItem, bool> F1(System.Func<GameItem, bool> f)
            => (Il2CppSystem.Func<GameItem, bool>)f;

        // ---------- 绑定 ----------

        public static void SetupHelper(GameItem helper)
        {
            // 不再挂任何 Il2Cpp 委托(原生 InvokeAllReduce 会因 interop 委托抛 TargetException)。
            // 交互走 Interaction.cs 的 GameItem.May/Can/Target patch。
        }

        public static bool BindablePublic(GameItem helper, GameItem item) => Bindable(helper, item);
        public static void OpenBindPublic(GameItem helper, List<GameItem> items) => OpenBind(helper, items);

        private static bool Bindable(GameItem helper, GameItem item)
        {
            if (helper == null || item == null) return false;
            if (ReferenceEquals(helper, item)) return false;
            if (IsHelper(item)) return false;
            if (Conversion.IsPile(item)) return false;
            return true;
        }

        /// <summary>收集本次拖放要绑定的物品:单拖就 1 个;多选组拖(ItemMultiSelectHandler)就全选集合。</summary>
        public static List<GameItem> CollectBindTargets(GameItem helper, GameItem primary)
        {
            var list = new List<GameItem>();
            if (primary != null) list.Add(primary);
            try
            {
                var ms = ItemMultiSelectHandler.current;
                if (ms != null && ms.HasSelection)
                {
                    var sel = ms.selectedItems;
                    // 主物品必须在选中集合里,否则视为过期选择(单拖)
                    if (sel != null && sel.Count > 1 && ContainsPtr(sel, primary))
                    {
                        foreach (var el in sel)
                        {
                            if (el == null) continue;
                            var gi = (GameItem)el; // GameItemElement : GameItem
                            if (!ContainsPtr(list, gi) && Bindable(helper, gi)) list.Add(gi);
                        }
                    }
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("多选收集: " + e.Message); }
            return list;
        }

        private static bool ContainsPtr(Il2CppSystem.Collections.Generic.List<GameItemElement> list, GameItem item)
        {
            foreach (var el in list)
                if (el != null && item != null && el.Pointer == item.Pointer) return true;
            return false;
        }

        private static bool ContainsPtr(List<GameItem> list, GameItem item)
        {
            foreach (var el in list)
                if (el != null && item != null && el.Pointer == item.Pointer) return true;
            return false;
        }

        private static void OpenBind(GameItem helper, List<GameItem> items)
        {
            // 延一帧再开菜单,避免打断原版拖拽清理;物品引用在此刻已收齐,选择集后续清空无所谓
            MelonCoroutines.Start(OpenBindNextFrame(helper, items, BindWindow.Generation));
        }

        private static IEnumerator OpenBindNextFrame(GameItem helper, List<GameItem> items, int generation)
        {
            yield return null;
            if (generation != BindWindow.Generation) yield break;
            try { BindWindow.Open(helper, items); }
            catch (Exception e) { WarehouseHelperMod.Err("打开绑定菜单失败: " + e); }
        }

        /// <summary>写入单个物品的绑定。菜单通过 BindingBatch.Replace 一次覆盖整个槽位。</summary>
        public static void SetBinding(GameItem item, int slot, int action)
        {
            if (item == null || slot < 0 || slot >= SlotCount || action < 0 || action >= ActionCount)
                throw new ArgumentOutOfRangeException("Invalid item binding");
            Conversion.SetTagInt(item, "wh_b_s", slot + 1);
            Conversion.SetTagInt(item, "wh_b_a", action);

        }

        public static void ClearBinding(GameItem item)
        {
            Conversion.SetTagInt(item, "wh_b_s", 0);
            Conversion.SetTagInt(item, "wh_b_a", 0);

        }

        /// <summary>物品当前的绑定;未绑返回 false。slot 为 0 起。</summary>
        public static bool GetBinding(GameItem item, out int slot, out int action)
        {
            slot = Conversion.GetTagInt(item, "wh_b_s") - 1;
            action = Conversion.GetTagInt(item, "wh_b_a");
            return slot >= 0 && slot < SlotCount && action >= 0 && action < ActionCount;
        }

        /// <summary>绑定到某槽位(0 起)的物品总数,绑定窗口下拉里显示用。</summary>
        public static int CountBound(int slot) => FindBoundItems(slot).Count;

        // ---------- tooltip ----------

        /// <summary>任何物品的 tooltip:若已绑定,显示绑定信息。</summary>
        public static void AppendItemBinding(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (!GetBinding(item, out int slot, out int action)) return;
                string act = I18n.ActionName(action);
                string mod = Config.HotkeyModifier.Value;
                string prefix = mod == "None" ? "" : mod + "+";
                TooltipTail.Line(builder, I18n.F("binding.tooltip", prefix + (slot + 1), act), bold: true);
            }
            catch { }
        }

        // ---------- 触发 ----------

        public static void OnSceneChange()
        {
            BatchUseController.Reset();
            MoveController.Reset();
            BindWindow.Reset();
        }

        public static void Tick()
        {
            MoveController.Tick();
            if (BindWindow.IsOpen || NativeMenu.BlocksGameInput || MoveController.BlocksHotkeys || !Config.ModifierHeld()) return;
            for (int i = 0; i < SlotCount; i++)
                if (HelperHotkeys.NumberDown(i))
                    TryTrigger(i);
        }

        private static void TryTrigger(int slot)
        {
            try
            {
                if (MoveController.TryFinish(slot)) return;
                var items = FindBoundItems(slot);

                var batches = new Dictionary<int, List<GameItem>>();
                foreach (var item in items)
                {
                    if (!GetBinding(item, out int s, out int action) || s != slot) continue;
                    if (!batches.TryGetValue(action, out var batch)) batches[action] = batch = new List<GameItem>();
                    batch.Add(item);
                }
                foreach (var batch in batches)
                {
                    if (batch.Key == ACT_MOVE) MoveController.Request(batch.Value, slot);
                    else if (batch.Key == ACT_USE || batch.Key == ACT_EQUIP)
                        BatchUseController.Begin(batch.Value, batch.Key == ACT_EQUIP);
                    else foreach (var item in batch.Value)
                    {
                        try { Execute(item, batch.Key); }
                        catch (Exception e) { WarehouseHelperMod.Warn(SafeName(item) + ": " + e.Message); }
                    }
                }
            }
            catch (Exception e) { WarehouseHelperMod.Err("TryTrigger: " + e); }
        }

        public static void Execute(GameItem target, int action)
        {
            if (target == null || action < 0 || action >= ActionCount) return;
            switch (action)
            {
                case ACT_USE:
                case ACT_EQUIP:
                    if (!ItemActions.Select(target, action == ACT_EQUIP))
                    { return; }
                    break;
                case ACT_ACTIVATE:
                    if (!SafeBool(target.CanActivateSlotItem)) { return; }
                    target.ActivateSlotItem();
                    break;
                case ACT_TOGGLE:
                    if (!SafeBool(target.CanToggleSlotItem)) { return; }
                    target.ToggleSlotItem();
                    break;
                case ACT_OPEN:
                    if (!ItemActions.CanOpen(target)) return;
                    ToggleOpen(target);
                    break;
                case ACT_UNLOAD:
                    if (!ItemActions.CanOpen(target)) return;
                    target.TryUnloadAll();
                    break;
                case ACT_MOVE:
                    if (GetBinding(target, out int moveSlot, out _)) MoveController.Request(target, moveSlot);
                    break;
            }

        }

        /// <summary>打开/关闭切换(与双击物品一致):窗口已开 -> CloseContentWindow 关掉(含子窗口);没开 -> 双击开箱原生路径打开。</summary>
        private static void ToggleOpen(GameItem target)
        {
            try
            {
                var win = target.contentWindow;
                if (win == null) { return; }
                if (win.IsVisible())
                {
                    target.CloseContentWindow(true);

                    return;
                }
                var dc = ItemMouseDoubleClickHandler.current;
                if (dc == null) { WarehouseHelperMod.Warn("ItemMouseDoubleClickHandler 不存在,无法打开窗口"); return; }
                Vector3 mp = Input.mousePosition;
                dc.OpenContentAction(target, new Vector2(mp.x, mp.y));
            }
            catch (Exception e) { WarehouseHelperMod.Warn("ToggleOpen: " + e.Message); }
        }

        // ---------- 可用动作枚举(绑定时选) ----------

        public static List<int> ValidActions(GameItem item) => ItemActions.Available(item);

        // ---------- 图搜索 ----------

        /// <summary>全图查找绑定了某槽位(0 起)的所有物品。</summary>
        public static List<GameItem> FindBoundItems(int slot)
        {
            var result = new List<GameItem>();
            try
            {
                var roots = GraphHandler.windowGraphNodeRoots;
                if (roots == null) return result;
                int want = slot + 1;
                var filter = F1(i => i != null && !IsHelper(i) && Conversion.GetTagInt(i, "wh_b_s") == want);
                var pass = F1(i => true);
                foreach (var w in roots)
                {
                    var child = w?.child;
                    if (child == null) continue;
                    var found = GraphUtils.FindAllChildrenType<GameItem>(child, filter, pass);
                    if (found == null) continue;
                    foreach (var f in found)
                        if (!ContainsPtr(result, f)) result.Add(f);
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("FindBoundItems: " + e.Message); }
            return result;
        }

        public static List<GameItem> FindHelpers()
        {
            var result = new List<GameItem>();
            try
            {
                var roots = GraphHandler.windowGraphNodeRoots;
                if (roots == null) return result;
                var filter = F1(i => IsHelper(i));
                var pass = F1(i => true);
                foreach (var w in roots)
                {
                    var child = w?.child;
                    if (child == null) continue;
                    var found = GraphUtils.FindAllChildrenType<GameItem>(child, filter, pass);
                    if (found == null) continue;
                    foreach (var f in found)
                        if (!ContainsPtr(result, f)) result.Add(f);
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("FindHelpers: " + e.Message); }
            return result;
        }

        // ---------- 工具 ----------

        public static string SafeName(GameItem item)
        {
            if (item != null && I18n.ItemName(item.identifier) is string translated) return translated;
            try { return item.GetDisplayName(false) ?? item.identifier; }
            catch { return item?.identifier ?? "?"; }
        }

        private static bool SafeBool(Func<bool> f)
        {
            try { return f(); } catch { return false; }
        }
    }
}
