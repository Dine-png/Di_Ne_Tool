#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiNeTool.ExtraModifier.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// All components below are resolved from real installed SDK assemblies copied
// into the isolated project. No test substitute defines an SDK component/type.
public static class ExtraModifierTransplantSdkRegression
{
    private const string PhysBone = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
    private const string PhysCollider = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider";
    private const string MergeArmature = "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature";
    private const string ScaleAdjuster = "nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster";
    private const string Descriptor = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
    private static readonly string[] ConstraintNames = { "VRCParentConstraint", "VRCPositionConstraint", "VRCRotationConstraint", "VRCScaleConstraint", "VRCAimConstraint", "VRCLookAtConstraint" };

    public static void RegisterTests(Action<string, Action> test)
    {
        // NDMF's editor PlatformRegistry normally registers this exact SDK root
        // type. The isolated project intentionally loads runtime assemblies only,
        // so reproduce that registry setup, then use the real RuntimeUtil/Get.
        var runtimeUtil = Resolve("nadena.dev.ndmf.runtime.RuntimeUtil");
        var roots = (HashSet<Type>)runtimeUtil.GetField("AllRootTypes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        roots.Add(Resolve(Descriptor));
        test("Real VRC PhysBone and collider values/remapped lists reach the exact second target with Undo", PhysBoneReferences);
        test("Real VRC PhysBone prefab source stays byte-identical while scene target receives its configuration", PhysBonePrefabSource);
        foreach (var name in ConstraintNames)
        {
            string constraintName = name;
            test("Real " + name + " copies native inline/overflow Sources and destination transforms", () => ConstraintReferences(constraintName));
        }
        test("Real MA MergeArmature path-only reference resolves the revised outfit with lock configuration retained", () => AvatarObjectPaths("path"));
        test("Real MA empty AvatarObjectReference remains disabled despite its stored object", () => AvatarObjectPaths("empty"));
        test("Real MA stale outside-avatar direct object yields to its valid internal path", () => AvatarObjectPaths("stale"));
        test("Real MA external avatar reference retains its path and actual outside-outfit object", () => AvatarObjectPaths("external"));
        test("Real MA no-avatar reference keeps a revised direct object and diagnoses its path context", () => AvatarObjectPaths("no-avatar"));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static Component Add(GameObject host, string typeName)
    {
        var type = Resolve(typeName);
        Require(type.Assembly.GetName().Name != "Assembly-CSharp", "SDK component resolved to a test substitute");
        return host.AddComponent(type);
    }

    private static Component Get(GameObject host, string typeName)
    {
        var component = host.GetComponent(Resolve(typeName));
        Require(component != null, "Missing copied real SDK component: " + typeName);
        return component;
    }

    private static SerializedProperty Property(SerializedObject serialized, string path)
    {
        var property = serialized.FindProperty(path);
        Require(property != null, "Installed SDK schema lacks property: " + path);
        return property;
    }

    private static void Write(Component component, Action<SerializedObject> action)
    {
        using (var serialized = new SerializedObject(component))
        {
            action(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static string Snapshot(GameObject root)
    {
        var lines = new List<string>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            lines.Add(transform.gameObject.GetInstanceID() + ":" + EditorJsonUtility.ToJson(transform.gameObject));
            foreach (var component in transform.GetComponents<Component>())
                lines.Add(component == null ? "Missing Script" : component.GetInstanceID() + ":" + EditorJsonUtility.ToJson(component));
        }
        return string.Join("\n", lines);
    }

    private static DiNePrefabTransplantUtility.Report Execute(GameObject source, GameObject target)
    {
        string sourceBefore = Snapshot(source), name = target.name;
        var targetStates = ExtraModifierTransplantRegression.CaptureExistingTarget(target);
        var parent = target.transform.parent;
        int id = target.GetInstanceID(), sibling = target.transform.GetSiblingIndex(), roots = target.scene.rootCount;
        bool active = target.activeSelf;
        var report = DiNePrefabTransplantUtility.Execute(source, target);
        Require(report.Succeeded, report.Error);
        Require(report.ResultRoot == target && target.GetInstanceID() == id && target != source,
            "Real SDK copy did not mutate the exact passed second target");
        Require(target.name == name && target.transform.parent == parent && target.transform.GetSiblingIndex() == sibling && target.activeSelf == active && target.scene.rootCount == roots,
            "SDK copy changed target root identity or created a clone");
        Require(Snapshot(source) == sourceBefore, "Real SDK source serialized data or hierarchy changed");
        ExtraModifierTransplantRegression.RequireExistingTargetUnchanged(targetStates);
        return report;
    }

    private static void References(SerializedObject serialized, string path, params Object[] values)
    {
        var array = Property(serialized, path);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void AssertReference(Component component, string path, Object expected)
    {
        using (var serialized = new SerializedObject(component))
            Require(Property(serialized, path).objectReferenceValue == expected, "Incorrect actual SDK destination reference: " + path);
    }

    private static void AssertFloat(Component component, string path, float expected)
    {
        using (var serialized = new SerializedObject(component))
            Require(Mathf.Abs(Property(serialized, path).floatValue - expected) < .000001f, "Incorrect actual SDK float: " + path);
    }

    private static void ConfigureCollider(Component collider, Transform root, float radius)
    {
        Write(collider, serialized =>
        {
            Property(serialized, "rootTransform").objectReferenceValue = root;
            Property(serialized, "radius").floatValue = radius;
            Property(serialized, "height").floatValue = 1.37f;
            Property(serialized, "insideBounds").boolValue = true;
            Property(serialized, "position").vector3Value = new Vector3(.12f, .23f, .34f);
            Property(serialized, "rotation").quaternionValue = Quaternion.Euler(11, 27, 43);
        });
    }

    private static void PhysBoneReferences()
    {
        var source = new GameObject("OldOutfit"); var target = new GameObject("RevisedOutfit");
        var sourceBone = Child(source, "Bone"); var targetBone = Child(target, "Bone");
        target.transform.localPosition = new Vector3(4, 5, 6); target.transform.localRotation = Quaternion.Euler(17, 29, 43); target.transform.localScale = new Vector3(1.7f, .8f, 1.2f);
        targetBone.transform.localPosition = new Vector3(7, 8, 9); targetBone.transform.localRotation = Quaternion.Euler(11, 23, 31); targetBone.transform.localScale = new Vector3(.9f, 1.1f, 1.3f);
        var sourceTip = Child(sourceBone, "Tip"); var targetTip = Child(targetBone, "Tip");
        var sourceA = Add(Child(source, "CreatedCollider"), PhysCollider);
        var sourceB = Add(Child(source, "ExistingCollider"), PhysCollider);
        var targetB = Add(Child(target, "ExistingCollider"), PhysCollider);
        var external = Add(new GameObject("ExternalCollider"), PhysCollider);
        var removedHost = Child(source, "RemovedGeometry"); removedHost.AddComponent<MeshRenderer>();
        var removedCollider = Add(removedHost, PhysCollider);
        ConfigureCollider(sourceA, sourceBone.transform, .41f);
        ExtraModifierTransplantRegression.WriteRawRotation(sourceA.transform, new Quaternion(.3933636f, .2968083f, .42958885f, -.75671893f));
        ConfigureCollider(sourceB, sourceTip.transform, .52f);
        var sourcePb = Add(sourceBone, PhysBone); var targetPb = Add(targetBone, PhysBone);
        Write(targetPb, s => Property(s, "pull").floatValue = .01f);
        Write(sourcePb, s =>
        {
            Property(s, "rootTransform").objectReferenceValue = sourceBone.transform;
            References(s, "ignoreTransforms", sourceTip.transform, external.transform);
            References(s, "colliders", sourceA, sourceB, external, removedCollider);
            Property(s, "pull").floatValue = .73f;
            Property(s, "spring").floatValue = .39f;
            Property(s, "radius").floatValue = .17f;
            Property(s, "parameter").stringValue = "TransplantRegression";
            Property(s, "endpointPosition").vector3Value = new Vector3(.2f, .4f, .6f);
            Property(s, "pullCurve").animationCurveValue = AnimationCurve.Linear(0, .21f, 1, .86f);
        });
        string targetBefore = Snapshot(target), sourceBefore = Snapshot(source);
        var report = Execute(source, target);
        var targetA = Get(target.transform.Find("CreatedCollider").gameObject, PhysCollider);
        Require(Get(targetBone, PhysBone) == targetPb, "Existing second-target PB was not reused");
        AssertFloat(targetPb, "pull", .73f); AssertFloat(targetPb, "spring", .39f); AssertFloat(targetPb, "radius", .17f);
        AssertReference(targetPb, "rootTransform", targetBone.transform);
        AssertReference(targetA, "rootTransform", targetBone.transform); AssertFloat(targetA, "radius", .41f);
        AssertReference(targetB, "rootTransform", targetTip.transform); AssertFloat(targetB, "radius", .52f);
        using (var s = new SerializedObject(targetPb))
        {
            var colliders = Property(s, "colliders"); var ignores = Property(s, "ignoreTransforms");
            Require(colliders.arraySize == 4 && colliders.GetArrayElementAtIndex(0).objectReferenceValue == targetA && colliders.GetArrayElementAtIndex(1).objectReferenceValue == targetB && colliders.GetArrayElementAtIndex(2).objectReferenceValue == external && colliders.GetArrayElementAtIndex(3).objectReferenceValue == null, "Actual PB collider list did not remap/create/retain/clear all four entries");
            Require(ignores.arraySize == 2 && ignores.GetArrayElementAtIndex(0).objectReferenceValue == targetTip.transform && ignores.GetArrayElementAtIndex(1).objectReferenceValue == external.transform, "Actual PB ignoreTransforms list is incorrect");
            Require(Property(s, "parameter").stringValue == "TransplantRegression" && Mathf.Abs(Property(s, "pullCurve").animationCurveValue.Evaluate(.5f) - .535f) < .00001f, "PB string/curve configuration not copied");
        }
        Require(target.transform.Find("RemovedGeometry") == null && report.CopiedComponents >= 3, "PB fixture copied no components or resurrected old geometry");
        Undo.PerformUndo();
        Require(target != null && Snapshot(target) == targetBefore && Snapshot(source) == sourceBefore, "Real PB Undo failed to restore exact destination/source snapshots");
        Undo.PerformRedo();
        AssertFloat(Get(targetBone, PhysBone), "pull", .73f);
        AssertReference(Get(targetBone, PhysBone), "rootTransform", targetBone.transform);
        Require(Snapshot(source) == sourceBefore, "Real PB Redo changed source");
    }

    private static void PhysBonePrefabSource()
    {
        var original = new GameObject("OriginalPrefab"); var bone = Child(original, "Bone");
        var collider = Add(Child(original, "Collider"), PhysCollider); ConfigureCollider(collider, bone.transform, .63f);
        var pb = Add(bone, PhysBone);
        Write(pb, s => { Property(s, "rootTransform").objectReferenceValue = bone.transform; Property(s, "pull").floatValue = .82f; References(s, "colliders", collider); });
        string folder = "Assets/SdkPrefab_" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string path = folder + "/Original.prefab";
        var asset = PrefabUtility.SaveAsPrefabAsset(original, path); Object.DestroyImmediate(original);
        byte[] before = File.ReadAllBytes(path); string snapshot = Snapshot(asset);
        var target = new GameObject("EditedSceneInstance"); var targetBone = Child(target, "Bone");
        Execute(asset, target);
        AssertFloat(Get(targetBone, PhysBone), "pull", .82f);
        var targetCollider = Get(target.transform.Find("Collider").gameObject, PhysCollider);
        AssertReference(Get(targetBone, PhysBone), "rootTransform", targetBone.transform);
        using (var s = new SerializedObject(Get(targetBone, PhysBone))) Require(Property(s, "colliders").GetArrayElementAtIndex(0).objectReferenceValue == targetCollider, "Prefab PB collider still points to source asset");
        AssetDatabase.SaveAssets();
        Require(before.SequenceEqual(File.ReadAllBytes(path)) && Snapshot(asset) == snapshot, "Real SDK prefab source bytes or serialized source changed");
    }

    private static void ConstraintReferences(string shortName)
    {
        string typeName = "VRC.SDK3.Dynamics.Constraint.Components." + shortName;
        var source = new GameObject("OldOutfit"); var target = new GameObject("RevisedOutfit");
        var a = Child(source, "BoneA"); var b = Child(source, "BoneB"); var ta = Child(target, "BoneA"); var tb = Child(target, "BoneB");
        target.transform.localPosition = new Vector3(4, 7, 9); target.transform.localRotation = Quaternion.Euler(11, 23, 37); target.transform.localScale = new Vector3(1.4f, .8f, 1.7f);
        ta.transform.localPosition = new Vector3(2, 3, 5); ta.transform.localRotation = Quaternion.Euler(19, 31, 43); ta.transform.localScale = new Vector3(.7f, 1.3f, 1.6f);
        tb.transform.localPosition = new Vector3(6, 8, 10);
        var external = new GameObject("ExternalConstraintSource");
        var original = Add(source, typeName); var existing = Add(target, typeName);
        Write(original, s =>
        {
            Property(s, "IsActive").boolValue = false;
            Property(s, "GlobalWeight").floatValue = .37f;
            Property(s, "TargetTransform").objectReferenceValue = a.transform;
            Property(s, "SolveInLocalSpace").boolValue = true;
            Property(s, "Sources.totalLength").intValue = 18;
            Property(s, "Sources.overflowList").arraySize = 2;
            for (int i = 0; i < 18; i++)
            {
                var slot = i < 16 ? Property(s, "Sources.source" + i) : Property(s, "Sources.overflowList").GetArrayElementAtIndex(i - 16);
                slot.FindPropertyRelative("SourceTransform").objectReferenceValue = i == 17 ? external.transform : i % 2 == 0 ? a.transform : b.transform;
                slot.FindPropertyRelative("Weight").floatValue = .1f + i * .03f;
                slot.FindPropertyRelative("ParentPositionOffset").vector3Value = new Vector3(i, i + 1, i + 2);
                slot.FindPropertyRelative("ParentRotationOffset").vector3Value = new Vector3(i + 3, i + 4, i + 5);
            }
            if (s.FindProperty("WorldUpTransform") != null) Property(s, "WorldUpTransform").objectReferenceValue = b.transform;
            foreach (string field in new[] { "PositionAtRest", "RotationAtRest", "ScaleAtRest", "PositionOffset", "RotationOffset", "ScaleOffset" })
                if (s.FindProperty(field) != null) Property(s, field).vector3Value = new Vector3(1.3f, 2.4f, 3.5f);
        });
        Execute(source, target);
        Require(Get(target, typeName) == existing, "Existing actual VRC constraint was not reused");
        AssertReference(existing, "TargetTransform", ta.transform); AssertFloat(existing, "GlobalWeight", .37f);
        using (var s = new SerializedObject(existing))
        {
            Require(!Property(s, "IsActive").boolValue && Property(s, "SolveInLocalSpace").boolValue && Property(s, "Sources.totalLength").intValue == 18 && Property(s, "Sources.overflowList").arraySize == 2, "Real VRC constraint active/local/count configuration was not copied");
            for (int i = 0; i < 18; i++)
            {
                var slot = i < 16 ? Property(s, "Sources.source" + i) : Property(s, "Sources.overflowList").GetArrayElementAtIndex(i - 16);
                Object expected = i == 17 ? external.transform : i % 2 == 0 ? ta.transform : tb.transform;
                Require(slot.FindPropertyRelative("SourceTransform").objectReferenceValue == expected && Mathf.Abs(slot.FindPropertyRelative("Weight").floatValue - (.1f + i * .03f)) < .000001f && slot.FindPropertyRelative("ParentPositionOffset").vector3Value == new Vector3(i, i + 1, i + 2) && slot.FindPropertyRelative("ParentRotationOffset").vector3Value == new Vector3(i + 3, i + 4, i + 5), "Incorrect actual " + shortName + " inline/overflow source " + i);
            }
            if (s.FindProperty("WorldUpTransform") != null) Require(Property(s, "WorldUpTransform").objectReferenceValue == tb.transform, "VRC WorldUpTransform points to source");
            foreach (string field in new[] { "PositionAtRest", "RotationAtRest", "ScaleAtRest", "PositionOffset", "RotationOffset", "ScaleOffset" })
                if (s.FindProperty(field) != null) Require(Property(s, field).vector3Value == new Vector3(1.3f, 2.4f, 3.5f), "Real VRC constraint native value changed: " + field);
        }
        object list = existing.GetType().GetField("Sources").GetValue(existing);
        Require((int)list.GetType().GetProperty("Count").GetValue(list) == 18, "Actual VRC Sources API disagrees with its copied serialized length");
        object last = list.GetType().GetProperty("Item").GetValue(list, new object[] { 16 });
        Require((Object)last.GetType().GetField("SourceTransform").GetValue(last) == ta.transform, "Actual VRC Sources API retained source refs in overflow");
    }

    private static void AvatarObjectPaths(string mode)
    {
        GameObject avatar = null;
        if (mode != "no-avatar") { avatar = new GameObject("Avatar"); Add(avatar, Descriptor); }
        var source = avatar == null ? new GameObject("OldOutfit") : Child(avatar, "OldOutfit");
        var target = avatar == null ? new GameObject("NewOutfit") : Child(avatar, "NewOutfit");
        var sourceBone = Child(source, "Bone"); var targetBone = Child(target, "Bone");
        target.transform.localPosition = new Vector3(3, 5, 7); target.transform.localRotation = Quaternion.Euler(13, 29, 41); target.transform.localScale = new Vector3(1.5f, .8f, 1.2f);
        targetBone.transform.localPosition = new Vector3(11, 17, 19); targetBone.transform.localRotation = Quaternion.Euler(7, 23, 31); targetBone.transform.localScale = new Vector3(.9f, 1.1f, 1.3f);
        var external = avatar == null ? new GameObject("ExternalBone") : Child(avatar, "BaseBone");
        var outside = new GameObject("OutsideAvatar");
        var merge = Add(source, MergeArmature);
        Write(merge, s =>
        {
            Property(s, "mergeTarget.referencePath").stringValue = mode == "empty" ? string.Empty : mode == "external" ? "BaseBone" : "OldOutfit/Bone";
            Property(s, "mergeTarget.targetObject").objectReferenceValue = mode == "empty" ? sourceBone : mode == "stale" ? outside : mode == "external" ? external : null;
            Property(s, "LockMode").enumValueIndex = 3; // Actual ArmatureLockMode.BidirectionalExact.
            Property(s, "prefix").stringValue = "pre_"; Property(s, "suffix").stringValue = "_post";
            Property(s, "mangleNames").boolValue = false;
        });
        // Run the actual source runtime lock configuration, rather than merely
        // assigning an enum on a marker type. Prefix/suffix avoid unintended
        // bone-pair motion in this serialization-focused fixture.
        merge.GetType().GetMethod("SetLockMode", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(merge, null);
        var scale = Add(sourceBone, ScaleAdjuster);
        Write(scale, s => Property(s, "m_Scale").vector3Value = new Vector3(1.2f, .8f, 1.4f));
        var report = Execute(source, target); var copied = Get(target, MergeArmature);
        using (var s = new SerializedObject(copied))
        {
            string path = Property(s, "mergeTarget.referencePath").stringValue;
            var direct = Property(s, "mergeTarget.targetObject").objectReferenceValue;
            Require(Property(s, "LockMode").enumValueIndex == 3 && Property(s, "prefix").stringValue == "pre_" && Property(s, "suffix").stringValue == "_post" && !Property(s, "mangleNames").boolValue && ((Behaviour)copied).enabled, "Actual MA copied configuration was silently disabled or changed");
            if (mode == "empty") Require(path == string.Empty && direct == targetBone, "Disabled MA reference was activated or generic direct object failed remap");
            else if (mode == "external") Require(path == "BaseBone" && direct == external, "External avatar reference was incorrectly rebound");
            else Require(path == "NewOutfit/Bone" && direct == targetBone, "Real MA internal reference still resolves the old outfit or stale direct pointer");
        }
        var resolved = copied.GetType().GetProperty("mergeTargetObject").GetValue(copied) as GameObject;
        Require(resolved == (mode == "empty" || mode == "no-avatar" ? null : mode == "external" ? external : targetBone), "Actual MA Get resolved the wrong source/destination object");
        using (var s = new SerializedObject(Get(targetBone, ScaleAdjuster))) Require(Property(s, "m_Scale").vector3Value == new Vector3(1.2f, .8f, 1.4f), "Actual private MA scale configuration not copied");
        if (mode == "no-avatar") Require(report.Diagnostics.Any(d => d.Code == "AvatarObjectPathOutsideAvatar"), "Missing no-avatar context diagnostic");
        if (mode == "path" || mode == "stale") Require(report.Diagnostics.Any(d => d.Code == "AvatarObjectPathRemapped"), "Missing actual path-rebind diagnostic");
    }

    private static Type Resolve(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name, false);
            if (type != null) return type;
        }
        throw new InvalidOperationException("Real installed SDK type unavailable: " + name);
    }

    public static void DescribeSdk()
    {
        var lines = new List<string>();
        try
        {
            foreach (var name in new[] { PhysBone, PhysCollider, MergeArmature, ScaleAdjuster, Descriptor }.Concat(ConstraintNames.Select(n => "VRC.SDK3.Dynamics.Constraint.Components." + n)))
            {
                var type = Resolve(name); var host = new GameObject("SDK Inspection"); var component = host.AddComponent(type);
                lines.Add("TYPE " + type.FullName + " [" + type.Assembly.GetName().Name + "]");
                using (var serialized = new SerializedObject(component))
                {
                    var iterator = serialized.GetIterator(); bool children = true;
                    while (iterator.Next(children))
                    {
                        children = iterator.propertyType != SerializedPropertyType.String;
                        lines.Add("PROP " + iterator.propertyPath + " type=" + iterator.propertyType + " class=" + iterator.type + " array=" + iterator.isArray);
                    }
                }
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (field.Name.IndexOf("Source", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lines.Add("FIELD " + field.Name + " " + field.FieldType.FullName);
                        foreach (var method in field.FieldType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) lines.Add("SOURCE METHOD " + method);
                    }
                Object.DestroyImmediate(host);
            }
            File.WriteAllLines("ExtraModifierTransplantSdkRegression-description.txt", lines);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            lines.Add("ERROR " + error); File.WriteAllLines("ExtraModifierTransplantSdkRegression-description.txt", lines); EditorApplication.Exit(1);
        }
    }
}
#endif
