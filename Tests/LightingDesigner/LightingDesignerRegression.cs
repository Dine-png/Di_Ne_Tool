// Compiled only in the isolated project created by Run-LightingDesignerRegression.ps1.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LightingDesignerRegression
{
    private const int Size = 32;
    private static readonly Vector4 Neutral = new Vector4(0f, 1f, 1f, 1f);
    private static readonly List<string> Results = new List<string>();
    private static Shader _oracle;
    private static int _failed;

    public static void Run()
    {
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            _oracle = Shader.Find("Hidden/ltsother_baker");
            Require(_oracle != null && _oracle.isSupported, "The real lilToon baker shader is unavailable.");
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                "These pixel tests need a graphics device; do not use -nographics.");
            Debug.Log("Lighting regression device: " + SystemInfo.graphicsDeviceName + "; color space: " + QualitySettings.activeColorSpace);

            Test("Color temperature preserves existing gamma and tint", () =>
                MainCase(new[] { DiNeLightingControl.ColorTemperature }, new Color(.435f, .435f, .435f, 1f), new Vector4(0, 1, 1, .74f), false, .01f));
            Test("HSVG-only preserves unconsumed tint and alpha", () =>
                MainCase(new[] { DiNeLightingControl.Gamma }, new Color(.45f, .7f, .6f, .37f), new Vector4(.08f, .8f, .9f, .74f)));
            Test("Texture alpha survives an opaque tint", () =>
                MainCase(new[] { DiNeLightingControl.ColorTemperature }, new Color(.8f, .65f, .5f, 1f), Neutral));
            Test("An alpha-only tint is preserved", () =>
                MainCase(new[] { DiNeLightingControl.ColorTemperature }, new Color(1f, 1f, 1f, .3f), Neutral));
            Test("Combined tone and temperature preserve RGBA", () =>
                MainCase(new[] { DiNeLightingControl.ColorTemperature, DiNeLightingControl.Gamma }, new Color(.45f, .7f, .6f, .37f), new Vector4(.08f, .8f, .9f, .74f)));
            Test("Masked tone and gradation precede baked tint", MaskedTone);
            Test("Second and third layers retain effective alpha", SubLayers);
            Test("Color temperature curves explicitly set neutral alpha", TemperatureCurves);
            Test("Color temperature animation preserves distinct material alphas", TemperatureAnimationAlphas);
            Test("Baking preserves texture sampling settings", Sampling);
            Test("Baking preserves textures without mipmaps", NoMipmaps);
            Test("Alpha mask modes and parameters remain unchanged", AlphaMasks);
            Test("Opaque RGB tint leaves material alpha at every mask mode", AlphaAtEveryMaskMode);
            Test("RenderTexture normalization does not mutate the source", RenderTextureSource);
            Test("Null texture uses the white fallback", NullTexture);
        }
        catch (Exception e)
        {
            _failed++;
            Results.Add("FATAL " + e);
            Debug.LogException(e);
        }
        finally
        {
            var report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LightingDesignerRegression-results.txt");
            Results.Add("Failures: " + _failed);
            File.WriteAllLines(report, Results);
            Debug.Log("Lighting regression report: " + report + "; failures: " + _failed);
            EditorApplication.Exit(_failed == 0 ? 0 : 1);
        }
    }

    private static void Test(string name, Action action)
    {
        try
        {
            action();
            Results.Add("PASS " + name);
            Debug.Log("PASS " + name);
        }
        catch (Exception e)
        {
            _failed++;
            Results.Add("FAIL " + name + ": " + e.Message);
            Debug.LogError("FAIL " + name + ": " + e);
        }
    }

    private static Material Material(Texture source, Color tint, Vector4 tone)
    {
        var material = new Material(_oracle);
        material.SetTexture("_MainTex", source);
        material.SetColor("_Color", tint);
        material.SetVector("_MainTexHSVG", tone);
        material.SetTexture("_MainGradationTex", Texture2D.whiteTexture);
        material.SetTexture("_MainColorAdjustMask", Texture2D.whiteTexture);
        material.SetFloat("_MainGradationStrength", 0f);
        return material;
    }

    private static Texture2D Texture(bool varyingAlpha = true)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false);
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float v = (x / 8) / 3f;
            pixels[y * Size + x] = new Color(.24f + .5f * v, .63f - .24f * v, .38f + .26f * v,
                varyingAlpha ? v : 1f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static void Normalize(Material material, params DiNeLightingControl[] controls)
    {
        Require(DiNeShaderProfileLilToon.Instance.NormalizeMaterial(material,
            new HashSet<DiNeLightingControl>(controls), new DiNeLightingBakeSession()), "Expected normalization to occur.");
    }

    // The unmodified lilToon baker is the oracle. Float readback avoids introducing
    // the production bake's encoding/compression into the reference calculation.
    private static Color[] Render(Material material)
    {
        var previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true);
        try
        {
            GL.sRGBWrite = false;
            Graphics.Blit(material.GetTexture("_MainTex") ?? Texture2D.whiteTexture, rt, material);
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            readback.Apply();
            return readback.GetPixels();
        }
        finally
        {
            RenderTexture.active = previous;
            GL.sRGBWrite = previousSrgb;
            Object.DestroyImmediate(readback);
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static void Compare(Color[] before, Color[] after, string context, float rgbTolerance = .045f)
    {
        float rgb = 0f, alpha = 0f;
        for (int i = 0; i < before.Length; i++)
        {
            rgb = Mathf.Max(rgb, Mathf.Abs(before[i].r - after[i].r), Mathf.Abs(before[i].g - after[i].g), Mathf.Abs(before[i].b - after[i].b));
            alpha = Mathf.Max(alpha, Mathf.Abs(before[i].a - after[i].a));
        }
        // DXT color quantization is expected; lost alpha or the old gamma/tint
        // reset are substantially larger than these limits on these fixtures.
        Debug.Log(context + ": max RGB error=" + rgb + ", max alpha error=" + alpha);
        Require(rgb <= rgbTolerance && alpha <= .025f,
            context + ": max RGB error=" + rgb + ", max alpha error=" + alpha);
    }

    private static void MainCase(DiNeLightingControl[] controls, Color tint, Vector4 tone, bool varyingAlpha = true, float rgbTolerance = .045f)
    {
        var material = Material(Texture(varyingAlpha), tint, tone);
        var before = Render(material);
        Normalize(material, controls);
        Compare(before, Render(material), "Main texture", rgbTolerance);
        if (!controls.Contains(DiNeLightingControl.ColorTemperature))
            Require(material.GetColor("_Color") == tint, "HSVG-only normalization changed the unconsumed tint.");
    }

    private static void MaskedTone()
    {
        var material = Material(Texture(), new Color(.43f, .61f, .8f, .6f), new Vector4(.13f, .75f, 1.12f, .74f));
        var mask = Texture(false);
        var gradation = Texture(false);
        material.SetTexture("_MainColorAdjustMask", mask);
        material.SetTexture("_MainGradationTex", gradation);
        material.SetFloat("_MainGradationStrength", .4f);
        var before = Render(material);
        Normalize(material, DiNeLightingControl.ColorTemperature);
        Compare(before, Render(material), "Masked tone and gradation");
    }

    private static void SubLayers()
    {
        var material = Material(Texture(), Color.white, Neutral);
        var tints = new[] { new Color(.55f, .7f, .8f, .31f), new Color(.7f, .45f, .6f, .63f) };
        var before = new Color[2][];
        for (int i = 0; i < 2; i++)
        {
            string suffix = i == 0 ? "2nd" : "3rd";
            var source = Texture();
            material.SetTexture("_Main" + suffix + "Tex", source);
            material.SetColor("_Color" + suffix, tints[i]);
            material.SetFloat("_UseMain" + suffix + "Tex", 1f);
            before[i] = Render(Material(source, tints[i], Neutral));
        }
        Normalize(material, DiNeLightingControl.ColorTemperature);
        for (int i = 0; i < 2; i++)
        {
            string suffix = i == 0 ? "2nd" : "3rd";
            var tint = material.GetColor("_Color" + suffix);
            Compare(before[i], Render(Material(material.GetTexture("_Main" + suffix + "Tex"), tint, Neutral)), suffix);
        }
    }

    private static void TemperatureCurves()
    {
        var context = new DiNeLightingContext { Materials = new[] { Material(Texture(), new Color(1, 1, 1, .31f), Neutral) } };
        foreach (bool defaults in new[] { true, false })
        {
            var sink = new DiNeLightingSink("Regression", null) { CurrentPath = "Body", CurrentType = typeof(SkinnedMeshRenderer) };
            if (defaults) DiNeShaderProfileLilToon.Instance.WriteDefault(sink, DiNeLightingControl.ColorTemperature, in context);
            else DiNeShaderProfileLilToon.Instance.WriteControl(sink, DiNeLightingControl.ColorTemperature, in context);
            var bindings = sink.Clips.SelectMany(pair => AnimationUtility.GetCurveBindings(pair.Value)).ToArray();
            Require(bindings.Length > 0, "No color curves generated.");
            foreach (string property in new[] { "_Color", "_Color2nd", "_Color3rd" })
            foreach (string channel in new[] { "r", "g", "b", "a" })
                Require(bindings.Any(binding => binding.propertyName == "material." + property + "." + channel), "Missing RGB binding.");
            foreach (var pair in sink.Clips)
            foreach (var binding in AnimationUtility.GetCurveBindings(pair.Value).Where(binding => binding.propertyName.EndsWith(".a", StringComparison.Ordinal)))
                Require(Mathf.Approximately(AnimationUtility.GetEditorCurve(pair.Value, binding).Evaluate(0f), 1f), "Temperature alpha multiplier must be one.");
        }
    }

    private static void Sampling()
    {
        var source = Texture();
        source.wrapModeU = TextureWrapMode.Mirror;
        source.wrapModeV = TextureWrapMode.Clamp;
        source.wrapModeW = TextureWrapMode.Repeat;
        source.filterMode = FilterMode.Trilinear;
        source.anisoLevel = 4;
        source.mipMapBias = .25f;
        var material = Material(source, new Color(.8f, .7f, .6f, 1), Neutral);
        Normalize(material, DiNeLightingControl.ColorTemperature);
        var baked = material.GetTexture("_MainTex");
        Require(baked.wrapModeU == source.wrapModeU && baked.wrapModeV == source.wrapModeV && baked.wrapModeW == source.wrapModeW,
            "Texture wrap modes changed.");
        Require(baked.filterMode == source.filterMode && baked.anisoLevel == source.anisoLevel && Mathf.Approximately(baked.mipMapBias, source.mipMapBias),
            "Texture filtering settings changed.");
    }

    private static void NoMipmaps()
    {
        var source = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        source.SetPixels(Enumerable.Repeat(new Color(.5f, .6f, .7f, .4f), Size * Size).ToArray());
        source.Apply(false);
        var material = Material(source, new Color(.8f, .7f, .6f, 1), Neutral);
        Normalize(material, DiNeLightingControl.ColorTemperature);
        Require(((Texture2D)material.GetTexture("_MainTex")).mipmapCount == 1, "Baking introduced mipmaps that the source did not have.");
    }

    private static void TemperatureAnimationAlphas()
    {
        var root = new GameObject("RegressionRoot");
        var body = new GameObject("Body");
        body.transform.SetParent(root.transform);
        var renderer = body.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { Material(Texture(), new Color(1, 1, 1, .2f), Neutral), Material(Texture(), new Color(1, 1, 1, .7f), Neutral) };
        try
        {
            var before = renderer.sharedMaterials.Select(Render).ToArray();
            foreach (var material in renderer.sharedMaterials)
                Normalize(material, DiNeLightingControl.ColorTemperature);
            var context = new DiNeLightingContext { Materials = renderer.sharedMaterials };
            var sink = new DiNeLightingSink("AlphaAnimation", null) { CurrentPath = "Body", CurrentType = typeof(MeshRenderer) };
            DiNeShaderProfileLilToon.Instance.WriteControl(sink, DiNeLightingControl.ColorTemperature, in context);
            foreach (var pair in sink.Clips)
            {
                pair.Value.SampleAnimation(root, 0f);
                var shared = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(shared);
                for (int i = 0; i < 2; i++)
                {
                    var indexed = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(indexed, i);
                    int colorId = Shader.PropertyToID("_Color");
                    Color actual = indexed.HasColor(colorId) ? indexed.GetColor(colorId) :
                        shared.HasColor(colorId) ? shared.GetColor(colorId) : renderer.sharedMaterials[i].GetColor(colorId);
                    var effectiveMaterial = new Material(renderer.sharedMaterials[i]);
                    effectiveMaterial.SetColor(colorId, actual);
                    var after = Render(effectiveMaterial);
                    for (int mode = 0; mode <= 4; mode++)
                    for (int pixel = 0; pixel < before[i].Length; pixel++)
                        Require(Mathf.Abs(MaskAlpha(before[i][pixel].a, mode) - MaskAlpha(after[pixel].a, mode)) <= .025f,
                            "Animation point " + pair.Key + " changed slot " + i + " effective alpha with mask mode " + mode);
                    Object.DestroyImmediate(effectiveMaterial);
                }
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void AlphaMasks()
    {
        for (int mode = 0; mode <= 4; mode++)
        {
            var material = Material(Texture(), new Color(.8f, .7f, .6f, .4f), new Vector4(0, 1, 1, .74f));
            var mask = Texture(false);
            material.SetTexture("_AlphaMask", mask);
            material.SetFloat("_AlphaMaskMode", mode);
            material.SetFloat("_AlphaMaskScale", .7f);
            material.SetFloat("_AlphaMaskValue", .13f);
            material.SetTextureScale("_AlphaMask", new Vector2(2f, 3f));
            material.SetTextureOffset("_AlphaMask", new Vector2(.12f, .23f));
            Normalize(material, DiNeLightingControl.ColorTemperature, DiNeLightingControl.Gamma);
            Require(material.GetTexture("_AlphaMask") == mask && material.GetFloat("_AlphaMaskMode") == mode &&
                Mathf.Approximately(material.GetFloat("_AlphaMaskScale"), .7f) && Mathf.Approximately(material.GetFloat("_AlphaMaskValue"), .13f) &&
                material.GetTextureScale("_AlphaMask") == new Vector2(2f, 3f) && material.GetTextureOffset("_AlphaMask") == new Vector2(.12f, .23f), "Alpha mask properties changed for mode " + mode);
        }
    }

    private static void AlphaAtEveryMaskMode()
    {
        // Runtime alpha-mask operations happen after texture alpha * material alpha.
        for (int mode = 0; mode <= 4; mode++)
        {
            var material = Material(Texture(), new Color(.8f, .7f, .6f, .4f), Neutral);
            var before = Render(material);
            material.SetFloat("_AlphaMaskMode", mode);
            Normalize(material, DiNeLightingControl.ColorTemperature);
            var after = Render(material);
            for (int i = 0; i < before.Length; i++)
                Require(Mathf.Abs(MaskAlpha(before[i].a, mode) - MaskAlpha(after[i].a, mode)) <= .025f,
                    "Post-mask alpha changed for mode " + mode);
        }
    }

    private static float MaskAlpha(float alpha, int mode)
    {
        const float mask = .28f;
        switch (mode)
        {
            case 1: return mask;
            case 2: return alpha * mask;
            case 3: return Mathf.Clamp01(alpha + mask);
            case 4: return Mathf.Clamp01(alpha - mask);
            default: return alpha;
        }
    }

    private static void RenderTextureSource()
    {
        var source = new RenderTexture(Size, Size, 0);
        var tint = new Color(.45f, .65f, .75f, .33f);
        var material = Material(source, tint, new Vector4(0, 1, 1, .74f));
        try
        {
            DiNeShaderProfileLilToon.Instance.NormalizeMaterial(material,
                new HashSet<DiNeLightingControl> { DiNeLightingControl.ColorTemperature }, new DiNeLightingBakeSession());
        }
        catch (InvalidOperationException)
        {
            // A deliberate failure is safe if no non-neutral properties were lost.
        }
        Require(material.GetTexture("_MainTex") == source && material.GetColor("_Color") == tint && material.GetVector("_MainTexHSVG").w == .74f,
            "A live RenderTexture or its unbaked properties were changed.");
        Object.DestroyImmediate(source);
    }

    private static void NullTexture()
    {
        var material = Material(null, new Color(.3f, .65f, .8f, .18f), Neutral);
        var before = Render(material);
        Normalize(material, DiNeLightingControl.ColorTemperature);
        Compare(before, Render(material), "Null texture fallback");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
