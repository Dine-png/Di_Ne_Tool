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
    [SerializeField] private EditorMode currentMode = EditorMode.Armature;

    [SerializeField] private GameObject targetAvatarRoot;

    [SerializeField] private HumanoidBodyPart selectedPart = HumanoidBodyPart.None;

    private Dictionary<HumanoidBodyPart, Vector3>    scaleValues    = new Dictionary<HumanoidBodyPart, Vector3>();
    private Dictionary<HumanoidBodyPart, Quaternion> rotationValues = new Dictionary<HumanoidBodyPart, Quaternion>();
    private Dictionary<HumanoidBodyPart, Vector3>    positionValues = new Dictionary<HumanoidBodyPart, Vector3>();

    private string[]  UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;
    [SerializeField] private Vector2 scrollPosition;
    private Texture2D selectedButtonTex;

    private Dictionary<HumanBodyBones, Transform> boneMapping;

    private Vector3    lastKnownScale    = Vector3.one;
    private Quaternion lastKnownRotation = Quaternion.identity;
    private Vector3    lastKnownPosition = Vector3.zero;

    private string[] presetFiles;
    [SerializeField] private int    selectedPresetIndex = -1;
    [SerializeField] private string selectedPresetName  = "";
    [SerializeField] private Vector2 presetScrollPosition;

    // ─── 아바타 정보 ───
    private struct AvatarInfo
    {
        public int   meshCount;
        public int   totalVertices;
        public int   totalTriangles;
        public int   totalBlendShapes;
        public int   materialCount;
        public int   boneCount;
        public long  meshMemoryBytes;
        public int   textureCount;
        public long  textureMemoryBytes;
        public int   skinnedMeshCount;
        public int   staticMeshCount;
    }
    private AvatarInfo  _avatarInfo;
    private bool        _avatarInfoReady;
    [SerializeField] private bool _avatarInfoFoldout = true;

    // ─── Animation Freezer 필드 ───
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private float clipTime = 0.0f;
    [SerializeField] private bool  _realtimePreview = false;
    private string[] SK_TEXT;

    // 스냅샷 (이제 일반 오브젝트의 T포즈 기억용으로 주로 쓰임)
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
    // 제스처 쉐이프키 포함 옵션
    [SerializeField] private bool    _includeGestureKeys  = true;

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
            _avatarInfo      = CalcAvatarInfo(targetAvatarRoot);
            _avatarInfoReady = true;
        }
        
        RefreshPresetList();
        
        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedo;

        if (_faceRT != null)
        {
            _faceRT.Release();
            DestroyImmediate(_faceRT);
            _faceRT = null;
        }
    }

    private void OnUndoRedo()
    {
        if (currentMode != EditorMode.ShapeKey || animationClip == null || targetAvatarRoot == null)
        {
            Repaint();
            return;
        }

        AnimationCurve firstCurve = null;
        SkinnedMeshRenderer firstSmr = null;
        int firstIdx = -1;

        foreach (var b in AnimationUtility.GetCurveBindings(animationClip))
        {
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            var tr = string.IsNullOrEmpty(b.path) ? targetAvatarRoot.transform : targetAvatarRoot.transform.Find(b.path);
            if (tr == null) continue;
            var smr = tr.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;
            int idx = smr.sharedMesh.GetBlendShapeIndex(b.propertyName.Substring("blendShape.".Length));
            if (idx < 0) continue;
            firstCurve = AnimationUtility.GetEditorCurve(animationClip, b);
            firstSmr = smr;
            firstIdx = idx;
            break;
        }

        if (firstCurve != null && firstSmr != null && firstIdx >= 0)
        {
            float currentVal = firstSmr.GetBlendShapeWeight(firstIdx);
            float bestTime = clipTime;
            float bestDiff = float.MaxValue;
            int steps = 200;
            for (int i = 0; i <= steps; i++)
            {
                float t = animationClip.length * i / steps;
                float diff = Mathf.Abs(firstCurve.Evaluate(t) - currentVal);
                if (diff < bestDiff) { bestDiff = diff; bestTime = t; }
            }
            clipTime = bestTime;
        }

        Repaint();
    }

    private void OnEditorUpdate()
    {
        if (selectedPart != HumanoidBodyPart.None && targetAvatarRoot != null && boneMapping != null)
        {
            return;
        }

        HumanBodyBones boneType = GetBoneType(selectedPart);
        if (boneMapping.TryGetValue(boneType, out Transform boneTransform))
        {
            if (boneTransform.localScale != lastKnownScale)
            {
                scaleValues[selectedPart] = boneTransform.localScale;
                lastKnownScale = boneTransform.localScale;
                Repaint();
            }

            if (CanRotate(selectedPart) && boneTransform.localRotation != lastKnownRotation)
            {
                rotationValues[selectedPart] = boneTransform.localRotation;
                lastKnownRotation = boneTransform.localRotation;
                Repaint();
            }

            if (boneTransform.localPosition != lastKnownPosition)
            {
                positionValues[selectedPart] = boneTransform.localPosition;
                lastKnownPosition = boneTransform.localPosition;
                Repaint();
            }
            
            if (currentMode == EditorMode.ShapeKey && _realtimePreview && targetAvatarRoot != null && animationClip != null)
            {
                PreviewPoseNoUndo();
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.Repaint();
                }
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
                if (!scaleValues.ContainsKey(part)) scaleValues.Add(part, Vector3.one);
                else scaleValues[part] = Vector3.one;
                
                if (!rotationValues.ContainsKey(part)) rotationValues.Add(part, Quaternion.identity);
                else rotationValues[part] = Quaternion.identity;

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
                positionValues[part] = t.localPosition;
            }
        }
    }

    private void ForceUpdateScene(UnityEngine.Object obj)
    {
        if (obj == null) return;
        EditorUtility.SetDirty(obj);
        
        #if UNITY_EDITOR
        if (PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }
        #endif
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
        
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(UI_TEXT[0], targetAvatarRoot, typeof(GameObject), true);

        if (EditorGUI.EndChangeCheck())
        {
            if (targetAvatarRoot != null)
            {
                boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
                LoadCurrentValues();
                selectedPart = HumanoidBodyPart.None;
                _avatarInfo = CalcAvatarInfo(targetAvatarRoot);
                _avatarInfoReady = true;
            }
            else
            {
                boneMapping = null;
                selectedPart = HumanoidBodyPart.None;
                InitializeValues();
                _avatarInfoReady = false;
            }
        }

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        var _prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("↺", GUILayout.Width(28), GUILayout.Height(18)))
        {
            boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
            LoadCurrentValues();
            selectedPart = HumanoidBodyPart.None;
            _avatarInfo = CalcAvatarInfo(targetAvatarRoot);
            _avatarInfoReady = true;
        }
        GUI.backgroundColor = _prevBg;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (_avatarInfoReady)
            DrawAvatarInfo();

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
        
        EditorGUI.BeginDisabledGroup(selectedPresetIndex == -1);
        
        if (GUILayout.Button(UI_TEXT[28]))
        {
            LoadSelectedPreset(true, true, true);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(UI_TEXT[39]))
        {
            LoadSelectedPreset(true, false, false);
        }
        if (GUILayout.Button(UI_TEXT[40]))
        {
            LoadSelectedPreset(false, true, false);
        }
        if (GUILayout.Button(UI_TEXT[43]))
        {
            LoadSelectedPreset(false, false, true);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();

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

            GUILayout.Space(10);
            GUILayout.Label(UI_TEXT[41], EditorStyles.boldLabel);
            Vector3 position = GetPartPosition(selectedPart);
            
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = EditorGUILayout.Vector3Field(UI_TEXT[42], position);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartPosition(newPosition);
                ArmatureScalerLogic.ApplyPosition(boneMapping, MapToHumanBodyBones(positionValues));
            }
            
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
        // 타겟 변경 시 자동 스냅샷 (초기 T포즈 기억용)
        if (targetAvatarRoot != _prevSnapshotTarget)
        {
            _prevSnapshotTarget = targetAvatarRoot;
            if (targetAvatarRoot != null) TakeSnapshot();
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(SK_TEXT[0], EditorStyles.boldLabel);
        GUILayout.Space(3);
        
        EditorGUILayout.BeginHorizontal();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(SK_TEXT[1], targetAvatarRoot, typeof(GameObject), true);

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        var _prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("↺", GUILayout.Width(28), GUILayout.Height(18)))
        {
            if (targetAvatarRoot != null)
            {
                TakeSnapshot();
                Debug.Log("[Avi Editor] 스냅샷(초기 포즈/쉐이프키)을 새로고침했습니다.");
            }
        }
        GUI.backgroundColor = _prevBg;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        animationClip = (AnimationClip)EditorGUILayout.ObjectField(SK_TEXT[2], animationClip, typeof(AnimationClip), false);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || animationClip == null);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(SK_TEXT[3], EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        if (animationClip != null)
            GUILayout.Label($"{clipTime:F3}s / {animationClip.length:F3}s",
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
        
        GUILayout.Space(4);

        // ─── [핵심 수정 부분] 실시간 미리보기 버튼 ───
        var _prevRtBg = GUI.backgroundColor;
        // 활성화 상태에 따라 색상 피드백 (민트색 / 어두운색)
        GUI.backgroundColor = _realtimePreview ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.35f, 0.35f, 0.38f);
        
        var rtStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 10,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = _realtimePreview ? Color.white : new Color(0.7f, 0.7f, 0.7f) },
            hover     = { textColor = Color.white },
        };

        // 버튼 클릭 시 SetRealtimePreview를 호출하여 즉시 포즈 반영
        if (GUILayout.Button(SK_TEXT[13], rtStyle, GUILayout.Height(18)))
        {
            SetRealtimePreview(!_realtimePreview);
        }
        GUI.backgroundColor = _prevRtBg;
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(3);

        // ─── [핵심 수정 부분] 타임라인 슬라이더 ───
        EditorGUI.BeginChangeCheck();
        clipTime = EditorGUILayout.Slider(clipTime, 0f, animationClip != null ? animationClip.length : 1f);
        if (EditorGUI.EndChangeCheck())
        {
            // 슬라이더를 움직일 때 실시간 미리보기가 켜져 있다면 즉시 포즈 업데이트
            ApplyShapeKeys();
            if (_realtimePreview) 
            {
                PreviewPoseNoUndo();
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();

        // ... (이후 Apply 버튼 및 복원 버튼 로직은 기존과 동일)
        GUILayout.Space(8);
        // (이하 생략)
    }
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
            ForceUpdateScene(smr);
        }
    }

    // --- 수정한 ApplyPose 부분 ---
    private void ApplyPose()
    {
        if (targetAvatarRoot == null || animationClip == null) return;

        // Undo 기록을 타겟 아바타 하위 전체로 잡습니다. (SampleAnimation이 많은 본을 수정할 수 있으므로)
        Undo.RegisterFullObjectHierarchyUndo(targetAvatarRoot, "Avi Editor Freeze Pose");

        // 1. 현재 쉐이프키 상태 백업
        var smrs = targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var backupWeights = new Dictionary<SkinnedMeshRenderer, float[]>();
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null) continue;
            int count = smr.sharedMesh.blendShapeCount;
            var weights = new float[count];
            for (int i = 0; i < count; i++) weights[i] = smr.GetBlendShapeWeight(i);
            backupWeights[smr] = weights;
        }

        // 2. Unity 내장 기능으로 애니메이션 전체 적용 
        // (이 기능을 쓰면 휴머노이드 머슬 데이터가 포함된 포즈도 정상적으로 적용됩니다)
        animationClip.SampleAnimation(targetAvatarRoot, clipTime);

        // 3. 쉐이프키 상태 복원 (포즈는 남기고 쉐이프키는 SampleAnimation 이전 상태로 되돌림)
        foreach (var kvp in backupWeights)
        {
            var smr = kvp.Key;
            var weights = kvp.Value;
            for (int i = 0; i < weights.Length; i++)
            {
                smr.SetBlendShapeWeight(i, weights[i]);
            }
            ForceUpdateScene(smr);
        }

        // 전체 Transform 변경사항 씬 반영
        var allTransforms = targetAvatarRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            ForceUpdateScene(t);
        }
    }

    private bool ClipHasPoseData()
    {
        if (animationClip == null) return false;
        foreach (var b in AnimationUtility.GetCurveBindings(animationClip))
            if (!b.propertyName.StartsWith("blendShape.")) return true;
        return false;
    }

    private void PreviewPoseNoUndo()
    {
        if (targetAvatarRoot == null || animationClip == null) return;
        if (!ClipHasPoseData()) return;

        // 현재 쉐이프키 상태 백업
        var smrs = targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var backup = new Dictionary<SkinnedMeshRenderer, float[]>();
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null) continue;
            int cnt = smr.sharedMesh.blendShapeCount;
            var w = new float[cnt];
            for (int i = 0; i < cnt; i++) w[i] = smr.GetBlendShapeWeight(i);
            backup[smr] = w;
        }

        // Undo 없이 전체 애니메이션 적용 (포즈)
        animationClip.SampleAnimation(targetAvatarRoot, clipTime);

        // 쉐이프키 복원 (포즈만 남김)
        foreach (var kvp in backup)
        {
            for (int i = 0; i < kvp.Value.Length; i++)
                kvp.Key.SetBlendShapeWeight(i, kvp.Value[i]);
            ForceUpdateScene(kvp.Key);
        }
        foreach (var t in targetAvatarRoot.GetComponentsInChildren<Transform>(true))
            ForceUpdateScene(t);
    }

    private void RestoreTransformsOnly()
    {
        if (targetAvatarRoot == null || !_hasSnapshot) return;

        if (PrefabUtility.IsPartOfPrefabInstance(targetAvatarRoot))
        {
            foreach (var tr in targetAvatarRoot.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RevertObjectOverride(tr, InteractionMode.UserAction);
        }
        else
        {
            foreach (var kvp in _snapTransforms)
            {
                if (kvp.Key == null) continue;
                kvp.Key.localPosition = kvp.Value.pos;
                kvp.Key.localRotation = kvp.Value.rot;
                kvp.Key.localScale    = kvp.Value.scl;
                ForceUpdateScene(kvp.Key);
            }
        }

        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.Repaint();
    }

    private void SetRealtimePreview(bool on)
    {
        _realtimePreview = on;
        if (on)
        {
            // 버튼 클릭 즉시 현재 슬라이더 시간(clipTime)의 포즈를 적용
            PreviewPoseNoUndo();
        }
        else
        {
            // 미리보기를 끌 때는 스냅샷이나 프리팹 원래 상태로 복구
            RestoreTransformsOnly();
            ApplyShapeKeys();
        }
    }

    // 아바타 할당 시 초기 T포즈 스냅샷 저장
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

    private void RestoreToOriginal()
    {
        if (targetAvatarRoot == null) return;

        bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(targetAvatarRoot);

        if (isPrefab)
        {
            foreach (var smr in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                PrefabUtility.RevertObjectOverride(smr, InteractionMode.UserAction);
            }
            foreach (var tr in targetAvatarRoot.GetComponentsInChildren<Transform>(true))
            {
                PrefabUtility.RevertObjectOverride(tr, InteractionMode.UserAction);
            }
        }
        else
        {
            foreach (var smr in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                Undo.RecordObject(smr, "Avi Editor Restore SMR");
                int count = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < count; i++)
                {
                    smr.SetBlendShapeWeight(i, 0f);
                }
                ForceUpdateScene(smr);
            }

            foreach (var kvp in _snapTransforms)
            {
                if (kvp.Key == null) continue;
                Undo.RecordObject(kvp.Key, "Avi Editor Restore Pose");
                kvp.Key.localPosition = kvp.Value.pos;
                kvp.Key.localRotation = kvp.Value.rot;
                kvp.Key.localScale    = kvp.Value.scl;
                ForceUpdateScene(kvp.Key);
            }
        }

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Repaint();
        }

        clipTime = 0f;
        GUI.FocusControl(null);
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
                    "↩  원본 초기화 (초기 포즈/0)",    // 10
                    "원본 상태(프리팹 또는 T포즈/쉐이프키 0)로 복원했습니다.", // 11
                    "슬라이더로 미리보기, 버튼으로 씬에 적용합니다.", // 12
                    "실시간 미리보기",                            // 13
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
                    "↩  元に初期化 (初期ポーズ/0)",
                    "元の状態(プレハブまたはTポーズ/シェイプキー0)に復元しました。",
                    "スライダーでプレビュー、ボタンでシーンに適用します。",
                    "リアルタイムプレビュー",
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
                    "↩  Initialize Original (T-Pose/0)",
                    "Restored to original state (Prefab or T-Pose/ShapeKeys 0).",
                    "Slide to preview. Buttons apply to scene.",
                    "Real-time Preview",
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
                lastKnownPosition = boneTransform.localPosition;
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
    
    // ─── 아바타 정보 계산/표시 ───

    private AvatarInfo CalcAvatarInfo(GameObject root)
    {
        var info = new AvatarInfo();
        if (root == null) return info;

        var smrs     = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var mfs      = root.GetComponentsInChildren<MeshFilter>(true);
        var allBones = new HashSet<Transform>();
        var matSet   = new HashSet<Material>();
        var seenMeshes = new HashSet<int>(); // 중복 메쉬 방지

        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null) continue;
            info.skinnedMeshCount++;
            info.totalVertices    += smr.sharedMesh.vertexCount;
            info.totalTriangles   += (int)(smr.sharedMesh.triangles.LongLength / 3);
            info.totalBlendShapes += smr.sharedMesh.blendShapeCount;

            // 메쉬 메모리: 버텍스·인덱스 데이터 기반 추정 (Profiler는 RAM+VRAM 합산으로 부정확)
            if (seenMeshes.Add(smr.sharedMesh.GetInstanceID()))
                info.meshMemoryBytes += CalcMeshMemory(smr.sharedMesh);

            foreach (var m in smr.sharedMaterials) if (m != null) matSet.Add(m);
            if (smr.bones != null) foreach (var b in smr.bones) if (b != null) allBones.Add(b);
        }

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            info.staticMeshCount++;
            info.totalVertices  += mf.sharedMesh.vertexCount;
            info.totalTriangles += (int)(mf.sharedMesh.triangles.LongLength / 3);

            if (seenMeshes.Add(mf.sharedMesh.GetInstanceID()))
                info.meshMemoryBytes += CalcMeshMemory(mf.sharedMesh);

            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null) foreach (var m in mr.sharedMaterials) if (m != null) matSet.Add(m);
        }

        info.meshCount     = info.skinnedMeshCount + info.staticMeshCount;
        info.materialCount = matSet.Count;
        info.boneCount     = allBones.Count;

        // 텍스처 메모리: DiNeTextureVRAM 공유 유틸 사용 (Material Tool과 동일 계산)
        info.textureMemoryBytes = DiNeTextureVRAM.CalcAvatarTextureVRAM(root, out int texCount);
        info.textureCount       = texCount;

        return info;
    }

    /// <summary>
    /// 메쉬의 GPU 메모리 사용량을 버텍스/인덱스 데이터 기반으로 추정합니다.
    /// </summary>
    private static long CalcMeshMemory(Mesh mesh)
    {
        if (mesh == null) return 0;
        long bytes = 0;

        // 버텍스 어트리뷰트별 바이트 크기 합산
        foreach (var attr in mesh.GetVertexAttributes())
        {
            int compSize = attr.format switch
            {
                UnityEngine.Rendering.VertexAttributeFormat.Float32  => 4,
                UnityEngine.Rendering.VertexAttributeFormat.Float16  => 2,
                UnityEngine.Rendering.VertexAttributeFormat.UNorm8   => 1,
                UnityEngine.Rendering.VertexAttributeFormat.SNorm8   => 1,
                UnityEngine.Rendering.VertexAttributeFormat.UNorm16  => 2,
                UnityEngine.Rendering.VertexAttributeFormat.SNorm16  => 2,
                UnityEngine.Rendering.VertexAttributeFormat.UInt8    => 1,
                UnityEngine.Rendering.VertexAttributeFormat.SInt8    => 1,
                UnityEngine.Rendering.VertexAttributeFormat.UInt16   => 2,
                UnityEngine.Rendering.VertexAttributeFormat.SInt16   => 2,
                UnityEngine.Rendering.VertexAttributeFormat.UInt32   => 4,
                UnityEngine.Rendering.VertexAttributeFormat.SInt32   => 4,
                _ => 4
            };
            bytes += (long)compSize * attr.dimension * mesh.vertexCount;
        }

        // 인덱스 버퍼 (16bit or 32bit)
        bool use32 = mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32;
        bytes += (long)(use32 ? 4 : 2) * mesh.triangles.Length;

        return bytes;
    }

    private void DrawAvatarInfo()
    {
        EditorGUILayout.BeginVertical("box");
        _avatarInfoFoldout = EditorGUILayout.Foldout(_avatarInfoFoldout,
            language == LanguagePreset.Korean  ? "아바타 정보"
          : language == LanguagePreset.Japanese ? "アバター情報" : "Avatar Info", true);

        if (!_avatarInfoFoldout) { EditorGUILayout.EndVertical(); return; }

        var i = _avatarInfo;

        var labelStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
        var valueStyle = new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.MiddleRight, richText = true };

        void Row(string label, string value, Color col)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            string colored = $"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{value}</color>";
            GUILayout.Label(colored, valueStyle);
            EditorGUILayout.EndHorizontal();
        }

        string FmtBytes(long bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            if (bytes >= 1024)        return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }

        Color WarnColor(int val, int warn, int error) =>
            val >= error ? new Color(1f, 0.35f, 0.35f) :
            val >= warn  ? new Color(1f, 0.8f, 0.2f)   :
            new Color(0.5f, 1f, 0.6f);

        Color WarnColorL(long val, long warn, long error) =>
            val >= error ? new Color(1f, 0.35f, 0.35f) :
            val >= warn  ? new Color(1f, 0.8f, 0.2f)   :
            new Color(0.5f, 1f, 0.6f);

        GUILayout.Space(2);

        string meshLabel = language == LanguagePreset.Korean  ? "메쉬 수"
                         : language == LanguagePreset.Japanese ? "メッシュ数" : "Meshes";
        Row(meshLabel, $"{i.meshCount}  (Skinned {i.skinnedMeshCount} / Static {i.staticMeshCount})",
            WarnColor(i.meshCount, 8, 16));

        string vtxLabel = language == LanguagePreset.Korean  ? "버텍스"
                        : language == LanguagePreset.Japanese ? "頂点数" : "Vertices";
        Row(vtxLabel, $"{i.totalVertices:N0}", WarnColor(i.totalVertices, 70000, 120000));

        string triLabel = language == LanguagePreset.Korean  ? "폴리곤 (tri)"
                        : language == LanguagePreset.Japanese ? "ポリゴン" : "Polygons (tri)";
        Row(triLabel, $"{i.totalTriangles:N0}", WarnColor(i.totalTriangles, 70000, 120000));

        string bsLabel = language == LanguagePreset.Korean  ? "블렌드쉐이프"
                       : language == LanguagePreset.Japanese ? "ブレンドシェイプ" : "Blend Shapes";
        Row(bsLabel, $"{i.totalBlendShapes}", WarnColor(i.totalBlendShapes, 200, 500));

        string boneLabel = language == LanguagePreset.Korean  ? "본 수"
                         : language == LanguagePreset.Japanese ? "ボーン数" : "Bones";
        Row(boneLabel, $"{i.boneCount}", WarnColor(i.boneCount, 256, 512));

        string matLabel = language == LanguagePreset.Korean  ? "머티리얼"
                        : language == LanguagePreset.Japanese ? "マテリアル" : "Materials";
        Row(matLabel, $"{i.materialCount}", WarnColor(i.materialCount, 8, 16));

        string texLabel = language == LanguagePreset.Korean  ? "텍스처"
                        : language == LanguagePreset.Japanese ? "テクスチャ" : "Textures";
        Row(texLabel, $"{i.textureCount}", WarnColor(i.textureCount, 20, 40));

        GUILayout.Space(3);

        string meshMemLabel = language == LanguagePreset.Korean  ? "메쉬 메모리"
                            : language == LanguagePreset.Japanese ? "メッシュメモリ" : "Mesh Memory";
        Row(meshMemLabel, FmtBytes(i.meshMemoryBytes),
            WarnColorL(i.meshMemoryBytes, 20 * 1024 * 1024, 50 * 1024 * 1024));

        string texMemLabel = language == LanguagePreset.Korean  ? "텍스처 메모리"
                           : language == LanguagePreset.Japanese ? "テクスチャメモリ" : "Texture Memory";
        Row(texMemLabel, FmtBytes(i.textureMemoryBytes),
            WarnColorL(i.textureMemoryBytes, 60 * 1024 * 1024, 100 * 1024 * 1024));

        long totalMem = i.meshMemoryBytes + i.textureMemoryBytes;
        string totalMemLabel = language == LanguagePreset.Korean  ? "총 메모리 (추정)"
                             : language == LanguagePreset.Japanese ? "合計メモリ (推定)" : "Total Memory (est.)";
        Row($"<b>{totalMemLabel}</b>", $"<b>{FmtBytes(totalMem)}</b>",
            WarnColorL(totalMem, 80 * 1024 * 1024, 150 * 1024 * 1024));

        GUILayout.Space(2);
        EditorGUILayout.EndVertical();
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

        Vector2 headC = new Vector2(cx, top + 35);
        DrawCircleOutline(headC, 28, lc, lw);

        float neckTop = top + 63;
        float neckBot = top + 80;
        DrawLineAA(new Vector2(cx - 8, neckTop), new Vector2(cx - 8, neckBot), lc, lw);
        DrawLineAA(new Vector2(cx + 8, neckTop), new Vector2(cx + 8, neckBot), lc, lw);

        float shY = top + 85;
        float shW = 85;
        DrawLineAA(new Vector2(cx - 8, neckBot), new Vector2(cx - shW, shY + 5), lc, lw);
        DrawLineAA(new Vector2(cx + 8, neckBot), new Vector2(cx + shW, shY + 5), lc, lw);

        float chestY = top + 130;
        float chestW = 52;
        float waistY = top + 200;
        float waistW = 34;
        float hipY = top + 260;
        float hipW = 55;

        DrawLineAA(new Vector2(cx - shW, shY + 5), new Vector2(cx - chestW, chestY), lc, lw);
        DrawLineAA(new Vector2(cx + shW, shY + 5), new Vector2(cx + chestW, chestY), lc, lw);

        DrawLineAA(new Vector2(cx - chestW, chestY), new Vector2(cx - waistW, waistY), lc, lw);
        DrawLineAA(new Vector2(cx + chestW, chestY), new Vector2(cx + waistW, waistY), lc, lw);

        DrawLineAA(new Vector2(cx - waistW, waistY), new Vector2(cx - hipW, hipY), lc, lw);
        DrawLineAA(new Vector2(cx + waistW, waistY), new Vector2(cx + hipW, hipY), lc, lw);

        DrawLineAA(new Vector2(cx - hipW, hipY), new Vector2(cx - 22, hipY + 20), lc, lw);
        DrawLineAA(new Vector2(cx + hipW, hipY), new Vector2(cx + 22, hipY + 20), lc, lw);

        DrawLineAA(new Vector2(cx - 18, chestY - 10), new Vector2(cx - 25, chestY + 8), lc, lw);
        DrawLineAA(new Vector2(cx - 25, chestY + 8), new Vector2(cx - 15, chestY + 18), lc, lw);
        DrawLineAA(new Vector2(cx + 18, chestY - 10), new Vector2(cx + 25, chestY + 8), lc, lw);
        DrawLineAA(new Vector2(cx + 25, chestY + 8), new Vector2(cx + 15, chestY + 18), lc, lw);

        float elbowY = top + 200;
        float handY = top + 290;
        float armOffX = 30;
        DrawLineAA(new Vector2(cx - shW, shY + 5), new Vector2(cx - shW - armOffX, elbowY), lc, lw);
        DrawLineAA(new Vector2(cx - shW - armOffX, elbowY), new Vector2(cx - shW - armOffX - 18, handY), lc, lw);
        DrawLineAA(new Vector2(cx + shW, shY + 5), new Vector2(cx + shW + armOffX, elbowY), lc, lw);
        DrawLineAA(new Vector2(cx + shW + armOffX, elbowY), new Vector2(cx + shW + armOffX + 18, handY), lc, lw);

        float legSplit = 22;
        float kneeY = top + 410;
        float footY = top + 550;
        DrawLineAA(new Vector2(cx - legSplit, hipY + 20), new Vector2(cx - legSplit - 12, kneeY), lc, lw);
        DrawLineAA(new Vector2(cx - legSplit - 12, kneeY), new Vector2(cx - legSplit - 8, footY), lc, lw);
        DrawLineAA(new Vector2(cx + legSplit, hipY + 20), new Vector2(cx + legSplit + 12, kneeY), lc, lw);
        DrawLineAA(new Vector2(cx + legSplit + 12, kneeY), new Vector2(cx + legSplit + 8, footY), lc, lw);

        DrawLineAA(new Vector2(cx - legSplit - 8, footY), new Vector2(cx - legSplit - 22, footY + 18), lc, lw);
        DrawLineAA(new Vector2(cx + legSplit + 8, footY), new Vector2(cx + legSplit + 22, footY + 18), lc, lw);

        float r = 12;
        float rs = 8; 

        DrawJointButton(HumanoidBodyPart.Head, headC, r);
        DrawJointButton(HumanoidBodyPart.Neck, new Vector2(cx, neckTop + 8), rs);
        DrawJointButton(HumanoidBodyPart.Torso, new Vector2(cx, chestY - 20), r);
        DrawJointButton(HumanoidBodyPart.Spine, new Vector2(cx, waistY), r);
        DrawJointButton(HumanoidBodyPart.Hips, new Vector2(cx, hipY + 5), r);

        DrawJointButton(HumanoidBodyPart.LeftShoulder, new Vector2(cx - shW * 0.52f, shY + 2), rs);
        DrawJointButton(HumanoidBodyPart.LeftArm, new Vector2(cx - shW, shY + 5), r);
        DrawJointButton(HumanoidBodyPart.LeftLowerArm, new Vector2(cx - shW - armOffX, elbowY), r);
        DrawJointButton(HumanoidBodyPart.LeftHand, new Vector2(cx - shW - armOffX - 18, handY), r);

        DrawJointButton(HumanoidBodyPart.RightShoulder, new Vector2(cx + shW * 0.52f, shY + 2), rs);
        DrawJointButton(HumanoidBodyPart.RightArm, new Vector2(cx + shW, shY + 5), r);
        DrawJointButton(HumanoidBodyPart.RightLowerArm, new Vector2(cx + shW + armOffX, elbowY), r);
        DrawJointButton(HumanoidBodyPart.RightHand, new Vector2(cx + shW + armOffX + 18, handY), r);

        DrawJointButton(HumanoidBodyPart.LeftLeg, new Vector2(cx - legSplit - 5, hipY + 40), r);
        DrawJointButton(HumanoidBodyPart.LeftLowerLeg, new Vector2(cx - legSplit - 12, kneeY), r);
        DrawJointButton(HumanoidBodyPart.LeftFoot, new Vector2(cx - legSplit - 15, footY + 8), r);

        DrawJointButton(HumanoidBodyPart.RightLeg, new Vector2(cx + legSplit + 5, hipY + 40), r);
        DrawJointButton(HumanoidBodyPart.RightLowerLeg, new Vector2(cx + legSplit + 12, kneeY), r);
        DrawJointButton(HumanoidBodyPart.RightFoot, new Vector2(cx + legSplit + 15, footY + 8), r);

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
                    "포지션 조절", "포지션", "포지션만 불러오기", 
                    "목", "왼쪽 어깨", "오른쪽 어깨" 
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
                    "位置調整", "位置", "位置のみロード", 
                    "首", "左肩", "右肩" 
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
                    "Position Adjustment", "Position", "Load Position Only", 
                    "Neck", "Left Shoulder", "Right Shoulder" 
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
            
            foreach (var kvp in scaleValues)
            {
                newPreset.scales.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));
            }

            foreach (var kvp in rotationValues)
            {
                if(CanRotate(kvp.Key))
                {
                    newPreset.rotations.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableQuaternion(kvp.Value));
                }
            }

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
        if (targetAvatarRoot != _prevExprTarget)
        {
            _prevExprTarget = targetAvatarRoot;
            RefreshBodySmr();
            _facePreviewDirty = true;
        }

        var prevBg = GUI.backgroundColor;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(language == LanguagePreset.Korean  ? "대상 설정"
                                 : language == LanguagePreset.Japanese ? "対象設定" : "Target", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
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

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        var _prevBgExpr = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("↺", GUILayout.Width(28), GUILayout.Height(18)))
        {
            RefreshBodySmr();
            _facePreviewDirty = true;
            Debug.Log("[Avi Editor] 표정 타겟 메쉬와 FX 컨트롤러를 새로고침했습니다.");
        }
        GUI.backgroundColor = _prevBgExpr;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

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

        DrawFacePreview();
        GUILayout.Space(5);

        _exprMainScroll = EditorGUILayout.BeginScrollView(_exprMainScroll);

        DrawExpressionClipSection(prevBg);
        GUILayout.Space(5);

        DrawExpressionFxSection(prevBg);
        GUILayout.Space(5);

        DrawExpressionShapeKeys(prevBg);

        EditorGUILayout.EndScrollView();
        GUI.backgroundColor = prevBg;
    }

    private void DrawFacePreview()
    {
        float size = Mathf.Min(position.width - 20f, 260f);
        Rect previewRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
        previewRect.x = (position.width - size) * 0.5f;

        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
        EditorGUI.DrawRect(previewRect, bgCol);

        if (Event.current.type == EventType.Repaint)
        {
            RenderFacePreview((int)size);
            if (_faceRT != null)
                GUI.DrawTexture(previewRect, _faceRT, ScaleMode.ScaleToFit, true);
            _facePreviewDirty = false;
        }

        if (Event.current.type == EventType.Used)
            _facePreviewDirty = true;
    }

    private void RenderFacePreview(int size)
    {
        if (_bodySmr == null || targetAvatarRoot == null) return;
        if (size <= 0) return;

        if (_faceRT == null || _faceRT.width != size)
        {
            if (_faceRT != null) _faceRT.Release();
            _faceRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            _faceRT.antiAliasing = 2;
            _faceRT.Create();
        }

        Vector3 headPos = targetAvatarRoot.transform.position + Vector3.up * 1.5f;
        if (boneMapping != null && boneMapping.TryGetValue(HumanBodyBones.Head, out Transform headBone))
            headPos = headBone.position;

        var camGo = new GameObject("__AviExprPreviewCam__") { hideFlags = HideFlags.HideAndDontSave };
        var cam   = camGo.AddComponent<Camera>();
        cam.backgroundColor    = new Color(0, 0, 0, 0);
        cam.clearFlags         = CameraClearFlags.SolidColor;
        cam.orthographic       = false;
        cam.fieldOfView        = 22f;
        cam.nearClipPlane      = 0.15f;  
        cam.farClipPlane       = 100f;
        cam.targetTexture      = _faceRT;
        cam.cullingMask        = -1;
        cam.enabled            = false;

        Vector3 avatarForward = targetAvatarRoot.transform.forward;
        cam.transform.position = headPos + avatarForward * 0.45f + Vector3.up * 0.04f;
        cam.transform.LookAt(headPos + Vector3.up * 0.04f);

        cam.Render();
        cam.targetTexture = null;
        DestroyImmediate(camGo);
    }

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

        if (_exprClip == null)
        {
            _exprNewClipName = EditorGUILayout.TextField(
                language == LanguagePreset.Korean ? "클립 이름" : language == LanguagePreset.Japanese ? "クリップ名" : "Clip Name",
                _exprNewClipName);
        }

        // 제스처 쉐이프키 포함 옵션
        string gestureKeyLabel = language == LanguagePreset.Korean  ? "재스쳐 클립의 쉐이프키 0값 포함"
                               : language == LanguagePreset.Japanese ? "ジェスチャークリップのシェイプキー0値を含める"
                               : "Include Gesture ShapeKeys at 0";
        string gestureKeyTooltip = language == LanguagePreset.Korean
            ? "FX 컨트롤러의 재스쳐 애니메이션에 사용된 쉐이프키를 0값으로 함께 저장합니다.\n표정이 겹쳐 일그러지는 현상을 방지합니다."
            : language == LanguagePreset.Japanese
            ? "FXコントローラーのジェスチャーアニメーションで使用されているシェイプキーを0値で一緒に保存します。\n表情が重なって歪む現象を防ぎます。"
            : "Also saves shapekeys used in gesture animations at value 0.\nPrevents expression blending artifacts.";
        _includeGestureKeys = EditorGUILayout.ToggleLeft(
            new GUIContent(gestureKeyLabel, gestureKeyTooltip),
            _includeGestureKeys);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.25f, 0.65f, 0.60f);
        EditorGUI.BeginDisabledGroup(_exprClip == null);
        if (GUILayout.Button(overwrite, GUILayout.ExpandWidth(true), GUILayout.Height(28)))
            SaveExpressionClip(overwriteExisting: true);
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(saveAsNew, GUILayout.ExpandWidth(true), GUILayout.Height(28)))
            SaveExpressionClip(overwriteExisting: false);

        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

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

            EditorGUI.BeginChangeCheck();
            _exprFxController = (UnityEditor.Animations.AnimatorController)EditorGUILayout.ObjectField(
                "FX Controller", _exprFxController,
                typeof(UnityEditor.Animations.AnimatorController), false);
            if (EditorGUI.EndChangeCheck()) _exprFxStateSel = -1;

            if (_exprFxController != null)
            {
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

                            GUI.backgroundColor = sel ? new Color(0.30f, 0.82f, 0.76f) : prevBg;
                            string stateLabel = clips[i].state != null ? clips[i].state.name : "(no state)";
                            if (GUILayout.Button(stateLabel, GUILayout.ExpandWidth(true), GUILayout.Height(22)))
                            {
                                if (sel)
                                {
                                    _exprFxStateSel    = -1;
                                    _exprFxPreviewMode = false;
                                    RestoreWorkingValues();
                                }
                                else
                                {
                                    if (!_exprFxPreviewMode) SaveWorkingValues();
                                    _exprFxStateSel    = i;
                                    _exprFxPreviewMode = true;
                                    PreviewFxClip(clips[i].clip);
                                }
                            }
                            GUI.backgroundColor = prevBg;

                            string clipName = clips[i].clip != null ? clips[i].clip.name : "(empty)";
                            GUILayout.Label(clipName, clipNameStyle, GUILayout.Width(110));

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

        _exprShapeSearch = EditorGUILayout.TextField("", _exprShapeSearch, EditorStyles.toolbarSearchField);
        GUILayout.Space(3);

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

        for (int i = 0; i < cnt; i++)
        {
            _bodySmr.SetBlendShapeWeight(i, 0f);
            if (_exprShapeValues != null && i < _exprShapeValues.Length)
                _exprShapeValues[i] = 0f;
        }

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

    private void RefreshBodySmr()
    {
        _bodySmr = null;
        _exprShapeValues = null;
        if (targetAvatarRoot == null) return;

        if (boneMapping == null)
            boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);

        foreach (var smr in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Body")
            {
                _bodySmr = smr;
                break;
            }
        }

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

        _exprFxController = null;
        _exprFxStateSel   = -1;
        AutoFindFxController();
    }

    private void LoadExpressionFromClip()
    {
        if (_exprClip == null || _bodySmr == null || _bodySmr.sharedMesh == null) return;

        Undo.RecordObject(_bodySmr, "Load Expression Clip");

        int cnt = _bodySmr.sharedMesh.blendShapeCount;
        if (_exprShapeValues == null || _exprShapeValues.Length != cnt)
            _exprShapeValues = new float[cnt];

        foreach (var b in AnimationUtility.GetCurveBindings(_exprClip))
        {
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            var curve = AnimationUtility.GetEditorCurve(_exprClip, b);
            if (curve == null) continue;

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

    private void SaveExpressionClip(bool overwriteExisting = false)
    {
        if (_bodySmr == null || _bodySmr.sharedMesh == null) return;

        AnimationClip clip;
        string path;

        if (overwriteExisting && _exprClip != null)
        {
            clip = _exprClip;
            path = AssetDatabase.GetAssetPath(clip);
        }
        else
        {
            clip = new AnimationClip();
            string dir = "Assets/Di Ne/Expressions";
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            string baseName = string.IsNullOrEmpty(_exprNewClipName) ? "New Expression" : _exprNewClipName;
            if (!overwriteExisting && _exprClip != null)
                baseName = _exprClip.name;
            path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{baseName}.anim");
        }

        Undo.RecordObject(clip, "Save Expression Clip");
        clip.ClearCurves();

        // 제스처 클립에 기록된 쉐이프키 이름 수집 (0값 포함 옵션)
        HashSet<string> gestureShapeKeyNames = (_includeGestureKeys && _exprFxController != null)
            ? CollectGestureShapeKeys(_exprFxController)
            : new HashSet<string>();

        string smrPath = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        int cnt = _bodySmr.sharedMesh.blendShapeCount;
        for (int i = 0; i < cnt; i++)
        {
            float val = _exprShapeValues != null ? _exprShapeValues[i] : _bodySmr.GetBlendShapeWeight(i);
            string shapeName = _bodySmr.sharedMesh.GetBlendShapeName(i);

            // val이 0이면서 제스처 클립에 포함된 쉐이프키도 0값으로 기록
            if (val == 0f && !gestureShapeKeyNames.Contains(shapeName)) continue;

            string propName = "blendShape." + shapeName;
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

    /// <summary>
    /// FX 컨트롤러의 모든 애니메이션 클립에서 blendShape 프로퍼티 이름(shapekey명)을 수집합니다.
    /// 값이 0이든 100이든 기록된 쉐이프키 이름을 전부 반환합니다.
    /// </summary>
    private HashSet<string> CollectGestureShapeKeys(UnityEditor.Animations.AnimatorController ctrl)
    {
        var result = new HashSet<string>();
        var visitedClips = new HashSet<int>();

        foreach (var layer in ctrl.layers)
        {
            CollectFromStateMachine(layer.stateMachine, result, visitedClips);
        }
        return result;
    }

    private void CollectFromStateMachine(
        UnityEditor.Animations.AnimatorStateMachine sm,
        HashSet<string> result,
        HashSet<int> visitedClips)
    {
        if (sm == null) return;

        foreach (var childState in sm.states)
        {
            var clip = childState.state.motion as AnimationClip;
            if (clip != null && visitedClips.Add(clip.GetInstanceID()))
                CollectShapeKeysFromClip(clip, result);
        }
        // 서브 스테이트 머신도 순회
        foreach (var sub in sm.stateMachines)
            CollectFromStateMachine(sub.stateMachine, result, visitedClips);
    }

    private void CollectShapeKeysFromClip(AnimationClip clip, HashSet<string> result)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(SkinnedMeshRenderer)) continue;
            if (!binding.propertyName.StartsWith("blendShape.")) continue;
            // "blendShape.ShapeName" → "ShapeName"
            string shapeName = binding.propertyName.Substring("blendShape.".Length);
            result.Add(shapeName);
        }
    }

    private void AutoFindFxController()
    {
        if (targetAvatarRoot == null) return;

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

                            string typeStr = typeProp.GetValue(layer).ToString();
                            if (typeStr != "FX") continue;

                            bool isDef = defProp != null && (bool)defProp.GetValue(layer);
                            if (isDef) continue;

                            var ctrl = animProp.GetValue(layer) as UnityEditor.Animations.AnimatorController;
                            if (ctrl != null) { _exprFxController = ctrl; return; }
                        }
                    }
                }
            }
        }

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

    private void ReplaceClipInFxLayer(UnityEditor.Animations.AnimatorController ctrl, string layerName, int stateIndex)
    {
        AnimationClip targetClip = _exprClip;
        if (targetClip == null)
        {
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