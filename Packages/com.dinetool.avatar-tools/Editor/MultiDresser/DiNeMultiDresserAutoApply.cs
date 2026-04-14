#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

public class DiNeMultiDresserAutoApply : IVRCSDKBuildRequestedCallback, IVRCSDKPreprocessAvatarCallback
{
    public int callbackOrder => 0;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ApplyAllDressersInScene();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // 플레이 모드 종료 후 Unity 씬 복원 완료를 기다렸다가 복원
            // ExitingEditMode에서 SaveProfile()로 최신 상태가 저장되므로
            // 항상 프로필에서 복원해야 linkedObjects 등 누락 없이 정확히 복구됨
            EditorApplication.delayCall += () =>
            {
                DiNeMultiDresser[] dressers = Object.FindObjectsOfType<DiNeMultiDresser>();
                foreach (var dresser in dressers)
                {
                    if (dresser == null) continue;
                    dresser.TryRestoreFromProfile();
                    EditorUtility.SetDirty(dresser);
                }
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  IVRCSDKBuildRequestedCallback
    //  VRC SDK가 에셋을 메모리에 로드하기 전에 실행되므로
    //  여기서 Generate해야 수정된 애니메이터 컨트롤러가 빌드에 반영된다.
    // ─────────────────────────────────────────────────────────────────────────
    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Avatar)
        {
            Debug.Log("[DiNe] 🏗 빌드 요청 감지 — SDK 에셋 로드 전에 멀티 드레서 생성 시작");
            ApplyAllDressersInScene();
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  IVRCSDKPreprocessAvatarCallback
    //  안전망 역할 (OnBuildRequested가 있으면 사실상 중복이지만 남겨둠)
    // ─────────────────────────────────────────────────────────────────────────
    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        return true;
    }

    private static void ApplyAllDressersInScene()
    {
        DiNeMultiDresser[] dressers = Object.FindObjectsOfType<DiNeMultiDresser>();
        foreach (var dresser in dressers)
        {
            if (dresser.gameObject.activeInHierarchy)
            {
                GenerateDresser(dresser);
            }
        }
    }

    private static void GenerateDresser(DiNeMultiDresser dresser, bool saveProfile = true)
    {
        Debug.Log($"[DiNe] 🚀 멀티 드레서 자동 생성 시작: {dresser.name}");
        DiNeMultiIconGenerator.GenerateIcons(dresser);
        dresser.Generate(saveProfile);
    }
}
#endif
