using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DiNeTool.ExtraModifier.Editor
{
    internal enum DiNeVrmIssueKind
    {
        /// <summary>UniVRM 자체가 없음.</summary>
        UniVrmMissing,
        /// <summary>휴머노이드 Animator가 없어 VRM으로 내보낼 수 없음.</summary>
        NoHumanoid,
        /// <summary>루트 트랜스폼이 원점/단위스케일이 아님.</summary>
        RootTransform,
        /// <summary>Missing script가 남아 있음.</summary>
        MissingScripts,
        /// <summary>PhysBone이 남아 있음.</summary>
        PhysBones,
        /// <summary>VRC/MA/NDMF 등 VRM 비호환 컴포넌트가 남아 있음.</summary>
        VrcComponents,
        /// <summary>UniVRM이 그대로 내보내지 못하는 셰이더.</summary>
        UnsupportedShaders,
        /// <summary>VRMMeta가 없음(내보내기 창에서 입력 가능).</summary>
        NoVrmMeta,
        /// <summary>SpringBone이 내보내기 루트 밖이거나 비활성인 오브젝트를 참조함.</summary>
        SpringBoneReferences
    }

    internal enum DiNeVrmIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    internal sealed class DiNeVrmIssue
    {
        public DiNeVrmIssueKind Kind;
        public DiNeVrmIssueSeverity Severity;
        public int Count;
        public string Detail;
    }

    /// <summary>
    /// UniVRM으로 넘기기 전에 우리가 대신 잡아 주는 사전 점검.
    /// UniVRM의 내보내기 마법사는 "왜 안 되는지"를 잘 알려주지 않기 때문에,
    /// 우리 기능으로 고칠 수 있는 항목만 골라서 먼저 보여 준다.
    /// </summary>
    internal static class DiNeVrmPreflight
    {
        /// <summary>UniVRM이 전용 익스포터를 가진 셰이더들. 나머지는 폴백 머티리얼로 나간다.</summary>
        private static readonly string[] ExportableShaders =
        {
            "VRM/MToon",
            "VRM10/MToon10",
            "Standard",
            "UniGLTF/UniUnlit",
            "Unlit/Color",
            "Unlit/Texture",
            "Unlit/Transparent",
            "Unlit/Transparent Cutout"
        };

        public static List<DiNeVrmIssue> Run(GameObject avatarRoot)
        {
            var issues = new List<DiNeVrmIssue>();
            if (avatarRoot == null)
                return issues;

            if (!DiNeUniVrmBridge.HasVrm0 && !DiNeUniVrmBridge.HasVrm1)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.UniVrmMissing,
                    Severity = DiNeVrmIssueSeverity.Error
                });
            }

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.NoHumanoid,
                    Severity = DiNeVrmIssueSeverity.Error
                });
            }

            var transform = avatarRoot.transform;
            if (transform.localPosition != Vector3.zero ||
                transform.localRotation != Quaternion.identity ||
                (transform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.RootTransform,
                    Severity = DiNeVrmIssueSeverity.Warning
                });
            }

            var missingScripts = avatarRoot.GetComponentsInChildren<Transform>(true)
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
            if (missingScripts > 0)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.MissingScripts,
                    Severity = DiNeVrmIssueSeverity.Warning,
                    Count = missingScripts
                });
            }

            var components = avatarRoot.GetComponentsInChildren<Component>(true)
                .Where(component => component != null)
                .ToArray();

            var physBones = components.Count(component => IsNamed(component, "VRCPhysBone"));
            if (physBones > 0)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.PhysBones,
                    Severity = DiNeVrmIssueSeverity.Warning,
                    Count = physBones
                });
            }

            // UniVRM 내보내기 검사에서 "is out of hierarchy" / "is not active"로 막히는 참조.
            var brokenSpringBones = DiNeVrmUtility.CountBrokenSpringBones(avatarRoot);
            if (brokenSpringBones > 0)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.SpringBoneReferences,
                    Severity = DiNeVrmIssueSeverity.Error,
                    Count = brokenSpringBones
                });
            }

            var incompatible = components.Count(IsIncompatible);
            if (incompatible > 0)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.VrcComponents,
                    Severity = DiNeVrmIssueSeverity.Warning,
                    Count = incompatible
                });
            }

            var unsupported = avatarRoot.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials ?? Array.Empty<Material>())
                .Where(material => material != null && material.shader != null)
                .Where(material => !ExportableShaders.Contains(material.shader.name))
                .Distinct()
                .ToArray();
            if (unsupported.Length > 0)
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.UnsupportedShaders,
                    Severity = DiNeVrmIssueSeverity.Warning,
                    Count = unsupported.Length,
                    Detail = string.Join(", ", unsupported.Take(3).Select(material => material.name)) +
                             (unsupported.Length > 3 ? " ..." : string.Empty)
                });
            }

            if (DiNeUniVrmBridge.HasVrm0 &&
                !components.Any(component => IsNamed(component, "VRMMeta")) &&
                !components.Any(component => IsNamed(component, "Vrm10Instance")))
            {
                issues.Add(new DiNeVrmIssue
                {
                    Kind = DiNeVrmIssueKind.NoVrmMeta,
                    Severity = DiNeVrmIssueSeverity.Info
                });
            }

            return issues;
        }

        private static bool IsNamed(Component component, string keyword)
        {
            var name = component.GetType().FullName ?? component.GetType().Name;
            return name.Contains(keyword);
        }

        /// <summary>VRM으로 나갈 수 없는(정리 대상) MonoBehaviour인지.</summary>
        private static bool IsIncompatible(Component component)
        {
            if (!(component is MonoBehaviour))
                return false;

            var type = component.GetType();
            var name = type.FullName ?? type.Name;
            if (name.Contains("VRCPhysBone"))
                return false; // PhysBone은 별도 항목으로 보고한다.

            var ns = type.Namespace ?? string.Empty;
            if (ns == "VRM" || ns.StartsWith("VRM.") || ns.StartsWith("UniVRM10") || ns.StartsWith("UniGLTF"))
                return false;

            return ns.StartsWith("VRC") ||
                   ns.StartsWith("nadena") ||
                   ns.StartsWith("modular_avatar") ||
                   name.Contains("VRC") ||
                   name.Contains("ModularAvatar");
        }
    }
}
