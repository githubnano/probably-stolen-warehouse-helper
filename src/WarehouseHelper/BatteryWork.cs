using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WarehouseHelper
{
    /// <summary>
    /// 电池勤务(全局自动化,时机对齐游戏原生节奏):
    /// 1) 充电器自动换电池:睡醒结算后、且供电正常(isPowerOn,交电费没停电、白天确实充上了电)时,
    ///    遍历所有充电器,把充满的电池换成没满的(普通=剩余电量高优先;大容量模式=容量大优先)。
    ///    全局开关存在配置 charger_auto_swap(0关/1普通/2大容量),右键任一充电器循环切换。
    /// 2) 机器低电自动换电池:机器右键逐台开启(tag wh_ab 随存档)。机器每运行一次
    ///    (原生 TryDrawCyclePower / OnMachineActioned / 夜间结算后的睡醒点)就检查一次,
    ///    不够再跑一轮就换,满电优先。
    /// 判定全部走原生帮助类(PowerHelper / MachineHelper / MachineryHelper),
    /// 移动走原生图方法(GameInventory.Expel + GraphUtils.PlaceAtShape / TryAccept),失败全量回滚。
    /// </summary>
    public static class BatteryWork
    {
        public const string TagAuto = "wh_ab";

        // ---------- 判定 ----------

        public static bool IsCharger(GameItem item)
            => item != null && item.identifier == Config.IdRecharger.Value;

        /// <summary>是不是电池:原生电源标签 power_source_item(energy_credit/energy_credit_ext 等电池都有;
        /// powerblock 之类的供电机器没有此标签,天然排除)。</summary>
        public static bool IsBattery(GameItem item)
        {
            if (item == null || IsCharger(item) || HelperLogic.IsHelper(item) || Conversion.IsPile(item)) return false;
            return ItemActions.Check(() => item.IsTag("power_source_item"));
        }

        /// <summary>机器是否带电池槽(原生 MachineHelper;非机器会抛,静默吞掉)。</summary>
        public static bool HasBatterySlot(GameItem item)
        {
            if (item == null || IsCharger(item)) return false;
            try { return MachineHelper.GetBatterySlot(item) != null; }
            catch { return false; }
        }

        public static bool IsAutoSwap(GameItem machine)
            => Conversion.GetTagInt(machine, TagAuto) == 1;

        public static int EnergyOf(GameItem battery)
        {
            try { return PowerHelper.GetAvailableEnergyFromItem(battery, false); }
            catch { return Conversion.GetTagInt(battery, "power_source_item_energy"); }
        }

        public static int CapacityOf(GameItem battery)
            => Conversion.GetTagInt(battery, "power_source_item_max_energy");

        public static bool IsFull(GameItem battery)
        {
            if (ItemActions.Check(() => PowerHelper.IsPowerSourceFull(battery))) return true;
            int cap = CapacityOf(battery);
            return cap > 0 && EnergyOf(battery) >= cap;
        }

        /// <summary>电池此刻"闲置"在哪:库存格子里(不在充电器/机器电池槽/装备位/垃圾桶)。</summary>
        private static bool IsLooseBattery(GameItem item)
        {
            if (!IsBattery(item)) return false;
            if (item.parentInventory == null) return false;
            if (item.parentInventory is GameSlotInventory) return false; // 电池槽/装备位
            try { if (InsideCharger(item) || InsideTrash(item)) return false; } catch { }
            return true;
        }

        private static bool InsideTrash(GameItem item)
        {
            var inv = item.parentInventory;
            int depth = 0;
            while (inv != null && depth++ < 6)
            {
                var pi = inv.GetParentItem();
                if (pi != null && pi.identifier == Config.IdTrashcan.Value) return true;
                inv = pi?.parentInventory;
            }
            return false;
        }

        private static bool InsideCharger(GameItem item)
        {
            var inv = item.parentInventory;
            int depth = 0;
            while (inv != null && depth++ < 6)
            {
                var pi = inv.GetParentItem();
                if (IsCharger(pi)) return true;
                inv = pi?.parentInventory;
            }
            return false;
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
            catch (Exception e) { WarehouseHelperMod.Warn("BatteryWork.FindAll: " + e.Message); }
            return result;
        }

        // ---------- 功能 1:充电器自动换电池(睡觉结算后) ----------

        /// <summary>全局模式:0关 1普通 2大容量优先(配置持久化)。</summary>
        public static int ChargerMode => Config.ChargerAutoSwap?.Value ?? 0;

        public static int CycleChargerMode()
        {
            int next = (ChargerMode + 1) % 3;
            Config.ChargerAutoSwap.Value = next;
            return next;
        }

        /// <summary>睡醒点(ModHook.OnWakingUpLate)调用,这是主通道:
        /// 电池是白天实时慢慢充的,睡一觉起来该满的都满了;供电正常(isPowerOn)说明夜里确实充上了电。
        /// 然后机器夜里也都运行过一轮,逐台检查开了自动换电池的机器。</summary>
        public static void OnWakeUp()
        {
            try
            {
                if (IsPowerOn() && ChargerMode != 0) SwapAllChargers();
                foreach (var machine in FindAll(i => IsAutoSwap(i)))
                    TryReplaceMachineBattery(machine);
            }
            catch (Exception e) { WarehouseHelperMod.Err("睡醒电池勤务: " + e); }
        }

        /// <summary>供电正常(交电费没停电)——原生 PlayerStore.isPowerOn。</summary>
        private static bool IsPowerOn()
        {
            try
            {
                var ps = PlayerStore.instance;
                return ps != null && ps.isPowerOn;
            }
            catch { return false; }
        }

        /// <summary>遍历所有充电器,把充满的电池换成没满的,返回换出总数。</summary>
        private static int SwapAllChargers()
        {
            int swapped = 0;
            foreach (var charger in FindAll(IsCharger))
                swapped += SwapChargerInternal(charger, ChargerMode == 2);
            if (swapped > 0) Notice.Show(I18n.F("battery.swapped", swapped));
            return swapped;
        }

        /// <summary>如果游戏哪天走批量充电(活动/读档补偿等),充完那一刻也换一轮;
        /// 与睡醒点幂等,换过的充电器不会重复动。</summary>
        [HarmonyLib.HarmonyPatch(typeof(PowerHelper), nameof(PowerHelper.ChargeAllRechargable))]
        public static class ChargeAllPatch
        {
            public static void Postfix()
            {
                try
                {
                    if (ChargerMode != 0 && IsPowerOn()) SwapAllChargers();
                }
                catch (Exception e) { WarehouseHelperMod.Err("充完电换电池: " + e); }
            }
        }

        /// <summary>单个充电器:满电电池换成没满的,返回换出的数量。</summary>
        private static int SwapChargerInternal(GameItem charger, bool preferLarge)
        {
            var full = new List<GameItem>();
            var children = charger.FindAllChildItems();
            if (children == null) return 0;
            foreach (var c in children)
                if (IsBattery(c) && IsFull(c) && ItemActions.Check(c.MayRemove) && c.MaxNumRemove() > 0)
                    full.Add(c);
            if (full.Count == 0) return 0;

            var spares = FindAll(IsLooseBattery);
            // 只要没满的;去掉正挂在任何充电器里的(保险)和空引用
            spares.RemoveAll(b => b == null || IsFull(b) || InsideCharger(b));
            if (preferLarge)
                spares.Sort((a, b) => CapacityOf(b) != CapacityOf(a)
                    ? CapacityOf(b).CompareTo(CapacityOf(a))
                    : EnergyOf(b).CompareTo(EnergyOf(a)));
            else
                spares.Sort((a, b) => EnergyOf(b).CompareTo(EnergyOf(a)));

            int swapped = 0;
            foreach (var oldFull in full)
            {
                if (spares.Count == 0) break;
                var spare = spares[0];
                spares.RemoveAt(0);
                if (SwapTwo(oldFull, spare)) swapped++;
            }
            return swapped;
        }

        /// <summary>两块电池跨库存互换位置:记录形状→双拔→交叉放回(优先原形状格,失败任意格)→全量回滚。
        /// 实测 SwapItemsInPlace 跨库存不可靠(会把物品抬到上一层库存),不要用它。</summary>
        private static bool SwapTwo(GameItem a, GameItem b)
        {
            if (a == null || b == null) return false;
            GameInventory invA = null, invB = null;
            GridShape shapeA = null, shapeB = null;
            try
            {
                invA = a.parentInventory; invB = b.parentInventory;
                if (invA == null || invB == null) return false;
                if (a.modifiedShape != null) shapeA = new GridShapeBuilder(a.modifiedShape).Build();
                if (b.modifiedShape != null) shapeB = new GridShapeBuilder(b.modifiedShape).Build();

                if (!invA.Expel(a) || a.parentInventory != null) return false;
                if (!invB.Expel(b) || b.parentInventory != null)
                {
                    PutBack(a, invA, shapeA);
                    return false;
                }
                // a 去 b 的老家(优先原位),b 去 a 的老家(优先原位)
                bool aPlaced = PlaceInto(a, invB, shapeB);
                bool bPlaced = aPlaced && PlaceInto(b, invA, shapeA);
                if (aPlaced && bPlaced) return true;
                // 全量回滚:各回各家,绝不丢件
                if (b.parentInventory == null || b.parentInventory != invB) PutBack(b, invB, shapeB);
                if (a.parentInventory == null || a.parentInventory != invA) PutBack(a, invA, shapeA);
                return false;
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Warn("换电池: " + e.Message);
                try { if (a != null && a.parentInventory == null && invA != null) PutBack(a, invA, shapeA); } catch { }
                try { if (b != null && b.parentInventory == null && invB != null) PutBack(b, invB, shapeB); } catch { }
                return false;
            }
        }

        /// <summary>放进目标库存:优先原形状位置,其次任意合法格。返回是否确实落在该库存。</summary>
        private static bool PlaceInto(GameItem item, GameInventory inv, GridShape shape)
        {
            if (shape != null && ItemActions.Check(() => GraphUtils.PlaceAtShape(item, inv, shape))
                && item.parentInventory == inv) return true;
            try
            {
                return GraphUtils.TryAccept(inv.Cast<GraphNodeStorage>(), item) > 0 && item.parentInventory == inv;
            }
            catch { return false; }
        }

        private static void PutBack(GameItem item, GameInventory inv, GridShape shape)
        {
            try { PlaceInto(item, inv, shape); }
            catch (Exception e) { WarehouseHelperMod.Warn("回滚归位: " + e.Message); }
        }

        // ---------- 功能 2:机器低电自动换电池 ----------

        public static void ToggleAutoSwap(GameItem machine)
        {
            bool next = !IsAutoSwap(machine);
            Conversion.SetTagInt(machine, TagAuto, next ? 1 : 0);
            Notice.Show(I18n.T(next ? "battery.auto_on" : "battery.auto_off"));
        }

        /// <summary>tooltip 末尾追加开关状态。</summary>
        public static void AppendTooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (!IsAutoSwap(item) || !HasBatterySlot(item)) return;
                TooltipTail.Line(builder, I18n.T("battery.tooltip"), bold: true);
            }
            catch { }
        }

        /// <summary>机器每运行一次就检查一次(两个原生钩子 + 睡醒巡检都进这里)。</summary>
        public static void OnMachineRan(GameItem machine)
        {
            try
            {
                if (IsAutoSwap(machine)) TryReplaceMachineBattery(machine);
            }
            catch (Exception e) { WarehouseHelperMod.Warn("机器换电池检查: " + e.Message); }
        }

        /// <summary>电量不够再跑一轮就换电池,优先满电。</summary>
        private static void TryReplaceMachineBattery(GameItem machine)
        {
            var slot = MachineHelper.GetBatterySlot(machine);
            if (slot == null) return;
            // 正在跑流程时不打扰;没电停下后下一轮检查会接手
            if (ItemActions.Check(() => MachineryHelper.IsProcessingActive(machine))) return;
            var current = slot.currentItem;
            if (current != null && ItemActions.Check(() => MachineHelper.CanPower(machine, slot)))
            {
                _noSpareNotified.Remove(machine.uniqueId); // 电量恢复,下次缺电重新提示
                return;
            }

            var spares = FindAll(IsLooseBattery);
            spares.RemoveAll(b => b == null || ReferenceEquals(b, current) || EnergyOf(b) <= 0);
            if (spares.Count == 0)
            {
                NotifyNoSpare(machine);
                return;
            }
            // 满电优先,其次剩余电量高
            spares.Sort((a, b) =>
            {
                bool fa = IsFull(a), fb = IsFull(b);
                if (fa != fb) return fb.CompareTo(fa);
                return EnergyOf(b).CompareTo(EnergyOf(a));
            });
            var best = spares[0];
            bool done;
            if (current == null)
            {
                // 槽是空的:直接把 best 放进电池槽
                int n = GraphUtils.TryAccept(slot.Cast<GraphNodeStorage>(), best);
                done = n > 0 && best.parentInventory == slot;
            }
            else done = SwapTwo(current, best);
            if (done)
            {
                _noSpareNotified.Remove(machine.uniqueId);
                Notice.Show(I18n.F("battery.machine_swapped", HelperLogic.SafeName(machine), HelperLogic.SafeName(best)));
            }
        }

        // "找不到电池"提示:每台机器只报一次,换好或电池归位后才会再报
        private static readonly HashSet<int> _noSpareNotified = new();

        private static void NotifyNoSpare(GameItem machine)
        {
            try
            {
                if (!_noSpareNotified.Add(machine.uniqueId)) return;
                Notice.Show(I18n.F("battery.machine_none", HelperLogic.SafeName(machine)));
            }
            catch { }
        }

        /// <summary>机器被动作/使用(手动开机、放料等)后检查。</summary>
        [HarmonyLib.HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.OnMachineActioned))]
        public static class MachineActionedPatch
        {
            public static void Postfix(GameItem machine, GameInventory moduleGrid)
            {
                OnMachineRan(machine);
            }
        }

        /// <summary>机器每跑完一个循环(原生抽电)后检查 —— "运行一次之后检查"的精确位置。</summary>
        [HarmonyLib.HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.TryDrawCyclePower))]
        public static class CyclePowerPatch
        {
            public static void Postfix(GameItem machine, GameSlotInventory batterySlot, bool __result)
            {
                if (__result) OnMachineRan(machine);
            }
        }
    }
}
