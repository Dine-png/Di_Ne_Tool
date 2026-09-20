#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 빌드 클론용 머티리얼/텍스처 생성물을 임시 폴더에 모아두는 세션.
/// 같은 머티리얼·같은 베이크 결과는 재사용해서 텍스처가 중복 생성되는 것을 막는다.
/// </summary>
internal sealed class DiNeLightingBakeSession
{
    public const string TempRootFolder = "Assets/Di Ne/LightingDesigner/__Temp";

    private readonly Dictionary<Material, Material> _clonedMaterials = new Dictionary<Material, Material>();
    private readonly Dictionary<int, Texture2D> _bakedTextures = new Dictionary<int, Texture2D>();
    private readonly string _folder;

    public DiNeLightingBakeSession()
    {
        EnsureFolder(TempRootFolder);
        _folder = AssetDatabase.GenerateUniqueAssetPath($"{TempRootFolder}/Bake_{Guid.NewGuid():N}");
        EnsureFolder(_folder);
    }

    public int ClonedMaterialCount => _clonedMaterials.Count;

    /// <summary>원본 머티리얼의 복제본. 원본은 절대 건드리지 않는다.</summary>
    public Material GetOrCloneMaterial(Material original)
    {
        if (original == null) return null;
        if (_clonedMaterials.TryGetValue(original, out var clone))
            return clone;

        clone = new Material(original) { name = original.name };
        AssetDatabase.CreateAsset(clone, AssetDatabase.GenerateUniqueAssetPath($"{_folder}/{SafeName(original.name)}.mat"));
        _clonedMaterials.Add(original, clone);
        return clone;
    }

    private readonly HashSet<Material> _prepared = new HashSet<Material>();

    /// <summary>이 복제 머티리얼을 아직 손보지 않았으면 true를 돌려주고 처리됨으로 표시한다.</summary>
    public bool MarkPrepared(Material clone) => clone != null && _prepared.Add(clone);

    public bool TryGetBaked(int hash, out Texture2D texture) => _bakedTextures.TryGetValue(hash, out texture);

    /// <summary>
    /// blitMaterial로 source를 구워 새 텍스처 에셋을 만든다. 소스가 Read/Write Enabled일 필요는 없다.
    /// </summary>
    public Texture2D Bake(int hash, Texture source, Material blitMaterial, bool hasAlpha)
    {
        if (_bakedTextures.TryGetValue(hash, out var cached))
            return cached;

        int width = 32;
        int height = 32;
        if (source != null)
        {
            width = Mathf.Max(32, source.width);
            height = Mathf.Max(32, source.height);
        }
        // Keep textures authored without mipmaps from gaining distance-dependent
        // filtering after normalization. White fallbacks retain the default chain.
        bool useMipMaps = !(source is Texture2D sourceTexture) || sourceTexture.mipmapCount > 1;

        // The baker outputs shader colors, so store them as sRGB color data in both
        // the render target and the resulting Texture2D. An explicit RGBA target
        // also avoids platform defaults that can discard the baked alpha channel.
        var renderTexture = RenderTexture.GetTemporary(
            width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        Texture2D baked = null;
        string assetPath = null;
        try
        {
            // Blit can inherit sRGB write state from an earlier editor render.
            // Encoding here must match the sRGB Texture2D read after GPU readback.
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            // Graphics.Blit assigns its source to _MainTex. Passing null can therefore
            // overwrite the baker material's white fallback with an undefined texture.
            Graphics.Blit(source != null ? source : Texture2D.whiteTexture, renderTexture, blitMaterial);

            baked = new Texture2D(width, height, TextureFormat.RGBA32, useMipMaps, false)
            {
                name = (source != null ? source.name : "DiNeBaked")
            };
            if (source != null)
            {
                baked.wrapModeU = source.wrapModeU;
                baked.wrapModeV = source.wrapModeV;
                baked.wrapModeW = source.wrapModeW;
                baked.filterMode = source.filterMode;
                baked.anisoLevel = source.anisoLevel;
                baked.mipMapBias = source.mipMapBias;
            }

            bool readbackComplete = false;
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                var request = AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32);
                request.WaitForCompletion();
                if (!request.hasError)
                {
                    // This is a non-owning view into request memory. Copy it while
                    // the request is alive, and never dispose the returned array.
                    var data = request.GetData<Color32>();
                    baked.SetPixelData(data, 0);
                    readbackComplete = true;
                }
            }
            if (!readbackComplete)
            {
                // GPU 읽기가 실패하면 동기 ReadPixels로 대체한다.
                RenderTexture.active = renderTexture;
                baked.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            }

            // The caller's hasAlpha hint describes material tint, not texture
            // transparency. The actual result is authoritative: even one
            // non-opaque pixel requires an alpha-capable compression format.
            // GetPixelData is another borrowed view and must not be disposed.
            bool bakedHasAlpha = false;
            var pixels = baked.GetPixelData<Color32>(0);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == byte.MaxValue) continue;
                bakedHasAlpha = true;
                break;
            }
            baked.Apply(useMipMaps);

            // DXT는 4의 배수 크기에서만 안전하다. 아니면 비압축으로 둔다.
            if (width % 4 == 0 && height % 4 == 0)
                EditorUtility.CompressTexture(baked, bakedHasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1, 50);
            assetPath = AssetDatabase.GenerateUniqueAssetPath($"{_folder}/{SafeName(baked.name)}_baked.asset");
            AssetDatabase.CreateAsset(baked, assetPath);
            if (AssetDatabase.GetAssetPath(baked) != assetPath)
                throw new InvalidOperationException($"Could not save baked texture: {assetPath}");

            _bakedTextures.Add(hash, baked);
            return baked;
        }
        catch
        {
            // Only publish a usable texture after the bake and asset creation
            // succeed. Failed readback/compression must not leak editor objects.
            if (baked != null)
            {
                if (assetPath != null && AssetDatabase.GetAssetPath(baked) == assetPath)
                    AssetDatabase.DeleteAsset(assetPath);
                else
                    UnityEngine.Object.DestroyImmediate(baked);
            }
            throw;
        }
        finally
        {
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    public void Save()
    {
        AssetDatabase.SaveAssets();
    }

    /// <summary>빌드가 끝났을 때 임시 폴더 전체를 지운다.</summary>
    public static void CleanupTempAssets()
    {
        if (!AssetDatabase.IsValidFolder(TempRootFolder))
            return;

        AssetDatabase.DeleteAsset(TempRootFolder);
        AssetDatabase.Refresh();
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Material";
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace('/', '_').Replace('\\', '_').Trim();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }
}
#endif
