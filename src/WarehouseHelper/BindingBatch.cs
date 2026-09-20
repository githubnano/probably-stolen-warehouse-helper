using System;
using System.Collections.Generic;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>A new selection replaces the previous selection on this key.</summary>
    public static class BindingBatch
    {
        public static int Replace(List<GameItem> targets, int slot, int action)
        {
            if (slot < 0 || slot >= HelperLogic.SlotCount || action < 0 || action > HelperLogic.ACT_EQUIP)
                throw new ArgumentOutOfRangeException("Invalid binding");

            var eligible = new List<GameItem>();
            var seen = new HashSet<IntPtr>();
            foreach (var item in targets)
                if (item != null && seen.Add(item.Pointer) && HelperLogic.ValidActions(item).Contains(action))
                    eligible.Add(item);
            // Cancelling the menu or choosing an unsupported action must keep the old binding.
            if (eligible.Count == 0) return 0;

            // Clear once per selection, not once per item: multi-selection stays together.
            foreach (var previous in HelperLogic.FindBoundItems(slot)) HelperLogic.ClearBinding(previous);
            foreach (var item in eligible) HelperLogic.SetBinding(item, slot, action);
            return eligible.Count;
        }
    }
}
