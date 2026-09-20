using System;
using Il2Cpp;

namespace WarehouseHelper
{
    public static class LegacyItemCleanup
    {
        /// <summary>进场景后清掉历史版本造出的坏物品(错误 identifier 的无形状残次品,会卡死拖拽)。</summary>
        public static void CleanupBrokenItems()
        {
            MelonLoader.MelonCoroutines.Start(CleanupRoutine());
        }

        private static System.Collections.IEnumerator CleanupRoutine()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                yield return new UnityEngine.WaitForSeconds(2f + attempt * 3f);
                int n = ScanAndDestroyBroken();
                if (n > 0) WarehouseHelperMod.Warn($"清理坏物品 x{n} (第 {attempt + 1} 轮)");
            }
        }

        private static int ScanAndDestroyBroken()
        {
            int count = 0;
            try
            {
                var roots = GraphHandler.windowGraphNodeRoots;
                if (roots == null) return 0;
                var filter = (Il2CppSystem.Func<GameItem, bool>)(System.Func<GameItem, bool>)(i =>
                {
                    try { return i != null && i.identifier == "module_extractor_adv"; }
                    catch { return false; }
                });
                var pass = (Il2CppSystem.Func<GameItem, bool>)(System.Func<GameItem, bool>)(i => true);
                foreach (var w in roots)
                {
                    var child = w?.child;
                    if (child == null) continue;
                    var found = GraphUtils.FindAllChildrenType<GameItem>(child, filter, pass);
                    if (found == null) continue;
                    foreach (var f in found)
                    {
                        try { f.Destroy(); count++; } catch { }
                    }
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("清理坏物品: " + e.Message); }
            return count;
        }
    }
}
