using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;

public partial class ArmatureScalerEditor : EditorWindow
{
    private const string ArmaturePresetPreferenceKey = "DiNe.AviEditor.ArmaturePreset";
    private const string MaPresetPreferenceKey = "DiNe.AviEditor.MAScalePreset";

    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language
    {
        get
        {
            int value = EditorPrefs.GetInt("DiNeLang", 0);
            if (value < 0 || value > 2) value = 0;
            return (LanguagePreset)value;
        }
        set => EditorPrefs.SetInt("DiNeLang", (int)value);
    }
    private LanguagePreset appliedLanguage = (LanguagePreset)(-1);

    // ?????? ??????癲ル슢?꾤땟?????????
    private enum EditorMode { Armature = 0, ShapeKeyEditor = 3, Extra = 4 }
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
    private GUIStyle themedButtonStyle;
    private GUIStyle themedBoldButtonStyle;
    [SerializeField] private Vector2 scrollPosition;
    private Texture2D selectedButtonTex;

    private Dictionary<HumanBodyBones, Transform> boneMapping;

    private Vector3    lastKnownScale    = Vector3.one;
    private Quaternion lastKnownRotation = Quaternion.identity;
    private Vector3    lastKnownPosition = Vector3.zero;

    private string[] presetFiles;
    [SerializeField] private int    selectedPresetIndex = -1;
    [SerializeField] private string selectedPresetName  = "";

    private string armaturePresetStatus = "";
    private int skippedPresetEntries;
    [SerializeField] private List<string> maAdjustChildPositionParts = new List<string>();

    // ?????? ??ш끽維곻쭚?? ?嶺뚮㉡?€쾮???????
// ?????? Animation Freezer ??ш끽維????????
    // Each editor owns a separate renderer in its private preview scene.
    private DiNeAviHeadPreview _headPreview;

    // Mesh shape-key editing stays in Avi Editor.
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
    private readonly Dictionary<int, float> _skePreviewWeights = new Dictionary<int, float>();
    private Mesh                  _skePreviewMesh;
    private Mesh                  _skeSourceMesh;
    private SkinnedMeshRenderer   _skePreviewSource;
    private GUIStyle              _skeRowStyle;
    private GUIStyle              _skeSelectedRowStyle;
    // ??????ш낄援???域밸Ŧ遊얕짆??
    private string                _skeSearch             = "";
    private Vector2               _skeSKListScroll;

    // Extra
    [SerializeField] private Vector2 _extraScroll;

    // ── 얼굴 미리보기 카메라 (표정/쉐이프키 공용) ──
    [SerializeField] private float   _headPrevYaw   = 0f;    // 좌우 회전 (도)
    [SerializeField] private float   _headPrevPitch = 0f;    // 상하 회전 (도)
    [SerializeField] private float   _headPrevZoom  = 1f;    // 1 = 머리 전체가 들어오는 기본 배율
    [SerializeField] private Vector2 _headPrevPan   = Vector2.zero; // 머리 크기 기준 비율 오프셋

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
        LanguagePreset selectedLanguage = language;
        SetLanguage(selectedLanguage);
        appliedLanguage = selectedLanguage;
        if (currentMode != EditorMode.Armature && currentMode != EditorMode.ShapeKeyEditor && currentMode != EditorMode.Extra)
            currentMode = EditorMode.Armature;
        InitializeValues();
        if (targetAvatarRoot != null)
        {
            boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
            LoadCurrentValues();
        }

        RefreshPresetList();

        EditorApplication.update += OnEditorUpdate;
        EditorApplication.projectChanged += OnProjectAssetsChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.playModeStateChanged += OnPreviewPlayModeChanged;
    }

    void OnDisable()
    {
        _tutorial?.Suspend();
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.projectChanged -= OnProjectAssetsChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.playModeStateChanged -= OnPreviewPlayModeChanged;

        if (_skePreviewRT != null)
        {
            _skePreviewRT.Release();
            DestroyImmediate(_skePreviewRT);
            _skePreviewRT = null;
        }
        SkeRestoreAndClearPreview();
        ReleaseHeadPreview();
    }

    private void ReleaseHeadPreview()
    {
        _headPreview?.Dispose();
        _headPreview = null;
        _skePreviewDirty = true;
    }

    private void OnPreviewPlayModeChanged(PlayModeStateChange state)
    {
        SkeRestoreAndClearPreview();
        ReleaseHeadPreview();
        Repaint();
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
        LoadCurrentValues();
        armaturePresetStatus = "";
        SkeRestoreAndClearPreview();
        _skeModifyScale = 100f;
        ReleaseHeadPreview();
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

    private void OnProjectAssetsChanged()
    {
        RefreshPresetList();
        Repaint();
    }

    private void RefreshPresetList(string preferredPresetPath = null)
    {
        if (string.IsNullOrEmpty(preferredPresetPath) &&
            presetFiles != null && selectedPresetIndex >= 0 && selectedPresetIndex < presetFiles.Length)
            preferredPresetPath = presetFiles[selectedPresetIndex];
        string contextPath = GetPresetContextPath();
        presetFiles = DiNePresetAssetSelector.FindPresetPaths<ArmatureScalerPresetData>()
            .Concat(DiNePresetAssetSelector.FindPresetPaths<MAScaleAdjusterPresetData>())
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(Path.GetFileNameWithoutExtension, System.StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(path => path, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (string.IsNullOrEmpty(preferredPresetPath) &&
            string.IsNullOrEmpty(EditorPrefs.GetString(ArmaturePresetPreferenceKey + ".Guid", "")))
            preferredPresetPath = EditorPrefs.GetString(MaPresetPreferenceKey + ".Path", "");
        selectedPresetIndex = DiNePresetAssetSelector.RestoreSelection(
            ArmaturePresetPreferenceKey,
            presetFiles,
            preferredPresetPath,
            contextPath);
        selectedPresetName = selectedPresetIndex >= 0
            ? Path.GetFileNameWithoutExtension(presetFiles[selectedPresetIndex])
            : "";
    }

    private string GetPresetContextPath()
    {
        if (targetAvatarRoot == null) return string.Empty;
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(targetAvatarRoot);
        return !string.IsNullOrEmpty(prefabPath) ? prefabPath : targetAvatarRoot.scene.path;
    }

    private void SelectArmaturePreset(int index)
    {
        armaturePresetStatus = "";
        selectedPresetIndex = index;
        selectedPresetName = index >= 0 && index < presetFiles.Length
            ? Path.GetFileNameWithoutExtension(presetFiles[index])
            : "";
        if (index >= 0 && index < presetFiles.Length)
            DiNePresetAssetSelector.RememberSelection(ArmaturePresetPreferenceKey, presetFiles[index]);
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
        BeginTutorialFrame();
        try
        {
        Color headerBackground = GUI.backgroundColor;
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
                "Edit your avatar's armature, mesh shape keys, and PhysBone settings.",
                "아바타의 본, 메시 쉐이프키와 PhysBone 설정을 편집합니다.",
                "アバターのボーン、メッシュのシェイプキー、PhysBone設定を編集します。"),
            new GUIStyle(EditorStyles.wordWrappedLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = headerBackground;

        GUILayout.Space(5);

        LanguagePreset selectedLanguage = language;
        if (selectedLanguage != appliedLanguage)
        {
            armaturePresetStatus = "";
            SetLanguage(selectedLanguage);
            appliedLanguage = selectedLanguage;
        }

        int currentLanguageIndex = (int)selectedLanguage;
        string[] languageButtons = { "English", "한국어", "日本語" };
        int newLanguageIndex = DrawCustomToolbar(currentLanguageIndex, languageButtons, 35);
        if (newLanguageIndex != currentLanguageIndex)
        {
            armaturePresetStatus = "";
            language = (LanguagePreset)newLanguageIndex;
            selectedLanguage = language;
            SetLanguage(selectedLanguage);
            appliedLanguage = selectedLanguage;
        }
        _tutorial.DrawControls();
        GUILayout.Space(15);

        string[] modeLabels =
        {
            Tr("Armature", "아마추어", "アーマチュア"),
            Tr("Shape Key", "쉐이프키", "シェイプキー"),
            Tr("PhysBone", "PhysBone", "PhysBone")
        };
        int currentMainMode = GetMainModeIndex();
        int newMainMode = DrawCustomToolbar(currentMainMode, modeLabels, 35);
        if (newMainMode != currentMainMode)
            SetMainModeIndex(newMainMode);

        GUILayout.Space(10);

        if (currentMode == EditorMode.Armature)
            DrawArmatureGUI();
        else if (currentMode == EditorMode.ShapeKeyEditor)
            DrawShapeKeyEditorGUI();
        else
            DrawExtraGUI();
            }
        finally { _tutorial.EndFrame(); }
    }

    private int GetMainModeIndex()
    {
        return currentMode == EditorMode.ShapeKeyEditor ? 1 : currentMode == EditorMode.Extra ? 2 : 0;
    }

    private void SetMainModeIndex(int index)
    {
        currentMode = index == 1 ? EditorMode.ShapeKeyEditor : index == 2 ? EditorMode.Extra : EditorMode.Armature;
        SkeRestoreAndClearPreview();
        GUI.FocusControl(null);
    }


    private void DrawExtraGUI()
    {
        GameObject tutorialPreviousAvatar = targetAvatarRoot;
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        GameObject nextAvatarRoot = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent(
                Tr("Avatar", "아바타", "アバター"),
                Tr("The avatar root whose child PhysBones will be edited.",
                    "하위 PhysBone을 일괄 편집할 아바타 루트입니다.",
                    "子PhysBoneを一括編集するアバタールートです。")),
            targetAvatarRoot, typeof(GameObject), true);
        TutorialAnchor("avatar");
        if (EditorGUI.EndChangeCheck())
        {
            targetAvatarRoot = nextAvatarRoot;
            if (targetAvatarRoot != null)
            {
                boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
                LoadCurrentValues();
            }
            else
            {
                boneMapping = null;
                InitializeValues();
            }

            selectedPart = HumanoidBodyPart.None;
            GUI.FocusControl(null);
        }

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(new GUIContent("\u21BA", Tr("Refresh", "새로고침", "更新")),
                GUILayout.Width(28), GUILayout.Height(18)))
        {
            Repaint();
        }
        GUI.backgroundColor = previousBackground;
        EditorGUI.EndDisabledGroup();
        TutorialAnchor("refresh");
        EditorGUILayout.EndHorizontal();
        if (targetAvatarRoot != tutorialPreviousAvatar) TutorialNotify("avatar");
        TutorialDraw("avatar", "refresh");

        GUILayout.Space(6);

        if (targetAvatarRoot == null)
        {
            EditorGUILayout.HelpBox(
                Tr("Assign an avatar to batch edit its PhysBones.",
                    "PhysBone을 일괄 편집할 아바타를 지정해 주세요.",
                    "PhysBoneを一括編集するアバターを指定してください。"),
                MessageType.Info);
            return;
        }

        _tutorial?.BeginScrollScope();
        _extraScroll = EditorGUILayout.BeginScrollView(_extraScroll);

        DiNePhysBoneBatchSummary total = DiNePhysBoneBatchUtility.GetSummary(
            targetAvatarRoot, DiNePhysBoneBatchSetting.AllowGrabbing);

        EditorGUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField(
            Tr("PhysBone Interaction", "PhysBone 상호작용", "PhysBoneインタラクション"),
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            Tr($"Found {total.Total} PhysBone component(s), including inactive objects.",
                $"비활성 오브젝트를 포함해 PhysBone {total.Total}개를 찾았습니다.",
                $"非アクティブを含むPhysBoneが{total.Total}個見つかりました。"),
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        EditorGUI.BeginDisabledGroup(total.Total == 0);
        DrawPhysBoneBatchRow(
            DiNePhysBoneBatchSetting.AllowGrabbing,
            Tr("Grabbing", "잡기", "つかむ"),
            Tr("Allow players to grab and move PhysBones.",
                "플레이어가 PhysBone을 잡아 움직일 수 있게 합니다.",
                "プレイヤーがPhysBoneをつかんで動かせるようにします。"));

        GUILayout.Space(8);

        DrawPhysBoneBatchRow(
            DiNePhysBoneBatchSetting.AllowPosing,
            Tr("Pose Lock", "포즈 고정", "ポーズ固定"),
            Tr("Allow a grabbed PhysBone to remain fixed in a pose.",
                "잡은 PhysBone을 원하는 자세로 고정할 수 있게 합니다.",
                "つかんだPhysBoneを任意のポーズで固定できるようにします。"));

        GUILayout.Space(8);

        DrawPhysBoneBatchRow(
            DiNePhysBoneBatchSetting.AllowCollision,
            Tr("Player Collider Response", "플레이어 콜라이더 반응", "プレイヤーコライダー反応"),
            Tr("Control collisions with player hands and other global colliders.",
                "플레이어의 손과 기타 글로벌 콜라이더에 대한 충돌 반응을 조절합니다.",
                "プレイヤーの手やその他のグローバルコライダーとの衝突を調整します。"));
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            Tr("Turning off Player Collider Response does not remove or disable colliders explicitly assigned in each PhysBone's Colliders list.",
                "플레이어 콜라이더 반응을 꺼도 각 PhysBone의 Colliders 목록에 직접 지정한 콜라이더는 제거되거나 비활성화되지 않습니다.",
                "プレイヤーコライダー反応をオフにしても、各PhysBoneのCollidersリストに直接指定したコライダーは削除・無効化されません。"),
            MessageType.Info);

        EditorGUILayout.EndScrollView();
        _tutorial?.EndScrollScope(GUILayoutUtility.GetLastRect());
    }

    private void DrawPhysBoneBatchRow(
        DiNePhysBoneBatchSetting setting, string title, string description)
    {
        string tutorialPrefix = setting == DiNePhysBoneBatchSetting.AllowGrabbing ? "grab" : setting == DiNePhysBoneBatchSetting.AllowPosing ? "pose" : "collision";
        DiNePhysBoneBatchSummary summary = DiNePhysBoneBatchUtility.GetSummary(targetAvatarRoot, setting);

        EditorGUILayout.BeginVertical("GroupBox");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(FormatPhysBoneSummary(summary), EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        GUIContent enableContent = new GUIContent(
            Tr("Enable All", "모두 켜기", "すべてオン"),
            Tr("Enable this setting on every found PhysBone.",
                "찾은 모든 PhysBone에서 이 설정을 켭니다.",
                "見つかったすべてのPhysBoneでこの設定をオンにします。"));
        if (GUILayout.Button(enableContent, GUILayout.Height(30)))
            ApplyPhysBoneBatchSetting(setting, true, title);
        TutorialAnchor(tutorialPrefix + "-on");

        GUI.backgroundColor = previousBackground;
        GUIContent disableContent = new GUIContent(
            Tr("Disable All", "모두 끄기", "すべてオフ"),
            Tr("Disable this setting on every found PhysBone.",
                "찾은 모든 PhysBone에서 이 설정을 끕니다.",
                "見つかったすべてのPhysBoneでこの設定をオフにします。"));
        if (GUILayout.Button(disableContent, GUILayout.Height(30)))
            ApplyPhysBoneBatchSetting(setting, false, title);
        TutorialAnchor(tutorialPrefix + "-off");

        GUI.backgroundColor = previousBackground;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        TutorialDraw(tutorialPrefix + "-on", tutorialPrefix + "-off");
    }

    private string FormatPhysBoneSummary(DiNePhysBoneBatchSummary summary)
    {
        string result = Tr(
            $"On {summary.Enabled} / Off {summary.Disabled}",
            $"켜짐 {summary.Enabled} / 꺼짐 {summary.Disabled}",
            $"オン {summary.Enabled} / オフ {summary.Disabled}");

        if (summary.Custom > 0)
            result += Tr($" / Custom {summary.Custom}", $" / 개별 {summary.Custom}", $" / 個別 {summary.Custom}");
        if (summary.Unsupported > 0)
            result += Tr($" / Unsupported {summary.Unsupported}", $" / 미지원 {summary.Unsupported}", $" / 未対応 {summary.Unsupported}");

        return result;
    }

    private void ApplyPhysBoneBatchSetting(
        DiNePhysBoneBatchSetting setting, bool enabled, string settingTitle)
    {
        DiNePhysBoneBatchResult result = DiNePhysBoneBatchUtility.Apply(targetAvatarRoot, setting, enabled);
        if (result.Unsupported > 0)
        {
            Debug.LogWarning(Tr(
                $"[Avi Editor] {result.Unsupported} PhysBone(s) did not expose the {settingTitle} setting.",
                $"[Avi Editor] PhysBone {result.Unsupported}개에서 {settingTitle} 설정을 찾지 못했습니다.",
                $"[Avi Editor] {result.Unsupported}個のPhysBoneで{settingTitle}設定が見つかりませんでした。"),
                targetAvatarRoot);
        }

        Debug.Log(Tr(
            $"[Avi Editor] {settingTitle}: set {result.Changed} of {result.Total} PhysBone(s) to {(enabled ? "On" : "Off")}.",
            $"[Avi Editor] {settingTitle}: PhysBone {result.Total}개 중 {result.Changed}개를 {(enabled ? "켜짐" : "꺼짐")}으로 변경했습니다.",
            $"[Avi Editor] {settingTitle}: {result.Total}個のPhysBoneのうち{result.Changed}個を{(enabled ? "オン" : "オフ")}に変更しました。"),
            targetAvatarRoot);

        SceneView.RepaintAll();
        Repaint();
    }
    private GameObject _boneMappingRoot;

    private void DrawArmatureGUI()
    {
        GameObject tutorialPreviousAvatar = targetAvatarRoot;
        // 다른 탭에서 대상 아바타가 바뀌었으면 본 매핑을 다시 만든다.
        if (targetAvatarRoot != _boneMappingRoot && Event.current.type == EventType.Layout)
        {
            armaturePresetStatus = "";
            _boneMappingRoot = targetAvatarRoot;
            selectedPart = HumanoidBodyPart.None;
            if (targetAvatarRoot != null)
            {
                boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
                LoadCurrentValues();
            }
            else
            {
                boneMapping = null;
                InitializeValues();
            }
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(UI_TEXT[0], targetAvatarRoot, typeof(GameObject), true);
        TutorialAnchor("avatar");

        if (EditorGUI.EndChangeCheck())
        {
            armaturePresetStatus = "";
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
        TutorialAnchor("refresh");
        EditorGUILayout.EndHorizontal();
        if (targetAvatarRoot != tutorialPreviousAvatar) TutorialNotify("avatar");
        TutorialDraw("avatar", "refresh");

        GUILayout.Space(4);
        string[] armatureModes =
        {
            Tr("Direct Bone Editing", "직접 뼈 조정", "ボーン直接調整"),
            Tr("MA Scale Adjustment", "MA 비율 조정", "MA比率調整")
        };
        int nextArmatureMode = DrawCustomToolbar((int)armatureEditMode, armatureModes, 30);
        TutorialAnchor("direct-mode");
        TutorialAnchor("ma-mode");
        TutorialDraw("direct-mode", "ma-mode");
        if (nextArmatureMode != (int)armatureEditMode)
        {
            armatureEditMode = (ArmatureEditMode)nextArmatureMode;
            if (armatureEditMode == ArmatureEditMode.DirectTransform) LoadCurrentValues();
            selectedPart = HumanoidBodyPart.None;
            GUI.FocusControl(null);
        }

        DrawArmaturePresetGUI();

        _tutorial?.BeginScrollScope();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        HumanoidBodyPart tutorialPreviousPart = selectedPart;
        DrawBodyMap();
        TutorialAnchor("bone");
        if (selectedPart != tutorialPreviousPart) TutorialNotify("bone");
        EditorGUILayout.EndScrollView();
        _tutorial?.EndScrollScope(GUILayoutUtility.GetLastRect());
        TutorialDraw("bone");

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
            TutorialAnchor(armatureEditMode == ArmatureEditMode.DirectTransform ? "scale-uniform" : "ma-scale-uniform");
            if (EditorGUI.EndChangeCheck())
            {
                ApplySelectedScale(new Vector3(uniformScale, uniformScale, uniformScale));
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newScale = EditorGUILayout.Vector3Field(GetPartName(selectedPart) + $" Scale", scale);
            TutorialAnchor(armatureEditMode == ArmatureEditMode.DirectTransform ? "scale-vector" : "ma-scale-vector");
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
            TutorialAnchor("position");
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
                TutorialAnchor("rotation");
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
        TutorialDraw("scale-uniform", "scale-vector", "position", "rotation", "ma-scale-uniform", "ma-scale-vector");
    }

    // ?????? ???ル늅??씤異?에?ル씔???癲ル슢?꾤땟???GUI ??????
    private void DrawArmaturePresetGUI()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(Tr("Armature Presets", "아마추어 프리셋", "アーマチュアプリセット"), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(Tr(
            "Save direct bone size, position and rotation together with MA Scale Adjuster values.",
            "기본 뼈의 크기·위치·회전과 MA Scale Adjuster 값을 함께 저장합니다.",
            "ボーンのサイズ・位置・回転とMA Scale Adjusterの値をまとめて保存します。"), EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(3f);
        int nextPreset = DiNePresetAssetSelector.DrawPopup(
            new GUIContent(
                Tr("Select Preset", "프리셋 선택", "プリセット選択"),
                Tr("Project presets, including older bone and MA presets, are detected automatically.",
                    "기존 기본 뼈·MA 프리셋도 자동으로 인식합니다.",
                    "従来のボーン・MAプリセットも自動検出します。")),
            selectedPresetIndex,
            presetFiles,
            Tr("No presets found", "프리셋 없음", "プリセットなし"));
        TutorialAnchor("preset");
        if (nextPreset != selectedPresetIndex)
            SelectArmaturePreset(nextPreset);

        bool hasSelection = presetFiles != null && selectedPresetIndex >= 0 && selectedPresetIndex < presetFiles.Length;
        ArmatureScalerPresetData selectedPreset = hasSelection
            ? AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(presetFiles[selectedPresetIndex]) : null;
        bool hasDirectData = selectedPreset != null;
        bool hasMaData = hasSelection && (selectedPreset == null || selectedPreset.maScales?.Count > 0);
        if (hasSelection && !hasDirectData)
            EditorGUILayout.LabelField(Tr("Legacy MA preset: loads MA values only.", "기존 MA 프리셋: MA 값만 불러옵니다.",
                "従来のMAプリセット：MAの値のみ読み込みます。"), EditorStyles.wordWrappedMiniLabel);

        GUILayout.Space(3f);
        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null || !hasSelection);
        if (DrawThemedButton(
                new GUIContent(Tr("Load Entire Preset", "프리셋 전체 불러오기", "プリセット全体を読み込む"),
                    Tr("Apply the saved direct bone values and MA Scale Adjusters together. Missing Adjusters are added.",
                        "저장된 기본 뼈 값과 MA Scale Adjuster를 함께 적용합니다. 없는 Adjuster는 추가합니다.",
                        "保存したボーンの値とMA Scale Adjusterをまとめて適用します。未追加のAdjusterは追加します。")),
                new Color(0.30f, 0.82f, 0.76f), true, GUILayout.Height(28)))
            LoadSelectedPreset();
        TutorialAnchor("preset-load");

        EditorGUI.BeginDisabledGroup(!hasDirectData);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(Tr("Bone Scale Only", "기본 크기만", "ボーンサイズのみ"),
                Tr("Load direct bone scale without changing MA Scale Adjusters, rotation or position.",
                    "MA Scale Adjuster·회전·위치를 유지하고 기본 뼈 크기만 불러옵니다.",
                    "MA Scale Adjuster・回転・位置を保持し、ボーンのサイズのみ読み込みます。")), GUILayout.Height(24)))
            LoadSelectedPreset(true, false, false, false);
        TutorialAnchor("preset-scale");
        if (GUILayout.Button(new GUIContent(Tr("Rotation Only", "회전만", "回転のみ"),
                Tr("Load saved bone rotations only.", "저장된 뼈 회전만 불러옵니다.", "保存したボーンの回転のみ読み込みます。")), GUILayout.Height(24)))
            LoadSelectedPreset(false, true, false, false);
        TutorialAnchor("preset-rotation");
        if (GUILayout.Button(new GUIContent(Tr("Position Only", "위치만", "位置のみ"),
                Tr("Load saved bone positions only.", "저장된 뼈 위치만 불러옵니다.", "保存したボーンの位置のみ読み込みます。")), GUILayout.Height(24)))
            LoadSelectedPreset(false, false, true, false);
        TutorialAnchor("preset-position");
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!hasMaData);
        if (GUILayout.Button(new GUIContent(Tr("MA Scale Adjuster Only", "MA Scale Adjuster만 불러오기", "MA Scale Adjusterのみ読み込む"),
                Tr("Apply MA values and saved child-position options without loading direct bone values.",
                    "기본 뼈 값을 불러오지 않고 MA 값과 저장된 자식 위치 조정 옵션을 적용합니다.",
                    "ボーンの値を読み込まず、MAの値と保存した子位置調整オプションを適用します。")), GUILayout.Height(24)))
            LoadSelectedPreset(false, false, false, true);
        TutorialAnchor("preset-ma");
        EditorGUI.EndDisabledGroup();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        if (GUILayout.Button(
                new GUIContent(Tr("＋ Save Both as New Preset", "＋ 두 조정값을 새 프리셋으로 저장", "＋ 両方の調整値を新規プリセットとして保存"),
                    Tr("Save the current values from both editing modes in one asset.",
                        "두 조정 모드의 현재 값을 하나의 에셋에 저장합니다.",
                        "両方の調整モードの現在値を1つのアセットに保存します。")), GUILayout.Height(30f)))
            SaveNewPreset();
        TutorialAnchor("preset-save");
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!hasSelection);
        if (DrawThemedButton(UI_TEXT[31], new Color(0.78f, 0.34f, 0.34f), false, GUILayout.Height(24)) &&
            EditorUtility.DisplayDialog(UI_TEXT[31], UI_TEXT[32] + selectedPresetName + UI_TEXT[33], UI_TEXT[34], UI_TEXT[35]))
        {
            string deletedPath = presetFiles[selectedPresetIndex];
            DeletePreset(deletedPath);
            armaturePresetStatus = "";
            RefreshPresetList(preferredPresetPath: deletedPath);
        }
        TutorialAnchor("preset-delete");
        EditorGUI.EndDisabledGroup();
        if (armatureEditMode == ArmatureEditMode.DirectTransform)
        {
            EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
            if (GUILayout.Button(UI_TEXT[36], GUILayout.Height(24))) ResetScalesToDefault();
            TutorialAnchor("reset-scales");
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(armaturePresetStatus))
            EditorGUILayout.HelpBox(armaturePresetStatus, skippedPresetEntries > 0 ? MessageType.Warning : MessageType.Info);
        EditorGUILayout.EndVertical();
        TutorialDraw("preset", "preset-load", "preset-scale", "preset-rotation", "preset-position", "preset-ma", "preset-save", "preset-delete", "reset-scales");
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
            TutorialAnchor("ma-add");
        }
        else
        {
            bool nextAdjustChildPositions = DrawThemedCheckboxToggle(
                Tr("Adjust Child Positions", "자식 위치 조정", "子位置調整"),
                adjustChildPositions,
                GUILayout.Height(25));
            TutorialAnchor("ma-children");
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
        TutorialAnchor("ma-remove");
        EditorGUILayout.EndHorizontal();
        TutorialDraw("ma-add", "ma-children", "ma-remove");

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

    // --- ???쒓낯???ApplyPose ??딅텑???---

    // ??ш끽維곻쭚?? ???ル늅?????縕?猿녿뎨?T???????怨좊룴??????

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
    private bool TryGetPresetBone(string key, out HumanoidBodyPart part, out Transform boneTransform)
    {
        boneTransform = null;
        return System.Enum.TryParse(key, out part) && part != HumanoidBodyPart.None &&
            System.Enum.IsDefined(typeof(HumanoidBodyPart), part) &&
            TryGetLiveBoneTransform(GetBoneType(part), out boneTransform);
    }

    private void CaptureArmaturePreset(ArmatureScalerPresetData preset)
    {
        preset.scales.dictionary.Clear();
        preset.rotations.dictionary.Clear();
        preset.positions.dictionary.Clear();
        if (preset.maScales == null) preset.maScales = new List<MAScaleAdjusterPresetData.Entry>();
        preset.maScales.Clear();

        // Read the scene instead of the editing-mode cache, which can be stale in MA mode.
        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part == HumanoidBodyPart.None || !TryGetLiveBoneTransform(GetBoneType(part), out Transform boneTransform)) continue;
            string key = part.ToString();
            preset.scales[key] = new ArmatureScalerPresetData.SerializableVector3(boneTransform.localScale);
            preset.positions[key] = new ArmatureScalerPresetData.SerializableVector3(boneTransform.localPosition);
            if (CanRotate(part))
                preset.rotations[key] = new ArmatureScalerPresetData.SerializableQuaternion(boneTransform.localRotation);
            ModularAvatarScaleAdjuster adjuster = boneTransform.GetComponent<ModularAvatarScaleAdjuster>();
            if (adjuster != null)
                preset.maScales.Add(new MAScaleAdjusterPresetData.Entry(key, adjuster.Scale, GetMAAdjustChildPositions(part)));
        }
        preset.scales.OnBeforeSerialize();
        preset.rotations.OnBeforeSerialize();
        preset.positions.OnBeforeSerialize();
    }

    // Returns skipped entries. Unrecorded Adjusters are preserved, including for legacy presets.
    private int ApplyMAPresetEntries(IReadOnlyList<MAScaleAdjusterPresetData.Entry> entries)
    {
        if (entries == null) return 0;
        int skipped = 0;
        foreach (var entry in entries)
        {
            if (entry == null || !TryGetPresetBone(entry.part, out HumanoidBodyPart part, out Transform boneTransform))
            {
                skipped++;
                continue;
            }
            SetMAAdjustChildPositions(part, entry.adjustChildPositions);
            SetMAScale(boneTransform, entry.Scale, "Load Armature Preset", entry.adjustChildPositions);
        }
        return skipped;
    }

    private void ApplyArmaturePreset(ArmatureScalerPresetData preset, bool applyScale, bool applyRotation,
        bool applyPosition, bool applyMaScale)
    {
        if (preset == null || targetAvatarRoot == null) return;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Load Armature Preset");
        Undo.RecordObject(this, "Load Armature Preset");
        skippedPresetEntries = 0;
        try
        {
            // MA child adjustment comes first. Saved absolute bone positions below take precedence,
            // so mapped children are restored exactly while unrecorded children retain MA behavior.
            if (applyMaScale) skippedPresetEntries += ApplyMAPresetEntries(preset.maScales);
            var keys = new HashSet<string>();
            if (applyScale && preset.scales != null) keys.UnionWith(preset.scales.dictionary.Keys);
            if (applyRotation && preset.rotations != null) keys.UnionWith(preset.rotations.dictionary.Keys);
            if (applyPosition && preset.positions != null) keys.UnionWith(preset.positions.dictionary.Keys);
            foreach (string key in keys)
            {
                if (!TryGetPresetBone(key, out HumanoidBodyPart part, out Transform boneTransform))
                {
                    skippedPresetEntries++;
                    continue;
                }
                Undo.RecordObject(boneTransform, "Load Armature Preset");
                if (applyScale && preset.scales != null && preset.scales.TryGetValue(key, out var scale) && scale != null)
                    boneTransform.localScale = scale.ToVector3();
                if (applyRotation && CanRotate(part) && preset.rotations != null && preset.rotations.TryGetValue(key, out var rotation) && rotation != null)
                    boneTransform.localRotation = rotation.ToQuaternion();
                if (applyPosition && preset.positions != null && preset.positions.TryGetValue(key, out var position) && position != null)
                    boneTransform.localPosition = position.ToVector3();
                ForceUpdateScene(boneTransform);
            }
            LoadCurrentValues();
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private void LoadSelectedPreset(bool applyScale = true, bool applyRotation = true, bool applyPosition = true,
        bool applyMaScale = true)
    {
        if (targetAvatarRoot == null)
        {
            Debug.LogError(Tr("Please assign an avatar first.", "먼저 아바타를 지정해 주세요.", "先にアバターを指定してください。"));
            return;
        }

        if (presetFiles != null && selectedPresetIndex >= 0 && selectedPresetIndex < presetFiles.Length)
        {
            string filePath = presetFiles[selectedPresetIndex];
            DiNePresetAssetSelector.RememberSelection(ArmaturePresetPreferenceKey, filePath);
            ArmatureScalerPresetData loadedData = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(filePath);
            if (loadedData != null)
            {
                ApplyArmaturePreset(loadedData, applyScale, applyRotation, applyPosition, applyMaScale);
            }
            else
            {
                MAScaleAdjusterPresetData legacyPreset = AssetDatabase.LoadAssetAtPath<MAScaleAdjusterPresetData>(filePath);
                if (legacyPreset == null || !applyMaScale) return;
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Load Armature Preset");
                Undo.RecordObject(this, "Load Armature Preset");
                try
                {
                    skippedPresetEntries = ApplyMAPresetEntries(legacyPreset.entries);
                    LoadCurrentValues();
                }
                finally
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }
            }
            string name = Path.GetFileNameWithoutExtension(filePath);
            armaturePresetStatus = Tr($"Loaded '{name}'.", $"'{name}' 프리셋을 불러왔습니다.", $"'{name}' を読み込みました。");
            if (skippedPresetEntries > 0)
                armaturePresetStatus += Tr($" Skipped {skippedPresetEntries} missing or invalid entries.",
                    $" 찾을 수 없거나 잘못된 항목 {skippedPresetEntries}개를 건너뛰었습니다.",
                    $" 見つからない、または無効な{skippedPresetEntries}項目をスキップしました。");
            Repaint();
        }
    }
    private void SaveNewPreset()
    {
        if (targetAvatarRoot == null) return;
        string path = EditorUtility.SaveFilePanelInProject(
            Tr("Save Armature Preset", "아마추어 프리셋 저장", "アーマチュアプリセットを保存"),
            "NewArmaturePreset",
            "asset",
            Tr("Choose a save location and name.", "저장 위치와 이름을 선택하세요.", "保存先と名前を選択してください。"));
        if (string.IsNullOrEmpty(path)) return;

        ArmatureScalerPresetData preset = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(path);
        if (preset == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            EditorUtility.DisplayDialog(Tr("Choose a New Preset File", "새 프리셋 파일 선택", "新しいプリセットファイルを選択"),
                Tr("This file is a different asset type. Save the combined preset under a new name.",
                    "이 파일은 다른 종류의 에셋입니다. 통합 프리셋을 새 이름으로 저장하세요.",
                    "このファイルは別のアセット形式です。統合プリセットを新しい名前で保存してください。"),
                Tr("OK", "확인", "確認"));
            return;
        }
        bool isNew = preset == null;
        if (!isNew)
        {
            if (!EditorUtility.DisplayDialog(
                    Tr("Overwrite Preset", "프리셋 덮어쓰기", "プリセットを上書き"),
                    Tr("Overwrite the existing preset?", "기존 프리셋을 덮어쓸까요?", "既存のプリセットを上書きしますか？"),
                    Tr("Overwrite", "덮어쓰기", "上書き"),
                    Tr("Cancel", "취소", "キャンセル")))
                return;
            Undo.RecordObject(preset, "Overwrite Armature Preset");
        }
        else
        {
            preset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
        }

        CaptureArmaturePreset(preset);

        if (isNew)
            AssetDatabase.CreateAsset(preset, path);
        else
            EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshPresetList(preferredPresetPath: path);
        EditorGUIUtility.PingObject(preset);
        skippedPresetEntries = 0;
        armaturePresetStatus = Tr($"Saved bone and MA values: {Path.GetFileNameWithoutExtension(path)}",
            $"기본 뼈·MA 값 저장 완료: {Path.GetFileNameWithoutExtension(path)}",
            $"ボーン・MAの値を保存しました: {Path.GetFileNameWithoutExtension(path)}");
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
        return DrawThemedButton(new GUIContent(label), backgroundColor, bold, options);
    }

    private bool DrawThemedButton(
        GUIContent label,
        Color backgroundColor,
        bool bold,
        params GUILayoutOption[] options)
    {
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;
        GUIStyle style = bold ? themedBoldButtonStyle : themedButtonStyle;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.button)
            {
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            if (bold) themedBoldButtonStyle = style;
            else themedButtonStyle = style;
        }
        bool pressed = GUILayout.Button(label, style, options);
        GUI.backgroundColor = previousBackground;
        return pressed;
    }

    // ── 얼굴 미리보기 공용 로직 (표정 / 쉐이프키 탭 공용) ──────────────
    // 머리 본 위치·머리 크기·정면 방향을 실제 계층에서 구해서 카메라를 배치한다.
    // 예전에는 "루트 위치 + 1.5m" 와 고정 거리 0.45m 를 썼기 때문에
    // 아바타 키가 다르거나 루트가 회전돼 있으면 얼굴이 화면 밖으로 벗어났다.
    private bool ComputeHeadFraming(Renderer fallbackRenderer, out Vector3 focus, out Vector3 faceDir, out float headSize)
    {
        return DiNeAvatarHeadFraming.Compute(targetAvatarRoot, fallbackRenderer, out focus, out faceDir, out headSize);
    }

    // Renderer bounds may be enlarged for culling (often to several metres).
    // Measure the posed vertices without changing the renderer or its source mesh.

    // 좌(왼쪽 본) → 우(오른쪽 본) 벡터에서 정면 방향을 구한다.

    private void RenderHeadPreviewTo(RenderTexture rt, SkinnedMeshRenderer fallbackRenderer,
        IReadOnlyDictionary<int, float> weights = null, Mesh previewMesh = null)
    {
        if (rt == null) return;

        Vector3 focus, faceDir;
        float   headSize;
        if (!ComputeHeadFraming(fallbackRenderer, out focus, out faceDir, out headSize)) return;

        const float fov = 30f;
        float zoom   = Mathf.Clamp(_headPrevZoom, 0.25f, 8f);
        float viewH  = Mathf.Max(0.01f, headSize * 1.9f / zoom);
        float dist   = (viewH * 0.5f) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        Vector3 dir   = Quaternion.AngleAxis(_headPrevYaw, Vector3.up) * faceDir;
        Vector3 axis  = Vector3.Cross(Vector3.up, dir).normalized;
        if (axis.sqrMagnitude > 1e-6f)
            dir = Quaternion.AngleAxis(-Mathf.Clamp(_headPrevPitch, -80f, 80f), axis) * dir;

        Vector3 camPos = focus + dir.normalized * dist;

        Quaternion camRotation = Quaternion.LookRotation((focus - camPos).normalized, Vector3.up);
        camPos += camRotation * Vector3.right * (_headPrevPan.x * headSize)
                + camRotation * Vector3.up * (_headPrevPan.y * headSize);

        if (_headPreview == null) _headPreview = new DiNeAviHeadPreview();
        _headPreview.Render(targetAvatarRoot, fallbackRenderer, weights, previewMesh, rt,
            camPos, camRotation, fov, Mathf.Max(0.001f, dist * 0.02f), dist * 10f + 100f);
    }

    // 드래그 = 회전, 휠 = 확대/축소, 가운데 버튼(또는 Alt+드래그) = 이동
    private bool HandleHeadPreviewInput(Rect r)
    {
        Event e = Event.current;
        if (e == null || !r.Contains(e.mousePosition)) return false;

        if (e.type == EventType.MouseDrag && (e.button == 0 || e.button == 2))
        {
            if (e.button == 2 || e.alt)
            {
                _headPrevPan.x -= e.delta.x / Mathf.Max(1f, r.width)  * 2f;
                _headPrevPan.y += e.delta.y / Mathf.Max(1f, r.height) * 2f;
            }
            else
            {
                _headPrevYaw   += e.delta.x * 0.5f;
                _headPrevPitch  = Mathf.Clamp(_headPrevPitch - e.delta.y * 0.5f, -80f, 80f);
            }
            e.Use();
            Repaint();
            return true;
        }

        if (e.type == EventType.ScrollWheel)
        {
            _headPrevZoom = Mathf.Clamp(_headPrevZoom * (1f - e.delta.y * 0.05f), 0.25f, 8f);
            e.Use();
            Repaint();
            return true;
        }

        return false;
    }

    // 미리보기 위에 겹쳐 그리는 시점 초기화 버튼
    private void DrawHeadPreviewToolbar(Rect r)
    {
        var resetRect = new Rect(r.xMax - 26f, r.y + 4f, 22f, 18f);
        TutorialAnchor("head-reset", resetRect);
        var tip = new GUIContent("⟳", Tr("Reset view (drag: rotate, wheel: zoom, alt+drag: pan)",
                                         "시점 초기화 (드래그: 회전, 휠: 확대, Alt+드래그: 이동)",
                                         "視点リセット (ドラッグ: 回転, ホイール: ズーム, Alt+ドラッグ: 移動)"));
        if (GUI.Button(resetRect, tip, EditorStyles.miniButton))
        {
            ResetHeadPreviewView();
        }
    }

    private void ResetHeadPreviewView()
    {
        _headPrevYaw      = 0f;
        _headPrevPitch    = 0f;
        _headPrevZoom     = 1f;
        _headPrevPan      = Vector2.zero;
        _skePreviewDirty  = true;
        Repaint();
    }

    /// <summary>
    /// FX ???爾??용굞肉???곷첓??癲ル슢?꾤땟??????ル늅??씤異?에?ル씔????????????blendShape ??ш끽維곩ㅇ???紐껎룂 ?????shapekey癲??????쒓낯???筌뤾퍓???
    /// ??좊즴???0?????100???????れ삀??쎈뭄????????ш낄援??????????? ?袁⑸즵????筌뤾퍓???
    /// </summary>

    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已?
    //  SHAPE KEY EDITOR TAB
    // ??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已??誘딆궠已?
    private void DrawShapeKeyEditorGUI()
    {
        GameObject tutorialPreviousAvatar = targetAvatarRoot;
        SkinnedMeshRenderer tutorialPreviousMesh = _skeSmr;
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
        TutorialAnchor("avatar");
        if (EditorGUI.EndChangeCheck() || targetAvatarRoot != _skePrevTarget)
        {
            _skePrevTarget = targetAvatarRoot;
            SkeRestoreAndClearPreview();
            _skeSmr = DiNeShapeKeyEditorCore.FindBodySmr(targetAvatarRoot);
            SkeResetTarget();
            _skeStatus = "";
            _skePreviewDirty = true;
        }

        EditorGUI.BeginDisabledGroup(targetAvatarRoot == null);
        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("↺", GUILayout.Width(28), GUILayout.Height(18)))
        {
            SkeRestoreAndClearPreview();
            _skeSmr = DiNeShapeKeyEditorCore.FindBodySmr(targetAvatarRoot);
            SkeResetTarget();
            _skeStatus = "";
            _skePreviewDirty = true;
        }
        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();
        TutorialAnchor("refresh");
        EditorGUILayout.EndHorizontal();
        if (targetAvatarRoot != tutorialPreviousAvatar) TutorialNotify("avatar");
        TutorialDraw("avatar", "refresh");

        EditorGUI.BeginChangeCheck();
        _skeSmr = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            language == LanguagePreset.Korean  ? "대상 메쉬"
          : language == LanguagePreset.Japanese ? "対象メッシュ" : "Target Mesh",
            _skeSmr, typeof(SkinnedMeshRenderer), true);
        TutorialAnchor("mesh");
        if (EditorGUI.EndChangeCheck())
        {
            SkeResetTarget();
            _skeStatus = "";
            _skePreviewDirty = true;
        }

        EditorGUILayout.EndVertical();
        if (_skeSmr != tutorialPreviousMesh || targetAvatarRoot != tutorialPreviousAvatar) TutorialNotify("mesh");
        TutorialDraw("mesh");
        GUILayout.Space(5);

        if (_skePreviewSource != _skeSmr) SkeResetTarget();
        if (_skeSmr != null && _skeSourceMesh != _skeSmr.sharedMesh)
        {
            SkeResetTarget();
        }

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
        _skeModifyIndex = Mathf.Clamp(_skeModifyIndex, 0, shapeNames.Length - 1);

        // ─ 프리뷰 ────────────────────────────────────────────────────────────
        DrawSkePreview();
        _tutorial?.BeginScrollScope();
        _skeOuterScroll = EditorGUILayout.BeginScrollView(_skeOuterScroll);
        EditorGUILayout.LabelField(Tr(
            "Preview stays in this window. Create / Apply saves the mesh to the avatar.",
            "미리보기는 이 창에서만 표시됩니다. 생성 / 적용을 누르면 아바타에 반영됩니다.",
            "プレビューはこのウィンドウ内だけに表示されます。作成 / 適用でアバターに反映します。"),
            EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(5);

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
        _tutorial?.EndScrollScope(GUILayoutUtility.GetLastRect());
    }
    // Keep the face visible while controls scroll, with room left for controls in short windows.
    private float HeadPreviewSize => Mathf.Max(1f, Mathf.Min(position.width - 20f, 360f,
        Mathf.Max(180f, position.height * 0.38f)));

    private void DrawSkePreview()
    {
        float size = HeadPreviewSize;
        Rect previewRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
        previewRect.x = Mathf.Max(0f, (position.width - size) * 0.5f);

        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.76f, 0.76f, 0.76f);
        EditorGUI.DrawRect(previewRect, bgCol);

        if (Event.current.type == EventType.Repaint)
        {
            RenderSkePreview((int)size);
            if (_skePreviewRT != null)
                GUI.DrawTexture(previewRect, _skePreviewRT, ScaleMode.ScaleToFit, true);
            _skePreviewDirty = false;
        }

        if (HandleHeadPreviewInput(previewRect))
        {
            _skePreviewDirty  = true;
        }
        DrawHeadPreviewToolbar(previewRect);
        TutorialAnchor("head-rotate", previewRect);
        TutorialAnchor("head-zoom", previewRect);
        TutorialAnchor("head-pan", previewRect);
        TutorialDraw("head-rotate", "head-zoom", "head-pan", "head-reset");

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
            if (_skePreviewRT != null)
            {
                _skePreviewRT.Release();
                DestroyImmediate(_skePreviewRT);
            }
            _skePreviewRT = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
                { hideFlags = HideFlags.HideAndDontSave };
            _skePreviewRT.antiAliasing = 2;
            _skePreviewRT.Create();
        }

        // 머리 위치·크기·정면 방향은 표정 탭과 동일한 공용 로직으로 계산한다.
        RenderHeadPreviewTo(_skePreviewRT, _skeSmr, _skePreviewWeights, _skePreviewMesh);
    }

    // listMode: 0=create(??⑤베堉??類????  1=modify-select(???????ャ뀕??  2=modify-select+雅?퍔瑗????뽱돯??
    private void DrawSkeShapeKeyList(string[] shapeNames, int listMode, Color prevBg)
    {
        if (_skeRowStyle == null)
        {
            _skeRowStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
            _skeSelectedRowStyle = new GUIStyle(_skeRowStyle)
                { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
        _skeSearch = EditorGUILayout.TextField("", _skeSearch, EditorStyles.toolbarSearchField);
        TutorialAnchor("shape-search");
        TutorialDraw("shape-search");
        GUILayout.Space(2);
        string searchLower = _skeSearch.ToLower();
        string modifyPreviewTooltip = Tr(
            "Preview this key while keeping the avatar's other values. Click again to restore its original value.",
            "아바타의 다른 값은 유지하고 이 키만 미리 봅니다. 다시 누르면 원래 값으로 돌아갑니다.",
            "アバターの他の値を維持して、このキーだけをプレビューします。もう一度押すと元の値に戻ります。");

        _tutorial?.BeginScrollScope();
        _skeSKListScroll = EditorGUILayout.BeginScrollView(_skeSKListScroll, GUILayout.Height(210));
        for (int i = 0; i < shapeNames.Length; i++)
        {
            string name = shapeNames[i];
            if (!string.IsNullOrEmpty(searchLower) && !name.ToLower().Contains(searchLower)) continue;

            bool isTarget   = (listMode >= 1) && _skeModifyIndex == i && _skePreviewWeights.ContainsKey(i);
            bool isInCreate = listMode == 0 && _skeMixEntries.Any(e => e.index == i);
            bool isInModMix = listMode == 2 && _skeModifyMixEntries.Any(e => e.index == i);

            EditorGUILayout.BeginHorizontal();

            if (listMode == 0) // ─ 새로 만들기: 레이블 + [추가] ─
            {
                GUILayout.Label(name, GUILayout.ExpandWidth(true));
                GUI.backgroundColor = isInCreate ? new Color(0.30f, 0.82f, 0.76f) : prevBg;
                string addL = language == LanguagePreset.Korean ? "추가" : language == LanguagePreset.Japanese ? "追加" : "Add";
                EditorGUI.BeginDisabledGroup(isInCreate);
                if (GUILayout.Button(addL, GUILayout.Width(42), GUILayout.Height(19)))
                {
                    _skeMixEntries.Add(new DiNeSkeMixEntry { index = i, weight = 100f });
                    SkeApplyMixPreview(_skeMixEntries);
                }
                EditorGUI.EndDisabledGroup();
                TutorialAnchor("mix-add");
                GUI.backgroundColor = prevBg;
            }
            else // ─ 수정하기: 클릭으로 대상 선택 (+ 믹스 추가 버튼) ─
            {
                GUI.backgroundColor = isTarget ? new Color(0.30f, 0.82f, 0.76f) : prevBg;
                GUIStyle rowStyle = isTarget ? _skeSelectedRowStyle : _skeRowStyle;
                if (GUILayout.Button(new GUIContent(name, modifyPreviewTooltip), rowStyle,
                        GUILayout.ExpandWidth(true), GUILayout.Height(20)))
                {
                    SkeSelectModifyTarget(i);
                }
                TutorialAnchor("shape-select");
                if (listMode == 2) // 믹스 추가 버튼
                {
                    GUI.backgroundColor = isInModMix ? new Color(0.30f, 0.82f, 0.76f) : prevBg;
                    EditorGUI.BeginDisabledGroup(isInModMix);
                    if (GUILayout.Button(new GUIContent("+", Tr("Add to mix", "믹스에 추가", "ミックスに追加")),
                            GUILayout.Width(24), GUILayout.Height(20)))
                    {
                        _skeModifyMixEntries.Add(new DiNeSkeMixEntry { index = i, weight = 100f });
                        SkeApplyMixPreview(_skeModifyMixEntries);
                    }
                    TutorialAnchor("mix-add");
                    EditorGUI.EndDisabledGroup();
                }
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndHorizontal();
            TutorialDraw("mix-add", "shape-select");
        }
        EditorGUILayout.EndScrollView();
        _tutorial?.EndScrollScope(GUILayoutUtility.GetLastRect());
    }
    private bool DrawSkeMixList(List<DiNeSkeMixEntry> mixList, string[] shapeNames,
        Vector2 scroll, out Vector2 newScroll, Color prevBg, bool isModifyMix)
    {
        bool changed = false;
        _tutorial?.BeginScrollScope();
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
            TutorialAnchor("mix-weight");
            if (EditorGUI.EndChangeCheck()) changed = true;
            if (GUILayout.Button(new GUIContent("−", Tr("Remove from mix", "믹스에서 제거", "ミックスから削除")),
                    GUILayout.Width(22), GUILayout.Height(18)))
                removeAt = i;
            TutorialAnchor("mix-remove");
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            TutorialDraw("mix-weight", "mix-remove");
        }
        EditorGUILayout.EndScrollView();
        _tutorial?.EndScrollScope(GUILayoutUtility.GetLastRect());
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
        TutorialAnchor("mix-preview");
        GUI.backgroundColor = new Color(0.38f, 0.38f, 0.38f);
        string restL = language == LanguagePreset.Korean ? "원본 복원" : language == LanguagePreset.Japanese ? "元に戻す" : "Restore";
        if (GUILayout.Button(restL, GUILayout.Height(22))) { SkeRestoreAndClearPreview(); _skePreviewDirty = true; }
        TutorialAnchor("mix-restore");
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        TutorialDraw("mix-preview", "mix-restore");
        GUILayout.Space(5);

        // ─ 이름 + 생성 ───────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical("box");
        string nameLabel = language == LanguagePreset.Korean ? "새 쉐이프키 이름" : language == LanguagePreset.Japanese ? "新規シェイプキー名" : "New Shape Key Name";
        _skeNewName = EditorGUILayout.TextField(nameLabel, _skeNewName);
        TutorialAnchor("shape-name");
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
            if (ok)
            {
                _skeNewName = "";
                SkeMeshApplied();
            }
        }
        GUI.backgroundColor = prevBg;
        TutorialAnchor("shape-create");
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
        TutorialDraw("shape-name", "shape-create");
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
            string hint = language == LanguagePreset.Korean  ? "클릭: 대상 선택  /  [+]: 교체할 믹스에 추가"
                        : language == LanguagePreset.Japanese ? "クリック: 対象選択  /  [+]: ミックスに追加"
                        : "Click: select target  /  [+]: add to mix";
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
            EditorGUI.BeginChangeCheck();
            _skeModifyScale = EditorGUILayout.Slider(scaleLabel, _skeModifyScale, 0f, 200f);
            TutorialAnchor("shape-scale");
            if (EditorGUI.EndChangeCheck())
            {
                if (!_skePreviewWeights.ContainsKey(_skeModifyIndex))
                    _skePreviewWeights[_skeModifyIndex] = 100f;
                SkeUpdateScalePreviewMesh();
                Repaint();
            }

            float previewWeight = _skePreviewWeights.TryGetValue(_skeModifyIndex, out float weight)
                ? weight : _skeSmr.GetBlendShapeWeight(_skeModifyIndex);
            EditorGUI.BeginChangeCheck();
            previewWeight = EditorGUILayout.Slider(new GUIContent(
                Tr("Preview weight", "미리보기 값", "プレビュー値"),
                Tr("Test this key at different weights without changing the scene.",
                    "씬을 변경하지 않고 이 키를 여러 값으로 확인합니다.",
                    "シーンを変更せず、このキーを異なる値で確認します。")), previewWeight, 0f, 100f);
            TutorialAnchor("shape-preview-weight");
            if (EditorGUI.EndChangeCheck())
            {
                _skePreviewWeights[_skeModifyIndex] = previewWeight;
                _skePreviewDirty = true;
                Repaint();
            }
            if (!Mathf.Approximately(_skeModifyScale, 100f))
            {
                string info = Tr(
                    $"At weight 100, deformation becomes {_skeModifyScale:F0}% of the current mesh. Apply saves this change.",
                    $"키 값 100일 때 변형이 현재 메쉬의 {_skeModifyScale:F0}%가 됩니다. 적용을 누르면 저장됩니다.",
                    $"キー値100の変形が現在のメッシュの{_skeModifyScale:F0}%になります。適用すると保存します。");
                EditorGUILayout.HelpBox(info, MessageType.None);
            }
            GUILayout.Space(4);
            EditorGUI.BeginDisabledGroup(Mathf.Approximately(_skeModifyScale, 0f));
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            string applyL = language == LanguagePreset.Korean ? "배율 적용" : language == LanguagePreset.Japanese ? "倍率を適用" : "Apply Scale";
            if (GUILayout.Button(applyL, GUILayout.Height(28)))
            {
                float factor = _skeModifyScale / 100f;
                bool ok = DiNeShapeKeyEditorCore.ModifyShapeKeyScale(_skeSmr, _skeModifyIndex, factor, out string err);
                string kn = _skeModifyIndex < shapeNames.Length ? shapeNames[_skeModifyIndex] : _skeModifyIndex.ToString();
                _skeStatus = ok
                    ? Tr($"✅ '{kn}' scale {factor * 100f:F0}% applied",
                        $"✅ '{kn}' 배율 {factor * 100f:F0}% 적용 완료",
                        $"✅ '{kn}' 倍率{factor * 100f:F0}%を適用しました")
                    : err;
                _skeStatusIsError = !ok;
                if (ok) SkeMeshApplied();
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();
            TutorialAnchor("shape-apply-scale");
            if (GUILayout.Button(Tr("Restore preview", "미리보기 초기화", "プレビューをリセット"), GUILayout.Height(24)))
            {
                SkeRestoreAndClearPreview();
                Repaint();
            }
            TutorialAnchor("shape-reset-preview");
            EditorGUILayout.EndVertical();
            TutorialDraw("shape-scale", "shape-preview-weight", "shape-apply-scale", "shape-reset-preview");
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
            TutorialAnchor("mix-preview");
            GUI.backgroundColor = new Color(0.38f, 0.38f, 0.38f);
            string restL2 = language == LanguagePreset.Korean ? "원본 복원" : language == LanguagePreset.Japanese ? "元に戻す" : "Restore";
            if (GUILayout.Button(restL2, GUILayout.Height(22))) { SkeRestoreAndClearPreview(); _skePreviewDirty = true; }
            TutorialAnchor("mix-restore");
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            TutorialDraw("mix-preview", "mix-restore");
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
                if (ok)
                {
                    SkeMeshApplied();
                    SkeApplyModifyPreview(_skeModifyIndex);
                }
            }
            GUI.backgroundColor = prevBg;
            TutorialAnchor("shape-replace");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            TutorialDraw("shape-replace");
        }
    }
    private void SkeSelectModifyTarget(int keyIndex)
    {
        if (_skeSmr == null || _skeSmr.sharedMesh == null ||
            keyIndex < 0 || keyIndex >= _skeSmr.sharedMesh.blendShapeCount) return;

        if (_skeModifyIndex == keyIndex && _skePreviewWeights.ContainsKey(keyIndex))
        {
            SkeRestoreAndClearPreview();
            Repaint();
            return;
        }

        if (_skeModifyIndex != keyIndex) _skeModifyScale = 100f;
        _skeModifyIndex = keyIndex;
        if (_skeModifySubMode == 1 && _skeModifyMixEntries.Count > 0)
            SkeApplyMixPreview(_skeModifyMixEntries);
        else
            SkeApplyModifyPreview(keyIndex);
    }

    private void SkeApplyModifyPreview(int keyIndex)
    {
        if (_skeSmr == null || _skeSmr.sharedMesh == null) return;
        if (keyIndex < 0 || keyIndex >= _skeSmr.sharedMesh.blendShapeCount) return;
        // Only the current target is overridden. Removing the previous override lets
        // the isolated renderer use that key's original (possibly non-zero) weight.
        _skePreviewWeights.Clear();
        _skePreviewWeights[keyIndex] = 100f;
        SkeUpdateScalePreviewMesh();
        Repaint();
    }

    private void SkeApplyMixPreview(List<DiNeSkeMixEntry> mixList)
    {
        if (_skeSmr == null || _skeSmr.sharedMesh == null) return;
        SkeRestoreAndClearPreview();
        if (_skeSubMode == 1 && _skeModifySubMode == 1)
        {
            // Preview the replacement itself so existing values on contributing keys
            // remain visible exactly as they will after Apply.
            var entries = mixList.Select(e => (e.index, e.weight)).ToList();
            _skePreviewMesh = DiNeShapeKeyEditorCore.BuildReplacementMesh(
                _skeSmr.sharedMesh, _skeModifyIndex, entries, out _);
            if (_skePreviewMesh != null)
            {
                _skePreviewMesh.hideFlags = HideFlags.HideAndDontSave;
                _skePreviewWeights[_skeModifyIndex] = 100f;
                _skeStatus = "";
                _skeStatusIsError = false;
            }
            else if (mixList.Count > 0)
            {
                _skeStatus = Tr("Add a shape key with a non-zero weight to preview the replacement.",
                    "교체할 쉐이프키를 추가하고 값을 0보다 높게 설정해 주세요.",
                    "置換するシェイプキーを追加し、値を0より大きくしてください。");
                _skeStatusIsError = true;
            }
            Repaint();
            return;
        }
        int n = _skeSmr.sharedMesh.blendShapeCount;
        foreach (var entry in mixList)
        {
            if (entry.index >= 0 && entry.index < n)
            {
                _skePreviewWeights.TryGetValue(entry.index, out float accumulated);
                _skePreviewWeights[entry.index] = accumulated + entry.weight;
            }
        }
        _skePreviewDirty = true;
        Repaint();
    }

    private void SkeRestoreAndClearPreview()
    {
        _skePreviewWeights.Clear();
        SkeReleaseScalePreviewMesh();
        _skeModifyScale = 100f;
        _skePreviewDirty = true;
    }

    private void SkeReleaseScalePreviewMesh()
    {
        if (_skePreviewMesh != null) DestroyImmediate(_skePreviewMesh);
        _skePreviewMesh = null;
    }

    private void SkeUpdateScalePreviewMesh()
    {
        SkeReleaseScalePreviewMesh();
        if (_skeSubMode == 1 && _skeModifySubMode == 0 && _skeSmr != null &&
            _skeSmr.sharedMesh != null && _skeModifyIndex >= 0 &&
            _skeModifyIndex < _skeSmr.sharedMesh.blendShapeCount &&
            !Mathf.Approximately(_skeModifyScale, 100f))
        {
            _skePreviewMesh = DiNeShapeKeyEditorCore.BuildScaledMesh(
                _skeSmr.sharedMesh, _skeModifyIndex, _skeModifyScale / 100f);
            _skePreviewMesh.hideFlags = HideFlags.HideAndDontSave;
        }
        _skePreviewDirty = true;
    }

    private void SkeResetTarget()
    {
        SkeRestoreAndClearPreview();
        _skePreviewSource = _skeSmr;
        _skeSourceMesh = _skeSmr != null ? _skeSmr.sharedMesh : null;
        _skeMixEntries.Clear();
        _skeModifyMixEntries.Clear();
        _skeModifyIndex = 0;
        _skeModifyScale = 100f;
        _skeStatus = "";
        ReleaseHeadPreview();
    }

    private void SkeMeshApplied()
    {
        SkeReleaseScalePreviewMesh();
        _skeSourceMesh = _skeSmr.sharedMesh;
        // The saved mesh now includes this factor; start the next edit at 100%.
        _skeModifyScale = 100f;
        _skePreviewDirty = true;
        Repaint();
    }
}
