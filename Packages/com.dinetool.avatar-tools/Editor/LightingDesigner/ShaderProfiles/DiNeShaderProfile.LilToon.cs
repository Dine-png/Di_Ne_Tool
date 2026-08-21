#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>lilToon 매핑. 프로퍼티 이름은 lilToon 셰이더 소스에서 확인한 것들이다.</summary>
internal sealed class DiNeShaderProfileLilToon : DiNeShaderProfile
{
    public static readonly DiNeShaderProfileLilToon Instance = new DiNeShaderProfileLilToon();

    private const string LightMinLimit = "_LightMinLimit";
    private const string LightMaxLimit = "_LightMaxLimit";
    private const string AsUnlit = "_AsUnlit";
    private const string MainTexHSVG = "_MainTexHSVG";
    private const string MonochromeLighting = "_MonochromeLighting";
    private const string EmissionBlend = "_EmissionBlend";
    private const string Emission2ndBlend = "_Emission2ndBlend";
    private const string ShadowStrength = "_ShadowStrength";
    private const string ShadowBorder = "_ShadowBorder";
    private const string OutlineColor = "_OutlineColor";
    private const string OutlineWidth = "_OutlineWidth";
    private const string Reflectance = "_Reflectance";
    private const string LightDirectionOverride = "_LightDirectionOverride";
    private const string ColorMain = "_Color";
    private const string Color2nd = "_Color2nd";
    private const string Color3rd = "_Color3rd";

    /// <summary>lilToon이 색 보정을 하지 않는 기본 HSVG 값.</summary>
    private static readonly Vector4 NeutralHSVG = new Vector4(0f, 1f, 1f, 1f);

    /// <summary>lilToon의 _LightDirectionOverride 기본값(= 오버라이드 없음).</summary>
    private static readonly Vector4 NeutralLightDirection = new Vector4(0.001f, 0.002f, 0.001f, 0f);

    public override string Name => "lilToon";
    public override DiNeLightingTargetShaders Flag => DiNeLightingTargetShaders.LilToon;

    private static readonly string[] SignatureProperties = { LightMinLimit, LightMaxLimit, MainTexHSVG, AsUnlit };

    public override bool IsTarget(Shader shader)
    {
        if (shader == null) return false;
        if (shader.name.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // 이름에 lilToon이 없는 커스텀 셰이더 대응: 핵심 프로퍼티가 전부 있으면 lilToon 계열로 본다.
        return SignatureProperties.All(property => shader.FindPropertyIndex(property) >= 0);
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
            default:
                return false;
        }
    }

    public override string GetRequiredFeatureToggle(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.OutlineTint:
            case DiNeLightingControl.OutlineWidth:
                return "_UseOutline";
            case DiNeLightingControl.Emission:
                return "_UseEmission";
            case DiNeLightingControl.ShadowStrength:
            case DiNeLightingControl.ShadowBorder:
                return "_UseShadow";
            case DiNeLightingControl.Reflectance:
                return "_UseReflection";
            default:
                return null;
        }
    }

    public override void WriteDefault(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
                sink.SetFloatConstant(LightMinLimit, context.DefaultMinLight);
                sink.SetFloatConstant(LightMaxLimit, context.DefaultMaxLight);
                break;
            case DiNeLightingControl.Unlit:
                sink.SetFloatConstant(AsUnlit, 0f);
                break;
            case DiNeLightingControl.Saturation:
                sink.SetFloatConstant(MainTexHSVG + ".y", NeutralHSVG.y);
                break;
            case DiNeLightingControl.Hue:
                sink.SetFloatConstant(MainTexHSVG + ".x", NeutralHSVG.x);
                break;
            case DiNeLightingControl.Brightness:
                sink.SetFloatConstant(MainTexHSVG + ".z", NeutralHSVG.z);
                break;
            case DiNeLightingControl.Gamma:
                sink.SetFloatConstant(MainTexHSVG + ".w", NeutralHSVG.w);
                break;
            case DiNeLightingControl.ColorTemperature:
                sink.SetColorConstant(ColorMain, Color.white);
                sink.SetColorConstant(Color2nd, Color.white);
                sink.SetColorConstant(Color3rd, Color.white);
                break;
            case DiNeLightingControl.Monochrome:
                sink.SetFloatConstant(MonochromeLighting, context.DefaultMonochrome);
                break;
            case DiNeLightingControl.Emission:
                sink.SetFloatConstant(EmissionBlend, 1f);
                sink.SetFloatConstant(Emission2ndBlend, 1f);
                break;
            case DiNeLightingControl.ShadowStrength:
                sink.SetFloatConstant(ShadowStrength, ReadFloat(context.Materials, ShadowStrength, 0f));
                break;
            case DiNeLightingControl.ShadowBorder:
                sink.SetFloatConstant(ShadowBorder, ReadFloat(context.Materials, ShadowBorder, 0.5f));
                break;
            case DiNeLightingControl.OutlineTint:
                sink.SetColorConstant(OutlineColor, ReadColor(context.Materials, OutlineColor, Color.black));
                break;
            case DiNeLightingControl.OutlineWidth:
                sink.SetFloatConstant(OutlineWidth, ReadFloat(context.Materials, OutlineWidth, 0.08f));
                break;
            case DiNeLightingControl.Reflectance:
                sink.SetFloatConstant(Reflectance, ReadFloat(context.Materials, Reflectance, 0.04f));
                break;
            case DiNeLightingControl.LightDirection:
                sink.SetVectorConstant(LightDirectionOverride, NeutralLightDirection);
                break;
        }
    }

    public override void WriteControl(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
                sink.SetFloatRange(LightMinLimit, context.MinLight, context.MaxLight);
                sink.SetFloatRange(LightMaxLimit, context.MinLight, context.MaxLight);
                break;
            case DiNeLightingControl.Unlit:
                sink.SetFloatRange(AsUnlit, 0f, 1f);
                break;
            case DiNeLightingControl.Saturation:
                // 0.5가 원본(1.0)이 되도록 0~2 범위로 편다.
                sink.SetFloatRange(MainTexHSVG + ".y", 0f, 2f);
                break;
            case DiNeLightingControl.Hue:
                sink.SetFloatRange(MainTexHSVG + ".x", 0f, 1f);
                break;
            case DiNeLightingControl.Brightness:
                sink.SetFloatRange(MainTexHSVG + ".z", 0f, 2f);
                break;
            case DiNeLightingControl.Gamma:
                sink.SetFloatRange(MainTexHSVG + ".w", 0f, 2f);
                break;
            case DiNeLightingControl.ColorTemperature:
                WriteColorTemperature(sink, ColorMain);
                WriteColorTemperature(sink, Color2nd);
                WriteColorTemperature(sink, Color3rd);
                break;
            case DiNeLightingControl.Monochrome:
                sink.SetFloatRange(MonochromeLighting, 0f, 1f);
                break;
            case DiNeLightingControl.Emission:
                sink.SetFloatRange(EmissionBlend, 0f, 1f);
                sink.SetFloatRange(Emission2ndBlend, 0f, 1f);
                break;
            case DiNeLightingControl.ShadowStrength:
                sink.SetFloatRange(ShadowStrength, 0f, 1f);
                break;
            case DiNeLightingControl.ShadowBorder:
                sink.SetFloatRange(ShadowBorder, 0f, 1f);
                break;
            case DiNeLightingControl.OutlineTint:
                sink.SetColorRange(OutlineColor, context.OutlineTintFrom, context.OutlineTintTo);
                break;
            case DiNeLightingControl.OutlineWidth:
                sink.SetFloatRange(OutlineWidth, 0f, context.OutlineWidthMax);
                break;
            case DiNeLightingControl.Reflectance:
                sink.SetFloatRange(Reflectance, 0f, context.ReflectanceMax);
                break;
            case DiNeLightingControl.LightDirection:
                // 토글: OFF는 오버라이드 없음, ON은 지정 방향.
                sink.SetVector(0f, LightDirectionOverride, NeutralLightDirection);
                var direction = context.LightDirection.sqrMagnitude > 0.0001f
                    ? context.LightDirection.normalized
                    : Vector3.up;
                sink.SetVector(1f, LightDirectionOverride, new Vector4(direction.x, direction.y, direction.z, 0f));
                break;
        }
    }

    /// <summary>0이 차가운 색, 0.5가 원본, 1이 따뜻한 색. (LLC와 동일한 곡선)</summary>
    private static void WriteColorTemperature(DiNeLightingSink sink, string property)
    {
        sink.SetColor(0f, property, new Color(0.6f, 0.95f, 1f, 1f));
        sink.SetColor(0.5f, property, Color.white);
        sink.SetColor(1f, property, new Color(1f, 0.8f, 0.6f, 1f));
    }

    private const string MainTex = "_MainTex";
    private const string Main2ndTex = "_Main2ndTex";
    private const string Main3rdTex = "_Main3rdTex";
    private const string MainGradationTex = "_MainGradationTex";
    private const string MainGradationStrength = "_MainGradationStrength";
    private const string MainColorAdjustMask = "_MainColorAdjustMask";

    private static Shader _bakerShader;

    /// <summary>lilToon이 직접 제공하는 베이커 셰이더. 색 보정 수식이 lilToon 본체와 동일하다.</summary>
    private static Shader BakerShader
    {
        get
        {
            if (_bakerShader == null)
                _bakerShader = Shader.Find("Hidden/ltsother_baker");
            return _bakerShader;
        }
    }

    public override bool NormalizeMaterial(
        Material material,
        ISet<DiNeLightingControl> controls,
        DiNeLightingBakeSession session)
    {
        if (material == null || !NeedsNormalization(controls))
            return false;

        if (BakerShader == null)
        {
            Debug.LogWarning(
                "[DiNe 라이팅 디자이너] lilToon의 베이커 셰이더(Hidden/ltsother_baker)를 찾지 못해 머티리얼 정규화를 건너뜁니다. " +
                "채도/색온도 결과가 원본과 달라질 수 있습니다.");
            return false;
        }

        bool bakeColorAdjust = controls.Contains(DiNeLightingControl.Saturation)
            || controls.Contains(DiNeLightingControl.Hue)
            || controls.Contains(DiNeLightingControl.Brightness)
            || controls.Contains(DiNeLightingControl.Gamma);
        bool bakeColor = controls.Contains(DiNeLightingControl.ColorTemperature);

        bool modified = NormalizeMainTex(material, session, bakeColorAdjust, bakeColor);

        if (bakeColor)
        {
            modified |= NormalizeSubTex(material, session, Main2ndTex, Color2nd);
            modified |= NormalizeSubTex(material, session, Main3rdTex, Color3rd);
        }

        return modified;
    }

    private static bool NormalizeMainTex(
        Material material,
        DiNeLightingBakeSession session,
        bool bakeColorAdjust,
        bool bakeColor)
    {
        if (!material.HasProperty(MainTex))
            return false;

        var source = material.GetTexture(MainTex);
        if (source is RenderTexture)
            return false;   // 런타임 생성 텍스처는 구울 수 없다.

        var bakeColorValue = Color.white;
        var bakeHsvg = NeutralHSVG;
        Texture gradationTexture = null;
        float gradationStrength = 0f;
        bool needed = false;
        bool colorAdjusted = false;

        if (bakeColor && material.HasProperty(ColorMain))
        {
            var color = material.GetColor(ColorMain);
            if (color != Color.white)
            {
                bakeColorValue = color;
                needed = true;
            }
        }

        if (bakeColorAdjust)
        {
            if (material.HasProperty(MainTexHSVG))
            {
                var hsvg = material.GetVector(MainTexHSVG);
                if (hsvg != NeutralHSVG)
                {
                    bakeHsvg = hsvg;
                    needed = true;
                    colorAdjusted = true;
                }
            }

            if (material.HasProperty(MainGradationTex) && material.HasProperty(MainGradationStrength))
            {
                var gradation = material.GetTexture(MainGradationTex);
                float strength = material.GetFloat(MainGradationStrength);
                if (gradation != null && !Mathf.Approximately(strength, 0f))
                {
                    gradationTexture = gradation;
                    gradationStrength = strength;
                    needed = true;
                    colorAdjusted = true;
                }
            }
        }

        if (!needed)
            return false;

        Texture mask = null;
        if (colorAdjusted && material.HasProperty(MainColorAdjustMask))
            mask = material.GetTexture(MainColorAdjustMask);

        var baker = new Material(BakerShader);
        baker.SetTexture(MainTex, source != null ? source : Texture2D.whiteTexture);
        baker.SetColor(ColorMain, bakeColorValue);
        baker.SetVector(MainTexHSVG, bakeHsvg);
        baker.SetTexture(MainGradationTex, gradationTexture != null ? gradationTexture : Texture2D.whiteTexture);
        baker.SetFloat(MainGradationStrength, gradationStrength);
        baker.SetTexture(MainColorAdjustMask, mask != null ? mask : Texture2D.whiteTexture);

        int hash = HashOf(source, bakeColorValue, bakeHsvg, gradationTexture, gradationStrength, mask);
        var baked = session.Bake(hash, source, baker, bakeColorValue.a < 1f);
        UnityEngine.Object.DestroyImmediate(baker);

        material.SetTexture(MainTex, baked);
        if (material.HasProperty(ColorMain)) material.SetColor(ColorMain, Color.white);
        if (material.HasProperty(MainTexHSVG)) material.SetVector(MainTexHSVG, NeutralHSVG);
        if (gradationTexture != null)
        {
            material.SetTexture(MainGradationTex, null);
            material.SetFloat(MainGradationStrength, 0f);
        }
        if (mask != null) material.SetTexture(MainColorAdjustMask, null);

        return true;
    }

    private static bool NormalizeSubTex(
        Material material,
        DiNeLightingBakeSession session,
        string textureProperty,
        string colorProperty)
    {
        if (!material.HasProperty(textureProperty) || !material.HasProperty(colorProperty))
            return false;

        var color = material.GetColor(colorProperty);
        if (color == Color.white)
            return false;

        var source = material.GetTexture(textureProperty);
        if (source is RenderTexture)
            return false;

        var baker = new Material(BakerShader);
        baker.SetTexture(MainTex, source != null ? source : Texture2D.whiteTexture);
        baker.SetColor(ColorMain, color);
        baker.SetVector(MainTexHSVG, NeutralHSVG);
        baker.SetTexture(MainGradationTex, Texture2D.whiteTexture);
        baker.SetFloat(MainGradationStrength, 0f);
        baker.SetTexture(MainColorAdjustMask, Texture2D.whiteTexture);

        int hash = HashOf(source, color, NeutralHSVG, null, 0f, null);
        var baked = session.Bake(hash, source, baker, color.a < 1f);
        UnityEngine.Object.DestroyImmediate(baker);

        material.SetTexture(textureProperty, baked);
        material.SetColor(colorProperty, Color.white);
        return true;
    }

    private static int HashOf(Texture source, Color color, Vector4 hsvg, Texture gradation, float strength, Texture mask)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (source != null ? source.GetInstanceID() : 0);
            hash = hash * 31 + color.GetHashCode();
            hash = hash * 31 + hsvg.GetHashCode();
            hash = hash * 31 + (gradation != null ? gradation.GetInstanceID() : 0);
            hash = hash * 31 + strength.GetHashCode();
            hash = hash * 31 + (mask != null ? mask.GetInstanceID() : 0);
            return hash;
        }
    }

    public override bool TryGetLightRange(Material[] materials, out float min, out float max)
    {
        min = ReadFloat(materials, LightMinLimit, 0.05f);
        max = ReadFloat(materials, LightMaxLimit, 1f);
        return true;
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
