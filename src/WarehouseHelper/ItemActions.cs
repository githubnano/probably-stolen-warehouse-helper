using System;
using System.Collections.Generic;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>
    /// Verified against native ItemContextHandler.UpdateConditions / TriggerButton (0.46D).
    /// Context Use is MaySelectSlotItem -> TrySelectItem, not ItemBehaviourManager.Use.
    /// Binding checks capability; execution rechecks availability like the original menu.
    /// </summary>
    public static class ItemActions
    {
        public static readonly int[] MenuOrder = {
            HelperLogic.ACT_USE, HelperLogic.ACT_EQUIP, HelperLogic.ACT_ACTIVATE,
            HelperLogic.ACT_TOGGLE, HelperLogic.ACT_OPEN, HelperLogic.ACT_UNLOAD, HelperLogic.ACT_MOVE
        };

        public static List<int> Available(GameItem item)
        {
            var actions = new List<int>();
            if (item == null) return actions;
            if (Check(item.MaySelectSlotItem))
            {
                actions.Add(HelperLogic.ACT_USE);
                actions.Add(HelperLogic.ACT_EQUIP);
            }
            if (Check(item.MayActivateSlotItem)) actions.Add(HelperLogic.ACT_ACTIVATE);
            if (Check(item.MayToggleSlotItem)) actions.Add(HelperLogic.ACT_TOGGLE);
            if (Check(() => item.contentWindow != null))
            {
                actions.Add(HelperLogic.ACT_OPEN);
                actions.Add(HelperLogic.ACT_UNLOAD);
            }
            actions.Add(HelperLogic.ACT_MOVE);
            return actions;
        }

        public static bool CanOpen(GameItem item) => Check(() => item.contentWindow != null
            && !item.contentWindow.isLocked && GeneralHelper.IsItemOwned(item));

        public static bool Select(GameItem item, bool equip)
        {
            var handler = ItemSelectHandler.current;
            if (handler == null || !Check(item.MaySelectSlotItem) || !Check(item.CanSelectSlotItem))
                return false;
            handler.TryInit();
            handler.Enable();
            // Opening the real context menu resets the older selection before TriggerButton.
            // TrySelectItem otherwise refuses any new item while currentItem is still set.
            if (!equip) handler.OnEventReset();
            if (equip) handler.TryEquipItem(item);
            else handler.TrySelectItem(item);
            return true;
        }

        public static bool Check(Func<bool> predicate)
        {
            try { return predicate(); }
            catch (Exception e) { WarehouseHelperMod.Warn("物品动作判定: " + e.Message); return false; }
        }
    }
}
