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
        ApplyAllDressers(avatarGameObject);
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

    private static void ApplyAllDressers(GameObject root)
    {
        var dressers = root.GetComponentsInChildren<DiNeMultiDresser>(true);
        foreach (var dresser in dressers)
        {
            if (dresser.gameObject.activeSelf)
            {
                // 업로드 시에는 빌드 클론의 임시 참조가 프로필에 저장되지 않도록 saveProfile=false
                GenerateDresser(dresser, saveProfile: false);
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
