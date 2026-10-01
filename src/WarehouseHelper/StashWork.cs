using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WarehouseHelper
{
    /// <summary>
    /// 一键藏匿:把所有不在走私者暗格里的违禁品/赃物转移进走私者暗格。
    /// 顺序:违禁品按等级从高到低,然后赃物按单价从高到低。
    /// 放不下的提示并标红(RedMark);右键"放回原位"按记录的原位恢复并取消标红。
    /// 移动只走原生方法:GraphUtils.CanAccept / TryAccept / PlaceAtShape。
    /// 原位记录是会话级的(重载存档后不再提供"放回原位")。
    /// </summary>
    public static class StashWork
    {
        private class Record
        {
            public GameItem Item;
            public GameInventory Home;
            public GridShape Shape;
        }

        private static readonly List<Record> _moved = new();
        public static bool HasRecord => _moved.Count > 0;

        // ---------- 判定 ----------

        public static bool IsBay(GameItem item)
            => item != null && Config.IsSmugglerBayId(item.identifier);

        public static bool IsStashable(GameItem item)
        {
            if (item == null) return false;
            try { if (item.IsRootedInSmugglerBay()) return false; } catch { }
            if (item.parentInventory == null) return false;
            if (item.parentInventory is GameSlotInventory) return false; // 装备位/电池槽里的不碰
            if (IsBay(item) || HelperLogic.IsHelper(item) || Conversion.IsPile(item)) return false;
            bool contraband = ItemActions.Check(() => ContrabandHelper.IsContraband(item));
            bool stolen = ItemActions.Check(() => StolenHelper.IsStolenItem(item));
            return contraband || stolen;
        }

        private static GameInventory FirstFitting(GameItem item, List<GameInventory> bays)
        {
            foreach (var bay in bays)
                if (ItemActions.Check(() => GraphUtils.CanAccept(bay.Cast<GraphNodeStorage>(), item)))
                    return bay;
            return null;
        }

        private static int ContrabandLevel(GameItem item)
        {
            try { return ContrabandHelper.GetContrabandLevelInInt(item); }
            catch { return 0; }
        }

        // ---------- 全图搜索 ----------

        private static List<GameItem> FindAll(Func<GameItem, bool> filter)
        {
            var result = new List<GameItem>();
            try
            {
                var roots = GraphHandler.windowGraphNodeRoots;
                if (roots == null) return result;
                var f = (Il2CppSystem.Func<GameItem, bool>)filter;
                var pass = (Il2CppSystem.Func<GameItem, bool>)(Func<GameItem, bool>)(i => true);
                var seen = new HashSet<IntPtr>();
                foreach (var w in roots)
                {
                    var child = w?.child;
                    if (child == null) continue;
                    var found = GraphUtils.FindAllChildrenType<GameItem>(child, f, pass);
                    if (found == null) continue;
                    foreach (var item in found)
                        if (item != null && seen.Add(item.Pointer)) result.Add(item);
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("StashWork.FindAll: " + e.Message); }
            return result;
        }

        /// <summary>所有走私者暗格的内容库存(实测:库存节点不带 GameInventory 类型名,
        /// 但 parentInventory 链可用 —— 直接按"父物品是暗格"在图里搜 GameInventory)。</summary>
        private static List<GameInventory> FindBays()
        {
            var bays = new List<GameInventory>();
            try
            {
                var roots = GraphHandler.windowGraphNodeRoots;
                if (roots == null) return bays;
                var filter = (Il2CppSystem.Func<GameInventory, bool>)(Func<GameInventory, bool>)(inv =>
                {
                    if (inv == null) return false;
                    try { return IsBay(inv.GetParentItem()); } catch { return false; }
                });
                var pass = (Il2CppSystem.Func<GameInventory, bool>)(Func<GameInventory, bool>)(i => true);
                var seen = new HashSet<IntPtr>();
                foreach (var w in roots)
                {
                    var child = w?.child;
                    if (child == null) continue;
                    var found = GraphUtils.FindAllChildrenType<GameInventory>(child, filter, pass);
                    if (found == null) continue;
                    foreach (var inv in found)
                        if (inv != null && seen.Add(inv.Pointer)) bays.Add(inv);
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("StashWork.FindBays: " + e.Message); }
            return bays;
        }

        // ---------- 转移 ----------

        public static void TransferAll()
        {
            try
            {
                if (!BatteryWork.HelperPresent()) { Notice.Show(I18n.T("helper.required")); return; }
                var bays = FindBays();
                if (bays.Count == 0) { Notice.Show(I18n.T("stash.no_bay")); return; }

                var contraband = new List<GameItem>();
                var stolen = new List<GameItem>();
                foreach (var item in FindAll(i => IsStashable(i)))
                {
                    try
                    {
                        if (!ItemActions.Check(item.MayRemove) || item.MaxNumRemove() < 1) continue;
                        if (ItemActions.Check(() => ContrabandHelper.IsContraband(item))) contraband.Add(item);
                        else stolen.Add(item);
                    }
                    catch { }
                }
                contraband.Sort((a, b) => ContrabandLevel(b).CompareTo(ContrabandLevel(a)));
                stolen.Sort((a, b) => b.unitValue.CompareTo(a.unitValue));

                int moved = 0, stuck = 0;
                foreach (var item in contraband) if (MoveOne(item, bays)) moved++; else stuck++;
                foreach (var item in stolen) if (MoveOne(item, bays)) moved++; else stuck++;

                if (moved > 0) Notice.Show(I18n.F("stash.moved", moved));
                else if (stuck == 0) { Notice.Show(I18n.T("stash.nothing")); return; }
                if (stuck > 0) Notice.Show(I18n.F("stash.no_space", stuck));
            }
            catch (Exception e) { WarehouseHelperMod.Err("一键藏匿: " + e); }
        }

        private static bool MoveOne(GameItem item, List<GameInventory> bays)
        {
            GameInventory home = null;
            GridShape shape = null;
            try
            {
                home = item.parentInventory;
                if (home == null) return false;
                if (item.modifiedShape != null)
                    shape = new GridShapeBuilder(item.modifiedShape).Build();
                GameInventory target = FirstFitting(item, bays);
                if (target == null) { RedMark.Mark(item); return false; }
                int n = GraphUtils.TryAccept(target.Cast<GraphNodeStorage>(), item);
                if (n < 1 || item.parentInventory != target)
                {
                    // 没进去:回原位(还在原库存就直接放着)
                    RedMark.Mark(item);
                    return false;
                }
                _moved.Add(new Record { Item = item, Home = home, Shape = shape });
                return true;
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Warn("藏匿单件: " + HelperLogic.SafeName(item) + " " + e.Message);
                RedMark.Mark(item);
                return false;
            }
        }

        // ---------- 放回 ----------

        public static void RestoreAll()
        {
            try
            {
                int ok = 0, failed = 0;
                for (int i = _moved.Count - 1; i >= 0; i--)
                {
                    var rec = _moved[i];
                    _moved.RemoveAt(i);
                    if (RestoreOne(rec)) ok++;
                    else failed++;
                }
                RedMark.ClearAll();
                if (ok > 0) Notice.Show(I18n.F("stash.restored", ok));
                if (failed > 0) Notice.Show(I18n.F("stash.restore_failed", failed));
                if (ok == 0 && failed == 0) Notice.Show(I18n.T("stash.nothing_restore"));
            }
            catch (Exception e) { WarehouseHelperMod.Err("放回原位: " + e); }
        }

        private static bool RestoreOne(Record rec)
        {
            try
            {
                if (rec.Item == null || rec.Home == null) return false;
                // 优先精确放回记录的形状位置
                if (rec.Shape != null && ItemActions.Check(() =>
                    GraphUtils.PlaceAtShape(rec.Item, rec.Home, rec.Shape))
                    && rec.Item.parentInventory == rec.Home)
                    return true;
                // 其次原生找位
                int n = GraphUtils.TryAccept(rec.Home.Cast<GraphNodeStorage>(), rec.Item);
                if (n > 0 && rec.Item.parentInventory == rec.Home) return true;
                // 最后背包兜底
                var entries = UnityEngine.Object.FindObjectsOfType<EmporiumEntry>();
                if (entries != null && entries.Length > 0 && entries[0].backInvinvElement != null)
                {
                    int m = GraphUtils.TryAccept(entries[0].backInvinvElement.Cast<GraphNodeStorage>(), rec.Item);
                    if (m > 0) return true;
                }
                return false;
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Warn("放回单件: " + HelperLogic.SafeName(rec.Item) + " " + e.Message);
                return false;
            }
        }

        public static void Reset()
        {
            _moved.Clear();
            RedMark.ClearAll();
        }
    }
}
