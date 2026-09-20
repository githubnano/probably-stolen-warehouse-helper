using System;
using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>
    /// 所有拖拽交互的统一入口(Kitchen mod 同款方案):
    /// patch GameItem 实例方法 MayTarget / CanTarget / Target。
    /// 绝不往物品上挂 Il2Cpp 委托 —— 原生 GameItem.MayTarget 用反射 InvokeAllReduce
    /// 评估委托链,Il2CppInterop 生成的委托会让它抛 TargetException,污染整个容器拖不动。
    /// 拆解/改装双向匹配(拖着 A 点 B 或拖着 B 点 A 都行);绑定是单向的:
    /// 必须拖着【物品】放到【仓库助手】上(b = 助手),反向不允许。
    /// 执行走双通道(GameItem.Target 实例 + ItemBehaviourManager.Target 静态),
    /// 同一帧同一对物品去重,不会重复消耗。
    /// </summary>
    public static class Interaction
    {
        // 同帧同对去重
        private static int _lastExecFrame = -1;
        private static string _lastExecKey;

        /// <summary>这一对物品是不是我们的交互组合。</summary>
        public static bool IsOurPair(GameItem a, GameItem b)
        {
            try
            {
                if (a == null || b == null) return false;
                if (ReferenceEquals(a, b)) return false;

                // 螺丝刀 ↔ 模组提取器 = 拆解
                if ((Disassembly.IsScrewdriver(a) && Disassembly.IsExtractor(b))
                    || (Disassembly.IsScrewdriver(b) && Disassembly.IsExtractor(a)))
                    return true;

                // 材料 ↔ 零件堆 = 改装(仍需缺这种材料)
                var pile = Conversion.IsPile(a) ? a : (Conversion.IsPile(b) ? b : null);
                if (pile != null)
                {
                    var other = ReferenceEquals(pile, a) ? b : a;
                    return Conversion.MayAcceptPublic(pile, other);
                }

                // 物品 → 仓库助手 = 绑定(单向:拖动的 a 是物品,落点 b 必须是助手;
                // 绑定存在物品 tag 里,所以一个键能绑任意多物品)
                if (HelperLogic.IsHelper(b))
                    return HelperLogic.BindablePublic(b, a);

                return false;
            }
            catch { return false; }
        }

        /// <summary>原版 Target 跑完后执行我们的动作(每种动作自身防重复执行)。</summary>
        public static void Execute(GameItem a, GameItem b)
        {
            var extractor = Disassembly.IsExtractor(a) ? a : (Disassembly.IsExtractor(b) ? b : null);
            if (extractor != null)
            {
                var tool = ReferenceEquals(extractor, a) ? b : a;
                if (Disassembly.IsScrewdriver(tool)) { Disassembly.DoDisassemble(extractor); return; }
            }

            var pile = Conversion.IsPile(a) ? a : (Conversion.IsPile(b) ? b : null);
            if (pile != null)
            {
                var mat = ReferenceEquals(pile, a) ? b : a;
                if (Conversion.MayAcceptPublic(pile, mat)) { Conversion.AcceptPublic(pile, mat); return; }
            }

            // 绑定:落点 b 是助手,拖动的 a 是被绑物品;多选组拖时收齐全部选中物品
            if (HelperLogic.IsHelper(b) && HelperLogic.BindablePublic(b, a))
                HelperLogic.OpenBindPublic(b, HelperLogic.CollectBindTargets(b, a));
        }

        private static string PairKey(GameItem a, GameItem b)
        {
            int ua = SafeUid(a), ub = SafeUid(b);
            return ua <= ub ? ua + "|" + ub : ub + "|" + ua;
        }

        private static int SafeUid(GameItem i)
        {
            try { return i.uniqueId; } catch { return 0; }
        }

        private static void ExecuteOnce(GameItem a, GameItem b)
        {
            try
            {
                int frame = UnityEngine.Time.frameCount;
                string key = PairKey(a, b);
                if (frame == _lastExecFrame && key == _lastExecKey) return; // 双通道同帧去重
                _lastExecFrame = frame;
                _lastExecKey = key;

                Execute(a, b);
            }
            catch (Exception e) { WarehouseHelperMod.Err("交互执行: " + e.Message); }
        }

        [HarmonyLib.HarmonyPatch(typeof(GameItem), "MayTarget", new Type[] { typeof(GameItem) })]
        public static class MayTargetPatch
        {
            public static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
            {
                if (__result) return;
                try
                {
                    if (IsOurPair(__instance, targetItem))
                    {
                        __result = true;

                    }
                }
                catch { }
            }
        }

        [HarmonyLib.HarmonyPatch(typeof(GameItem), "CanTarget", new Type[] { typeof(GameItem) })]
        public static class CanTargetPatch
        {
            public static void Postfix(GameItem __instance, GameItem targetItem, ref bool __result)
            {
                if (__result) return;
                try { if (IsOurPair(__instance, targetItem)) __result = true; } catch { }
            }
        }

        [HarmonyLib.HarmonyPatch(typeof(GameItem), "Target", new Type[] { typeof(GameItem) })]
        public static class TargetPatch
        {
            // 前缀只记录匹配,不跳过原版 —— 原版的分发/清理必须完整跑完
            public static void Prefix(GameItem __instance, GameItem targetItem, out bool __state)
            {
                __state = false;
                try { __state = IsOurPair(__instance, targetItem); } catch { }
            }

            public static void Postfix(GameItem __instance, GameItem targetItem, bool __state)
            {
                if (!__state) return;
                ExecuteOnce(__instance, targetItem);
            }
        }

        /// <summary>备用通道:有的拖放可能走管理器分发,不经过实例方法。</summary>
        [HarmonyLib.HarmonyPatch(typeof(ItemBehaviourManager), "Target", new Type[] { typeof(GameItem), typeof(GameItem) })]
        public static class MgrTargetPatch
        {
            public static void Postfix(GameItem source, GameItem target)
            {
                try
                {
                    if (IsOurPair(source, target))
                        ExecuteOnce(source, target);
                }
                catch { }
            }
        }

    }
}
