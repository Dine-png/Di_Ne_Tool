using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace DiNeTool.ExtraModifier.Editor
{
    internal sealed class DiNeVrmReport
    {
        public int MergedBones;
        public int SpringBones;
        public int SpringColliders;
        public int ConvertedConstraints;
        public int RemovedComponents;
        public int MissingScripts;
        public string Error;

        public bool Succeeded => string.IsNullOrEmpty(Error);

        public string Summary => Succeeded
            ? $"Bones {MergedBones} / SpringBones {SpringBones} / Constraints {ConvertedConstraints} / Removed {RemovedComponents}"
            : Error;

        public void Add(DiNeVrmReport other)
        {
            if (other == null)
                return;
            MergedBones += other.MergedBones;
            SpringBones += other.SpringBones;
            SpringColliders += other.SpringColliders;
            ConvertedConstraints += other.ConvertedConstraints;
            RemovedComponents += other.RemovedComponents;
            MissingScripts += other.MissingScripts;
            if (!string.IsNullOrEmpty(other.Error))
                Error = other.Error;
        }
    }

    /// <summary>
    /// Compact VRM preparation workflow inspired by VRM Supporter by Kuroiine Ushina.
    /// The implementation intentionally focuses on three automatic operations only.
    /// </summary>
    internal static class DiNeVrmUtility
    {
        private const string VrmCopySuffix = "_VRM";

        public static bool IsUniVrmAvailable => FindType("VRM.VRMSpringBone") != null;

        public static GameObject CreateWorkingCopy(GameObject source)
        {
            if (source == null)
                return null;

            var copy = UnityEngine.Object.Instantiate(source, source.transform.parent);
            copy.name = ObjectNames.GetUniqueName(
                source.transform.parent != null
                    ? source.transform.parent.Cast<Transform>().Select(child => child.name).ToArray()
                    : Array.Empty<string>(),
                source.name + VrmCopySuffix);
            Undo.RegisterCreatedObjectUndo(copy, "Create VRM working copy");
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Selection.activeGameObject = copy;
            EditorGUIUtility.PingObject(copy);
            return copy;
        }

        public static DiNeVrmReport RunAll(GameObject source, out GameObject workingCopy)
        {
            var report = new DiNeVrmReport();
            workingCopy = CreateWorkingCopy(source);
            if (workingCopy == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare avatar for VRM");
            try
            {
                var mergeReport = MergeOutfitBones(workingCopy);
                report.Add(mergeReport);
                if (!mergeReport.Succeeded)
                    return report;
                var springReport = ConvertPhysBones(workingCopy);
                report.Add(springReport);
                if (!springReport.Succeeded)
                    return report;
                report.Add(CleanupForVrm(workingCopy));
                EditorUtility.SetDirty(workingCopy);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            Debug.Log($"[DiNe VRM] Complete: {report.Summary}", workingCopy);
            return report;
        }

        public static DiNeVrmReport MergeOutfitBones(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            var mainBones = FindMainBones(avatarRoot);
            if (mainBones.Count == 0)
            {
                report.Error = "Could not find the main humanoid armature.";
                return report;
            }

            var mainSet = new HashSet<Transform>(mainBones.Values);
            var candidates = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(renderer => renderer.bones ?? Array.Empty<Transform>())
                .Where(bone => bone != null && !mainSet.Contains(bone))
                .Distinct()
                .OrderBy(GetDepth)
                .ToList();

            var nameCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var bone in candidates)
            {
                if (bone == null || !mainBones.TryGetValue(NormalizeBoneName(bone.name), out var target) || target == bone)
                    continue;

                var baseName = bone.name + "_VRM";
                if (!nameCounters.TryGetValue(baseName, out var index))
                    index = 0;
                nameCounters[baseName] = index + 1;

                Undo.SetTransformParent(bone, target, "Merge outfit bone for VRM");
                Undo.RecordObject(bone.gameObject, "Rename merged outfit bone");
                bone.name = index == 0 ? baseName : $"{baseName}_{index + 1}";
                report.MergedBones++;
            }

            EditorUtility.SetDirty(avatarRoot);
            Debug.Log($"[DiNe VRM] Merged {report.MergedBones} outfit bones.", avatarRoot);
            return report;
        }

        public static DiNeVrmReport ConvertPhysBones(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            var springBoneType = FindType("VRM.VRMSpringBone");
            var colliderGroupType = FindType("VRM.VRMSpringBoneColliderGroup");
            if (springBoneType == null || colliderGroupType == null)
            {
                report.Error = "UniVRM 0.x was not found. Install UniVRM before converting PhysBones.";
                return report;
            }

            var physBones = avatarRoot.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && IsPhysBone(component.GetType()))
                .ToList();
            if (physBones.Count == 0)
                return report;

            var secondary = avatarRoot.transform.Find("secondary");
            if (secondary == null)
            {
                var secondaryObject = new GameObject("secondary");
                Undo.RegisterCreatedObjectUndo(secondaryObject, "Create VRM secondary");
                Undo.SetTransformParent(secondaryObject.transform, avatarRoot.transform, "Create VRM secondary");
                secondaryObject.transform.localPosition = Vector3.zero;
                secondaryObject.transform.localRotation = Quaternion.identity;
                secondaryObject.transform.localScale = Vector3.one;
                secondary = secondaryObject.transform;
            }

            var colliderMap = new Dictionary<Component, Component>();
            foreach (var physBone in physBones)
            {
                var root = GetMember(physBone, "rootTransform") as Transform ?? physBone.transform;
                if (root == null)
                    continue;

                var springBone = Undo.AddComponent(secondary.gameObject, springBoneType);
                var springObject = new SerializedObject(springBone);
                if (!SetObjectArray(springObject, new[] { "m_roots", "RootBones" }, new UnityEngine.Object[] { root }))
                {
                    Undo.DestroyObjectImmediate(springBone);
                    continue;
                }

                var pull = ReadFloat(physBone, "pull", 0f);
                var stiffness = ReadFloat(physBone, "stiffness", 0f);
                var spring = ReadFloat(physBone, "spring", 0f);
                var radius = Mathf.Max(0.001f, ReadFloat(physBone, "radius", 0.02f));
                var gravity = ReadFloat(physBone, "gravity", 0f);
                var isHair = IsHair(root.name);

                SetFloat(springObject, new[] { "m_stiffnessForce", "StiffnessForce" },
                    Mathf.Clamp((pull + stiffness) * (isHair ? 2f : 1f), 0.02f, 4f));
                SetFloat(springObject, new[] { "m_dragForce", "DragForce" },
                    Mathf.Clamp(0.6f * (1f - spring), isHair ? 0.15f : 0.35f, 0.9f));
                SetFloat(springObject, new[] { "m_hitRadius", "HitRadius" }, Mathf.Clamp(radius, 0.001f, 0.5f));
                SetFloat(springObject, new[] { "m_gravityPower", "GravityPower" }, Mathf.Abs(gravity));
                SetVector(springObject, new[] { "m_gravityDir", "GravityDir" }, gravity < 0f ? Vector3.up : Vector3.down);

                var colliderGroups = new List<UnityEngine.Object>();
                foreach (var collider in ReadComponents(physBone, "colliders"))
                {
                    if (collider == null)
                        continue;
                    if (!colliderMap.TryGetValue(collider, out var group))
                    {
                        group = collider.gameObject.GetComponent(colliderGroupType) ?? Undo.AddComponent(collider.gameObject, colliderGroupType);
                        ApplyCollider(group, collider);
                        colliderMap.Add(collider, group);
                        report.SpringColliders++;
                    }
                    colliderGroups.Add(group);
                }
                SetObjectArray(springObject, new[] { "m_colliderGroups", "ColliderGroups" }, colliderGroups.ToArray());
                springObject.ApplyModifiedPropertiesWithoutUndo();

                Undo.DestroyObjectImmediate(physBone);
                report.SpringBones++;
            }

            foreach (var collider in colliderMap.Keys.Where(collider => collider != null).ToArray())
                Undo.DestroyObjectImmediate(collider);

            EditorUtility.SetDirty(avatarRoot);
            Debug.Log($"[DiNe VRM] Converted {report.SpringBones} PhysBones and {report.SpringColliders} colliders.", avatarRoot);
            return report;
        }

        public static DiNeVrmReport CleanupForVrm(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            report.ConvertedConstraints = ConvertVrcConstraints(avatarRoot);

            foreach (var animator in avatarRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator != null && animator.gameObject != avatarRoot)
                {
                    Undo.DestroyObjectImmediate(animator);
                    report.RemovedComponents++;
                }
            }

            var components = avatarRoot.GetComponentsInChildren<Component>(true).Reverse().ToArray();
            foreach (var component in components)
            {
                if (component == null || component is Transform || IsVrmCompatible(component))
                    continue;

                if (component is MonoBehaviour)
                {
                    Undo.DestroyObjectImmediate(component);
                    report.RemovedComponents++;
                }
            }

            foreach (var transform in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (count <= 0)
                    continue;
                report.MissingScripts += count;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }

            EditorUtility.SetDirty(avatarRoot);
            Debug.Log($"[DiNe VRM] Converted {report.ConvertedConstraints} constraints and removed {report.RemovedComponents} incompatible components.", avatarRoot);
            return report;
        }

        private static int ConvertVrcConstraints(GameObject root)
        {
            var converted = 0;
            var constraints = root.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && IsVrcConstraint(component.GetType()))
                .ToArray();

            foreach (var sourceConstraint in constraints)
            {
                var target = CreateUnityConstraint(sourceConstraint);
                if (target == null)
                    continue;

                target.weight = ReadFloat(sourceConstraint, "GlobalWeight", 1f);
                target.constraintActive = ReadBool(sourceConstraint, "IsActive", true);

                var sources = ReadConstraintSources(sourceConstraint).ToList();
                foreach (var source in sources)
                {
                    if (source.Transform == null)
                        continue;
                    var index = target.AddSource(new ConstraintSource
                    {
                        sourceTransform = source.Transform,
                        weight = source.Weight
                    });
                    if (target is ParentConstraint parent)
                    {
                        parent.SetTranslationOffset(index, source.PositionOffset);
                        parent.SetRotationOffset(index, source.RotationOffset);
                    }
                }

                ApplyConstraintSettings(sourceConstraint, target);
                Undo.DestroyObjectImmediate(sourceConstraint);
                converted++;
            }
            return converted;
        }

        private static IConstraint CreateUnityConstraint(Component source)
        {
            var name = source.GetType().Name;
            if (name.Contains("ParentConstraint")) return Undo.AddComponent<ParentConstraint>(source.gameObject);
            if (name.Contains("PositionConstraint")) return Undo.AddComponent<PositionConstraint>(source.gameObject);
            if (name.Contains("RotationConstraint")) return Undo.AddComponent<RotationConstraint>(source.gameObject);
            if (name.Contains("ScaleConstraint")) return Undo.AddComponent<ScaleConstraint>(source.gameObject);
            if (name.Contains("AimConstraint")) return Undo.AddComponent<AimConstraint>(source.gameObject);
            if (name.Contains("LookAtConstraint")) return Undo.AddComponent<LookAtConstraint>(source.gameObject);
            return null;
        }

        private static void ApplyConstraintSettings(Component source, IConstraint target)
        {
            if (target is PositionConstraint position)
            {
                position.translationAtRest = ReadVector(source, "PositionAtRest", position.translationAtRest);
                position.translationOffset = ReadVector(source, "PositionOffset", position.translationOffset);
                position.translationAxis = ReadAxes(source, "AffectsPosition");
            }
            else if (target is RotationConstraint rotation)
            {
                rotation.rotationAtRest = ReadVector(source, "RotationAtRest", rotation.rotationAtRest);
                rotation.rotationOffset = ReadVector(source, "RotationOffset", rotation.rotationOffset);
                rotation.rotationAxis = ReadAxes(source, "AffectsRotation");
            }
            else if (target is ScaleConstraint scale)
            {
                scale.scaleAtRest = ReadVector(source, "ScaleAtRest", scale.scaleAtRest);
                scale.scaleOffset = ReadVector(source, "ScaleOffset", scale.scaleOffset);
                scale.scalingAxis = ReadAxes(source, "AffectsScale");
            }
            else if (target is AimConstraint aim)
            {
                aim.aimVector = ReadVector(source, "AimAxis", aim.aimVector);
                aim.upVector = ReadVector(source, "UpAxis", aim.upVector);
                aim.worldUpVector = ReadVector(source, "WorldUpVector", aim.worldUpVector);
                aim.worldUpObject = GetMember(source, "WorldUpTransform") as Transform;
            }
            else if (target is LookAtConstraint lookAt)
            {
                lookAt.roll = ReadFloat(source, "Roll", lookAt.roll);
                lookAt.useUpObject = ReadBool(source, "UseUpTransform", lookAt.useUpObject);
                lookAt.worldUpObject = GetMember(source, "WorldUpTransform") as Transform;
            }

            var serialized = new SerializedObject((Component)target);
            var locked = serialized.FindProperty("m_IsLocked");
            if (locked != null)
                locked.boolValue = ReadBool(source, "Locked", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerable<ConstraintSourceData> ReadConstraintSources(Component constraint)
        {
            var raw = GetMember(constraint, "Sources");
            if (raw == null)
                yield break;

            foreach (var source in EnumerateValues(raw))
            {
                var transform = GetMember(source, "SourceTransform") as Transform;
                if (transform == null)
                    continue;
                yield return new ConstraintSourceData
                {
                    Transform = transform,
                    Weight = ReadFloat(source, "Weight", 1f),
                    PositionOffset = ReadVector(source, "ParentPositionOffset", Vector3.zero),
                    RotationOffset = ReadVector(source, "ParentRotationOffset", Vector3.zero)
                };
            }
        }

        private static IEnumerable<object> EnumerateValues(object value)
        {
            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    if (item != null)
                        yield return item;
                yield break;
            }

            var members = value.GetType()
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(member => member.Name.StartsWith("source", StringComparison.OrdinalIgnoreCase));
            foreach (var member in members)
            {
                var item = GetMemberValue(value, member);
                if (item != null)
                    yield return item;
            }
        }

        private static Dictionary<string, Transform> FindMainBones(GameObject root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var animator = root.GetComponent<Animator>();
            Transform hips = null;
            if (animator != null && animator.isHuman)
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            if (hips == null)
            {
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(renderer => renderer.name.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0 && renderer.rootBone != null);
                hips = body != null ? body.rootBone : null;
            }
            if (hips == null)
                hips = root.transform.Find("Armature/Hips") ?? root.transform.Find("Hips");
            if (hips == null)
                return result;

            foreach (var bone in hips.GetComponentsInChildren<Transform>(true).Prepend(hips))
            {
                var key = NormalizeBoneName(bone.name);
                if (!result.ContainsKey(key))
                    result.Add(key, bone);
            }
            return result;
        }

        private static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var colon = name.LastIndexOf(':');
            if (colon >= 0 && colon + 1 < name.Length)
                name = name.Substring(colon + 1);
            return name.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        private static bool IsVrmCompatible(Component component)
        {
            if (component is Animator)
                return true;
            if (component is Renderer || component is MeshFilter || component is Collider || component is Rigidbody ||
                component is Cloth || component is Light || component is Camera || component is ParticleSystem ||
                component is AudioSource || component is LODGroup || component is IConstraint)
                return true;

            var ns = component.GetType().Namespace ?? string.Empty;
            return ns == "VRM" || ns.StartsWith("VRM.") || ns.StartsWith("UniGLTF") || ns.StartsWith("VSeeFace");
        }

        private static bool IsPhysBone(Type type)
        {
            var name = type.FullName ?? type.Name;
            return name.Contains("VRCPhysBone") && !name.Contains("Collider");
        }

        private static bool IsVrcConstraint(Type type)
        {
            var name = type.FullName ?? type.Name;
            return name.Contains("VRC") && name.Contains("Constraint") && !name.Contains("ConstraintSource");
        }

        private static void ApplyCollider(Component target, Component source)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty("Colliders") ?? serialized.FindProperty("m_colliders");
            if (property == null || !property.isArray)
                return;
            property.ClearArray();
            property.InsertArrayElementAtIndex(0);
            var element = property.GetArrayElementAtIndex(0);
            var offset = element.FindPropertyRelative("Offset") ?? element.FindPropertyRelative("m_offset");
            var radius = element.FindPropertyRelative("Radius") ?? element.FindPropertyRelative("m_radius");
            if (offset != null) offset.vector3Value = ReadVector(source, "position", Vector3.zero);
            if (radius != null) radius.floatValue = Mathf.Max(0.001f, ReadFloat(source, "radius", 0.05f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool SetObjectArray(SerializedObject serialized, string[] names, UnityEngine.Object[] values)
        {
            var property = FindProperty(serialized, names);
            if (property == null || !property.isArray)
                return false;
            property.ClearArray();
            for (var i = 0; i < values.Length; i++)
            {
                property.InsertArrayElementAtIndex(i);
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            return true;
        }

        private static void SetFloat(SerializedObject serialized, string[] names, float value)
        {
            var property = FindProperty(serialized, names);
            if (property != null) property.floatValue = value;
        }

        private static void SetVector(SerializedObject serialized, string[] names, Vector3 value)
        {
            var property = FindProperty(serialized, names);
            if (property != null) property.vector3Value = value;
        }

        private static SerializedProperty FindProperty(SerializedObject serialized, IEnumerable<string> names)
        {
            return names.Select(serialized.FindProperty).FirstOrDefault(property => property != null);
        }

        private static IEnumerable<Component> ReadComponents(object target, string name)
        {
            var value = GetMember(target, name);
            if (!(value is IEnumerable enumerable))
                yield break;
            foreach (var item in enumerable)
                if (item is Component component)
                    yield return component;
        }

        private static object GetMember(object target, string name)
        {
            if (target == null)
                return null;
            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null)
                return property.GetValue(target);
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return field != null ? field.GetValue(target) : null;
        }

        private static object GetMemberValue(object target, MemberInfo member)
        {
            if (member is FieldInfo field) return field.GetValue(target);
            if (member is PropertyInfo property && property.GetIndexParameters().Length == 0) return property.GetValue(target);
            return null;
        }

        private static float ReadFloat(object target, string name, float fallback)
        {
            var value = GetMember(target, name);
            try { return value != null ? Convert.ToSingle(value) : fallback; }
            catch { return fallback; }
        }

        private static bool ReadBool(object target, string name, bool fallback)
        {
            var value = GetMember(target, name);
            try { return value != null ? Convert.ToBoolean(value) : fallback; }
            catch { return fallback; }
        }

        private static Vector3 ReadVector(object target, string name, Vector3 fallback)
        {
            return GetMember(target, name) is Vector3 value ? value : fallback;
        }

        private static Axis ReadAxes(object target, string prefix)
        {
            var axes = (Axis)0;
            if (ReadBool(target, prefix + "X", true)) axes |= Axis.X;
            if (ReadBool(target, prefix + "Y", true)) axes |= Axis.Y;
            if (ReadBool(target, prefix + "Z", true)) axes |= Axis.Z;
            return axes;
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            while (transform != null)
            {
                depth++;
                transform = transform.parent;
            }
            return depth;
        }

        private static bool IsHair(string name)
        {
            var lower = (name ?? string.Empty).ToLowerInvariant();
            return lower.Contains("hair") || lower.Contains("헤어") || lower.Contains("髪");
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

        private struct ConstraintSourceData
        {
            public Transform Transform;
            public float Weight;
            public Vector3 PositionOffset;
            public Vector3 RotationOffset;
        }
    }
}
