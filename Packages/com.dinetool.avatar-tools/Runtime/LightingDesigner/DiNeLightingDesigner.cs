#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 라이팅 디자이너: 아바타 하위에 두면 업로드 시 자동으로 FX/메뉴/파라미터에 조명 조절 기능을 설치한다.
/// Modular Avatar 없이 동작하며, 실제 설치는 DiNeMultiDresserAutoApply의 임시 세션에서 수행된다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("DiNe/Lighting Designer")]
public sealed class DiNeLightingDesigner : MonoBehaviour
{
    /// <summary>제어 항목 하나의 사용자 설정.</summary>
    [System.Serializable]
    public sealed class ControlSetting
    {
        public DiNeLightingControl control;
        public bool enabled;
        public bool saved = true;
        [Range(0f, 1f)] public float initialValue = 0.5f;
        public string displayNameOverride;
        public Texture2D icon;
    }

    /// <summary>일부 렌더러만 따로 조절하기 위한 그룹. (예: 얼굴만 밝게)</summary>
    [System.Serializable]
    public sealed class RendererGroup
    {
        public string name = "새 그룹";
        public Texture2D icon;
        public List<Renderer> renderers = new List<Renderer>();

        /// <summary>이 그룹이 독립 슬라이더를 갖는 항목들. 나머지는 공용 슬라이더를 따라간다.</summary>
        public List<DiNeLightingControl> separateControls = new List<DiNeLightingControl>();

        public bool Separates(DiNeLightingControl control)
        {
            return separateControls != null && separateControls.Contains(control);
        }
    }

    /// <summary>메뉴 버튼 하나로 여러 슬라이더를 한꺼번에 지정 값으로 옮기는 프리셋.</summary>
    [System.Serializable]
    public sealed class PresetEntry
    {
        public string name = "새 프리셋";
        public Texture2D icon;

        // control/value를 병렬 리스트로 직렬화한다(Dictionary는 Unity가 직렬화하지 못한다).
        public List<DiNeLightingControl> controls = new List<DiNeLightingControl>();
        public List<float> values = new List<float>();

        public bool TryGetValue(DiNeLightingControl control, out float value)
        {
            int index = controls != null ? controls.IndexOf(control) : -1;
            if (index >= 0 && values != null && index < values.Count)
            {
                value = values[index];
                return true;
            }
            value = 0f;
            return false;
        }

        public void SetValue(DiNeLightingControl control, float value)
        {
            if (controls == null) controls = new List<DiNeLightingControl>();
            if (values == null) values = new List<float>();

            int index = controls.IndexOf(control);
            if (index < 0)
            {
                controls.Add(control);
                values.Add(value);
                return;
            }

            while (values.Count <= index) values.Add(0f);
            values[index] = value;
        }

        public void Remove(DiNeLightingControl control)
        {
            int index = controls != null ? controls.IndexOf(control) : -1;
            if (index < 0) return;
            controls.RemoveAt(index);
            if (values != null && index < values.Count) values.RemoveAt(index);
        }
    }

    // ── 메뉴 ─────────────────────────────────────────────
    [SerializeField] private string menuName = "Lighting Designer";
    [SerializeField] private Texture2D menuIcon;

    // ── 전체 On/Off ──────────────────────────────────────
    [SerializeField] private bool useEnableToggle = true;
    [SerializeField] private bool enableDefaultOn;
    [SerializeField] private bool enableSaved = true;

    // ── 밝기 범위 ────────────────────────────────────────
    [SerializeField, Range(0f, 10f)] private float minLightValue;
    [SerializeField, Range(0f, 10f)] private float maxLightValue = 1f;
    [SerializeField] private bool overwriteDefaultLightMinMax = true;
    [SerializeField, Range(0f, 1f)] private float defaultMinLightValue;
    [SerializeField, Range(0f, 1f)] private float defaultMaxLightValue = 1f;

    // ── 항목별 부가 설정 ─────────────────────────────────
    [SerializeField] private Vector3 lightDirection = new Vector3(0f, 1f, -0.5f);
    [SerializeField] private Color outlineTintFrom = Color.black;
    [SerializeField] private Color outlineTintTo = Color.white;
    [SerializeField, Range(0f, 1f)] private float outlineWidthMax = 0.1f;
    [SerializeField, Range(0f, 1f)] private float reflectanceMax = 1f;

    // ── 대상 ─────────────────────────────────────────────
    [SerializeField] private DiNeLightingTargetShaders targetShaders = DiNeLightingTargetShaders.Everything;
    [SerializeField] private List<Renderer> excludes = new List<Renderer>();

    [SerializeField] private List<ControlSetting> controls = new List<ControlSetting>();
    [SerializeField] private List<RendererGroup> groups = new List<RendererGroup>();
    [SerializeField] private List<PresetEntry> presets = new List<PresetEntry>();

    public string MenuName { get => menuName; set => menuName = value; }
    public Texture2D MenuIcon { get => menuIcon; set => menuIcon = value; }
    public bool UseEnableToggle { get => useEnableToggle; set => useEnableToggle = value; }
    public bool EnableDefaultOn { get => enableDefaultOn; set => enableDefaultOn = value; }
    public bool EnableSaved { get => enableSaved; set => enableSaved = value; }
    public float MinLightValue { get => minLightValue; set => minLightValue = value; }
    public float MaxLightValue { get => maxLightValue; set => maxLightValue = value; }
    public bool OverwriteDefaultLightMinMax { get => overwriteDefaultLightMinMax; set => overwriteDefaultLightMinMax = value; }
    public float DefaultMinLightValue { get => defaultMinLightValue; set => defaultMinLightValue = value; }
    public float DefaultMaxLightValue { get => defaultMaxLightValue; set => defaultMaxLightValue = value; }
    public Vector3 LightDirection { get => lightDirection; set => lightDirection = value; }
    public Color OutlineTintFrom { get => outlineTintFrom; set => outlineTintFrom = value; }
    public Color OutlineTintTo { get => outlineTintTo; set => outlineTintTo = value; }
    public float OutlineWidthMax { get => outlineWidthMax; set => outlineWidthMax = value; }
    public float ReflectanceMax { get => reflectanceMax; set => reflectanceMax = value; }
    public DiNeLightingTargetShaders TargetShaders { get => targetShaders; set => targetShaders = value; }
    public List<Renderer> Excludes => excludes;
    public List<ControlSetting> Controls => controls;
    public List<RendererGroup> Groups => groups;
    public List<PresetEntry> Presets => presets;

    private void Reset()
    {
        EnsureDefaults();
        GetSetting(DiNeLightingControl.LightMin).enabled = true;
        GetSetting(DiNeLightingControl.LightMax).enabled = true;
    }

    /// <summary>정의 테이블에 있는 모든 항목의 설정 슬롯을 보장하고, 사라진 항목을 정리한다.</summary>
    public void EnsureDefaults()
    {
        if (controls == null)
            controls = new List<ControlSetting>();

        if (string.IsNullOrWhiteSpace(menuName))
            menuName = "Lighting Designer";

        controls.RemoveAll(setting => setting == null || DiNeLightingControlDef.Get(setting.control) == null);

        foreach (var def in DiNeLightingControlDef.All)
        {
            if (controls.Exists(setting => setting.control == def.Control))
                continue;

            controls.Add(new ControlSetting
            {
                control = def.Control,
                enabled = false,
                saved = true,
                initialValue = def.DefaultInitialValue
            });
        }

        // 정의 테이블 순서로 정렬해 인스펙터 표시 순서를 고정한다.
        controls.Sort((a, b) => a.control.CompareTo(b.control));

        if (maxLightValue < minLightValue)
            maxLightValue = minLightValue;

        if (groups == null) groups = new List<RendererGroup>();
        groups.RemoveAll(group => group == null);
        foreach (var group in groups)
        {
            if (group.renderers == null) group.renderers = new List<Renderer>();
            if (group.separateControls == null) group.separateControls = new List<DiNeLightingControl>();

            // 꺼진 항목이 그룹에만 남아 있으면 파라미터가 붕 뜨므로 정리한다.
            group.separateControls.RemoveAll(control => !IsEnabled(control));
        }

        if (presets == null) presets = new List<PresetEntry>();
        presets.RemoveAll(preset => preset == null);
        foreach (var preset in presets)
        {
            if (preset.controls == null) preset.controls = new List<DiNeLightingControl>();
            if (preset.values == null) preset.values = new List<float>();
            while (preset.values.Count < preset.controls.Count) preset.values.Add(0f);
            while (preset.values.Count > preset.controls.Count) preset.values.RemoveAt(preset.values.Count - 1);

            for (int i = preset.controls.Count - 1; i >= 0; i--)
            {
                if (!IsEnabled(preset.controls[i]))
                    preset.Remove(preset.controls[i]);
            }
        }
    }

    public ControlSetting GetSetting(DiNeLightingControl control)
    {
        EnsureDefaults();
        return controls.Find(setting => setting.control == control);
    }

    public bool IsEnabled(DiNeLightingControl control)
    {
        var setting = controls?.Find(item => item != null && item.control == control);
        return setting != null && setting.enabled;
    }

    public string GetDisplayName(DiNeLightingControl control)
    {
        var setting = GetSetting(control);
        if (setting != null && !string.IsNullOrWhiteSpace(setting.displayNameOverride))
            return setting.displayNameOverride.Trim();
        return DiNeLightingControlDef.Get(control)?.DisplayName ?? control.ToString();
    }

    /// <summary>
    /// 이 컴포넌트가 소비하는 동기화 파라미터 비트 수.
    /// 프리셋은 Parameter Driver로 동작하고 파라미터를 비동기화로 두기 때문에 0비트다.
    /// </summary>
    public int CalculateParameterCost()
    {
        int cost = useEnableToggle ? 1 : 0;
        if (controls == null) return cost;

        foreach (var setting in controls)
        {
            if (setting == null || !setting.enabled) continue;
            var def = DiNeLightingControlDef.Get(setting.control);
            if (def == null) continue;

            cost += def.CostBits;

            // 그룹이 따로 떼어간 항목은 그룹 수만큼 파라미터가 더 필요하다.
            if (groups == null) continue;
            foreach (var group in groups)
            {
                if (group != null && group.Separates(setting.control))
                    cost += def.CostBits;
            }
        }
        return cost;
    }

    public static string ParameterPrefix => "DiNe/Lighting/";
    public static string EnableParameterName => ParameterPrefix + "Enable";
    public static string DirectParameterName => ParameterPrefix + "One";
    public static string PresetParameterName => ParameterPrefix + "Preset";

    public static string BuildParameterName(DiNeLightingControl control)
    {
        var def = DiNeLightingControlDef.Get(control);
        return ParameterPrefix + (def != null ? def.ParameterSuffix : control.ToString());
    }

    /// <summary>그룹 전용 파라미터 이름. 그룹 이름이 겹치지 않도록 인덱스를 함께 넣는다.</summary>
    public static string BuildParameterName(DiNeLightingControl control, RendererGroup group, int groupIndex)
    {
        if (group == null) return BuildParameterName(control);

        var def = DiNeLightingControlDef.Get(control);
        string suffix = def != null ? def.ParameterSuffix : control.ToString();
        return $"{ParameterPrefix}G{groupIndex}/{suffix}";
    }

    /// <summary>메뉴에 보일 그룹 이름(비어 있으면 기본값).</summary>
    public static string GetGroupDisplayName(RendererGroup group, int groupIndex)
    {
        if (group != null && !string.IsNullOrWhiteSpace(group.name))
            return group.name.Trim();
        return "그룹 " + (groupIndex + 1);
    }
}
#endif
