#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Di Ne 아이콘의 캡처, 후처리, 저장을 담당한다.
/// 미리보기와 실제 저장 모두 이 클래스를 거쳐 결과가 달라지지 않게 한다.
/// </summary>
public static class DiNeIconMaker
{
    public const int OutputSize = 256;

    private const string SavePath = "Assets/Di Ne/Icons";
    private const string ForbiddenOverlayRelativePath = "Assets/IconMaker/ForbiddenOverlay.png";
    // 256px 출력에 4배 슈퍼샘플링을 적용해 품질과 편집 창 반응성을 함께 확보한다.
    private const int CaptureSize = 1024;

    private static Texture2D cachedForbiddenOverlay;
    private static Color[] outlineResultBuffer;
    private static float[] outlineHorizontalBuffer;
    private static float[] outlineDilatedBuffer;

    [Serializable]
    public sealed class Settings
    {
        public bool outlineEnabled = true;
        public Color outlineColor = new Color(0.03f, 0.03f, 0.03f, 1f);
        [Range(1, 12)] public int outlineSize = 4;
        public bool forbiddenOverlay;
        [Range(0f, 1f)] public float forbiddenOpacity = 1f;
        [Range(0.2f, 1.2f)] public float forbiddenScale = 0.85f;
        public bool forbiddenBehindObject = true;

        public Settings Clone()
        {
            return new Settings
            {
                outlineEnabled = outlineEnabled,
                outlineColor = outlineColor,
                outlineSize = outlineSize,
                forbiddenOverlay = forbiddenOverlay,
                forbiddenOpacity = forbiddenOpacity,
                forbiddenScale = forbiddenScale,
                forbiddenBehindObject = forbiddenBehindObject
            };
        }
    }

    [MenuItem("GameObject/Di Ne/Smart Icon/Create Separate Icons", false, 0)]
    public static void CreateSeparateIcons()
    {
        GameObject[] targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("[DiNe] Smart Icon: 하나 이상의 GameObject를 선택해주세요.");
            return;
        }

        Texture2D lastGenerated = null;
        var quickSettings = new Settings { outlineEnabled = false, forbiddenOverlay = false };
        foreach (GameObject target in targets)
            lastGenerated = GenerateIcon(target, null, GetDefaultIconAssetPath(target.name), quickSettings);

        if (lastGenerated != null)
        {
            Selection.activeObject = lastGenerated;
            EditorGUIUtility.PingObject(lastGenerated);
        }

        Debug.Log($"[DiNe] Smart Icon: {targets.Length}개의 256px 아이콘을 생성했습니다.");
    }

    [MenuItem("GameObject/Di Ne/Smart Icon/Create Separate Icons", true)]
    private static bool ValidateCreateSeparateIcons() => Selection.gameObjects.Length > 0;

    [MenuItem("GameObject/Di Ne/Smart Icon/Create Combined Icon", false, 1)]
    public static void CreateCombinedIcon()
    {
        List<GameObject> targets = GetTopLevelSelection();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[DiNe] Smart Icon: 하나 이상의 GameObject를 선택해주세요.");
            return;
        }

        GameObject primary = targets[0];
        string fileName = targets.Count == 1 ? primary.name : primary.name + "_Combined";
        Texture2D generated = GenerateIcon(
            primary,
            targets.Skip(1),
            GetDefaultIconAssetPath(fileName),
            new Settings { outlineEnabled = false, forbiddenOverlay = false });

        if (generated != null)
        {
            Selection.activeObject = generated;
            EditorGUIUtility.PingObject(generated);
            Debug.Log($"[DiNe] Smart Icon: {targets.Count}개의 오브젝트를 하나의 256px 아이콘으로 생성했습니다.");
        }
    }

    [MenuItem("GameObject/Di Ne/Smart Icon/Create Combined Icon", true)]
    private static bool ValidateCreateCombinedIcon() => Selection.gameObjects.Length > 0;

    /// <summary>기존 외부 호출과의 호환을 위한 기본 생성 API.</summary>
    public static Texture2D GenerateIcon(GameObject target)
    {
        return GenerateIcon(target, null, null, new Settings());
    }

    public static Texture2D GenerateIcon(
        GameObject target,
        IEnumerable<GameObject> linkedObjects,
        string outputAssetPath,
        Settings settings)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        string path = NormalizeOutputPath(outputAssetPath, target.name);
        Texture2D icon = CreateIconTexture(target, linkedObjects, settings);
        if (icon == null)
            return null;

        try
        {
            return SaveTexture(icon, path);
        }
        finally
        {
            Object.DestroyImmediate(icon);
        }
    }

    /// <summary>저장하지 않고 256×256 미리보기 텍스처를 만든다. 호출자가 파괴해야 한다.</summary>
    public static Texture2D CreateIconTexture(
        GameObject target,
        IEnumerable<GameObject> linkedObjects,
        Settings settings)
    {
        if (target == null)
            return null;

        settings = settings ?? new Settings();
        Texture2D captured = Capture(target, linkedObjects);
        if (captured == null)
            return null;

        try
        {
            Texture2D fitted = CropAndFit(captured, settings);
            if (fitted == null)
                return null;

            try
            {
                ApplyEffects(fitted, settings);
                fitted.name = target.name + "_IconPreview";
                fitted.alphaIsTransparency = true;
                fitted.Apply(false, false);
                return fitted;
            }
            catch
            {
                Object.DestroyImmediate(fitted);
                throw;
            }
        }
        finally
        {
            Object.DestroyImmediate(captured);
        }
    }

    public static string GetDefaultIconAssetPath(string targetName)
    {
        return $"{SavePath}/{GetSafeFileName(targetName)}.png";
    }

    public static bool CanOverwriteAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string normalized = assetPath.Replace('\\', '/');
        return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
               normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>현재 열린 씬에서 사용하지 않는 레이어를 골라 캡처 대상만 렌더되게 한다.</summary>
    public static int FindAvailableCaptureLayer()
    {
        var used = new bool[32];
        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject == null || EditorUtility.IsPersistent(gameObject))
                continue;
            used[gameObject.layer] = true;
        }

        for (int layer = 31; layer >= 8; layer--)
            if (!used[layer]) return layer;
        for (int layer = 7; layer >= 0; layer--)
            if (!used[layer]) return layer;

        Debug.LogWarning("[DiNe] 비어 있는 캡처 레이어가 없어 31번 레이어를 임시 사용합니다.");
        return 31;
    }

    /// <summary>
    /// 캡처 루트 밖의 씬 Renderer를 동기 렌더 동안만 제외한다.
    /// 레이어를 무시하는 특수 구성이나 다른 프리뷰 카메라가 있어도 대상 복제본만 남긴다.
    /// </summary>
    public static IDisposable IsolateSceneRenderers(GameObject captureRoot)
    {
        return new SceneRendererIsolation(captureRoot);
    }

    private sealed class SceneRendererIsolation : IDisposable
    {
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<bool> previousStates = new List<bool>();

        public SceneRendererIsolation(GameObject captureRoot)
        {
            if (captureRoot == null)
                return;

            Transform captureTransform = captureRoot.transform;
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null || EditorUtility.IsPersistent(renderer) ||
                    renderer.transform == captureTransform || renderer.transform.IsChildOf(captureTransform))
                    continue;

                renderers.Add(renderer);
                previousStates.Add(renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                    renderers[i].forceRenderingOff = previousStates[i];
            }
            renderers.Clear();
            previousStates.Clear();
        }
    }

    private static Texture2D Capture(GameObject target, IEnumerable<GameObject> linkedObjects)
    {
        GameObject root = null;
        GameObject cameraObject = null;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            int captureLayer = FindAvailableCaptureLayer();
            root = new GameObject("DiNe_IconRoot") { hideFlags = HideFlags.HideAndDontSave };
            AddClone(target, root.transform);

            if (linkedObjects != null)
            {
                foreach (GameObject linked in linkedObjects.Where(item => item != null && item != target))
                    AddClone(linked, root.transform);
            }

            ChangeLayerRecursively(root, captureLayer);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                .ToArray();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[DiNe] 아이콘을 생성할 Renderer가 없습니다: {target.name}");
                return null;
            }

            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float radius = Mathf.Max(bounds.extents.magnitude, 0.001f);
            cameraObject = new GameObject("DiNe_IconCamera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << captureLayer;
            camera.fieldOfView = 30f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            float distance = radius / Mathf.Sin(Mathf.Deg2Rad * camera.fieldOfView * 0.5f) * 1.08f;
            camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            camera.transform.position = bounds.center + Vector3.forward * distance;
            camera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2.5f);
            camera.farClipPlane = distance + radius * 2.5f;

            renderTexture = RenderTexture.GetTemporary(
                CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            renderTexture.filterMode = FilterMode.Bilinear;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            using (IsolateSceneRenderers(root))
                camera.Render();

            var result = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGBA32, false, false);
            result.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0, false);
            result.Apply(false, false);
            return result;
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);
            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);
            if (root != null)
                Object.DestroyImmediate(root);
        }
    }

    private static void AddClone(GameObject source, Transform parent)
    {
        GameObject clone = Object.Instantiate(source, parent, true);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.SetActive(true);
    }

    private static Texture2D CropAndFit(Texture2D source, Settings settings)
    {
        Color32[] pixels = source.GetPixels32();
        int minX = source.width;
        int minY = source.height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < source.height; y++)
        {
            int row = y * source.width;
            for (int x = 0; x < source.width; x++)
            {
                if (pixels[row + x].a <= 1)
                    continue;

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return null;

        int cropWidth = maxX - minX + 1;
        int cropHeight = maxY - minY + 1;
        int squareSize = Mathf.Max(cropWidth, cropHeight);
        var square = new Texture2D(squareSize, squareSize, TextureFormat.RGBA32, false, false);
        FillClear(square);
        square.SetPixels(
            (squareSize - cropWidth) / 2,
            (squareSize - cropHeight) / 2,
            cropWidth,
            cropHeight,
            source.GetPixels(minX, minY, cropWidth, cropHeight));
        square.Apply(false, false);

        int effectMargin = settings.outlineEnabled ? Mathf.Clamp(settings.outlineSize, 1, 12) + 2 : 2;
        int contentSize = OutputSize - effectMargin * 2;
        Texture2D resized = ResizeTexture(square, contentSize, contentSize);
        Object.DestroyImmediate(square);

        var output = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false, false);
        FillClear(output);
        output.SetPixels(effectMargin, effectMargin, contentSize, contentSize, resized.GetPixels());
        output.Apply(false, false);
        Object.DestroyImmediate(resized);
        return output;
    }

    public static void ApplyEffects(Texture2D texture, Settings settings)
    {
        Color[] basePixels = texture.GetPixels();
        Color[] result = basePixels;

        if (settings.outlineEnabled)
            result = AddOutline(basePixels, texture.width, texture.height, settings.outlineColor,
                Mathf.Clamp(settings.outlineSize, 1, 12));

        if (settings.forbiddenOverlay)
            CompositeForbiddenOverlay(result, texture.width, texture.height,
                Mathf.Clamp01(settings.forbiddenOpacity), Mathf.Clamp(settings.forbiddenScale, 0.2f, 1.2f),
                settings.forbiddenBehindObject);

        texture.SetPixels(result);
        texture.Apply(false, false);
    }

    private static Color[] AddOutline(Color[] source, int width, int height, Color outlineColor, int radius)
    {
        if (outlineResultBuffer == null || outlineResultBuffer.Length != source.Length)
        {
            outlineResultBuffer = new Color[source.Length];
            outlineHorizontalBuffer = new float[source.Length];
            outlineDilatedBuffer = new float[source.Length];
        }

        Color[] result = outlineResultBuffer;
        Array.Copy(source, result, source.Length);

        // 가로/세로 두 번의 최대값 필터로 알파를 확장한다.
        // 픽셀마다 원형 범위를 전부 검색하던 방식보다 드래그 프리뷰에서 훨씬 가볍다.
        float[] horizontalMax = outlineHorizontalBuffer;
        float[] dilatedAlpha = outlineDilatedBuffer;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                float maximum = 0f;
                int minX = Mathf.Max(0, x - radius);
                int maxX = Mathf.Min(width - 1, x + radius);
                for (int nx = minX; nx <= maxX; nx++)
                    maximum = Mathf.Max(maximum, source[row + nx].a);
                horizontalMax[row + x] = maximum;
            }
        }

        for (int y = 0; y < height; y++)
        {
            int minY = Mathf.Max(0, y - radius);
            int maxY = Mathf.Min(height - 1, y + radius);
            for (int x = 0; x < width; x++)
            {
                float maximum = 0f;
                for (int ny = minY; ny <= maxY; ny++)
                    maximum = Mathf.Max(maximum, horizontalMax[ny * width + x]);
                dilatedAlpha[y * width + x] = maximum;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float sourceAlpha = source[index].a;
                if (sourceAlpha >= 0.999f)
                    continue;

                float outlineAlpha = dilatedAlpha[index] * (1f - sourceAlpha) * outlineColor.a;
                if (outlineAlpha <= 0f)
                    continue;

                Color behind = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineAlpha);
                result[index] = AlphaOver(source[index], behind);
            }
        }

        return result;
    }

    private static void CompositeForbiddenOverlay(
        Color[] destination, int width, int height, float opacity, float scale, bool behindObject)
    {
        Texture2D overlay = GetForbiddenOverlay();
        if (overlay == null)
            return;

        float scaledWidth = width * scale;
        float scaledHeight = height * scale;
        float startX = (width - scaledWidth) * 0.5f;
        float startY = (height - scaledHeight) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f - startY) / scaledHeight;
            if (v < 0f || v > 1f)
                continue;

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f - startX) / scaledWidth;
                if (u < 0f || u > 1f)
                    continue;

                Color foreground = overlay.GetPixelBilinear(u, v);
                foreground.a *= opacity;
                int index = y * width + x;
                destination[index] = behindObject
                    ? AlphaOver(destination[index], foreground)
                    : AlphaOver(foreground, destination[index]);
            }
        }
    }

    private static Texture2D GetForbiddenOverlay()
    {
        if (cachedForbiddenOverlay != null)
            return cachedForbiddenOverlay;

        string assetPath = DiNePackageAssets.GetAssetPath(ForbiddenOverlayRelativePath);
        if (!File.Exists(assetPath))
        {
            Debug.LogWarning($"[DiNe] 금지 오버레이 PNG를 찾지 못했습니다: {assetPath}");
            return null;
        }

        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        if (!source.LoadImage(File.ReadAllBytes(assetPath), false))
        {
            Object.DestroyImmediate(source);
            return null;
        }

        cachedForbiddenOverlay = ResizeTexture(source, OutputSize, OutputSize);
        cachedForbiddenOverlay.name = "DiNe_ForbiddenOverlay_256";
        cachedForbiddenOverlay.hideFlags = HideFlags.HideAndDontSave;
        Object.DestroyImmediate(source);

        Color[] pixels = cachedForbiddenOverlay.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            float saturation = max <= 0.0001f ? 0f : (max - min) / max;
            color.a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.20f, saturation));
            pixels[i] = color;
        }

        cachedForbiddenOverlay.SetPixels(pixels);
        cachedForbiddenOverlay.Apply(false, false);
        return cachedForbiddenOverlay;
    }

    private static Color AlphaOver(Color foreground, Color background)
    {
        float outputAlpha = foreground.a + background.a * (1f - foreground.a);
        if (outputAlpha <= 0.0001f)
            return Color.clear;

        return new Color(
            (foreground.r * foreground.a + background.r * background.a * (1f - foreground.a)) / outputAlpha,
            (foreground.g * foreground.a + background.g * background.a * (1f - foreground.a)) / outputAlpha,
            (foreground.b * foreground.a + background.b * background.a * (1f - foreground.a)) / outputAlpha,
            outputAlpha);
    }

    private static Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                pixels[y * width + x] = source.GetPixelBilinear(u, v);
            }
        }

        result.SetPixels(pixels);
        result.Apply(false, false);
        return result;
    }

    private static Texture2D SaveTexture(Texture2D texture, string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = OutputSize;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static string NormalizeOutputPath(string outputAssetPath, string targetName)
    {
        string path = string.IsNullOrWhiteSpace(outputAssetPath)
            ? GetDefaultIconAssetPath(targetName)
            : outputAssetPath.Replace('\\', '/');

        if (!CanOverwriteAsset(path))
            path = GetDefaultIconAssetPath(targetName);

        return path;
    }

    private static string GetSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Icon";

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');

        value = value.Trim();
        return string.IsNullOrEmpty(value) ? "Icon" : value;
    }

    private static List<GameObject> GetTopLevelSelection()
    {
        GameObject[] selected = Selection.gameObjects;
        var selectedSet = new HashSet<GameObject>(selected);
        List<GameObject> result = selected
            .Where(item => item != null && !HasSelectedAncestor(item.transform.parent, selectedSet))
            .OrderBy(item => item.transform.GetSiblingIndex())
            .ToList();

        GameObject active = Selection.activeGameObject;
        int activeIndex = result.IndexOf(active);
        if (activeIndex > 0)
        {
            result.RemoveAt(activeIndex);
            result.Insert(0, active);
        }
        return result;
    }

    private static bool HasSelectedAncestor(Transform parent, HashSet<GameObject> selected)
    {
        while (parent != null)
        {
            if (selected.Contains(parent.gameObject))
                return true;
            parent = parent.parent;
        }
        return false;
    }

    private static void FillClear(Texture2D texture)
    {
        texture.SetPixels(Enumerable.Repeat(Color.clear, texture.width * texture.height).ToArray());
    }

    private static void ChangeLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            ChangeLayerRecursively(child.gameObject, layer);
    }
}
#endif
