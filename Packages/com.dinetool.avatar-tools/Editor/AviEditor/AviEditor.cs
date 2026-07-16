using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;

public class ArmatureScalerEditor : EditorWindow
{
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    // ?????? ??????癲ル슢?꾤땟?????????
    private enum EditorMode { Armature, ShapeKey, Expression, ShapeKeyEditor }
    private enum ArmatureEditMode { DirectTransform, ModularAvatarScale }
    [SerializeField] private EditorMode currentMode = EditorMode.Armature;
    [SerializeField] private ArmatureEditMode armatureEditMode = ArmatureEditMode.DirectTransform;

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

    private string[] maPresetFiles;
    [SerializeField] private int selectedMaPresetIndex = -1;
    [SerializeField] private string selectedMaPresetName = "";
    [SerializeField] private Vector2 maPresetScrollPosition;
    [SerializeField] private List<string> maAdjustChildPositionParts = new List<string>();

    // ?????? ??ш끽維곻쭚?? ?嶺뚮㉡?€쾮???????
// ?????? Animation Freezer ??ш끽維????????
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private float clipTime = 0.0f;
    [SerializeField] private bool  _realtimePreview = false;
    private string[] SK_TEXT;

    // ???怨좊룴??(???⑤챷?????⑥ロ떘 ????щ빘???됰씭肄??T??????れ삀?節낆젂??獄?筌뤿뱶????낆뒩?戮⑤뭄????Β???
    private Dictionary<SkinnedMeshRenderer, float[]>                          _snapShapeKeys  = new Dictionary<SkinnedMeshRenderer, float[]>();
    private Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scl)> _snapTransforms = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
    private bool   _hasSnapshot;
    private GameObject _prevSnapshotTarget;

    // ?????? Expression ????ш끽維????????
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
    // ??筌?痢⑼┼???????ш낄援???????????
    [SerializeField] private bool    _includeGestureKeys  = true;

    // ?????? ShapeKey Editor ????ш끽維????????
    private SkinnedMeshRenderer   _skeSmr;
    private int                   _skeSubMode            = 0;  // 0=????궈?┼??뵯????琉왈?1=???쒓낯?????꾨탿
    // ????궈?癲ル슢?????琉왈?
    private List<DiNeSkeMixEntry> _skeMixEntries         = new List<DiNeSkeMixEntry>();
    private string                _skeNewName            = "";
    private Vector2               _skeMixScroll;
    // ???쒓낯?????꾨탿
    private int                   _skeModifySubMode      = 0;  // 0=?袁⑸즲??????Β???1=雅?퍔瑗????우Ŀ????
    private int                   _skeModifyIndex        = 0;
    private float                 _skeModifyScale        = 100f;
    private List<DiNeSkeMixEntry> _skeModifyMixEntries   = new List<DiNeSkeMixEntry>();
    private Vector2               _skeModifyMixScroll;
    // ???살씁??
    private string                _skeStatus             = "";
    private bool                  _skeStatusIsError      = false;
    private GameObject            _skePrevTarget;
    private Vector2               _skeOuterScroll;
    // ??ш끽諭욥걡??
    private RenderTexture         _skePreviewRT;
    private bool                  _skePreviewDirty       = true;
    private float[]               _skeOrigWeights;
    private bool                  _skeHasPreviewWeights  = false;
    // ??????ш낄援???域밸Ŧ遊얕짆??
    private string                _skeSearch             = "";
    private Vector2               _skeSKListScroll;

    [System.Serializable]
    private class DiNeSkeMixEntry
    {
        public int   index  = 0;
        public float weight = 100f;
    }

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
        windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        tabIcon    = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
        titleFont  = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
        titleContent = new GUIContent("Avi Editor", tabIcon);
        selectedButtonTex = MakeTex(1, 1, new Color(0.30f, 0.82f, 0.76f, 1f));
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
        if (_skePreviewRT != null)
        {
            _skePreviewRT.Release();
            DestroyImmediate(_skePreviewRT);
            _skePreviewRT = null;
        }
        SkeRestoreAndClearPreview();
    }

    private string Tr(string english, string korean, string japanese)
    {
        switch (language)
        {
            case LanguagePreset.Korean: return korean;
            case LanguagePreset.Japanese: return japanese;
            default: return english;
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
        if (selectedPart == HumanoidBodyPart.None || targetAvatarRoot == null || boneMapping == null)
        {
            return;
        }

        HumanBodyBones boneType = GetBoneType(selectedPart);
        if (TryGetLiveBoneTransform(boneType, out Transform boneTransform))
        {
            Vector3 liveScale = GetDisplayedScale(boneTransform);
            if (liveScale != lastKnownScale)
            {
                if (armatureEditMode == ArmatureEditMode.DirectTransform)
                    scaleValues[selectedPart] = liveScale;
                lastKnownScale = liveScale;
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
        }
    }

    private bool TryGetLiveBoneTransform(HumanBodyBones boneType, out Transform boneTransform)
    {
        boneTransform = null;
        if (boneMapping == null) return false;
        if (!boneMapping.TryGetValue(boneType, out var mappedTransform)) return false;
        if (mappedTransform == null)
        {
            boneMapping.Remove(boneType);
            return false;
        }

        boneTransform = mappedTransform;
        return true;
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

        string[] maGuids = AssetDatabase.FindAssets("t:MAScaleAdjusterPresetData", new[] { "Assets" });
        maPresetFiles = maGuids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
        selectedMaPresetIndex = -1;
        selectedMaPresetName = "";
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
            if (TryGetLiveBoneTransform(boneType, out Transform t))
            {
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
        GUILayout.Label(Tr(
                "Easily and safely edit your avatar's armature and shapekeys.",
                "아바타의 아마추어와 쉐이프키를 쉽고 안전하게 편집합니다.",
                "アバターのアーマチュアとシェイプキーを簡単かつ安全に編集します。"),
            new GUIStyle(EditorStyles.wordWrappedLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

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

        string[] modeLabels =
        {
            Tr("Armature", "아마추어", "アーマチュア"),
            Tr("Animation", "애니메이션", "アニメーション"),
            Tr("Expression", "표정", "表情"),
            Tr("Shape Key", "쉐이프키", "シェイプキー")
        };
        int newMode = DrawCustomToolbar((int)currentMode, modeLabels, 30);
        if (newMode != (int)currentMode)
            currentMode = (EditorMode)newMode;

        GUILayout.Space(10);

        if (currentMode == EditorMode.Armature)
            DrawArmatureGUI();
        else if (currentMode == EditorMode.ShapeKey)
            DrawShapeKeyGUI();
        else if (currentMode == EditorMode.Expression)
            DrawExpressionGUI();
        else
            DrawShapeKeyEditorGUI();
    }
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
            }
            else
            {
                boneMapping = null;
                selectedPart = HumanoidBodyPart.None;
                InitializeValues();
            }
        }

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        var _prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(new GUIContent("\u21BA", Tr("Refresh", "\uC0C8\uB85C\uACE0\uCE68", "\u66F4\u65B0")), GUILayout.Width(28), GUILayout.Height(18)))
        {
            boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
            LoadCurrentValues();
            selectedPart = HumanoidBodyPart.None;
        }
        GUI.backgroundColor = _prevBg;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string[] armatureModes =
        {
            Tr("Direct Bone Editing", "직접 뼈 조정", "ボーン直接調整"),
            Tr("MA Scale Adjustment", "MA 비율 조정", "MA比率調整")
        };
        int nextArmatureMode = DrawCustomToolbar((int)armatureEditMode, armatureModes, 30);
        if (nextArmatureMode != (int)armatureEditMode)
        {
            armatureEditMode = (ArmatureEditMode)nextArmatureMode;
            if (armatureEditMode == ArmatureEditMode.DirectTransform) LoadCurrentValues();
            selectedPart = HumanoidBodyPart.None;
            GUI.FocusControl(null);
        }

        if (armatureEditMode == ArmatureEditMode.DirectTransform)
        {
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
            EditorGUILayout.LabelField(Tr("No presets found.", "프리셋이 없습니다.", "プリセットがありません。"), EditorStyles.wordWrappedLabel);
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
        }
        else
        {
            DrawMAPresetGUI();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawBodyMap();
        EditorGUILayout.EndScrollView();
        
        GuiLine(1, 10);
        
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(UI_TEXT[24], EditorStyles.boldLabel);

        if (selectedPart != HumanoidBodyPart.None)
        {
            bool canEditScale = true;
            if (armatureEditMode == ArmatureEditMode.ModularAvatarScale)
                canEditScale = DrawMASelectedPartControls();

            EditorGUI.BeginDisabledGroup(!canEditScale);
            Vector3 scale = GetSelectedScale();
            EditorGUI.BeginChangeCheck();
            float uniformScale = EditorGUILayout.FloatField(UI_TEXT[26], scale.x);
            if (EditorGUI.EndChangeCheck())
            {
                ApplySelectedScale(new Vector3(uniformScale, uniformScale, uniformScale));
            }
            
            EditorGUI.BeginChangeCheck();
            Vector3 newScale = EditorGUILayout.Vector3Field(GetPartName(selectedPart) + $" Scale", scale);
            if (EditorGUI.EndChangeCheck())
            {
                ApplySelectedScale(newScale);
            }
            EditorGUI.EndDisabledGroup();

            if (armatureEditMode == ArmatureEditMode.DirectTransform)
            {
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
        }
        else
        {
            EditorGUILayout.LabelField(UI_TEXT[25]);
        }
        
        EditorGUILayout.EndVertical();
    }

    // ?????? ???ル늅??씤異?에?ル씔???癲ル슢?꾤땟???GUI ??????
    private void DrawMAPresetGUI()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(Tr("MA Scale Presets", "MA 비율 프리셋", "MA比率プリセット"), EditorStyles.boldLabel);

        maPresetScrollPosition = EditorGUILayout.BeginScrollView(maPresetScrollPosition, GUILayout.Height(60));
        if (maPresetFiles != null && maPresetFiles.Length > 0)
        {
            for (int i = 0; i < maPresetFiles.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < 2 && i + j < maPresetFiles.Length; j++)
                {
                    int index = i + j;
                    string fileName = Path.GetFileNameWithoutExtension(maPresetFiles[index]);
                    GUIStyle style = new GUIStyle(GUI.skin.button);
                    if (selectedMaPresetIndex == index) style.normal.background = selectedButtonTex;
                    if (GUILayout.Button(fileName, style, GUILayout.ExpandWidth(true), GUILayout.Height(25)))
                    {
                        selectedMaPresetIndex = index;
                        selectedMaPresetName = fileName;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField(Tr("No MA presets found.", "MA 프리셋이 없습니다.", "MAプリセットがありません。"));
        }
        EditorGUILayout.EndScrollView();

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || selectedMaPresetIndex < 0);
        if (DrawThemedButton(
                Tr("Install Selected MA Preset", "선택한 MA 프리셋 설치", "選択したMAプリセットを適用"),
                new Color(0.30f, 0.82f, 0.76f), true, GUILayout.Height(26)))
            LoadSelectedMAPreset();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        if (DrawThemedButton(
                Tr("Save Current MA Ratios", "현재 MA 비율 저장", "現在のMA比率を保存"),
                new Color(0.30f, 0.82f, 0.76f), true, GUILayout.Height(26)))
            SaveNewMAPreset();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(selectedMaPresetIndex < 0);
        if (DrawThemedButton(
                Tr("Delete Selected MA Preset", "선택한 MA 프리셋 삭제", "選択したMAプリセットを削除"),
                new Color(0.78f, 0.34f, 0.34f), false, GUILayout.Height(23)) &&
            EditorUtility.DisplayDialog(
                Tr("Delete MA Preset", "MA 프리셋 삭제", "MAプリセットを削除"),
                Tr($"Delete '{selectedMaPresetName}'?", $"'{selectedMaPresetName}' 프리셋을 삭제할까요?", $"'{selectedMaPresetName}' を削除しますか？"),
                Tr("Delete", "삭제", "削除"), Tr("Cancel", "취소", "キャンセル")))
        {
            AssetDatabase.DeleteAsset(maPresetFiles[selectedMaPresetIndex]);
            AssetDatabase.Refresh();
            RefreshPresetList();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    private bool DrawMASelectedPartControls()
    {
        if (!TryGetLiveBoneTransform(GetBoneType(selectedPart), out Transform boneTransform)) return false;
        ModularAvatarScaleAdjuster adjuster = boneTransform.GetComponent<ModularAvatarScaleAdjuster>();

        bool adjustChildPositions = GetMAAdjustChildPositions(selectedPart);
        EditorGUILayout.BeginHorizontal();
        if (adjuster == null)
        {
            if (DrawThemedButton(
                    Tr("Add MA Scale Adjuster", "MA Scale Adjuster 추가", "MA Scale Adjusterを追加"),
                    new Color(0.30f, 0.82f, 0.76f), true, GUILayout.Height(25)))
                SetMAScale(boneTransform, Vector3.one, "Add MA Scale Adjuster", adjustChildPositions);
        }
        else
        {
            bool nextAdjustChildPositions = DrawThemedCheckboxToggle(
                Tr("Adjust Child Positions", "자식 위치 조정", "子位置調整"),
                adjustChildPositions,
                GUILayout.Height(25));
            if (nextAdjustChildPositions != adjustChildPositions)
            {
                adjustChildPositions = nextAdjustChildPositions;
                SetMAAdjustChildPositions(selectedPart, adjustChildPositions);
            }
            if (DrawThemedButton(
                    Tr("Remove Adjuster", "Adjuster 제거", "Adjusterを削除"),
                    new Color(0.78f, 0.34f, 0.34f), false, GUILayout.Height(25)) &&
                EditorUtility.DisplayDialog(
                    Tr("Remove MA Scale Adjuster", "MA Scale Adjuster 제거", "MA Scale Adjusterを削除"),
                    Tr("Remove it from the selected bone? Child positions previously adjusted by the option above are not restored.",
                        "선택한 뼈에서 제거할까요? 위 옵션으로 이미 조정된 자식 위치는 복구되지 않습니다.",
                        "選択したボーンから削除しますか？上のオプションで調整済みの子位置は復元されません。"),
                    Tr("Remove", "제거", "削除"), Tr("Cancel", "취소", "キャンセル")))
            {
                Undo.DestroyObjectImmediate(adjuster);
                lastKnownScale = Vector3.one;
                Selection.activeGameObject = boneTransform.gameObject;
            }
        }
        EditorGUILayout.EndHorizontal();

        return boneTransform.GetComponent<ModularAvatarScaleAdjuster>() != null;
    }

    private Vector3 GetDisplayedScale(Transform boneTransform)
    {
        if (armatureEditMode == ArmatureEditMode.ModularAvatarScale)
        {
            ModularAvatarScaleAdjuster adjuster = boneTransform.GetComponent<ModularAvatarScaleAdjuster>();
            return adjuster != null ? adjuster.Scale : Vector3.one;
        }
        return boneTransform.localScale;
    }

    private Vector3 GetSelectedScale()
    {
        if (selectedPart == HumanoidBodyPart.None) return Vector3.one;
        if (armatureEditMode == ArmatureEditMode.DirectTransform) return GetPartScale(selectedPart);
        return TryGetLiveBoneTransform(GetBoneType(selectedPart), out Transform boneTransform)
            ? GetDisplayedScale(boneTransform) : Vector3.one;
    }

    private void ApplySelectedScale(Vector3 scale)
    {
        if (selectedPart == HumanoidBodyPart.None) return;
        if (armatureEditMode == ArmatureEditMode.DirectTransform)
        {
            UpdatePartScale(scale);
            ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
            return;
        }

        if (TryGetLiveBoneTransform(GetBoneType(selectedPart), out Transform boneTransform) &&
            boneTransform.GetComponent<ModularAvatarScaleAdjuster>() != null)
        {
            SetMAScale(boneTransform, scale, "Adjust MA Scale", GetMAAdjustChildPositions(selectedPart));
        }
    }

    private bool GetMAAdjustChildPositions(HumanoidBodyPart part)
    {
        return maAdjustChildPositionParts.Contains(part.ToString());
    }

    private void SetMAAdjustChildPositions(HumanoidBodyPart part, bool enabled)
    {
        string key = part.ToString();
        if (enabled)
        {
            if (!maAdjustChildPositionParts.Contains(key)) maAdjustChildPositionParts.Add(key);
        }
        else
        {
            maAdjustChildPositionParts.Remove(key);
        }
    }

    private void SetMAScale(Transform boneTransform, Vector3 scale, string undoName, bool adjustChildPositions)
    {
        ModularAvatarScaleAdjuster adjuster = boneTransform.GetComponent<ModularAvatarScaleAdjuster>();
        if (adjuster == null) adjuster = Undo.AddComponent<ModularAvatarScaleAdjuster>(boneTransform.gameObject);

        Vector3 oldScale = SanitizeMAScale(adjuster.Scale);
        Vector3 newScale = SanitizeMAScale(scale);
        Matrix4x4 targetLocalToWorld = boneTransform.localToWorldMatrix;

        Undo.RecordObject(adjuster, undoName);
        adjuster.Scale = newScale;
        ForceUpdateScene(adjuster);

        if (adjustChildPositions && oldScale != newScale)
            AdjustMAChildPositions(boneTransform, targetLocalToWorld, oldScale, newScale, undoName);

        lastKnownScale = adjuster.Scale;
    }

    private void AdjustMAChildPositions(
        Transform boneTransform,
        Matrix4x4 targetLocalToWorld,
        Vector3 oldScale,
        Vector3 newScale,
        string undoName)
    {
        Matrix4x4 baseToScaleCoordinates =
            (targetLocalToWorld * Matrix4x4.Scale(ClampMAChildScale(oldScale))).inverse * targetLocalToWorld;
        Matrix4x4 scaleToBaseCoordinates = Matrix4x4.Scale(ClampMAChildScale(newScale));
        Matrix4x4 updateTransform = scaleToBaseCoordinates * baseToScaleCoordinates;

        foreach (Transform child in boneTransform)
        {
            Undo.RecordObject(child, undoName);
            child.localPosition = updateTransform.MultiplyPoint(child.localPosition);
            ForceUpdateScene(child);
        }
    }

    private static Vector3 ClampMAChildScale(Vector3 scale)
    {
        const float threshold = 1f / (1 << 14);
        return new Vector3(
            Mathf.Max(threshold, scale.x),
            Mathf.Max(threshold, scale.y),
            Mathf.Max(threshold, scale.z));
    }

    private static Vector3 SanitizeMAScale(Vector3 scale)
    {
        const float minimum = 0.0001f;
        return new Vector3(
            float.IsNaN(scale.x) || float.IsInfinity(scale.x) ? 1f : Mathf.Max(minimum, scale.x),
            float.IsNaN(scale.y) || float.IsInfinity(scale.y) ? 1f : Mathf.Max(minimum, scale.y),
            float.IsNaN(scale.z) || float.IsInfinity(scale.z) ? 1f : Mathf.Max(minimum, scale.z));
    }

    private void SaveNewMAPreset()
    {
        if (targetAvatarRoot == null) return;
        MAScaleAdjusterPresetData preset = ScriptableObject.CreateInstance<MAScaleAdjusterPresetData>();
        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part == HumanoidBodyPart.None || !TryGetLiveBoneTransform(GetBoneType(part), out Transform boneTransform)) continue;
            ModularAvatarScaleAdjuster adjuster = boneTransform.GetComponent<ModularAvatarScaleAdjuster>();
            if (adjuster != null)
                preset.entries.Add(new MAScaleAdjusterPresetData.Entry(
                    part.ToString(), adjuster.Scale, GetMAAdjustChildPositions(part)));
        }

        if (preset.entries.Count == 0)
        {
            DestroyImmediate(preset);
            EditorUtility.DisplayDialog(Tr("No MA Adjusters", "MA Adjuster 없음", "MA Adjusterがありません"),
                Tr("No mapped bones have an MA Scale Adjuster.", "매핑된 뼈에 MA Scale Adjuster가 없습니다.",
                    "マッピングされたボーンにMA Scale Adjusterがありません。"), "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(Tr("Save MA Scale Preset", "MA 비율 프리셋 저장", "MA比率プリセットを保存"),
            "NewMAScalePreset", "asset", Tr("Choose a save location.", "저장 위치를 선택하세요.", "保存先を選択してください。"));
        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(preset);
            return;
        }

        MAScaleAdjusterPresetData existing = AssetDatabase.LoadAssetAtPath<MAScaleAdjusterPresetData>(path);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(Tr("Overwrite Preset", "프리셋 덮어쓰기", "プリセットを上書き"),
                    Tr("Overwrite the existing MA preset?", "기존 MA 프리셋을 덮어쓸까요?", "既存のMAプリセットを上書きしますか？"),
                    Tr("Overwrite", "덮어쓰기", "上書き"), Tr("Cancel", "취소", "キャンセル")))
            {
                DestroyImmediate(preset);
                return;
            }
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshPresetList();
    }

    private void LoadSelectedMAPreset()
    {
        if (targetAvatarRoot == null || maPresetFiles == null || selectedMaPresetIndex < 0 ||
            selectedMaPresetIndex >= maPresetFiles.Length) return;
        MAScaleAdjusterPresetData preset = AssetDatabase.LoadAssetAtPath<MAScaleAdjusterPresetData>(maPresetFiles[selectedMaPresetIndex]);
        if (preset == null) return;

        int installable = 0;
        int existingAdjusters = 0;
        int missing = 0;
        int childPositionAdjustments = 0;
        foreach (MAScaleAdjusterPresetData.Entry entry in preset.entries)
        {
            if (!System.Enum.TryParse(entry.part, out HumanoidBodyPart part) || part == HumanoidBodyPart.None ||
                !TryGetLiveBoneTransform(GetBoneType(part), out Transform boneTransform))
            {
                missing++;
                continue;
            }
            installable++;
            if (boneTransform.GetComponent<ModularAvatarScaleAdjuster>() != null) existingAdjusters++;
            if (entry.adjustChildPositions) childPositionAdjustments++;
        }

        string confirmation = Tr(
            $"Install on {installable} bones? {existingAdjusters} existing Adjusters will be updated; {missing} entries will be skipped. {childPositionAdjustments} entries will also change direct child positions.",
            $"{installable}개 뼈에 설치할까요? 기존 Adjuster {existingAdjusters}개는 갱신되고 {missing}개 항목은 건너뜁니다. {childPositionAdjustments}개 항목은 직계 자식 위치도 변경합니다.",
            $"{installable}個のボーンに適用しますか？既存Adjuster {existingAdjusters}個を更新し、{missing}項目をスキップします。{childPositionAdjustments}項目は直下の子位置も変更します。" );
        if (!EditorUtility.DisplayDialog(
                Tr("Install MA Preset", "MA 프리셋 설치", "MAプリセットを適用"), confirmation,
                Tr("Install", "설치", "適用"), Tr("Cancel", "취소", "キャンセル"))) return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install MA Scale Preset");
        int installed = 0;
        int skipped = 0;
        foreach (MAScaleAdjusterPresetData.Entry entry in preset.entries)
        {
            if (!System.Enum.TryParse(entry.part, out HumanoidBodyPart part) || part == HumanoidBodyPart.None ||
                !TryGetLiveBoneTransform(GetBoneType(part), out Transform boneTransform))
            {
                skipped++;
                continue;
            }
            SetMAAdjustChildPositions(part, entry.adjustChildPositions);
            SetMAScale(boneTransform, entry.Scale, "Install MA Scale Preset", entry.adjustChildPositions);
            installed++;
        }
        Undo.CollapseUndoOperations(undoGroup);
        Repaint();

        EditorUtility.DisplayDialog(Tr("MA Preset Installed", "MA 프리셋 설치 완료", "MAプリセット適用完了"),
            Tr($"Installed on {installed} bones. Skipped {skipped} entries.",
                $"{installed}개 뼈에 설치했습니다. {skipped}개 항목을 건너뛰었습니다.",
                $"{installed}個のボーンに適用しました。{skipped}項目をスキップしました。"), "OK");
    }

    private void DrawShapeKeyGUI()
    {
        // ?????怨뚮뼚????????筌????怨좊룴??(?縕?猿녿뎨?T??????れ삀?節낆젂??
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
        if (GUILayout.Button(new GUIContent("↺", Tr("Refresh", "새로고침", "更新")), GUILayout.Width(28), GUILayout.Height(18)))
        {
            if (targetAvatarRoot != null)
            {
                TakeSnapshot();
                Debug.Log("[Avi Editor] ???怨좊룴???縕?猿녿뎨???????????ш낄援????????궈??關履????怨?????덊렡.");
            }
        }
        GUI.backgroundColor = _prevBg;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        animationClip    = (AnimationClip)EditorGUILayout.ObjectField(SK_TEXT[2], animationClip, typeof(AnimationClip), false);
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
        var _prevRtBg = GUI.backgroundColor;
        GUI.backgroundColor = _realtimePreview ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.35f, 0.35f, 0.38f);
        var rtStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 10,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = _realtimePreview ? Color.white : new Color(0.7f, 0.7f, 0.7f) },
            hover     = { textColor = Color.white },
        };
        if (GUILayout.Button(SK_TEXT[13], rtStyle, GUILayout.Height(18)))
            SetRealtimePreview(!_realtimePreview);
        GUI.backgroundColor = _prevRtBg;
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(3);

        EditorGUI.BeginChangeCheck();
        clipTime = EditorGUILayout.Slider(clipTime, 0f, animationClip != null ? animationClip.length : 1f);
        if (EditorGUI.EndChangeCheck())
        {
            ApplyShapeKeys();
            if (_realtimePreview) PreviewPoseNoUndo();
        }

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || animationClip == null);

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 36,
            normal      = { textColor = Color.white },
            hover       = { textColor = Color.white },
        };
        var prevBgGroup = GUI.backgroundColor;

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(SK_TEXT[4], btnStyle))
        {
            ApplyShapeKeys();
            Debug.Log($"[Avi Editor] {targetAvatarRoot.name} - {SK_TEXT[7]}");
        }

        GUI.backgroundColor = new Color(0.25f, 0.65f, 0.60f);
        if (GUILayout.Button(SK_TEXT[5], btnStyle))
        {
            ApplyPose();
            Debug.Log($"[Avi Editor] {targetAvatarRoot.name} - {SK_TEXT[8]}");
        }

        GUI.backgroundColor = new Color(0.21f, 0.21f, 0.24f);
        if (GUILayout.Button(SK_TEXT[6], new GUIStyle(btnStyle)
            { normal = { textColor = new Color(0.30f, 0.82f, 0.76f) }, hover = { textColor = Color.white } }))
        {
            ApplyShapeKeys();
            ApplyPose();
            Debug.Log($"[Avi Editor] {targetAvatarRoot.name} - {SK_TEXT[9]}");
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = prevBgGroup;

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(5);

        // ?????? ?怨뚮옖甕???類????(????궈???縕?猿녿뎨???棺??짆?먰맪????ㅼ굣?? ??????
        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || !_hasSnapshot);
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
            RestoreToOriginal();
            Debug.Log($"[Avi Editor] {SK_TEXT[11]}");
        }
        GUI.backgroundColor = prevBgGroup;
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(5);
        EditorGUILayout.HelpBox(SK_TEXT[12], MessageType.Info);
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

    // --- ???쒓낯???ApplyPose ??딅텑???---
    private void ApplyPose()
    {
        if (targetAvatarRoot == null || animationClip == null) return;

        // Undo ??れ삀??쎈뭄????????ш끽維곻쭚?? ???쒓낮彛???ш끽維?琯???????????덊렡. (SampleAnimation??癲ル슢??? ?怨뚮옖筌?????쒓낯????????源끹걬雅?퍔源???
        Undo.RegisterFullObjectHierarchyUndo(targetAvatarRoot, "Avi Editor Freeze Pose");

        // 1. ??ш끽維????????ш낄援?????ㅺ컼???袁⑸즲??罹?
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

        // 2. Unity ???⑤챷沅???れ삀?????⑥?????ル늅??씤異?에?ル씔?????ш끽維?????ㅼ굣??
        // (????れ삀???????ㅽ떝???????嶺뚮ㅎ?닻???沃섃뫗援?????Β????? ???????????嶺뚮Ĳ?놅쭕???ㅼ굣筌뤿뱶?????ㅼ굣???筌뤾퍓???
        animationClip.SampleAnimation(targetAvatarRoot, clipTime);

        // 3. ??????ш낄援?????ㅺ컼???怨뚮옖甕??(???????影??탿????????ш낄援???SampleAnimation ???⑤챷?????ㅺ컼?얜쑚????嚥▲꺃???
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

        // ??ш끽維??Transform ?怨뚮뼚??濡ろ뜑??듭쒜?????袁⑸즵???
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

        // ??ш끽維????????ш낄援?????ㅺ컼???袁⑸즲??罹?
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

        // Undo ???⑤챶????ш끽維?????ル늅??씤異?에?ル씔??????ㅼ굣??(????
        animationClip.SampleAnimation(targetAvatarRoot, clipTime);

        // ??????ш낄援???怨뚮옖甕??(???κ옇?壤????)
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
        if (!on)
        {
            RestoreTransformsOnly();
            ApplyShapeKeys();
        }
    }

    // ??ш끽維곻쭚?? ???ル늅?????縕?猿녿뎨?T???????怨좊룴??????
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
                SK_TEXT = new[]
                {
                    "대상 설정",
                    "대상 오브젝트 (루트)",
                    "애니메이션 클립",
                    "시간 조정",
                    "쉐이프키만 적용",
                    "포즈만 적용",
                    "전체 적용",
                    "쉐이프키 적용 완료.",
                    "포즈 적용 완료.",
                    "전체 적용 완료.",
                    "원본으로 초기화 (T포즈/0)",
                    "원본 상태로 복원했습니다. (프리팹 또는 T포즈/쉐이프키 0)",
                    "슬라이더는 미리보기용이며 버튼은 씬에 적용됩니다.",
                    "실시간 미리보기",
                };
                break;
            case LanguagePreset.Japanese:
                SK_TEXT = new[]
                {
                    "対象設定",
                    "対象オブジェクト (ルート)",
                    "アニメーションクリップ",
                    "時間調整",
                    "シェイプキーのみ適用",
                    "ポーズのみ適用",
                    "すべて適用",
                    "シェイプキーを適用しました。",
                    "ポーズを適用しました。",
                    "すべて適用しました。",
                    "初期状態に戻す (Tポーズ/0)",
                    "元の状態に復元しました。 (Prefab または Tポーズ/シェイプキー 0)",
                    "スライダーはプレビュー用で、ボタンはシーンに適用されます。",
                    "リアルタイムプレビュー",
                };
                break;
            default:
                SK_TEXT = new[]
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
                    "Initialize Original (T-Pose/0)",
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
        string boneName = TryGetLiveBoneTransform(boneType, out Transform liveBone) ? liveBone.name : UI_TEXT[30];
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
            
            if (armatureEditMode == ArmatureEditMode.DirectTransform &&
                previousSelectedPart != part && previousSelectedPart != HumanoidBodyPart.None)
            {
                ApplyCurrentChanges(previousSelectedPart);
            }
            
            selectedPart = part;
            HumanBodyBones selectedBoneType = GetBoneType(part);
            if (TryGetLiveBoneTransform(selectedBoneType, out Transform boneTransform))
            {
                lastKnownScale = GetDisplayedScale(boneTransform);
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
        if (TryGetLiveBoneTransform(boneType, out Transform boneTransform))
        {
            return boneTransform.name;
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
            default: return "None";
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
    
    // ?????? ??ш끽維곻쭚?? ?嶺뚮㉡?€쾮???節뚮쳮雅???筌?六???????
    private string GetShortLabel(HumanoidBodyPart part)
    {
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
            default: return part.ToString();
        }
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
            string selName = TryGetLiveBoneTransform(selBone, out Transform selectedBoneTransform) ? selectedBoneTransform.name : "---";
            GUIStyle infoStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.30f, 0.82f, 0.76f) },
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
        bool found = TryGetLiveBoneTransform(boneType, out _);
        bool isSelected = selectedPart == part;

        Color dotColor = !found ? new Color(0.3f, 0.3f, 0.3f, 0.6f)
                       : isSelected ? new Color(0.30f, 0.82f, 0.76f, 1f)
                       : new Color(0.2f, 0.8f, 0.3f, 0.9f);

        float clickSize = Mathf.Max(radius * 2.5f, 26f);
        Rect clickRect = new Rect(center.x - clickSize / 2, center.y - clickSize / 2, clickSize, clickSize);

        DrawFilledCircle(center, radius, dotColor);
        if (isSelected)
            DrawCircleOutline(center, radius + 4, new Color(0.30f, 0.82f, 0.76f, 0.5f), 2f);

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
            if (armatureEditMode == ArmatureEditMode.DirectTransform &&
                selectedPart != part && selectedPart != HumanoidBodyPart.None)
                ApplyCurrentChanges(selectedPart);

            selectedPart = part;
            if (TryGetLiveBoneTransform(boneType, out Transform boneTransform))
            {
                lastKnownScale = GetDisplayedScale(boneTransform);
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
                UI_TEXT = new[]
                {
                    "대상 아바타 루트", "스케일 저장", "저장된 스케일 불러오기",
                    "왼팔", "왼아래팔", "왼손", "머리", "상체", "척추", "엉덩이",
                    "오른팔", "오른아래팔", "오른손",
                    "왼다리", "왼아래다리", "왼발",
                    "오른다리", "오른아래다리", "오른발",
                    "세부 파츠", "왼가슴", "오른가슴", "왼엉덩이", "오른엉덩이",
                    "선택된 파츠", "파츠를 선택하세요.", "균일 스케일",
                    "스케일 프리셋", "프리셋 불러오기", "새 프리셋 저장",
                    "없음", "선택한 프리셋 삭제", "정말로 프리셋 '", "' 을(를) 삭제하시겠습니까?",
                    "삭제", "취소", "스케일 초기화", "회전 조정", "회전",
                    "스케일만 불러오기", "회전만 불러오기",
                    "위치 조정", "위치", "위치만 불러오기",
                    "목", "왼어깨", "오른어깨"
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new[]
                {
                    "対象アバタールート", "スケールを保存", "保存したスケールを読み込む",
                    "左腕", "左前腕", "左手", "頭", "胴体", "背骨", "腰",
                    "右腕", "右前腕", "右手",
                    "左脚", "左ひざ下", "左足",
                    "右脚", "右ひざ下", "右足",
                    "詳細パーツ", "左胸", "右胸", "左お尻", "右お尻",
                    "選択中のパーツ", "パーツを選択してください。", "均一スケール",
                    "スケールプリセット", "プリセットを読み込む", "新規プリセットを保存",
                    "なし", "選択したプリセットを削除", "本当にプリセット '", "' を削除しますか？",
                    "削除", "キャンセル", "スケールを初期化", "回転調整", "回転",
                    "スケールのみ読み込む", "回転のみ読み込む",
                    "位置調整", "位置", "位置のみ読み込む",
                    "首", "左肩", "右肩"
                };
                break;
            default:
                UI_TEXT = new[]
                {
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
            Debug.LogError(Tr("Please assign an avatar first.", "먼저 아바타를 지정해 주세요.", "先にアバターを指定してください。"));
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
                            scaleValues[part] = kvp.Value.ToVector3();
                    }
                    ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
                }

                if (applyRotation)
                {
                    rotationValues.Clear();
                    foreach (var kvp in loadedData.rotations.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                            rotationValues[part] = kvp.Value.ToQuaternion();
                    }
                    ArmatureScalerLogic.ApplyRotation(boneMapping, MapToHumanBodyBonesForRotation(rotationValues));
                }

                if (applyPosition)
                {
                    positionValues.Clear();
                    foreach (var kvp in loadedData.positions.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                            positionValues[part] = kvp.Value.ToVector3();
                    }
                    ArmatureScalerLogic.ApplyPosition(boneMapping, MapToHumanBodyBones(positionValues));
                }

                string appliedType = "preset";
                if (applyScale && applyRotation && applyPosition) appliedType = "full preset";
                else
                {
                    List<string> types = new List<string>();
                    if (applyScale) types.Add("scale");
                    if (applyRotation) types.Add("rotation");
                    if (applyPosition) types.Add("position");
                    appliedType = string.Join(", ", types) + " preset";
                }

                Debug.Log($"Preset '{Path.GetFileNameWithoutExtension(filePath)}' loaded successfully as {appliedType}.");
            }
            else
            {
                Debug.LogError("Preset file could not be loaded.");
            }
        }
    }
    private void SaveNewPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Preset", "NewPreset", "asset", "Choose a location and name for the new preset.");
        if (!string.IsNullOrEmpty(path))
        {
            ArmatureScalerPresetData existingPreset = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(path);
            if (existingPreset != null)
            {
                if (!EditorUtility.DisplayDialog("Warning", "Overwrite the existing preset?", "Overwrite", "Cancel"))
                    return;
                AssetDatabase.DeleteAsset(path);
            }

            ArmatureScalerPresetData newPreset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            foreach (var kvp in scaleValues)
                newPreset.scales.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));
            foreach (var kvp in rotationValues)
                if (CanRotate(kvp.Key))
                    newPreset.rotations.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableQuaternion(kvp.Value));
            foreach (var kvp in positionValues)
                newPreset.positions.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));

            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshPresetList();
            Debug.Log($"Preset saved: {path}");
        }
    }
    private void DeletePreset(string filePath)
    {
        AssetDatabase.DeleteAsset(filePath);
        AssetDatabase.Refresh();
        Debug.Log("Preset deleted successfully.");
    }
    private void ResetScalesToDefault()
    {
        if (targetAvatarRoot == null)
        {
            Debug.LogError(Tr("Please assign an avatar first.", "먼저 아바타를 지정해 주세요.", "先にアバターを指定してください。"));
            return;
        }

        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part != HumanoidBodyPart.None && scaleValues.ContainsKey(part))
                scaleValues[part] = Vector3.one;
        }

        ArmatureScalerLogic.ApplyScale(boneMapping, MapToHumanBodyBones(scaleValues));
        selectedPart = HumanoidBodyPart.None;
        Debug.Log("All scales have been reset.");
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

    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已?
    //  EXPRESSION TAB
    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已?
    private bool DrawThemedCheckboxToggle(string label, bool value, params GUILayoutOption[] options)
    {
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = value
            ? new Color(0.30f, 0.82f, 0.76f)
            : new Color(0.42f, 0.42f, 0.45f);
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        Rect buttonRect = GUILayoutUtility.GetRect(GUIContent.none, buttonStyle, options);
        bool nextValue = GUI.Toggle(buttonRect, value, GUIContent.none, buttonStyle);

        const float boxSize = 14f;
        Rect boxRect = new Rect(
            buttonRect.x + 8f,
            buttonRect.y + (buttonRect.height - boxSize) * 0.5f,
            boxSize,
            boxSize);
        EditorGUI.DrawRect(boxRect, value ? Color.white : new Color(0.72f, 0.72f, 0.74f));
        Rect boxInner = new Rect(boxRect.x + 2f, boxRect.y + 2f, boxRect.width - 4f, boxRect.height - 4f);
        EditorGUI.DrawRect(boxInner, value ? new Color(0.22f, 0.62f, 0.57f) : new Color(0.25f, 0.25f, 0.27f));

        if (value)
        {
            Handles.BeginGUI();
            Color previousHandleColor = Handles.color;
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(2f,
                new Vector3(boxRect.x + 3.2f, boxRect.y + 7.2f),
                new Vector3(boxRect.x + 6.0f, boxRect.y + 10.0f),
                new Vector3(boxRect.x + 11.2f, boxRect.y + 4.0f));
            Handles.color = previousHandleColor;
            Handles.EndGUI();
        }

        GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = value ? FontStyle.Bold : FontStyle.Normal,
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = value ? Color.white : new Color(0.82f, 0.82f, 0.82f) }
        };
        Rect labelRect = new Rect(
            boxRect.xMax + 6f,
            buttonRect.y,
            Mathf.Max(0f, buttonRect.xMax - boxRect.xMax - 10f),
            buttonRect.height);
        GUI.Label(labelRect, label, labelStyle);

        GUI.backgroundColor = previousBackground;
        return nextValue;
    }

    private bool DrawThemedButton(
        string label,
        Color backgroundColor,
        bool bold,
        params GUILayoutOption[] options)
    {
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            fontSize = 12,
            normal = { textColor = Color.white }
        };
        bool pressed = GUILayout.Button(label, style, options);
        GUI.backgroundColor = previousBackground;
        return pressed;
    }

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

        // ??筌?痢⑼┼?????????れ삀??쎈뭄????????ш낄援??????????쒓낯??(0???????????
        HashSet<string> gestureShapeKeyNames = (_includeGestureKeys && _exprFxController != null)
            ? CollectGestureShapeKeys(_exprFxController)
            : new HashSet<string>();

        string smrPath = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        int cnt = _bodySmr.sharedMesh.blendShapeCount;
        for (int i = 0; i < cnt; i++)
        {
            float val = _exprShapeValues != null ? _exprShapeValues[i] : _bodySmr.GetBlendShapeWeight(i);
            string shapeName = _bodySmr.sharedMesh.GetBlendShapeName(i);

            // val??0????????筌?痢⑼┼??????????????????ш낄援???0??좊즴????肉???れ삀??쎈뭄?
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
        Debug.Log($"[Avi Editor] Expression saved ??{path}");
    }

    /// <summary>
    /// FX ???爾??용굞肉???곷첓??癲ル슢?꾤땟??????ル늅??씤異?에?ル씔????????????blendShape ??ш끽維곩ㅇ???紐껎룂 ?????shapekey癲??????쒓낯???筌뤾퍓???
    /// ??좊즴???0?????100???????れ삀??쎈뭄????????ш낄援??????????? ?袁⑸즵????筌뤾퍓???
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
        // ??筌먐삳빘 ????읐??熬곣뫀肄??沃섃뫗援???????琉?
        foreach (var sub in sm.stateMachines)
            CollectFromStateMachine(sub.stateMachine, result, visitedClips);
    }

    private void CollectShapeKeysFromClip(AnimationClip clip, HashSet<string> result)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(SkinnedMeshRenderer)) continue;
            if (!binding.propertyName.StartsWith("blendShape.")) continue;
            // "blendShape.ShapeName" ??"ShapeName"
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
                Debug.Log($"[Avi Editor] Replaced clip in {layerName}[{stateIndex}] ??{targetClip.name}");
            }
            break;
        }
    }

    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已?
    //  SHAPE KEY EDITOR TAB
    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已?
    private void DrawShapeKeyEditorGUI()
    {
        var prevBg = GUI.backgroundColor;

        // ─ 대상 설정 ────────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            language == LanguagePreset.Korean  ? "대상 설정"
          : language == LanguagePreset.Japanese ? "対象設定" : "Target Settings",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(
            language == LanguagePreset.Korean  ? "아바타 루트"
          : language == LanguagePreset.Japanese ? "アバタールート" : "Avatar Root",
            targetAvatarRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() || targetAvatarRoot != _skePrevTarget)
        {
            _skePrevTarget = targetAvatarRoot;
            SkeRestoreAndClearPreview();
            _skeSmr = DiNeShapeKeyEditorCore.FindBodySmr(targetAvatarRoot);
            _skeStatus = "";
            _skePreviewDirty = true;
        }

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("↺", GUILayout.Width(28), GUILayout.Height(18)))
        {
            SkeRestoreAndClearPreview();
            _skeSmr = DiNeShapeKeyEditorCore.FindBodySmr(targetAvatarRoot);
            _skeStatus = "";
            _skePreviewDirty = true;
        }
        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        _skeSmr = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            language == LanguagePreset.Korean  ? "대상 메쉬"
          : language == LanguagePreset.Japanese ? "対象メッシュ" : "Target Mesh",
            _skeSmr, typeof(SkinnedMeshRenderer), true);
        if (EditorGUI.EndChangeCheck())
        {
            SkeRestoreAndClearPreview();
            _skeStatus = "";
            _skePreviewDirty = true;
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);

        if (_skeSmr == null || _skeSmr.sharedMesh == null)
        {
            EditorGUILayout.HelpBox(
                language == LanguagePreset.Korean  ? "아바타 루트를 지정하거나 대상 메쉬를 직접 드래그해 주세요."
              : language == LanguagePreset.Japanese ? "アバタールートを指定するか、対象メッシュを直接ドラッグしてください。"
              : "Set an Avatar Root or drag a Target Mesh directly.", MessageType.Info);
            return;
        }
        if (_skeSmr.sharedMesh.blendShapeCount == 0)
        {
            EditorGUILayout.HelpBox(
                language == LanguagePreset.Korean  ? "쉐이프키가 없는 메쉬입니다."
              : language == LanguagePreset.Japanese ? "シェイプキーのないメッシュです。"
              : "This mesh has no shape keys.", MessageType.Warning);
            return;
        }

        // ─ 서브 모드 탭 ──────────────────────────────────────────────────────
        string[] subLabels = language == LanguagePreset.Korean  ? new[] { "새로 만들기", "수정하기" }
                           : language == LanguagePreset.Japanese ? new[] { "新規作成", "編集" }
                           : new[] { "Create New", "Modify" };
        int newSubMode = DrawCustomToolbar(_skeSubMode, subLabels, 26);
        if (newSubMode != _skeSubMode)
        {
            SkeRestoreAndClearPreview();
            _skeSubMode = newSubMode;
            _skeStatus = "";
            _skePreviewDirty = true;
        }
        GUILayout.Space(5);

        string[] shapeNames = DiNeShapeKeyEditorCore.GetShapeKeyNames(_skeSmr);

        // ─ 프리뷰 ────────────────────────────────────────────────────────────
        DrawSkePreview();
        GUILayout.Space(5);

        // ─ 모드별 내용 (아래 전체를 스크롤 가능하게) ──────────────────────────
        _skeOuterScroll = EditorGUILayout.BeginScrollView(_skeOuterScroll);

        if (_skeSubMode == 0)
            DrawSkeCreateMix(shapeNames, prevBg);
        else
            DrawSkeModify(shapeNames, prevBg);

        // ─ 상태 메시지 ───────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_skeStatus))
        {
            GUILayout.Space(4);
            EditorGUILayout.HelpBox(_skeStatus, _skeStatusIsError ? MessageType.Error : MessageType.Info);
        }
        GUILayout.Space(8);
        EditorGUILayout.EndScrollView();
    }
    private void DrawSkePreview()
    {
        float size = Mathf.Min(position.width - 20f, 200f);
        Rect previewRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
        previewRect.x = (position.width - size) * 0.5f;

        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.76f, 0.76f, 0.76f);
        EditorGUI.DrawRect(previewRect, bgCol);

        if (Event.current.type == EventType.Repaint)
        {
            RenderSkePreview((int)size);
            if (_skePreviewRT != null)
                GUI.DrawTexture(previewRect, _skePreviewRT, ScaleMode.ScaleToFit, true);
            _skePreviewDirty = false;
        }
        if (Event.current.type == EventType.Used)
            _skePreviewDirty = true;
    }

    private void RenderSkePreview(int size)
    {
        if (_skeSmr == null) return;
        if (size <= 0) return;
        if (!_skePreviewDirty && _skePreviewRT != null && _skePreviewRT.width == size) return;

        if (_skePreviewRT == null || _skePreviewRT.width != size)
        {
            if (_skePreviewRT != null) _skePreviewRT.Release();
            _skePreviewRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            _skePreviewRT.antiAliasing = 2;
            _skePreviewRT.Create();
        }

        // ??筌믨퀣??????????곕럡????곕쿊 Head ????れ삀?????⑥???怨멸텭?嶺???袁⑸즲???
        Vector3 headPos, camFwd;
        if (targetAvatarRoot != null)
        {
            // boneMapping?????⑤챶?뺧┼?癲ル슣????癲?????(??ш끽維곭빊??⑤베毓??????癲꾧퀗???域밟뫁?? ????⒱봼???濡ろ뜑???????
            if (boneMapping == null)
                boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);

            headPos = targetAvatarRoot.transform.position + Vector3.up * 1.5f;
            if (boneMapping != null && boneMapping.TryGetValue(HumanBodyBones.Head, out Transform headBone))
                headPos = headBone.position;
            camFwd = targetAvatarRoot.transform.forward;
        }
        else
        {
            // SMR癲?????덉툗 ?濡ろ뜑??? bounds ???ㅿ폍???딅텑???????살젢????⑤베毓??
            var bounds = _skeSmr.bounds;
            headPos = bounds.center + Vector3.up * bounds.extents.y * 0.35f;
            camFwd  = _skeSmr.transform.forward;
        }

        var camGo = new GameObject("__SkePrevCam__") { hideFlags = HideFlags.HideAndDontSave };
        var cam   = camGo.AddComponent<Camera>();
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.orthographic    = false;
        cam.fieldOfView     = 22f;
        cam.nearClipPlane   = 0.01f;
        cam.farClipPlane    = 100f;
        cam.targetTexture   = _skePreviewRT;
        cam.cullingMask     = -1;
        cam.enabled         = false;

        cam.transform.position = headPos + camFwd * 0.45f + Vector3.up * 0.04f;
        cam.transform.LookAt(headPos + Vector3.up * 0.04f);

        cam.Render();
        cam.targetTexture = null;
        DestroyImmediate(camGo);
    }

    // listMode: 0=create(??⑤베堉??類????  1=modify-select(???????ャ뀕??  2=modify-select+雅?퍔瑗????뽱돯??
    private void DrawSkeShapeKeyList(string[] shapeNames, int listMode, Color prevBg)
    {
        _skeSearch = EditorGUILayout.TextField("", _skeSearch, EditorStyles.toolbarSearchField);
        GUILayout.Space(2);
        string searchLower = _skeSearch.ToLower();

        _skeSKListScroll = EditorGUILayout.BeginScrollView(_skeSKListScroll, GUILayout.Height(210));
        for (int i = 0; i < shapeNames.Length; i++)
        {
            string name = shapeNames[i];
            if (!string.IsNullOrEmpty(searchLower) && !name.ToLower().Contains(searchLower)) continue;

            bool isTarget   = (listMode >= 1) && _skeModifyIndex == i;
            bool isInCreate = listMode == 0 && _skeMixEntries.Any(e => e.index == i);
            bool isInModMix = listMode == 2 && _skeModifyMixEntries.Any(e => e.index == i);

            EditorGUILayout.BeginHorizontal();

            if (listMode == 0) // ─ 새로 만들기: 레이블 + [추가] ─
            {
                GUI.backgroundColor = isInCreate ? new Color(0.15f, 0.45f, 0.15f) : prevBg;
                GUILayout.Label(name, GUILayout.ExpandWidth(true));
                GUI.backgroundColor = isInCreate ? new Color(0.15f, 0.45f, 0.15f) : new Color(0.25f, 0.55f, 0.25f);
                string addL = language == LanguagePreset.Korean ? "추가" : language == LanguagePreset.Japanese ? "追加" : "Add";
                if (GUILayout.Button(addL, GUILayout.Width(42), GUILayout.Height(19)))
                {
                    _skeMixEntries.Add(new DiNeSkeMixEntry { index = i, weight = 100f });
                    SkeApplyMixPreview(_skeMixEntries);
                }
                GUI.backgroundColor = prevBg;
            }
            else // ─ 수정하기: 클릭으로 대상 선택 (+ 믹스 추가 버튼) ─
            {
                GUI.backgroundColor = isTarget ? new Color(0.30f, 0.82f, 0.76f) : prevBg;
                GUIStyle rowStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft, fontSize = 11,
                    fontStyle = isTarget ? FontStyle.Bold : FontStyle.Normal,
                    normal    = { textColor = isTarget ? Color.white : GUI.skin.label.normal.textColor },
                };
                if (GUILayout.Button(name, rowStyle, GUILayout.ExpandWidth(true), GUILayout.Height(20)))
                {
                    _skeModifyIndex = i;
                    SkeApplyModifyPreview(i);
                }
                if (listMode == 2) // 믹스 추가 버튼
                {
                    GUI.backgroundColor = isInModMix ? new Color(0.15f, 0.45f, 0.15f) : new Color(0.25f, 0.55f, 0.25f);
                    if (GUILayout.Button("+", GUILayout.Width(24), GUILayout.Height(20)))
                    {
                        _skeModifyMixEntries.Add(new DiNeSkeMixEntry { index = i, weight = 100f });
                        SkeApplyMixPreview(_skeModifyMixEntries);
                    }
                }
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
    private bool DrawSkeMixList(List<DiNeSkeMixEntry> mixList, string[] shapeNames,
        Vector2 scroll, out Vector2 newScroll, Color prevBg, bool isModifyMix)
    {
        bool changed = false;
        newScroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(150));
        int removeAt = -1;
        for (int i = 0; i < mixList.Count; i++)
        {
            var entry = mixList[i];
            EditorGUILayout.BeginHorizontal();
            string n = entry.index >= 0 && entry.index < shapeNames.Length ? shapeNames[entry.index] : "?";
            GUILayout.Label(n, GUILayout.Width(115));
            EditorGUI.BeginChangeCheck();
            entry.weight = EditorGUILayout.Slider(entry.weight, 0f, 200f);
            if (EditorGUI.EndChangeCheck()) changed = true;
            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button("R", GUILayout.Width(22), GUILayout.Height(18)))
                removeAt = i;
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (removeAt >= 0) { mixList.RemoveAt(removeAt); changed = true; }
        return changed;
    }
    private void DrawSkeCreateMix(string[] shapeNames, Color prevBg)
    {
        // ─ 쉐이프키 선택 ────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            language == LanguagePreset.Korean  ? "쉐이프키 선택 (클릭으로 추가)"
          : language == LanguagePreset.Japanese ? "シェイプキー選択 (クリックで追加)"
          : "Select Shape Key (click Add)",
            EditorStyles.boldLabel);
        GUILayout.Space(2);
        DrawSkeShapeKeyList(shapeNames, 0, prevBg);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);

        // ─ 혼합 목록 ────────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            language == LanguagePreset.Korean  ? "혼합 목록"
          : language == LanguagePreset.Japanese ? "ミックスリスト" : "Mix List",
            EditorStyles.boldLabel);
        GUILayout.Space(3);
        if (DrawSkeMixList(_skeMixEntries, shapeNames, _skeMixScroll, out _skeMixScroll, prevBg, false))
            SkeApplyMixPreview(_skeMixEntries);

        GUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.27f, 0.55f, 0.82f);
        string prevL = language == LanguagePreset.Korean ? "미리보기 갱신" : language == LanguagePreset.Japanese ? "プレビュー更新" : "Update Preview";
        if (GUILayout.Button(prevL, GUILayout.Height(22))) SkeApplyMixPreview(_skeMixEntries);
        GUI.backgroundColor = new Color(0.38f, 0.38f, 0.38f);
        string restL = language == LanguagePreset.Korean ? "원본 복원" : language == LanguagePreset.Japanese ? "元に戻す" : "Restore";
        if (GUILayout.Button(restL, GUILayout.Height(22))) { SkeRestoreAndClearPreview(); _skePreviewDirty = true; }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);

        // ─ 이름 + 생성 ───────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical("box");
        string nameLabel = language == LanguagePreset.Korean ? "새 쉐이프키 이름" : language == LanguagePreset.Japanese ? "新規シェイプキー名" : "New Shape Key Name";
        _skeNewName = EditorGUILayout.TextField(nameLabel, _skeNewName);
        GUILayout.Space(4);
        bool canCreate = _skeMixEntries.Count > 0 && !string.IsNullOrWhiteSpace(_skeNewName);
        EditorGUI.BeginDisabledGroup(!canCreate);
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        string createL = language == LanguagePreset.Korean ? "쉐이프키 생성" : language == LanguagePreset.Japanese ? "シェイプキー生成" : "Create Shape Key";
        if (GUILayout.Button(createL, GUILayout.Height(28)))
        {
            SkeRestoreAndClearPreview();
            var entries = _skeMixEntries.Select(e => ((int)e.index, e.weight)).ToList<(int, float)>();
            bool ok = DiNeShapeKeyEditorCore.CreateMixedShapeKey(_skeSmr, entries, _skeNewName, out string err);
            _skeStatus = ok
                ? (language == LanguagePreset.Korean ? $"✅ '{_skeNewName}' 생성 완료" : language == LanguagePreset.Japanese ? $"✅ '{_skeNewName}' 生成完了" : $"✅ '{_skeNewName}' created")
                : err;
            _skeStatusIsError = !ok;
            if (ok) _skeNewName = "";
        }
        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawSkeModify(string[] shapeNames, Color prevBg)
    {
        // ─ 수정 방식 토글 ─────────────────────────────────────────────────────
        string[] modifyModes = language == LanguagePreset.Korean  ? new[] { "배율 조정", "믹스로 교체" }
                             : language == LanguagePreset.Japanese ? new[] { "倍率調整", "ミックス置換" }
                             : new[] { "Scale", "Replace with Mix" };
        int newModSub = DrawCustomToolbar(_skeModifySubMode, modifyModes, 24);
        if (newModSub != _skeModifySubMode)
        {
            SkeRestoreAndClearPreview();
            _skeModifySubMode = newModSub;
            _skePreviewDirty = true;
        }
        GUILayout.Space(5);

        string selectedName = _skeModifyIndex >= 0 && _skeModifyIndex < shapeNames.Length
            ? shapeNames[_skeModifyIndex] : "-";

        // ─ 대상 쉐이프키 선택 ─────────────────────────────────────────────────
        EditorGUILayout.BeginVertical("box");
        string listHeader = language == LanguagePreset.Korean  ? $"대상 쉐이프키 선택  ✦ {selectedName}"
                          : language == LanguagePreset.Japanese ? $"対象シェイプキー選択  ✦ {selectedName}"
                          : $"Select Target Shape Key  ✦ {selectedName}";
        EditorGUILayout.LabelField(listHeader, EditorStyles.boldLabel);
        GUILayout.Space(2);

        // listMode 1=배율조정(단순선택), 2=믹스교체(선택+믹스추가버튼)
        int listMode = _skeModifySubMode == 0 ? 1 : 2;
        DrawSkeShapeKeyList(shapeNames, listMode, prevBg);
        if (_skeModifySubMode == 1)
        {
            string hint = language == LanguagePreset.Korean  ? "클릭: 대상 선택(파란색)  /  [+]: 교체할 믹스에 추가(초록색)"
                        : language == LanguagePreset.Japanese ? "クリック: 対象選択  /  [+]: ミックスに追加"
                        : "Click: select target (blue)  /  [+]: add to mix (green)";
            EditorGUILayout.HelpBox(hint, MessageType.None);
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);

        if (_skeModifySubMode == 0)
        {
            // ─ 배율 조정 ──────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(
                language == LanguagePreset.Korean ? "배율 조정" : language == LanguagePreset.Japanese ? "倍率調整" : "Scale Adjustment",
                EditorStyles.boldLabel);
            GUILayout.Space(3);
            string scaleLabel = language == LanguagePreset.Korean ? "새 배율 (%)" : language == LanguagePreset.Japanese ? "新しい倍率 (%)" : "New Scale (%)";
            _skeModifyScale = EditorGUILayout.Slider(scaleLabel, _skeModifyScale, 0f, 200f);
            if (!Mathf.Approximately(_skeModifyScale, 100f))
            {
                string info = language == LanguagePreset.Korean
                    ? $"기존 100% 값이 새 100% 기준 {_skeModifyScale:F0}%로 변경됩니다"
                    : language == LanguagePreset.Japanese
                    ? $"既存の100%が新しい基準で{_skeModifyScale:F0}%に変更されます"
                    : $"Existing 100% will become {_skeModifyScale:F0}% of the new maximum";
                EditorGUILayout.HelpBox(info, MessageType.None);
            }
            GUILayout.Space(4);
            EditorGUI.BeginDisabledGroup(Mathf.Approximately(_skeModifyScale, 0f));
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            string applyL = language == LanguagePreset.Korean ? "배율 적용" : language == LanguagePreset.Japanese ? "倍率を適用" : "Apply Scale";
            if (GUILayout.Button(applyL, GUILayout.Height(28)))
            {
                SkeRestoreAndClearPreview();
                float factor = _skeModifyScale / 100f;
                bool ok = DiNeShapeKeyEditorCore.ModifyShapeKeyScale(_skeSmr, _skeModifyIndex, factor, out string err);
                string kn = _skeModifyIndex < shapeNames.Length ? shapeNames[_skeModifyIndex] : _skeModifyIndex.ToString();
                _skeStatus = ok
                    ? (language == LanguagePreset.Korean ? $"✅ '{kn}' 배율 수정 완료" : language == LanguagePreset.Japanese ? $"✅ '{kn}' 倍率修正完了" : $"✅ '{kn}' scale modified")
                    : err;
                _skeStatusIsError = !ok;
                if (ok) SkeApplyModifyPreview(_skeModifyIndex);
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
        else
        {
            // ─ 믹스로 교체 ────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(
                language == LanguagePreset.Korean  ? $"교체할 내용 (믹스)  →  '{selectedName}'"
              : language == LanguagePreset.Japanese ? $"置換内容 (ミックス)  →  '{selectedName}'"
              : $"Replacement Mix  →  '{selectedName}'",
                EditorStyles.boldLabel);
            GUILayout.Space(3);

            if (DrawSkeMixList(_skeModifyMixEntries, shapeNames, _skeModifyMixScroll, out _skeModifyMixScroll, prevBg, true))
                SkeApplyMixPreview(_skeModifyMixEntries);

            GUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.27f, 0.55f, 0.82f);
            string prevL2 = language == LanguagePreset.Korean ? "미리보기 갱신" : language == LanguagePreset.Japanese ? "プレビュー更新" : "Update Preview";
            if (GUILayout.Button(prevL2, GUILayout.Height(22))) SkeApplyMixPreview(_skeModifyMixEntries);
            GUI.backgroundColor = new Color(0.38f, 0.38f, 0.38f);
            string restL2 = language == LanguagePreset.Korean ? "원본 복원" : language == LanguagePreset.Japanese ? "元に戻す" : "Restore";
            if (GUILayout.Button(restL2, GUILayout.Height(22))) { SkeRestoreAndClearPreview(); _skePreviewDirty = true; }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            bool canReplace = _skeModifyMixEntries.Count > 0;
            EditorGUI.BeginDisabledGroup(!canReplace);
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            string replaceL = language == LanguagePreset.Korean  ? "믹스로 교체 적용"
                            : language == LanguagePreset.Japanese ? "ミックスで置換を適用" : "Apply Mix Replace";
            if (GUILayout.Button(replaceL, GUILayout.Height(28)))
            {
                SkeRestoreAndClearPreview();
                var entries = _skeModifyMixEntries.Select(e => ((int)e.index, e.weight)).ToList<(int, float)>();
                bool ok = DiNeShapeKeyEditorCore.ReplaceShapeKeyWithMix(_skeSmr, _skeModifyIndex, entries, out string err);
                string kn = _skeModifyIndex < shapeNames.Length ? shapeNames[_skeModifyIndex] : _skeModifyIndex.ToString();
                _skeStatus = ok
                    ? (language == LanguagePreset.Korean ? $"✅ '{kn}' 믹스 교체 완료" : language == LanguagePreset.Japanese ? $"✅ '{kn}' ミックス置換完了" : $"✅ '{kn}' replaced with mix")
                    : err;
                _skeStatusIsError = !ok;
                if (ok) SkeApplyModifyPreview(_skeModifyIndex);
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
    private void SkeApplyModifyPreview(int keyIndex)
    {
        if (_skeSmr == null || _skeSmr.sharedMesh == null) return;
        SkeRestoreAndClearPreview();
        int n = _skeSmr.sharedMesh.blendShapeCount;
        _skeOrigWeights = new float[n];
        for (int i = 0; i < n; i++) _skeOrigWeights[i] = _skeSmr.GetBlendShapeWeight(i);
        _skeHasPreviewWeights = true;
        for (int i = 0; i < n; i++) _skeSmr.SetBlendShapeWeight(i, i == keyIndex ? 100f : 0f);
        _skePreviewDirty = true;
        Repaint();
    }

    private void SkeApplyMixPreview(List<DiNeSkeMixEntry> mixList)
    {
        if (_skeSmr == null || _skeSmr.sharedMesh == null) return;
        SkeRestoreAndClearPreview();
        int n = _skeSmr.sharedMesh.blendShapeCount;
        _skeOrigWeights = new float[n];
        for (int i = 0; i < n; i++) _skeOrigWeights[i] = _skeSmr.GetBlendShapeWeight(i);
        _skeHasPreviewWeights = true;
        for (int i = 0; i < n; i++) _skeSmr.SetBlendShapeWeight(i, 0f);
        foreach (var entry in mixList)
        {
            if (entry.index >= 0 && entry.index < n)
                _skeSmr.SetBlendShapeWeight(entry.index, entry.weight);
        }
        _skePreviewDirty = true;
        Repaint();
    }

    private void SkeRestoreAndClearPreview()
    {
        if (!_skeHasPreviewWeights || _skeSmr == null || _skeOrigWeights == null) return;
        int n = Mathf.Min(_skeOrigWeights.Length,
            _skeSmr.sharedMesh != null ? _skeSmr.sharedMesh.blendShapeCount : 0);
        for (int i = 0; i < n; i++) _skeSmr.SetBlendShapeWeight(i, _skeOrigWeights[i]);
        _skeOrigWeights = null;
        _skeHasPreviewWeights = false;
        _skePreviewDirty = true;
    }
}
