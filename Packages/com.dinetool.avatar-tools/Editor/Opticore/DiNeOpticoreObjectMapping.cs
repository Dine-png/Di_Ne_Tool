#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

/// <summary>
/// Tracks how the Opticore optimization passes move, remove, merge or rename objects/properties on
/// a (cloned) avatar, then rewrites all affected animations so they keep working.
///
/// Design notes:
///  - Original clip and controller ASSETS are never mutated. We clone each affected
///    <see cref="AnimationClip"/> (a single clip clone is safe) and substitute it through an
///    <see cref="AnimatorOverrideController"/>, so the controller graph itself is left untouched.
///  - Surviving objects that merely changed path are remapped automatically by diffing each
///    transform's original path against its final path (captured at construction vs. computed in
///    <see cref="Apply"/>). Passes do not need to report simple reparents.
///  - Destroyed/merged objects must be reported via <see cref="RecordMerge"/> (with optional
///    blendShape-rename and material-slot remaps) so their animation bindings can be redirected to
///    the surviving renderer.
///
/// This is the shared foundation that more aggressive merges (blendShape-bearing skinned mesh
/// merge, animation-referenced bone merge, material-slot merge, ...) build on.
/// </summary>
internal sealed class DiNeOpticoreObjectMapping
{
    private sealed class Redirect
    {
        public Transform Target;                                 // surviving object that absorbs the bindings
        public Dictionary<string, string> BlendShapeRename;      // old shape name -> new name; "" => removed (frozen)
        public Dictionary<int, int> MaterialSlotRemap;           // old material slot -> new slot
    }

    private readonly Transform _root;
    private readonly Dictionary<Transform, string> _originalPaths = new Dictionary<Transform, string>();

    // Keyed by the ORIGINAL (pre-removal) path string, captured at record time.
    private readonly Dictionary<string, Redirect> _redirects = new Dictionary<string, Redirect>();

    public DiNeOpticoreObjectMapping(GameObject root)
    {
        _root = root != null ? root.transform : null;
        if (_root == null)
            return;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string path = ComputePath(_root, t);
            if (path != null)
                _originalPaths[t] = path;
        }
    }

    /// <summary>The original (pre-optimization) animation path of a transform, or null if unknown.</summary>
    public bool TryGetOriginalPath(Transform t, out string path) => _originalPaths.TryGetValue(t, out path);

    /// <summary>
    /// Record that <paramref name="source"/> is being merged into / replaced by <paramref name="target"/>.
    /// Animation bindings that target the source renderer are redirected to the target, applying the
    /// optional blendShape-name and material-slot remaps. Call this BEFORE destroying the source.
    /// </summary>
    public void RecordMerge(
        Transform source,
        Transform target,
        Dictionary<string, string> blendShapeRename = null,
        Dictionary<int, int> materialSlotRemap = null)
    {
        if (source == null || target == null)
            return;

        if (!_originalPaths.TryGetValue(source, out string sourcePath))
            return;

        _redirects[sourcePath] = new Redirect
        {
            Target = target,
            BlendShapeRename = blendShapeRename,
            MaterialSlotRemap = materialSlotRemap,
        };
    }

    /// <summary>
    /// Rewrites every animation on the avatar so it follows the recorded moves/merges. Safe to call
    /// unconditionally: it is a no-op when nothing actually changed.
    /// </summary>
    public void Apply(GameObject root)
    {
        if (_root == null || root == null)
            return;

        Dictionary<string, string> pathRemap = BuildPathRemap();
        if (pathRemap.Count == 0 && _redirects.Count == 0)
            return;

        var clipCache = new Dictionary<AnimationClip, AnimationClip>();

        // Animator / AnimatorOverrideController hosts.
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null)
                continue;

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                continue;

            AnimationClip[] clips = controller.animationClips;
            if (clips == null || clips.Length == 0)
                continue;

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip clip in clips.Distinct())
            {
                AnimationClip rewritten = GetRewrittenClip(clip, pathRemap, clipCache);
                if (rewritten != null && rewritten != clip)
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, rewritten));
            }

            if (overrides.Count == 0)
                continue;

            // Substitute the rewritten clips without touching the controller graph itself.
            var overrideController = new AnimatorOverrideController(controller)
            {
                name = controller.name + " [Opticore Remapped]"
            };
            overrideController.ApplyOverrides(overrides);
            animator.runtimeAnimatorController = overrideController;
        }

        // Legacy Animation hosts.
        foreach (Animation animation in root.GetComponentsInChildren<Animation>(true))
        {
            if (animation == null)
                continue;

            AnimationClip mainClip = animation.clip;
            var states = animation.Cast<AnimationState>().ToList();
            foreach (AnimationState state in states)
            {
                AnimationClip original = state != null ? state.clip : null;
                if (original == null)
                    continue;

                AnimationClip rewritten = GetRewrittenClip(original, pathRemap, clipCache);
                if (rewritten == null || rewritten == original)
                    continue;

                animation.RemoveClip(original);
                animation.AddClip(rewritten, rewritten.name);
                if (mainClip == original)
                    animation.clip = rewritten;
            }
        }
    }

    // ---------------------------------------------------------------------------------------------

    private Dictionary<string, string> BuildPathRemap()
    {
        var remap = new Dictionary<string, string>();
        foreach (KeyValuePair<Transform, string> kv in _originalPaths)
        {
            Transform t = kv.Key;
            if (t == null)
                continue; // destroyed: handled through _redirects instead

            string current = ComputePath(_root, t);
            if (current != null && current != kv.Value)
                remap[kv.Value] = current;
        }
        return remap;
    }

    /// <summary>Returns a rewritten clone of <paramref name="clip"/>, or the clip itself if no binding changed.</summary>
    private AnimationClip GetRewrittenClip(
        AnimationClip clip,
        Dictionary<string, string> pathRemap,
        Dictionary<AnimationClip, AnimationClip> cache)
    {
        if (clip == null)
            return null;

        if (cache.TryGetValue(clip, out AnimationClip cached))
            return cached;

        // First pass: does anything actually change? Avoid cloning untouched clips.
        bool needsRewrite = false;
        foreach (EditorCurveBinding binding in EnumerateAllBindings(clip))
        {
            if (TryRemapBinding(binding, pathRemap, out _, out _))
            {
                needsRewrite = true;
                break;
            }
        }

        if (!needsRewrite)
        {
            cache[clip] = clip;
            return clip;
        }

        var clone = UnityObject.Instantiate(clip);
        clone.name = clip.name + " [Opticore]";
        RewriteClipBindings(clone, pathRemap);
        cache[clip] = clone;
        return clone;
    }

    private void RewriteClipBindings(AnimationClip clip, Dictionary<string, string> pathRemap)
    {
        // Float curves.
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (!TryRemapBinding(binding, pathRemap, out EditorCurveBinding newBinding, out bool drop))
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            AnimationUtility.SetEditorCurve(clip, binding, null);
            if (!drop)
                AnimationUtility.SetEditorCurve(clip, newBinding, curve);
        }

        // Object-reference curves (e.g. material swap animations).
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (!TryRemapBinding(binding, pathRemap, out EditorCurveBinding newBinding, out bool drop))
                continue;

            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            if (!drop)
                AnimationUtility.SetObjectReferenceCurve(clip, newBinding, keys);
        }
    }

    private static IEnumerable<EditorCurveBinding> EnumerateAllBindings(AnimationClip clip)
    {
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
            yield return b;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            yield return b;
    }

    /// <summary>
    /// Computes the new binding for an old one. Returns false when the binding is unaffected.
    /// <paramref name="drop"/> is true when the binding should be removed entirely (e.g. a frozen blendShape).
    /// </summary>
    private bool TryRemapBinding(
        EditorCurveBinding binding,
        Dictionary<string, string> pathRemap,
        out EditorCurveBinding newBinding,
        out bool drop)
    {
        newBinding = binding;
        drop = false;

        // Redirect (the target object was merged/removed).
        if (_redirects.TryGetValue(binding.path, out Redirect redirect) && IsRendererBinding(binding))
        {
            if (redirect.Target == null)
                return false; // cannot resolve a target; leave the binding untouched

            string targetPath = ComputePath(_root, redirect.Target);
            if (targetPath == null)
                return false;

            string newProperty = RemapProperty(binding.propertyName, redirect, out bool dropProperty);
            if (dropProperty)
            {
                drop = true;
                return true;
            }

            newBinding = new EditorCurveBinding
            {
                path = targetPath,
                type = binding.type,
                propertyName = newProperty,
            };
            return newBinding.path != binding.path || newBinding.propertyName != binding.propertyName;
        }

        // Simple path remap (object survived but moved).
        if (pathRemap.TryGetValue(binding.path, out string remappedPath) && remappedPath != binding.path)
        {
            newBinding = new EditorCurveBinding
            {
                path = remappedPath,
                type = binding.type,
                propertyName = binding.propertyName,
            };
            return true;
        }

        return false;
    }

    private static string RemapProperty(string propertyName, Redirect redirect, out bool drop)
    {
        drop = false;
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        // blendShape.<name>
        if (redirect.BlendShapeRename != null && propertyName.StartsWith("blendShape.", System.StringComparison.Ordinal))
        {
            string shapeName = propertyName.Substring("blendShape.".Length);
            if (redirect.BlendShapeRename.TryGetValue(shapeName, out string newName))
            {
                if (string.IsNullOrEmpty(newName))
                {
                    drop = true; // shape was frozen/removed
                    return propertyName;
                }
                return "blendShape." + newName;
            }
        }

        // m_Materials.Array.data[<index>]
        if (redirect.MaterialSlotRemap != null &&
            propertyName.StartsWith("m_Materials.Array.data[", System.StringComparison.Ordinal) &&
            propertyName.EndsWith("]", System.StringComparison.Ordinal))
        {
            int start = "m_Materials.Array.data[".Length;
            string indexText = propertyName.Substring(start, propertyName.Length - start - 1);
            if (int.TryParse(indexText, out int oldIndex) &&
                redirect.MaterialSlotRemap.TryGetValue(oldIndex, out int newIndex))
            {
                return "m_Materials.Array.data[" + newIndex + "]";
            }
        }

        return propertyName;
    }

    private static bool IsRendererBinding(EditorCurveBinding binding)
    {
        return binding.type != null && typeof(Renderer).IsAssignableFrom(binding.type);
    }

    private static string ComputePath(Transform root, Transform target)
    {
        if (target == null || root == null)
            return null;

        if (target == root)
            return "";

        var segments = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        if (current != root)
            return null; // target is not under root

        segments.Reverse();
        return string.Join("/", segments);
    }
}
#endif
