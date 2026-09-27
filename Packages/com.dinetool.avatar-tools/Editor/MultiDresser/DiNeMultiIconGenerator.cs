#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Multi Dresser 아이콘 생성과 참조 관리를 담당한다.</summary>
public static class DiNeMultiIconGenerator
{
    public static void GenerateIcons(DiNeMultiDresser context)
    {
        if (context == null || context.layers == null)
            return;

        Undo.RecordObject(context, "Generate Multi Dresser Icons");
        foreach (DiNeMultiDresser.DresserLayer layer in context.layers)
        {
            if (layer?.targets == null)
                continue;

            while (layer.icons.Count < layer.targets.Count)
                layer.icons.Add(null);

            for (int i = 1; i < layer.targets.Count; i++)
            {
                if (layer.targets[i] == null || layer.icons[i] != null)
                    continue;
                EnsureIcon(layer, i);
            }
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(context);
        EditorUtility.SetDirty(context);
        AssetDatabase.SaveAssets();
        Debug.Log("[DiNe] Multi Dresser 256px 아이콘 생성을 완료했습니다.");
    }

    // Automatic assignment must reuse an existing asset before capturing again.
    public static void EnsureIcon(DiNeMultiDresser.DresserLayer layer, int buttonIdx)
    {
        if (layer?.targets == null || buttonIdx <= 0 || buttonIdx >= layer.targets.Count)
            return;

        while (layer.icons.Count <= buttonIdx)
            layer.icons.Add(null);

        GameObject target = layer.targets[buttonIdx];
        if (target == null)
        {
            layer.icons[buttonIdx] = null;
            return;
        }

        string path = GetIconAssetPath(target.name);
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            layer.icons[buttonIdx] = existing;
        else
            RegenerateIcon(layer, buttonIdx, path);
    }

    public static void RegenerateIcon(DiNeMultiDresser.DresserLayer layer, int buttonIdx,
        string outputAssetPath = null)
    {
        if (layer?.targets == null || buttonIdx <= 0 || buttonIdx >= layer.targets.Count)
            return;
        GameObject target = layer.targets[buttonIdx];
        if (target == null)
            return;

        while (layer.icons.Count <= buttonIdx)
            layer.icons.Add(null);

        string path = outputAssetPath;
        if (string.IsNullOrEmpty(path))
        {
            path = AssetDatabase.GetAssetPath(layer.icons[buttonIdx]);
            if (!DiNeIconMaker.CanOverwriteAsset(path))
                path = GetIconAssetPath(target.name);
        }

        Texture2D generated = DiNeIconMaker.GenerateIcon(
            target,
            null,
            path,
            new DiNeIconMaker.Settings { outlineEnabled = false });
        if (generated != null)
            layer.icons[buttonIdx] = generated;
    }

    public static void ReleaseIconReference(ref Texture2D icon)
    {
        icon = null;
    }

    public static void ReleaseIcons(DiNeMultiDresser context)
    {
        if (context == null || context.layers == null)
            return;

        foreach (DiNeMultiDresser.DresserLayer layer in context.layers)
        {
            if (layer?.icons == null)
                continue;

            for (int i = 0; i < layer.icons.Count; i++)
                layer.icons[i] = null;
        }
    }

    public static string GetIconAssetPath(string iconName)
    {
        return DiNeIconMaker.GetDefaultIconAssetPath(iconName);
    }
}
#endif
