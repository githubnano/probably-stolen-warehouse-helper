using System;
using System.Collections.Generic;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>
    /// 物品标红/取消。用原生格位高亮:GameInventory.HighlightSlot(SlotMarker, Red, true),
    /// 就是拖放时无效格子的那种红底。高亮节点被原生 ClearHighlight/拖拽清掉后低频重画;
    /// 取消时销毁自己生成的高亮节点。物品换库存后按新库存重画。
    /// 放不下的物品若在容器里,容器(以及上层容器,最多 4 层)也一并标红,方便不开窗口定位;
    /// 祖先标红在物品全部移走后自动撤销。
    /// 注意:直接摆在店面地板/墙洞这类世界渲染处的物品没有格子窗口,红底不可见,但计数飘字照常。
    /// </summary>
    public static class RedMark
    {
        private class Entry
        {
            public GameItem Item;
            public GameInventory Inv;
            public bool Ancestor;
            public readonly List<TreeNodeRender> Nodes = new();

            public bool Alive()
            {
                foreach (var n in Nodes)
                    if (n != null) return true; // Il2Cpp 对象被销毁后 == null
                return false;
            }
        }

        private static readonly List<Entry> _entries = new();
        private static float _nextApply;

        /// <summary>标红物品,并沿容器链标红它的各级容器(不开窗口也能定位到它在哪个箱子里)。</summary>
        public static void Mark(GameItem item)
        {
            if (item == null) return;
            Add(item, false);
            var inv = item.parentInventory;
            for (int depth = 0; inv != null && depth < 4; depth++)
            {
                GameItem parentItem = null;
                try { parentItem = inv.GetParentItem(); } catch { }
                if (parentItem == null) break;
                Add(parentItem, true);
                inv = parentItem.parentInventory;
            }
        }

        private static void Add(GameItem item, bool ancestor)
        {
            foreach (var e in _entries)
                if (e.Item != null && e.Item.Pointer == item.Pointer)
                {
                    if (!ancestor && e.Ancestor) e.Ancestor = false; // 升级为直接标记
                    return;
                }
            var entry = new Entry { Item = item, Ancestor = ancestor };
            _entries.Add(entry);
            Apply(entry);
        }

        public static void ClearAll()
        {
            foreach (var e in _entries) ClearNodes(e, true);
            _entries.Clear();
        }

        public static void Tick()
        {
            if (_entries.Count == 0) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextApply) return;
            _nextApply = now + 0.5f;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Item == null || (e.Ancestor && !IsNeededAncestor(e.Item)))
                { ClearNodes(e, true); _entries.RemoveAt(i); continue; }
                if (e.Item.parentInventory != e.Inv) { ClearNodes(e, true); e.Inv = null; } // 换了库存,先清旧的
                if (!e.Alive()) Apply(e); // 高亮被原生清掉了就重画
            }
        }

        /// <summary>这个祖先容器里是否还有被直接标红的物品。</summary>
        private static bool IsNeededAncestor(GameItem ancestor)
        {
            foreach (var e in _entries)
            {
                if (e.Ancestor || e.Item == null) continue;
                var inv = e.Item.parentInventory;
                for (int depth = 0; inv != null && depth < 4; depth++)
                {
                    GameItem parentItem = null;
                    try { parentItem = inv.GetParentItem(); } catch { }
                    if (parentItem == null) break;
                    if (parentItem.Pointer == ancestor.Pointer) return true;
                    inv = parentItem.parentInventory;
                }
            }
            return false;
        }

        private static void Apply(Entry e)
        {
            try
            {
                var inv = e.Item.parentInventory;
                if (inv == null) return;
                e.Inv = inv;
                var slot = new SlotMarker
                {
                    item = e.Item,
                    inventory = inv,
                    itemGridShape = e.Item.modifiedShape
                };
                var nodes = inv.HighlightSlot(slot, RenderHandler.BackgroundColor.Red, true);
                if (nodes != null)
                    foreach (var n in nodes)
                        if (n != null) e.Nodes.Add(n);
            }
            catch { }
        }

        private static void ClearNodes(Entry e, bool clearInventory)
        {
            foreach (var n in e.Nodes)
                try { if (n != null && n.gameObject != null) UnityEngine.Object.Destroy(n.gameObject); } catch { }
            e.Nodes.Clear();
            // 兜底:节点可能被原生回收过,按库存整体清一次
            if (clearInventory)
                try { e.Inv?.ClearHighlight(); } catch { }
            e.Inv = null;
        }
    }
}
