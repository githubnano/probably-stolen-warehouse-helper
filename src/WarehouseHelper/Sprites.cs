using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace WarehouseHelper
{
    /// <summary>
    /// 从嵌入资源加载 modimg 的四张 PNG,并按物品 identifier 提供 Sprite。
    /// 补丁覆盖四条贴图解析路径:
    ///   GameItemElement.ResolveItemSprite / ResolveSpriteByName
    ///   RenderHandler.LoadFromAtlas / LoadFromFile
    /// </summary>
    public static class Sprites
    {
        private static readonly Dictionary<string, Sprite> _byItemId = new Dictionary<string, Sprite>();

        public static void Load()
        {
            try
            {
                Map(Items.PartsBasicId, "WarehouseHelper.Assets.parts_basic.png", 48);
                Map(Items.PartsAdvId, "WarehouseHelper.Assets.parts_adv.png", 48);
                Map(Items.HelperBasicId, "WarehouseHelper.Assets.helper_basic.png", Items.HelperPixels);
                Map(Items.HelperAdvId, "WarehouseHelper.Assets.helper_adv.png", Items.HelperPixels);


            }
            catch (Exception e)
            {
                WarehouseHelperMod.Err("贴图加载失败: " + e);
            }
        }

        private static void Map(string itemId, string resource, int pixels)
        {
            _resourceByItemId[itemId] = (resource, pixels);
            var sprite = Create(resource, pixels);
            if (sprite == null)
            {
                WarehouseHelperMod.Warn("贴图创建失败: " + resource);
                return;
            }
            _byItemId[itemId] = sprite;

        }

        private static byte[] ReadResource(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    foreach (var n in asm.GetManifestResourceNames())
                    {
                        if (n.EndsWith(Path.GetFileName(name), StringComparison.OrdinalIgnoreCase))
                            return ReadResource(n);
                    }
                    return null;
                }
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        public static Sprite Get(string itemId)
        {
            if (itemId == null) return null;
            Sprite s;
            if (_byItemId.TryGetValue(itemId, out s) && s != null) return s;

            // 自愈:缓存里没有(或被回收)就从嵌入资源重建
            if (_resourceByItemId.TryGetValue(itemId, out var res))
            {
                WarehouseHelperMod.Warn("贴图缓存缺失,重建: " + itemId + " (dict size=" + _byItemId.Count + ")");
                s = Create(res.Resource, res.Pixels);
                if (s != null) _byItemId[itemId] = s;
                return s;
            }
            return null;
        }

        private static readonly Dictionary<string, (string Resource, int Pixels)> _resourceByItemId = new();

        private static Sprite Create(string resource, int pixels)
        {
            var bytes = ReadResource(resource);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            // Keep the original artwork; fit its runtime sprite to the item's grid footprint.
            // Point sampling preserves the pixel-art edges and transparency, including drag ghosts.
            if (tex.width != pixels || tex.height != pixels)
            {
                int sourceWidth = tex.width, sourceHeight = tex.height;
                var source = tex.GetPixels32();
                var scaled = new Color32[pixels * pixels];
                for (int y = 0; y < pixels; y++)
                {
                    int sourceY = (2 * y + 1) * sourceHeight / (2 * pixels);
                    for (int x = 0; x < pixels; x++)
                    {
                        int sourceX = (2 * x + 1) * sourceWidth / (2 * pixels);
                        scaled[y * pixels + x] = source[sourceY * sourceWidth + sourceX];
                    }
                }
                var resized = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
                resized.SetPixels32(scaled);
                resized.Apply(false, false);
                UnityEngine.Object.Destroy(tex);
                tex = resized;
            }
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }


    }

    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.GameItemElement), "ResolveItemSprite")]
    public static class SpritePatch
    {
        public static bool Prefix(Il2Cpp.GameItemElement __instance, ref UnityEngine.Sprite __result)
        {
            try
            {
                var s = Sprites.Get(__instance.identifier);

                if (s == null) return true;
                __result = s;
                return false;
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Err("SpritePatch: " + e.Message);
                return true;
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.GameItemElement), "ResolveSpriteByName")]
    public static class SpriteByNamePatch
    {
        public static bool Prefix(Il2Cpp.GameItemElement __instance, string name, ref UnityEngine.Sprite __result)
        {
            try
            {
                var s = Sprites.Get(__instance.identifier) ?? Sprites.Get(name);

                if (s == null) return true;
                __result = s;
                return false;
            }
            catch (Exception e)
            {
                WarehouseHelperMod.Err("SpriteByNamePatch: " + e.Message);
                return true;
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.RenderHandler), "LoadFromAtlas")]
    public static class AtlasPatch
    {
        public static bool Prefix(string atlasPath, string name, ref UnityEngine.Sprite __result)
        {
            try
            {
                var s = Sprites.Get(name);

                if (s == null) return true;
                __result = s;
                return false;
            }
            catch { return true; }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.RenderHandler), "LoadFromFile")]
    public static class FilePatch
    {
        public static bool Prefix(string spritePath, ref UnityEngine.Sprite __result)
        {
            try
            {
                var s = Sprites.Get(spritePath);

                if (s == null) return true;
                __result = s;
                return false;
            }
            catch { return true; }
        }
    }
}
