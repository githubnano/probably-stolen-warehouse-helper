using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>Resize saved helpers before the game builds inventory occupancy from saved shapes.</summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
    public static class HelperLayout
    {
        public static void Prefix(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems)
        {
            if (savedItems == null) return;
            RemoveRetiredManuals(savedItems);
            foreach (var saved in savedItems)
            {
                if (saved == null || (saved.identifier != Items.HelperBasicId && saved.identifier != Items.HelperAdvId))
                    continue;
                try
                {
                    // Both the base shape and the placed shape are restored by DecodeNodes.
                    // Preserve the saved position, rotation and flip while shrinking their filled area.
                    var shape = Resize(saved.itemShape);
                    var placedShape = Resize(saved.itemModifiedShape);
                    saved.itemShape = shape;
                    saved.itemModifiedShape = placedShape;
                }
                catch (Exception e) { WarehouseHelperMod.Warn("助手尺寸更新: " + e.Message); }
            }
        }

        private static void RemoveRetiredManuals(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems)
        {
            // v0.32-v0.33.1 had a separate paper item. Remove only that retired item
            // and its paired parent links before the native decoder resolves factories.
            var retired = new HashSet<long>();
            foreach (var saved in savedItems)
                if (saved != null && saved.identifier == "wh_helper_manual") retired.Add(saved.uuid);
            if (retired.Count == 0) return;
            foreach (var saved in savedItems)
            {
                if (saved?.childItems == null) continue;
                for (int i = saved.childItems.Count - 1; i >= 0; i--)
                {
                    if (!retired.Contains(saved.childItems[i])) continue;
                    saved.childItems.RemoveAt(i);
                    if (saved.childItemInventoryNode != null && i < saved.childItemInventoryNode.Count)
                        saved.childItemInventoryNode.RemoveAt(i);
                }
            }
            for (int i = savedItems.Count - 1; i >= 0; i--)
                if (savedItems[i]?.identifier == "wh_helper_manual") savedItems.RemoveAt(i);
        }

        private static GridShapeBuilder Resize(GridShapeBuilder previous)
        {
            if (previous != null && previous.width == Items.HelperCells && previous.height == Items.HelperCells)
                return previous;
            var result = new GridShapeBuilder(Items.HelperCells, Items.HelperCells);
            result.SetDataFill(Items.HelperCells, Items.HelperCells, 1);
            result.SetDataOutside(0);
            if (previous != null)
                result.SetTransform(previous.minX, previous.minY, previous.flipped, previous.orientation);
            return result;
        }
    }
}
