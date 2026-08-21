#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>라이팅 디자이너가 제어할 수 있는 항목.</summary>
public enum DiNeLightingControl
{
    // 숫자 값은 기존 컴포넌트/프리셋의 직렬화 호환을 위해 유지한다.
    // 실제로 노출·생성되는 항목은 아래 DiNeLightingControlDef.All의 공통 항목뿐이다.
    LightMin = 0,
    LightMax = 1,
    Unlit = 2,
    Saturation = 3,
    Hue = 4,
    Brightness = 5,
    Gamma = 6,
    ColorTemperature = 7,
    Monochrome = 8,
    Emission = 9,
    ShadowStrength = 10,
    ShadowBorder = 11,
    OutlineTint = 12,
    OutlineWidth = 13,
    Reflectance = 14,
    LightDirection = 15,
}

/// <summary>메뉴에 노출되는 형태. Radial은 float 8bit, Toggle은 bool 1bit를 먹는다.</summary>
public enum DiNeLightingControlKind
{
    Radial,
    Toggle,
}

[Flags]
public enum DiNeLightingTargetShaders
{
    None = 0,
    LilToon = 1 << 0,
    Poiyomi = 1 << 1,
    Sunao = 1 << 2,
    MToon = 1 << 3,
    Everything = ~0,
}

/// <summary>제어 항목의 정적 메타데이터. 인스펙터와 제너레이터가 공유한다.</summary>
public sealed class DiNeLightingControlDef
{
    public DiNeLightingControl Control { get; private set; }
    public string DisplayName { get; private set; }
    public string ParameterSuffix { get; private set; }
    public DiNeLightingControlKind Kind { get; private set; }
    public float DefaultInitialValue { get; private set; }
    public string Tooltip { get; private set; }

    public int CostBits => Kind == DiNeLightingControlKind.Toggle ? 1 : 8;

    private DiNeLightingControlDef(
        DiNeLightingControl control,
        string displayName,
        string parameterSuffix,
        DiNeLightingControlKind kind,
        float defaultInitialValue,
        string tooltip)
    {
        Control = control;
        DisplayName = displayName;
        ParameterSuffix = parameterSuffix;
        Kind = kind;
        DefaultInitialValue = defaultInitialValue;
        Tooltip = tooltip;
    }

    public static readonly DiNeLightingControlDef[] All =
    {
        // 직렬화 호환을 위해 enum 값 0(LightMin)을 재사용하지만, 실제 기능은 최소/최대를 함께 움직이는 단일 밝기다.
        new DiNeLightingControlDef(DiNeLightingControl.LightMin, "조명 밝기", "Light", DiNeLightingControlKind.Radial, 0.5f,
            "아바타의 조명 밝기를 한 번에 조절한다."),
        new DiNeLightingControlDef(DiNeLightingControl.Saturation, "채도", "Saturation", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 낮추면 탁해지고 올리면 선명해진다."),
        new DiNeLightingControlDef(DiNeLightingControl.Hue, "색조", "Hue", DiNeLightingControlKind.Radial, 0f,
            "색상환을 따라 전체 색을 회전시킨다. 0이 원본."),
        new DiNeLightingControlDef(DiNeLightingControl.Brightness, "명도", "Brightness", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 텍스처 자체의 밝기를 조절한다."),
        new DiNeLightingControlDef(DiNeLightingControl.ColorTemperature, "색온도", "ColorTemp", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 낮추면 따뜻하게(주황), 올리면 차갑게(파랑)."),
        new DiNeLightingControlDef(DiNeLightingControl.Monochrome, "흑백화", "Monochrome", DiNeLightingControlKind.Radial, 0f,
            "라이팅의 색 성분을 제거해 조명 색에 덜 물들게 한다."),
        new DiNeLightingControlDef(DiNeLightingControl.ShadowStrength, "그림자 농도", "ShadowStrength", DiNeLightingControlKind.Radial, 0f,
            "그림자가 얼마나 진하게 깔릴지."),
        new DiNeLightingControlDef(DiNeLightingControl.OutlineTint, "아웃라인 색", "OutlineTint", DiNeLightingControlKind.Radial, 0f,
            "아웃라인 색을 지정한 두 색 사이에서 보간한다."),
        new DiNeLightingControlDef(DiNeLightingControl.OutlineWidth, "아웃라인 두께", "OutlineWidth", DiNeLightingControlKind.Radial, 0.5f,
            "아웃라인 굵기."),
        new DiNeLightingControlDef(DiNeLightingControl.Reflectance, "반사/광택", "Reflectance", DiNeLightingControlKind.Radial, 0f,
            "표면 반사율. 올리면 광택이 강해진다."),
    };

    private static Dictionary<DiNeLightingControl, DiNeLightingControlDef> _byControl;

    public static DiNeLightingControlDef Get(DiNeLightingControl control)
    {
        if (_byControl == null)
        {
            _byControl = new Dictionary<DiNeLightingControl, DiNeLightingControlDef>();
            foreach (var def in All)
                _byControl[def.Control] = def;
        }
        return _byControl.TryGetValue(control, out var found) ? found : null;
    }
}
#endif
