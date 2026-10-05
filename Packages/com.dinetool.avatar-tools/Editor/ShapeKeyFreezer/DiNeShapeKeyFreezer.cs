#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public partial class DiNeShapeKeyFreezer : EditorWindow
{
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    private GameObject targetObject;
    private AnimationClip animationClip;
    private float clipTime = 0.0f;

    private string[] UI_TEXT;
    private Texture2D tabIcon;
    // Legacy tool menu intentionally hidden.
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow<DiNeShapeKeyFreezer>("ShapeKey Freezer");
        window.minSize = new Vector2(300, 300);
        window.position = new Rect(window.position.x, window.position.y, 420, 420);
    }

    void OnEnable()
    {
        tabIcon    = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
        titleContent = new GUIContent("ShapeKey", tabIcon);
        language = (LanguagePreset)Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
        SetLanguage(language);
    }

    void OnGUI()
    {
        EnsureTutorial();
        Color previousBackground = GUI.backgroundColor;
        tutorial.BeginFrame();
        try
        {
            tutorial.BeginScrollScope();
            tutorialScroll = EditorGUILayout.BeginScrollView(tutorialScroll);
            try { DrawWindowContent(); }
            finally
            {
                EditorGUILayout.EndScrollView();
                tutorial.EndScrollScope(GUILayoutUtility.GetLastRect());
            }
        }
        finally { tutorial.EndFrame(); GUI.backgroundColor = previousBackground; }
    }

    private void OnDisable() => tutorial?.Suspend();

    private void DrawWindowContent()
    {
        var sharedLanguage = (LanguagePreset)Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
        if (language != sharedLanguage) { language = sharedLanguage; SetLanguage(language); }
        string desc;
        switch (language)
        {
            case LanguagePreset.Korean: desc = "애니메이션 클립의 쉐이프키 값을 고정시켜 저장합니다."; break;
            case LanguagePreset.Japanese: desc = "アニメーションクリップのシェイプキー値を固定して保存します。"; break;
            default: desc = "Freeze and save BlendShape (ShapeKey) values from an animation clip."; break;
        }
        DiNeEditorUI.DrawHeader("ShapeKey Freezer", desc);
        GUILayout.Space(5f);
        int currentLangIndex = (int)language;
        int newLangIndex = DiNeEditorUI.DrawLanguageToolbar(currentLangIndex);
        if (newLangIndex != currentLangIndex)
        {
            language = (LanguagePreset)newLangIndex;
            SetLanguage(language);
        }
        GUILayout.Space(15f);
        tutorial.DrawControls();

        // ─── 오브젝트 / 클립 선택 ───
        EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);
        EditorGUILayout.LabelField(UI_TEXT[0], EditorStyles.boldLabel);
        GUILayout.Space(3);
        EditorGUI.BeginChangeCheck();
        targetObject  = (GameObject)EditorGUILayout.ObjectField(UI_TEXT[1], targetObject,  typeof(GameObject),  true);
        if (EditorGUI.EndChangeCheck()) tutorial.NotifyAction("target");
        tutorial.Draw("target", GUILayoutUtility.GetLastRect());
        EditorGUI.BeginChangeCheck();
        animationClip = (AnimationClip)EditorGUILayout.ObjectField(UI_TEXT[2], animationClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck()) tutorial.NotifyAction("clip");
        tutorial.Draw("clip", GUILayoutUtility.GetLastRect());
        EditorGUILayout.EndVertical();

        GUILayout.Space(DiNeEditorUI.CardSpacing);

        // ─── 슬라이더 + 버튼 ───
        EditorGUI.BeginDisabledGroup(targetObject == null || animationClip == null);

        EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);
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
            tutorial.NotifyAction("time");
        }
        tutorial.Draw("time", GUILayoutUtility.GetLastRect());

        EditorGUILayout.EndVertical();

        GUILayout.Space(DiNeEditorUI.CardSpacing);

        var prevBg = GUI.backgroundColor;
        if (DiNeEditorUI.Button(UI_TEXT[4]))
        {
            SampleBlendShapesOnly();
            Debug.Log($"[DiNe ShapeKey Freezer] {targetObject.name} — {UI_TEXT[5]}");
            tutorial.NotifyAction("apply");
        }
        tutorial.Anchor("apply", GUILayoutUtility.GetLastRect());
        tutorial.Anchor("undo", GUILayoutUtility.GetLastRect());
        GUI.backgroundColor = prevBg;

        EditorGUI.EndDisabledGroup();
        tutorial.Draw("apply");
        tutorial.Draw("undo");

        GUILayout.Space(DiNeEditorUI.CardSpacing);

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

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        return DiNeEditorUI.DrawToolbar(selected, options, height);
    }
}
#endif
