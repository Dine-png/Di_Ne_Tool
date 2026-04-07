#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class DiNeAnimationPoseFreezer : EditorWindow
{
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    private GameObject targetObject;
    private AnimationClip animationClip;
    private float clipTime = 0.0f;

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;

    [MenuItem("DiNe/Avatar/Animation Pose Freezer")]
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow<DiNeAnimationPoseFreezer>("Pose Freezer");
        window.minSize = new Vector2(300, 300);
        window.position = new Rect(window.position.x, window.position.y, 420, 420);
    }

    void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("Pose Freezer", tabIcon);
        SetLanguage(language);
    }

    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // ─── 타이틀 바 ───
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            font      = titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 36,
            normal    = new GUIStyleState() { textColor = Color.white }
        };
        float iconSize = windowIcon != null ? windowIcon.height * 2f / 3f : 48;
        GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label("Pose Freezer", titleStyle, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = "";
        switch (language)
        {
            case LanguagePreset.Korean: desc = "특정 애니메이션 포즈를 프리팹 형태로 고정시켜 저장합니다."; break;
            case LanguagePreset.Japanese: desc = "特定のアニメーションポーズを固定してプレハブとして保存します。"; break;
            default: desc = "Freeze and save a specific animation pose as a static prefab."; break;
        }
        GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel) 
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 언어 선택 ───
        int currentLangIndex = (int)language;
        string[] langButtons = { "English", "한국어", "日本語" };
        int newLangIndex = GUILayout.Toolbar(currentLangIndex, langButtons, GUILayout.Height(30));
        if (newLangIndex != currentLangIndex)
        {
            language = (LanguagePreset)newLangIndex;
            SetLanguage(language);
        }
        GUILayout.Space(10);

        // ─── 오브젝트 / 클립 선택 ───
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(UI_TEXT[0], EditorStyles.boldLabel);
        GUILayout.Space(3);
        targetObject  = (GameObject)EditorGUILayout.ObjectField(UI_TEXT[1], targetObject,  typeof(GameObject),  true);
        animationClip = (AnimationClip)EditorGUILayout.ObjectField(UI_TEXT[2], animationClip, typeof(AnimationClip), false);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 슬라이더 + 버튼 ───
        EditorGUI.BeginDisabledGroup(targetObject == null || animationClip == null);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(UI_TEXT[3], EditorStyles.boldLabel);
        GUILayout.Space(3);

        if (animationClip != null)
        {
            GUIStyle timeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUILayout.Label($"{clipTime:F3}s / {animationClip.length:F3}s", timeStyle);
        }

        EditorGUI.BeginChangeCheck();
        float maxTime = animationClip != null ? animationClip.length : 1f;
        clipTime = EditorGUILayout.Slider(clipTime, 0f, maxTime);
        if (EditorGUI.EndChangeCheck())
        {
            // 슬라이더 이동 시 쉐이프키만 실시간 반영
            SampleBlendShapesOnly();
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.18f, 0.76f, 0.64f);
        if (GUILayout.Button(UI_TEXT[4], new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } }, GUILayout.Height(38)))
        {
            SampleBlendShapesOnly();
            Debug.Log($"[DiNe Pose Freezer] {targetObject.name} — {UI_TEXT[5]}");
        }
        GUI.backgroundColor = prevBg;

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(5);

        // ─── 안내 메시지 ───
        EditorGUILayout.HelpBox(UI_TEXT[6], MessageType.Info);
    }

    /// <summary>
    /// 애니메이션 클립에서 blendShape 커브만 골라서 SkinnedMeshRenderer에 적용합니다.
    /// 본(Transform) 데이터는 일절 건드리지 않습니다.
    /// </summary>
    private void SampleBlendShapesOnly()
    {
        if (targetObject == null || animationClip == null) return;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(animationClip);

        foreach (var binding in bindings)
        {
            // blendShape 바인딩만 처리
            if (!binding.propertyName.StartsWith("blendShape.")) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, binding);
            if (curve == null) continue;

            float value = curve.Evaluate(clipTime);

            // 경로로 자식 오브젝트 찾기 (루트 자신이면 빈 문자열)
            Transform targetTransform;
            if (string.IsNullOrEmpty(binding.path))
                targetTransform = targetObject.transform;
            else
                targetTransform = targetObject.transform.Find(binding.path);

            if (targetTransform == null) continue;

            SkinnedMeshRenderer smr = targetTransform.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            string shapeName = binding.propertyName.Substring("blendShape.".Length);
            int index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0) continue;

            Undo.RecordObject(smr, "Sample BlendShape Pose");
            smr.SetBlendShapeWeight(index, value);
        }
    }

    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new string[]
                {
                    "대상 설정",                                               // 0
                    "대상 오브젝트 (Root)",                                    // 1
                    "애니메이션 클립",                                         // 2
                    "시점 조절",                                               // 3
                    "이 포즈로 쉐이프키 저장 (Save Pose)",                      // 4
                    "쉐이프키 포즈 고정 완료!",                                 // 5
                    "본(Transform) 위치는 변경되지 않습니다.\n애니메이션 클립 내 쉐이프키 값만 적용됩니다.", // 6
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new string[]
                {
                    "対象設定",
                    "対象オブジェクト (Root)",
                    "アニメーションクリップ",
                    "タイミング調整",
                    "このポーズでシェイプキーを保存 (Save Pose)",
                    "シェイプキーポーズを保存しました！",
                    "ボーン(Transform)は変更されません。\nアニメーションクリップ内のシェイプキー値のみ適用されます。",
                };
                break;
            default: // English
                UI_TEXT = new string[]
                {
                    "Target Settings",
                    "Target Object (Root)",
                    "Animation Clip",
                    "Time Adjustment",
                    "Save BlendShape Pose",
                    "BlendShape pose saved!",
                    "Bone (Transform) positions are NOT changed.\nOnly BlendShape values from the animation clip are applied.",
                };
                break;
        }
    }
}
#endif
