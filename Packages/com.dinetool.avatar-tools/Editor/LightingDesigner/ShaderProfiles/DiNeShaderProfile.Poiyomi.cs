#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poiyomi 매핑.
///
/// 두 가지를 반드시 빌드 시점에 처리해야 애니메이션이 실제로 먹는다.
/// 1) 메인 색 보정은 [ThryToggle(COLOR_GRADING_HDR)] 키워드로 켜진다.
///    _MainColorAdjustToggle 값을 애니메이션해도 키워드는 안 켜지므로, 머티리얼에서 직접 켜야 한다.
/// 2) Poiyomi 잠금(최적화)은 애니메이션되지 않는 프로퍼티를 상수로 인라인한다.
///    "&lt;프로퍼티&gt;Animated" 오버라이드 태그를 붙여두면 잠금 후에도 유니폼으로 남는다.
/// </summary>
internal sealed class DiNeShaderProfilePoiyomi : DiNeShaderProfile
{
    public static readonly DiNeShaderProfilePoiyomi Instance = new DiNeShaderProfilePoiyomi();

    private const string LightingMinLightBrightness = "_LightingMinLightBrightness";
    private const string LightingCap = "_LightingCap";
    private const string MainColorAdjustToggle = "_MainColorAdjustToggle";
    private const string MainColorAdjustTexture = "_MainColorAdjustTexture";
    private const string Saturation = "_Saturation";
    private const string MainHueShift = "_MainHueShift";
    private const string MainHueShiftReplace = "_MainHueShiftReplace";
    private const string MainBrightness = "_MainBrightness";
    private const string MonochromeLighting = "_LightingMonochromatic";
    private const string ShadowStrength = "_ShadowStrength";
    private const string LineColor = "_LineColor";
    private const string LineWidth = "_LineWidth";
    private const string Reflectance = "_Reflectance";
    private const string ColorMain = "_Color";
    private const string MainTex = "_MainTex";

    /// <summary>메인 색 보정을 켜는 셰이더 키워드.</summary>
    private const string ColorAdjustKeyword = "COLOR_GRADING_HDR";

    /// <summary>Poiyomi 최적화기가 읽는 "애니메이션됨" 태그 접미사.</summary>
    private const string AnimatedTagSuffix = "Animated";

    public override string Name => "Poiyomi";
    public override DiNeLightingTargetShaders Flag => DiNeLightingTargetShaders.Poiyomi;

    public override bool IsTarget(Shader shader)
    {
        return shader != null && shader.name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public override bool Supports(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
            case DiNeLightingControl.Saturation:
            case DiNeLightingControl.Hue:
            case DiNeLightingControl.Brightness:
            case DiNeLightingControl.ColorTemperature:
            case DiNeLightingControl.Monochrome:
            case DiNeLightingControl.ShadowStrength:
            case DiNeLightingControl.OutlineTint:
            case DiNeLightingControl.OutlineWidth:
            case DiNeLightingControl.Reflectance:
                return true;

            // Poiyomi에 대응 프로퍼티가 없는 항목들.
            case DiNeLightingControl.Gamma:
            case DiNeLightingControl.Unlit:
            case DiNeLightingControl.Emission:
            case DiNeLightingControl.ShadowBorder:
            case DiNeLightingControl.LightDirection:
            default:
                return false;
        }
    }

    // ──────────────────────────────────────────────────
    //  애니메이션 값
    // ──────────────────────────────────────────────────

    public override void WriteDefault(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
                sink.SetFloatConstant(LightingMinLightBrightness, context.DefaultMinLight);
                sink.SetFloatConstant(LightingCap, context.DefaultMaxLight);
                break;
            case DiNeLightingControl.Saturation:
                sink.SetFloatConstant(Saturation, 0f);
                break;
            case DiNeLightingControl.Hue:
                sink.SetFloatConstant(MainHueShift, 0f);
                break;
            case DiNeLightingControl.Brightness:
                sink.SetFloatConstant(MainBrightness, 0f);
                break;
            case DiNeLightingControl.ColorTemperature:
                sink.SetColorConstant(ColorMain, Color.white);
                break;
            case DiNeLightingControl.Monochrome:
                sink.SetFloatConstant(MonochromeLighting, context.DefaultMonochrome);
                break;
            case DiNeLightingControl.ShadowStrength:
                sink.SetFloatConstant(ShadowStrength, ReadFloat(context.Materials, ShadowStrength, 1f));
                break;
            case DiNeLightingControl.OutlineTint:
                sink.SetColorConstant(LineColor, ReadColor(context.Materials, LineColor, Color.black));
                break;
            case DiNeLightingControl.OutlineWidth:
                sink.SetFloatConstant(LineWidth, ReadFloat(context.Materials, LineWidth, 0.1f));
                break;
            case DiNeLightingControl.Reflectance:
                sink.SetFloatConstant(Reflectance, ReadFloat(context.Materials, Reflectance, 0.04f));
                break;
        }
    }

    public override void WriteControl(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
                sink.SetFloatRange(LightingMinLightBrightness, context.MinLight, context.MaxLight);
                sink.SetFloatRange(LightingCap, context.MinLight, context.MaxLight);
                break;
            case DiNeLightingControl.Saturation:
                // Poiyomi의 _Saturation은 0이 원본, -1이 완전 흑백. 상한은 10까지 열려 있지만
                // 슬라이더로 쓰기엔 과해서 1까지만 쓴다.
                sink.SetFloatRange(Saturation, -1f, 1f);
                break;
            case DiNeLightingControl.Hue:
                sink.SetFloatRange(MainHueShift, 0f, 1f);
                break;
            case DiNeLightingControl.Brightness:
                // Range(-1,1) 가산. 0이 원본이므로 슬라이더 중앙(0.5)이 원본이 된다.
                sink.SetFloatRange(MainBrightness, -1f, 1f);
                break;
            case DiNeLightingControl.ColorTemperature:
                sink.SetColor(0f, ColorMain, new Color(0.6f, 0.95f, 1f, 1f));
                sink.SetColor(0.5f, ColorMain, Color.white);
                sink.SetColor(1f, ColorMain, new Color(1f, 0.8f, 0.6f, 1f));
                break;
            case DiNeLightingControl.Monochrome:
                sink.SetFloatRange(MonochromeLighting, 0f, 1f);
                break;
            case DiNeLightingControl.ShadowStrength:
                sink.SetFloatRange(ShadowStrength, 0f, 1f);
                break;
            case DiNeLightingControl.OutlineTint:
                sink.SetColorRange(LineColor, context.OutlineTintFrom, context.OutlineTintTo);
                break;
            case DiNeLightingControl.OutlineWidth:
                sink.SetFloatRange(LineWidth, 0f, context.OutlineWidthMax);
                break;
            case DiNeLightingControl.Reflectance:
                sink.SetFloatRange(Reflectance, 0f, context.ReflectanceMax);
                break;
        }
    }

    // ──────────────────────────────────────────────────
    //  머티리얼 준비 (키워드 / 잠금 대응 태그)
    // ──────────────────────────────────────────────────

    /// <summary>Poiyomi는 항목이 하나라도 켜져 있으면 Animated 태그를 붙여야 하므로 항상 준비가 필요하다.</summary>
    public override bool RequiresMaterialPreparation(ISet<DiNeLightingControl> controls)
    {
        foreach (var control in controls)
            if (Supports(control))
                return true;
        return false;
    }

    public override void PrepareMaterial(Material material, ISet<DiNeLightingControl> controls)
    {
        if (material == null) return;

        bool usesColorAdjust =
            controls.Contains(DiNeLightingControl.Hue) ||
            controls.Contains(DiNeLightingControl.Saturation) ||
            controls.Contains(DiNeLightingControl.Brightness);

        if (usesColorAdjust)
        {
            // float만 바꿔서는 안 되고 키워드를 켜야 실제로 색 보정 블록이 컴파일된다.
            material.EnableKeyword(ColorAdjustKeyword);
            if (material.HasProperty(MainColorAdjustToggle))
                material.SetFloat(MainColorAdjustToggle, 1f);
        }

        // 잠긴(최적화된) 머티리얼에서도 프로퍼티가 유니폼으로 남도록 표시한다.
        foreach (var control in controls)
        {
            foreach (string property in GetAnimatedProperties(control))
            {
                if (material.HasProperty(property))
                    material.SetOverrideTag(property + AnimatedTagSuffix, "1");
            }
        }
    }

    /// <summary>해당 항목이 실제로 애니메이션하는 Poiyomi 프로퍼티들.</summary>
    private static IEnumerable<string> GetAnimatedProperties(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
                yield return LightingMinLightBrightness;
                yield return LightingCap;
                break;
            case DiNeLightingControl.Saturation: yield return Saturation; break;
            case DiNeLightingControl.Hue: yield return MainHueShift; break;
            case DiNeLightingControl.Brightness: yield return MainBrightness; break;
            case DiNeLightingControl.ColorTemperature: yield return ColorMain; break;
            case DiNeLightingControl.Monochrome: yield return MonochromeLighting; break;
            case DiNeLightingControl.ShadowStrength: yield return ShadowStrength; break;
            case DiNeLightingControl.OutlineTint: yield return LineColor; break;
            case DiNeLightingControl.OutlineWidth: yield return LineWidth; break;
            case DiNeLightingControl.Reflectance: yield return Reflectance; break;
        }
    }

    // ──────────────────────────────────────────────────
    //  베이킹
    // ──────────────────────────────────────────────────

    private static Shader _bakerShader;

    private static Shader BakerShader
    {
        get
        {
            if (_bakerShader == null)
            {
                _bakerShader = DiNePackageAssets.LoadAsset<Shader>(
                    "Editor/LightingDesigner/Baking/DiNePoiyomiColorAdjustBaker.shader");
            }
            if (_bakerShader == null)
                _bakerShader = Shader.Find("Hidden/DiNe/LightingDesigner/PoiyomiColorAdjustBaker");
            return _bakerShader;
        }
    }

    /// <summary>
    /// 머티리얼에 이미 들어가 있는 _Color / 색조 / 채도 / 명도를 텍스처에 구워 넣고 프로퍼티를 중립으로 되돌린다.
    /// 그래야 슬라이더 중앙이 "원본 색"이 된다. 연산 순서는 Poiyomi 본체와 동일하다.
    /// </summary>
    public override bool NormalizeMaterial(
        Material material,
        ISet<DiNeLightingControl> controls,
        DiNeLightingBakeSession session)
    {
        if (material == null || !NeedsNormalization(controls))
            return false;

        if (!material.HasProperty(MainTex))
            return false;

        var source = material.GetTexture(MainTex);
        if (source is RenderTexture)
            return false;   // 런타임 생성 텍스처는 구울 수 없다.

        bool bakeColor = controls.Contains(DiNeLightingControl.ColorTemperature);
        bool bakeHue = controls.Contains(DiNeLightingControl.Hue);
        bool bakeSaturation = controls.Contains(DiNeLightingControl.Saturation);
        bool bakeBrightness = controls.Contains(DiNeLightingControl.Brightness);

        var color = bakeColor ? ReadColor(new[] { material }, ColorMain, Color.white) : Color.white;
        float hue = bakeHue ? ReadFloat(new[] { material }, MainHueShift, 0f) : 0f;
        float saturation = bakeSaturation ? ReadFloat(new[] { material }, Saturation, 0f) : 0f;
        float brightness = bakeBrightness ? ReadFloat(new[] { material }, MainBrightness, 0f) : 0f;

        bool needed = color != Color.white
            || !Mathf.Approximately(hue, 0f)
            || !Mathf.Approximately(saturation, 0f)
            || !Mathf.Approximately(brightness, 0f);

        if (!needed)
            return false;

        var shader = BakerShader;
        if (shader == null)
        {
            Debug.LogWarning(
                "[DiNe 라이팅 디자이너] Poiyomi 베이커 셰이더를 찾지 못해 머티리얼 정규화를 건너뜁니다. " +
                "채도/색조/명도 슬라이더의 중앙이 원본 색과 달라질 수 있습니다.");
            return false;
        }

        var mask = material.HasProperty(MainColorAdjustTexture) ? material.GetTexture(MainColorAdjustTexture) : null;
        float hueReplace = material.HasProperty(MainHueShiftReplace) ? material.GetFloat(MainHueShiftReplace) : 1f;

        var baker = new Material(shader);
        baker.SetTexture(MainTex, source != null ? source : Texture2D.whiteTexture);
        baker.SetColor(ColorMain, color);
        baker.SetTexture(MainColorAdjustTexture, mask != null ? mask : Texture2D.whiteTexture);
        baker.SetFloat(MainHueShift, hue);
        baker.SetFloat(MainHueShiftReplace, hueReplace);
        baker.SetFloat(Saturation, saturation);
        baker.SetFloat(MainBrightness, brightness);

        int hash = HashOf(source, color, mask, hue, hueReplace, saturation, brightness);
        var baked = session.Bake(hash, source, baker, color.a < 1f);
        UnityEngine.Object.DestroyImmediate(baker);

        material.SetTexture(MainTex, baked);

        if (bakeColor && material.HasProperty(ColorMain)) material.SetColor(ColorMain, Color.white);
        if (bakeHue && material.HasProperty(MainHueShift)) material.SetFloat(MainHueShift, 0f);
        if (bakeSaturation && material.HasProperty(Saturation)) material.SetFloat(Saturation, 0f);
        if (bakeBrightness && material.HasProperty(MainBrightness)) material.SetFloat(MainBrightness, 0f);

        // 마스크까지 구웠으므로 더 이상 적용하면 안 된다.
        if (mask != null && (bakeHue || bakeSaturation || bakeBrightness))
            material.SetTexture(MainColorAdjustTexture, null);

        return true;
    }

    private static int HashOf(Texture source, Color color, Texture mask, float hue, float hueReplace, float saturation, float brightness)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (source != null ? source.GetInstanceID() : 0);
            hash = hash * 31 + color.GetHashCode();
            hash = hash * 31 + (mask != null ? mask.GetInstanceID() : 0);
            hash = hash * 31 + hue.GetHashCode();
            hash = hash * 31 + hueReplace.GetHashCode();
            hash = hash * 31 + saturation.GetHashCode();
            hash = hash * 31 + brightness.GetHashCode();
            return hash;
        }
    }

    public override bool TryGetLightRange(Material[] materials, out float min, out float max)
    {
        min = ReadFloat(materials, LightingMinLightBrightness, 0f);
        max = ReadFloat(materials, LightingCap, 1f);
        return true;
    }

    /// <summary>Poiyomi 잠금(최적화) 상태인지.</summary>
    public static bool IsLocked(Material material)
    {
        return material != null
            && material.HasProperty("_ShaderOptimizerEnabled")
            && material.GetFloat("_ShaderOptimizerEnabled") > 0.5f;
    }

    private static float ReadFloat(Material[] materials, string property, float fallback)
    {
        if (materials == null) return fallback;
        foreach (var material in materials)
            if (material != null && material.HasProperty(property))
                return material.GetFloat(property);
        return fallback;
    }

    private static Color ReadColor(Material[] materials, string property, Color fallback)
    {
        if (materials == null) return fallback;
        foreach (var material in materials)
            if (material != null && material.HasProperty(property))
                return material.GetColor(property);
        return fallback;
    }
}
#endif
