using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// 物品注册编排 + 共享工厂辅助。
    /// 每个物品一个定义文件(Items/ 目录,继承 WhItem):自带 identifier、工厂、tooltip。
    /// 新增物品 = Items/ 下新建一个类 + 在下面 _defs 里登记一行。
    /// 注册走 ModItemDirectory.InitDirectory patch(Kitchen/PSPDA 同款,见关键坑 12);
    /// tooltip 派发走 TooltipTail -> Items.Find(id).AppendTooltip。
    /// </summary>
    public static class Items
    {
        // identifier 写存档,改了就认不出旧物品,永远别动
        public const string PartsBasicId = "wh_parts_basic";
        public const string PartsAdvId = "wh_parts_adv";
        public const string HelperBasicId = "wh_helper_basic";
        public const string HelperAdvId = "wh_helper_adv";
        public const int HelperCells = 3;
        public const int HelperPixels = HelperCells * 16;

        /// <summary>全部自定义物品定义(新增物品在这登记)。</summary>
        private static readonly List<WhItem> _defs = new List<WhItem>
        {
            new PartsBasicItem(),
            new PartsAdvItem(),
            new HelperBasicItem(),
            new HelperAdvItem(),
        };

        public static IReadOnlyList<WhItem> Defs => _defs;

        public static WhItem Find(string id)
        {
            if (id == null) return null;
            foreach (var d in _defs) if (d.Id == id) return d;
            return null;
        }

        private static bool _validatedIds;

        /// <summary>枚举场景中所有 ItemDirectory 的工厂表键,得到真实可创建的物品 identifier 全集。</summary>
        public static HashSet<string> CollectAllIdentifiers()
        {
            var set = new HashSet<string>();
            try
            {
                var dirs = UnityEngine.Object.FindObjectsOfType<ItemDirectory>();
                if (dirs == null) return set;
                foreach (var dir in dirs)
                {
                    try
                    {
                        var fd = dir.factoryDictionary;
                        if (fd == null) continue;
                        foreach (var kv in fd)
                        {
                            if (kv.Key != null) set.Add(kv.Key);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { WarehouseHelperMod.Warn("CollectAllIdentifiers: " + e.Message); }
            return set;
        }

        private static void RegisterInto(ItemDirectory dir)
        {
            if (dir == null) return;
            // Kitchen 同款:先查本地 factoryDictionary,没有才 Add
            var fd = dir.factoryDictionary;
            if (fd == null) return;
            int n = 0;
            foreach (var def in _defs)
            {
                var d = def;
                if (fd.ContainsKey(d.Id)) continue;
                dir.Add(d.Id, (Il2CppSystem.Func<GameItem>)(System.Func<GameItem>)(() => d.Create()));
                n++;
            }
            if (n > 0)
            {

                ValidateVanillaIds(CollectAllIdentifiers());
            }
        }

        private static void ValidateVanillaIds(HashSet<string> ids)
        {
            if (_validatedIds) return;
            _validatedIds = true;
            CheckId(ids, "螺丝刀", Config.IdScrewdriver.Value);
            CheckId(ids, "低级机械臂", Config.IdExtractorBasic.Value);
            CheckId(ids, "高级机械臂", Config.IdExtractorAdv.Value);
            CheckId(ids, "垃圾", Config.IdJunk.Value);
            CheckId(ids, "电路板", Config.IdCircuit.Value);
            CheckId(ids, "废金属", Config.IdScrap.Value);
        }

        private static void CheckId(HashSet<string> ids, string label, string id)
        {
            if (!ids.Contains(id)) WarehouseHelperMod.Warn($"找不到 identifier: {label} = \"{id}\" — 请在 UserData/MelonPreferences.cfg 的 [WarehouseHelper] 里修正");
        }

        /// <summary>Kitchen/PSPDA 同款挂载:patch 游戏目录初始化,每次初始化都被游戏亲自调一遍。</summary>
        [HarmonyLib.HarmonyPatch(typeof(ModItemDirectory), "InitDirectory")]
        public static class ModItemDirectoryInitPatch
        {
            public static void Postfix(ModItemDirectory __instance)
            {
                try { RegisterInto(__instance); }
                catch (Exception e) { WarehouseHelperMod.Err("InitDirectory 注册: " + e); }
            }
        }

        // ---------- 共享工厂辅助(各物品定义的 Create 用) ----------

        /// <summary>零件堆基座:3x3,贴图 48x48(格子 16px)。</summary>
        public static GameItem MakePile(string id, string displayName, string desc, long value)
        {
            var item = ItemDirectory.CreateEmptyItem(id);
            BaseSetup(item, displayName, desc, value, "material", 3, 3);
            return item;
        }

        /// <summary>仓库助手基座:3x3,显示贴图 48x48。</summary>
        public static GameItem MakeHelper(string id, string displayName, long value)
        {
            var item = ItemDirectory.CreateEmptyItem(id);
            BaseSetup(item, displayName, HelperDescription(item), value, "tool", HelperCells, HelperCells);
            return item;
        }

        private static string HelperDescription(GameItem item)
        {
            int slots = HelperLogic.SlotCountOf(item);
            string prefix = (Config.HotkeyModifier.Value ?? "None").ToLowerInvariant() switch
            {
                "alt" => "Alt+",
                "ctrl" => "Ctrl+",
                "shift" => "Shift+",
                _ => ""
            };
            return I18n.T(slots == 9 ? "helper.advanced.flavor" : "helper.basic.flavor")
                + "\n" + I18n.T("helper.instructions") + "\n" + I18n.F("helper.capacity", slots, prefix);
        }

        public static void RefreshLocalizedText(GameItem item)
        {
            if (item == null) return;
            string name = I18n.ItemName(item.identifier);
            if (name == null) return;
            if (item.name != name) item.SetName(name);
            string description = HelperLogic.IsHelper(item) ? HelperDescription(item)
                : I18n.T(item.identifier == PartsAdvId ? "parts.advanced.description" : "parts.basic.description");
            if (item.shortDescription != description) item.shortDescription = description;
            if (item.longDescription != description) item.longDescription = description;
        }

        public static void BaseSetup(GameItem item, string displayName, string desc, long value, string itemType, int shapeW, int shapeH,
            string spriteAtlas = "WarehouseHelper", string spriteName = null)
        {
            try { item.SetName(displayName); } catch (Exception e) { WarehouseHelperMod.Warn("SetName: " + e.Message); }
            try { item.shortDescription = desc; } catch { }
            try { item.longDescription = desc; } catch { }
            try { item.unitValue = value; } catch { }
            try { item.SetGameItemType(itemType); } catch (Exception e) { WarehouseHelperMod.Warn("SetGameItemType: " + e.Message); }
            try { item.SetShape(BuildShape(shapeW, shapeH)); }
            catch (Exception e) { WarehouseHelperMod.Warn("SetShape: " + e.Message); }
            // 自定义图默认走虚拟图集。
            try { item.SetSprite(spriteAtlas, spriteName ?? item.identifier); }
            catch (Exception e) { WarehouseHelperMod.Warn("SetSprite: " + e.Message); }

            // 特征全部标记为已发现/已公开,否则显示为问号;读档后也重新修一遍
            ExposeAllFeatures(item);
            try
            {
                item.onLoaded = (Il2CppSystem.Action<GameItem>)(System.Action<GameItem>)(i =>
                {
                    ExposeAllFeatures(i);
                    RefreshLocalizedText(i);
                });
            }
            catch (Exception e) { WarehouseHelperMod.Warn("onLoaded: " + e.Message); }
        }

        /// <summary>直接构建实心矩形形状(格子数据 = 行主序字节,1=实;与存档格式一致)。</summary>
        public static GridShape BuildShape(int w, int h)
        {
            var b = new GridShapeBuilder(w, h);
            b.SetDataFill(w, h, 1);
            b.SetDataOutside(0);
            return b.Build();
        }

        public static void ExposeAllFeatures(GameItem item)
        {
            try
            {
                var feats = item.itemFeatures;
                if (feats == null) return;
                foreach (var f in feats)
                {
                    try { f.DiscoverFeature(); f.ExposeFeature(false, false); } catch { }
                }
            }
            catch { }
        }
    }
}
