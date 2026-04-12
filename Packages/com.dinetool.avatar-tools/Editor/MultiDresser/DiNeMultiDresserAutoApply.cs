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
            // 플레이 모드 종료 후 Unity 씬 복원 완료를 기다렸다가 체크
            EditorApplication.delayCall += () =>
            {
                DiNeMultiDresser[] dressers = Object.FindObjectsOfType<DiNeMultiDresser>();
                foreach (var dresser in dressers)
                {
                    if (dresser == null) continue;
                    // 레이어가 비어있는 경우에만 프로필에서 복구 시도
                    if (dresser.layers == null || dresser.layers.Count == 0)
                    {
                        dresser.TryRestoreFromProfile();
                        EditorUtility.SetDirty(dresser);
                    }
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
                GenerateDresser(dresser);
            }
        }
    }

    private static void GenerateDresser(DiNeMultiDresser dresser)
    {
        Debug.Log($"[DiNe] 🚀 멀티 드레서 자동 생성 시작: {dresser.name}");
        DiNeMultiIconGenerator.GenerateIcons(dresser);
        dresser.Generate();
    }
}
#endif
