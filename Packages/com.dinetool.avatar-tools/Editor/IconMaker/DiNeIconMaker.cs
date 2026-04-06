// Assets/Di Ne/IconMaker/Editor/DiNeIconMaker.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class DiNeIconMaker
{
    private const string SavePath = "Assets/Di Ne/IconMaker/Icons";
    private const int TargetLayer = 21;

    [MenuItem("GameObject/Di Ne/IconMaker", false, 0)]
    public static void GenerateSingle(MenuCommand menuCommand)
    {
        GameObject target = menuCommand.context as GameObject;
        if (target == null)
        {
            Debug.LogWarning("GameObject를 선택한 후 우클릭하세요.");
            return;
        }

        var tex = GenerateIcon(target);
        Debug.Log($"✅ 아이콘 생성 완료: {AssetDatabase.GetAssetPath(tex)}");
    }

    public static Texture2D GenerateIcon(GameObject target)
    {
        // 아이콘 저장 폴더
        if (!Directory.Exists(SavePath))
        {
            Directory.CreateDirectory(SavePath);
            AssetDatabase.Refresh();
        }

        // 1. 복제 및 레이어 설정
        var root = new GameObject("IconRoot");
        var clone = GameObject.Instantiate(target, root.transform);
        clone.SetActive(true);
        ChangeLayerRecursively(root, TargetLayer);

        // 2. 카메라 세팅
        var camObj = new GameObject("IconCamera");
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Nothing;
        cam.nearClipPlane = 0.00001f;
        cam.cullingMask = 1 << TargetLayer;

        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            r.updateWhenOffscreen = true;

        // 3. 렌더링 영역 계산
        var boundsList = root.GetComponentsInChildren<Renderer>().Select(r =>
        {
            if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null)
                return new Bounds(sk.bounds.center, sk.sharedMesh.bounds.size);
            return r.bounds;
        }).ToArray();

        Bounds bounds = boundsList.FirstOrDefault();
        foreach (var b in boundsList.Skip(1)) bounds.Encapsulate(b);

        var maxExtent = bounds.extents.magnitude;
        var distance = maxExtent / Mathf.Sin(Mathf.Deg2Rad * cam.fieldOfView / 2f);
        var center = bounds.center;

        camObj.transform.eulerAngles = new Vector3(0, -180, 0);
        camObj.transform.position = center + root.transform.position + Vector3.forward * distance;

        // 4. 렌더링 → Texture2D
        var rt = new RenderTexture(2048, 2048, 0);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        var raw = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        raw.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        raw.alphaIsTransparency = true;
        raw.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;

        // 5. 크롭 처리
        int minX = rt.width, maxX = 0, minY = rt.height, maxY = 0;
        for (int x = 0; x < rt.width; x++)
            for (int y = 0; y < rt.height; y++)
            {
                var a = raw.GetPixel(x, y).a;
                if (a > 0f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

        var croppedW = maxX - minX;
        var croppedH = maxY - minY;
        var croppedPixels = raw.GetPixels(minX, minY, croppedW, croppedH);

        int size = Mathf.Max(croppedW, croppedH);
        var clipped = new Texture2D(size, size, TextureFormat.ARGB32, false);
        MakeTransparent(clipped, size, size);
        clipped.SetPixels(size / 2 - croppedW / 2, size / 2 - croppedH / 2, croppedW, croppedH, croppedPixels);
        clipped.Apply();

        var resized = ResizeTexture(clipped, 256, 256);

        // 6. 저장 및 리임포트
        var bytes = resized.EncodeToPNG();
        var filePath = Path.Combine(SavePath, target.name + ".png");
        File.WriteAllBytes(filePath, bytes);
        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // 7. 정리
        GameObject.DestroyImmediate(rt);
        GameObject.DestroyImmediate(camObj);
        GameObject.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
    }

    private static void ChangeLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            ChangeLayerRecursively(child.gameObject, layer);
    }

    private static void MakeTransparent(Texture2D tex, int w, int h)
    {
        var colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;
        tex.SetPixels(colors);
    }

    private static Texture2D ResizeTexture(Texture2D src, int w, int h)
    {
        Texture2D dst = new Texture2D(w, h, src.format, true);
        Color[] rp = dst.GetPixels(0);
        float incX = 1.0f / w, incY = 1.0f / h;
        for (int i = 0; i < rp.Length; i++)
        {
            rp[i] = src.GetPixelBilinear(
                incX * (i % w),
                incY * Mathf.Floor(i / w)
            );
        }
        dst.SetPixels(rp, 0);
        dst.Apply();
        return dst;
    }
}
#endif
