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
                RegenerateIcon(layer, i);
            }
        }

        EditorUtility.SetDirty(context);
        AssetDatabase.SaveAssets();
        Debug.Log("[DiNe] Multi Dresser 256px 아이콘 생성을 완료했습니다.");
    }

    public static void RegenerateIcon(DiNeMultiDresser.DresserLayer layer, int buttonIdx)
    {
        if (layer == null || buttonIdx < 0)
            return;

        while (layer.icons.Count <= buttonIdx)
            layer.icons.Add(null);

        layer.icons[buttonIdx] = null;
        if (buttonIdx == 0 || layer.targets == null || buttonIdx >= layer.targets.Count)
            return;

        GameObject target = layer.targets[buttonIdx];
        if (target == null)
            return;

        layer.icons[buttonIdx] = DiNeIconMaker.GenerateIcon(
            target,
            null,
            GetIconAssetPath(target.name),
            new DiNeIconMaker.Settings { outlineEnabled = false });
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
