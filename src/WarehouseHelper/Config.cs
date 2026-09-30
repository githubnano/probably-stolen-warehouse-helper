using MelonLoader;
using System;
using System.Collections.Generic;

namespace WarehouseHelper
{
    /// <summary>
    /// MelonPreferences 配置:所有原版物品 identifier 都可改,防止猜错。
    /// </summary>
    public static class Config
    {
        public static MelonPreferences_Category Cat;
        private static readonly Dictionary<MelonPreferences_Entry, string> LabelKeys = new();

        private static MelonPreferences_Entry<T> Setting<T>(string id, T value, string labelKey)
        {
            var entry = Cat.CreateEntry(id, value, I18n.T(labelKey));
            LabelKeys[entry] = labelKey;
            return entry;
        }

        public static void RefreshLabels()
        {
            if (Cat == null) return;
            Cat.DisplayName = I18n.T("mod.name");
            foreach (var pair in LabelKeys) pair.Key.DisplayName = I18n.T(pair.Value);
        }

        public static MelonPreferences_Entry<string> IdScrewdriver;
        public static MelonPreferences_Entry<string> IdExtractorBasic;
        public static MelonPreferences_Entry<string> IdExtractorAdv;
        public static MelonPreferences_Entry<string> IdJunk;
        public static MelonPreferences_Entry<string> IdCircuit;   // “电路板”,默认电子元件(普通)
        public static MelonPreferences_Entry<string> IdScrap;

        public static MelonPreferences_Entry<int> BasicNeedJunk;
        public static MelonPreferences_Entry<int> BasicNeedCircuit;
        public static MelonPreferences_Entry<int> AdvNeedScrap;
        public static MelonPreferences_Entry<int> AdvNeedCircuit;

        public static MelonPreferences_Entry<string> HotkeyModifier; // None/Shift/Ctrl/Alt

        public static MelonPreferences_Entry<string> IdRecharger;
        public static MelonPreferences_Entry<string> IdSmugglerBays;
        public static MelonPreferences_Entry<string> IdTrashcan;
        public static MelonPreferences_Entry<int> ChargerAutoSwap; // 0关 1普通 2大容量优先

        public static void Init()
        {
            Cat = MelonPreferences.CreateCategory("WarehouseHelper", I18n.T("mod.name"));

            IdScrewdriver = Setting("id_screwdriver", "screwdriver", "config.screwdriver");
            IdExtractorBasic = Setting("id_extractor_basic", "module_extractor", "config.extractor_basic");
            IdExtractorAdv = Setting("id_extractor_adv", "module_extractor_advanced", "config.extractor_advanced");

            // 旧版默认值 "module_extractor_adv" 是错的,自动修正已存配置
            if (IdExtractorAdv.Value == "module_extractor_adv")
            {
                IdExtractorAdv.Value = "module_extractor_advanced";
                Cat.SaveToFile(false);
            }
            IdJunk = Setting("id_junk", "junk", "config.junk");
            IdCircuit = Setting("id_circuit", "common_electronic", "config.circuit");
            IdScrap = Setting("id_scrap", "scrap_metal", "config.scrap");

            BasicNeedJunk = Setting("basic_need_junk", 1, "config.basic_junk");
            BasicNeedCircuit = Setting("basic_need_circuit", 1, "config.basic_circuit");
            AdvNeedScrap = Setting("adv_need_scrap", 2, "config.advanced_scrap");
            AdvNeedCircuit = Setting("adv_need_circuit", 4, "config.advanced_circuit");

            HotkeyModifier = Setting("hotkey_modifier", "Alt", "config.modifier");

            IdRecharger = Setting("id_recharger", "recharger", "config.recharger");
            IdSmugglerBays = Setting("id_smuggler_bays", "smuggler_bay,smuggler_bay_mini,smuggler_bay_mod,smuggler_bay_large", "config.smuggler_bays");
            IdTrashcan = Setting("id_trashcan", "trashcan", "config.trashcan");
            ChargerAutoSwap = Setting("charger_auto_swap", 0, "config.charger_auto");
        }

        public static bool IsSmugglerBayId(string id)
        {
            if (id == null) return false;
            foreach (var part in (IdSmugglerBays.Value ?? "").Split(','))
                if (string.Equals(part.Trim(), id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool ModifierHeld()
        {
            var m = (HotkeyModifier.Value ?? "None").ToLowerInvariant();
            if (m == "none") return true;
            if (m == "shift")
                return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);
            if (m == "ctrl")
                return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);
            if (m == "alt")
                return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt);
            return true;
        }
    }
}
