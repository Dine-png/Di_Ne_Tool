#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Multi Dresser 아이콘 생성과 참조 관리를 담당한다.</summary>
public static class DiNeMultiIconGenerator
{
    private const string CustomDefaultKey           = "DiNe_MD_DefaultIcon_Customized";
    private const string DefaultPitchKey            = "DiNe_MD_DefaultIcon_Pitch";
    private const string DefaultYawKey              = "DiNe_MD_DefaultIcon_Yaw";
    private const string DefaultPanXKey             = "DiNe_MD_DefaultIcon_PanRelX";
    private const string DefaultPanYKey             = "DiNe_MD_DefaultIcon_PanRelY";
    // 1.8.6부터 촬영 범위를 실제 메시로 재므로, 그 전 바운드 기준으로 맞춘 줌 값은 옷을 잘라 먹어 새 키로 버린다.
    private const string DefaultZoomKey             = "DiNe_MD_DefaultIcon_ZoomV2";
    private const string DefaultIdlePoseKey         = "DiNe_MD_DefaultIcon_IdlePose";
    private const string DefaultOutlineKey          = "DiNe_MD_DefaultIcon_Outline";
    private const string DefaultOutlineColorKey     = "DiNe_MD_DefaultIcon_OutlineColor";
    private const string DefaultOutlineSizeKey      = "DiNe_MD_DefaultIcon_OutlineSize";
    private const string DefaultForbiddenKey        = "DiNe_MD_DefaultIcon_Forbidden";
    private const string DefaultForbiddenOpacityKey = "DiNe_MD_DefaultIcon_ForbiddenOpacity";
    private const string DefaultForbiddenScaleKey   = "DiNe_MD_DefaultIcon_ForbiddenScale";
    private const string DefaultForbiddenBehindKey  = "DiNe_MD_DefaultIcon_ForbiddenBehind";

    public struct CapturePreset
    {
        public Vector2 euler;
        public Vector2 pan;
        public float zoom;
        public bool idlePose;
        public DiNeIconMaker.Settings effects;
    }

    public static bool HasCustomDefaultCapture => EditorPrefs.GetBool(CustomDefaultKey, false);

    public static CapturePreset GetDefaultCapturePreset()
    {
        var defaultOutlineColor = new Color(0.03f, 0.03f, 0.03f, 1f);
        if (!HasCustomDefaultCapture)
        {
            return new CapturePreset
            {
                euler = new Vector2(0f, 180f),
                pan = Vector2.zero,
                zoom = 1f,
                idlePose = false,
                effects = new DiNeIconMaker.Settings
                {
                    outlineEnabled = false,
                    outlineColor = defaultOutlineColor,
                    outlineSize = 4,
                    forbiddenOverlay = false,
                    forbiddenOpacity = 1f,
                    forbiddenScale = 0.85f,
                    forbiddenBehindObject = true
                }
            };
        }

        Color outlineColor = defaultOutlineColor;
        if (EditorPrefs.HasKey(DefaultOutlineColorKey))
        {
            ColorUtility.TryParseHtmlString(
                "#" + EditorPrefs.GetString(DefaultOutlineColorKey), out outlineColor);
        }

        return new CapturePreset
        {
            euler = new Vector2(
                EditorPrefs.GetFloat(DefaultPitchKey, 0f),
                EditorPrefs.GetFloat(DefaultYawKey, 180f)),
            pan = new Vector2(
                EditorPrefs.GetFloat(DefaultPanXKey, 0f),
                EditorPrefs.GetFloat(DefaultPanYKey, 0f)),
            zoom = Mathf.Clamp(EditorPrefs.GetFloat(DefaultZoomKey, 1f), 0.2f, 5f),
            idlePose = EditorPrefs.GetBool(DefaultIdlePoseKey, false),
            effects = new DiNeIconMaker.Settings
            {
                outlineEnabled = EditorPrefs.GetBool(DefaultOutlineKey, false),
                outlineColor = outlineColor,
                outlineSize = Mathf.Clamp(EditorPrefs.GetInt(DefaultOutlineSizeKey, 4), 1, 12),
                forbiddenOverlay = EditorPrefs.GetBool(DefaultForbiddenKey, false),
                forbiddenOpacity = Mathf.Clamp01(EditorPrefs.GetFloat(DefaultForbiddenOpacityKey, 1f)),
                forbiddenScale = Mathf.Clamp(EditorPrefs.GetFloat(DefaultForbiddenScaleKey, 0.85f), 0.2f, 1.2f),
                forbiddenBehindObject = EditorPrefs.GetBool(DefaultForbiddenBehindKey, true)
            }
        };
    }

    public static void SaveDefaultCapturePreset(
        Vector2 euler,
        Vector2 pan,
        float zoom,
        DiNeIconMaker.Settings effects,
        bool idlePose)
    {
        effects = effects ?? new DiNeIconMaker.Settings { outlineEnabled = false };
        EditorPrefs.SetBool(CustomDefaultKey, true);
        EditorPrefs.SetFloat(DefaultPitchKey, euler.x);
        EditorPrefs.SetFloat(DefaultYawKey, euler.y);
        EditorPrefs.SetFloat(DefaultPanXKey, pan.x);
        EditorPrefs.SetFloat(DefaultPanYKey, pan.y);
        EditorPrefs.SetFloat(DefaultZoomKey, Mathf.Clamp(zoom, 0.2f, 5f));
        EditorPrefs.SetBool(DefaultIdlePoseKey, idlePose);
        EditorPrefs.SetBool(DefaultOutlineKey, effects.outlineEnabled);
        EditorPrefs.SetString(DefaultOutlineColorKey, ColorUtility.ToHtmlStringRGBA(effects.outlineColor));
        EditorPrefs.SetInt(DefaultOutlineSizeKey, Mathf.Clamp(effects.outlineSize, 1, 12));
        EditorPrefs.SetBool(DefaultForbiddenKey, effects.forbiddenOverlay);
        EditorPrefs.SetFloat(DefaultForbiddenOpacityKey, Mathf.Clamp01(effects.forbiddenOpacity));
        EditorPrefs.SetFloat(DefaultForbiddenScaleKey, Mathf.Clamp(effects.forbiddenScale, 0.2f, 1.2f));
        EditorPrefs.SetBool(DefaultForbiddenBehindKey, effects.forbiddenBehindObject);
    }

    public static void ResetDefaultCapturePreset()
    {
        EditorPrefs.DeleteKey(CustomDefaultKey);
        EditorPrefs.DeleteKey(DefaultPitchKey);
        EditorPrefs.DeleteKey(DefaultYawKey);
        EditorPrefs.DeleteKey(DefaultPanXKey);
        EditorPrefs.DeleteKey(DefaultPanYKey);
        EditorPrefs.DeleteKey(DefaultZoomKey);
        EditorPrefs.DeleteKey(DefaultIdlePoseKey);
        EditorPrefs.DeleteKey(DefaultOutlineKey);
        EditorPrefs.DeleteKey(DefaultOutlineColorKey);
        EditorPrefs.DeleteKey(DefaultOutlineSizeKey);
        EditorPrefs.DeleteKey(DefaultForbiddenKey);
        EditorPrefs.DeleteKey(DefaultForbiddenOpacityKey);
        EditorPrefs.DeleteKey(DefaultForbiddenScaleKey);
        EditorPrefs.DeleteKey(DefaultForbiddenBehindKey);
    }

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

    /// <summary>
    /// 현재 설정된 기본 촬영 구도·효과로 멀티 드레서의 등록된 모든 의상 아이콘을 다시 캡처해 덮어쓴다.
    /// </summary>
    public static int RegenerateAllIcons(DiNeMultiDresser context)
    {
        if (context == null || context.layers == null)
            return 0;

        Undo.RecordObject(context, "Regenerate All Multi Dresser Icons");
        int regeneratedCount = 0;
        foreach (DiNeMultiDresser.DresserLayer layer in context.layers)
        {
            if (layer?.targets == null)
                continue;

            while (layer.icons.Count < layer.targets.Count)
                layer.icons.Add(null);

            for (int i = 1; i < layer.targets.Count; i++)
            {
                if (layer.targets[i] == null)
                    continue;

                RegenerateIcon(layer, i);
                if (layer.icons[i] != null)
                    regeneratedCount++;
            }
        }

        if (regeneratedCount > 0)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(context);
            EditorUtility.SetDirty(context);
            AssetDatabase.SaveAssets();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        return regeneratedCount;
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

        CapturePreset preset = GetDefaultCapturePreset();
        Texture2D generated = HasCustomDefaultCapture
            ? DiNeScreenSaver.DiNeScreenSaver.GenerateConfiguredIcon(
                target, preset.euler, preset.pan, preset.zoom, preset.effects, path, preset.idlePose)
            : DiNeIconMaker.GenerateIcon(target, null, path, preset.effects);
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
