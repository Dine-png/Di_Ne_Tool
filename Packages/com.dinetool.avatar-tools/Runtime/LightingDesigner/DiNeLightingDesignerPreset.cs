#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아바타에 종속되지 않는 라이팅 디자이너 설정 프리셋.
/// 렌더러 그룹과 제외 목록은 다른 아바타에서 유효하지 않으므로 저장하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "Lighting Designer Preset", menuName = "Di Ne/Lighting Designer Preset")]
public sealed class DiNeLightingDesignerPreset : ScriptableObject
{
    private const int CurrentFormatVersion = 1;

    [SerializeField, HideInInspector] private int formatVersion = CurrentFormatVersion;

    [SerializeField, HideInInspector] private string menuName = "Lighting Designer";
    [SerializeField, HideInInspector] private Texture2D menuIcon;
    [SerializeField, HideInInspector] private bool useEnableToggle = true;
    [SerializeField, HideInInspector] private bool enableDefaultOn;
    [SerializeField, HideInInspector] private bool enableSaved = true;

    [SerializeField, HideInInspector] private float minLightValue;
    [SerializeField, HideInInspector] private float maxLightValue = 1f;
    [SerializeField, HideInInspector] private bool overwriteDefaultLightMinMax = true;
    [SerializeField, HideInInspector] private float defaultMinLightValue;
    [SerializeField, HideInInspector] private float defaultMaxLightValue = 1f;

    [SerializeField, HideInInspector] private Color outlineTintFrom = Color.black;
    [SerializeField, HideInInspector] private Color outlineTintTo = Color.white;
    [SerializeField, HideInInspector] private float outlineWidthMax = 0.1f;
    [SerializeField, HideInInspector] private float reflectanceMax = 1f;
    [SerializeField, HideInInspector] private DiNeLightingTargetShaders targetShaders = DiNeLightingTargetShaders.Everything;

    [SerializeField, HideInInspector]
    private List<DiNeLightingDesigner.ControlSetting> controls = new List<DiNeLightingDesigner.ControlSetting>();

    [SerializeField, HideInInspector]
    private List<DiNeLightingDesigner.PresetEntry> menuPresets = new List<DiNeLightingDesigner.PresetEntry>();

    public string MenuName => menuName;
    public int FormatVersion => formatVersion;

    public int EnabledControlCount
    {
        get
        {
            int count = 0;
            if (controls == null) return count;
            foreach (var setting in controls)
            {
                if (setting != null && setting.enabled) count++;
            }
            return count;
        }
    }

    public int MenuPresetCount => menuPresets != null ? menuPresets.Count : 0;

    /// <summary>현재 컴포넌트의 이식 가능한 설정을 프리셋에 저장한다.</summary>
    public void Capture(DiNeLightingDesigner source)
    {
        if (source == null) return;

        source.EnsureDefaults();
        formatVersion = CurrentFormatVersion;

        menuName = source.MenuName;
        menuIcon = source.MenuIcon;
        useEnableToggle = source.UseEnableToggle;
        enableDefaultOn = source.EnableDefaultOn;
        enableSaved = source.EnableSaved;

        minLightValue = source.MinLightValue;
        maxLightValue = source.MaxLightValue;
        overwriteDefaultLightMinMax = source.OverwriteDefaultLightMinMax;
        defaultMinLightValue = source.DefaultMinLightValue;
        defaultMaxLightValue = source.DefaultMaxLightValue;

        outlineTintFrom = source.OutlineTintFrom;
        outlineTintTo = source.OutlineTintTo;
        outlineWidthMax = source.OutlineWidthMax;
        reflectanceMax = source.ReflectanceMax;
        targetShaders = source.TargetShaders;

        controls = new List<DiNeLightingDesigner.ControlSetting>();
        foreach (var setting in source.Controls)
        {
            if (setting != null) controls.Add(Clone(setting));
        }

        menuPresets = new List<DiNeLightingDesigner.PresetEntry>();
        foreach (var preset in source.Presets)
        {
            if (preset != null) menuPresets.Add(Clone(preset));
        }
    }

    /// <summary>
    /// 프리셋을 컴포넌트에 적용한다. 아바타별 렌더러 그룹과 제외 목록은 그대로 유지한다.
    /// </summary>
    public void ApplyTo(DiNeLightingDesigner destination)
    {
        if (destination == null) return;

        destination.MenuName = menuName;
        destination.MenuIcon = menuIcon;
        destination.UseEnableToggle = useEnableToggle;
        destination.EnableDefaultOn = enableDefaultOn;
        destination.EnableSaved = enableSaved;

        destination.MinLightValue = minLightValue;
        destination.MaxLightValue = maxLightValue;
        destination.OverwriteDefaultLightMinMax = overwriteDefaultLightMinMax;
        destination.DefaultMinLightValue = defaultMinLightValue;
        destination.DefaultMaxLightValue = defaultMaxLightValue;

        destination.OutlineTintFrom = outlineTintFrom;
        destination.OutlineTintTo = outlineTintTo;
        destination.OutlineWidthMax = outlineWidthMax;
        destination.ReflectanceMax = reflectanceMax;
        destination.TargetShaders = targetShaders;

        destination.Controls.Clear();
        if (controls != null)
        {
            foreach (var setting in controls)
            {
                if (setting != null) destination.Controls.Add(Clone(setting));
            }
        }

        destination.Presets.Clear();
        if (menuPresets != null)
        {
            foreach (var preset in menuPresets)
            {
                if (preset != null) destination.Presets.Add(Clone(preset));
            }
        }

        destination.EnsureDefaults();
    }

    private static DiNeLightingDesigner.ControlSetting Clone(DiNeLightingDesigner.ControlSetting source)
    {
        return new DiNeLightingDesigner.ControlSetting
        {
            control = source.control,
            enabled = source.enabled,
            saved = source.saved,
            initialValue = source.initialValue,
            displayNameOverride = source.displayNameOverride,
            icon = source.icon
        };
    }

    private static DiNeLightingDesigner.PresetEntry Clone(DiNeLightingDesigner.PresetEntry source)
    {
        return new DiNeLightingDesigner.PresetEntry
        {
            name = source.name,
            icon = source.icon,
            controls = source.controls != null
                ? new List<DiNeLightingControl>(source.controls)
                : new List<DiNeLightingControl>(),
            values = source.values != null
                ? new List<float>(source.values)
                : new List<float>()
        };
    }
}
#endif
