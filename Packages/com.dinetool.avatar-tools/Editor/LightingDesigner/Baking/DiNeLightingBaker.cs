#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

/// <summary>
/// 머티리얼 정규화 진입점.
///
/// 반드시 IVRCSDKPreprocessAvatarCallback.OnPreprocessAvatar에서만 호출한다.
/// 그 콜백이 받는 GameObject는 SDK가 업로드용으로 만든 '빌드 클론'이라서
/// 렌더러의 sharedMaterials를 갈아끼워도 씬의 원본 아바타는 그대로다.
/// (NDMF/Modular Avatar도 같은 전제로 이 지점에서 파괴적 변경을 한다.)
/// 절대 OnBuildRequested 쪽에서 부르면 안 된다 — 거기서는 씬 원본을 망가뜨린다.
/// </summary>
public static class DiNeLightingBaker
{
    public static void NormalizeForBuild(GameObject avatarGameObject)
    {
        if (avatarGameObject == null)
            return;

        var descriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>()
            ?? avatarGameObject.GetComponentInChildren<VRCAvatarDescriptor>(true);
        if (descriptor == null)
            return;

        var designer = avatarGameObject.GetComponentsInChildren<DiNeLightingDesigner>(true)
            .FirstOrDefault(item => item != null && item.enabled);
        if (designer == null)
            return;

        designer.EnsureDefaults();

        var controls = new HashSet<DiNeLightingControl>(
            designer.Controls.Where(setting => setting != null && setting.enabled).Select(setting => setting.control));

        if (controls.Count == 0)
            return;

        // 어느 프로파일도 손댈 게 없으면 머티리얼을 복제하지 않는다.
        if (!DiNeShaderProfile.All.Any(profile => profile.RequiresMaterialPreparation(controls)))
            return;

        var excluded = new HashSet<Renderer>(designer.Excludes.Where(item => item != null));
        var renderers = avatarGameObject.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
            .Where(renderer => !excluded.Contains(renderer))
            .ToList();

        DiNeLightingBakeSession session = null;
        int normalizedCount = 0;

        try
        {
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                Material[] replacement = null;

                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || original.shader == null) continue;

                    var profile = DiNeShaderProfile.All.FirstOrDefault(item =>
                        (designer.TargetShaders & item.Flag) != 0 && item.IsTarget(original.shader));
                    if (profile == null) continue;
                    if (!profile.RequiresMaterialPreparation(controls)) continue;

                    if (session == null)
                        session = new DiNeLightingBakeSession();

                    var clone = session.GetOrCloneMaterial(original);
                    if (clone == null) continue;

                    // 같은 머티리얼이 여러 렌더러에 붙어 있으면 복제본은 하나뿐이므로,
                    // 준비/정규화도 처음 한 번만 돌린다.
                    if (session.MarkPrepared(clone))
                    {
                        profile.PrepareMaterial(clone, controls);
                        if (profile.NormalizeMaterial(clone, controls, session))
                            normalizedCount++;
                    }

                    if (replacement == null)
                        replacement = (Material[])materials.Clone();
                    replacement[i] = clone;
                }

                if (replacement != null)
                    renderer.sharedMaterials = replacement;
            }

            if (session != null)
            {
                session.Save();
                Debug.Log(
                    $"[DiNe 라이팅 디자이너] 머티리얼 정규화 완료: 복제 {session.ClonedMaterialCount}개, 베이킹 {normalizedCount}개.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe 라이팅 디자이너] 머티리얼 정규화 중 예외: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>빌드/업로드가 끝났을 때 임시 머티리얼·텍스처를 정리한다.</summary>
    public static void CleanupTempAssets() => DiNeLightingBakeSession.CleanupTempAssets();
}
#endif
