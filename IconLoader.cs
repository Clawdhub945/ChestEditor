using System.Collections.Generic;
using UnityEngine;

namespace ChestEditor;

internal static class IconLoader
{
    private static readonly Dictionary<int, Texture2D?> _iconCache = new();
    private static Dictionary<string, Sprite>? _spriteDict;
    private static bool _spriteScanned;

    internal static void EnsureScanned()
    {
        if (_spriteScanned) return;
        _spriteScanned = true;
        _spriteDict = new Dictionary<string, Sprite>();
        foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sp != null && !string.IsNullOrEmpty(sp.name) && !_spriteDict.ContainsKey(sp.name))
                _spriteDict[sp.name] = sp;
        }
    }

    internal static Texture2D? GetIconTexture(int stuffId)
    {
        if (_iconCache.TryGetValue(stuffId, out var cached)) return cached;

        try
        {
            EnsureScanned();
            string targetName = $"ui_{stuffId}";

            if (_spriteDict != null && _spriteDict.TryGetValue(targetName, out var sprite))
            {
                var atlas = sprite.texture;
                var texRect = sprite.textureRect;
                int sx = (int)texRect.x, sy = (int)texRect.y;
                int sw = (int)texRect.width, sh = (int)texRect.height;

                Color[]? pixels = null;
                try
                {
                    pixels = atlas.GetPixels(sx, sy, sw, sh);
                }
                catch
                {
                    int aw = atlas.width, ah = atlas.height;
                    var atlasRT = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(atlas, atlasRT);
                    var prev = RenderTexture.active;
                    RenderTexture.active = atlasRT;

                    var tmpTex = new Texture2D(aw, ah, TextureFormat.RGBA32, false);
                    tmpTex.ReadPixels(new Rect(0, 0, aw, ah), 0, 0);
                    tmpTex.Apply();
                    pixels = tmpTex.GetPixels(sx, sy, sw, sh);
                    UnityEngine.Object.Destroy(tmpTex);

                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(atlasRT);
                }

                var smallTex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
                smallTex.SetPixels(pixels);
                smallTex.Apply();

                _iconCache[stuffId] = smallTex;
                return smallTex;
            }

            _iconCache[stuffId] = null;
            return null;
        }
        catch
        {
            _iconCache[stuffId] = null;
            return null;
        }
    }
}
