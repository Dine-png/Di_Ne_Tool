#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>라이팅 디자이너가 제어할 수 있는 항목.</summary>
public enum DiNeLightingControl
{
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
        new DiNeLightingControlDef(DiNeLightingControl.LightMin, "최소 밝기", "LightMin", DiNeLightingControlKind.Radial, 0.5f,
            "어두운 곳에서 아바타가 얼마나 밝게 보일지. 라이트 리미트의 하한."),
        new DiNeLightingControlDef(DiNeLightingControl.LightMax, "최대 밝기", "LightMax", DiNeLightingControlKind.Radial, 1f,
            "밝은 곳에서 아바타가 얼마나 밝아질지. 라이트 리미트의 상한."),
        new DiNeLightingControlDef(DiNeLightingControl.Unlit, "Unlit", "Unlit", DiNeLightingControlKind.Radial, 0f,
            "라이팅을 무시하고 텍스처 원본 색으로 표시하는 정도."),
        new DiNeLightingControlDef(DiNeLightingControl.Saturation, "채도", "Saturation", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 낮추면 탁해지고 올리면 선명해진다."),
        new DiNeLightingControlDef(DiNeLightingControl.Hue, "색조", "Hue", DiNeLightingControlKind.Radial, 0f,
            "색상환을 따라 전체 색을 회전시킨다. 0이 원본."),
        new DiNeLightingControlDef(DiNeLightingControl.Brightness, "명도", "Brightness", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 텍스처 자체의 밝기를 조절한다."),
        new DiNeLightingControlDef(DiNeLightingControl.Gamma, "감마", "Gamma", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 중간톤의 대비를 조절한다."),
        new DiNeLightingControlDef(DiNeLightingControl.ColorTemperature, "색온도", "ColorTemp", DiNeLightingControlKind.Radial, 0.5f,
            "0.5가 원본. 낮추면 따뜻하게(주황), 올리면 차갑게(파랑)."),
        new DiNeLightingControlDef(DiNeLightingControl.Monochrome, "흑백화", "Monochrome", DiNeLightingControlKind.Radial, 0f,
            "라이팅의 색 성분을 제거해 조명 색에 덜 물들게 한다."),
        new DiNeLightingControlDef(DiNeLightingControl.Emission, "발광 강도", "Emission", DiNeLightingControlKind.Radial, 1f,
            "Emission 블렌드 강도. 0이면 발광이 꺼진다."),
        new DiNeLightingControlDef(DiNeLightingControl.ShadowStrength, "그림자 농도", "ShadowStrength", DiNeLightingControlKind.Radial, 0f,
            "그림자가 얼마나 진하게 깔릴지."),
        new DiNeLightingControlDef(DiNeLightingControl.ShadowBorder, "그림자 경계", "ShadowBorder", DiNeLightingControlKind.Radial, 0.5f,
            "그림자가 지는 경계선의 위치."),
        new DiNeLightingControlDef(DiNeLightingControl.OutlineTint, "아웃라인 색", "OutlineTint", DiNeLightingControlKind.Radial, 0f,
            "아웃라인 색을 지정한 두 색 사이에서 보간한다."),
        new DiNeLightingControlDef(DiNeLightingControl.OutlineWidth, "아웃라인 두께", "OutlineWidth", DiNeLightingControlKind.Radial, 0.5f,
            "아웃라인 굵기."),
        new DiNeLightingControlDef(DiNeLightingControl.Reflectance, "반사/광택", "Reflectance", DiNeLightingControlKind.Radial, 0f,
            "표면 반사율. 올리면 광택이 강해진다."),
        new DiNeLightingControlDef(DiNeLightingControl.LightDirection, "라이트 방향 고정", "LightDir", DiNeLightingControlKind.Toggle, 0f,
            "월드 조명 방향을 무시하고 지정한 방향에서 빛이 오는 것처럼 고정한다."),
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
