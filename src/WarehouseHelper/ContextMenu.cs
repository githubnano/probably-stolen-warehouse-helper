using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// 右键菜单扩展:命中目标物品(充电器 / 带电池槽的机器 / 走私者暗格)时,
    /// 用 NativeMenu(原生 UI 类型自建菜单)取代原生右键菜单 —— 前面是自定义功能,
    /// 后面照常列出原生可用动作(打开/使用/激活/切换/卸载),不丢原版功能。
    /// 挂法:patch ItemContextHandler.OnEventPress,命中就 return false 拦掉原生菜单。
    /// </summary>
    public static class ContextMenu
    {
        public static bool TryOpen(GameItem item)
        {
            if (item == null) return false;
            var labels = new List<string>();
            var runs = new List<Action>();

            if (BatteryWork.IsCharger(item))
            {
                // 全局自动化开关(0关/1普通/2大容量优先),点击循环;睡觉结算后对所有充电器生效
                labels.Add(I18n.F("menu.charger_auto", I18n.T("charger.mode_" + BatteryWork.ChargerMode)));
                runs.Add(() =>
                {
                    int mode = BatteryWork.CycleChargerMode();
                    Notice.Show(I18n.F("menu.charger_auto", I18n.T("charger.mode_" + mode)));
                });
            }
            if (BatteryWork.HasBatterySlot(item))
            {
                var machine = item;
                labels.Add(I18n.T(BatteryWork.IsAutoSwap(machine) ? "menu.auto_battery_on" : "menu.auto_battery_off"));
                runs.Add(() => BatteryWork.ToggleAutoSwap(machine));
            }
            if (StashWork.IsBay(item))
            {
                labels.Add(I18n.T("menu.stash_all"));
                runs.Add(StashWork.TransferAll);
                if (StashWork.HasRecord)
                {
                    labels.Add(I18n.T("menu.stash_restore"));
                    runs.Add(StashWork.RestoreAll);
                }
            }
            if (runs.Count == 0) return false;

            foreach (var action in ItemActions.Available(item))
            {
                if (action == HelperLogic.ACT_MOVE) continue; // 移动要配绑定,右键里没有意义
                int a = action;
                labels.Add(I18n.ActionName(a));
                runs.Add(() => HelperLogic.Execute(item, a));
            }
            return NativeMenu.Show(labels, index =>
            {
                try { runs[index](); }
                catch (Exception e) { WarehouseHelperMod.Err("右键功能: " + e); }
            });
        }

        private static GameItem ItemUnderCursor(ItemContextHandler handler)
        {
            try
            {
                Vector3 mp = Input.mousePosition;
                var node = handler.FindContextMenuNode(new Vector2(mp.x, mp.y), handler.overlayCanvas);
                if (node == null) return null;
                return node.GetComponentInParent<GameItemElement>();
            }
            catch { return null; }
        }

        [HarmonyLib.HarmonyPatch(typeof(ItemContextHandler), nameof(ItemContextHandler.OnEventPress))]
        public static class PressPatch
        {
            public static bool Prefix(ItemContextHandler __instance,
                Il2CppSystem.Collections.Generic.ISet<KeyCode> keysPressed)
            {
                try
                {
                    if (NativeMenu.IsOpen) return false;
                    if (keysPressed == null) return true;
                    if (!keysPressed.Cast<Il2CppSystem.Collections.Generic.ICollection<KeyCode>>()
                        .Contains(__instance.triggerKey)) return true;
                    var item = ItemUnderCursor(__instance);
                    if (item == null) return true;
                    return !TryOpen(item);
                }
                catch { return true; }
            }
        }
    }
}
