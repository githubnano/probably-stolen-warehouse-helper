using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>Player-facing text. Follow the game's locale; unsupported languages use English.</summary>
    public static class I18n
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Text = LoadText();

        private static Dictionary<string, Dictionary<string, string>> LoadText()
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(I18n).Assembly;
            const string prefix = "WarehouseHelper.Locales.";
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal)) continue;
                using var stream = assembly.GetManifestResourceStream(name);
                result[name.Substring(prefix.Length, name.Length - prefix.Length - 5)] =
                    System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            }
            return result;
        }

        private static string NormalizeLocale(string code)
        {
            code = code.Replace('_', '-').ToLowerInvariant();
            // The game's Brazilian Portuguese bundle uses ptbr; also accept standard locale codes.
            if (code == "ptbr" || code == "pt" || code.StartsWith("pt-")) return "pt-BR";
            int separator = code.IndexOf('-');
            return separator < 0 ? code : code.Substring(0, separator);
        }

        private static readonly string[] ActionKeys =
            { "action.use", "action.activate", "action.toggle", "action.open", "action.unload", "action.move", "action.equip" };
        private static string _lastKnown = "en", _applied;
        private static float _nextCheck;

        private static string LocaleCode
        {
            get
            {
                try
                {
                    string code = LocHelper.GetCurrentLocaleCode();
                    if (!string.IsNullOrWhiteSpace(code)) _lastKnown = code.Trim();
                }
                catch { } // The locale system may not exist during early mod initialization.
                return _lastKnown;
            }
        }

        public static string T(string key)
        {
            if (Text.TryGetValue(NormalizeLocale(LocaleCode), out var translated) && translated.TryGetValue(key, out var value))
                return value;
            return Text.TryGetValue("en", out var english) && english.TryGetValue(key, out value) ? value : key;
        }

        public static string F(string key, params object[] args)
            => string.Format(CultureInfo.InvariantCulture, T(key), args);

        // Persisted action numbers stay unchanged; only their visible labels are translated.
        public static string ActionName(int action)
            => action >= 0 && action < ActionKeys.Length ? T(ActionKeys[action]) : "?";

        public static string ItemName(string id) => id switch
        {
            Items.HelperBasicId => T("helper.basic.name"),
            Items.HelperAdvId => T("helper.advanced.name"),
            Items.PartsBasicId => T("parts.basic.name"),
            Items.PartsAdvId => T("parts.advanced.name"),
            _ => null
        };

        public static void OnSceneChange() { _applied = null; _nextCheck = 0; }

        public static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextCheck) return;
            _nextCheck = now + 0.5f;
            string code = LocaleCode;
            if (string.Equals(code, _applied, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                Config.RefreshLabels();
                var roots = GraphHandler.windowGraphNodeRoots;
                if (roots != null)
                {
                    var seen = new HashSet<IntPtr>();
                    var filter = (Il2CppSystem.Func<GameItem, bool>)(Func<GameItem, bool>)(i => i != null && Items.Find(i.identifier) != null);
                    var pass = (Il2CppSystem.Func<GameItem, bool>)(Func<GameItem, bool>)(i => true);
                    foreach (var window in roots)
                    {
                        if (window?.child == null) continue;
                        var items = GraphUtils.FindAllChildrenType<GameItem>(window.child, filter, pass);
                        if (items == null) continue;
                        foreach (var item in items)
                            if (seen.Add(item.Pointer)) Items.RefreshLocalizedText(item);
                    }
                }
                BindWindow.RefreshLocale();
                _applied = code;
            }
            catch (Exception e) { WarehouseHelperMod.Warn("Language refresh: " + e.Message); }
        }
    }
}
