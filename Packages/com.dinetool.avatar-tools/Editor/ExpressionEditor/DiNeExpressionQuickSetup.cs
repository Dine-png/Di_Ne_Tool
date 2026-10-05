using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace DiNeTool.ExpressionEditor
{
    internal static class DiNeExpressionQuickSetup
    {
        [MenuItem("CONTEXT/VRCAvatarDescriptor/[Di Ne] Quick Setup", false, 650)]
        private static void QuickSetup(MenuCommand command) => Apply(command.context as VRCAvatarDescriptor);

        [MenuItem("CONTEXT/VRCAvatarDescriptor/[Di Ne] Quick Setup", true)]
        private static bool CanQuickSetup(MenuCommand command) => command.context is VRCAvatarDescriptor descriptor &&
            DiNeExpressionUtility.CanEditAsset(descriptor);

        internal static void Apply(VRCAvatarDescriptor descriptor)
        {
            if (!DiNeExpressionUtility.CanEditAsset(descriptor)) return;
            Undo.RecordObject(descriptor, "Di Ne Quick Setup");
            var animator = descriptor.GetComponent<Animator>();
            var transforms = descriptor.GetComponentsInChildren<Transform>(true);
            Transform Bone(HumanBodyBones bone, string name) => animator != null && animator.isHuman
                ? animator.GetBoneTransform(bone) : transforms.FirstOrDefault(t => string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase));
            var left = Bone(HumanBodyBones.LeftEye, "LeftEye");
            var right = Bone(HumanBodyBones.RightEye, "RightEye");
            if (left != null && left.parent != null) left = left.parent.Find("LeftEye") ?? left;
            if (right != null && right.parent != null) right = right.parent.Find("RightEye") ?? right;
            using (var serialized = new SerializedObject(descriptor))
            {
                serialized.Update();
                if (left != null && right != null)
                {
                    var view = descriptor.transform.InverseTransformPoint((left.position + right.position) * 0.5f);
                    if (Mathf.Abs(view.x) < 0.00001f) view.x = 0;
                    view.z = (view.z + view.y * 0.0547f) * 0.5f;
                    serialized.FindProperty("ViewPosition").vector3Value = view;
                    serialized.FindProperty("enableEyeLook").boolValue = true;
                    var eyes = serialized.FindProperty("customEyeLookSettings");
                    eyes.FindPropertyRelative("leftEye").objectReferenceValue = left;
                    eyes.FindPropertyRelative("rightEye").objectReferenceValue = right;
                    SetRotation(eyes, "eyesLookingUp", Quaternion.Euler(-15, 0, 0));
                    SetRotation(eyes, "eyesLookingDown", Quaternion.Euler(15, 0, 0));
                    SetRotation(eyes, "eyesLookingLeft", Quaternion.Euler(0, -15, 0));
                    SetRotation(eyes, "eyesLookingRight", Quaternion.Euler(0, 15, 0));
                    foreach (var renderer in descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer.sharedMesh == null) continue;
                        int blink = renderer.sharedMesh.GetBlendShapeIndex("Blink");
                        if (blink < 0) continue;
                        eyes.FindPropertyRelative("eyelidType").enumValueIndex = 2;
                        eyes.FindPropertyRelative("eyelidsSkinnedMesh").objectReferenceValue = renderer;
                        var shapes = eyes.FindPropertyRelative("eyelidsBlendshapes");
                        shapes.arraySize = 3;
                        for (int i = 0; i < 3; i++) shapes.GetArrayElementAtIndex(i).intValue = i == 0 ? blink : -1;
                        break;
                    }
                }
                else
                {
                    var head = Bone(HumanBodyBones.Head, "Head");
                    if (head != null)
                    {
                        var view = descriptor.transform.InverseTransformPoint(head.position);
                        view.y *= 1.0357f;
                        view.z = view.y * 0.0547f * 0.5f;
                        serialized.FindProperty("ViewPosition").vector3Value = view;
                    }
                }
                serialized.ApplyModifiedProperties();
            }

            // Reuse an already open SDK editor, never create one that can initialize avatar references.
            var sdkEditor = Resources.FindObjectsOfTypeAll<Editor>().FirstOrDefault(editor => editor != null &&
                editor.target == descriptor && editor.GetType().Assembly.GetName().Name == "VRC.SDK3A.Editor" &&
                editor.GetType().Name == "AvatarDescriptorEditor3");
            var detect = sdkEditor?.GetType().GetMethod("AutoDetectLipSync", BindingFlags.Instance | BindingFlags.NonPublic);
            if (detect != null)
            {
                try { detect.Invoke(sdkEditor, null); }
                catch (TargetInvocationException) { DetectLipSync(descriptor, Bone(HumanBodyBones.Jaw, "Jaw")); }
            }
            else DetectLipSync(descriptor, Bone(HumanBodyBones.Jaw, "Jaw"));
            EditorUtility.SetDirty(descriptor);
            if (PrefabUtility.IsPartOfPrefabInstance(descriptor)) PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
        }

        private static void SetRotation(SerializedProperty eyes, string field, Quaternion rotation)
        {
            var state = eyes.FindPropertyRelative(field);
            state.FindPropertyRelative("left").quaternionValue = rotation;
            state.FindPropertyRelative("right").quaternionValue = rotation;
        }

        private static void DetectLipSync(VRCAvatarDescriptor descriptor, Transform jaw)
        {
            string[] visemes = Enum.GetNames(typeof(VRC_AvatarDescriptor.Viseme)).Where(name => name != "Count").ToArray();
            foreach (var renderer in descriptor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0) continue;
                var names = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
                var matches = visemes.Select(viseme => FindViseme(names, "v_" + viseme, viseme) ?? FindViseme(names, viseme, viseme)).ToArray();
                descriptor.VisemeSkinnedMesh = renderer;
                descriptor.lipSyncJawBone = null;
                descriptor.lipSync = mesh.blendShapeCount >= visemes.Length && matches.Count(name => name != null) > 2
                    ? VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape : VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                if (descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape) descriptor.VisemeBlendShapes = matches;
                return;
            }
            if (jaw == null) return;
            descriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone;
            descriptor.lipSyncJawBone = jaw;
            descriptor.VisemeSkinnedMesh = null;
        }

        private static string FindViseme(string[] names, string search, string viseme)
        {
            return names.Where(name => name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(name => name.Length - name.LastIndexOf(viseme, StringComparison.OrdinalIgnoreCase) - viseme.Length)
                .ThenBy(name => new string(name.Reverse().ToArray()), StringComparer.InvariantCultureIgnoreCase).FirstOrDefault();
        }
    }
}
