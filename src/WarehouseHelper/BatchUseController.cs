using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// One native cursor represents the batch. Once the native handler accepts a click,
    /// apply the same native target/activation path to every other bound source once.
    /// No resource-type, empty-container, or consume-one policy is imposed by the mod.
    /// </summary>
    public static class BatchUseController
    {
        private static List<GameItem> _sources;
        private static ItemSelectHandler _handler;
        private static GameItem _anchor;
        private static bool _equipped, _internal, _updating, _updateReset;
        private static int _generation;
        private static PressContext _press;

        public sealed class PressContext
        {
            public List<GameItem> Sources;
            public IntPtr Anchor;
            public int Generation;
            public bool Equipped, NativeReset, Activate;
            public GameItem Target;
        }

        private static bool Same(GameItem a, GameItem b) => a != null && b != null && a.Pointer == b.Pointer;
        private static bool Owns(ItemSelectHandler handler) => _sources != null && _handler != null
            && handler != null && _handler.Pointer == handler.Pointer;

        private static bool Alive(GameItem item) => ItemActions.Check(() => item != null
            && !(item.TryCast<GameItemElement>()?.IsDestroyed() ?? false));

        private static bool Selectable(GameItem item, bool equip) => Alive(item)
            && ItemActions.Check(item.MaySelectSlotItem) && ItemActions.Check(item.CanSelectSlotItem)
            && (!equip || ItemActions.Check(() => GeneralHelper.IsItemEffectivelyOwned(item)));

        private static void Clear()
        {
            _generation++;
            _sources = null;
            _handler = null;
            _anchor = null;
            _press = null;
            _updating = _updateReset = _equipped = false;
        }

        public static void Reset()
        {
            var handler = _handler;
            var anchor = _anchor;
            Clear();
            try { if (handler != null && Same(handler.currentItem, anchor)) handler.OnEventReset(); }
            catch (Exception e) { WarehouseHelperMod.Warn("清理整组使用: " + e.Message); }
        }

        public static void Begin(List<GameItem> sources, bool equip)
        {
            Reset();
            var unique = new List<GameItem>();
            var seen = new HashSet<IntPtr>();
            foreach (var source in sources)
                if (source != null && seen.Add(source.Pointer)) unique.Add(source);
            if (unique.Count == 0) return;
            if (unique.Count == 1) { ItemActions.Select(unique[0], equip); return; }
            _sources = unique;
            _handler = ItemSelectHandler.current;
            _equipped = equip;
            if (!ChooseRepresentative()) { Reset(); return; }

        }

        private static bool ChooseRepresentative()
        {
            if (_handler == null || _sources == null) return false;
            _internal = true;
            try
            {
                foreach (var source in _sources)
                {
                    if (!Selectable(source, _equipped)) continue;
                    if (!ItemActions.Select(source, _equipped) || !Same(_handler.currentItem, source)) continue;
                    _anchor = source;
                    // The native equipped cursor may expire independently of CanSelect.
                    if (_equipped && _handler.ShouldExitEquip()) continue;
                    return true;
                }
                return false;
            }
            finally { _internal = false; }
        }

        private static void FinishPress(PressContext press)
        {
            if (press == null || _press != press) return;
            _press = null;
            if (press.Generation != _generation) return;
            if (press.Target == null && !press.Activate)
            {
                if (press.NativeReset) Clear();
                return;
            }
            _internal = true;
            try
            {
                foreach (var source in press.Sources)
                {
                    if (press.Generation != _generation) break; // Scene changed during an action.
                    if (source.Pointer == press.Anchor) continue; // Native handler already ran it.
                    try
                    {
                        if (!Selectable(source, press.Equipped)) continue;
                        if (press.Activate)
                        {
                            if (source.MayActivateSlotItem() && source.CanActivateSlotItem()) source.ActivateSlotItem();
                        }
                        else
                        {
                            // Keep the original target, even if consuming it reveals another
                            // item under the cursor. Never raycast again for later sources.
                            if (!Alive(press.Target)) break;
                            if (Same(source, press.Target)) continue;
                            ItemBehaviourManager.current?.Target(source, press.Target);
                            if (press.Generation != _generation) break;
                            if (Alive(source) && Alive(press.Target)
                                && source.MayTarget(press.Target) && source.CanTarget(press.Target))
                                source.Target(press.Target);
                        }
                    }
                    catch (Exception e) { WarehouseHelperMod.Warn("整组使用 " + HelperLogic.SafeName(source) + ": " + e.Message); }
                }
            }
            finally { _internal = false; }

            if (press.Generation != _generation) return;
            if (press.Equipped)
            {
                if (!ChooseRepresentative()) Reset();
            }
            else Reset();
        }

        [HarmonyPatch(typeof(ItemSelectHandler), nameof(ItemSelectHandler.OnEventPress))]
        public static class PressPatch
        {
            public static void Prefix(ItemSelectHandler __instance,
                Il2CppSystem.Collections.Generic.ISet<KeyCode> keysPressed, out PressContext __state)
            {
                __state = null;
                if (_internal || _press != null || !Owns(__instance) || !Same(__instance.currentItem, _anchor)
                    || keysPressed == null) return;
                var keys = keysPressed.Cast<Il2CppSystem.Collections.Generic.ICollection<KeyCode>>();
                if (keys.Contains(KeyCode.Mouse1) || !keys.Contains(__instance.key)) return;
                __state = _press = new PressContext
                {
                    Sources = new List<GameItem>(_sources), Anchor = _anchor.Pointer,
                    Generation = _generation, Equipped = _equipped
                };
            }

            public static void Postfix(PressContext __state)
            {
                try { FinishPress(__state); }
                catch (Exception e) { WarehouseHelperMod.Err("整组使用: " + e); Reset(); }
            }

            public static Exception Finalizer(Exception __exception)
            {
                if (__exception != null && _press != null) Reset();
                return __exception;
            }
        }

        [HarmonyPatch(typeof(ItemBehaviourManager), nameof(ItemBehaviourManager.Target), new Type[] { typeof(GameItem), typeof(GameItem) })]
        public static class TargetPatch
        {
            public static void Prefix(GameItem source, GameItem target)
            {
                if (!_internal && _press != null && source != null && source.Pointer == _press.Anchor
                    && _press.Target == null && !_press.Activate) _press.Target = target;
            }
        }

        [HarmonyPatch(typeof(GameItem), nameof(GameItem.ActivateSlotItem))]
        public static class ActivatePatch
        {
            public static void Prefix(GameItem __instance)
            {
                if (!_internal && _press != null && __instance.Pointer == _press.Anchor && _press.Target == null)
                    _press.Activate = true;
            }
        }

        [HarmonyPatch(typeof(ItemSelectHandler), nameof(ItemSelectHandler.OnEventReset))]
        public static class ResetPatch
        {
            public static void Postfix(ItemSelectHandler __instance)
            {
                if (_internal || !Owns(__instance)) return;
                if (_press != null) _press.NativeReset = true;
                else if (_updating && _equipped) _updateReset = true;
                else Clear();
            }
        }

        [HarmonyPatch(typeof(ItemSelectHandler), nameof(ItemSelectHandler.OnEventUpdate))]
        public static class UpdatePatch
        {
            public static void Prefix(ItemSelectHandler __instance, out bool __state)
            {
                __state = false;
                if (_internal || !Owns(__instance)) return;
                if (!Same(__instance.currentItem, _anchor)) { Clear(); return; }
                __state = _updating = true;
                _updateReset = false;
            }

            public static void Postfix(bool __state)
            {
                if (!__state) return;
                _updating = false;
                try { if (_updateReset && !ChooseRepresentative()) Reset(); }
                catch (Exception e) { WarehouseHelperMod.Warn("整组装备刷新: " + e.Message); Reset(); }
            }

            public static Exception Finalizer(Exception __exception)
            {
                _updating = false;
                if (__exception != null && _sources != null) Reset();
                return __exception;
            }
        }
    }
}
