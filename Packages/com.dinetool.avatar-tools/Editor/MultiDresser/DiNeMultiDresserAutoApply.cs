#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

public class DiNeMultiDresserAutoApply : IVRCSDKPreprocessAvatarCallback
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

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        // 빌드 클론에서는 에디터 전용 컴포넌트가 제거되어 검색되지 않으므로
        // 씬 원본 오브젝트에서 직접 드레서를 찾아 적용
        ApplyAllDressersInScene();
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
