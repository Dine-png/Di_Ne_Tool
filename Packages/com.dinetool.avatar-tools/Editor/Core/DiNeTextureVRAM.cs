using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레퍼런스: de.thryrallo.vrc.avatar-performance-tools / TextureVRAM.cs
/// BPP 딕셔너리와 밉맵 포함 계산 공식을 기반으로 실제 VRAM 크기를 산출합니다.
/// </summary>
public static class DiNeTextureVRAM
{
    // ── TextureFormat별 BPP (레퍼런스 툴 기준) ────────────────────────────────
    private static readonly Dictionary<TextureFormat, float> BPP =
        new Dictionary<TextureFormat, float>
    {
        { TextureFormat.Alpha8,              9    },
        { TextureFormat.ARGB4444,           16    },
        { TextureFormat.RGB24,              24    },
        { TextureFormat.RGBA32,             32    },
        { TextureFormat.ARGB32,             32    },
        { TextureFormat.RGB565,             16    },
        { TextureFormat.R16,                16    },
        { TextureFormat.DXT1,                4    },
        { TextureFormat.DXT5,                8    },
        { TextureFormat.RGBA4444,           16    },
        { TextureFormat.BGRA32,             32    },
        { TextureFormat.RHalf,              16    },
        { TextureFormat.RGHalf,             32    },
        { TextureFormat.RGBAHalf,           64    },
        { TextureFormat.RFloat,             32    },
        { TextureFormat.RGFloat,            64    },
        { TextureFormat.RGBAFloat,         128    },
        { TextureFormat.YUY2,               16    },
        { TextureFormat.RGB9e5Float,        32    },
        { TextureFormat.BC6H,                8    },
        { TextureFormat.BC7,                 8    },
        { TextureFormat.BC4,                 4    },
        { TextureFormat.BC5,                 8    },
        { TextureFormat.DXT1Crunched,        4    },
        { TextureFormat.DXT5Crunched,        8    },
        { TextureFormat.PVRTC_RGB2,          6    },
        { TextureFormat.PVRTC_RGBA2,         8    },
        { TextureFormat.PVRTC_RGB4,         12    },
        { TextureFormat.PVRTC_RGBA4,        16    },
        { TextureFormat.ETC_RGB4,           12    },
        { TextureFormat.EAC_R,               4    },
        { TextureFormat.EAC_R_SIGNED,        4    },
        { TextureFormat.EAC_RG,              8    },
        { TextureFormat.EAC_RG_SIGNED,       8    },
        { TextureFormat.ETC2_RGB,           12    },
        { TextureFormat.ETC2_RGBA1,         12    },
        { TextureFormat.ETC2_RGBA8,         32    },
        { TextureFormat.ASTC_4x4,            8    },
        { TextureFormat.ASTC_5x5,            5.12f},
        { TextureFormat.ASTC_6x6,            3.55f},
        { TextureFormat.ASTC_8x8,            2    },
        { TextureFormat.ASTC_10x10,          1.28f},
        { TextureFormat.ASTC_12x12,          1    },
        { TextureFormat.RG16,               16    },
        { TextureFormat.R8,                  8    },
        { TextureFormat.ETC_RGB4Crunched,   12    },
        { TextureFormat.ETC2_RGBA8Crunched, 32    },
        { TextureFormat.ASTC_HDR_4x4,        8    },
        { TextureFormat.ASTC_HDR_5x5,        5.12f},
        { TextureFormat.ASTC_HDR_6x6,        3.55f},
        { TextureFormat.ASTC_HDR_8x8,        2    },
        { TextureFormat.ASTC_HDR_10x10,      1.28f},
        { TextureFormat.ASTC_HDR_12x12,      1    },
        { TextureFormat.RG32,               32    },
        { TextureFormat.RGB48,              48    },
        { TextureFormat.RGBA64,             64    },
    };

    private static readonly Dictionary<RenderTextureFormat, float> RT_BPP =
        new Dictionary<RenderTextureFormat, float>
    {
        { RenderTextureFormat.ARGB32,       32 },
        { RenderTextureFormat.Depth,         0 },
        { RenderTextureFormat.ARGBHalf,     64 },
        { RenderTextureFormat.Shadowmap,     8 },
        { RenderTextureFormat.RGB565,       32 },
        { RenderTextureFormat.ARGB4444,     16 },
        { RenderTextureFormat.ARGB1555,     16 },
        { RenderTextureFormat.Default,      32 },
        { RenderTextureFormat.ARGB2101010,  32 },
        { RenderTextureFormat.DefaultHDR,   64 },
        { RenderTextureFormat.ARGBFloat,   128 },
        { RenderTextureFormat.RGFloat,      64 },
        { RenderTextureFormat.RGHalf,       32 },
        { RenderTextureFormat.RFloat,       32 },
        { RenderTextureFormat.RHalf,        16 },
        { RenderTextureFormat.R8,            8 },
        { RenderTextureFormat.ARGBInt,     128 },
        { RenderTextureFormat.RGInt,        64 },
        { RenderTextureFormat.RInt,         32 },
        { RenderTextureFormat.BGRA32,       32 },
        { RenderTextureFormat.RGB111110Float,32},
        { RenderTextureFormat.RG32,         32 },
        { RenderTextureFormat.RGBAUShort,   64 },
        { RenderTextureFormat.RG16,         16 },
        { RenderTextureFormat.BGRA10101010_XR,40},
        { RenderTextureFormat.BGR101010_XR, 30 },
        { RenderTextureFormat.R16,          16 },
    };

    // ── BPP 조회 (외부에서 포맷 절약량 계산 등에 활용) ───────────────────────
    public static bool TryGetBPP(TextureFormat fmt, out float bpp) => BPP.TryGetValue(fmt, out bpp);

    /// <summary>BPP와 해상도 스케일을 직접 지정해서 VRAM 계산 (최적화 시뮬레이션용)</summary>
    public static long CalcTextureVRAMWithBPP(Texture tex, float bpp, float resolutionScale = 1f)
        => TextureToBytesUsingBPP(tex, bpp, resolutionScale);

    // ── 단일 텍스쳐 VRAM 계산 ─────────────────────────────────────────────────
    public static long CalcTextureVRAM(Texture tex)
    {
        if (tex == null) return 0;

        if (tex is Texture2D t2d)
        {
            if (!BPP.TryGetValue(t2d.format, out float bpp)) bpp = 16f;
            return TextureToBytesUsingBPP(tex, bpp);
        }
        if (tex is Texture2DArray t2dArr)
        {
            if (!BPP.TryGetValue(t2dArr.format, out float bpp)) bpp = 16f;
            return TextureToBytesUsingBPP(tex, bpp) * t2dArr.depth;
        }
        if (tex is Cubemap cm)
        {
            if (!BPP.TryGetValue(cm.format, out float bpp)) bpp = 16f;
            long s = TextureToBytesUsingBPP(tex, bpp);
            return cm.dimension == UnityEngine.Rendering.TextureDimension.Tex3D ? s * 6 : s;
        }
        if (tex is RenderTexture rt)
        {
            if (!RT_BPP.TryGetValue(rt.format, out float bpp)) bpp = 16f;
            bpp += rt.depth;
            return TextureToBytesUsingBPP(tex, bpp);
        }

        // 알 수 없는 타입: fallback
        return UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
    }

    // ── BPP × 픽셀수 × 밉맵 합산 (레퍼런스 공식) ──────────────────────────────
    private static long TextureToBytesUsingBPP(Texture t, float bpp, float resolutionScale = 1f)
    {
        int w = (int)(t.width  * resolutionScale);
        int h = (int)(t.height * resolutionScale);
        long bytes = 0;

        if (t is Texture2D || t is Texture2DArray || t is Cubemap)
        {
            for (int i = 0; i < t.mipmapCount; i++)
            {
                int mw = Mathf.Max(1, w >> i);
                int mh = Mathf.Max(1, h >> i);
                bytes += (long)Mathf.RoundToInt(mw * mh * bpp / 8f);
            }
        }
        else if (t is RenderTexture rt)
        {
            double mipmaps = 1.0;
            for (int i = 0; i < rt.mipmapCount; i++) mipmaps += System.Math.Pow(0.25, i + 1);
            if (!RT_BPP.TryGetValue(rt.format, out float rtBpp)) rtBpp = 16f;
            bytes = (long)((rtBpp + rt.depth) * w * h * (rt.useMipMap ? mipmaps : 1.0) / 8.0);
        }
        else
        {
            bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
        }

        return bytes;
    }

    // ── GameObject에서 텍스쳐 수집 후 총 VRAM 반환 ──────────────────────────────
    public static long CalcAvatarTextureVRAM(GameObject root, out int textureCount)
    {
        var seen  = new HashSet<Texture>();
        long total = 0;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;
                foreach (int id in mat.GetTexturePropertyNameIDs())
                {
                    if (!mat.HasProperty(id)) continue;
                    var tex = mat.GetTexture(id);
                    if (tex == null || !seen.Add(tex)) continue;
                    total += CalcTextureVRAM(tex);
                }
            }
        }

        textureCount = seen.Count;
        return total;
    }
}
