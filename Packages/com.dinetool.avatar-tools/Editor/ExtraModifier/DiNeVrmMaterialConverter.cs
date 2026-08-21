using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DiNeTool.ExtraModifier.Editor
{
    /// <summary>
    /// 어떤 부가 데이터를 MToon으로 살려서 옮길지 결정한다.
    /// 전부 끄면 "부가 데이터를 지우고 베이스 컬러만 변환"이 된다.
    /// </summary>
    [Serializable]
    internal struct DiNeVrmMaterialOptions
    {
        public bool KeepNormalMap;
        public bool KeepMatcap;
        public bool KeepEmission;
        public bool KeepShadeColor;
        public bool KeepOutline;
        public bool KeepRim;
        public bool SaveAsAssets;

        public static DiNeVrmMaterialOptions Preserve => new DiNeVrmMaterialOptions
        {
            KeepNormalMap = true,
            KeepMatcap = true,
            KeepEmission = true,
            KeepShadeColor = true,
            KeepOutline = true,
            KeepRim = true,
            SaveAsAssets = true
        };

        public static DiNeVrmMaterialOptions Strip => new DiNeVrmMaterialOptions
        {
            KeepNormalMap = false,
            KeepMatcap = false,
            KeepEmission = false,
            KeepShadeColor = false,
            KeepOutline = false,
            KeepRim = false,
            SaveAsAssets = true
        };

        public bool KeepsAnything =>
            KeepNormalMap || KeepMatcap || KeepEmission || KeepShadeColor || KeepOutline || KeepRim;
    }

    /// <summary>
    /// lilToon / Poiyomi / Standard 계열 머티리얼을 VRM MToon으로 변환한다.
    /// 소스 셰이더 이름에 의존하지 않고 "존재하는 프로퍼티 이름"으로 슬롯을 찾기 때문에
    /// 파생 셰이더에도 대체로 동작한다.
    /// </summary>
    internal static class DiNeVrmMaterialConverter
    {
        private const string OutputRoot = "Assets/DiNe/VRM Materials";

        // 소스 셰이더에서 찾을 프로퍼티 후보들 (앞에 있을수록 우선).
        private static readonly string[] MainTexNames = { "_MainTex", "_BaseMap", "_BaseColorMap", "_MainTexture", "_Diffuse" };
        private static readonly string[] MainColorNames = { "_Color", "_BaseColor", "_MainColor", "_Tint", "_TintColor" };
        private static readonly string[] NormalTexNames = { "_BumpMap", "_NormalMap", "_NormalMapTex", "_NormalTex", "_MainNormalMap" };
        private static readonly string[] NormalScaleNames = { "_BumpScale", "_NormalMapScale", "_NormalScale", "_NormalStrength", "_BumpMapScale" };
        private static readonly string[] ShadeTexNames = { "_ShadowColorTex", "_ShadeTexture", "_1st_ShadeMap", "_ShadowTex", "_ShadingGradeMap" };
        private static readonly string[] ShadeColorNames = { "_ShadowColor", "_ShadeColor", "_1st_ShadeColor", "_ShadowColorA" };
        private static readonly string[] MatcapTexNames = { "_MatCapTex", "_MatcapTex", "_MatCap", "_Matcap", "_MatCap_Sampler", "_SphereAdd", "_SphereTex", "_MatCapTexture" };
        private static readonly string[] MatcapColorNames = { "_MatCapColor", "_MatcapColor", "_MatCap_Color", "_MatcapTint" };
        private static readonly string[] MatcapBlendNames = { "_MatCapBlendMode", "_MatcapBlendType", "_MatCapBlend", "_MatcapBlendMode" };
        private static readonly string[] MatcapPowerNames = { "_MatCapBlend", "_MatcapIntensity", "_MatCapPower", "_MatcapBlendStrength" };
        private static readonly string[] EmissionTexNames = { "_EmissionMap", "_EmissionMap0", "_EmissiveTex", "_Emissive_Tex", "_EmissionTex" };
        private static readonly string[] EmissionColorNames = { "_EmissionColor", "_EmissionColor0", "_Emissive_Color", "_EmissiveColor" };
        private static readonly string[] EmissionToggleNames = { "_UseEmission", "_EmissionEnabled", "_EnableEmission" };
        private static readonly string[] OutlineWidthNames = { "_OutlineWidth", "_outline_width", "_LineWidth", "_Outline_Width" };
        private static readonly string[] OutlineColorNames = { "_OutlineColor", "_LineColor", "_Outline_Color", "_OutlineCol" };
        private static readonly string[] OutlineTexNames = { "_OutlineWidthMask", "_OutlineWidthTexture", "_Outline_Sampler", "_OutlineMask" };
        private static readonly string[] OutlineToggleNames = { "_UseOutline", "_OutlineEnabled", "_EnableOutline" };
        private static readonly string[] RimColorNames = { "_RimColor", "_RimLightColor", "_RimLight_Color" };
        private static readonly string[] RimTexNames = { "_RimColorTex", "_RimTexture", "_Set_RimLightMask" };
        private static readonly string[] RimPowerNames = { "_RimFresnelPower", "_RimBorder", "_RimLightPower", "_RimPower" };
        private static readonly string[] CutoffNames = { "_Cutoff", "_AlphaCutoff", "_Clip", "_AlphaClip" };
        private static readonly string[] CullNames = { "_Cull", "_CullMode" };

        public static bool IsMToonAvailable => FindMToonShader() != null;

        public static Shader FindMToonShader()
        {
            return Shader.Find("VRM/MToon") ?? Shader.Find("VRM10/MToon10");
        }

        public static DiNeVrmReport ConvertToMToon(GameObject avatarRoot, DiNeVrmMaterialOptions options)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            var shader = FindMToonShader();
            if (shader == null)
            {
                report.Error = "MToon shader was not found. Install UniVRM before converting materials.";
                return report;
            }

            var isMToon10 = shader.name.Contains("MToon10");
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return report;

            var folder = options.SaveAsAssets ? PrepareFolder(avatarRoot.name) : null;
            var cache = new Dictionary<Material, Material>();

            foreach (var renderer in renderers)
            {
                var sources = renderer.sharedMaterials;
                if (sources == null || sources.Length == 0)
                    continue;

                var changed = false;
                var results = new Material[sources.Length];
                for (var i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (source == null)
                    {
                        results[i] = null;
                        continue;
                    }
                    if (source.shader == shader)
                    {
                        results[i] = source;
                        continue;
                    }

                    if (!cache.TryGetValue(source, out var converted))
                    {
                        converted = Convert(source, shader, isMToon10, options, report);
                        if (converted != null && folder != null)
                            converted = SaveAsset(converted, folder, source.name);
                        cache.Add(source, converted);
                        if (converted != null)
                            report.ConvertedMaterials++;
                    }

                    results[i] = converted ?? source;
                    changed |= converted != null;
                }

                if (!changed)
                    continue;

                Undo.RecordObject(renderer, "Convert materials for VRM");
                renderer.sharedMaterials = results;
                EditorUtility.SetDirty(renderer);
            }

            if (folder != null)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.SetDirty(avatarRoot);
            Debug.Log($"[DiNe VRM] Converted {report.ConvertedMaterials} materials to MToon (dropped extras: {report.DroppedMaterialFeatures}).", avatarRoot);
            return report;
        }

        private static Material Convert(Material source, Shader shader, bool isMToon10, DiNeVrmMaterialOptions options, DiNeVrmReport report)
        {
            var target = new Material(shader) { name = source.name + "_MToon" };

            var mainTex = GetTexture(source, MainTexNames);
            var mainColor = GetColor(source, MainColorNames, Color.white);
            SetTexture(target, isMToon10 ? "_BaseMap" : "_MainTex", mainTex);
            SetTextureTransform(target, isMToon10 ? "_BaseMap" : "_MainTex", source, MainTexNames);
            SetColor(target, isMToon10 ? "_BaseColor" : "_Color", mainColor);

            // 그림자(2도 음영) 색. 없으면 베이스 컬러를 조금 어둡게 깔아 준다.
            var shadeColor = mainColor * 0.75f;
            shadeColor.a = mainColor.a;
            var shadeTex = mainTex;
            if (options.KeepShadeColor)
            {
                var sourceShadeTex = GetTexture(source, ShadeTexNames);
                if (sourceShadeTex != null)
                    shadeTex = sourceShadeTex;
                if (TryGetColor(source, ShadeColorNames, out var sourceShadeColor))
                {
                    // lilToon의 _ShadowColor는 곱해지는 색이라 베이스와 합성해야 MToon의 절대 색이 된다.
                    shadeColor = sourceShadeTex != null ? sourceShadeColor : mainColor * sourceShadeColor;
                    shadeColor.a = mainColor.a;
                }
            }
            else if (HasAny(source, ShadeTexNames) || HasAny(source, ShadeColorNames))
            {
                report.DroppedMaterialFeatures++;
            }

            SetTexture(target, isMToon10 ? "_ShadeTex" : "_ShadeTexture", shadeTex);
            SetColor(target, "_ShadeColor", shadeColor);
            if (!isMToon10)
            {
                SetFloat(target, "_ShadeToony", 0.9f);
                SetFloat(target, "_ShadeShift", 0f);
                SetFloat(target, "_ReceiveShadowRate", 1f);
                SetFloat(target, "_ShadingGradeRate", 1f);
                SetFloat(target, "_IndirectLightIntensity", 0.1f);
            }
            else
            {
                SetFloat(target, "_ShadingToonyFactor", 0.9f);
                SetFloat(target, "_ShadingShiftFactor", 0f);
            }

            // 노말맵.
            var normalTex = GetTexture(source, NormalTexNames);
            if (normalTex != null)
            {
                if (options.KeepNormalMap)
                {
                    SetTexture(target, "_BumpMap", normalTex);
                    SetFloat(target, "_BumpScale", GetFloat(source, NormalScaleNames, 1f));
                    target.EnableKeyword("_NORMALMAP");
                    if (isMToon10)
                        target.EnableKeyword("_MTOON_NORMALMAP");
                    report.KeptNormalMaps++;
                }
                else
                {
                    report.DroppedMaterialFeatures++;
                }
            }

            // 맷캡. MToon은 가산(Additive) 스피어맵만 지원한다.
            var matcapTex = GetTexture(source, MatcapTexNames);
            if (matcapTex != null)
            {
                if (options.KeepMatcap)
                {
                    if (IsMultiplyMatcap(source))
                    {
                        // 곱 연산 맷캡은 MToon에 대응 슬롯이 없어 그대로 넣으면 색이 망가진다.
                        report.DroppedMaterialFeatures++;
                        report.Warning = "Multiply matcap is not supported by MToon and was skipped.";
                    }
                    else if (isMToon10)
                    {
                        SetTexture(target, "_MatcapTex", matcapTex);
                        var tint = GetColor(source, MatcapColorNames, Color.white);
                        var power = GetFloat(source, MatcapPowerNames, 1f);
                        SetColor(target, "_MatcapColor", tint * Mathf.Clamp01(power));
                        report.KeptMatcaps++;
                    }
                    else
                    {
                        SetTexture(target, "_SphereAdd", matcapTex);
                        report.KeptMatcaps++;
                    }
                }
                else
                {
                    report.DroppedMaterialFeatures++;
                }
            }

            // 이미시브.
            var emissionTex = GetTexture(source, EmissionTexNames);
            var hasEmissionColor = TryGetColor(source, EmissionColorNames, out var emissionColor);
            var emissionEnabled = GetFloat(source, EmissionToggleNames, emissionTex != null || hasEmissionColor ? 1f : 0f) > 0.5f;
            if (emissionEnabled && (emissionTex != null || hasEmissionColor))
            {
                if (options.KeepEmission)
                {
                    SetTexture(target, "_EmissionMap", emissionTex);
                    SetColor(target, "_EmissionColor", hasEmissionColor ? emissionColor : Color.white);
                    target.EnableKeyword("_EMISSION");
                    target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    report.KeptEmissions++;
                }
                else
                {
                    report.DroppedMaterialFeatures++;
                }
            }
            else
            {
                SetColor(target, "_EmissionColor", Color.black);
            }

            // 림라이트.
            if (TryGetColor(source, RimColorNames, out var rimColor) && rimColor.maxColorComponent > 0.001f)
            {
                if (options.KeepRim)
                {
                    SetColor(target, "_RimColor", rimColor);
                    SetTexture(target, isMToon10 ? "_RimMultiplyTex" : "_RimTexture", GetTexture(source, RimTexNames));
                    SetFloat(target, isMToon10 ? "_RimFresnelPowerFactor" : "_RimFresnelPower",
                        Mathf.Clamp(GetFloat(source, RimPowerNames, 3f), 0f, 100f));
                    report.KeptRims++;
                }
                else
                {
                    report.DroppedMaterialFeatures++;
                }
            }
            else
            {
                SetColor(target, "_RimColor", Color.black);
            }

            // 아웃라인.
            var outlineWidth = GetFloat(source, OutlineWidthNames, 0f);
            var outlineEnabled = outlineWidth > 0.0001f && GetFloat(source, OutlineToggleNames, 1f) > 0.5f;
            if (outlineEnabled)
            {
                if (options.KeepOutline)
                {
                    SetFloat(target, "_OutlineWidthMode", 1f); // World coordinates
                    SetFloat(target, isMToon10 ? "_OutlineWidthFactor" : "_OutlineWidth", Mathf.Clamp(outlineWidth, 0.01f, 1f));
                    SetColor(target, isMToon10 ? "_OutlineColorFactor" : "_OutlineColor",
                        GetColor(source, OutlineColorNames, Color.black));
                    SetTexture(target, isMToon10 ? "_OutlineWidthMultiplyTex" : "_OutlineWidthTexture",
                        GetTexture(source, OutlineTexNames));
                    if (!isMToon10)
                    {
                        SetFloat(target, "_OutlineColorMode", 0f);
                        SetFloat(target, "_OutlineLightingMix", 0f);
                        SetFloat(target, "_OutlineCullMode", 1f);
                    }
                    report.KeptOutlines++;
                }
                else
                {
                    SetFloat(target, "_OutlineWidthMode", 0f);
                    report.DroppedMaterialFeatures++;
                }
            }
            else
            {
                SetFloat(target, "_OutlineWidthMode", 0f);
            }

            ApplyRenderMode(source, target, isMToon10);
            SetFloat(target, isMToon10 ? "_M_CullMode" : "_CullMode", GetFloat(source, CullNames, 2f));
            target.renderQueue = -1;
            ValidateMToon(target, isMToon10);
            target.renderQueue = source.renderQueue > 2500 ? source.renderQueue : target.shader.renderQueue;
            return target;
        }

        /// <summary>곱 연산 맷캡인지. MToon에는 대응 슬롯이 없어 그대로 옮기면 색이 망가진다.</summary>
        private static bool IsMultiplyMatcap(Material source)
        {
            var name = FindName(source, MatcapBlendNames);
            if (name == null)
                return false;

            var blend = source.GetFloat(name);
            switch (name)
            {
                case "_MatCapBlendMode": // lilToon: 0 Normal / 1 Add / 2 Screen / 3 Multiply
                    return Mathf.RoundToInt(blend) == 3;
                case "_MatcapBlendType": // Poiyomi: 0 Additive / 1 Multiply / 2 Replace
                    return Mathf.RoundToInt(blend) == 1;
                default:
                    return false;
            }
        }

        private static void ApplyRenderMode(Material source, Material target, bool isMToon10)
        {
            var renderType = source.GetTag("RenderType", false, string.Empty);
            var cutoff = GetFloat(source, CutoffNames, 0.5f);
            var transparent = renderType == "Transparent" || source.renderQueue >= 3000;
            var cutout = renderType == "TransparentCutout" ||
                         (!transparent && (source.IsKeywordEnabled("_ALPHATEST_ON") || renderType == "Cutout"));

            var mode = transparent ? 2f : cutout ? 1f : 0f;
            SetFloat(target, isMToon10 ? "_AlphaMode" : "_BlendMode", mode);
            SetFloat(target, "_Cutoff", Mathf.Clamp01(cutout ? cutoff : 0.5f));
            if (isMToon10 && transparent)
                SetFloat(target, "_TransparentWithZWrite", 0f);
        }

        /// <summary>MToon의 키워드/블렌드 상태를 셰이더 규칙대로 정리한다.</summary>
        private static void ValidateMToon(Material material, bool isMToon10)
        {
            // UniVRM 0.x: MToon.Utils.ValidateProperties(Material, bool)
            // UniVRM 1.x: UniVRM10.MToonValidator(Material).Validate()
            var utils = isMToon10
                ? FindType("UniVRM10.MToonValidator") ?? FindType("UniVRM10.MToon10Validator")
                : FindType("MToon.Utils") ?? FindType("VRM.MToon.Utils");
            if (utils == null)
            {
                ApplyBlendModeFallback(material, isMToon10);
                return;
            }

            try
            {
                if (isMToon10)
                {
                    var validator = Activator.CreateInstance(utils, material);
                    utils.GetMethod("Validate", BindingFlags.Public | BindingFlags.Instance)?.Invoke(validator, null);
                    return;
                }

                var method = utils.GetMethod("ValidateProperties", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    var parameters = method.GetParameters();
                    method.Invoke(null, parameters.Length >= 2
                        ? new object[] { material, true }
                        : new object[] { material });
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DiNe VRM] MToon validation failed, using fallback: {exception.Message}");
            }

            ApplyBlendModeFallback(material, isMToon10);
        }

        private static void ApplyBlendModeFallback(Material material, bool isMToon10)
        {
            var mode = material.HasProperty(isMToon10 ? "_AlphaMode" : "_BlendMode")
                ? material.GetFloat(isMToon10 ? "_AlphaMode" : "_BlendMode")
                : 0f;

            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            if (mode >= 2f)
            {
                material.EnableKeyword("_ALPHABLEND_ON");
                SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                SetFloat(material, "_ZWrite", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (mode >= 1f)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                SetFloat(material, "_ZWrite", 1f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                SetFloat(material, "_ZWrite", 1f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
        }

        private static string PrepareFolder(string avatarName)
        {
            var safeName = string.Join("_", (avatarName ?? "Avatar").Split(Path.GetInvalidFileNameChars()));
            var path = $"{OutputRoot}/{safeName}";
            if (AssetDatabase.IsValidFolder(path))
                return path;

            var current = "Assets";
            foreach (var part in path.Substring("Assets/".Length).Split('/'))
            {
                var next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
            return current;
        }

        private static Material SaveAsset(Material material, string folder, string sourceName)
        {
            if (material == null)
                return null;

            var safeName = string.Join("_", (sourceName ?? "Material").Split(Path.GetInvalidFileNameChars()));
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_MToon.mat");
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path) ?? material;
        }

        private static bool HasAny(Material material, IEnumerable<string> names)
        {
            return names.Any(material.HasProperty);
        }

        private static string FindName(Material material, IEnumerable<string> names)
        {
            return names.FirstOrDefault(material.HasProperty);
        }

        private static Texture GetTexture(Material material, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                if (!material.HasProperty(name))
                    continue;
                var texture = material.GetTexture(name);
                if (texture != null)
                    return texture;
            }
            return null;
        }

        private static bool TryGetColor(Material material, IEnumerable<string> names, out Color color)
        {
            var name = FindName(material, names);
            if (name == null)
            {
                color = Color.white;
                return false;
            }
            color = material.GetColor(name);
            return true;
        }

        private static Color GetColor(Material material, IEnumerable<string> names, Color fallback)
        {
            return TryGetColor(material, names, out var color) ? color : fallback;
        }

        private static float GetFloat(Material material, IEnumerable<string> names, float fallback)
        {
            var name = FindName(material, names);
            return name != null ? material.GetFloat(name) : fallback;
        }

        private static void SetTexture(Material material, string name, Texture texture)
        {
            if (!string.IsNullOrEmpty(name) && material.HasProperty(name))
                material.SetTexture(name, texture);
        }

        private static void SetTextureTransform(Material target, string targetName, Material source, IEnumerable<string> sourceNames)
        {
            var sourceName = FindName(source, sourceNames);
            if (sourceName == null || !target.HasProperty(targetName))
                return;
            target.SetTextureScale(targetName, source.GetTextureScale(sourceName));
            target.SetTextureOffset(targetName, source.GetTextureOffset(sourceName));
        }

        private static void SetColor(Material material, string name, Color color)
        {
            if (!string.IsNullOrEmpty(name) && material.HasProperty(name))
                material.SetColor(name, color);
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (!string.IsNullOrEmpty(name) && material.HasProperty(name))
                material.SetFloat(name, value);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
