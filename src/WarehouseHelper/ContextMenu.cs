using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// 右键菜单扩展:只在仓库助手上生效——右键助手打开功能面板(充电器换电池模式/
    /// 机器换电池开关/一键藏匿/放回原位),后面照常列出原生可用动作。
    /// 其他物品(充电器/机器/暗格等)不加任何自定义入口,原生菜单原样保留。
    /// 挂法:patch ItemContextHandler.OnEventPress,命中助手就 return false 拦掉原生菜单。
    /// </summary>
    public static class ContextMenu
    {
        public static bool TryOpen(GameItem item)
        {
            if (item == null) return false;
            var labels = new List<string>();
            var runs = new List<Action>();

            // 这三个是仓库助手的功能,入口只在助手上:右键助手打开功能面板。
            // 充电器/机器/暗格不加任何自定义入口,原生右键菜单原样保留。
            if (HelperLogic.IsHelper(item))
            {
                labels.Add(I18n.F("menu.charger_auto", I18n.T("charger.mode_" + BatteryWork.ChargerMode)));
                runs.Add(() =>
                {
                    int mode = BatteryWork.CycleChargerMode();
                    Notice.Show(I18n.F("menu.charger_auto", I18n.T("charger.mode_" + mode)));
                });
                labels.Add(I18n.T(Config.MachineAutoSwap?.Value == true ? "menu.machine_auto_on" : "menu.machine_auto_off"));
                runs.Add(() => BatteryWork.ToggleAutoSwap(null));
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
