using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// Initialize the complete native drag gesture after InputActionManager's current update.
    /// Native Drag performs raycasts, cross-inventory targeting and red/green cell highlights.
    /// Never pin lastInventory to the source, and never suppress AbortDrag / OnEventReset.
    /// </summary>
    public static class MoveController
    {
        private static readonly MoveGesture Gesture = new();
        private static readonly MoveCancelInput CancelInput = new();
        private static ItemMouseDragHandler _handler;
        private static ItemMultiSelectHandler _group;
        private static List<GameItem> _pending;
        private static int _pendingSlot;
        private static bool _finishRequested;
        public static bool BlocksHotkeys => CancelInput.Blocks(Time.frameCount);

        private static bool Owns(ItemMouseDragHandler handler) => Gesture.Active && handler != null
            && _handler != null && handler.Pointer == _handler.Pointer;

        private static bool Owns(ItemMultiSelectHandler handler) => Gesture.Active && handler != null
            && _group != null && handler.Pointer == _group.Pointer;

        private static bool HasKey(Il2CppSystem.Collections.Generic.ISet<KeyCode> keys, KeyCode key)
            => keys != null && keys.Cast<Il2CppSystem.Collections.Generic.ICollection<KeyCode>>().Contains(key);

        public static void Reset()
        {
            Cancel();
            CancelInput.Reset();
        }

        private static void ClearSession()
        {
            Gesture.Reset();
            _handler = null;
            _group = null;
            _pending = null;
            _finishRequested = false;
        }

        private static void CheckCancelInput()
        {
            if (CancelInput.Observe(Time.frameCount, Gesture.Active || _pending != null,
                Input.GetMouseButton(1), Input.GetMouseButtonDown(1), Input.GetKeyDown(KeyCode.Escape)))
            {
                Cancel();

            }
        }

        public static void Tick()
        {
            CheckCancelInput();
            if (!Gesture.Active) return;
            try
            {
                bool valid = _group != null
                    ? _group == ItemMultiSelectHandler.current && _group.state == ItemMultiSelectHandler.State.GroupDragging
                    : _handler != null && _handler == ItemMouseDragHandler.current && _handler.IsDraggingItem;
                if (!valid)
                { Cancel(); return; }
                Gesture.ObserveMouse(Time.frameCount, Input.GetMouseButton(0), Input.GetMouseButtonDown(0));
            }
            catch (Exception e) { WarehouseHelperMod.Warn("移动状态: " + e.Message); Cancel(); }
        }

        public static bool TryFinish(int slot)
        {
            if (!Gesture.MatchesSlot(slot)) return false;
            _finishRequested = true;
            return true;
        }

        public static void Request(GameItem target, int slot)
            => Request(new List<GameItem> { target }, slot);

        public static void Request(List<GameItem> targets, int slot)
        {
            if (targets == null || targets.Count == 0 || _pending != null || Gesture.Active
                || BlocksHotkeys || Input.GetMouseButton(1)) return;
            var drag = ItemMouseDragHandler.current;
            if (drag == null || drag.IsDraggingItem) return;
            if (ItemMultiSelectHandler.current?.state == ItemMultiSelectHandler.State.GroupDragging) return;
            _pending = new List<GameItem>(targets);
            _pendingSlot = slot;
        }

        private static void Pump()
        {
            if (_finishRequested)
            {
                _finishRequested = false;
                Finish();
                return;
            }
            var targets = _pending;
            int slot = _pendingSlot;
            _pending = null;
            if (targets == null) return;
            BatchUseController.Reset();
            if (targets.Count == 1) Start(targets[0], slot);
            else StartGroup(targets, slot);
        }

        private static void StartGroup(List<GameItem> targets, int slot)
        {
            try
            {
                var group = ItemMultiSelectHandler.current;
                if (group == null || BindWindow.IsOpen) return;
                var elements = new List<GameItemElement>();
                var seen = new HashSet<IntPtr>();
                foreach (var item in targets)
                {
                    if (item == null || !seen.Add(item.Pointer)) continue;
                    var element = item.TryCast<GameItemElement>();
                    if (element != null && !element.IsDestroyed() && element.fakeMiddle != null
                        && item.parentInventory != null && item.MayRemove() && item.MaxNumRemove() > 0)
                        elements.Add(element);
                }
                if (elements.Count == 0) return;
                if (elements.Count == 1) { Start(elements[0], slot); return; }

                group.TryInit();
                group.Enable();
                group.OnEventReset();
                foreach (var element in elements) group.selectedItems.Add(element);
                // FinalizeBand records each item's original inventory in selectedHomes.
                // StartGroupDrag and PruneSelection rely on these exact native records.
                group.FinalizeBand();
                _group = group;
                group.StartGroupDrag();
                if (group.state != ItemMultiSelectHandler.State.GroupDragging) { Cancel(); return; }
                Gesture.Begin(slot, Time.frameCount, Input.GetMouseButton(0));

                // Move the formation to the cursor, including when its source windows are
                // closed. Keep native relative offsets instead of overlapping the items.
                var first = group.selectedItems[0];
                var anchor = group.dragOffsets[first];
                var mouse = first.fakeMiddle.parent.rectTransform.InverseTransformPoint(Input.mousePosition);
                foreach (var element in group.selectedItems)
                {
                    Vector2 offset = group.dragOffsets[element] - anchor;
                    group.dragOffsets[element] = offset;
                    element.SetCenteredPosition(mouse.x + offset.x, mouse.y + offset.y);
                }
                UpdateGroup(group);

            }
            catch (Exception e) { WarehouseHelperMod.Err("整组移动开始: " + e); Cancel(); }
        }

        private static void UpdateGroup(ItemMultiSelectHandler group)
        {
            group.PruneSelection();
            if (group.state != ItemMultiSelectHandler.State.GroupDragging) return;
            group.MoveGhosts();
            group.UpdateDropTarget();
            if (group.precisePlacementActive) group.ClearSelectionHighlightNodes();
            else group.RefreshSelectionHighlight();
        }

        private static void Start(GameItem target, int slot)
        {
            try
            {
                var drag = ItemMouseDragHandler.current;
                var element = target.TryCast<GameItemElement>();
                if (drag == null || drag.IsDraggingItem || BindWindow.IsOpen) return;
                if (element == null || element.IsDestroyed() || element.handler == null || element.fakeMiddle == null)
                { Notice.Show(I18n.T("move.not_ready")); return; }
                if (target.parentInventory == null || !target.MayRemove() || target.MaxNumRemove() < 1)
                { Notice.Show(I18n.F("move.unavailable", HelperLogic.SafeName(target))); return; }

                // Enable may call OnEventReset. Do it BEFORE assigning a new gesture.
                drag.TryInit();
                drag.Enable();
                drag.OnEventReset();
                drag.currentItem = element;
                drag.lastMouseClickedPosition = Input.mousePosition;
                // OnEventMouseMove returns immediately if this key is None (old mod left it unset).
                drag.lastMouseClickedKey = drag.key;
                drag.lastItem = null;
                drag.lastInventory = null;
                drag.lastSlot = null;
                _handler = drag;
                drag.StartDrag();
                if (!drag.IsDraggingItem) { drag.OnEventReset(); ClearSession(); return; }

                Gesture.Begin(slot, Time.frameCount, Input.GetMouseButton(0));
                // Native StartDrag -> ToggleFakeBackground(true) moves only fakeMiddle to
                // the overlay root. Its source inventory can remain closed/inactive.
                // Snap using that overlay's coordinates, never the hidden source's position.
                var point = element.fakeMiddle.parent.rectTransform.InverseTransformPoint(Input.mousePosition);
                element.SetCenteredPosition(point.x, point.y);
                drag.OnEventUpdate();

            }
            catch (Exception e) { WarehouseHelperMod.Err("移动开始: " + e); Cancel(); }
        }

        private static void Finish()
        {
            var group = _group;
            if (group != null && Gesture.Active)
            {
                try
                {
                    UpdateGroup(group);
                    if (group.state == ItemMultiSelectHandler.State.GroupDragging) group.EndGroupDrag();
                    group.OnEventReset();

                }
                catch (Exception e) { WarehouseHelperMod.Err("整组移动落位: " + e); Cancel(); }
                finally { ClearSession(); }
                return;
            }
            var drag = _handler;
            if (drag == null || !Gesture.Active) { ClearSession(); return; }
            try
            {
                if (drag.IsDraggingItem)
                {
                    // Resolve the destination even if the mouse has stopped moving this frame.
                    drag.Drag(Vector3.zero);
                    drag.EndDrag();
                }
                drag.OnEventReset();

            }
            catch (Exception e) { WarehouseHelperMod.Err("移动落位: " + e); Cancel(); }
            finally { ClearSession(); }
        }

        private static void Cancel()
        {
            var drag = _handler;
            var group = _group;
            ClearSession();
            // Native OnEventReset clears the destination before EndDrag. The real item
            // was never removed, and native validation restores its original appearance.
            try
            {
                if (group != null) group.OnEventReset(); // RestoreGhosts; never EndGroupDrag on cancel.
                if (drag != null) { drag.Cancel(); drag.OnEventReset(); }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("取消移动: " + e.Message); }
        }

        [HarmonyPatch(typeof(InputActionManager), nameof(InputActionManager.Update))]
        public static class DispatchPatch
        {
            public static bool Prefix(InputActionManager __instance, out bool __state)
            {
                // Run before any native handler can rotate, drop or open a context menu.
                CheckCancelInput();
                __state = !NativeMenu.BlocksGameInput && !CancelInput.Blocks(Time.frameCount);
                if (!__state) __instance.lastMousePosition = Input.mousePosition;
                return __state;
            }
            public static void Postfix(bool __state) { if (__state) Pump(); }
        }

        [HarmonyPatch(typeof(ItemMouseDragHandler), nameof(ItemMouseDragHandler.OnEventPress))]
        public static class PressPatch
        {
            public static void Prefix(ItemMouseDragHandler __instance,
                Il2CppSystem.Collections.Generic.ISet<KeyCode> keysPressed)
            {
                if (Owns(__instance))
                    Gesture.ObserveMouse(Time.frameCount, Input.GetMouseButton(0), HasKey(keysPressed, __instance.key));
            }
        }

        [HarmonyPatch(typeof(ItemMouseDragHandler), nameof(ItemMouseDragHandler.OnEventRelease))]
        public static class ReleasePatch
        {
            public static bool Prefix(ItemMouseDragHandler __instance,
                Il2CppSystem.Collections.Generic.ISet<KeyCode> keysReleased)
            {
                // Ignore the previous UI click's release, not arbitrary EndDrag / cancellation calls.
                if (!Owns(__instance) || !HasKey(keysReleased, __instance.lastMouseClickedKey)
                    || Gesture.CanRelease(Time.frameCount)) return true;
                if (HasKey(keysReleased, __instance.scrollMult10Key)) __instance.isMult10 = false;
                if (HasKey(keysReleased, __instance.scrollMult100Key)) __instance.isMult100 = false;
                if (HasKey(keysReleased, __instance.scrollIgnoreKey)) __instance.isScrollIgnore = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemMouseDragHandler), nameof(ItemMouseDragHandler.OnEventUpdate))]
        public static class UpdatePatch
        {
            public static void Prefix(ItemMouseDragHandler __instance)
            {
                if (!Owns(__instance) || !__instance.IsDraggingItem || __instance.IsDragTargetDestroyed()) return;
                try { __instance.Drag(Vector3.zero); }
                catch (Exception e) { WarehouseHelperMod.Warn("移动目标刷新: " + e.Message); Cancel(); }
            }
        }

        [HarmonyPatch(typeof(ItemMouseDragHandler), nameof(ItemMouseDragHandler.EndDrag))]
        public static class EndPatch
        {
            public static void Postfix(ItemMouseDragHandler __instance) { if (Owns(__instance)) ClearSession(); }
        }
        [HarmonyPatch(typeof(ItemMouseDragHandler), nameof(ItemMouseDragHandler.AbortDrag))]
        public static class AbortPatch
        {
            public static void Postfix(ItemMouseDragHandler __instance) { if (Owns(__instance)) ClearSession(); }
        }
        [HarmonyPatch(typeof(ItemMouseDragHandler), nameof(ItemMouseDragHandler.OnEventReset))]
        public static class ResetPatch
        {
            public static void Postfix(ItemMouseDragHandler __instance) { if (Owns(__instance)) ClearSession(); }
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), nameof(ItemMultiSelectHandler.OnEventPress))]
        public static class GroupPressPatch
        {
            public static void Prefix(ItemMultiSelectHandler __instance,
                Il2CppSystem.Collections.Generic.ISet<KeyCode> keysPressed)
            {
                if (Owns(__instance))
                    Gesture.ObserveMouse(Time.frameCount, Input.GetMouseButton(0), HasKey(keysPressed, __instance.key));
            }
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), nameof(ItemMultiSelectHandler.OnEventRelease))]
        public static class GroupReleasePatch
        {
            public static bool Prefix(ItemMultiSelectHandler __instance,
                Il2CppSystem.Collections.Generic.ISet<KeyCode> keysReleased)
                => !Owns(__instance) || !HasKey(keysReleased, __instance.key) || Gesture.CanRelease(Time.frameCount);
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), nameof(ItemMultiSelectHandler.OnEventUpdate))]
        public static class GroupUpdatePatch
        {
            public static bool Prefix(ItemMultiSelectHandler __instance, ref bool __result)
            {
                if (!Owns(__instance)) return true;
                // Native group update cancels whenever Mouse0 is not held. A keyboard
                // gesture instead ends only on a fresh click, its hotkey, or cancellation.
                try
                {
                    UpdateGroup(__instance);
                    __result = Owns(__instance) && __instance.state == ItemMultiSelectHandler.State.GroupDragging;
                }
                catch (Exception e) { WarehouseHelperMod.Warn("整组移动刷新: " + e.Message); Cancel(); __result = false; }
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), nameof(ItemMultiSelectHandler.EndGroupDrag))]
        public static class GroupEndPatch
        {
            public static void Postfix(ItemMultiSelectHandler __instance) { if (Owns(__instance)) ClearSession(); }
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), nameof(ItemMultiSelectHandler.OnEventReset))]
        public static class GroupResetPatch
        {
            public static void Postfix(ItemMultiSelectHandler __instance) { if (Owns(__instance)) ClearSession(); }
        }
    }
}
