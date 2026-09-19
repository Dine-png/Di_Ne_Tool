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
    /// <summary>PhysBone을 어떻게 처리할지.</summary>
    internal enum DiNeVrmPhysBoneMode
    {
        /// <summary>UniVRM SpringBone으로 변환한다.</summary>
        Convert,
        /// <summary>변환하지 않고 삭제한다.</summary>
        Delete,
        /// <summary>손대지 않는다. (직접 정리할 때)</summary>
        Keep
    }

    internal sealed class DiNeVrmReport
    {
        public int MergedBones;
        public int SpringBones;
        public int SpringColliders;
        public int SkippedColliders;
        /// <summary>내보내기 대상이 아니라 변환하지 않은 PhysBone(비활성 오브젝트 등).</summary>
        public int SkippedSpringBones;
        /// <summary>참조를 정리한 SpringBone.</summary>
        public int RepairedSpringBones;
        /// <summary>쓸 수 있는 RootBone이 남지 않아 제거한 SpringBone.</summary>
        public int RemovedSpringBones;
        /// <summary>아무 역할이 없어 제거한 빈 오브젝트.</summary>
        public int RemovedEmptyObjects;
        public int RemovedPhysBones;
        public int KeptPhysBones;
        public int ConvertedConstraints;
        public int RemovedComponents;
        public int MissingScripts;
        public int ConvertedMaterials;
        public int KeptNormalMaps;
        public int KeptMatcaps;
        public int KeptEmissions;
        public int KeptOutlines;
        public int KeptRims;
        public int DroppedMaterialFeatures;
        public string Warning;
        public string Error;

        public bool Succeeded => string.IsNullOrEmpty(Error);

        public string Summary => Succeeded
            ? $"Bones {MergedBones} / SpringBones {SpringBones} / Constraints {ConvertedConstraints} / Materials {ConvertedMaterials} / Removed {RemovedComponents}" +
              (SkippedSpringBones > 0 ? $" / Skipped {SkippedSpringBones}" : string.Empty) +
              (RepairedSpringBones > 0 || RemovedSpringBones > 0
                  ? $" / Repaired {RepairedSpringBones}+{RemovedSpringBones}"
                  : string.Empty) +
              (RemovedEmptyObjects > 0 ? $" / Empty {RemovedEmptyObjects}" : string.Empty)
            : Error;

        public void Add(DiNeVrmReport other)
        {
            if (other == null)
                return;
            MergedBones += other.MergedBones;
            SpringBones += other.SpringBones;
            SpringColliders += other.SpringColliders;
            SkippedColliders += other.SkippedColliders;
            SkippedSpringBones += other.SkippedSpringBones;
            RemovedEmptyObjects += other.RemovedEmptyObjects;
            RepairedSpringBones += other.RepairedSpringBones;
            RemovedSpringBones += other.RemovedSpringBones;
            RemovedPhysBones += other.RemovedPhysBones;
            KeptPhysBones += other.KeptPhysBones;
            ConvertedConstraints += other.ConvertedConstraints;
            RemovedComponents += other.RemovedComponents;
            MissingScripts += other.MissingScripts;
            ConvertedMaterials += other.ConvertedMaterials;
            KeptNormalMaps += other.KeptNormalMaps;
            KeptMatcaps += other.KeptMatcaps;
            KeptEmissions += other.KeptEmissions;
            KeptOutlines += other.KeptOutlines;
            KeptRims += other.KeptRims;
            DroppedMaterialFeatures += other.DroppedMaterialFeatures;
            if (!string.IsNullOrEmpty(other.Warning))
                Warning = other.Warning;
            if (!string.IsNullOrEmpty(other.Error))
                Error = other.Error;
        }
    }

    /// <summary>전체 자동 처리에서 사용할 설정.</summary>
    internal struct DiNeVrmOptions
    {
        public DiNeVrmPhysBoneMode PhysBoneMode;
        public bool ConvertMaterials;
        public DiNeVrmMaterialOptions MaterialOptions;
        /// <summary>마지막에 UniVRM의 Freeze T-Pose(본 정규화)를 실행할지.</summary>
        public bool FreezeTPose;
        /// <summary>정리가 끝난 뒤 남은 빈 오브젝트를 지울지.</summary>
        public bool RemoveEmptyObjects;

        public static DiNeVrmOptions Default => new DiNeVrmOptions
        {
            PhysBoneMode = DiNeVrmPhysBoneMode.Convert,
            ConvertMaterials = true,
            MaterialOptions = DiNeVrmMaterialOptions.Default,
            FreezeTPose = false,
            RemoveEmptyObjects = false
        };
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
            return RunAll(source, DiNeVrmOptions.Default, out workingCopy);
        }

        public static DiNeVrmReport RunAll(GameObject source, DiNeVrmOptions options, out GameObject workingCopy)
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

                var physBoneReport = ProcessPhysBones(workingCopy, options.PhysBoneMode);
                report.Add(physBoneReport);
                if (!physBoneReport.Succeeded)
                    return report;

                if (options.ConvertMaterials)
                {
                    var materialReport = DiNeVrmMaterialConverter.ConvertToMToon(workingCopy, options.MaterialOptions);
                    report.Add(materialReport);
                    if (!materialReport.Succeeded)
                        return report;
                }

                report.Add(CleanupForVrm(workingCopy));

                // 컴포넌트를 다 걷어낸 뒤에 해야 "이제서야 비게 된" 오브젝트까지 잡힌다.
                if (options.RemoveEmptyObjects)
                    report.Add(RemoveEmptyObjects(workingCopy));

                // Freeze T-Pose는 반드시 마지막에. UniVRM의 본 정규화는 VRM 컴포넌트만 이해하므로
                // PhysBone이 남은 상태에서 돌리면 참조가 끊긴다.
                if (options.FreezeTPose && !DiNeUniVrmBridge.Invoke(DiNeUniVrmAction.FreezeTPose, workingCopy, out var freezeMessage))
                    report.Warning = freezeMessage;

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

        /// <summary>선택한 방식(변환/삭제/유지)대로 PhysBone을 처리한다.</summary>
        public static DiNeVrmReport ProcessPhysBones(GameObject avatarRoot, DiNeVrmPhysBoneMode mode)
        {
            switch (mode)
            {
                case DiNeVrmPhysBoneMode.Delete:
                    return RemovePhysBones(avatarRoot);
                case DiNeVrmPhysBoneMode.Keep:
                    return CountPhysBones(avatarRoot);
                default:
                    return ConvertPhysBones(avatarRoot);
            }
        }

        /// <summary>변환하지 않고 PhysBone과 PhysBoneCollider만 제거한다.</summary>
        public static DiNeVrmReport RemovePhysBones(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            foreach (var component in CollectPhysBoneComponents(avatarRoot))
            {
                Undo.DestroyObjectImmediate(component);
                report.RemovedPhysBones++;
            }

            EditorUtility.SetDirty(avatarRoot);
            Debug.Log($"[DiNe VRM] Removed {report.RemovedPhysBones} PhysBone components without converting.", avatarRoot);
            return report;
        }

        private static DiNeVrmReport CountPhysBones(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }
            report.KeptPhysBones = CollectPhysBoneComponents(avatarRoot).Count;
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

            // PhysBoneCollider는 rootTransform 기준의 로컬 좌표를 쓰므로,
            // 같은 호스트 트랜스폼에 붙는 콜라이더들을 하나의 ColliderGroup으로 모은다.
            var groupByHost = new Dictionary<Transform, Component>();
            var groupByCollider = new Dictionary<Component, Component>();
            var sphereByHost = new Dictionary<Transform, List<VrmSphere>>();
            var usedColliders = new List<Component>();

            foreach (var physBone in physBones)
            {
                foreach (var collider in ReadComponents(physBone, "colliders"))
                {
                    if (collider == null || groupByCollider.ContainsKey(collider))
                        continue;

                    var host = GetMember(collider, "rootTransform") as Transform ?? collider.transform;
                    // UniVRM은 내보내기 루트 밖이거나 비활성인 트랜스폼을 내보내지 않는다.
                    // 그런 곳에 ColliderGroup을 만들면 내보내기에서 "is out of hierarchy"로 막힌다.
                    if (!IsExportTarget(host, avatarRoot.transform))
                    {
                        report.SkippedColliders++;
                        continue;
                    }

                    var spheres = ToSpheres(collider, host, out var skipped);
                    if (skipped)
                        report.SkippedColliders++;
                    if (spheres.Count == 0)
                        continue;

                    if (!groupByHost.TryGetValue(host, out var group))
                    {
                        group = host.GetComponent(colliderGroupType) ?? Undo.AddComponent(host.gameObject, colliderGroupType);
                        groupByHost.Add(host, group);
                        sphereByHost.Add(host, new List<VrmSphere>());
                        report.SpringColliders++;
                    }

                    sphereByHost[host].AddRange(spheres);
                    groupByCollider.Add(collider, group);
                    usedColliders.Add(collider);
                }
            }

            foreach (var pair in groupByHost)
                WriteColliderGroup(pair.Value, sphereByHost[pair.Key]);

            foreach (var physBone in physBones)
            {
                var root = GetMember(physBone, "rootTransform") as Transform ?? physBone.transform;
                if (root == null)
                    continue;

                // 비활성 오브젝트(꺼 둔 의상 등)는 VRM으로 내보내지지 않으므로 SpringBone도 만들지 않는다.
                // 만들어 두면 UniVRM 내보내기에서 "RootBones[0] is not active"로 막힌다.
                if (!IsExportTarget(root, avatarRoot.transform))
                {
                    report.SkippedSpringBones++;
                    continue;
                }

                var springBone = Undo.AddComponent(secondary.gameObject, springBoneType);
                var springObject = new SerializedObject(springBone);
                if (!SetObjectArray(springObject, new[] { "RootBones", "m_roots" }, new UnityEngine.Object[] { root }))
                {
                    Undo.DestroyObjectImmediate(springBone);
                    continue;
                }

                ApplySpringParameters(springObject, physBone);

                var colliderGroups = ReadComponents(physBone, "colliders")
                    .Where(collider => collider != null && groupByCollider.ContainsKey(collider))
                    .Select(collider => (UnityEngine.Object)groupByCollider[collider])
                    .Distinct()
                    .ToArray();
                SetObjectArray(springObject, new[] { "ColliderGroups", "m_colliderGroups" }, colliderGroups);
                SetString(springObject, new[] { "m_comment", "Comment" }, physBone.gameObject.name);
                springObject.ApplyModifiedPropertiesWithoutUndo();

                Undo.DestroyObjectImmediate(physBone);
                report.SpringBones++;
            }

            // 참조가 남지 않도록 변환이 끝난 뒤에 원본 콜라이더를 제거한다.
            foreach (var collider in usedColliders.Where(collider => collider != null).Distinct().ToArray())
                Undo.DestroyObjectImmediate(collider);
            foreach (var collider in CollectPhysBoneComponents(avatarRoot))
                Undo.DestroyObjectImmediate(collider);

            // 예전 변환이 남긴 잘못된 참조까지 여기서 함께 정리한다.
            report.Add(RepairSpringBones(avatarRoot));

            EditorUtility.SetDirty(avatarRoot);
            Debug.Log($"[DiNe VRM] Converted {report.SpringBones} PhysBones and {report.SpringColliders} collider groups " +
                      $"(unsupported colliders: {report.SkippedColliders}, " +
                      $"skipped inactive/out-of-hierarchy PhysBones: {report.SkippedSpringBones}).", avatarRoot);
            return report;
        }

        /// <summary>
        /// UniVRM이 내보낼 수 있는 트랜스폼인지. 내보내기 루트의 자손이면서 비활성이 아니어야 한다.
        /// UniVRM 0.x의 내보내기 검사는 이 조건을 벗어난 참조를
        /// "is out of hierarchy" / "is not active" 오류로 막는다.
        /// </summary>
        private static bool IsExportTarget(Transform transform, Transform exportRoot)
        {
            if (transform == null || exportRoot == null || !transform.IsChildOf(exportRoot))
                return false;

            // 루트 자신이 씬에서 꺼져 있어도 작업은 할 수 있어야 하므로 루트까지만 확인한다.
            for (var current = transform; current != null && current != exportRoot; current = current.parent)
                if (!current.gameObject.activeSelf)
                    return false;
            return true;
        }

        /// <summary>
        /// 이미 만들어진 UniVRM 0.x SpringBone에서 내보낼 수 없는 참조를 걷어낸다.
        /// 쓸 수 있는 RootBone이 하나도 남지 않으면 그 SpringBone 자체를 지운다.
        /// </summary>
        public static DiNeVrmReport RepairSpringBones(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            var springBoneType = FindType("VRM.VRMSpringBone");
            if (springBoneType == null)
            {
                report.Error = "UniVRM 0.x was not found.";
                return report;
            }

            var exportRoot = avatarRoot.transform;
            foreach (var springBone in CollectSpringBones(avatarRoot, springBoneType))
            {
                var serialized = new SerializedObject(springBone);
                var roots = FindProperty(serialized, new[] { "RootBones", "m_roots" });
                var groups = FindProperty(serialized, new[] { "ColliderGroups", "m_colliderGroups" });

                var validRoots = CollectValidReferences(roots, exportRoot, out var removedRoots);
                var validGroups = CollectValidReferences(groups, exportRoot, out var removedGroups);

                if (validRoots.Count == 0)
                {
                    Undo.DestroyObjectImmediate(springBone);
                    report.RemovedSpringBones++;
                    continue;
                }

                if (removedRoots == 0 && removedGroups == 0)
                    continue;

                SetObjectArray(serialized, new[] { "RootBones", "m_roots" }, validRoots.ToArray());
                SetObjectArray(serialized, new[] { "ColliderGroups", "m_colliderGroups" }, validGroups.ToArray());
                serialized.ApplyModifiedPropertiesWithoutUndo();
                report.RepairedSpringBones++;
            }

            if (report.RepairedSpringBones > 0 || report.RemovedSpringBones > 0)
            {
                EditorUtility.SetDirty(avatarRoot);
                Debug.Log($"[DiNe VRM] Repaired {report.RepairedSpringBones} SpringBones and " +
                          $"removed {report.RemovedSpringBones} unusable SpringBones.", avatarRoot);
            }
            return report;
        }

        /// <summary>
        /// 아무 역할도 없는 빈 오브젝트를 지운다.
        /// Transform 말고는 컴포넌트가 없고, 자식도 없고, 휴머노이드 본도 아니고,
        /// 어떤 컴포넌트도 참조하지 않는 오브젝트만 대상이다. 안쪽부터 지우므로 빈 사슬도 함께 사라진다.
        /// </summary>
        public static DiNeVrmReport RemoveEmptyObjects(GameObject avatarRoot)
        {
            var report = new DiNeVrmReport();
            if (avatarRoot == null)
            {
                report.Error = "Avatar is not assigned.";
                return report;
            }

            var keep = CollectReferencedTransforms(avatarRoot);
            keep.Add(avatarRoot.transform);
            foreach (var bone in CollectHumanoidBones(avatarRoot))
                keep.Add(bone);

            var removedNames = new List<string>();
            // 깊은 곳부터 확인해야 "빈 오브젝트만 담고 있던 부모"까지 연쇄로 정리된다.
            foreach (var transform in avatarRoot.GetComponentsInChildren<Transform>(true)
                         .OrderByDescending(GetDepth)
                         .ToArray())
            {
                if (transform == null || transform == avatarRoot.transform)
                    continue;
                if (transform.childCount > 0 || keep.Contains(transform))
                    continue;
                if (transform.GetComponents<Component>().Length > 1)
                    continue; // Transform 외의 컴포넌트가 있으면 역할이 있는 오브젝트다.

                removedNames.Add(transform.name);
                Undo.DestroyObjectImmediate(transform.gameObject);
                report.RemovedEmptyObjects++;
            }

            if (report.RemovedEmptyObjects > 0)
            {
                EditorUtility.SetDirty(avatarRoot);
                Debug.Log($"[DiNe VRM] Removed {report.RemovedEmptyObjects} empty objects: " +
                          string.Join(", ", removedNames.Take(20)) +
                          (removedNames.Count > 20 ? " ..." : string.Empty), avatarRoot);
            }
            return report;
        }

        /// <summary>지우면 안 되는(어딘가에서 참조 중인) 트랜스폼을 모은다.</summary>
        private static HashSet<Transform> CollectReferencedTransforms(GameObject avatarRoot)
        {
            var referenced = new HashSet<Transform>();
            foreach (var component in avatarRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                    continue;

                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var value = property.objectReferenceValue;
                    var transform = value is GameObject gameObject
                        ? gameObject.transform
                        : (value as Component)?.transform;
                    if (transform != null)
                        referenced.Add(transform);
                }
            }
            return referenced;
        }

        /// <summary>Animator가 휴머노이드로 쓰고 있는 본. 비어 보여도 지우면 리그가 깨진다.</summary>
        private static IEnumerable<Transform> CollectHumanoidBones(GameObject avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                yield break;

            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                    continue;
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                    yield return transform;
            }
        }

        /// <summary>내보내기에서 막힐 참조를 가진 SpringBone 수를 센다. (사전 점검용)</summary>
        public static int CountBrokenSpringBones(GameObject avatarRoot)
        {
            var springBoneType = avatarRoot != null ? FindType("VRM.VRMSpringBone") : null;
            if (springBoneType == null)
                return 0;

            var exportRoot = avatarRoot.transform;
            var broken = 0;
            foreach (var springBone in CollectSpringBones(avatarRoot, springBoneType))
            {
                var serialized = new SerializedObject(springBone);
                CollectValidReferences(FindProperty(serialized, new[] { "RootBones", "m_roots" }), exportRoot, out var removedRoots);
                CollectValidReferences(FindProperty(serialized, new[] { "ColliderGroups", "m_colliderGroups" }), exportRoot, out var removedGroups);
                if (removedRoots > 0 || removedGroups > 0)
                    broken++;
            }
            return broken;
        }

        private static List<Component> CollectSpringBones(GameObject avatarRoot, Type springBoneType)
        {
            return avatarRoot.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && springBoneType.IsInstanceOfType(component))
                .ToList();
        }

        /// <summary>배열 프로퍼티에서 내보낼 수 있는 참조만 골라낸다.</summary>
        private static List<UnityEngine.Object> CollectValidReferences(
            SerializedProperty property, Transform exportRoot, out int removed)
        {
            var valid = new List<UnityEngine.Object>();
            removed = 0;
            if (property == null || !property.isArray)
                return valid;

            for (var i = 0; i < property.arraySize; i++)
            {
                var value = property.GetArrayElementAtIndex(i).objectReferenceValue;
                var transform = value is Component component ? component.transform
                    : value as Transform;
                if (transform != null && IsExportTarget(transform, exportRoot))
                    valid.Add(value);
                else
                    removed++;
            }
            return valid;
        }

        /// <summary>VRC PhysBone 값을 UniVRM SpringBone 파라미터로 옮긴다.</summary>
        private static void ApplySpringParameters(SerializedObject springObject, Component physBone)
        {
            var pull = ReadFloat(physBone, "pull", 0.2f);
            var stiffness = ReadFloat(physBone, "stiffness", 0.2f);
            var spring = ReadFloat(physBone, "spring", 0.2f);
            var immobile = ReadFloat(physBone, "immobile", 0f);
            var radius = Mathf.Max(0.001f, ReadFloat(physBone, "radius", 0.02f));
            var gravity = ReadFloat(physBone, "gravity", 0f);
            var gravityFalloff = ReadFloat(physBone, "gravityFalloff", 0f);

            // pull(원위치로 당기는 힘)과 stiffness(형태 유지)를 합쳐 VRM의 복원력으로 쓴다.
            var stiffnessForce = Mathf.Clamp(pull * 4f + stiffness * 2f, 0.05f, 4f);
            // spring이 클수록 잘 튀므로 감쇠(Drag)는 작아진다. immobile은 감쇠로 근사한다.
            var dragForce = Mathf.Clamp01(Mathf.Lerp(0.7f, 0.05f, Mathf.Clamp01(spring)) + immobile * 0.2f);

            SetFloat(springObject, new[] { "m_stiffnessForce", "StiffnessForce" }, stiffnessForce);
            SetFloat(springObject, new[] { "m_dragForce", "DragForce" }, dragForce);
            SetFloat(springObject, new[] { "m_hitRadius", "HitRadius" }, Mathf.Clamp(radius, 0.001f, 0.5f));
            SetFloat(springObject, new[] { "m_gravityPower", "GravityPower" },
                Mathf.Abs(gravity) * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(gravityFalloff)));
            SetVector(springObject, new[] { "m_gravityDir", "GravityDir" }, gravity < 0f ? Vector3.up : Vector3.down);
        }

        private static List<Component> CollectPhysBoneComponents(GameObject avatarRoot)
        {
            return avatarRoot.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && IsPhysBoneRelated(component.GetType()))
                .ToList();
        }

        /// <summary>VRC 콜라이더 하나를 VRM이 쓰는 구(Sphere) 목록으로 근사한다.</summary>
        private static List<VrmSphere> ToSpheres(Component collider, Transform host, out bool skipped)
        {
            skipped = false;
            var result = new List<VrmSphere>();
            if (host == null)
                return result;

            var offset = ReadVector(collider, "position", Vector3.zero);
            var rotation = GetMember(collider, "rotation") is Quaternion value ? value : Quaternion.identity;
            var radius = Mathf.Max(0.001f, ReadFloat(collider, "radius", 0.05f));
            var height = ReadFloat(collider, "height", 0f);
            var shape = ReadShapeType(collider);

            // VRC 콜라이더의 position은 rootTransform(없으면 자기 트랜스폼) 기준 로컬 좌표라
            // host를 그 트랜스폼으로 잡아 두면 VRM ColliderGroup의 Offset과 좌표계가 일치한다.
            switch (shape)
            {
                case ColliderShape.Plane:
                    // VRM 0.x SpringBone에는 평면 콜라이더가 없다.
                    skipped = true;
                    return result;
                case ColliderShape.Capsule:
                {
                    var span = Mathf.Max(0f, height - radius * 2f);
                    if (span <= 0.0001f)
                    {
                        result.Add(new VrmSphere { Offset = offset, Radius = radius });
                        return result;
                    }

                    var axis = (rotation * Vector3.up).normalized;
                    var steps = Mathf.Clamp(Mathf.CeilToInt(span / radius) + 1, 2, 6);
                    for (var i = 0; i < steps; i++)
                    {
                        var t = steps == 1 ? 0.5f : i / (float)(steps - 1);
                        var position = offset + axis * Mathf.Lerp(-span * 0.5f, span * 0.5f, t);
                        result.Add(new VrmSphere { Offset = position, Radius = radius });
                    }
                    return result;
                }
                default:
                    result.Add(new VrmSphere { Offset = offset, Radius = radius });
                    return result;
            }
        }

        private static ColliderShape ReadShapeType(Component collider)
        {
            var value = GetMember(collider, "shapeType");
            if (value == null)
                return ColliderShape.Sphere;

            var name = value.ToString();
            if (name.IndexOf("Capsule", StringComparison.OrdinalIgnoreCase) >= 0)
                return ColliderShape.Capsule;
            if (name.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0)
                return ColliderShape.Plane;
            return ColliderShape.Sphere;
        }

        private static void WriteColliderGroup(Component group, List<VrmSphere> spheres)
        {
            if (group == null || spheres == null || spheres.Count == 0)
                return;

            var serialized = new SerializedObject(group);
            var property = serialized.FindProperty("Colliders") ?? serialized.FindProperty("m_colliders");
            if (property == null || !property.isArray)
                return;

            property.ClearArray();
            for (var i = 0; i < spheres.Count; i++)
            {
                property.InsertArrayElementAtIndex(i);
                var element = property.GetArrayElementAtIndex(i);
                var offset = element.FindPropertyRelative("Offset") ?? element.FindPropertyRelative("m_offset");
                var radius = element.FindPropertyRelative("Radius") ?? element.FindPropertyRelative("m_radius");
                if (offset != null) offset.vector3Value = spheres[i].Offset;
                if (radius != null) radius.floatValue = Mathf.Max(0.001f, spheres[i].Radius);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private enum ColliderShape
        {
            Sphere,
            Capsule,
            Plane
        }

        private struct VrmSphere
        {
            public Vector3 Offset;
            public float Radius;
        }

        public static DiNeVrmReport CleanupForVrm(GameObject avatarRoot)
        {
            return CleanupForVrm(avatarRoot, true);
        }

        /// <param name="keepPhysBones">
        /// true면 PhysBone과 콜라이더는 건드리지 않는다. 변환/삭제는 전용 단계에서만 처리해야
        /// "변환한 줄 알았는데 그냥 사라지는" 일이 생기지 않는다.
        /// </param>
        public static DiNeVrmReport CleanupForVrm(GameObject avatarRoot, bool keepPhysBones)
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

                // PhysBone은 전용 단계(ProcessPhysBones)에서만 다룬다. 여기서 지우면
                // 변환한 줄 알았는데 그냥 사라지는 문제가 생긴다.
                if (keepPhysBones && IsPhysBoneRelated(component.GetType()))
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

        /// <summary>PhysBone 본체와 콜라이더. 정리 단계가 임의로 지우면 안 되는 대상이다.</summary>
        private static bool IsPhysBoneRelated(Type type)
        {
            var name = type.FullName ?? type.Name;
            return name.Contains("VRCPhysBone");
        }

        private static bool IsVrcConstraint(Type type)
        {
            var name = type.FullName ?? type.Name;
            return name.Contains("VRC") && name.Contains("Constraint") && !name.Contains("ConstraintSource");
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

        private static void SetString(SerializedObject serialized, string[] names, string value)
        {
            var property = FindProperty(serialized, names);
            if (property != null && property.propertyType == SerializedPropertyType.String)
                property.stringValue = value;
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
