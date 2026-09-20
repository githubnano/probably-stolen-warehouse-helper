using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace WarehouseHelper
{
    public static class HelperHotkeys
    {
        public static bool NumberDown(int slot) => Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + slot))
            || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + slot));

        // ToolboxHelper reads raw number keys outside InputActionManager, so blocking
        // the drag dispatch alone cannot prevent its second response to Alt+number.
        [HarmonyPatch(typeof(ToolboxHelper), nameof(ToolboxHelper.HandleHotkeys))]
        public static class ToolboxPatch
        {
            public static bool Prefix()
            {
                if (NativeMenu.BlocksGameInput || MoveController.BlocksHotkeys) return false;
                if (!Config.ModifierHeld()) return true;
                for (int slot = 0; slot < HelperLogic.SlotCount; slot++)
                    if (NumberDown(slot)) return false;
                return true;
            }
        }
    }
}
