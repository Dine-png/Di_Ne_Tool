#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class DiNeMultiIconGenerator
{
    private const string SavePath = "Assets/Di Ne/MultiDresser/Icons";
    private const int CaptureSize = 2048;
    private const int OutputSize = 128;

    public static void GenerateIcons(DiNeMultiDresser context)
    {
        EnsureSavePath();

        // MultiDresser 구조에 맞게 레이어 순회
        foreach (var layer in context.layers)
        {
            if (layer.targets == null) continue;
            
            // 아이콘 리스트 크기 맞추기
            while (layer.icons.Count < layer.targets.Count) layer.icons.Add(null);

            for (int i = 1; i < layer.targets.Count; i++)
            {
                var go = layer.targets[i];
                if (go == null) continue;
                
                // 이미 아이콘이 있으면 건너뛰기 (필요시 이 조건은 첫번째 스크립트의 isSimpleTab 로직처럼 수정 가능)
                if (layer.icons[i] != null) continue;
                RegenerateIcon(layer, i);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("✅ 모든 아이콘 생성 완료! (DiNeIconGenerator 로직 적용됨)");
    }

    public static void RegenerateIcon(DiNeMultiDresser.DresserLayer layer, int buttonIdx)
    {
        if (layer == null || buttonIdx < 0) return;

        while (layer.icons.Count <= buttonIdx)
            layer.icons.Add(null);

        layer.icons[buttonIdx] = null;

        if (buttonIdx == 0 || layer.targets == null || buttonIdx >= layer.targets.Count)
            return;

        var target = layer.targets[buttonIdx];
        if (target == null)
            return;

        var existingIcon = LoadExistingIcon(target.name);
        if (existingIcon != null)
        {
            layer.icons[buttonIdx] = existingIcon;
            return;
        }

        List<GameObject> linkedList = null;
        if (layer.linkedObjects.Count > buttonIdx && layer.linkedObjects[buttonIdx] != null)
            linkedList = layer.linkedObjects[buttonIdx].objects;

        layer.icons[buttonIdx] = GenerateSingleIcon(target, linkedList);
    }

    public static void ReleaseIconReference(ref Texture2D icon)
    {
        icon = null;
    }

    public static void ReleaseIcons(DiNeMultiDresser context)
    {
        if (context == null || context.layers == null) return;

        foreach (var layer in context.layers)
        {
            if (layer?.icons == null) continue;

            for (int i = 0; i < layer.icons.Count; i++)
            {
                layer.icons[i] = null;
            }
        }
    }

    // 로직을 첫 번째 스크립트(DiNeIconGenerator)와 완전히 동일하게 교체
    private static Texture2D GenerateSingleIcon(GameObject target, List<GameObject> linked = null)
    {
        EnsureSavePath();

        var cloned = new GameObject("IconRoot");
        var clone = GameObject.Instantiate(target);
        clone.transform.SetParent(cloned.transform);
        clone.SetActive(true);

        if (linked != null)
        {
            foreach (var obj in linked)
            {
                if (obj == null) continue;
                var linkedClone = GameObject.Instantiate(obj);
                linkedClone.transform.SetParent(cloned.transform);
                linkedClone.SetActive(true);
            }
        }

        // 렌더러 체크 (첫 번째 스크립트 방식)
        var allRenderers = cloned.GetComponentsInChildren<Renderer>();
        if (allRenderers == null || allRenderers.Length == 0)
        {
            Debug.LogWarning($"아이콘을 생성할 메쉬가 없습니다: {target.name}");
            GameObject.DestroyImmediate(cloned.gameObject);
            return null;
        }

        const int targetLayer = 21;
        ChangeLayerRecursively(cloned, targetLayer);

        cloned.transform.position = Vector3.zero;
        var cameraObject = new GameObject();
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Nothing;
        camera.nearClipPlane = 0.00001f; 
        camera.cullingMask = 1 << targetLayer;

        foreach (var renderer in cloned.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.updateWhenOffscreen = true;
        }

        // 첫 번째 스크립트의 Bounds 계산 로직 적용
        var boundList = cloned.GetComponentsInChildren<Renderer>().Select<Renderer, Bounds?>(e =>
        {
            if (e.TryGetComponent(out SkinnedMeshRenderer skinned))
            {
                if (skinned.sharedMesh == null) return null;
                return new Bounds(skinned.bounds.center, skinned.sharedMesh.bounds.size);
            }
            return e.bounds;
        }).OfType<Bounds>().ToArray();

        var bounds = boundList.Length > 0 ? boundList[0] : new Bounds();
        foreach (var b in boundList.Skip(1))
        {
            bounds.Encapsulate(b);
        }

        cameraObject.transform.eulerAngles = new Vector3(0, -180, 0);
        var maxExtent = bounds.extents.magnitude;
        var minDistance = (maxExtent) / Mathf.Sin(Mathf.Deg2Rad * camera.fieldOfView / 2.0f);
        var center = bounds.center;

        camera.transform.position = center + cloned.transform.position + Vector3.forward * minDistance;

        var captureWidth = CaptureSize;
        var captureHeight = CaptureSize;

        var rt = RenderTexture.GetTemporary(captureWidth, captureHeight, 16, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt;
        camera.Render();
        var prevActive = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;
        var image = new Texture2D(captureWidth, captureHeight, TextureFormat.ARGB32, false);
        image.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        image.alphaIsTransparency = true;
        image.Apply();
        camera.targetTexture = null;
        RenderTexture.active = prevActive;

        RenderTexture.ReleaseTemporary(rt);
        GameObject.DestroyImmediate(camera.gameObject);
        GameObject.DestroyImmediate(cloned.gameObject);

        int minX = captureWidth, maxX = 0, minY = captureHeight, maxY = 0;
        var pixels = image.GetPixels32();
        for (int y = 0; y < captureHeight; y++)
        {
            int rowOffset = y * captureWidth;
            for (int x = 0; x < captureWidth; x++)
            {
                var pixel = pixels[rowOffset + x];
                if (pixel.a != 0)
                {
                    if (minX > x) minX = x;
                    if (maxX < x) maxX = x;
                    if (minY > y) minY = y;
                    if (maxY < y) maxY = y;
                }
            }
        }

        if (maxX <= minX || maxY <= minY)
        {
            GameObject.DestroyImmediate(image);
            return null;
        }

        var size = Mathf.Max(maxX - minX, maxY - minY);
        if (size < 0) size = 1;

        int croppedSizeX = maxX - minX, croppedSizeY = maxY - minY;
        var croppedPixels = image.GetPixels(minX, minY, croppedSizeX, croppedSizeY);
        var clippedIcon = new Texture2D(size, size, TextureFormat.ARGB32, false);
        MakeTexture2DClear(clippedIcon, size, size);
        clippedIcon.SetPixels(size / 2 - croppedSizeX / 2, size / 2 - croppedSizeY / 2, croppedSizeX, croppedSizeY, croppedPixels);
        clippedIcon.Apply();

        var resizedIcon = ResizeTexture(clippedIcon, OutputSize, OutputSize);
        GameObject.DestroyImmediate(image);
        GameObject.DestroyImmediate(clippedIcon);

        var bytes = resizedIcon.EncodeToPNG();
        var path = GetIconAssetPath(target.name);
        
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        GameObject.DestroyImmediate(resizedIcon);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D LoadExistingIcon(string iconName)
    {
        var path = GetIconAssetPath(iconName);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (icon == null)
            return null;

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        return icon;
    }

    private static string GetIconAssetPath(string iconName)
    {
        return $"{SavePath}/{GetSafeFileName(iconName)}.png";
    }

    private static string GetSafeFileName(string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
            return "Icon";

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            iconName = iconName.Replace(invalidChar, '_');
        }

        iconName = iconName.Trim();
        return string.IsNullOrEmpty(iconName) ? "Icon" : iconName;
    }

    private static void EnsureSavePath()
    {
        if (AssetDatabase.IsValidFolder(SavePath))
            return;

        Directory.CreateDirectory(SavePath);
        AssetDatabase.Refresh();
    }

    private static void ChangeLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
        {
            ChangeLayerRecursively(child.gameObject, layer);
        }
    }

    // 첫 번째 스크립트의 최적화된 방식 (Array.Copy 사용) 적용
    private static void MakeTexture2DClear(Texture2D tex2D, int width, int height)
    {
        var clearColors = new Color[width * height];
        int blockSize = width;
        Color[] initialBlock = new Color[blockSize];
        for (int i = 0; i < blockSize; i++)
        {
            initialBlock[i] = Color.clear;
        }

        int remaining = clearColors.Length;
        int copyPos = 0;
        while (remaining > 0)
        {
            int copyLength = Mathf.Min(blockSize, remaining);
            System.Array.Copy(initialBlock, 0, clearColors, copyPos, copyLength);
            remaining -= copyLength;
            copyPos += copyLength;
        }

        tex2D.SetPixels(clearColors);
    }

    private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, true);
        Color[] rpixels = result.GetPixels(0);
        float incX = 1.0f / targetWidth;
        float incY = 1.0f / targetHeight;
        for (int px = 0; px < rpixels.Length; px++)
        {
            rpixels[px] = source.GetPixelBilinear(
                incX * ((float)px % targetWidth),
                incY * ((float)Mathf.Floor(px / targetWidth))
            );
        }
        result.SetPixels(rpixels, 0);
        result.Apply();
        return result;
    }
}
#endif
