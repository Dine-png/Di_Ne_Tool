using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DiNeTool.ExtraModifier.Editor
{
    /// <summary>UniVRM이 제공하는 기능 중 우리가 대신 호출해 줄 항목.</summary>
    internal enum DiNeUniVrmAction
    {
        /// <summary>UniVRM 0.x의 T-Pose 고정(본 정규화).</summary>
        FreezeTPose,
        /// <summary>UniVRM 0.x MeshUtility(메시 통합, 본 제거 등) 창.</summary>
        MeshUtility,
        /// <summary>VRM 0.x 내보내기 마법사.</summary>
        ExportVrm0,
        /// <summary>VRM 1.0 내보내기 다이얼로그.</summary>
        ExportVrm1,
        /// <summary>VRM Converter for VRChat(써드파티)의 VRChat→VRM 내보내기.</summary>
        VrmConverterForVrChat
    }

    /// <summary>
    /// UniVRM(과 주변 툴)을 asmdef 참조 없이 간접 호출하는 다리.
    /// UniVRM이 잘하는 일(T-Pose 고정, 메시 유틸, 내보내기 마법사, 메타 입력)은 그대로 UniVRM에 맡기고,
    /// UniVRM이 하지 않는 일(PhysBone→SpringBone, VRC 컴포넌트 정리, MToon 머티리얼 변환)만 우리가 채운다.
    /// </summary>
    internal static class DiNeUniVrmBridge
    {
        /// <summary>UniVRM 0.x(VRM 0) 설치 여부.</summary>
        public static bool HasVrm0 => FindType("VRM.VRMSpringBone") != null;

        /// <summary>UniVRM 1.x(VRM 1.0) 설치 여부.</summary>
        public static bool HasVrm1 => FindType("UniVRM10.Vrm10Instance") != null;

        public static bool HasVrmConverterForVrChat =>
            FindType("Esperecyan.Unity.VRMConverterForVRChat.UI.Menu") != null ||
            FindType("Esperecyan.Unity.VRMConverterForVRChat.Converter") != null;

        public static bool IsAvailable(DiNeUniVrmAction action)
        {
            switch (action)
            {
                case DiNeUniVrmAction.FreezeTPose:
                case DiNeUniVrmAction.MeshUtility:
                case DiNeUniVrmAction.ExportVrm0:
                    return HasVrm0;
                case DiNeUniVrmAction.ExportVrm1:
                    return HasVrm1;
                case DiNeUniVrmAction.VrmConverterForVrChat:
                    return HasVrmConverterForVrChat;
                default:
                    return false;
            }
        }

        /// <summary>설치된 VRM 관련 패키지를 사람이 읽을 수 있게 요약한다.</summary>
        public static string DescribeInstallation()
        {
            var parts = new List<string>();
            if (HasVrm0)
                parts.Add("UniVRM 0.x" + FormatVersion("VRM.VRMVersion", "VERSION"));
            if (HasVrm1)
                parts.Add("VRM 1.0" + FormatVersion("UniVRM10.PackageVersion", "VERSION"));
            if (HasVrmConverterForVrChat)
                parts.Add("VRM Converter for VRChat");
            return parts.Count == 0 ? string.Empty : string.Join(" / ", parts);
        }

        /// <summary>
        /// UniVRM 기능을 실행한다. 우선 공개 API를 리플렉션으로 호출하고,
        /// 버전 차이로 API를 못 찾으면 메뉴 항목 실행으로 물러난다.
        /// </summary>
        public static bool Invoke(DiNeUniVrmAction action, GameObject target, out string message)
        {
            switch (action)
            {
                case DiNeUniVrmAction.FreezeTPose:
                    return InvokeFreezeTPose(target, out message);

                case DiNeUniVrmAction.MeshUtility:
                    return InvokeAny(out message,
                        () => TryInvokeStatic("VRM.VrmMeshIntegratorWizard", "OpenWindow"),
                        () => TryExecuteMenu("VRM0/VRM 0.x MeshUtility", "VRM0/MeshUtility", "VRM0/Mesh Utility"));

                case DiNeUniVrmAction.ExportVrm0:
                    Select(target);
                    return InvokeAny(out message,
                        () => TryInvokeStatic("VRM.VRMExporterWizard", "OpenExportMenu"),
                        () => TryExecuteMenu("VRM0/Export VRM 0.x...", "VRM0/Export VRM", "VRM/UniVRM-0.xx/Export"));

                case DiNeUniVrmAction.ExportVrm1:
                    Select(target);
                    return InvokeAny(out message,
                        () => TryInvokeStatic("UniVRM10.Vrm10ExportDialog", "OpenExportMenu"),
                        () => TryInvokeStatic("UniVRM10.Vrm10ExportDialog", "Open"),
                        () => TryExecuteMenu("VRM1/Export VRM-1.0", "VRM1/Export VRM 1.0", "VRM1/Export VRM-1.0..."));

                case DiNeUniVrmAction.VrmConverterForVrChat:
                    Select(target);
                    return InvokeAny(out message,
                        () => TryExecuteMenu("VRM0/Export VRM file from VRChat avatar",
                                             "VRM0/Duplicate and Convert for VRChat"));

                default:
                    message = "Unknown action.";
                    return false;
            }
        }

        private static bool InvokeFreezeTPose(GameObject target, out string message)
        {
            if (target == null)
            {
                message = "Avatar is not assigned.";
                return false;
            }

            var animator = target.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                message = "Freeze T-Pose needs a humanoid Animator on the avatar root.";
                return false;
            }

            // VRM.VRMBoneNormalizer.Execute(GameObject, bool forceTPose)
            if (TryInvokeStatic("VRM.VRMBoneNormalizer", "Execute", new object[] { target, true }))
            {
                EditorUtility.SetDirty(target);
                message = string.Empty;
                return true;
            }

            Select(target);
            if (TryExecuteMenu("VRM0/VRM 0.x Freeze T-Pose", "VRM0/Freeze T-Pose"))
            {
                message = string.Empty;
                return true;
            }

            message = "UniVRM's Freeze T-Pose could not be called. Check the UniVRM version.";
            return false;
        }

        private static bool InvokeAny(out string message, params Func<bool>[] attempts)
        {
            foreach (var attempt in attempts)
            {
                try
                {
                    if (attempt())
                    {
                        message = string.Empty;
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    message = exception.Message;
                    Debug.LogWarning($"[DiNe VRM] UniVRM call failed: {exception}");
                    return false;
                }
            }

            message = "The matching UniVRM menu was not found. Check the installed UniVRM version.";
            return false;
        }

        private static void Select(GameObject target)
        {
            // UniVRM의 내보내기 창은 열릴 때 Selection을 Export Root로 잡는다.
            if (target == null)
                return;
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static bool TryInvokeStatic(string typeName, string methodName, object[] args = null)
        {
            var type = FindType(typeName);
            var method = type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate => candidate.Name == methodName &&
                                             candidate.GetParameters().Length == (args?.Length ?? 0));
            if (method == null)
                return false;

            method.Invoke(null, args);
            return true;
        }

        private static bool TryExecuteMenu(params string[] paths)
        {
            return paths.Any(EditorApplication.ExecuteMenuItem);
        }

        private static string FormatVersion(string typeName, string memberName)
        {
            var type = FindType(typeName);
            if (type == null)
                return string.Empty;

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
            var value = field != null
                ? field.GetValue(null)
                : type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return value != null ? $" ({value})" : string.Empty;
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
