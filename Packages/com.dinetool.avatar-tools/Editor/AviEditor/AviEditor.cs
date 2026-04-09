using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ArmatureScalerEditor : EditorWindow
{
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    // ─── 에디터 모드 ───
    private enum EditorMode { Armature, ShapeKey, Expression }
    private EditorMode currentMode = EditorMode.Armature;

    private GameObject targetAvatarRoot;

    private HumanoidBodyPart selectedPart = HumanoidBodyPart.None;

    private Dictionary<HumanoidBodyPart, Vector3> scaleValues = new Dictionary<HumanoidBodyPart, Vector3>();
    private Dictionary<HumanoidBodyPart, Quaternion> rotationValues = new Dictionary<HumanoidBodyPart, Quaternion>();
    // 추가: 포지션 값 딕셔너리
    private Dictionary<HumanoidBodyPart, Vector3> positionValues = new Dictionary<HumanoidBodyPart, Vector3>();

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;
    private Vector2 scrollPosition;
    private Texture2D selectedButtonTex;

    private Dictionary<HumanBodyBones, Transform> boneMapping;

    private Vector3 lastKnownScale = Vector3.one;
    private Quaternion lastKnownRotation = Quaternion.identity;
    private Vector3 lastKnownPosition = Vector3.zero; // 추가

    private string[] presetFiles;
    private int selectedPresetIndex = -1;
    private string selectedPresetName = "";
    private Vector2 presetScrollPosition;

    // ─── Animation Freezer 필드 ───
    private AnimationClip animationClip;
    private float clipTime = 0.0f;
    private string[] SK_TEXT;

    // 스냅샷
    private Dictionary<SkinnedMeshRenderer, float[]>                          _snapShapeKeys  = new Dictionary<SkinnedMeshRenderer, float[]>();
    private Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scl)> _snapTransforms = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
    private bool   _hasSnapshot;
    private GameObject _prevSnapshotTarget;

    // ─── Expression 탭 필드 ───
    private SkinnedMeshRenderer      _bodySmr;
    private RenderTexture            _faceRT;
    private PreviewRenderUtility     _facePreview;
    private bool                     _facePreviewDirty = true;
    private AnimationClip            _exprClip;
    private bool                     _exprIsNewClip;
    private string                   _exprNewClipName = "New Expression";
    private float[]                  _exprShapeValues;
    private Vector2                  _exprShapeScroll;
    private Vector2                  _exprMainScroll;
    private string                   _exprShapeSearch = "";
    // VRChat FX
    private UnityEditor.Animations.AnimatorController _exprFxController;
    private int                      _exprFxLayerSel  = 0;   // 0=LeftHand 1=RightHand
    private int                      _exprFxStateSel  = -1;
    private bool                     _exprFxExpanded      = false;
    private bool                     _exprFxPreviewMode   = false;
    private float[]                  _exprWorkingValues;
    private GameObject               _prevExprTarget;

    private enum HumanoidBodyPart
    {
        None,
        Head, Neck, Spine, Torso, Hips,
        LeftShoulder, LeftArm, LeftLowerArm, LeftHand,
        RightShoulder, RightArm, RightLowerArm, RightHand,
        LeftLeg, LeftLowerLeg, LeftFoot,
        RightLeg, RightLowerLeg, RightFoot,
        LeftBreast, RightBreast, LeftButt, RightButt
    }

    [MenuItem("DiNe/Avi Editor", false, 1)]
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow<ArmatureScalerEditor>();
        window.minSize = new Vector2(300, 400);
        window.position = new Rect(window.position.x, window.position.y, 420, 850);
    }

    void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("Avi Editor", tabIcon);
        selectedButtonTex = MakeTex(1, 1, new Color(0.2f, 0.4f, 1f, 1f));
        SetLanguage(language);
        SetShapeKeyLanguage(language);
        InitializeValues();
        if (targetAvatarRoot != null)
        {
            boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
            LoadCurrentValues();
        }
        
        RefreshPresetList();
        
        EditorApplication.update += OnEditorUpdate;
    }
    
    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (selectedPart == HumanoidBodyPart.None || targetAvatarRoot == null || boneMapping == null)
        {
            return;
        }

        HumanBodyBones boneType = GetBoneType(selectedPart);
        if (boneMapping.TryGetValue(boneType, out Transform boneTransform))
        {
            // 스케일 감지
            if (boneTransform.localScale != lastKnownScale)
            {
                scaleValues[selectedPart] = boneTransform.localScale;
                lastKnownScale = boneTransform.localScale;
                Repaint();
            }

            // 로테이션 감지 (가능한 부위만)
            if (CanRotate(selectedPart) && boneTransform.localRotation != lastKnownRotation)
            {
                rotationValues[selectedPart] = boneTransform.localRotation;
                lastKnownRotation = boneTransform.localRotation;
                Repaint();
            }

            // 포지션 감지 (전체 가능) - 추가
            if (boneTransform.localPosition != lastKnownPosition)
            {
                positionValues[selectedPart] = boneTransform.localPosition;
                lastKnownPosition = boneTransform.localPosition;
                Repaint();
            }
        }
    }
    
    void OnFocus()
    {
        RefreshPresetList();
    }

    private void RefreshPresetList()
    {
        string[] guids = AssetDatabase.FindAssets("t:ArmatureScalerPresetData", new[] { "Assets" });
        presetFiles = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();
        selectedPresetIndex = -1;
        selectedPresetName = "";
    }

    private void InitializeValues()
    {
        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part != HumanoidBodyPart.None)
            {
                // Scale Init
                if (!scaleValues.ContainsKey(part)) scaleValues.Add(part, Vector3.one);
                else scaleValues[part] = Vector3.one;
                
                // Rotation Init
                if (!rotationValues.ContainsKey(part)) rotationValues.Add(part, Quaternion.identity);
                else rotationValues[part] = Quaternion.identity;

                // Position Init - 추가 (기본값 0,0,0)
                if (!positionValues.ContainsKey(part)) positionValues.Add(part, Vector3.zero);
                else positionValues[part] = Vector3.zero;
            }
        }
    }

    private void LoadCurrentValues()
    {
        if (boneMapping == null) return;

        List<HumanoidBodyPart> partsToUpdate = new List<HumanoidBodyPart>(scaleValues.Keys);

        foreach (var part in partsToUpdate)
        {
            HumanBodyBones boneType = GetBoneType(part);
            if (boneMapping.ContainsKey(boneType))
            {
                Transform t = boneMapping[boneType];
                scaleValues[part] = t.localScale;
                rotationValues[part] = t.localRotation;
                positionValues[part] = t.localPosition; // 포지션 로드 추가
            }
        }
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
        float iconSize = 72f;
        GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label("Avi Editor", titleStyle, GUILayout.Height(iconSize));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = "";
        switch (language)
        {
            case LanguagePreset.Korean: desc = "아바타의 아마추어와 쉐이프키를 쉽고 안전하게 편집합니다."; break;
            case LanguagePreset.Japanese: desc = "アバターのアーマチュアとシェイプキーを簡単かつ安全に編集します。"; break;
            default: desc = "Easily and safely edit your avatar's armature and shapekeys."; break;
        }
        GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 언어 선택 ───
        int currentLanguageIndex = (int)language;
        string[] languageButtons = { "English", "한국어", "日本語" };
        int newLanguageIndex = DrawCustomToolbar(currentLanguageIndex, languageButtons, 30);
        if (newLanguageIndex != currentLanguageIndex)
        {
            language = (LanguagePreset)newLanguageIndex;
            SetLanguage(language);
            SetShapeKeyLanguage(language);
        }
        GUILayout.Space(5);

        // ─── 모드 탭 ───
        string[] modeLabels = language == LanguagePreset.Korean   ? new[] { "아마추어", "애니메이션", "표정" }
                            : language == LanguagePreset.Japanese  ? new[] { "アーマチュア", "アニメーション", "表情" }
                            : new[] { "Armature", "Animation", "Expression" };
        int newMode = DrawCustomToolbar((int)currentMode, modeLabels, 30);
        if (newMode != (int)currentMode)
        {
            currentMode = (EditorMode)newMode;
        }
        GUILayout.Space(10);

        if (currentMode == EditorMode.Armature)
            DrawArmatureGUI();
        else if (currentMode == EditorMode.ShapeKey)
            DrawShapeKeyGUI();
        else
            DrawExpressionGUI();
    }

    // ─── 아마추어 모드 GUI ───
    private void DrawArmatureGUI()
    {
        
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(UI_TEXT[0], targetAvatarRoot, typeof(GameObject), true);
        
        if (EditorGUI.EndChangeCheck())
        {
            if (targetAvatarRoot != null)
            {
                boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
                LoadCurrentValues();
                selectedPart = HumanoidBodyPart.None;
            }
            else
            {
                boneMapping = null;
                selectedPart = HumanoidBodyPart.None;
                InitializeValues();
            }
        }
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(UI_TEXT[27], EditorStyles.boldLabel);
        
        presetScrollPosition = EditorGUILayout.BeginScrollView(presetScrollPosition, GUILayout.Height(60));
        
        if (presetFiles != null && presetFiles.Length > 0)
        {
            const int buttonsPerRow = 2;
            int buttonCount = presetFiles.Length;

            for (int i = 0; i < buttonCount; i += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                
                int buttonsOnThisRow = Mathf.Min(buttonsPerRow, buttonCount - i);

                for (int j = 0; j < buttonsOnThisRow; j++)
                {
                    int index = i + j;
                    if (index < buttonCount)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(presetFiles[index]);
                        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                        if (selectedPresetIndex == index)
                        {
                            buttonStyle.normal.background = selectedButtonTex;
                        }
                        
                        if (buttonsOnThisRow == 1)
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(fileName, buttonStyle, GUILayout.Width(position.width * 0.45f), GUILayout.Height(25)))
                            {
                                selectedPresetIndex = index;
                                selectedPresetName = fileName;
                            }
                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            if (GUILayout.Button(fileName, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(25)))
                            {
                                selectedPresetIndex = index;
                                selectedPresetName = fileName;
                            }
                        }
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("No presets found.", EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndScrollView();
        
        // --- 프리셋 불러오기 버튼 영역 (수정됨) ---
        EditorGUI.BeginDisabledGroup(selectedPresetIndex == -1);
        
        // 1. 전체 불러오기
        if (GUILayout.Button(UI_TEXT[28]))
        {
            LoadSelectedPreset(true, true, true); // 스케일, 로테이션, 포지션 모두
        }

        // 2. 분리 적용 버튼 (3개로 확장)
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(UI_TEXT[39])) // 스케일만
        {
            LoadSelectedPreset(true, false, false);
        }
        if (GUILayout.Button(UI_TEXT[40])) // 로테이션만
        {
            LoadSelectedPreset(false, true, false);
        }
        if (GUILayout.Button(UI_TEXT[43])) // 포지션만 (새로 추가됨)
        {
            LoadSelectedPreset(false, false, true);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
        // ----------------------------------------

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(UI_TEXT[29], new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } }))
        {
            SaveNewPreset();
        }
        GUI.backgroundColor = prevBg;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(selectedPresetIndex == -1);
        if (GUILayout.Button(UI_TEXT[31]))
        {
            if (EditorUtility.DisplayDialog(UI_TEXT[31], UI_TEXT[32] + selectedPresetName + UI_TEXT[33], UI_TEXT[34], UI_TEXT[35]))
            {
                DeletePreset(presetFiles[selectedPresetIndex]);
                RefreshPresetList();
                selectedPresetIndex = -1;
            }
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button(UI_TEXT[36]))
        {
            ResetScalesToDefault();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawBodyMap();
        EditorGUILayout.EndScrollView();
        
        GuiLine(1, 10);
        
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(UI_TEXT[24], EditorStyles.boldLabel);

        if (selectedPart != HumanoidBodyPart.None)
        {
            // 1. Scale
            Vector3 scale = GetPartScale(selectedPart);
            EditorGUI.BeginChangeCheck();
            float uniformScale = EditorGUILayout.FloatField(UI_TEXT[26], scale.x);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartScale(new Vector3(uniformScale, uniformScale, uniformScale));
                ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
            }
            
            EditorGUI.BeginChangeCheck();
            Vector3 newScale = EditorGUILayout.Vector3Field(GetPartName(selectedPart) + $" Scale", scale);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartScale(newScale);
                ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
            }

            // 2. Position (추가됨) - 스케일처럼 전체 적용 가능
            GUILayout.Space(10);
            GUILayout.Label(UI_TEXT[41], EditorStyles.boldLabel); // "포지션 조절"
            Vector3 position = GetPartPosition(selectedPart);
            
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = EditorGUILayout.Vector3Field(UI_TEXT[42], position); // "포지션"
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartPosition(newPosition);
                ArmatureScalerLogic.ApplyPosition(boneMapping, MapToHumanBodyBones(positionValues));
            }
            
            // 3. Rotation (특정 부위만)
            if (CanRotate(selectedPart))
            {
                GUILayout.Space(10);
                GUILayout.Label(UI_TEXT[37], EditorStyles.boldLabel);
                Quaternion rotation = GetPartRotation(selectedPart);
                
                EditorGUI.BeginChangeCheck();
                Quaternion newRotation = Quaternion.Euler(EditorGUILayout.Vector3Field(UI_TEXT[38], rotation.eulerAngles));
                if (EditorGUI.EndChangeCheck())
                {
                    UpdatePartRotation(newRotation);
                    ArmatureScalerLogic.ApplyRotation(boneMapping, MapToHumanBodyBonesForRotation(rotationValues));
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField(UI_TEXT[25]);
        }
        
        EditorGUILayout.EndVertical();
    }

    // ─── 애니메이션 모드 GUI ───
    private void DrawShapeKeyGUI()
    {
        // 타겟 변경 시 자동 스냅샷
        if (targetAvatarRoot != _prevSnapshotTarget)
        {
            _prevSnapshotTarget = targetAvatarRoot;
            if (targetAvatarRoot != null) TakeSnapshot();
        }

        // ─── 대상 설정 ───
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(SK_TEXT[0], EditorStyles.boldLabel);
        GUILayout.Space(3);
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(SK_TEXT[1], targetAvatarRoot, typeof(GameObject), true);
        animationClip    = (AnimationClip)EditorGUILayout.ObjectField(SK_TEXT[2], animationClip, typeof(AnimationClip), false);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 슬라이더 ───
        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || animationClip == null);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(SK_TEXT[3], EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (animationClip != null)
            GUILayout.Label($"{clipTime:F3}s / {animationClip.length:F3}s",
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(3);

        EditorGUI.BeginChangeCheck();
        clipTime = EditorGUILayout.Slider(clipTime, 0f, animationClip != null ? animationClip.length : 1f);
        if (EditorGUI.EndChangeCheck())
            ApplyShapeKeys(); // 슬라이더: 쉐이프키만 실시간 미리보기

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);

        // ─── 적용 버튼 3개 ───
        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || animationClip == null);

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 36,
            normal      = { textColor = Color.white },
            hover       = { textColor = Color.white },
        };
        var prevBg = GUI.backgroundColor;

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(SK_TEXT[4], btnStyle))
        {
            ApplyShapeKeys();
            Debug.Log($"[Avi Editor] {targetAvatarRoot.name} — {SK_TEXT[7]}");
        }

        GUI.backgroundColor = new Color(0.25f, 0.65f, 0.60f);
        if (GUILayout.Button(SK_TEXT[5], btnStyle))
        {
            ApplyPose();
            Debug.Log($"[Avi Editor] {targetAvatarRoot.name} — {SK_TEXT[8]}");
        }

        GUI.backgroundColor = new Color(0.21f, 0.21f, 0.24f);
        if (GUILayout.Button(SK_TEXT[6], new GUIStyle(btnStyle)
            { normal = { textColor = new Color(0.30f, 0.82f, 0.76f) }, hover = { textColor = Color.white } }))
        {
            ApplyShapeKeys();
            ApplyPose();
            Debug.Log($"[Avi Editor] {targetAvatarRoot.name} — {SK_TEXT[9]}");
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = prevBg;

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(5);

        // ─── 복원 버튼 ───
        EditorGUI.BeginDisabledGroup(!_hasSnapshot);
        GUI.backgroundColor = new Color(0.21f, 0.21f, 0.24f);
        if (GUILayout.Button(SK_TEXT[10], new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 30,
            normal      = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            hover       = { textColor = Color.white },
        }))
        {
            RestoreSnapshot();
            Debug.Log($"[Avi Editor] {SK_TEXT[11]}");
        }
        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(5);
        EditorGUILayout.HelpBox(SK_TEXT[12], MessageType.Info);
    }

    // ─── 쉐이프키 적용 ───
    private void ApplyShapeKeys()
    {
        if (targetAvatarRoot == null || animationClip == null) return;

        foreach (var b in AnimationUtility.GetCurveBindings(animationClip))
        {
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            var curve = AnimationUtility.GetEditorCurve(animationClip, b);
            if (curve == null) continue;

            var t = string.IsNullOrEmpty(b.path) ? targetAvatarRoot.transform : targetAvatarRoot.transform.Find(b.path);
            if (t == null) continue;
            var smr = t.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;
            int idx = smr.sharedMesh.GetBlendShapeIndex(b.propertyName.Substring("blendShape.".Length));
            if (idx < 0) continue;

            Undo.RecordObject(smr, "Avi Editor Freeze ShapeKey");
            smr.SetBlendShapeWeight(idx, curve.Evaluate(clipTime));
        }
    }

    // ─── 포즈 적용 ───
    private void ApplyPose()
    {
        if (targetAvatarRoot == null || animationClip == null) return;

        var samples = new Dictionary<string, TransformSample>();
        foreach (var b in AnimationUtility.GetCurveBindings(animationClip))
        {
            if (b.propertyName.StartsWith("blendShape.")) continue;
            if (b.type != typeof(Transform)) continue;
            var curve = AnimationUtility.GetEditorCurve(animationClip, b);
            if (curve == null) continue;
            if (!samples.ContainsKey(b.path)) samples[b.path] = new TransformSample();
            samples[b.path].Set(b.propertyName, curve.Evaluate(clipTime));
        }
        foreach (var kvp in samples)
        {
            var t = string.IsNullOrEmpty(kvp.Key) ? targetAvatarRoot.transform : targetAvatarRoot.transform.Find(kvp.Key);
            if (t == null) continue;
            Undo.RecordObject(t, "Avi Editor Freeze Pose");
            kvp.Value.ApplyTo(t);
        }
    }

    // ─── 스냅샷 ───
    private void TakeSnapshot()
    {
        _snapShapeKeys.Clear();
        _snapTransforms.Clear();
        if (targetAvatarRoot == null) { _hasSnapshot = false; return; }

        foreach (var smr in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            int cnt = smr.sharedMesh.blendShapeCount;
            var w = new float[cnt];
            for (int i = 0; i < cnt; i++) w[i] = smr.GetBlendShapeWeight(i);
            _snapShapeKeys[smr] = w;
        }
        foreach (Transform t in targetAvatarRoot.GetComponentsInChildren<Transform>(true))
            _snapTransforms[t] = (t.localPosition, t.localRotation, t.localScale);

        _hasSnapshot = true;
    }

    private void RestoreSnapshot()
    {
        foreach (var kvp in _snapShapeKeys)
        {
            if (kvp.Key == null) continue;
            Undo.RecordObject(kvp.Key, "Avi Editor Restore");
            for (int i = 0; i < kvp.Value.Length; i++) kvp.Key.SetBlendShapeWeight(i, kvp.Value[i]);
        }
        foreach (var kvp in _snapTransforms)
        {
            if (kvp.Key == null) continue;
            Undo.RecordObject(kvp.Key, "Avi Editor Restore");
            kvp.Key.localPosition = kvp.Value.pos;
            kvp.Key.localRotation = kvp.Value.rot;
            kvp.Key.localScale    = kvp.Value.scl;
        }
    }

    // ─── Transform 샘플 누적기 ───
    private class TransformSample
    {
        float? px,py,pz, rx,ry,rz,rw, ex,ey,ez, sx,sy,sz;
        public void Set(string p, float v)
        {
            switch (p)
            {
                case "localPosition.x": case "m_LocalPosition.x": px=v; break;
                case "localPosition.y": case "m_LocalPosition.y": py=v; break;
                case "localPosition.z": case "m_LocalPosition.z": pz=v; break;
                case "localRotation.x": case "m_LocalRotation.x": rx=v; break;
                case "localRotation.y": case "m_LocalRotation.y": ry=v; break;
                case "localRotation.z": case "m_LocalRotation.z": rz=v; break;
                case "localRotation.w": case "m_LocalRotation.w": rw=v; break;
                case "localEulerAnglesRaw.x": ex=v; break;
                case "localEulerAnglesRaw.y": ey=v; break;
                case "localEulerAnglesRaw.z": ez=v; break;
                case "localScale.x": case "m_LocalScale.x": sx=v; break;
                case "localScale.y": case "m_LocalScale.y": sy=v; break;
                case "localScale.z": case "m_LocalScale.z": sz=v; break;
            }
        }
        public void ApplyTo(Transform t)
        {
            if (px.HasValue||py.HasValue||pz.HasValue)
            { var p=t.localPosition; if(px.HasValue)p.x=px.Value; if(py.HasValue)p.y=py.Value; if(pz.HasValue)p.z=pz.Value; t.localPosition=p; }
            if (rx.HasValue&&ry.HasValue&&rz.HasValue&&rw.HasValue)
                t.localRotation=new Quaternion(rx.Value,ry.Value,rz.Value,rw.Value);
            else if (ex.HasValue||ey.HasValue||ez.HasValue)
            { var e=t.localEulerAngles; if(ex.HasValue)e.x=ex.Value; if(ey.HasValue)e.y=ey.Value; if(ez.HasValue)e.z=ez.Value; t.localEulerAngles=e; }
            if (sx.HasValue||sy.HasValue||sz.HasValue)
            { var s=t.localScale; if(sx.HasValue)s.x=sx.Value; if(sy.HasValue)s.y=sy.Value; if(sz.HasValue)s.z=sz.Value; t.localScale=s; }
        }
    }

    private void SetShapeKeyLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                SK_TEXT = new string[]
                {
                    "대상 설정",                    // 0
                    "대상 오브젝트 (Root)",          // 1
                    "애니메이션 클립",               // 2
                    "시점 조절",                    // 3
                    "쉐이프키만 적용",               // 4
                    "포즈만 적용",                   // 5
                    "전체 적용",                    // 6
                    "쉐이프키 적용 완료!",           // 7
                    "포즈 적용 완료!",               // 8
                    "전체 적용 완료!",               // 9
                    "↩  원본으로 복원",             // 10
                    "원본 상태로 복원했습니다.",      // 11
                    "슬라이더로 미리보기, 버튼으로 씬에 적용합니다.", // 12
                };
                break;
            case LanguagePreset.Japanese:
                SK_TEXT = new string[]
                {
                    "対象設定",
                    "対象オブジェクト (Root)",
                    "アニメーションクリップ",
                    "タイミング調整",
                    "シェイプキーのみ",
                    "ポーズのみ",
                    "すべて適用",
                    "シェイプキーを適用しました。",
                    "ポーズを適用しました。",
                    "すべて適用しました。",
                    "↩  元に戻す",
                    "元の状態に戻しました。",
                    "スライダーでプレビュー、ボタンでシーンに適用します。",
                };
                break;
            default:
                SK_TEXT = new string[]
                {
                    "Target Settings",
                    "Target Object (Root)",
                    "Animation Clip",
                    "Time Adjustment",
                    "Shape Keys Only",
                    "Pose Only",
                    "Apply All",
                    "Shape Keys: applied.",
                    "Pose: applied.",
                    "All: applied.",
                    "↩  Restore Original",
                    "Restored to original state.",
                    "Slide to preview. Buttons apply to scene.",
                };
                break;
        }
    }

    private void DrawBoneButton(HumanoidBodyPart part, string buttonText, float width, float height)
    {
        HumanoidBodyPart previousSelectedPart = selectedPart;
        HumanBodyBones boneType = GetBoneType(part);
        string boneName = (boneMapping != null && boneMapping.ContainsKey(boneType)) ? boneMapping[boneType].name : UI_TEXT[30];
        string display = boneName == UI_TEXT[30] ? buttonText : boneName;
        
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        
        if (selectedPart == part)
        {
            buttonStyle.normal.background = selectedButtonTex;
        }
        else
        {
            buttonStyle.normal.background = GUI.skin.button.normal.background;
        }

        buttonStyle.normal.textColor = Color.white;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        buttonStyle.fontSize = 12;

        bool isEnabled = boneName != UI_TEXT[30];
        EditorGUI.BeginDisabledGroup(!isEnabled);
        
        if (GUILayout.Button(display, buttonStyle, GUILayout.Width(width), GUILayout.Height(height)))
        {
            GUI.FocusControl(null);
            
            if (previousSelectedPart != part && previousSelectedPart != HumanoidBodyPart.None)
            {
                ApplyCurrentChanges(previousSelectedPart);
            }
            
            selectedPart = part;
            HumanBodyBones selectedBoneType = GetBoneType(part);
            if (boneMapping != null && boneMapping.TryGetValue(selectedBoneType, out Transform boneTransform))
            {
                lastKnownScale = boneTransform.localScale;
                lastKnownRotation = boneTransform.localRotation;
                lastKnownPosition = boneTransform.localPosition; // 포지션 업데이트
                Selection.activeGameObject = boneTransform.gameObject;
            }
            else
            {
                lastKnownScale = Vector3.one;
                lastKnownRotation = Quaternion.identity;
                lastKnownPosition = Vector3.zero;
            }
        }
        
        EditorGUI.EndDisabledGroup();
    }

    private void ApplyCurrentChanges(HumanoidBodyPart part)
    {
        if (targetAvatarRoot == null || boneMapping == null) return;

        HumanBodyBones boneType = GetBoneType(part);
        if (scaleValues.TryGetValue(part, out Vector3 scale))
        {
            ArmatureScalerLogic.ApplyScale(boneMapping, new Dictionary<HumanBodyBones, Vector3> { { boneType, scale } });
        }
        if (positionValues.TryGetValue(part, out Vector3 position))
        {
            ArmatureScalerLogic.ApplyPosition(boneMapping, new Dictionary<HumanBodyBones, Vector3> { { boneType, position } });
        }
        if (CanRotate(part) && rotationValues.TryGetValue(part, out Quaternion rotation))
        {
            ArmatureScalerLogic.ApplyRotation(boneMapping, new Dictionary<HumanBodyBones, Quaternion> { { boneType, rotation } });
        }
    }

    private bool CanRotate(HumanoidBodyPart part)
    {
        return part == HumanoidBodyPart.LeftBreast || part == HumanoidBodyPart.RightBreast ||
               part == HumanoidBodyPart.LeftButt || part == HumanoidBodyPart.RightButt ||
               part == HumanoidBodyPart.Neck ||
               part == HumanoidBodyPart.LeftShoulder || part == HumanoidBodyPart.RightShoulder;
    }
    
    private Vector3 GetPartScale(HumanoidBodyPart part)
    {
        if (scaleValues.TryGetValue(part, out Vector3 s)) return s;
        return Vector3.one;
    }
    
    private Quaternion GetPartRotation(HumanoidBodyPart part)
    {
        if (rotationValues.TryGetValue(part, out Quaternion r)) return r;
        return Quaternion.identity;
    }

    // 추가: 포지션 Getter
    private Vector3 GetPartPosition(HumanoidBodyPart part)
    {
        if (positionValues.TryGetValue(part, out Vector3 p)) return p;
        return Vector3.zero;
    }

    private void UpdatePartScale(Vector3 newScale)
    {
        if (selectedPart != HumanoidBodyPart.None && scaleValues.ContainsKey(selectedPart))
        {
            scaleValues[selectedPart] = newScale;
            lastKnownScale = newScale;
        }
    }
    
    private void UpdatePartRotation(Quaternion newRotation)
    {
        if (selectedPart != HumanoidBodyPart.None && rotationValues.ContainsKey(selectedPart))
        {
            rotationValues[selectedPart] = newRotation;
            lastKnownRotation = newRotation;
        }
    }

    // 추가: 포지션 Updater
    private void UpdatePartPosition(Vector3 newPosition)
    {
        if (selectedPart != HumanoidBodyPart.None && positionValues.ContainsKey(selectedPart))
        {
            positionValues[selectedPart] = newPosition;
            lastKnownPosition = newPosition;
        }
    }
    
    private string GetBoneName(HumanoidBodyPart part)
    {
        HumanBodyBones boneType = GetBoneType(part);
        if (boneMapping != null && boneMapping.ContainsKey(boneType))
        {
            return boneMapping[boneType].name;
        }
        return UI_TEXT[30];
    }

    private string GetPartName(HumanoidBodyPart part)
    {
        // ... (기존 이름 반환 로직 동일) ...
        switch (part)
        {
            case HumanoidBodyPart.Head: return UI_TEXT[6];
            case HumanoidBodyPart.Neck: return UI_TEXT[44];
            case HumanoidBodyPart.Torso: return UI_TEXT[7];
            case HumanoidBodyPart.Spine: return UI_TEXT[8];
            case HumanoidBodyPart.Hips: return UI_TEXT[9];
            case HumanoidBodyPart.LeftShoulder: return UI_TEXT[45];
            case HumanoidBodyPart.RightShoulder: return UI_TEXT[46];
            case HumanoidBodyPart.LeftArm: return UI_TEXT[3];
            case HumanoidBodyPart.LeftLowerArm: return UI_TEXT[4];
            case HumanoidBodyPart.LeftHand: return UI_TEXT[5];
            case HumanoidBodyPart.RightArm: return UI_TEXT[10];
            case HumanoidBodyPart.RightLowerArm: return UI_TEXT[11];
            case HumanoidBodyPart.RightHand: return UI_TEXT[12];
            case HumanoidBodyPart.LeftLeg: return UI_TEXT[13];
            case HumanoidBodyPart.LeftLowerLeg: return UI_TEXT[14];
            case HumanoidBodyPart.LeftFoot: return UI_TEXT[15];
            case HumanoidBodyPart.RightLeg: return UI_TEXT[16];
            case HumanoidBodyPart.RightLowerLeg: return UI_TEXT[17];
            case HumanoidBodyPart.RightFoot: return UI_TEXT[18];
            case HumanoidBodyPart.LeftBreast: return UI_TEXT[20];
            case HumanoidBodyPart.RightBreast: return UI_TEXT[21];
            case HumanoidBodyPart.LeftButt: return UI_TEXT[22];
            case HumanoidBodyPart.RightButt: return UI_TEXT[23];
            default: return "선택 없음";
        }
    }
    
    private HumanBodyBones GetBoneType(HumanoidBodyPart part)
    {
        // ... (기존 로직 동일) ...
        switch (part)
        {
            case HumanoidBodyPart.Head: return HumanBodyBones.Head;
            case HumanoidBodyPart.Neck: return HumanBodyBones.Neck;
            case HumanoidBodyPart.Torso: return HumanBodyBones.Chest;
            case HumanoidBodyPart.Spine: return HumanBodyBones.Spine;
            case HumanoidBodyPart.Hips: return HumanBodyBones.Hips;
            case HumanoidBodyPart.LeftBreast: return (HumanBodyBones)100;
            case HumanoidBodyPart.RightBreast: return (HumanBodyBones)101;
            case HumanoidBodyPart.LeftButt: return (HumanBodyBones)102;
            case HumanoidBodyPart.RightButt: return (HumanBodyBones)103;
            case HumanoidBodyPart.LeftShoulder: return HumanBodyBones.LeftShoulder;
            case HumanoidBodyPart.RightShoulder: return HumanBodyBones.RightShoulder;
            case HumanoidBodyPart.LeftArm: return HumanBodyBones.LeftUpperArm;
            case HumanoidBodyPart.LeftLowerArm: return HumanBodyBones.LeftLowerArm;
            case HumanoidBodyPart.LeftHand: return HumanBodyBones.LeftHand;
            case HumanoidBodyPart.RightArm: return HumanBodyBones.RightUpperArm;
            case HumanoidBodyPart.RightLowerArm: return HumanBodyBones.RightLowerArm;
            case HumanoidBodyPart.RightHand: return HumanBodyBones.RightHand;
            case HumanoidBodyPart.LeftLeg: return HumanBodyBones.LeftUpperLeg;
            case HumanoidBodyPart.LeftLowerLeg: return HumanBodyBones.LeftLowerLeg;
            case HumanoidBodyPart.LeftFoot: return HumanBodyBones.LeftFoot;
            case HumanoidBodyPart.RightLeg: return HumanBodyBones.RightUpperLeg;
            case HumanoidBodyPart.RightLowerLeg: return HumanBodyBones.RightLowerLeg;
            case HumanoidBodyPart.RightFoot: return HumanBodyBones.RightFoot;
            default: return HumanBodyBones.LastBone;
        }
    }

    private Dictionary<HumanBodyBones, Vector3> MapToHumanBodyBones(Dictionary<HumanoidBodyPart, Vector3> scales)
    {
        Dictionary<HumanBodyBones, Vector3> result = new Dictionary<HumanBodyBones, Vector3>();
        foreach (var kvp in scales)
        {
            HumanBodyBones boneType = GetBoneType(kvp.Key);
            if (boneType != HumanBodyBones.LastBone)
            {
                result[boneType] = kvp.Value;
            }
        }
        return result;
    }

    private Dictionary<HumanBodyBones, Quaternion> MapToHumanBodyBonesForRotation(Dictionary<HumanoidBodyPart, Quaternion> rotations)
    {
        Dictionary<HumanBodyBones, Quaternion> result = new Dictionary<HumanBodyBones, Quaternion>();
        foreach (var kvp in rotations)
        {
            if (CanRotate(kvp.Key))
            {
                HumanBodyBones boneType = GetBoneType(kvp.Key);
                if (boneType != HumanBodyBones.LastBone)
                {
                    result[boneType] = kvp.Value;
                }
            }
        }
        return result;
    }
    
    // ─── 인체 실루엣 UI ───

    private string GetShortLabel(HumanoidBodyPart part)
    {
        switch (language)
        {
            case LanguagePreset.Korean:
                switch (part)
                {
                    case HumanoidBodyPart.Head: return "머리";
                    case HumanoidBodyPart.Neck: return "목";
                    case HumanoidBodyPart.Torso: return "몸통";
                    case HumanoidBodyPart.Spine: return "척추";
                    case HumanoidBodyPart.Hips: return "골반";
                    case HumanoidBodyPart.LeftShoulder: return "L 어깨";
                    case HumanoidBodyPart.RightShoulder: return "R 어깨";
                    case HumanoidBodyPart.LeftArm: return "L 팔";
                    case HumanoidBodyPart.LeftLowerArm: return "L 하완";
                    case HumanoidBodyPart.LeftHand: return "L 손";
                    case HumanoidBodyPart.RightArm: return "R 팔";
                    case HumanoidBodyPart.RightLowerArm: return "R 하완";
                    case HumanoidBodyPart.RightHand: return "R 손";
                    case HumanoidBodyPart.LeftLeg: return "L 다리";
                    case HumanoidBodyPart.LeftLowerLeg: return "L 하퇴";
                    case HumanoidBodyPart.LeftFoot: return "L 발";
                    case HumanoidBodyPart.RightLeg: return "R 다리";
                    case HumanoidBodyPart.RightLowerLeg: return "R 하퇴";
                    case HumanoidBodyPart.RightFoot: return "R 발";
                    case HumanoidBodyPart.LeftBreast: return "L 가슴";
                    case HumanoidBodyPart.RightBreast: return "R 가슴";
                    case HumanoidBodyPart.LeftButt: return "L 엉덩이";
                    case HumanoidBodyPart.RightButt: return "R 엉덩이";
                }
                break;
            case LanguagePreset.Japanese:
                switch (part)
                {
                    case HumanoidBodyPart.Head: return "頭";
                    case HumanoidBodyPart.Neck: return "首";
                    case HumanoidBodyPart.Torso: return "胴体";
                    case HumanoidBodyPart.Spine: return "脊椎";
                    case HumanoidBodyPart.Hips: return "骨盤";
                    case HumanoidBodyPart.LeftShoulder: return "L 肩";
                    case HumanoidBodyPart.RightShoulder: return "R 肩";
                    case HumanoidBodyPart.LeftArm: return "L 腕";
                    case HumanoidBodyPart.LeftLowerArm: return "L 前腕";
                    case HumanoidBodyPart.LeftHand: return "L 手";
                    case HumanoidBodyPart.RightArm: return "R 腕";
                    case HumanoidBodyPart.RightLowerArm: return "R 前腕";
                    case HumanoidBodyPart.RightHand: return "R 手";
                    case HumanoidBodyPart.LeftLeg: return "L 脚";
                    case HumanoidBodyPart.LeftLowerLeg: return "L 下脚";
                    case HumanoidBodyPart.LeftFoot: return "L 足";
                    case HumanoidBodyPart.RightLeg: return "R 脚";
                    case HumanoidBodyPart.RightLowerLeg: return "R 下脚";
                    case HumanoidBodyPart.RightFoot: return "R 足";
                    case HumanoidBodyPart.LeftBreast: return "L 胸";
                    case HumanoidBodyPart.RightBreast: return "R 胸";
                    case HumanoidBodyPart.LeftButt: return "L 尻";
                    case HumanoidBodyPart.RightButt: return "R 尻";
                }
                break;
            default: // English
                switch (part)
                {
                    case HumanoidBodyPart.Head: return "Head";
                    case HumanoidBodyPart.Neck: return "Neck";
                    case HumanoidBodyPart.Torso: return "Torso";
                    case HumanoidBodyPart.Spine: return "Spine";
                    case HumanoidBodyPart.Hips: return "Hips";
                    case HumanoidBodyPart.LeftShoulder: return "L Shoulder";
                    case HumanoidBodyPart.RightShoulder: return "R Shoulder";
                    case HumanoidBodyPart.LeftArm: return "L Arm";
                    case HumanoidBodyPart.LeftLowerArm: return "L Elbow";
                    case HumanoidBodyPart.LeftHand: return "L Hand";
                    case HumanoidBodyPart.RightArm: return "R Arm";
                    case HumanoidBodyPart.RightLowerArm: return "R Elbow";
                    case HumanoidBodyPart.RightHand: return "R Hand";
                    case HumanoidBodyPart.LeftLeg: return "L Leg";
                    case HumanoidBodyPart.LeftLowerLeg: return "L Knee";
                    case HumanoidBodyPart.LeftFoot: return "L Foot";
                    case HumanoidBodyPart.RightLeg: return "R Leg";
                    case HumanoidBodyPart.RightLowerLeg: return "R Knee";
                    case HumanoidBodyPart.RightFoot: return "R Foot";
                    case HumanoidBodyPart.LeftBreast: return "L Breast";
                    case HumanoidBodyPart.RightBreast: return "R Breast";
                    case HumanoidBodyPart.LeftButt: return "L Butt";
                    case HumanoidBodyPart.RightButt: return "R Butt";
                }
                break;
        }
        return part.ToString();
    }

    private void DrawBodyMap()
    {
        float panelWidth = position.width - 30;
        float panelHeight = 660;
        Rect area = GUILayoutUtility.GetRect(panelWidth, panelHeight);

        EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f, 1f));

        // 선택된 본 정보 표시 (상단)
        if (selectedPart != HumanoidBodyPart.None)
        {
            HumanBodyBones selBone = GetBoneType(selectedPart);
            string selName = (boneMapping != null && boneMapping.ContainsKey(selBone)) ? boneMapping[selBone].name : "---";
            GUIStyle infoStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                fontSize = 12
            };
            Rect infoRect = new Rect(area.x, area.y + 4, area.width, 20);
            GUI.Label(infoRect, GetPartName(selectedPart) + " : " + selName, infoStyle);
        }

        float cx = area.x + area.width * 0.5f;
        float top = area.y + 30;
        Color lc = new Color(0.2f, 0.8f, 0.3f, 0.5f);
        float lw = 2f;

        // ─── 여성형 실루엣 ───

        // 머리
        Vector2 headC = new Vector2(cx, top + 35);
        DrawCircleOutline(headC, 28, lc, lw);

        // 목
        float neckTop = top + 63;
        float neckBot = top + 80;
        DrawLineAA(new Vector2(cx - 8, neckTop), new Vector2(cx - 8, neckBot), lc, lw);
        DrawLineAA(new Vector2(cx + 8, neckTop), new Vector2(cx + 8, neckBot), lc, lw);

        // 어깨
        float shY = top + 85;
        float shW = 85;
        // 어깨 → 목 연결 (부드러운 경사)
        DrawLineAA(new Vector2(cx - 8, neckBot), new Vector2(cx - shW, shY + 5), lc, lw);
        DrawLineAA(new Vector2(cx + 8, neckBot), new Vector2(cx + shW, shY + 5), lc, lw);

        // 몸통 (여성형: 가슴 넓고 허리 좁고 골반 넓음)
        float chestY = top + 130;
        float chestW = 52;
        float waistY = top + 200;
        float waistW = 34;
        float hipY = top + 260;
        float hipW = 55;

        // 어깨 → 가슴 (벌어지는 곡선)
        DrawLineAA(new Vector2(cx - shW, shY + 5), new Vector2(cx - chestW, chestY), lc, lw);
        DrawLineAA(new Vector2(cx + shW, shY + 5), new Vector2(cx + chestW, chestY), lc, lw);

        // 가슴 → 허리 (좁아짐)
        DrawLineAA(new Vector2(cx - chestW, chestY), new Vector2(cx - waistW, waistY), lc, lw);
        DrawLineAA(new Vector2(cx + chestW, chestY), new Vector2(cx + waistW, waistY), lc, lw);

        // 허리 → 골반 (넓어짐)
        DrawLineAA(new Vector2(cx - waistW, waistY), new Vector2(cx - hipW, hipY), lc, lw);
        DrawLineAA(new Vector2(cx + waistW, waistY), new Vector2(cx + hipW, hipY), lc, lw);

        // 골반 하단 연결
        DrawLineAA(new Vector2(cx - hipW, hipY), new Vector2(cx - 22, hipY + 20), lc, lw);
        DrawLineAA(new Vector2(cx + hipW, hipY), new Vector2(cx + 22, hipY + 20), lc, lw);

        // 가슴 곡선 힌트
        DrawLineAA(new Vector2(cx - 18, chestY - 10), new Vector2(cx - 25, chestY + 8), lc, lw);
        DrawLineAA(new Vector2(cx - 25, chestY + 8), new Vector2(cx - 15, chestY + 18), lc, lw);
        DrawLineAA(new Vector2(cx + 18, chestY - 10), new Vector2(cx + 25, chestY + 8), lc, lw);
        DrawLineAA(new Vector2(cx + 25, chestY + 8), new Vector2(cx + 15, chestY + 18), lc, lw);

        // 팔
        float elbowY = top + 200;
        float handY = top + 290;
        float armOffX = 30;
        // 왼팔
        DrawLineAA(new Vector2(cx - shW, shY + 5), new Vector2(cx - shW - armOffX, elbowY), lc, lw);
        DrawLineAA(new Vector2(cx - shW - armOffX, elbowY), new Vector2(cx - shW - armOffX - 18, handY), lc, lw);
        // 오른팔
        DrawLineAA(new Vector2(cx + shW, shY + 5), new Vector2(cx + shW + armOffX, elbowY), lc, lw);
        DrawLineAA(new Vector2(cx + shW + armOffX, elbowY), new Vector2(cx + shW + armOffX + 18, handY), lc, lw);

        // 다리
        float legSplit = 22;
        float kneeY = top + 410;
        float footY = top + 550;
        // 왼다리
        DrawLineAA(new Vector2(cx - legSplit, hipY + 20), new Vector2(cx - legSplit - 12, kneeY), lc, lw);
        DrawLineAA(new Vector2(cx - legSplit - 12, kneeY), new Vector2(cx - legSplit - 8, footY), lc, lw);
        // 오른다리
        DrawLineAA(new Vector2(cx + legSplit, hipY + 20), new Vector2(cx + legSplit + 12, kneeY), lc, lw);
        DrawLineAA(new Vector2(cx + legSplit + 12, kneeY), new Vector2(cx + legSplit + 8, footY), lc, lw);

        // 발
        DrawLineAA(new Vector2(cx - legSplit - 8, footY), new Vector2(cx - legSplit - 22, footY + 18), lc, lw);
        DrawLineAA(new Vector2(cx + legSplit + 8, footY), new Vector2(cx + legSplit + 22, footY + 18), lc, lw);

        // ─── 관절 버튼 ───
        float r = 12;
        float rs = 8; // 세부 부위 반지름

        // 중심부
        DrawJointButton(HumanoidBodyPart.Head, headC, r);
        DrawJointButton(HumanoidBodyPart.Neck, new Vector2(cx, neckTop + 8), rs);
        DrawJointButton(HumanoidBodyPart.Torso, new Vector2(cx, chestY - 20), r);
        DrawJointButton(HumanoidBodyPart.Spine, new Vector2(cx, waistY), r);
        DrawJointButton(HumanoidBodyPart.Hips, new Vector2(cx, hipY + 5), r);

        // 왼팔 (어깨/쇄골 포함)
        DrawJointButton(HumanoidBodyPart.LeftShoulder, new Vector2(cx - shW * 0.52f, shY + 2), rs);
        DrawJointButton(HumanoidBodyPart.LeftArm, new Vector2(cx - shW, shY + 5), r);
        DrawJointButton(HumanoidBodyPart.LeftLowerArm, new Vector2(cx - shW - armOffX, elbowY), r);
        DrawJointButton(HumanoidBodyPart.LeftHand, new Vector2(cx - shW - armOffX - 18, handY), r);

        // 오른팔 (어깨/쇄골 포함)
        DrawJointButton(HumanoidBodyPart.RightShoulder, new Vector2(cx + shW * 0.52f, shY + 2), rs);
        DrawJointButton(HumanoidBodyPart.RightArm, new Vector2(cx + shW, shY + 5), r);
        DrawJointButton(HumanoidBodyPart.RightLowerArm, new Vector2(cx + shW + armOffX, elbowY), r);
        DrawJointButton(HumanoidBodyPart.RightHand, new Vector2(cx + shW + armOffX + 18, handY), r);

        // 왼다리
        DrawJointButton(HumanoidBodyPart.LeftLeg, new Vector2(cx - legSplit - 5, hipY + 40), r);
        DrawJointButton(HumanoidBodyPart.LeftLowerLeg, new Vector2(cx - legSplit - 12, kneeY), r);
        DrawJointButton(HumanoidBodyPart.LeftFoot, new Vector2(cx - legSplit - 15, footY + 8), r);

        // 오른다리
        DrawJointButton(HumanoidBodyPart.RightLeg, new Vector2(cx + legSplit + 5, hipY + 40), r);
        DrawJointButton(HumanoidBodyPart.RightLowerLeg, new Vector2(cx + legSplit + 12, kneeY), r);
        DrawJointButton(HumanoidBodyPart.RightFoot, new Vector2(cx + legSplit + 15, footY + 8), r);

        // 세부 (가슴/엉덩이)
        DrawJointButton(HumanoidBodyPart.LeftBreast, new Vector2(cx - 22, chestY + 5), rs);
        DrawJointButton(HumanoidBodyPart.RightBreast, new Vector2(cx + 22, chestY + 5), rs);
        DrawJointButton(HumanoidBodyPart.LeftButt, new Vector2(cx - 30, hipY + 10), rs);
        DrawJointButton(HumanoidBodyPart.RightButt, new Vector2(cx + 30, hipY + 10), rs);
    }

    private void DrawJointButton(HumanoidBodyPart part, Vector2 center, float radius)
    {
        HumanBodyBones boneType = GetBoneType(part);
        bool found = boneMapping != null && boneMapping.ContainsKey(boneType);
        bool isSelected = selectedPart == part;

        Color dotColor = !found ? new Color(0.3f, 0.3f, 0.3f, 0.6f)
                       : isSelected ? new Color(0.3f, 0.5f, 1f, 1f)
                       : new Color(0.2f, 0.8f, 0.3f, 0.9f);

        float clickSize = Mathf.Max(radius * 2.5f, 26f);
        Rect clickRect = new Rect(center.x - clickSize / 2, center.y - clickSize / 2, clickSize, clickSize);

        DrawFilledCircle(center, radius, dotColor);
        if (isSelected)
            DrawCircleOutline(center, radius + 4, new Color(0.3f, 0.5f, 1f, 0.5f), 2f);

        // 짧은 라벨
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = found ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.45f, 0.45f, 0.45f) },
            fontSize = 9
        };
        string shortLabel = GetShortLabel(part);
        Vector2 sz = labelStyle.CalcSize(new GUIContent(shortLabel));
        Rect labelRect = new Rect(center.x - sz.x / 2, center.y + radius + 2, sz.x, 13);
        GUI.Label(labelRect, shortLabel, labelStyle);

        // 클릭
        if (found && Event.current.type == EventType.MouseDown && Event.current.button == 0 && clickRect.Contains(Event.current.mousePosition))
        {
            Event.current.Use();
            GUI.FocusControl(null);
            if (selectedPart != part && selectedPart != HumanoidBodyPart.None)
                ApplyCurrentChanges(selectedPart);

            selectedPart = part;
            if (boneMapping.TryGetValue(boneType, out Transform boneTransform))
            {
                lastKnownScale = boneTransform.localScale;
                lastKnownRotation = boneTransform.localRotation;
                lastKnownPosition = boneTransform.localPosition;
                Selection.activeGameObject = boneTransform.gameObject;
            }
            Repaint();
        }
    }

    // ─── 드로잉 헬퍼 ───

    private void DrawLineAA(Vector2 a, Vector2 b, Color color, float width)
    {
        Handles.BeginGUI();
        Color prev = Handles.color;
        Handles.color = color;
        Handles.DrawAAPolyLine(width, new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
        Handles.color = prev;
        Handles.EndGUI();
    }

    private void DrawCircleOutline(Vector2 center, float radius, Color color, float width)
    {
        Handles.BeginGUI();
        Color prev = Handles.color;
        Handles.color = color;
        Vector3[] points = new Vector3[33];
        for (int i = 0; i <= 32; i++)
        {
            float angle = (float)i / 32 * Mathf.PI * 2;
            points[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, 0);
        }
        Handles.DrawAAPolyLine(width, points);
        Handles.color = prev;
        Handles.EndGUI();
    }

    private void DrawFilledCircle(Vector2 center, float radius, Color color)
    {
        Handles.BeginGUI();
        Color prev = Handles.color;
        Handles.color = color;
        Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0), Vector3.forward, radius);
        Handles.color = prev;
        Handles.EndGUI();
    }

    void GuiLine(int i_height = 1, int padding = 5)
    {
        GUILayout.Space(padding);
        Rect rect = EditorGUILayout.GetControlRect(false, i_height);
        rect.height = i_height;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        GUILayout.Space(padding);
    }
    
    private void SetLanguage(LanguagePreset lang)
    {
        // 텍스트 배열 확장 (41, 42, 43 인덱스 추가)
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new string[] {
                    "타겟 아바타 루트", "스케일 저장", "저장된 스케일 불러오기",
                    "왼쪽 팔", "왼쪽 아랫팔", "왼쪽 손", "머리", "몸통", "척추", "골반",
                    "오른쪽 팔", "오른쪽 아랫팔", "오른쪽 손",
                    "왼쪽 다리", "왼쪽 아랫다리", "왼쪽 발",
                    "오른쪽 다리", "오른쪽 아랫다리", "오른쪽 발",
                    "세부 부위", "왼쪽 가슴", "오른쪽 가슴", "왼쪽 엉덩이", "오른쪽 엉덩이",
                    "선택된 부위", "부위를 선택하세요.", "일괄 조절",
                    "스케일 프리셋", "프리셋 불러오기", "새 프리셋 저장",
                    "미발견", "선택한 프리셋 삭제", "프리셋 ‘", "’를 정말로 삭제하시겠습니까?",
                    "삭제", "취소", "사이즈 초기화", "로테이션 조절", "로테이션",
                    "스케일만 불러오기", "로테이션만 불러오기",
                    "포지션 조절", "포지션", "포지션만 불러오기" // 41, 42, 43 추가
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new string[] {
                    "対象アバターのルート", "スケールを保存", "保存されたスケールをロード",
                    "左腕", "左前腕", "左手", "頭", "胴体", "脊椎", "骨盤",
                    "右腕", "右前腕", "右手",
                    "左脚", "左下脚", "左足",
                    "右脚", "右下脚", "右足",
                    "詳細部位", "左胸", "右胸", "左お尻", "右お尻",
                    "選択された部位", "部位を選択してください。", "一括調整",
                    "スケールプリセット", "プリセットをロード", "新しいプリセットを保存",
                    "未発見", "選択したプリセットを削除", "プリセット「", "」を本当に削除しますか？",
                    "削除", "キャンセル", "サイズリセット", "回転調整", "回転",
                    "スケールのみロード", "回転のみロード",
                    "位置調整", "位置", "位置のみロード" // 41, 42, 43 추가
                };
                break;
            case LanguagePreset.English:
            default:
                UI_TEXT = new string[] {
                    "Target Avatar Root", "Save Scale", "Load Saved Scale",
                    "Left Arm", "Left Lower Arm", "Left Hand", "Head", "Torso", "Spine", "Hips",
                    "Right Arm", "Right Lower Arm", "Right Hand",
                    "Left Leg", "Left Lower Leg", "Left Foot",
                    "Right Leg", "Right Lower Leg", "Right Foot",
                    "Detailed Parts", "Left Breast", "Right Breast", "Left Butt", "Right Butt",
                    "Selected Part", "Select a part.", "Uniform Scale",
                    "Scale Presets", "Load Preset", "Save New Preset",
                    "Not Found", "Delete Selected Preset", "Are you sure you want to delete the preset '", "'?",
                    "Delete", "Cancel", "Reset Scales", "Rotation Adjustment", "Rotation",
                    "Load Scale Only", "Load Rotation Only",
                    "Position Adjustment", "Position", "Load Position Only" // 41, 42, 43 추가
                };
                break;
        }
    }
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    
    // 로드 함수 수정 (Position 플래그 추가)
    private void LoadSelectedPreset(bool applyScale = true, bool applyRotation = true, bool applyPosition = true)
    {
        if (targetAvatarRoot == null)
        {
            Debug.LogError("아바타를 먼저 지정해주세요.");
            return;
        }
        
        if (selectedPresetIndex >= 0 && selectedPresetIndex < presetFiles.Length)
        {
            string filePath = presetFiles[selectedPresetIndex];
            ArmatureScalerPresetData loadedData = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(filePath);
            if (loadedData != null)
            {
                // 1. Scale
                if (applyScale)
                {
                    scaleValues.Clear();
                    foreach (var kvp in loadedData.scales.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                        {
                            scaleValues[part] = kvp.Value.ToVector3();
                        }
                    }
                    ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
                }

                // 2. Rotation
                if (applyRotation)
                {
                    rotationValues.Clear();
                    foreach (var kvp in loadedData.rotations.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                        {
                            rotationValues[part] = kvp.Value.ToQuaternion();
                        }
                    }
                    ArmatureScalerLogic.ApplyRotation(boneMapping, MapToHumanBodyBonesForRotation(rotationValues));
                }

                // 3. Position (추가됨)
                if (applyPosition)
                {
                    positionValues.Clear();
                    foreach (var kvp in loadedData.positions.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                        {
                            positionValues[part] = kvp.Value.ToVector3();
                        }
                    }
                    ArmatureScalerLogic.ApplyPosition(boneMapping, MapToHumanBodyBones(positionValues));
                }
                
                string appliedType = "데이터";
                if (applyScale && applyRotation && applyPosition) appliedType = "전체 데이터";
                else
                {
                    List<string> types = new List<string>();
                    if (applyScale) types.Add("스케일");
                    if (applyRotation) types.Add("로테이션");
                    if (applyPosition) types.Add("포지션");
                    appliedType = string.Join(", ", types) + " 데이터";
                }

                Debug.Log($"프리셋 '{Path.GetFileNameWithoutExtension(filePath)}'의 {appliedType}가 성공적으로 불러와졌습니다.");
            }
            else
            {
                Debug.LogError("프리셋 파일을 찾을 수 없거나 형식이 올바르지 않습니다.");
            }
        }
    }

    private void SaveNewPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject("새 프리셋 저장", "NewPreset", "asset", "새로운 프리셋을 저장할 위치와 이름을 지정해주세요.");
        if (!string.IsNullOrEmpty(path))
        {
            ArmatureScalerPresetData existingPreset = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(path);
            if (existingPreset != null)
            {
                if (!EditorUtility.DisplayDialog("경고", "기존 프리셋을 덮어쓰시겠습니까?", "덮어쓰기", "취소"))
                {
                    return;
                }
                AssetDatabase.DeleteAsset(path);
            }
            
            ArmatureScalerPresetData newPreset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            
            // Scale 저장
            foreach (var kvp in scaleValues)
            {
                newPreset.scales.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));
            }

            // Rotation 저장
            foreach (var kvp in rotationValues)
            {
                if(CanRotate(kvp.Key))
                {
                    newPreset.rotations.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableQuaternion(kvp.Value));
                }
            }

            // Position 저장 (추가됨)
            foreach (var kvp in positionValues)
            {
                newPreset.positions.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));
            }

            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            RefreshPresetList();
            Debug.Log($"프리셋이 '{path}'에 성공적으로 저장되었습니다.");
        }
    }

    private void DeletePreset(string filePath)
    {
        AssetDatabase.DeleteAsset(filePath);
        AssetDatabase.Refresh();
        Debug.Log("프리셋이 성공적으로 삭제되었습니다.");
    }

    private void ResetScalesToDefault()
    {
        if (targetAvatarRoot == null)
        {
            Debug.LogError("아바타를 먼저 지정해주세요.");
            return;
        }
        
        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part != HumanoidBodyPart.None)
            {
                if (scaleValues.ContainsKey(part))
                {
                    scaleValues[part] = Vector3.one;
                }
                // *주의: 포지션은 0,0,0으로 초기화하면 뼈가 뭉개지므로 초기화 로직에 포함하지 않거나,
                // 최초 로드 시의 값을 기억해두었다가 복구하는 방식이어야 합니다.
                // 여기서는 스케일 초기화만 유지합니다. (요청에 스케일 리셋만 포함되어 있으므로)
            }
        }
        
        ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
        
        selectedPart = HumanoidBodyPart.None;
        Debug.Log("모든 스케일이 초기화되었습니다.");
    }

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button) { 
                fontStyle = (i == selected) ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal = { textColor = (i == selected) ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
            {
                newSelected = i;
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }

    // ════════════════════════════════════════════
    //  EXPRESSION TAB
    // ════════════════════════════════════════════

    private void DrawExpressionGUI()
    {
        // ── 타겟 변경 감지 → Body SMR 재탐색 ──
        if (targetAvatarRoot != _prevExprTarget)
        {
            _prevExprTarget = targetAvatarRoot;
            RefreshBodySmr();
            _facePreviewDirty = true;
        }

        var prevBg = GUI.backgroundColor;

        // ── 대상 오브젝트 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(language == LanguagePreset.Korean  ? "대상 설정"
                                 : language == LanguagePreset.Japanese ? "対象設定" : "Target", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(
            language == LanguagePreset.Korean  ? "아바타 루트"
          : language == LanguagePreset.Japanese ? "アバタールート" : "Avatar Root",
            targetAvatarRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            RefreshBodySmr();
            _facePreviewDirty = true;
        }

        if (_bodySmr == null && targetAvatarRoot != null)
            EditorGUILayout.HelpBox(
                language == LanguagePreset.Korean  ? "Body 메쉬를 찾을 수 없습니다."
              : language == LanguagePreset.Japanese ? "Bodyメッシュが見つかりません。" : "Body mesh not found.",
                MessageType.Warning);

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);

        if (targetAvatarRoot == null || _bodySmr == null)
        {
            EditorGUILayout.HelpBox(
                language == LanguagePreset.Korean  ? "아바타를 할당하면 표정 편집을 시작합니다."
              : language == LanguagePreset.Japanese ? "アバタを割り当てて表情編集を開始します。" : "Assign an avatar to start editing expressions.",
                MessageType.Info);
            return;
        }

        // ── 얼굴 프리뷰 (스크롤 독립 고정) ──
        DrawFacePreview();
        GUILayout.Space(5);

        _exprMainScroll = EditorGUILayout.BeginScrollView(_exprMainScroll);

        // ── 애니메이션 클립 ──
        DrawExpressionClipSection(prevBg);
        GUILayout.Space(5);

        // ── VRChat FX ──
        DrawExpressionFxSection(prevBg);
        GUILayout.Space(5);

        // ── 쉐이프키 슬라이더 ──
        DrawExpressionShapeKeys(prevBg);

        EditorGUILayout.EndScrollView();
        GUI.backgroundColor = prevBg;
    }

    // ── 얼굴 프리뷰 ──────────────────────────────
    private void DrawFacePreview()
    {
        float size = Mathf.Min(position.width - 20f, 260f);
        Rect previewRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
        previewRect.x = (position.width - size) * 0.5f;

        // 배경
        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
        EditorGUI.DrawRect(previewRect, bgCol);

        if (Event.current.type == EventType.Repaint)
        {
            RenderFacePreview((int)size);
            if (_faceRT != null)
                GUI.DrawTexture(previewRect, _faceRT, ScaleMode.ScaleToFit, true);
            _facePreviewDirty = false;
        }

        // 슬라이더 움직임 → dirty
        if (Event.current.type == EventType.Used)
            _facePreviewDirty = true;
    }

    private void RenderFacePreview(int size)
    {
        if (_bodySmr == null || targetAvatarRoot == null) return;
        if (size <= 0) return;

        // RenderTexture 준비
        if (_faceRT == null || _faceRT.width != size)
        {
            if (_faceRT != null) _faceRT.Release();
            _faceRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            _faceRT.antiAliasing = 2;
            _faceRT.Create();
        }

        // Head bone 위치
        Vector3 headPos = targetAvatarRoot.transform.position + Vector3.up * 1.5f;
        if (boneMapping != null && boneMapping.TryGetValue(HumanBodyBones.Head, out Transform headBone))
            headPos = headBone.position;

        // 임시 카메라
        var camGo = new GameObject("__AviExprPreviewCam__") { hideFlags = HideFlags.HideAndDontSave };
        var cam   = camGo.AddComponent<Camera>();
        cam.backgroundColor    = new Color(0, 0, 0, 0);
        cam.clearFlags         = CameraClearFlags.SolidColor;
        cam.orthographic       = false;
        cam.fieldOfView        = 22f;
        cam.nearClipPlane      = 0.01f;
        cam.farClipPlane       = 100f;
        cam.targetTexture      = _faceRT;
        cam.cullingMask        = -1;
        cam.enabled            = false;

        // 아바타 정면 방향 계산
        Vector3 avatarForward = targetAvatarRoot.transform.forward;
        // 카메라를 약간 위쪽에서 앞쪽으로 배치 (얼굴 줌인)
        cam.transform.position = headPos + avatarForward * 0.45f + Vector3.up * 0.04f;
        cam.transform.LookAt(headPos + Vector3.up * 0.04f);

        cam.Render();
        cam.targetTexture = null;
        DestroyImmediate(camGo);
    }

    // ── 애니메이션 클립 섹션 ─────────────────────
    private void DrawExpressionClipSection(Color prevBg)
    {
        EditorGUILayout.BeginVertical("box");

        string clipLabel    = language == LanguagePreset.Korean  ? "표정 애니메이션"
                            : language == LanguagePreset.Japanese ? "表情アニメーション" : "Expression Clip";
        string newLabel     = language == LanguagePreset.Korean  ? "새로 만들기"
                            : language == LanguagePreset.Japanese ? "新規作成" : "New";
        string overwrite    = language == LanguagePreset.Korean  ? "덮어쓰기 저장"
                            : language == LanguagePreset.Japanese ? "上書き保存" : "Overwrite";
        string saveAsNew    = language == LanguagePreset.Korean  ? "새 파일로 저장"
                            : language == LanguagePreset.Japanese ? "新規ファイル保存" : "Save as New";

        EditorGUILayout.LabelField(clipLabel, EditorStyles.boldLabel);

        // 클립 할당 필드 (할당 시 자동 불러오기)
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        _exprClip = (AnimationClip)EditorGUILayout.ObjectField(_exprClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck() && _exprClip != null)
        {
            _exprIsNewClip = false;
            LoadExpressionFromClip();
        }
        if (GUILayout.Button(newLabel, GUILayout.Width(80)))
        {
            _exprClip        = null;
            _exprIsNewClip   = true;
            _exprNewClipName = "New Expression";
        }
        EditorGUILayout.EndHorizontal();

        // 새 클립이거나 미할당 시 이름 입력
        if (_exprClip == null)
        {
            _exprNewClipName = EditorGUILayout.TextField(
                language == LanguagePreset.Korean ? "클립 이름" : language == LanguagePreset.Japanese ? "クリップ名" : "Clip Name",
                _exprNewClipName);
        }

        EditorGUILayout.BeginHorizontal();

        // 덮어쓰기 저장 (클립 있을 때만 활성)
        GUI.backgroundColor = new Color(0.25f, 0.65f, 0.60f);
        EditorGUI.BeginDisabledGroup(_exprClip == null);
        if (GUILayout.Button(overwrite, GUILayout.ExpandWidth(true), GUILayout.Height(28)))
            SaveExpressionClip(overwriteExisting: true);
        EditorGUI.EndDisabledGroup();

        // 새 파일로 저장
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(saveAsNew, GUILayout.ExpandWidth(true), GUILayout.Height(28)))
            SaveExpressionClip(overwriteExisting: false);

        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ── VRChat FX 섹션 ───────────────────────────
    private void DrawExpressionFxSection(Color prevBg)
    {
        string fxTitle      = language == LanguagePreset.Korean  ? "VRChat FX 연동"
                            : language == LanguagePreset.Japanese ? "VRChat FX 連携" : "VRChat FX";
        string replaceLabel = language == LanguagePreset.Korean  ? "현재 표정으로 교체"
                            : language == LanguagePreset.Japanese ? "現在の表情に差し替え" : "Replace with Current";

        _exprFxExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_exprFxExpanded, fxTitle);
        if (_exprFxExpanded)
        {
            EditorGUILayout.BeginVertical("box");

            // FX 컨트롤러 필드 (수동 지정용)
            EditorGUI.BeginChangeCheck();
            _exprFxController = (UnityEditor.Animations.AnimatorController)EditorGUILayout.ObjectField(
                "FX Controller", _exprFxController,
                typeof(UnityEditor.Animations.AnimatorController), false);
            if (EditorGUI.EndChangeCheck()) _exprFxStateSel = -1;

            if (_exprFxController != null)
            {
                // Left Hand / Right Hand 레이어 탭
                var layerNames = GetHandLayerNames(_exprFxController);
                if (layerNames.Length > 0)
                {
                    GUILayout.Space(4);
                    _exprFxLayerSel = GUILayout.Toolbar(_exprFxLayerSel, layerNames);
                    GUILayout.Space(4);

                    var clips = GetHandLayerClips(_exprFxController, layerNames[_exprFxLayerSel]);
                    if (clips.Length == 0)
                    {
                        EditorGUILayout.LabelField(
                            language == LanguagePreset.Korean ? "레이어에 애니메이션이 없습니다."
                          : language == LanguagePreset.Japanese ? "レイヤーにアニメーションがありません。"
                          : "No animations in this layer.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        var clipNameStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleRight,
                            normal    = { textColor = new Color(0.55f, 0.55f, 0.55f) },
                        };

                        for (int i = 0; i < clips.Length; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            bool sel = _exprFxStateSel == i;

                            // 메인 버튼: 스테이트 이름 (토글 미리보기)
                            GUI.backgroundColor = sel ? new Color(0.30f, 0.82f, 0.76f) : prevBg;
                            string stateLabel = clips[i].state != null ? clips[i].state.name : "(no state)";
                            if (GUILayout.Button(stateLabel, GUILayout.ExpandWidth(true), GUILayout.Height(22)))
                            {
                                if (sel)
                                {
                                    // 이미 선택된 버튼 → 토글 OFF, 작업 상태 복원
                                    _exprFxStateSel    = -1;
                                    _exprFxPreviewMode = false;
                                    RestoreWorkingValues();
                                }
                                else
                                {
                                    // 새 버튼 선택 → 현재 작업 저장 후 미리보기
                                    if (!_exprFxPreviewMode) SaveWorkingValues();
                                    _exprFxStateSel    = i;
                                    _exprFxPreviewMode = true;
                                    PreviewFxClip(clips[i].clip);
                                }
                            }
                            GUI.backgroundColor = prevBg;

                            // 클립 이름 (작게, 우측)
                            string clipName = clips[i].clip != null ? clips[i].clip.name : "(empty)";
                            GUILayout.Label(clipName, clipNameStyle, GUILayout.Width(110));

                            // 교체 버튼 (미리보기 없이도 항상 활성)
                            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
                            if (GUILayout.Button(replaceLabel, GUILayout.Width(110), GUILayout.Height(22)))
                            {
                                string stateName2 = clips[i].state != null ? clips[i].state.name : "?";
                                string confirmMsg =
                                    language == LanguagePreset.Korean
                                    ? $"'{stateName2}' 슬롯의 애니메이션을 현재 표정으로 교체합니다.\n(Ctrl+Z로 되돌릴 수 있습니다)"
                                    : language == LanguagePreset.Japanese
                                    ? $"'{stateName2}' スロットのアニメーションを現在の表情に差し替えます。\n(Ctrl+Z で元に戻せます)"
                                    : $"Replace the animation in '{stateName2}' with the current expression?\n(Undoable with Ctrl+Z)";
                                string confirmTitle =
                                    language == LanguagePreset.Korean  ? "표정 교체 확인"
                                  : language == LanguagePreset.Japanese ? "表情差し替え確認" : "Confirm Replace";
                                string ok  = language == LanguagePreset.Korean  ? "교체" : language == LanguagePreset.Japanese ? "差し替え" : "Replace";
                                string cancel = language == LanguagePreset.Korean  ? "취소" : language == LanguagePreset.Japanese ? "キャンセル" : "Cancel";
                                if (EditorUtility.DisplayDialog(confirmTitle, confirmMsg, ok, cancel))
                                    ReplaceClipInFxLayer(_exprFxController, layerNames[_exprFxLayerSel], i);
                            }
                            GUI.backgroundColor = prevBg;
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        language == LanguagePreset.Korean  ? "Left Hand 또는 Right Hand 레이어를 찾을 수 없습니다."
                      : language == LanguagePreset.Japanese ? "Left Hand / Right Hand レイヤーが見つかりません。"
                      : "No Left Hand or Right Hand layers found.", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── 쉐이프키 슬라이더 ────────────────────────
    private void DrawExpressionShapeKeys(Color prevBg)
    {
        if (_bodySmr == null || _bodySmr.sharedMesh == null) return;

        int count = _bodySmr.sharedMesh.blendShapeCount;
        if (_exprShapeValues == null || _exprShapeValues.Length != count)
        {
            _exprShapeValues = new float[count];
            for (int i = 0; i < count; i++)
                _exprShapeValues[i] = _bodySmr.GetBlendShapeWeight(i);
        }

        EditorGUILayout.BeginVertical("box");
        string skLabel = language == LanguagePreset.Korean  ? $"쉐이프키  ({count}개)"
                       : language == LanguagePreset.Japanese ? $"シェイプキー  ({count}個)" : $"Shape Keys  ({count})";
        EditorGUILayout.LabelField(skLabel, EditorStyles.boldLabel);

        // 검색
        _exprShapeSearch = EditorGUILayout.TextField("", _exprShapeSearch, EditorStyles.toolbarSearchField);
        GUILayout.Space(3);

        // 리셋 버튼
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(0.21f, 0.21f, 0.24f);
        string resetAll = language == LanguagePreset.Korean  ? "전체 초기화"
                        : language == LanguagePreset.Japanese ? "全てリセット" : "Reset All";
        if (GUILayout.Button(resetAll, GUILayout.Width(90), GUILayout.Height(22)))
        {
            Undo.RecordObject(_bodySmr, "Expr Reset ShapeKeys");
            for (int i = 0; i < count; i++)
            {
                _exprShapeValues[i] = 0f;
                _bodySmr.SetBlendShapeWeight(i, 0f);
            }
            _facePreviewDirty = true;
        }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(3);

        string searchLower = _exprShapeSearch.ToLower();
        for (int i = 0; i < count; i++)
        {
            string shapeName = _bodySmr.sharedMesh.GetBlendShapeName(i);
            if (!string.IsNullOrEmpty(searchLower) && !shapeName.ToLower().Contains(searchLower))
                continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(shapeName, GUILayout.Width(160));

            EditorGUI.BeginChangeCheck();
            float newVal = EditorGUILayout.Slider(_exprShapeValues[i], 0f, 100f);
            if (EditorGUI.EndChangeCheck())
            {
                _exprShapeValues[i] = newVal;
                Undo.RecordObject(_bodySmr, "Expr ShapeKey");
                _bodySmr.SetBlendShapeWeight(i, newVal);
                _facePreviewDirty = true;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(20);
        EditorGUILayout.EndVertical();
    }

    // ── 헬퍼: FX 미리보기 작업 상태 저장/복원 ────
    private void SaveWorkingValues()
    {
        if (_bodySmr == null || _bodySmr.sharedMesh == null) return;
        int cnt = _bodySmr.sharedMesh.blendShapeCount;
        _exprWorkingValues = new float[cnt];
        for (int i = 0; i < cnt; i++)
            _exprWorkingValues[i] = _exprShapeValues != null ? _exprShapeValues[i] : _bodySmr.GetBlendShapeWeight(i);
    }

    private void RestoreWorkingValues()
    {
        if (_bodySmr == null || _exprWorkingValues == null) return;
        Undo.RecordObject(_bodySmr, "Avi Editor FX Preview Restore");
        int cnt = Mathf.Min(_exprWorkingValues.Length, _bodySmr.sharedMesh.blendShapeCount);
        for (int i = 0; i < cnt; i++)
        {
            _bodySmr.SetBlendShapeWeight(i, _exprWorkingValues[i]);
            if (_exprShapeValues != null && i < _exprShapeValues.Length)
                _exprShapeValues[i] = _exprWorkingValues[i];
        }
        _facePreviewDirty = true;
        Repaint();
    }

    private void PreviewFxClip(AnimationClip clip)
    {
        if (clip == null || _bodySmr == null || _bodySmr.sharedMesh == null) return;
        Undo.RecordObject(_bodySmr, "Avi Editor FX Preview");
        int cnt = _bodySmr.sharedMesh.blendShapeCount;

        // 먼저 0으로 리셋 (이전 표정 지우기)
        for (int i = 0; i < cnt; i++)
        {
            _bodySmr.SetBlendShapeWeight(i, 0f);
            if (_exprShapeValues != null && i < _exprShapeValues.Length)
                _exprShapeValues[i] = 0f;
        }

        // 클립 값 적용
        string smrPath = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            if (b.path != smrPath) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            string skName = b.propertyName.Substring("blendShape.".Length);
            int idx = _bodySmr.sharedMesh.GetBlendShapeIndex(skName);
            if (idx < 0) continue;
            float val = curve.Evaluate(0f);
            _bodySmr.SetBlendShapeWeight(idx, val);
            if (_exprShapeValues != null && idx < _exprShapeValues.Length)
                _exprShapeValues[idx] = val;
        }

        _facePreviewDirty = true;
        Repaint();
    }

    // ── 헬퍼: Body SMR 탐색 ──────────────────────
    private void RefreshBodySmr()
    {
        _bodySmr = null;
        _exprShapeValues = null;
        if (targetAvatarRoot == null) return;

        foreach (var smr in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Body")
            {
                _bodySmr = smr;
                break;
            }
        }

        // Body가 없으면 블렌드쉐이프가 가장 많은 SMR 사용
        if (_bodySmr == null)
        {
            int best = -1;
            foreach (var smr in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                int cnt = smr.sharedMesh.blendShapeCount;
                if (cnt > best) { best = cnt; _bodySmr = smr; }
            }
        }

        if (_bodySmr != null && _bodySmr.sharedMesh != null)
        {
            int cnt = _bodySmr.sharedMesh.blendShapeCount;
            _exprShapeValues = new float[cnt];
            for (int i = 0; i < cnt; i++)
                _exprShapeValues[i] = _bodySmr.GetBlendShapeWeight(i);
        }

        // FX 컨트롤러 자동 탐색
        _exprFxController = null;
        _exprFxStateSel   = -1;
        AutoFindFxController();
    }

    // ── 헬퍼: 클립에서 쉐이프키 불러오기 ────────
    private void LoadExpressionFromClip()
    {
        if (_exprClip == null || _bodySmr == null || _bodySmr.sharedMesh == null) return;

        Undo.RecordObject(_bodySmr, "Load Expression Clip");

        int cnt = _bodySmr.sharedMesh.blendShapeCount;
        if (_exprShapeValues == null || _exprShapeValues.Length != cnt)
            _exprShapeValues = new float[cnt];

        // 클립의 t=0 값 적용
        foreach (var b in AnimationUtility.GetCurveBindings(_exprClip))
        {
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            var curve = AnimationUtility.GetEditorCurve(_exprClip, b);
            if (curve == null) continue;

            // Body 경로 매칭
            var t = string.IsNullOrEmpty(b.path) ? targetAvatarRoot.transform
                    : targetAvatarRoot.transform.Find(b.path);
            if (t == null || t.GetComponent<SkinnedMeshRenderer>() != _bodySmr) continue;

            string skName = b.propertyName.Substring("blendShape.".Length);
            int idx = _bodySmr.sharedMesh.GetBlendShapeIndex(skName);
            if (idx < 0) continue;

            float val = curve.Evaluate(0f);
            _exprShapeValues[idx] = val;
            _bodySmr.SetBlendShapeWeight(idx, val);
        }

        _facePreviewDirty = true;
        Repaint();
    }

    // ── 헬퍼: 클립 저장 ──────────────────────────
    private void SaveExpressionClip(bool overwriteExisting = false)
    {
        if (_bodySmr == null || _bodySmr.sharedMesh == null) return;

        AnimationClip clip;
        string path;

        if (overwriteExisting && _exprClip != null)
        {
            // 기존 클립 덮어쓰기
            clip = _exprClip;
            path = AssetDatabase.GetAssetPath(clip);
        }
        else
        {
            // 새 클립 생성
            clip = new AnimationClip();
            string dir = "Assets/Di Ne/Expressions";
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            string baseName = string.IsNullOrEmpty(_exprNewClipName) ? "New Expression" : _exprNewClipName;
            // 새로 저장 시 현재 클립 이름을 기본값으로 사용
            if (!overwriteExisting && _exprClip != null)
                baseName = _exprClip.name;
            path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{baseName}.anim");
        }

        // 전체 블렌드쉐이프 키프레임 쓰기
        Undo.RecordObject(clip, "Save Expression Clip");
        clip.ClearCurves();

        string smrPath = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        int cnt = _bodySmr.sharedMesh.blendShapeCount;
        for (int i = 0; i < cnt; i++)
        {
            float val = _exprShapeValues != null ? _exprShapeValues[i] : _bodySmr.GetBlendShapeWeight(i);
            if (val == 0f) continue; // 0인 키는 생략

            string propName = "blendShape." + _bodySmr.sharedMesh.GetBlendShapeName(i);
            var curve = AnimationCurve.Constant(0f, 0f, val);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(smrPath, typeof(SkinnedMeshRenderer), propName), curve);
        }

        if (!overwriteExisting || _exprClip == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            _exprClip      = clip;
            _exprIsNewClip = false;
        }
        else
        {
            EditorUtility.SetDirty(clip);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Avi Editor] Expression saved → {path}");
    }

    // ── 헬퍼: FX 컨트롤러 자동 탐색 ─────────────
    private void AutoFindFxController()
    {
        if (targetAvatarRoot == null) return;

        // ① VRCAvatarDescriptor 리플렉션 (SDK3 설치된 경우)
        //    SDK 어셈블리 이름이 프로젝트마다 다를 수 있어 모든 로드된 어셈블리를 순회
        System.Type vrcDescType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            vrcDescType = asm.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (vrcDescType != null) break;
        }

        if (vrcDescType != null)
        {
            var desc = targetAvatarRoot.GetComponent(vrcDescType);
            if (desc != null)
            {
                var layersProp = vrcDescType.GetField("baseAnimationLayers",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (layersProp != null)
                {
                    var layers = layersProp.GetValue(desc) as System.Array;
                    if (layers != null)
                    {
                        foreach (var layer in layers)
                        {
                            var layerType = layer.GetType();
                            var typeProp  = layerType.GetField("type",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            var animProp  = layerType.GetField("animatorController",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            var defProp   = layerType.GetField("isDefault",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (typeProp == null || animProp == null) continue;

                            // enum → string 비교 (값이 환경에 따라 달라질 수 있어 이름으로 비교)
                            string typeStr = typeProp.GetValue(layer).ToString();
                            if (typeStr != "FX") continue;

                            // isDefault == true이면 기본 컨트롤러(없는 것), false면 커스텀 할당
                            bool isDef = defProp != null && (bool)defProp.GetValue(layer);
                            if (isDef) continue;

                            var ctrl = animProp.GetValue(layer) as UnityEditor.Animations.AnimatorController;
                            if (ctrl != null) { _exprFxController = ctrl; return; }
                        }
                    }
                }
            }
        }

        // ② 폴백 A: 아바타 루트 Animator의 runtimeAnimatorController에서 FX 레이어 탐색
        var rootAnimator = targetAvatarRoot.GetComponent<Animator>();
        if (rootAnimator != null)
        {
            var ctrl = rootAnimator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (ctrl != null)
            {
                foreach (var layer in ctrl.layers)
                {
                    string n = layer.name.ToLower();
                    if (n.Contains("left hand") || n.Contains("right hand") || n == "fx")
                    {
                        _exprFxController = ctrl;
                        return;
                    }
                }
            }
        }

        // ③ 폴백 B: 자식 포함 모든 Animator 순회
        foreach (var anim in targetAvatarRoot.GetComponentsInChildren<Animator>(true))
        {
            var ctrl = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (ctrl == null) continue;
            bool hasHand = false;
            foreach (var layer in ctrl.layers)
            {
                string n = layer.name.ToLower();
                if (n.Contains("left hand") || n.Contains("right hand")) { hasHand = true; break; }
            }
            if (hasHand) { _exprFxController = ctrl; return; }
        }

        // ④ 폴백 C: 프로젝트 내 FX 이름 포함 AnimatorController 검색
        string[] guids = AssetDatabase.FindAssets("t:AnimatorController FX", new[] { "Assets" });
        foreach (var guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p);
            if (ctrl == null) continue;
            foreach (var layer in ctrl.layers)
            {
                string n = layer.name.ToLower();
                if (n.Contains("left hand") || n.Contains("right hand"))
                {
                    _exprFxController = ctrl;
                    return;
                }
            }
        }

        Debug.LogWarning("[Avi Editor] FX controller not found. Please assign it manually.");
    }

    // ── 헬퍼: Left/Right Hand 레이어 이름 목록 ───
    private string[] GetHandLayerNames(UnityEditor.Animations.AnimatorController ctrl)
    {
        var result = new System.Collections.Generic.List<string>();
        foreach (var layer in ctrl.layers)
        {
            string n = layer.name.ToLower();
            if (n.Contains("left hand") || n.Contains("lefthand") || n.Contains("right hand") || n.Contains("righthand"))
                result.Add(layer.name);
        }
        return result.ToArray();
    }

    // ── 헬퍼: 레이어 내 클립 목록 ───────────────
    private struct FxClipEntry { public AnimationClip clip; public UnityEditor.Animations.AnimatorState state; }
    private FxClipEntry[] GetHandLayerClips(UnityEditor.Animations.AnimatorController ctrl, string layerName)
    {
        var result = new System.Collections.Generic.List<FxClipEntry>();
        foreach (var layer in ctrl.layers)
        {
            if (layer.name != layerName) continue;
            foreach (var state in layer.stateMachine.states)
            {
                var motion = state.state.motion as AnimationClip;
                result.Add(new FxClipEntry { clip = motion, state = state.state });
            }
        }
        return result.ToArray();
    }

    // ── 헬퍼: FX 레이어 클립 교체 ───────────────
    private void ReplaceClipInFxLayer(UnityEditor.Animations.AnimatorController ctrl, string layerName, int stateIndex)
    {
        AnimationClip targetClip = _exprClip;
        if (targetClip == null)
        {
            // 저장 먼저
            SaveExpressionClip();
            targetClip = _exprClip;
        }
        if (targetClip == null) return;

        int idx = 0;
        foreach (var layer in ctrl.layers)
        {
            if (layer.name != layerName) continue;
            if (stateIndex < layer.stateMachine.states.Length)
            {
                Undo.RecordObject(ctrl, "Replace FX Clip");
                layer.stateMachine.states[stateIndex].state.motion = targetClip;
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Avi Editor] Replaced clip in {layerName}[{stateIndex}] → {targetClip.name}");
            }
            break;
        }
    }
}