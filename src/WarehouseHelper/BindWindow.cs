using System;
using System.Collections.Generic;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>One action menu and one hotkey menu for the entire dragged selection.</summary>
    public static class BindWindow
    {
        public static bool IsOpen { get; private set; }
        public static int Generation { get; private set; }
        private static Action _refreshLocale;

        public static void RefreshLocale()
        {
            if (IsOpen) _refreshLocale?.Invoke();
        }

        private static void Finish()
        {
            IsOpen = false;
            _refreshLocale = null;
        }

        public static void Reset()
        {
            Generation++;
            Finish();
            NativeMenu.Reset();
        }

        public static void Open(GameItem helper, List<GameItem> items)
        {
            if (IsOpen) return;
            var targets = new List<GameItem>();
            var seen = new HashSet<IntPtr>();
            if (items != null)
                foreach (var item in items)
                    if (item != null && seen.Add(item.Pointer) && HelperLogic.BindablePublic(helper, item))
                        targets.Add(item);
            if (targets.Count == 0) return;
            IsOpen = true;
            try { OpenActionMenu(helper, targets); }
            catch (Exception e) { Fail(e); }
        }

        private static void OpenActionMenu(GameItem helper, List<GameItem> targets)
        {
            _refreshLocale = () => OpenActionMenu(helper, targets);
            var available = new HashSet<int>();
            foreach (var item in targets)
                foreach (int action in HelperLogic.ValidActions(item)) available.Add(action);
            var actions = new List<int>();
            var labels = new List<string>();
            foreach (int action in ItemActions.MenuOrder)
                if (available.Contains(action)) { actions.Add(action); labels.Add(I18n.ActionName(action)); }
            bool bound = targets.Count == 1 && HelperLogic.GetBinding(targets[0], out _, out _);
            if (bound) { actions.Add(-1); labels.Add(I18n.T("binding.clear")); }
            if (!NativeMenu.Show(labels, index =>
            {
                try
                {
                    int action = actions[index];
                    if (action == -1)
                    {
                        HelperLogic.ClearBinding(targets[0]);
                        Finish();
                        Notice.Show(I18n.F("binding.cleared", HelperLogic.SafeName(targets[0])));
                    }
                    else OpenSlotMenu(helper, targets, action);
                }
                catch (Exception e) { Fail(e); }
            }, Finish)) Fail(null);
        }

        private static void OpenSlotMenu(GameItem helper, List<GameItem> targets, int action)
        {
            _refreshLocale = () => OpenSlotMenu(helper, targets, action);
            var labels = new List<string>();
            for (int i = 0; i < HelperLogic.SlotCountOf(helper); i++) labels.Add(KeyName(i));
            if (!NativeMenu.Show(labels, slot =>
            {
                Finish();
                try
                {
                    int count = BindingBatch.Replace(targets, slot, action);
                    string who = targets.Count == 1 ? HelperLogic.SafeName(targets[0]) : I18n.F("binding.count", count);
                    if (count > 0)
                        Notice.Show(I18n.F("binding.success", who, KeyName(slot), I18n.ActionName(action)));

                }
                catch (Exception e) { Fail(e); }
            }, Finish)) Fail(null);
        }

        private static string KeyName(int slot)
        {
            string modifier = Config.HotkeyModifier.Value;
            return (string.Equals(modifier, "None", StringComparison.OrdinalIgnoreCase) ? "" : modifier + "+") + (slot + 1);
        }

        private static void Fail(Exception error)
        {
            Finish();
            NativeMenu.Reset();
            if (error != null) WarehouseHelperMod.Err("绑定菜单: " + error);
            else WarehouseHelperMod.Warn("绑定菜单尚未就绪");
        }
    }
}
