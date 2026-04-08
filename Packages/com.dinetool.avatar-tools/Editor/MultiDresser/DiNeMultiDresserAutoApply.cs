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
