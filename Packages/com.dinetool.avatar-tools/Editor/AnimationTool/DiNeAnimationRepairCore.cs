using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Read-only binding inspection and copy-only animation repair. No avatar references are assigned.</summary>
public static class DiNeAnimationRepairCore
{
    public enum BindingStatus
    {
        Valid, MissingPath, AmbiguousPath, MissingComponent, MissingBlendShape,
        MissingMaterialSlot, UnverifiedProperty, UnverifiedHumanoid, IncompatibleCurveType
    }

    [Serializable]
    public sealed class BindingMapping
    {
        public string SourcePath = "";
        public string SourceProperty = "";
        public string TypeName = "";
        public bool IsObjectReference;
        public string TargetPath = "";
        public string TargetProperty = "";
        public bool Enabled = true;
    }

    [Serializable]
    public sealed class MappingProfile
    {
        public int Version = 1;
        public List<BindingMapping> Mappings = new List<BindingMapping>();
    }

    public sealed class BindingGroup
    {
        public EditorCurveBinding Binding;
        public bool IsObjectReference;
        public BindingStatus Status;
        public readonly List<AnimationClip> Clips = new List<AnimationClip>();
        public readonly List<string> CandidatePaths = new List<string>();
    }

    public sealed class RepairCollision
    {
        public AnimationClip Clip;
        public EditorCurveBinding Destination;
        public readonly List<EditorCurveBinding> Sources = new List<EditorCurveBinding>();
    }

    public sealed class MappingProblem
    {
        public EditorCurveBinding Source;
        public EditorCurveBinding Destination;
        public BindingStatus Status;
        public bool DuplicateMapping;
    }

    public sealed class RepairPlan
    {
        public readonly List<RepairCollision> Collisions = new List<RepairCollision>();
        public readonly List<MappingProblem> InvalidMappings = new List<MappingProblem>();
        public readonly List<BindingGroup> Remaining = new List<BindingGroup>();
        public readonly List<AnimationClip> ChangedClips = new List<AnimationClip>();
        public int ChangedBindingCount;
        public int ChangedClipCount;
        public bool CanSave => ChangedBindingCount > 0 && Collisions.Count == 0 && InvalidMappings.Count == 0;
    }

    private struct CurveSnapshot
    {
        public EditorCurveBinding Source;
        public EditorCurveBinding Destination;
        public bool IsObjectReference;
        public AnimationCurve Curve;
        public ObjectReferenceKeyframe[] ObjectKeys;
    }

    private sealed class AvatarIndex
    {
        public readonly GameObject Root;
        public readonly Dictionary<string, List<GameObject>> Objects = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, EditorCurveBinding[]> animatable = new Dictionary<GameObject, EditorCurveBinding[]>();

        public AvatarIndex(GameObject root)
        {
            Root = root;
            if (!root) return;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(transform, root.transform);
                if (!Objects.TryGetValue(path, out List<GameObject> objects)) Objects[path] = objects = new List<GameObject>();
                objects.Add(transform.gameObject);
            }
        }

        public EditorCurveBinding[] Animatable(GameObject target)
        {
            if (!animatable.TryGetValue(target, out EditorCurveBinding[] bindings))
            {
                bindings = AnimationUtility.GetAnimatableBindings(target, Root);
                animatable[target] = bindings;
            }
            return bindings;
        }
    }

    /// <summary>One current hierarchy index, scoped by the caller to a single inspection or GUI pass.</summary>
    public sealed class InspectionContext
    {
        private readonly AvatarIndex index;
        private readonly Dictionary<string, List<string>> propertyChoices = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public InspectionContext(GameObject root) { index = new AvatarIndex(root); }

        public BindingStatus ValidateBinding(EditorCurveBinding binding) => Validate(index, binding);

        public List<string> FindCandidatePaths(EditorCurveBinding binding) => Candidates(index, binding);

        public List<string> GetPropertyChoices(EditorCurveBinding binding)
        {
            string property = binding.propertyName ?? "";
            string category = property.StartsWith("blendShape.", StringComparison.Ordinal) ? "blendShape."
                : IsMaterialSlot(property, out _) ? "m_Materials" : "";
            string key = Key(binding.path, binding.type?.AssemblyQualifiedName, category, binding.isPPtrCurve);
            if (!propertyChoices.TryGetValue(key, out List<string> result))
            {
                result = Properties(index, binding);
                propertyChoices.Add(key, result);
            }
            return result;
        }
    }

    /// <summary>Collect effective clips, including nested blend trees and override-controller substitutions.</summary>
    public static List<AnimationClip> CollectClips(RuntimeAnimatorController controller)
    {
        return CollectController(controller, new HashSet<RuntimeAnimatorController>()).Where(c => c).Distinct().ToList();
    }

    private static List<AnimationClip> CollectController(RuntimeAnimatorController controller, HashSet<RuntimeAnimatorController> visited)
    {
        var result = new List<AnimationClip>();
        if (!controller || !visited.Add(controller)) return result;
        if (controller is AnimatorOverrideController overrides)
        {
            // animationClips is Unity's effective override list, including nested override substitutions.
            // Traversing the base as well would accidentally reintroduce replaced originals.
            AnimationClip[] effective = overrides.animationClips;
            if (effective != null && effective.Length > 0) return effective.Where(c => c).Distinct().ToList();
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            return pairs.Where(p => p.Key).Select(p => p.Value ? p.Value : p.Key).Distinct().ToList();
        }
        if (controller is AnimatorController animatorController)
        {
            var visitedMotions = new HashSet<Motion>();
            var visitedMachines = new HashSet<AnimatorStateMachine>();
            foreach (AnimatorControllerLayer layer in animatorController.layers)
                CollectStateMachine(layer.stateMachine, result, visitedMachines, visitedMotions);
        }
        foreach (AnimationClip clip in controller.animationClips) if (clip) result.Add(clip);
        return result.Distinct().ToList();
    }

    private static void CollectStateMachine(AnimatorStateMachine machine, List<AnimationClip> clips,
        HashSet<AnimatorStateMachine> visitedMachines, HashSet<Motion> visitedMotions)
    {
        if (!machine || !visitedMachines.Add(machine)) return;
        foreach (ChildAnimatorState state in machine.states) if (state.state) CollectMotion(state.state.motion, clips, visitedMotions);
        foreach (ChildAnimatorStateMachine child in machine.stateMachines) CollectStateMachine(child.stateMachine, clips, visitedMachines, visitedMotions);
    }

    private static void CollectMotion(Motion motion, List<AnimationClip> clips, HashSet<Motion> visited)
    {
        if (!motion || !visited.Add(motion)) return;
        if (motion is AnimationClip clip) clips.Add(clip);
        else if (motion is BlendTree tree) foreach (ChildMotion child in tree.children) CollectMotion(child.motion, clips, visited);
    }

    public static BindingMapping MappingFor(EditorCurveBinding source, bool isObjectReference, string targetPath, string targetProperty)
    {
        return new BindingMapping
        {
            SourcePath = source.path ?? "", SourceProperty = source.propertyName ?? "",
            TypeName = source.type?.AssemblyQualifiedName ?? "", IsObjectReference = isObjectReference,
            TargetPath = targetPath ?? "", TargetProperty = targetProperty ?? source.propertyName ?? "", Enabled = true
        };
    }

    public static List<BindingGroup> Analyze(GameObject root, IEnumerable<AnimationClip> clips)
    {
        var index = new AvatarIndex(root);
        var groups = new Dictionary<string, BindingGroup>(StringComparer.Ordinal);
        foreach (AnimationClip clip in DistinctClips(clips))
        {
            foreach (CurveSnapshot snapshot in Snapshot(clip, false))
            {
                string key = Key(snapshot.Source, snapshot.IsObjectReference);
                if (!groups.TryGetValue(key, out BindingGroup group))
                {
                    group = new BindingGroup { Binding = snapshot.Source, IsObjectReference = snapshot.IsObjectReference,
                        Status = Validate(index, snapshot.Source) };
                    if (group.Status != BindingStatus.Valid) group.CandidatePaths.AddRange(Candidates(index, snapshot.Source));
                    groups.Add(key, group);
                }
                group.Clips.Add(clip);
            }
        }
        return groups.Values.OrderBy(g => g.Status == BindingStatus.Valid ? 1 : 0)
            .ThenBy(g => g.Binding.path, StringComparer.Ordinal).ThenBy(g => g.Binding.propertyName, StringComparer.Ordinal).ToList();
    }

    public static BindingStatus ValidateBinding(GameObject root, EditorCurveBinding binding)
    {
        return new InspectionContext(root).ValidateBinding(binding);
    }

    public static List<string> FindCandidatePaths(GameObject root, EditorCurveBinding binding)
    {
        return new InspectionContext(root).FindCandidatePaths(binding);
    }

    public static List<string> GetPropertyChoices(GameObject root, EditorCurveBinding binding)
    {
        return new InspectionContext(root).GetPropertyChoices(binding);
    }

    private static List<string> Properties(AvatarIndex index, EditorCurveBinding binding)
    {
        var result = new List<string>();
        if (!index.Objects.TryGetValue(binding.path ?? "", out List<GameObject> matches) || matches.Count != 1) return result;
        GameObject target = matches[0];
        if (binding.type == typeof(SkinnedMeshRenderer) && (binding.propertyName ?? "").StartsWith("blendShape.", StringComparison.Ordinal))
        {
            if (binding.isPPtrCurve) return result;
            var renderer = target.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer ? renderer.sharedMesh : null;
            if (mesh) for (int i = 0; i < mesh.blendShapeCount; i++) result.Add("blendShape." + mesh.GetBlendShapeName(i));
        }
        else if (IsMaterialSlot(binding.propertyName, out _) && binding.type != null && typeof(Renderer).IsAssignableFrom(binding.type))
        {
            if (!binding.isPPtrCurve) return result;
            var renderer = target.GetComponent(binding.type) as Renderer;
            if (renderer) for (int i = 0; i < renderer.sharedMaterials.Length; i++) result.Add("m_Materials.Array.data[" + i + "]");
        }
        else
        {
            foreach (EditorCurveBinding candidate in index.Animatable(target))
                if (candidate.type == binding.type && candidate.path == binding.path && candidate.isPPtrCurve == binding.isPPtrCurve) result.Add(candidate.propertyName);
        }
        return result.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    public static RepairPlan Plan(GameObject root, IEnumerable<AnimationClip> clips, IEnumerable<BindingMapping> mappings)
    {
        var plan = new RepairPlan();
        var index = new AvatarIndex(root);
        List<AnimationClip> sources = DistinctClips(clips);
        var lookup = BuildMappingLookup(mappings);
        var remaining = new Dictionary<string, BindingGroup>(StringComparer.Ordinal);
        foreach (AnimationClip clip in sources)
        {
            bool clipChanged = false;
            var destinationSources = new Dictionary<string, List<EditorCurveBinding>>(StringComparer.Ordinal);
            var destinationBindings = new Dictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            foreach (CurveSnapshot snapshot in Snapshot(clip, false))
            {
                EditorCurveBinding destination = Resolve(snapshot.Source, snapshot.IsObjectReference, lookup, out bool duplicate);
                bool changed = !SameBinding(snapshot.Source, destination);
                BindingStatus status = Validate(index, destination);
                if (duplicate || (changed && status != BindingStatus.Valid))
                    plan.InvalidMappings.Add(new MappingProblem { Source = snapshot.Source, Destination = destination, Status = status, DuplicateMapping = duplicate });
                if (changed) { plan.ChangedBindingCount++; clipChanged = true; }
                if (status != BindingStatus.Valid)
                {
                    string remainingKey = Key(destination, snapshot.IsObjectReference);
                    if (!remaining.TryGetValue(remainingKey, out BindingGroup group))
                    {
                        group = new BindingGroup { Binding = destination, IsObjectReference = snapshot.IsObjectReference, Status = status };
                        remaining.Add(remainingKey, group);
                    }
                    group.Clips.Add(clip);
                }
                string destinationKey = Key(destination, false); // A property cannot receive both value and object-reference curves.
                if (!destinationSources.TryGetValue(destinationKey, out List<EditorCurveBinding> bindings))
                {
                    destinationSources.Add(destinationKey, bindings = new List<EditorCurveBinding>());
                    destinationBindings.Add(destinationKey, destination);
                }
                bindings.Add(snapshot.Source);
            }
            foreach (var pair in destinationSources.Where(p => p.Value.Count > 1))
            {
                var collision = new RepairCollision { Clip = clip, Destination = destinationBindings[pair.Key] };
                collision.Sources.AddRange(pair.Value);
                plan.Collisions.Add(collision);
            }
            if (clipChanged) { plan.ChangedClipCount++; plan.ChangedClips.Add(clip); }
        }
        plan.Remaining.AddRange(remaining.Values);
        return plan;
    }

    /// <summary>Build a nonpersistent copy. Changed curves are all removed before any destination is written.</summary>
    public static AnimationClip CreateRepairedClip(AnimationClip source, GameObject root, IEnumerable<BindingMapping> mappings)
    {
        if (!source) throw new ArgumentNullException(nameof(source));
        List<BindingMapping> mappingList = mappings?.Where(m => m != null).ToList() ?? new List<BindingMapping>();
        RepairPlan plan = Plan(root, new[] { source }, mappingList);
        if (plan.Collisions.Count != 0 || plan.InvalidMappings.Count != 0)
            throw new InvalidOperationException("Repair destinations must be verified and must not overwrite another curve.");
        var lookup = BuildMappingLookup(mappingList);
        List<CurveSnapshot> snapshots = Snapshot(source);
        for (int i = 0; i < snapshots.Count; i++)
        {
            CurveSnapshot snapshot = snapshots[i];
            snapshot.Destination = Resolve(snapshot.Source, snapshot.IsObjectReference, lookup, out _);
            snapshots[i] = snapshot;
        }
        AnimationClip result = UnityEngine.Object.Instantiate(source);
        result.name = source.name + "_Repaired";
        result.hideFlags = HideFlags.None;
        try
        {
            foreach (CurveSnapshot snapshot in snapshots.Where(s => !SameBinding(s.Source, s.Destination)))
            {
                if (snapshot.IsObjectReference) AnimationUtility.SetObjectReferenceCurve(result, snapshot.Source, null);
                else AnimationUtility.SetEditorCurve(result, snapshot.Source, null);
            }
            foreach (CurveSnapshot snapshot in snapshots.Where(s => !SameBinding(s.Source, s.Destination)))
            {
                if (snapshot.IsObjectReference) AnimationUtility.SetObjectReferenceCurve(result, snapshot.Destination, snapshot.ObjectKeys);
                else AnimationUtility.SetEditorCurve(result, snapshot.Destination, snapshot.Curve);
            }
            AnimationUtility.SetAnimationClipSettings(result, AnimationUtility.GetAnimationClipSettings(source));
            AnimationUtility.SetAnimationEvents(result, AnimationUtility.GetAnimationEvents(source));
            return result;
        }
        catch { UnityEngine.Object.DestroyImmediate(result); throw; }
    }

    public static string ToProfileJson(IEnumerable<BindingMapping> mappings)
    {
        return JsonUtility.ToJson(new MappingProfile { Mappings = mappings?.Where(m => m != null).ToList() ?? new List<BindingMapping>() }, true);
    }

    public static List<BindingMapping> FromProfileJson(string json)
    {
        MappingProfile profile = JsonUtility.FromJson<MappingProfile>(json);
        if (profile == null || profile.Version != 1 || profile.Mappings == null)
            throw new ArgumentException("Unsupported animation mapping profile.", nameof(json));
        if (profile.Mappings.Any(m => m == null || string.IsNullOrEmpty(m.TypeName) || m.SourceProperty == null || m.TargetProperty == null))
            throw new ArgumentException("The animation mapping profile contains incomplete rows.", nameof(json));
        return profile.Mappings;
    }

    private static List<AnimationClip> DistinctClips(IEnumerable<AnimationClip> clips)
    {
        return clips?.Where(c => c).Distinct().ToList() ?? new List<AnimationClip>();
    }

    private static List<CurveSnapshot> Snapshot(AnimationClip clip, bool captureValues = true)
    {
        var result = new List<CurveSnapshot>();
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            result.Add(new CurveSnapshot { Source = binding, Destination = binding, Curve = captureValues ? AnimationUtility.GetEditorCurve(clip, binding) : null });
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            result.Add(new CurveSnapshot { Source = binding, Destination = binding, IsObjectReference = true,
                ObjectKeys = captureValues ? AnimationUtility.GetObjectReferenceCurve(clip, binding) : null });
        return result;
    }

    private static Dictionary<string, List<BindingMapping>> BuildMappingLookup(IEnumerable<BindingMapping> mappings)
    {
        var result = new Dictionary<string, List<BindingMapping>>(StringComparer.Ordinal);
        if (mappings == null) return result;
        foreach (BindingMapping mapping in mappings.Where(m => m != null && m.Enabled))
        {
            string key = Key(mapping.SourcePath, mapping.TypeName, mapping.SourceProperty, mapping.IsObjectReference);
            if (!result.TryGetValue(key, out List<BindingMapping> list)) result[key] = list = new List<BindingMapping>();
            list.Add(mapping);
        }
        return result;
    }

    private static EditorCurveBinding Resolve(EditorCurveBinding source, bool objectReference,
        Dictionary<string, List<BindingMapping>> mappings, out bool duplicate)
    {
        duplicate = false;
        if (!mappings.TryGetValue(Key(source, objectReference), out List<BindingMapping> matches)) return source;
        BindingMapping mapping = matches[0];
        duplicate = matches.Any(m => (m.TargetPath ?? "") != (mapping.TargetPath ?? "") || (m.TargetProperty ?? "") != (mapping.TargetProperty ?? ""));
        // Copy the struct rather than recreating it, preserving discrete/serialize-reference binding flags.
        source.path = mapping.TargetPath ?? "";
        source.propertyName = mapping.TargetProperty ?? source.propertyName;
        return source;
    }

    private static bool SameBinding(EditorCurveBinding left, EditorCurveBinding right)
    {
        return left.type == right.type && left.path == right.path && left.propertyName == right.propertyName;
    }

    private static string Key(EditorCurveBinding binding, bool objectReference)
    {
        return Key(binding.path, binding.type?.AssemblyQualifiedName, binding.propertyName, objectReference);
    }

    private static string Key(string path, string typeName, string property, bool objectReference)
    {
        // Length prefixes keep literal names containing separators unambiguous.
        path = path ?? ""; typeName = typeName ?? ""; property = property ?? "";
        return path.Length + ":" + path + typeName.Length + ":" + typeName + property.Length + ":" + property + (objectReference ? "1" : "0");
    }

    private static BindingStatus Validate(AvatarIndex index, EditorCurveBinding binding)
    {
        if (!index.Root || !index.Objects.TryGetValue(binding.path ?? "", out List<GameObject> matches)) return BindingStatus.MissingPath;
        if (matches.Count != 1) return BindingStatus.AmbiguousPath;
        GameObject target = matches[0];
        Type type = binding.type;
        if (type == null || (type != typeof(GameObject) && (!typeof(Component).IsAssignableFrom(type) || !target.GetComponent(type))))
            return BindingStatus.MissingComponent;
        string property = binding.propertyName ?? "";
        if (property.StartsWith("blendShape.", StringComparison.Ordinal))
        {
            if (binding.isPPtrCurve) return BindingStatus.IncompatibleCurveType;
            var renderer = target.GetComponent<SkinnedMeshRenderer>();
            if (type != typeof(SkinnedMeshRenderer) || !renderer) return BindingStatus.MissingComponent;
            return renderer.sharedMesh && renderer.sharedMesh.GetBlendShapeIndex(property.Substring("blendShape.".Length)) >= 0
                ? BindingStatus.Valid : BindingStatus.MissingBlendShape;
        }
        if (IsMaterialSlot(property, out int slot))
        {
            if (!binding.isPPtrCurve) return BindingStatus.IncompatibleCurveType;
            if (!typeof(Renderer).IsAssignableFrom(type)) return BindingStatus.MissingComponent;
            var renderer = target.GetComponent(type) as Renderer;
            if (!renderer) return BindingStatus.MissingComponent;
            return slot >= 0 && slot < renderer.sharedMaterials.Length ? BindingStatus.Valid : BindingStatus.MissingMaterialSlot;
        }
        if (type == typeof(GameObject) && property == "m_IsActive")
            return binding.isPPtrCurve ? BindingStatus.IncompatibleCurveType : BindingStatus.Valid;
        if (type == typeof(Animator) && property != "m_Enabled") return BindingStatus.UnverifiedHumanoid;
        foreach (EditorCurveBinding candidate in index.Animatable(target))
            if (SameBinding(binding, candidate))
                return candidate.isPPtrCurve == binding.isPPtrCurve ? BindingStatus.Valid : BindingStatus.IncompatibleCurveType;
        // Muscle, root-motion and custom/nonserialized properties cannot be proven by hierarchy inspection.
        return type == typeof(Animator) ? BindingStatus.UnverifiedHumanoid : BindingStatus.UnverifiedProperty;
    }

    private static bool IsMaterialSlot(string property, out int slot)
    {
        const string prefix = "m_Materials.Array.data[";
        slot = -1;
        return property != null && property.StartsWith(prefix, StringComparison.Ordinal) && property.EndsWith("]", StringComparison.Ordinal)
            && int.TryParse(property.Substring(prefix.Length, property.Length - prefix.Length - 1), out slot);
    }

    private static List<string> Candidates(AvatarIndex index, EditorCurveBinding binding)
    {
        if (!index.Root || binding.type == null) return new List<string>();
        string oldLeaf = (binding.path ?? "").Split('/').LastOrDefault() ?? "";
        return index.Objects.Where(pair => pair.Value.Count == 1 && (binding.type == typeof(GameObject)
                || (typeof(Component).IsAssignableFrom(binding.type) && pair.Value[0].GetComponent(binding.type))))
            .Select(pair => pair.Key).OrderBy(path => string.Equals(path.Split('/').LastOrDefault(), oldLeaf, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => path, StringComparer.Ordinal).ToList();
    }
}
