#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

/// <summary>
/// 업로드 빌드에서 DiNeLightingDesigner 설정을 미리 붙잡아 두는 훅.
///
/// DiNeLightingDesigner는 IEditorOnly라서 SDK 버전이나 다른 빌드 훅(RemoveAvatarEditorOnly,
/// NDMF 계열 최적화 툴 등)에 따라 우리 정규화(callbackOrder 0)보다 먼저 빌드 클론에서
/// 지워질 수 있다. 머티리얼 정규화는 NDMF/Modular Avatar가 머티리얼을 갈아끼운 뒤에 돌아야
/// 하므로 순서를 앞당길 수는 없다. 그래서 가장 먼저 필요한 설정만 스냅샷으로 떼어 두고,
/// 나중에 클론에 디자이너가 남아 있지 않으면 그 스냅샷으로 정규화한다.
/// </summary>
internal sealed class DiNeLightingDesignerCaptureHook : IVRCSDKPreprocessAvatarCallback
{
    // NDMF 본 처리(-11000)가 EditorOnly 태그 오브젝트를 지우기 전에, 그리고 SDK의 IEditorOnly
    // 제거(-1024)보다 먼저. 디자이너 오브젝트가 EditorOnly 태그를 달고 있어도 설정을 건질 수 있다.
    public int callbackOrder => -12000;

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        try
        {
            DiNeLightingBaker.CaptureForBuild(avatarGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe 라이팅 디자이너] 빌드 스냅샷 캡처 중 예외: {e.Message}\n{e.StackTrace}");
        }

        return true;
    }
}

/// <summary>
/// 머티리얼 정규화 진입점.
///
/// 빌드 전처리 또는 플레이 모드 진입 뒤의 임시 아바타에서만 호출한다.
/// 두 경로가 받는 GameObject는 SDK/Unity가 만든 클론이라서
/// 렌더러의 sharedMaterials를 갈아끼워도 씬의 원본 아바타는 그대로다.
/// (NDMF/Modular Avatar도 같은 전제로 이 지점에서 파괴적 변경을 한다.)
/// 절대 OnBuildRequested 쪽에서 부르면 안 된다 — 거기서는 씬 원본을 망가뜨린다.
/// </summary>
public static class DiNeLightingBaker
{
    /// <summary>IEditorOnly 제거 전에 떼어 둔 디자이너 설정.</summary>
    private sealed class Snapshot
    {
        public HashSet<DiNeLightingControl> Controls;
        public DiNeLightingTargetShaders TargetShaders;
        public HashSet<Renderer> Excluded;
    }

    // 빌드 클론 루트 → 스냅샷. 클론은 빌드마다 새로 만들어지므로 참조로 구분해도 충분하다.
    private static readonly Dictionary<GameObject, Snapshot> PendingSnapshots = new Dictionary<GameObject, Snapshot>();

    /// <summary>
    /// SDK가 IEditorOnly 컴포넌트를 지우기 전에 호출해 라이팅 디자이너 설정을 붙잡아 둔다.
    /// 디자이너가 없거나 꺼져 있으면 아무것도 하지 않는다.
    /// </summary>
    public static void CaptureForBuild(GameObject avatarGameObject)
    {
        if (avatarGameObject == null)
            return;

        PendingSnapshots.Remove(avatarGameObject);

        var designer = avatarGameObject.GetComponentsInChildren<DiNeLightingDesigner>(true)
            .FirstOrDefault(item => item != null && item.enabled);
        if (designer == null)
            return;

        PendingSnapshots[avatarGameObject] = CreateSnapshot(designer);
    }

    private static Snapshot CreateSnapshot(DiNeLightingDesigner designer)
    {
        designer.EnsureDefaults();

        return new Snapshot
        {
            Controls = new HashSet<DiNeLightingControl>(
                designer.Controls.Where(setting => setting != null && setting.enabled).Select(setting => setting.control)),
            TargetShaders = designer.TargetShaders,
            Excluded = new HashSet<Renderer>(designer.Excludes.Where(item => item != null)),
        };
    }

    public static void NormalizeForBuild(GameObject avatarGameObject)
    {
        NormalizeTemporaryAvatar(avatarGameObject);
    }

    public static void NormalizeForPlayMode(GameObject avatarGameObject)
    {
        NormalizeTemporaryAvatar(avatarGameObject);
    }

    private static void NormalizeTemporaryAvatar(GameObject avatarGameObject)
    {
        if (avatarGameObject == null)
            return;

        // 스냅샷은 한 번 쓰고 버린다(실패해도 다음 빌드에서 다시 캡처된다).
        PendingSnapshots.TryGetValue(avatarGameObject, out var snapshot);
        PendingSnapshots.Remove(avatarGameObject);

        var descriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>()
            ?? avatarGameObject.GetComponentInChildren<VRCAvatarDescriptor>(true);
        if (descriptor == null)
            return;

        // 컴포넌트가 아직 살아 있으면(플레이 모드 등) 그쪽이 최신이므로 우선한다.
        var designer = avatarGameObject.GetComponentsInChildren<DiNeLightingDesigner>(true)
            .FirstOrDefault(item => item != null && item.enabled);
        if (designer != null)
            snapshot = CreateSnapshot(designer);

        if (snapshot == null)
            return;

        var controls = snapshot.Controls;
        var targetShaders = snapshot.TargetShaders;

        if (controls.Count == 0)
            return;

        // 어느 프로파일도 손댈 게 없으면 머티리얼을 복제하지 않는다.
        if (!DiNeShaderProfile.All.Any(profile => profile.RequiresMaterialPreparation(controls)))
            return;

        var excluded = snapshot.Excluded;
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
                        (targetShaders & item.Flag) != 0 && item.IsTarget(original.shader));
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
    public static void CleanupTempAssets()
    {
        PendingSnapshots.Clear();
        DiNeLightingBakeSession.CleanupTempAssets();
    }
}
#endif
