using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Avatars.Components;

namespace DiNeTool.ExtraModifier.Editor
{
    internal static class DiNeFocusProcessor
    {
        public static DiNeFocusReport Process(GameObject avatarObject, UnityEngine.Object assetContainer = null)
        {
            var report = new DiNeFocusReport();
            if (!DiNeFocus.FeatureEnabled)
                return report;

            var descriptor = avatarObject != null ? avatarObject.GetComponent<VRCAvatarDescriptor>() : null;
            var settings = FindSettings(descriptor);
            if (descriptor == null || settings == null)
                return report;

            report.Processed = true;
            var variants = new Dictionary<Material, Material>();
            var bodyMaterials = CollectBodyMaterials(descriptor.gameObject);
            AddAnimatedBodyMaterials(descriptor, bodyMaterials);

            foreach (var renderer in descriptor.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                var changed = false;
                var bodyRenderer = IsBodyName(renderer.name);

                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                        continue;

                    report.ScannedSlots++;
                    if (bodyRenderer || bodyMaterials.Contains(material))
                    {
                        report.ExcludedSlots++;
                        continue;
                    }

                    var decision = DiNeFocusMaterialClassifier.Evaluate(material, settings);
                    if (!decision.ShouldFix)
                    {
                        if (decision.IsExcluded)
                            report.ExcludedSlots++;
                        continue;
                    }

                    if (CanMutateGeneratedMaterial(material))
                    {
                        DiNeFocusMaterialSettings.Apply(material, settings);
                        EditorUtility.SetDirty(material);
                        report.ChangedSlots++;
                        continue;
                    }

                    if (!variants.TryGetValue(material, out var variant))
                    {
                        variant = UnityEngine.Object.Instantiate(material);
                        variant.name = $"{material.name} (DiNe Focus)";
                        variant.hideFlags = HideFlags.HideInHierarchy;
                        DiNeFocusMaterialSettings.Apply(variant, settings);

                        if (assetContainer != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(assetContainer)))
                            AssetDatabase.AddObjectToAsset(variant, assetContainer);

                        variants.Add(material, variant);
                    }

                    materials[i] = variant;
                    changed = true;
                    report.ChangedSlots++;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }

            ProcessAnimatedGeneratedMaterials(descriptor, settings, bodyMaterials, report);
            report.UniqueVariants = variants.Count;
            return report;
        }

        internal static DiNeFocus FindSettings(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
                return null;

            return descriptor.GetComponentsInChildren<DiNeFocus>(true)
                .FirstOrDefault(item => item != null && item.isActiveAndEnabled &&
                    item.GetComponentInParent<VRCAvatarDescriptor>(true) == descriptor);
        }

        private static HashSet<Material> CollectBodyMaterials(GameObject avatar)
        {
            var result = new HashSet<Material>();
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsBodyName(renderer.name))
                    continue;

                foreach (var material in renderer.sharedMaterials)
                    if (material != null)
                        result.Add(material);
            }
            return result;
        }

        private static void AddAnimatedBodyMaterials(VRCAvatarDescriptor avatar, HashSet<Material> result)
        {
            foreach (var reference in GetAnimatedMaterials(avatar))
                if (reference.Material != null && IsBodyPath(reference.Path))
                    result.Add(reference.Material);
        }

        private static void ProcessAnimatedGeneratedMaterials(
            VRCAvatarDescriptor avatar,
            DiNeFocus settings,
            HashSet<Material> bodyMaterials,
            DiNeFocusReport report)
        {
            var seen = new HashSet<Material>();
            foreach (var reference in GetAnimatedMaterials(avatar))
            {
                var material = reference.Material;
                if (material == null || !seen.Add(material) || !CanMutateGeneratedMaterial(material))
                    continue;
                if (IsBodyPath(reference.Path) || bodyMaterials.Contains(material))
                    continue;

                var decision = DiNeFocusMaterialClassifier.Evaluate(material, settings);
                if (!decision.ShouldFix)
                    continue;

                DiNeFocusMaterialSettings.Apply(material, settings);
                EditorUtility.SetDirty(material);
                report.ChangedSlots++;
            }
        }

        private static IEnumerable<AnimatedMaterialReference> GetAnimatedMaterials(VRCAvatarDescriptor avatar)
        {
            var controllers = avatar.GetComponentsInChildren<Animator>(true)
                .Select(animator => animator.runtimeAnimatorController)
                .Concat(avatar.baseAnimationLayers.Select(layer => layer.animatorController))
                .Concat(avatar.specialAnimationLayers.Select(layer => layer.animatorController))
                .Where(controller => controller != null)
                .Distinct();

            foreach (var controller in controllers)
            foreach (var clip in controller.animationClips.Distinct())
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (!typeof(Renderer).IsAssignableFrom(binding.type))
                    continue;

                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    if (key.value is Material material)
                        yield return new AnimatedMaterialReference(material, binding.path);
            }
        }

        private static bool CanMutateGeneratedMaterial(Material material)
        {
            if (!EditorUtility.IsPersistent(material))
                return true;

            var path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path))
                return false;

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            return mainAsset is GeneratedAssets || mainAsset is SubAssetContainer;
        }

        private static bool IsBodyPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.Split('/').Any(IsBodyName);
        }

        private static bool IsBodyName(string value)
        {
            return string.Equals(value, "Body", StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct AnimatedMaterialReference
        {
            public readonly Material Material;
            public readonly string Path;

            public AnimatedMaterialReference(Material material, string path)
            {
                Material = material;
                Path = path;
            }
        }
    }

    internal sealed class DiNeFocusReport
    {
        public bool Processed;
        public int ScannedSlots;
        public int ChangedSlots;
        public int ExcludedSlots;
        public int UniqueVariants;

        public void Log(string context)
        {
            if (!Processed)
                return;
            Debug.Log($"[DiNe Focus] {context} | scanned {ScannedSlots}, changed {ChangedSlots}, excluded {ExcludedSlots}, variants {UniqueVariants}");
        }
    }

    internal static class DiNeFocusMaterialSettings
    {
        private static readonly string[] ZWriteProperties =
        {
            "_ZWrite", "_PreZWrite", "_TransparentZWrite", "_ZWriteMode"
        };

        public static void Apply(Material material, DiNeFocus settings)
        {
            material.renderQueue = settings.TargetQueue;
            if (settings.TargetZWrite == DiNeFocusZWrite.Keep)
                return;

            var value = settings.TargetZWrite == DiNeFocusZWrite.On ? 1f : 0f;
            foreach (var property in ZWriteProperties)
                if (material.HasProperty(property))
                    material.SetFloat(property, value);
        }

        public static bool MatchesZWrite(Material material, DiNeFocusZWrite target)
        {
            if (target == DiNeFocusZWrite.Keep)
                return true;

            var expected = target == DiNeFocusZWrite.On ? 1f : 0f;
            foreach (var property in ZWriteProperties)
                if (material.HasProperty(property) && !Mathf.Approximately(material.GetFloat(property), expected))
                    return false;
            return true;
        }
    }

    internal static class DiNeFocusMaterialClassifier
    {
        public static MaterialDecision Evaluate(Material material, DiNeFocus settings)
        {
            if (material == null || material.shader == null)
                return MaterialDecision.Skip();

            var queue = material.renderQueue >= 0 ? material.renderQueue : material.shader.renderQueue;
            if (queue <= DiNeFocus.QueueThreshold ||
                (queue == settings.TargetQueue && DiNeFocusMaterialSettings.MatchesZWrite(material, settings.TargetZWrite)))
                return MaterialDecision.Skip();
            if (settings.ForceAll)
                return MaterialDecision.Fix();

            if (TryClassifyPoiyomi(material, out var decision))
                return decision;
            if (LooksUnsafe(material.shader.name))
                return MaterialDecision.Exclude();
            if (TryClassifyKnownProperty(material, out decision))
                return decision;
            if (TryClassifyTag(material.GetTag("RenderType", false, string.Empty), out decision))
                return decision;
            if (TryClassifyTag(material.GetTag("Queue", false, string.Empty), out decision))
                return decision;
            if (TryReadBlend(material, out var src, out var dst))
                return IsOpaque(src, dst) ? MaterialDecision.Fix() : MaterialDecision.Exclude();

            return MaterialDecision.Exclude();
        }

        private static bool TryClassifyPoiyomi(Material material, out MaterialDecision decision)
        {
            var isPoiyomi = Normalize(material.shader.name).Contains("poiyomi") ||
                (material.HasProperty("_AlphaMaskMode") && material.HasProperty("_SrcBlendFA") && material.HasProperty("_DstBlendFA"));
            if (!isPoiyomi || !TryReadBlend(material, out var src, out var dst))
            {
                decision = default;
                return false;
            }

            if (ReadFloat(material, "_AlphaMaskMode", out var alphaMask) && alphaMask > 0.5f)
                decision = MaterialDecision.Exclude();
            else
                decision = IsOpaque(src, dst) ? MaterialDecision.Fix() : MaterialDecision.Exclude();
            return true;
        }

        private static bool TryClassifyKnownProperty(Material material, out MaterialDecision decision)
        {
            if (ReadFloat(material, "_TransparentMode", out var value) ||
                ReadFloat(material, "_Surface", out value) ||
                (!Normalize(material.shader.name).Contains("poiyomi") && ReadFloat(material, "_Mode", out value)))
            {
                decision = value < 0.5f ? MaterialDecision.Fix() : MaterialDecision.Exclude();
                return true;
            }

            decision = default;
            return false;
        }

        private static bool TryClassifyTag(string tag, out MaterialDecision decision)
        {
            var value = Normalize(tag);
            if (string.IsNullOrEmpty(value))
            {
                decision = default;
                return false;
            }
            if (value.Contains("cutout") || value.Contains("alphatest") || value.Contains("transparent") ||
                value.Contains("overlay") || value.Contains("fade"))
            {
                decision = MaterialDecision.Exclude();
                return true;
            }
            if (value.Contains("opaque") || value.Contains("geometry"))
            {
                decision = MaterialDecision.Fix();
                return true;
            }
            decision = default;
            return false;
        }

        private static bool TryReadBlend(Material material, out int src, out int dst)
        {
            if (ReadFloat(material, "_SrcBlend", out var srcValue) && ReadFloat(material, "_DstBlend", out var dstValue))
            {
                src = Mathf.RoundToInt(srcValue);
                dst = Mathf.RoundToInt(dstValue);
                return true;
            }
            src = dst = 0;
            return false;
        }

        private static bool ReadFloat(Material material, string property, out float value)
        {
            if (material.HasProperty(property))
            {
                value = material.GetFloat(property);
                return true;
            }
            value = 0f;
            return false;
        }

        private static bool IsOpaque(int src, int dst)
        {
            return src == (int)BlendMode.One && dst == (int)BlendMode.Zero;
        }

        private static bool LooksUnsafe(string shaderName)
        {
            var value = Normalize(shaderName);
            var unsafeWords = new[]
            {
                "fade", "cutout", "alphatest", "fur", "gem", "glass", "additive", "particle",
                "refraction", "refract", "water", "transparent"
            };
            return unsafeWords.Any(value.Contains) || value.Contains("trans");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }
    }

    internal readonly struct MaterialDecision
    {
        public readonly bool ShouldFix;
        public readonly bool IsExcluded;

        private MaterialDecision(bool shouldFix, bool isExcluded)
        {
            ShouldFix = shouldFix;
            IsExcluded = isExcluded;
        }

        public static MaterialDecision Fix() => new MaterialDecision(true, false);
        public static MaterialDecision Exclude() => new MaterialDecision(false, true);
        public static MaterialDecision Skip() => new MaterialDecision(false, false);
    }
}
