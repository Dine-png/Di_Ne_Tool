#if UNITY_EDITOR // [중요] 이 줄이 있어야 게임 실행 시 코드가 무시되어 오류가 안 납니다.
using UnityEngine;
using UnityEditor;

public class AnimationPoseFreezer : EditorWindow
{
    private GameObject targetObject;
    private AnimationClip animationClip;
    private float clipTime = 0.0f;

    [MenuItem("Tools/Animation Pose Freezer")]
    public static void ShowWindow()
    {
        GetWindow<AnimationPoseFreezer>("Pose Freezer");
    }

    void OnGUI()
    {
        GUILayout.Label("애니메이션 포즈 고정 도구", EditorStyles.boldLabel);
        GUILayout.Space(10);

        targetObject = (GameObject)EditorGUILayout.ObjectField("대상 오브젝트 (Root)", targetObject, typeof(GameObject), true);
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("애니메이션 클립", animationClip, typeof(AnimationClip), false);

        if (targetObject != null && animationClip != null)
        {
            GUILayout.Space(10);

            GUILayout.Label($"애니메이션 시점: {clipTime:F3}초 / {animationClip.length:F3}초");
            
            // 슬라이더 조작 시 실시간 반영을 위해 ChangeCheck 사용
            EditorGUI.BeginChangeCheck();
            clipTime = EditorGUILayout.Slider(clipTime, 0f, animationClip.length);
            if (EditorGUI.EndChangeCheck())
            {
                // 슬라이더를 움직일 때마다 바로바로 포즈 샘플링
                SamplePose();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("이 포즈로 영구 적용 (Save Pose)"))
            {
                ApplyPosePermanently();
            }
            
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("게임 실행 중에는 사용하지 마세요. 에디터 모드에서만 작동합니다.", MessageType.Info);
        }
    }

    void SamplePose()
    {
        if (targetObject == null || animationClip == null) return;

        // Undo 기능을 위해 변경사항 기록
        Transform[] transforms = targetObject.GetComponentsInChildren<Transform>(true);
        Undo.RecordObjects(transforms, "Sample Animation Pose");

        animationClip.SampleAnimation(targetObject, clipTime);
    }

    void ApplyPosePermanently()
    {
        SamplePose();
        Debug.Log($"[Pose Freezer] {targetObject.name} 포즈 고정 완료!");
    }
}
#endif // [중요] 에디터 코드 끝