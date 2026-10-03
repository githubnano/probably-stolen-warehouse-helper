using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// 右键菜单扩展:右键仓库助手时,用 NativeMenu(原生 UI 控件自建菜单,与原版菜单同款)
    /// 替代原生右键菜单,列出助手功能;其他物品保持原生菜单不变。
    /// 原生按钮网格是建窗时定死的(实测无法后挂,挂进去不排版),所以不往原版菜单里塞行。
    /// 物品命中用原版 RenderHandler.RaycastElement(和原生右键同一套射线);
    /// 挂法:ItemContextHandler.OnEventPress 前缀,命中助手就 return false 拦掉原生菜单。
    /// </summary>
    public static class ContextMenu
    {
        public static bool IsOpen => NativeMenu.IsOpen;

        private static bool OpenPanel()
        {
            var labels = new List<string>();
            var runs = new List<Action>();
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
            return NativeMenu.Show(labels, index =>
            {
                try { runs[index](); }
                catch (Exception e) { WarehouseHelperMod.Err("右键功能: " + e); }
            });
        }

        /// <summary>测试钩子:不依赖右键,直接开面板(探针截图验证用)。</summary>
        public static bool DebugOpen() => OpenPanel();

        public static void Reset() { }

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
                    Vector3 mp = Input.mousePosition;
                    var el = RenderHandler.RaycastElement<GameItemElement>(new Vector2(mp.x, mp.y), null);
                    if (el == null || !HelperLogic.IsHelper(el)) return true;
                    return !OpenPanel();
                }
                catch (Exception e) { WarehouseHelperMod.Warn("WH-ctx press: " + e.Message); return true; }
            }
        }
    }
}
