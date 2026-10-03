#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(DiNeMultiDresser))]
public partial class DiNeMultiSupporter : Editor
{
    private Texture2D windowIcon;
    private Texture2D dresserPresetIcon;
    private Texture2D hairPresetIcon;
    private Texture2D accPresetIcon;
    private Font      titleFont;
    private int selectedLayerIndex = 0;
    private int selectedContentTab;
    private string ContentTabSessionKey => $"DiNe.MultiDresser.ContentTab.{target.GetInstanceID()}";
    private int draggedItemIndex = -1;
    private int dragTargetIndex  = -1;
    private readonly List<Rect> itemRects = new List<Rect>();
    private readonly List<string> shapeKeyNames = new List<string>();
    private readonly HashSet<string> shapeKeyNameSet = new HashSet<string>();
    private readonly HashSet<string> savedShapeKeyNames = new HashSet<string>();
    private DiNeToggleMenuChoices toggleMenuChoices;
    private string toggleTargetStatus;
    private GUIStyle cachedPreviewStyle, cachedDragHintStyle, cachedTitleStyle, cachedDescriptionStyle;
    private GUIStyle cachedToggleDropStyle;
    private GUIStyle cachedSelectedTabStyle, cachedNormalTabStyle;
    private sealed class ShapeSyncCache
    {
        public int buttonCount;
        public GameObject[] targets;
        public Mesh[] meshes;
        public int[] shapeCounts;
    }
    private readonly Dictionary<DiNeMultiDresser.DresserLayer, ShapeSyncCache> shapeSyncCache =
        new Dictionary<DiNeMultiDresser.DresserLayer, ShapeSyncCache>();

    private int previewLayerIndex  = -1;
    private int previewButtonIndex = -1;
    private readonly List<System.Action> previewRestoreActions = new List<System.Action>();
    private readonly Dictionary<GameObject, bool> previewOriginalObjectStates = new Dictionary<GameObject, bool>();
    private readonly Dictionary<SkinnedMeshRenderer, Dictionary<int, float>> previewOriginalShapeWeights =
        new Dictionary<SkinnedMeshRenderer, Dictionary<int, float>>();
    private readonly Dictionary<Renderer, Material[]> previewBaseMaterials = new Dictionary<Renderer, Material[]>();
    private string activeSkSessionKey;
    private string activeGoSessionKey;
    private string activeMatSessionKey;
    private int activePreviewOwnerId;

    private const string PreviewSessionOwnersKey = "DiNe.MultiDresser.PreviewOwners";
    private static readonly List<DiNeMultiSupporter> ActivePreviewEditors = new List<DiNeMultiSupporter>();
    private static bool previewCleanupHooksInitialized;
    private static bool restoringOrphanedPreviewSessions;

    private static bool ndmfPreviewReflectionInitialized;
    private static System.Reflection.MethodInfo invalidateNdmfPropCachesMethod;
    private static System.Reflection.MethodInfo flushNdmfInvalidatesMethod;
    private static object ndmfShadowHierarchy;
    private static System.Reflection.MethodInfo ndmfFireObjectChangeMethod;

    private static Material[] CloneMaterials(Material[] materials)
    {
        return materials != null ? (Material[])materials.Clone() : new Material[0];
    }

    private static DiNeMultiDresser.MaterialSwapEntry FindMaterialSwapEntry(DiNeMultiDresser.DresserLayer layerData, int buttonIdx, Renderer renderer)
    {
        if (layerData == null || renderer == null) return null;
        if (buttonIdx < 0 || buttonIdx >= layerData.perButtonMaterialSwaps.Count) return null;

        var swapList = layerData.perButtonMaterialSwaps[buttonIdx];
        if (swapList == null || swapList.entries == null) return null;

        return swapList.entries.Find(entry => entry != null && entry.renderer == renderer);
    }

    private static Material[] BuildPreviewBaseMaterials(DiNeMultiDresser.DresserLayer layerData, Renderer renderer)
    {
        var baseMaterials = CloneMaterials(renderer != null ? renderer.sharedMaterials : null);
        var defaultEntry = FindMaterialSwapEntry(layerData, 0, renderer);
        if (defaultEntry == null || defaultEntry.materials == null)
            return baseMaterials;

        for (int i = 0; i < defaultEntry.materials.Count && i < baseMaterials.Length; i++)
        {
            if (defaultEntry.materials[i] != null)
                baseMaterials[i] = defaultEntry.materials[i];
        }

        return baseMaterials;
    }

    private enum Language { English, Korean, Japanese }
    private static readonly string[] LangButtonLabels = { "English", "한국어", "日本語" };

    private Language currentLanguage
    {
        get 
        {
            int val = EditorPrefs.GetInt("DiNeLang", 0);
            if (val < 0 || val >= 3) val = 0;
            return (Language)val;
        }
        set => EditorPrefs.SetInt("DiNeLang", (int)value);
    }

    private readonly Dictionary<Language, Dictionary<string, string>> text = new Dictionary<Language, Dictionary<string, string>>
    {
        {
            Language.Korean, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "기본 설정 (아바타 & FX & 메뉴)" },
                { "avatarRoot", "아바타 Root" },
                { "refreshTooltip", "아바타 다시 찾기 & 저장된 설정 불러오기 (새로고침)" },
                { "shapeKeyTargets", "쉐이프키 타겟 (바디 메쉬) 🦋" },
                { "skDragHint", "여기로 드래그하여 쉐이프키 타겟 추가" },
                { "layerCategory", "옷장 카테고리 (레이어) 📂" },
                { "catName", "카테고리 이름" },
                { "delCat", "삭제" },
                { "mainDragHint", "🧲 여기로 옷 오브젝트들을 드래그하세요!" },
                { "defaultState", "기본 상태 (Default)" },
                { "menuButton", "메뉴 버튼" },
                { "defaultWarn", "기본 상태는 메뉴 버튼이 생성되지 않습니다. (꺼진 상태 or 기본 의상)" },
                { "emptyBtnWarn", "대상 오브젝트가 비어 있습니다(None).\n이대로 업로드하면 이 버튼은 자동으로 제거된 뒤 적용됩니다." },
                { "emptyBtnRemove", "이 버튼 삭제" },
                { "menuName", "메뉴 이름" },
                { "regenerateIcon", "아이콘 재생성" },
                { "regenerateIconTip", "현재 아이콘 파일을 다시 캡처해 저장합니다." },
                { "renameIcon", "이름 바꿔 생성" },
                { "renameIconTip", "새 이름의 PNG 파일로 생성합니다. 기존 파일은 보존됩니다." },
                { "linkedObj", "함께 켜질 오브젝트 (Linked):" },
                { "linkDragHint", "추가 오브젝트 드래그" },
                { "skSettings", "쉐이프키 설정 (ShapeKeys)" },
                { "generate", "🎀 적용 및 생성 (Generate)" },
                { "cleanup", "🗑 모든 데이터 삭제 (Clean Up)" }, // 추가됨
                { "catIcon", "아이콘" },
                { "fxController", "FX 컨트롤러" },
                { "expressionMenu", "익스프레션 메뉴" },
                { "newLayer", "새 레이어" },
                { "autoApplyHint", "Play Mode/업로드용 임시 아바타에만 자동 생성·적용됩니다. 원본 아바타는 변경되지 않습니다." },
                { "cleanupDialogTitle", "데이터 초기화" },
                { "cleanupDialogMsg", "드레서에 설정된 모든 데이터(레이어, 오브젝트, 쉐이프키)를 초기화하고 연결된 프리셋도 해제합니다.\n이 작업은 되돌릴 수 없습니다." },
                { "cleanupDialogOk", "초기화 (Yes)" },
                { "cleanupDialogCancel", "취소 (No)" },
                { "particle",   "파티클 오브젝트" },
                { "matSwap",    "마테리얼 교체" },
                { "addMatSwap", "+ 추가" }
            }
        },
        {
            Language.English, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "Global Settings (Avatar & FX & Menu)" },
                { "avatarRoot", "Avatar Root" },
                { "refreshTooltip", "Reload Avatar & Restore Settings (Refresh)" },
                { "shapeKeyTargets", "Shape Key Targets (Body Meshes) 🦋" },
                { "skDragHint", "Drag here to add Shape Key Targets" },
                { "layerCategory", "Outfit Categories (Layers) 📂" },
                { "catName", "Category Name" },
                { "delCat", "Delete" },
                { "mainDragHint", "🧲 Drag outfit objects here!" },
                { "defaultState", "Default State" },
                { "menuButton", "Menu Button" },
                { "defaultWarn", "Default state does not create a menu button. (Off state or Base outfit)" },
                { "emptyBtnWarn", "Target Object is empty (None).\nIf you upload as-is, this button is removed automatically before applying." },
                { "emptyBtnRemove", "Remove This Button" },
                { "menuName", "Menu Name" },
                { "regenerateIcon", "Regenerate Icon" },
                { "regenerateIconTip", "Capture and save the current icon again." },
                { "renameIcon", "Generate with New Name" },
                { "renameIconTip", "Generate a new PNG with a different name, preserving existing files." },
                { "linkedObj", "Linked Objects (Toggle Together):" },
                { "linkDragHint", "Drag extra objects here" },
                { "skSettings", "Shape Key Settings" },
                { "generate", "🎀 Generate All" },
                { "cleanup", "🗑 Clean Up All Data" }, // 추가됨
                { "catIcon", "Icon" },
                { "fxController", "FX Controller" },
                { "expressionMenu", "Expression Menu" },
                { "newLayer", "New Layer" },
                { "autoApplyHint", "All settings are generated/applied only on the temporary Play Mode or upload avatar. The original avatar is left untouched." },
                { "cleanupDialogTitle", "Reset Data" },
                { "cleanupDialogMsg", "This will clear all dresser settings (layers, objects, shape keys) and unlink the connected preset.\nThis action cannot be undone." },
                { "cleanupDialogOk", "Reset (Yes)" },
                { "cleanupDialogCancel", "Cancel (No)" },
                { "particle",   "Particle Object" },
                { "matSwap",    "Material Swap" },
                { "addMatSwap", "+ Add" }
            }
        },
        {
            Language.Japanese, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "基本設定 (アバター & FX & メニュー)" },
                { "avatarRoot", "アバター Root" },
                { "refreshTooltip", "アバター再検索 & 設定復元 (更新)" },
                { "shapeKeyTargets", "シェイプキー対象 (体メッシュ) 🦋" },
                { "skDragHint", "ここにドラッグしてシェイプキー対象を追加" },
                { "layerCategory", "衣装カテゴリー (レイヤー) 📂" },
                { "catName", "カテゴリー名" },
                { "delCat", "削除" },
                { "mainDragHint", "🧲 ここに衣装オブジェクトをドラッグ！" },
                { "defaultState", "基本状態 (Default)" },
                { "menuButton", "メニューボタン" },
                { "defaultWarn", "基本状態はメニューボタンが生成されません。(オフ状態 or 基本衣装)" },
                { "emptyBtnWarn", "対象オブジェクトが空です(None)。\nこのままアップロードすると、このボタンは自動的に削除されてから適用されます。" },
                { "emptyBtnRemove", "このボタンを削除" },
                { "menuName", "メニュー名" },
                { "regenerateIcon", "アイコン再生成" },
                { "regenerateIconTip", "現在のアイコンを再撮影して保存します。" },
                { "renameIcon", "名前を変えて生成" },
                { "renameIconTip", "既存ファイルを保持し、新しい名前のPNGを生成します。" },
                { "linkedObj", "連動オブジェクト (Linked):" },
                { "linkDragHint", "追加オブジェクトをドラッグ" },
                { "skSettings", "シェイプキー設定 (ShapeKeys)" },
                { "generate", "🎀 適用して生成 (Generate)" },
                { "cleanup", "🗑 全データ削除 (Clean Up)" }, // 추가됨
                { "catIcon", "アイコン" },
                { "fxController", "FX コントローラー" },
                { "expressionMenu", "表情メニュー" },
                { "newLayer", "新しいレイヤー" },
                { "autoApplyHint", "Play Mode / アップロード用の一時アバターにのみ自動生成・適用されます。元のアバターは変更されません。" },
                { "cleanupDialogTitle", "データ初期化" },
                { "cleanupDialogMsg", "ドレッサーに設定された全データ（レイヤー、オブジェクト、シェイプキー）を初期化し、接続されたプリセットも解除します。\nこの操作は元に戻せません。" },
                { "cleanupDialogOk", "初期化 (Yes)" },
                { "cleanupDialogCancel", "キャンセル (No)" },
                { "particle",   "パーティクル" },
                { "matSwap",    "マテリアル交換" },
                { "addMatSwap", "+ 追加" }
            }
        }
    };

    private string SkSessionKey => $"DiNe_SKWas_{target.GetInstanceID()}";
    private string GoSessionKey => $"DiNe_GOWas_{target.GetInstanceID()}";
    private string MatSessionKey => $"DiNe_MatWas_{target.GetInstanceID()}";

    [InitializeOnLoadMethod]
    private static void InitializePreviewCleanupHooks()
    {
        if (previewCleanupHooksInitialized) return;
        previewCleanupHooksInitialized = true;

        Selection.selectionChanged -= ClearAllActivePreviews;
        Selection.selectionChanged += ClearAllActivePreviews;

        AssemblyReloadEvents.beforeAssemblyReload -= ClearAllActivePreviews;
        AssemblyReloadEvents.beforeAssemblyReload += ClearAllActivePreviews;

        EditorApplication.quitting -= ClearAllActivePreviews;
        EditorApplication.quitting += ClearAllActivePreviews;

        EditorApplication.playModeStateChanged -= OnPreviewPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPreviewPlayModeStateChanged;

        // 이전 도메인이 비정상적으로 종료되어 인스턴스 복원 액션이 사라진 경우에도
        // SessionState에 남은 스냅샷으로 씬 값을 되돌린다.
        EditorApplication.delayCall += RestoreAllOrphanedPreviewSessions;
    }

    private static void OnPreviewPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            ClearAllActivePreviews();
    }

    private static void RegisterActivePreviewEditor(DiNeMultiSupporter editor)
    {
        for (int i = 0; i < ActivePreviewEditors.Count; i++)
        {
            if (ReferenceEquals(ActivePreviewEditors[i], editor)) return;
        }

        ActivePreviewEditors.Add(editor);
    }

    private static void UnregisterActivePreviewEditor(DiNeMultiSupporter editor)
    {
        for (int i = ActivePreviewEditors.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(ActivePreviewEditors[i], editor))
                ActivePreviewEditors.RemoveAt(i);
        }
    }

    public static void ClearAllActivePreviews()
    {
        DiNeTogglePreview.Clear();
        var editors = ActivePreviewEditors.ToArray();
        ActivePreviewEditors.Clear();

        for (int i = editors.Length - 1; i >= 0; i--)
        {
            var editor = editors[i];
            if (ReferenceEquals(editor, null)) continue;

            try { editor.ClearPreview(); }
            catch (System.Exception) { /* 파괴 중인 Inspector여도 나머지 세션은 계속 복원 */ }
        }

        RestoreAllOrphanedPreviewSessions();
    }

    private void OnDisable()
    {
        if (selectedContentTab == 0) SuspendTutorial();
        independentTutorial?.Suspend();
        DiNeTogglePreview.ClearForOwner(this);
        toggleMenuChoices?.Dispose();
        EditorApplication.hierarchyChanged -= InvalidateEditorCaches;
        EditorApplication.projectChanged -= InvalidateEditorCaches;
        Undo.undoRedoPerformed -= InvalidateEditorCaches;
        ClearPreview();
    }

    private void OnEnable()
    {
        selectedContentTab = Mathf.Clamp(SessionState.GetInt(ContentTabSessionKey, 0), 0, 1);
        toggleMenuChoices = new DiNeToggleMenuChoices();
        EditorApplication.hierarchyChanged += InvalidateEditorCaches;
        EditorApplication.projectChanged += InvalidateEditorCaches;
        Undo.undoRedoPerformed += InvalidateEditorCaches;
        InitializePreviewCleanupHooks();
        windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        dresserPresetIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/MultiDresser/DNDresser.png");
        hairPresetIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/MultiDresser/DNHair.png");
        accPresetIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/MultiDresser/DNAcc.png");
        titleFont  = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
        DiNeMultiDresser gen = (DiNeMultiDresser)target;
        if(gen.rootTransform == null) gen.TryAutoAssignFXController();

        int ownerId = target.GetInstanceID();
        if (!IsPreviewOwnerActive(ownerId))
        {
            bool restoredPreview = RestoreMaterialPreview(MatSessionKey);
            restoredPreview |= RestoreShapeKeyPreview(SkSessionKey);
            restoredPreview |= RestoreObjectPreview(GoSessionKey);
            UnregisterPreviewSessionOwner(ownerId);
            if (restoredPreview) ForcePreviewRepaint();
        }
        RestoreTutorial();
        if (selectedContentTab == 1) { SuspendTutorial(); tutorialActive = false; }
    }

    public override void OnInspectorGUI()
    {
        bool toggleCourse = selectedContentTab == 1;
        if (toggleCourse) { ConfigureIndependentTutorial(); independentTutorial.BeginFrame(); }
        else DiNeTutorialBubble.BeginFrame();
        try
        {
            DrawInspectorContent();
        }
        finally
        {
            if (toggleCourse) independentTutorial.EndFrame();
            else DiNeTutorialBubble.EndFrame();
        }
    }

    private void DrawInspectorContent()
    {
        serializedObject.Update();

        DiNeMultiDresser gen = (DiNeMultiDresser)target;
        SerializedProperty root = serializedObject.FindProperty("rootTransform");
        SerializedProperty controller = serializedObject.FindProperty("animatorController");
        SerializedProperty exMenu = serializedObject.FindProperty("expressionsMenu");
        SerializedProperty shapeKeyTargets = serializedObject.FindProperty("shapeKeyTargets");
        SerializedProperty layers = serializedObject.FindProperty("layers");

        DrawHeader("Multi Dresser");

        GUILayout.Space(5);
        int langIndex = DrawCustomToolbar((int)currentLanguage, LangButtonLabels, 35); 
        if ((int)currentLanguage != langIndex) currentLanguage = (Language)langIndex;
        var lang = text[currentLanguage]; 

        GUILayout.Space(15);
        if (selectedContentTab == 1) independentTutorial.DrawControls();
        else DrawTutorialControls();

        EditorGUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField(lang["globalSettings"], EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        Transform before = root.objectReferenceValue as Transform;
        Transform after = EditorGUILayout.ObjectField(lang["avatarRoot"], before, typeof(Transform), true) as Transform;
        Rect avatarRootAnchor = GUILayoutUtility.GetLastRect();

        if (GUILayout.Button(new GUIContent("↺", lang["refreshTooltip"]), GUILayout.Width(30), GUILayout.Height(20)))
        {
            Undo.RecordObject(gen, "Refresh Multi Dresser Bindings");
            // 임시(__Temp) 에셋이 아바타에 남아있으면 먼저 원본으로 복원한 뒤 재배정한다.
            // (복원 없이 재배정하면 디스크립터의 임시 값을 그대로 다시 가져와 복구가 안 된다)
            DiNeMultiDresserAutoApply.ForceRestoreNow("멀티 드레서 재배정 버튼");
            gen.ReassignFromAvatar();   // 루트/FX/메뉴/파라미터 전부 아바타 기준으로 재배정
            serializedObject.Update();
            GUI.FocusControl(null);
            NotifyTutorialAction(TutorialStep.Avatar);
        }
        EditorGUILayout.EndHorizontal();

        if (after != before) {
            root.objectReferenceValue = after;
            if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
            gen.ReassignFromAvatar();
            serializedObject.Update();
            NotifyTutorialAction(TutorialStep.Avatar);
        }
        DrawTutorialBubble(TutorialStep.Avatar, avatarRootAnchor);
        if (selectedContentTab == 1) independentTutorial.Draw("avatar", avatarRootAnchor);

        // ── 할당된 FX/메뉴가 아바타 내부의 것과 일치하는지 수시 검증 ──
        if (!gen.ValidateAssignment(out string validationMessage))
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            if (GUILayout.Button("아바타 기준으로 FX/메뉴 재배정 (↺)", GUILayout.Height(24)))
            {
                Undo.RecordObject(gen, "Refresh Multi Dresser Bindings");
                // 임시(__Temp) 에셋이 남아있으면 먼저 원본으로 복원한 뒤 재배정한다.
                DiNeMultiDresserAutoApply.ForceRestoreNow("멀티 드레서 재배정 버튼");
                gen.ReassignFromAvatar();
                serializedObject.Update();
                GUI.FocusControl(null);
                NotifyTutorialAction(TutorialStep.Avatar);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(lang["fxController"], GUILayout.Width(110));
        controller.objectReferenceValue = EditorGUILayout.ObjectField(controller.objectReferenceValue, typeof(RuntimeAnimatorController), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(lang["expressionMenu"], GUILayout.Width(110));
        exMenu.objectReferenceValue = EditorGUILayout.ObjectField(exMenu.objectReferenceValue, typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(8);
        int nextContentTab = DrawCustomToolbar(selectedContentTab, new[] {
            Localized("Wardrobe", "옷장", "衣装"),
            Localized("Independent Toggles", "독립 토글", "独立トグル")
        }, 35);
        DrawTutorialNavigation(GUILayoutUtility.GetLastRect());
        if (nextContentTab != selectedContentTab)
        {
            // Finish delayed text editing while the old page is still drawn this event.
            GUI.FocusControl(null);
        }
        GUILayout.Space(8);
        if (selectedContentTab == 0) DrawWardrobeSection(gen, shapeKeyTargets, layers, lang);
        else DrawSimpleToggleUI(gen);

        EditorGUILayout.Space(20);

        // [자동 적용 힌트]
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        EditorGUILayout.HelpBox(lang["autoApplyHint"], MessageType.Info);
        DrawTutorialBubble(TutorialStep.AutoApply, GUILayoutUtility.GetLastRect());
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        // [삭제 버튼] - 붉은색 경고 느낌
        GUI.backgroundColor = new Color(0.60f, 0.25f, 0.25f);
        var cleanBtnStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        if (false && GUILayout.Button(lang["cleanup"], cleanBtnStyle))
        {
            if (EditorUtility.DisplayDialog(lang["cleanupDialogTitle"], lang["cleanupDialogMsg"], lang["cleanupDialogOk"], lang["cleanupDialogCancel"]))
            {
                ClearPreview();
                Undo.RecordObject(gen, "Clear Multi Dresser Data");
                DiNeMultiIconGenerator.ReleaseIcons(gen);
                gen.DeleteAllGeneratedData();

                layers.ClearArray();
                shapeKeyTargets.ClearArray();
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                gen.ClearAllData(clearGeneratedData: false);
                selectedLayerIndex = 0;
                EditorUtility.SetDirty(gen);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }
        GUI.backgroundColor = Color.white;

        if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
        ValidateTutorialProgress();
        if (nextContentTab != selectedContentTab)
        {
            // Delayed fields commit on focus loss after OnInspectorGUI returns.
            // Keep their controls alive until that commit event has been handled.
            EditorApplication.delayCall += () =>
            {
                if (this != null && target != null) SelectContentTab(nextContentTab);
            };
        }
    }

    private void SelectContentTab(int tab)
    {
        tab = Mathf.Clamp(tab, 0, 1);
        if (tab == selectedContentTab) return;
        if (selectedContentTab == 0) { SuspendTutorial(); tutorialActive = false; }
        else independentTutorial?.Suspend();
        ClearPreview();
        DiNeTogglePreview.ClearForOwner(this);
        draggedItemIndex = -1;
        dragTargetIndex = -1;
        itemRects.Clear();
        selectedContentTab = tab;
        if (tab == 0) RestoreTutorial();
        SessionState.SetInt(ContentTabSessionKey, tab);
        Repaint();
    }

    private void DrawWardrobeSection(DiNeMultiDresser gen, SerializedProperty shapeKeyTargets,
        SerializedProperty layers, Dictionary<string, string> lang)
    {
        EditorGUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField(lang["shapeKeyTargets"], EditorStyles.boldLabel);
        if (DrawGlobalShapeKeyTargets(shapeKeyTargets, lang))
        {
            if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();

            foreach (var layer in gen.layers)
                SyncShapeKeyData(gen, layer);

            EditorUtility.SetDirty(gen);
            serializedObject.Update();

            if (previewLayerIndex >= 0 && previewLayerIndex < gen.layers.Count)
                RefreshPreview(gen, gen.layers[previewLayerIndex]);
        }
        EditorGUILayout.EndVertical();
        DrawTutorialBubble(TutorialStep.ShapeKeys, tutorialShapeTargetAnchor);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(lang["layerCategory"], EditorStyles.boldLabel);

        if (layers.arraySize == 0)
        {
            // 레이어가 비어있으면 기본 레이어 생성 (새 컴포넌트는 항상 빈 상태에서 시작)
            layers.InsertArrayElementAtIndex(0);
            layers.GetArrayElementAtIndex(0).FindPropertyRelative("layerName").stringValue = "Main";
            if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
            gen.layers[0].EnsureSize(0);
        }

        List<string> tabNames = new List<string>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layerNameProp = layers.GetArrayElementAtIndex(i).FindPropertyRelative("layerName");
            string name = layerNameProp != null ? layerNameProp.stringValue : $"Layer {i}";
            tabNames.Add(string.IsNullOrEmpty(name) ? $"Layer {i}" : name);
        }

        EditorGUILayout.BeginHorizontal();
        if (selectedLayerIndex >= layers.arraySize) selectedLayerIndex = layers.arraySize - 1;
        selectedLayerIndex = DrawCustomToolbar(selectedLayerIndex, tabNames.ToArray(), 35);
        Rect categoryTabsAnchor = GUILayoutUtility.GetLastRect();

        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("+", GUILayout.Width(40), GUILayout.Height(35)))
        {
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var newLayerProp = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            // 레이어 이름이 파라미터/FX 레이어/메뉴 에셋의 키라서 중복되면 서로 덮어쓴다.
            string newLayerName = lang["newLayer"];
            for (int n = 2; tabNames.Contains(newLayerName); n++) newLayerName = $"{lang["newLayer"]} {n}";
            newLayerProp.FindPropertyRelative("layerName").stringValue = newLayerName;
            newLayerProp.FindPropertyRelative("targets").ClearArray();
            newLayerProp.FindPropertyRelative("labels").ClearArray();
            newLayerProp.FindPropertyRelative("icons").ClearArray();

            if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
            gen.layers[gen.layers.Count - 1].EnsureSize(0);
            gen.layers[gen.layers.Count - 1].linkedObjects.Clear();
            gen.layers[gen.layers.Count - 1].perButtonShapeKeyStates.Clear();
            gen.layers[gen.layers.Count - 1].perButtonMaterialSwaps.Clear();
            gen.layers[gen.layers.Count - 1].particleObject = null;
            gen.layers[gen.layers.Count - 1].layerIcon = null;

            selectedLayerIndex = layers.arraySize - 1;
            GUI.FocusControl(null);
        }
        Rect addCategoryAnchor = GUILayoutUtility.GetLastRect();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        DrawTutorialBubble(TutorialStep.Categories, addCategoryAnchor);
        DrawTutorialBubble(TutorialStep.CategorySwitch, categoryTabsAnchor);

        DrawSelectedLayerUI(gen, layers, selectedLayerIndex, lang);
    }

    private void DrawSelectedLayerUI(DiNeMultiDresser gen, SerializedProperty layers, int index, Dictionary<string, string> lang)
    {
        if (index < 0 || index >= layers.arraySize) return;

        SerializedProperty layerProp = layers.GetArrayElementAtIndex(index);
        SerializedProperty layerName = layerProp.FindPropertyRelative("layerName");
        SerializedProperty layerIcon = layerProp.FindPropertyRelative("layerIcon");
        SerializedProperty targets = layerProp.FindPropertyRelative("targets");
        SerializedProperty labels = layerProp.FindPropertyRelative("labels");
        SerializedProperty icons = layerProp.FindPropertyRelative("icons");

        var currentLayerData = gen.layers[index];
        currentLayerData.EnsureSize(targets.arraySize);
        SyncShapeKeyData(gen, currentLayerData);

        // 레이어 전환 시 미리보기 해제
        if (previewLayerIndex >= 0 && previewLayerIndex != index) ClearPreview();

        EditorGUILayout.BeginVertical("helpBox"); 
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(70));
        GUILayout.Label(lang["catIcon"], EditorStyles.centeredGreyMiniLabel);
        layerIcon.objectReferenceValue = EditorGUILayout.ObjectField(layerIcon.objectReferenceValue, typeof(Texture2D), false, GUILayout.Width(64), GUILayout.Height(64));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(74));
        GUILayout.Space(18);
        DrawLayerIconPresetButton(layerIcon, "Clothes", dresserPresetIcon);
        DrawLayerIconPresetButton(layerIcon, "Hair", hairPresetIcon);
        DrawLayerIconPresetButton(layerIcon, "ACC", accPresetIcon);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        GUILayout.Space(5);
        EditorGUILayout.LabelField(lang["catName"], EditorStyles.boldLabel);
        layerName.stringValue = EditorGUILayout.TextField(layerName.stringValue, GUILayout.Height(25)); 
        Rect categoryNameAnchor = GUILayoutUtility.GetLastRect();
        Rect categoryConfirmAnchor = DrawTutorialCategoryAction(gen);
        if (categoryConfirmAnchor.width > 0f)
            categoryNameAnchor = Rect.MinMaxRect(Mathf.Min(categoryNameAnchor.xMin, categoryConfirmAnchor.xMin),
                Mathf.Min(categoryNameAnchor.yMin, categoryConfirmAnchor.yMin), Mathf.Max(categoryNameAnchor.xMax, categoryConfirmAnchor.xMax),
                Mathf.Max(categoryNameAnchor.yMax, categoryConfirmAnchor.yMax));

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (layers.arraySize > 1)
        {
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button(lang["delCat"], GUILayout.Width(80), GUILayout.Height(24)))
            {
                layers.DeleteArrayElementAtIndex(index);
                if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                selectedLayerIndex = Mathf.Max(0, index - 1);
                GUI.backgroundColor = Color.white;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.EndVertical(); 
        DrawTutorialBubble(TutorialStep.Category, categoryNameAnchor);

        EditorGUILayout.Space(8);

        // 파티클 오브젝트 (레이어 공통)
        SerializedProperty particleProp = layerProp.FindPropertyRelative("particleObject");
        EditorGUILayout.BeginHorizontal("helpBox");
        GUILayout.Label(lang["particle"], GUILayout.Width(120));
        particleProp.objectReferenceValue = EditorGUILayout.ObjectField(particleProp.objectReferenceValue, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        Event evt = Event.current;
        if (evt.type == EventType.Repaint) itemRects.Clear();

        // 대상이 비어있는(None) 버튼의 삭제 요청. GUI 레이아웃이 깨지지 않도록
        // 이번 프레임 그리기를 모두 마친 뒤에 실제 제거를 수행한다.
        int pendingEmptyButtonRemoval = -1;

        for (int i = 0; i < targets.arraySize; i++)
        {
            SerializedProperty t    = targets.GetArrayElementAtIndex(i);
            SerializedProperty l    = labels.GetArrayElementAtIndex(i);
            SerializedProperty icon = icons.GetArrayElementAtIndex(i);

            // ── 삽입 표시줄 ──
            Rect insertRect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
            if (evt.type == EventType.Repaint && draggedItemIndex >= 0 && dragTargetIndex == i)
                EditorGUI.DrawRect(new Rect(insertRect.x, insertRect.y + 1, insertRect.width, 2),
                                   new Color(0.3f, 0.82f, 0.9f));

            // ── 아이템 helpBox ──
            EditorGUILayout.BeginVertical("helpBox");

            // 헤더 행: [grip] [레이블] [X]
            EditorGUILayout.BeginHorizontal();

            Rect handleRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18), GUILayout.Height(18));
            if (evt.type == EventType.Repaint)
            {
                Color lc = (draggedItemIndex == i)
                    ? new Color(0.3f, 0.82f, 0.9f)
                    : new Color(0.6f, 0.6f, 0.6f);
                float lx = handleRect.x + 2f;
                float ly = handleRect.center.y - 3f;
                EditorGUI.DrawRect(new Rect(lx, ly,      13, 1.5f), lc);
                EditorGUI.DrawRect(new Rect(lx, ly + 3f, 13, 1.5f), lc);
                EditorGUI.DrawRect(new Rect(lx, ly + 6f, 13, 1.5f), lc);
            }
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);

            string headerLabel = (i == 0) ? lang["defaultState"] : $"{lang["menuButton"]} {i}";
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);
            Rect stateLabelAnchor = GUILayoutUtility.GetLastRect();

            // ── 미리보기 토글 ──
            bool isPreviewing = (previewLayerIndex == index && previewButtonIndex == i);
            GUI.backgroundColor = isPreviewing ? new Color(0.3f, 0.82f, 0.9f) : new Color(0.55f, 0.55f, 0.55f);
            GUIStyle previewStyle = cachedPreviewStyle ?? (cachedPreviewStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, normal = { textColor = Color.white } });
            string previewLabel = currentLanguage == Language.Korean
                ? "미리보기"
                : currentLanguage == Language.Japanese ? "プレビュー" : "Preview";
            bool previewClicked = GUILayout.Button(previewLabel, previewStyle, GUILayout.Width(76), GUILayout.Height(24));
            Rect previewAnchor = GUILayoutUtility.GetLastRect();
            if (previewClicked)
            {
                if (isPreviewing)
                {
                    bool tutorialPreview = IsTutorialPreview(index, i);
                    ClearPreview();
                    if (tutorialPreview) NotifyTutorialAction(TutorialStep.RestorePreview);
                }
                else
                {
                    ApplyPreview(gen, currentLayerData, i, index);
                    NotifyTutorialAction(TutorialStep.Preview);
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("×", GUILayout.Width(30), GUILayout.Height(24)))
            {
                ClearPreview();
                if (i < currentLayerData.icons.Count)
                {
                    var releasedIcon = currentLayerData.icons[i];
                    DiNeMultiIconGenerator.ReleaseIconReference(ref releasedIcon);
                    currentLayerData.icons[i] = releasedIcon;
                }
                currentLayerData.RemoveAt(i);
                EditorUtility.SetDirty(target);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            if (i == 0) DrawTutorialBubble(TutorialStep.DefaultState, stateLabelAnchor);
            DrawTutorialPreviewBubble(gen, index, i, previewAnchor);

            // One change-check scope for the entire item, ending before EndVertical below.
            // An extra EndChangeCheck makes idle repaints report a change and rebuild the
            // preview, whose repaint request then starts the same cycle again.
            EditorGUI.BeginChangeCheck();

            var previousTarget = t.objectReferenceValue as GameObject;
            string targetObjectLabel = currentLanguage == Language.Korean
                ? "대상 오브젝트"
                : currentLanguage == Language.Japanese ? "対象オブジェクト" : "Target Object";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(targetObjectLabel, EditorStyles.miniBoldLabel, GUILayout.Width(96), GUILayout.Height(22));
            t.objectReferenceValue = EditorGUILayout.ObjectField(
                GUIContent.none, t.objectReferenceValue, typeof(GameObject), true, GUILayout.Height(22));
            Rect targetObjectAnchor = GUILayoutUtility.GetLastRect();
            EditorGUILayout.EndHorizontal();
            if (i == 0)
            {
                DrawTutorialBubble(TutorialStep.DefaultOutfit, targetObjectAnchor);
                DrawTutorialBubble(TutorialStep.DefaultEmpty, targetObjectAnchor);
            }
            var currentTarget = t.objectReferenceValue as GameObject;

            if (previousTarget != currentTarget)
            {
                if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                var liveLayerData = gen.layers[index];
                Undo.RecordObject(gen, "Assign Multi Dresser Item Icon");

                if (i != 0)
                {
                    if (i < liveLayerData.labels.Count &&
                        (string.IsNullOrEmpty(liveLayerData.labels[i]) || liveLayerData.labels[i] == previousTarget?.name))
                    {
                        liveLayerData.labels[i] = currentTarget != null ? currentTarget.name : "";
                    }

                    DiNeMultiIconGenerator.EnsureIcon(liveLayerData, i);
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(gen);
                EditorUtility.SetDirty(gen);
                serializedObject.Update();

                if (previewLayerIndex == index && previewButtonIndex == i)
                    RefreshPreview(gen, gen.layers[index]);
                if (i > 0 && IsTutorialObject(gen, currentTarget)) NotifyTutorialAction(TutorialStep.Outfit);
            }

            if (i != 0)
            {
                if (t.objectReferenceValue == null)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.HelpBox(lang["emptyBtnWarn"], MessageType.Warning);

                    Color previousRemoveColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                    if (GUILayout.Button(lang["emptyBtnRemove"], GUILayout.Height(24)))
                        pendingEmptyButtonRemoval = i;
                    GUI.backgroundColor = previousRemoveColor;
                }

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                Rect iconRect = GUILayoutUtility.GetRect(76, 76, GUILayout.Width(76), GUILayout.Height(76));
                icon.objectReferenceValue = EditorGUI.ObjectField(
                    iconRect, icon.objectReferenceValue, typeof(Texture2D), false);

                GUILayout.Space(8);
                EditorGUILayout.BeginVertical(GUILayout.MinHeight(76));
                GUILayout.Label(lang["menuName"], EditorStyles.miniBoldLabel);
                l.stringValue = EditorGUILayout.TextField(l.stringValue, GUILayout.Height(22));
                Rect menuNameAnchor = GUILayoutUtility.GetLastRect();
                GUILayout.FlexibleSpace();

                string editIconLabel = currentLanguage == Language.Korean
                    ? "아이콘 편집"
                    : currentLanguage == Language.Japanese ? "アイコン編集" : "Edit Icon";
                Rect iconActionsRect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
                const float iconActionGap = 2f;
                float iconActionWidth = (iconActionsRect.width - iconActionGap * 2f) / 3f;
                Rect editIconRect = new Rect(iconActionsRect.x, iconActionsRect.y, iconActionWidth, iconActionsRect.height);
                Rect regenerateIconRect = new Rect(editIconRect.xMax + iconActionGap, iconActionsRect.y, iconActionWidth, iconActionsRect.height);
                Rect renameIconRect = new Rect(regenerateIconRect.xMax + iconActionGap, iconActionsRect.y, iconActionWidth, iconActionsRect.height);
                Color previousIconButtonColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
                if (GUI.Button(editIconRect, editIconLabel))
                {
                    if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                    Texture2D existingIcon = icon.objectReferenceValue as Texture2D;
                    GameObject editTarget = i < currentLayerData.targets.Count ? currentLayerData.targets[i] : null;

                    DiNeScreenSaver.DiNeScreenSaver.OpenIconEditor(
                        editTarget, existingIcon, gen, index, i, null);
                }
                GUI.backgroundColor = previousIconButtonColor;
                using (new EditorGUI.DisabledScope(currentTarget == null))
                {
                    if (GUI.Button(regenerateIconRect, new GUIContent(lang["regenerateIcon"], lang["regenerateIconTip"])))
                        GenerateItemIcon(gen, index, i, false, lang);
                    if (GUI.Button(renameIconRect, new GUIContent(lang["renameIcon"], lang["renameIconTip"])))
                        GenerateItemIcon(gen, index, i, true, lang);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                if (i == FindTutorialOutfit(gen))
                {
                    DrawTutorialBubble(TutorialStep.Menu, menuNameAnchor);
                    DrawTutorialBubble(TutorialStep.Icon, editIconRect);
                    DrawTutorialBubble(TutorialStep.RegenerateIcon, regenerateIconRect);
                }
                GUILayout.Space(7);

                Rect sectionLine = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(sectionLine, new Color(0.24f, 0.26f, 0.27f));
                GUILayout.Space(3);
            }
            else
            {
                EditorGUILayout.HelpBox(lang["defaultWarn"], MessageType.None);
            }

            DrawPerButtonShapeKeyUI(gen, currentLayerData, i, index, lang);
            DrawPerButtonMaterialSwapUI(currentLayerData, i, index, lang);

            if (currentLayerData.linkedObjects.Count > i)
            {
                int capturedI = i;
                EditorGUILayout.LabelField(lang["linkedObj"], EditorStyles.miniBoldLabel);
                for (int j = 0; j < currentLayerData.linkedObjects[i].objects.Count; j++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    currentLayerData.linkedObjects[i].objects[j] = (GameObject)EditorGUILayout.ObjectField(currentLayerData.linkedObjects[i].objects[j], typeof(GameObject), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        if (previewLayerIndex == index && previewButtonIndex == i)
                            RefreshPreview(gen, currentLayerData);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        currentLayerData.linkedObjects[i].objects.RemoveAt(j);
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        if (previewLayerIndex == index && previewButtonIndex == i)
                            RefreshPreview(gen, currentLayerData);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                Rect subDrop = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
                Color subOriginalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
                GUI.Box(subDrop, lang["linkDragHint"], EditorStyles.helpBox);
                GUI.backgroundColor = subOriginalColor;

                HandleDragDrop(subDrop, (objs) => {
                    foreach(var o in objs) currentLayerData.linkedObjects[capturedI].objects.Add(o);
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();
                    if (previewLayerIndex == index && previewButtonIndex == capturedI)
                        RefreshPreview(gen, currentLayerData);
                });
                if (i == FindTutorialOutfit(gen))
                    DrawTutorialBubble(TutorialStep.LinkedObjects, subDrop);
            }

            // 변경 감지 → 미리보기 즉시 갱신
            if (EditorGUI.EndChangeCheck() && previewLayerIndex == index && previewButtonIndex == i)
            {
                if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                RefreshPreview(gen, currentLayerData);
            }

            EditorGUILayout.EndVertical(); // helpBox 끝

            // 아이템 rect 저장
            if (evt.type == EventType.Repaint)
                itemRects.Add(GUILayoutUtility.GetLastRect());

            // 핸들 클릭 → 드래그 시작
            if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition))
            {
                draggedItemIndex = i;
                dragTargetIndex  = i;
                evt.Use();
            }
        }

        // ── 마지막 아이템 뒤 삽입 표시줄 ──
        Rect lastInsertRect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
        if (evt.type == EventType.Repaint && draggedItemIndex >= 0 && dragTargetIndex == targets.arraySize)
            EditorGUI.DrawRect(new Rect(lastInsertRect.x + 28, lastInsertRect.y + 1, lastInsertRect.width - 28, 2),
                               new Color(0.3f, 0.82f, 0.9f));

        // ── 의상 추가 드래그 영역 (하단) ──
        EditorGUILayout.Space(4);
        GUIStyle dragHintStyle = cachedDragHintStyle ?? (cachedDragHintStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize  = 13,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        });
        Rect dropArea = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        Color dropOrigColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
        GUI.Box(dropArea, lang["mainDragHint"], dragHintStyle);
        GUI.backgroundColor = dropOrigColor;
        // An empty category has no default-state card yet.
        if (targets.arraySize == 0)
        {
            DrawTutorialBubble(TutorialStep.DefaultState, dropArea);
            DrawTutorialBubble(TutorialStep.DefaultOutfit, dropArea);
        }

        HandleDragDrop(dropArea, (objs) => {
            Undo.RecordObject(gen, "Add Multi Dresser Items");
            bool addedTutorialOutfit = false;
            foreach (var go in objs) {
                currentLayerData.targets.Add(go);
                currentLayerData.labels.Add(go.name);
                currentLayerData.icons.Add(null);
                currentLayerData.linkedObjects.Add(new DiNeMultiDresser.LinkedGroup());
                currentLayerData.perButtonShapeKeyStates.Add(new DiNeMultiDresser.ShapeKeyMeshList());
                currentLayerData.perButtonMaterialSwaps.Add(new DiNeMultiDresser.MaterialSwapList());
                DiNeMultiIconGenerator.EnsureIcon(currentLayerData, currentLayerData.targets.Count - 1);
                addedTutorialOutfit |= currentLayerData.targets.Count > 1 && IsTutorialObject(gen, go);
            }
            SyncShapeKeyData(gen, currentLayerData);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gen);
            EditorUtility.SetDirty(target);
            serializedObject.Update();
            if (addedTutorialOutfit) NotifyTutorialAction(TutorialStep.Outfit);
        });
        DrawTutorialOutfitActions(gen, currentLayerData);
        DrawTutorialBubble(TutorialStep.Outfit, dropArea);
        DrawTutorialItemFallback(gen, dropArea);

        EditorGUILayout.Space(4);

        // ── 드래그 이벤트 처리 ──
        if (draggedItemIndex >= 0)
        {
            if (evt.type == EventType.MouseDrag)
            {
                float mouseY = evt.mousePosition.y;
                dragTargetIndex = itemRects.Count;
                for (int i = 0; i < itemRects.Count; i++)
                {
                    if (mouseY < itemRects[i].center.y)
                    {
                        dragTargetIndex = i;
                        break;
                    }
                }
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                int from     = draggedItemIndex;
                int to       = dragTargetIndex;
                int actualTo = (to > from) ? to - 1 : to;

                if (actualTo != from && actualTo >= 0 && actualTo < targets.arraySize)
                {
                    if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                    Undo.RecordObject(gen, "Reorder Dresser Items");
                    MoveItem(currentLayerData, from, actualTo);
                    EditorUtility.SetDirty(gen);
                    serializedObject.Update();
                }

                draggedItemIndex = -1;
                dragTargetIndex  = -1;
                Repaint();
                evt.Use();
            }

            EditorGUIUtility.AddCursorRect(
                new Rect(0, 0, EditorGUIUtility.currentViewWidth, Screen.height),
                MouseCursor.Pan);
        }

        // 확장 패키지(Random Dresser 등)의 레이어별 추가 UI
        EditorGUILayout.Space(4);
        DiNeMultiDresser.InvokeDrawLayerExtensionUI(gen, index);

        // 비어있는(None) 버튼 삭제 요청 처리 (레이아웃 그리기가 끝난 뒤)
        if (pendingEmptyButtonRemoval >= 0 && pendingEmptyButtonRemoval < currentLayerData.targets.Count)
        {
            if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
            ClearPreview();
            Undo.RecordObject(gen, "Remove Empty Dresser Button");
            currentLayerData.RemoveAt(pendingEmptyButtonRemoval);
            EditorUtility.SetDirty(gen);
            serializedObject.Update();
            Repaint();
        }
    }

    private void SwapItems(DiNeMultiDresser.DresserLayer layerData, int indexA, int indexB)
    {
        if (indexA < 0 || indexB < 0 || indexA >= layerData.targets.Count || indexB >= layerData.targets.Count) return;

        // Swap targets
        var tempTarget = layerData.targets[indexA];
        layerData.targets[indexA] = layerData.targets[indexB];
        layerData.targets[indexB] = tempTarget;

        // Swap labels
        var tempLabel = layerData.labels[indexA];
        layerData.labels[indexA] = layerData.labels[indexB];
        layerData.labels[indexB] = tempLabel;

        // Swap icons
        var tempIcon = layerData.icons[indexA];
        layerData.icons[indexA] = layerData.icons[indexB];
        layerData.icons[indexB] = tempIcon;

        // Swap linkedObjects
        if (indexA < layerData.linkedObjects.Count && indexB < layerData.linkedObjects.Count)
        {
            var tempLinked = layerData.linkedObjects[indexA];
            layerData.linkedObjects[indexA] = layerData.linkedObjects[indexB];
            layerData.linkedObjects[indexB] = tempLinked;
        }

        // Swap perButtonShapeKeyStates
        if (indexA < layerData.perButtonShapeKeyStates.Count && indexB < layerData.perButtonShapeKeyStates.Count)
        {
            var tempShape = layerData.perButtonShapeKeyStates[indexA];
            layerData.perButtonShapeKeyStates[indexA] = layerData.perButtonShapeKeyStates[indexB];
            layerData.perButtonShapeKeyStates[indexB] = tempShape;
        }
    }

    private void MoveItem(DiNeMultiDresser.DresserLayer layerData, int from, int to)
    {
        MoveInList(layerData.targets,                from, to);
        MoveInList(layerData.labels,                 from, to);
        MoveInList(layerData.icons,                  from, to);
        MoveInList(layerData.linkedObjects,           from, to);
        MoveInList(layerData.perButtonShapeKeyStates, from, to);
        MoveInList(layerData.perButtonMaterialSwaps,  from, to);
    }

    private void MoveInList<T>(List<T> list, int from, int to)
    {
        if (from < 0 || to < 0 || from >= list.Count || to >= list.Count) return;
        T item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
    }

    private void ApplyPreview(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData, int buttonIdx, int layerIdx)
    {
        // 잠긴 Inspector가 여러 개여도 씬에는 하나의 Multi Dresser 미리보기만 유지한다.
        ClearAllActivePreviews();

        previewLayerIndex  = layerIdx;
        previewButtonIndex = buttonIdx;
        previewRestoreActions.Clear();
        previewOriginalObjectStates.Clear();
        previewOriginalShapeWeights.Clear();
        previewBaseMaterials.Clear();

        activePreviewOwnerId = target.GetInstanceID();
        activeSkSessionKey = $"DiNe_SKWas_{activePreviewOwnerId}";
        activeGoSessionKey = $"DiNe_GOWas_{activePreviewOwnerId}";
        activeMatSessionKey = $"DiNe_MatWas_{activePreviewOwnerId}";
        SessionState.EraseString(activeSkSessionKey);
        SessionState.EraseString(activeGoSessionKey);
        SessionState.EraseString(activeMatSessionKey);
        RegisterPreviewSessionOwner(activePreviewOwnerId);
        RegisterActivePreviewEditor(this);

        // ── 메인 타겟 오브젝트 ──
        for (int j = 0; j < layerData.targets.Count; j++)
        {
            var go = layerData.targets[j];
            if (go == null) continue;
            bool next = (j == buttonIdx);
            CapturePreviewObjectState(go);
            SafeSetActive(go, next);
        }

        // ── 링크 오브젝트 (애니메이션 생성 로직과 동일) ──
        var linkedMap = new Dictionary<GameObject, bool>();
        for (int j = 0; j < layerData.linkedObjects.Count; j++)
        {
            if (layerData.linkedObjects[j] == null) continue;
            foreach (var linkObj in layerData.linkedObjects[j].objects)
            {
                if (linkObj == null) continue;
                if (j == buttonIdx)               linkedMap[linkObj] = true;
                else if (!linkedMap.ContainsKey(linkObj)) linkedMap[linkObj] = false;
            }
        }
        foreach (var kvp in linkedMap)
        {
            var go    = kvp.Key;
            bool next = kvp.Value;
            CapturePreviewObjectState(go);
            SafeSetActive(go, next);
        }

        // ── 쉐이프키 ──
        // 레이어 내 어느 버튼에서든 한 번이라도 everRecorded된 키만 "관리 대상"으로 수집
        var managedKeys = new Dictionary<int, HashSet<string>>(); // meshIndex → keyName set
        for (int j = 0; j < layerData.perButtonShapeKeyStates.Count; j++)
        {
            var bs = layerData.perButtonShapeKeyStates[j];
            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                if (m >= bs.meshShapeKeys.Count) continue;
                if (!managedKeys.ContainsKey(m)) managedKeys[m] = new HashSet<string>();
                foreach (var sk in bs.meshShapeKeys[m].shapeKeys)
                    if (sk.everRecorded) managedKeys[m].Add(sk.name);
            }
        }

        var currentBtnState = buttonIdx < layerData.perButtonShapeKeyStates.Count
            ? layerData.perButtonShapeKeyStates[buttonIdx] : null;

        foreach (var kvp in managedKeys)
        {
            int m = kvp.Key;
            var meshObj = gen.shapeKeyTargets[m];
            if (meshObj == null) continue;
            var smr = meshObj.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            foreach (var skName in kvp.Value)
            {
                int skIdx = smr.sharedMesh.GetBlendShapeIndex(skName);
                if (skIdx < 0) continue;

                CapturePreviewShapeKeyState(smr, skIdx);

                // 현재 버튼에서 이 키가 everRecorded면 그 값, 아니면 0
                float targetValue = 0f;
                if (currentBtnState != null && m < currentBtnState.meshShapeKeys.Count)
                {
                    var found = currentBtnState.meshShapeKeys[m].shapeKeys.Find(k => k.name == skName);
                    if (found.name != null && found.everRecorded) targetValue = found.value;
                }
                smr.SetBlendShapeWeight(skIdx, targetValue);
            }
        }

        // ── 머티리얼 교체 ──
        if (buttonIdx < layerData.perButtonMaterialSwaps.Count)
        {
            var swapList = layerData.perButtonMaterialSwaps[buttonIdx];
            foreach (var entry in swapList.entries)
            {
                var rend = entry.renderer;
                if (rend == null) continue;
                var wasMats = CloneMaterials(GetOrCapturePreviewBaseMaterials(layerData, rend));

                // entry.materials 슬롯 수만큼 교체 (나머지 슬롯은 원본 유지)
                var newMats = (Material[])wasMats.Clone();
                for (int si = 0; si < entry.materials.Count && si < newMats.Length; si++)
                {
                    if (entry.materials[si] != null)
                        newMats[si] = entry.materials[si];
                }
                rend.sharedMaterials = newMats;
            }
        }

        ForcePreviewRepaint();
    }

    private void CapturePreviewObjectState(GameObject go)
    {
        if (go == null || previewOriginalObjectStates.ContainsKey(go)) return;

        var capturedObject = go;
        bool originalActive = go.activeSelf;
        previewOriginalObjectStates[go] = originalActive;
        previewRestoreActions.Add(() =>
        {
            if (capturedObject != null) SafeSetActive(capturedObject, originalActive);
        });

        string previous = SessionState.GetString(activeGoSessionKey, "");
        SessionState.SetString(activeGoSessionKey,
            previous + $"{go.GetInstanceID()}:{(originalActive ? 1 : 0)};");
    }

    private void CapturePreviewShapeKeyState(SkinnedMeshRenderer smr, int shapeIndex)
    {
        if (smr == null || shapeIndex < 0) return;

        if (!previewOriginalShapeWeights.TryGetValue(smr, out var rendererWeights))
        {
            rendererWeights = new Dictionary<int, float>();
            previewOriginalShapeWeights[smr] = rendererWeights;
        }
        if (rendererWeights.ContainsKey(shapeIndex)) return;

        var capturedRenderer = smr;
        int capturedIndex = shapeIndex;
        float originalWeight = smr.GetBlendShapeWeight(shapeIndex);
        rendererWeights[shapeIndex] = originalWeight;
        previewRestoreActions.Add(() =>
        {
            if (capturedRenderer != null && capturedRenderer.sharedMesh != null &&
                capturedIndex < capturedRenderer.sharedMesh.blendShapeCount)
            {
                capturedRenderer.SetBlendShapeWeight(capturedIndex, originalWeight);
            }
        });

        string previous = SessionState.GetString(activeSkSessionKey, "");
        SessionState.SetString(activeSkSessionKey,
            previous + $"{smr.GetInstanceID()}:{shapeIndex}:" +
            originalWeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";");
    }

    private Material[] GetOrCapturePreviewBaseMaterials(
        DiNeMultiDresser.DresserLayer layerData,
        Renderer renderer)
    {
        if (previewBaseMaterials.TryGetValue(renderer, out var baseMaterials))
            return baseMaterials;

        var capturedRenderer = renderer;
        var originalMaterials = CloneMaterials(renderer.sharedMaterials);
        baseMaterials = BuildPreviewBaseMaterials(layerData, renderer);

        previewBaseMaterials[renderer] = baseMaterials;
        previewRestoreActions.Add(() =>
        {
            if (capturedRenderer != null)
                capturedRenderer.sharedMaterials = CloneMaterials(originalMaterials);
        });

        AppendMaterialPreviewSnapshot(activeMatSessionKey, renderer, originalMaterials);
        return baseMaterials;
    }

    // VRC SDK가 에디터에서 SetActive 시 뱉는 MissingReferenceException 억제
    internal static void SafeSetActive(GameObject go, bool active)
    {
        if (go == null || go.activeSelf == active) return;

        try { go.SetActive(active); }
        catch (System.Exception) { /* VRC 내부 stale 참조 — 무시 */ }

        // Undo를 거치지 않은 SetActive는 Unity의 ObjectChangeEvents를 발생시키지 않는다.
        // NDMF는 GameObject 관찰에 폴링을 쓰지 않고 이 이벤트에만 의존하므로, 직접
        // 알려주지 않으면 MA Shape Changer / Mesh Cutter가 옷이 꺼진 걸 모른 채 계속
        // 적용된다. Unity가 보냈을 알림을 동일하게 대신 발생시킨다.
        NotifyNdmfObjectChanged(go);
    }

    // NDMF ShadowHierarchy에 "이 오브젝트의 프로퍼티가 바뀌었다"고 알린다.
    // (Unity가 ObjectChangeKind.ChangeGameObjectOrComponentProperties에 대해 하는 것과 동일)
    private static void NotifyNdmfObjectChanged(Object obj)
    {
        if (obj == null) return;

        InitializeNdmfPreviewReflection();
        if (ndmfShadowHierarchy == null || ndmfFireObjectChangeMethod == null) return;

        try
        {
            ndmfFireObjectChangeMethod.Invoke(ndmfShadowHierarchy, new object[] { obj.GetInstanceID() });
        }
        catch (System.Exception)
        {
            // NDMF 내부 API가 바뀐 구/신 버전에서도 미리보기 자체는 계속 동작해야 한다.
        }
    }

    private void ClearPreview()
    {
        bool hadPreview = activePreviewOwnerId != 0 || previewRestoreActions.Count > 0;

        // 같은 오브젝트가 메인/링크 목록 등에 중복돼도 최초 상태까지 되감기도록
        // 적용의 역순으로 복원한다. 한 액션이 실패해도 나머지 복원은 계속한다.
        for (int i = previewRestoreActions.Count - 1; i >= 0; i--)
        {
            try { previewRestoreActions[i]?.Invoke(); }
            catch (System.Exception) { /* stale 참조 등 — 무시하고 계속 복원 */ }
        }
        previewRestoreActions.Clear();
        previewOriginalObjectStates.Clear();
        previewOriginalShapeWeights.Clear();
        previewBaseMaterials.Clear();
        previewLayerIndex  = -1;
        previewButtonIndex = -1;

        if (!string.IsNullOrEmpty(activeMatSessionKey)) SessionState.EraseString(activeMatSessionKey);
        if (!string.IsNullOrEmpty(activeSkSessionKey)) SessionState.EraseString(activeSkSessionKey);
        if (!string.IsNullOrEmpty(activeGoSessionKey)) SessionState.EraseString(activeGoSessionKey);
        if (activePreviewOwnerId != 0) UnregisterPreviewSessionOwner(activePreviewOwnerId);

        activeMatSessionKey = null;
        activeSkSessionKey = null;
        activeGoSessionKey = null;
        activePreviewOwnerId = 0;
        UnregisterActivePreviewEditor(this);

        // SetBlendShapeWeight로 되돌린 값이 화면에 반영되도록 강제 재-bake.
        // (선택 해제로 OnDisable이 호출될 땐 인스펙터가 더 이상 안 그려져
        //  자동 리페인트가 일어나지 않으므로 메쉬가 변형된 채 고정되는 문제 방지)
        if (hadPreview) ForcePreviewRepaint();
    }

    // Multi Dresser 미리보기는 Undo를 거치지 않고 오브젝트 상태를 바꾼다. NDMF의
    // ComputeContext는 일반적으로 Undo에 기록된 변경만 자동 감지하므로, 캐시를 직접
    // 무효화하지 않으면 MA Shape Changer / Mesh Cutter 프록시가 이전 상태에 머문다.
    // 리페인트 전에 캐시와 보류 중인 invalidation을 비워 실제 활성 상태로 다시 계산한다.
    internal static void ForcePreviewRepaint()
    {
        InvalidateNdmfPreviewCaches();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void InvalidateNdmfPreviewCaches()
    {
        InitializeNdmfPreviewReflection();

        try
        {
            invalidateNdmfPropCachesMethod?.Invoke(null, null);
            flushNdmfInvalidatesMethod?.Invoke(null, null);
        }
        catch (System.Exception)
        {
            // Modular Avatar/NDMF의 미리보기 API가 없는 구버전에서도
            // Multi Dresser 자체 미리보기는 계속 동작해야 한다.
        }
    }

    private static void InitializeNdmfPreviewReflection()
    {
        if (ndmfPreviewReflectionInitialized) return;
        ndmfPreviewReflectionInitialized = true;

        const System.Reflection.BindingFlags staticFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;

        var propCacheDebugType = FindLoadedType("nadena.dev.ndmf.preview.PropCacheDebug");
        invalidateNdmfPropCachesMethod = propCacheDebugType?.GetMethod(
            "InvalidateAllCaches",
            staticFlags,
            null,
            System.Type.EmptyTypes,
            null);

        var computeContextType = FindLoadedType("nadena.dev.ndmf.preview.ComputeContext");
        flushNdmfInvalidatesMethod = computeContextType?.GetMethod(
            "FlushInvalidates",
            staticFlags,
            null,
            System.Type.EmptyTypes,
            null);

        const System.Reflection.BindingFlags instanceFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        // ObjectWatcher.Instance.Hierarchy.FireObjectChangeNotification(instanceId)
        var objectWatcherType = FindLoadedType("nadena.dev.ndmf.cs.ObjectWatcher");
        var watcherInstance = objectWatcherType?
            .GetProperty("Instance", staticFlags)?.GetValue(null);
        ndmfShadowHierarchy = watcherInstance != null
            ? objectWatcherType.GetField("Hierarchy", instanceFlags)?.GetValue(watcherInstance)
            : null;

        ndmfFireObjectChangeMethod = ndmfShadowHierarchy?.GetType().GetMethod(
            "FireObjectChangeNotification",
            instanceFlags,
            null,
            new[] { typeof(int) },
            null);
    }

    private static System.Type FindLoadedType(string fullName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }

        return null;
    }

    private static void RegisterPreviewSessionOwner(int ownerId)
    {
        if (ownerId == 0) return;

        string data = SessionState.GetString(PreviewSessionOwnersKey, "");
        foreach (var entry in data.TrimEnd(';').Split(';'))
        {
            if (int.TryParse(entry, out int existingId) && existingId == ownerId)
                return;
        }

        SessionState.SetString(PreviewSessionOwnersKey, data + ownerId + ";");
    }

    private static void UnregisterPreviewSessionOwner(int ownerId)
    {
        if (ownerId == 0) return;

        string data = SessionState.GetString(PreviewSessionOwnersKey, "");
        if (string.IsNullOrEmpty(data)) return;

        var remaining = new System.Text.StringBuilder();
        foreach (var entry in data.TrimEnd(';').Split(';'))
        {
            if (!int.TryParse(entry, out int existingId) || existingId == ownerId) continue;
            remaining.Append(existingId).Append(';');
        }

        if (remaining.Length == 0) SessionState.EraseString(PreviewSessionOwnersKey);
        else SessionState.SetString(PreviewSessionOwnersKey, remaining.ToString());
    }

    private static bool IsPreviewOwnerActive(int ownerId)
    {
        foreach (var editor in ActivePreviewEditors)
        {
            if (!ReferenceEquals(editor, null) && editor.activePreviewOwnerId == ownerId)
                return true;
        }

        return false;
    }

    private static void RestoreAllOrphanedPreviewSessions()
    {
        if (restoringOrphanedPreviewSessions) return;
        restoringOrphanedPreviewSessions = true;

        bool restoredAny = false;
        try
        {
            string data = SessionState.GetString(PreviewSessionOwnersKey, "");
            if (string.IsNullOrEmpty(data)) return;

            var remaining = new System.Text.StringBuilder();
            var ownerEntries = data.TrimEnd(';').Split(';');
            for (int entryIndex = ownerEntries.Length - 1; entryIndex >= 0; entryIndex--)
            {
                var entry = ownerEntries[entryIndex];
                if (!int.TryParse(entry, out int ownerId)) continue;
                if (IsPreviewOwnerActive(ownerId))
                {
                    remaining.Append(ownerId).Append(';');
                    continue;
                }

                // 적용 순서(GameObject -> ShapeKey -> Material)의 반대로 복원한다.
                restoredAny |= RestoreMaterialPreview($"DiNe_MatWas_{ownerId}");
                restoredAny |= RestoreShapeKeyPreview($"DiNe_SKWas_{ownerId}");
                restoredAny |= RestoreObjectPreview($"DiNe_GOWas_{ownerId}");
            }

            if (remaining.Length == 0) SessionState.EraseString(PreviewSessionOwnersKey);
            else SessionState.SetString(PreviewSessionOwnersKey, remaining.ToString());
        }
        finally
        {
            restoringOrphanedPreviewSessions = false;
            if (restoredAny) ForcePreviewRepaint();
        }
    }

    private static void AppendMaterialPreviewSnapshot(string sessionKey, Renderer renderer, Material[] materials)
    {
        if (string.IsNullOrEmpty(sessionKey) || renderer == null) return;

        var entry = new System.Text.StringBuilder();
        entry.Append(renderer.GetInstanceID()).Append(':').Append(materials.Length).Append(':');
        for (int i = 0; i < materials.Length; i++)
        {
            if (i > 0) entry.Append(',');
            entry.Append(materials[i] != null ? materials[i].GetInstanceID() : 0);
        }
        entry.Append(';');

        SessionState.SetString(sessionKey, SessionState.GetString(sessionKey, "") + entry.ToString());
    }

    private static bool RestoreMaterialPreview(string sessionKey)
    {
        string data = SessionState.GetString(sessionKey, "");
        if (string.IsNullOrEmpty(data)) return false;

        var entries = data.TrimEnd(';').Split(';');
        for (int entryIndex = entries.Length - 1; entryIndex >= 0; entryIndex--)
        {
            var parts = entries[entryIndex].Split(':');
            if (parts.Length != 3) continue;
            if (!int.TryParse(parts[0], out int rendererId)) continue;
            if (!int.TryParse(parts[1], out int materialCount) || materialCount < 0) continue;

            var renderer = EditorUtility.InstanceIDToObject(rendererId) as Renderer;
            if (renderer == null) continue;

            var materialIds = string.IsNullOrEmpty(parts[2]) ? new string[0] : parts[2].Split(',');
            var materials = new Material[materialCount];
            for (int i = 0; i < materialCount && i < materialIds.Length; i++)
            {
                if (int.TryParse(materialIds[i], out int materialId) && materialId != 0)
                    materials[i] = EditorUtility.InstanceIDToObject(materialId) as Material;
            }

            try { renderer.sharedMaterials = materials; }
            catch (System.Exception) { /* stale renderer — 나머지 스냅샷은 계속 복원 */ }
        }

        SessionState.EraseString(sessionKey);
        return true;
    }

    private static bool RestoreShapeKeyPreview(string sessionKey)
    {
        string data = SessionState.GetString(sessionKey, "");
        if (string.IsNullOrEmpty(data)) return false;

        var entries = data.TrimEnd(';').Split(';');
        for (int entryIndex = entries.Length - 1; entryIndex >= 0; entryIndex--)
        {
            var parts = entries[entryIndex].Split(':');
            if (parts.Length != 3) continue;
            if (!int.TryParse(parts[0], out int instanceID)) continue;
            if (!int.TryParse(parts[1], out int skIdx)) continue;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float wasVal)) continue;

            var smr = EditorUtility.InstanceIDToObject(instanceID) as SkinnedMeshRenderer;
            if (smr != null && skIdx >= 0 && smr.sharedMesh != null && skIdx < smr.sharedMesh.blendShapeCount)
            {
                try { smr.SetBlendShapeWeight(skIdx, wasVal); }
                catch (System.Exception) { /* stale renderer — 나머지 스냅샷은 계속 복원 */ }
            }
        }

        SessionState.EraseString(sessionKey);
        return true;
    }

    private static bool RestoreObjectPreview(string sessionKey)
    {
        string data = SessionState.GetString(sessionKey, "");
        if (string.IsNullOrEmpty(data)) return false;

        var entries = data.TrimEnd(';').Split(';');
        for (int entryIndex = entries.Length - 1; entryIndex >= 0; entryIndex--)
        {
            var parts = entries[entryIndex].Split(':');
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], out int instanceID)) continue;
            if (!int.TryParse(parts[1], out int wasInt)) continue;

            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go != null) SafeSetActive(go, wasInt == 1);
        }

        SessionState.EraseString(sessionKey);
        return true;
    }

    // 원상태 저장 없이 현재 데이터를 다시 아바타에 적용 (미리보기 중 실시간 갱신용)
    private void RefreshPreview(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData)
    {
        int buttonIdx = previewButtonIndex;

        // 메인 타겟
        for (int j = 0; j < layerData.targets.Count; j++)
        {
            var go = layerData.targets[j];
            if (go == null) continue;
            CapturePreviewObjectState(go);
            SafeSetActive(go, j == buttonIdx);
        }

        // 링크 오브젝트
        var linkedMap = new Dictionary<GameObject, bool>();
        for (int j = 0; j < layerData.linkedObjects.Count; j++)
        {
            if (layerData.linkedObjects[j] == null) continue;
            foreach (var linkObj in layerData.linkedObjects[j].objects)
            {
                if (linkObj == null) continue;
                if (j == buttonIdx)                      linkedMap[linkObj] = true;
                else if (!linkedMap.ContainsKey(linkObj)) linkedMap[linkObj] = false;
            }
        }
        foreach (var kvp in linkedMap)
        {
            if (kvp.Key == null) continue;
            CapturePreviewObjectState(kvp.Key);
            SafeSetActive(kvp.Key, kvp.Value);
        }

        // 쉐이프키 — 관리 대상 키(어느 버튼이든 everRecorded된 것)만 갱신
        var refreshManaged = new Dictionary<int, HashSet<string>>();
        for (int j = 0; j < layerData.perButtonShapeKeyStates.Count; j++)
        {
            var bs = layerData.perButtonShapeKeyStates[j];
            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                if (m >= bs.meshShapeKeys.Count) continue;
                if (!refreshManaged.ContainsKey(m)) refreshManaged[m] = new HashSet<string>();
                foreach (var sk in bs.meshShapeKeys[m].shapeKeys)
                    if (sk.everRecorded) refreshManaged[m].Add(sk.name);
            }
        }

        var refreshBtnState = buttonIdx < layerData.perButtonShapeKeyStates.Count
            ? layerData.perButtonShapeKeyStates[buttonIdx] : null;

        foreach (var kvp in refreshManaged)
        {
            int m = kvp.Key;
            var meshObj = gen.shapeKeyTargets[m];
            if (meshObj == null) continue;
            var smr = meshObj.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            foreach (var skName in kvp.Value)
            {
                int skIdx = smr.sharedMesh.GetBlendShapeIndex(skName);
                if (skIdx < 0) continue;

                CapturePreviewShapeKeyState(smr, skIdx);

                float targetValue = 0f;
                if (refreshBtnState != null && m < refreshBtnState.meshShapeKeys.Count)
                {
                    var found = refreshBtnState.meshShapeKeys[m].shapeKeys.Find(k => k.name == skName);
                    if (found.name != null && found.everRecorded) targetValue = found.value;
                }
                smr.SetBlendShapeWeight(skIdx, targetValue);
            }
        }

        // 머티리얼 교체 갱신
        if (buttonIdx < layerData.perButtonMaterialSwaps.Count)
        {
            var swapList = layerData.perButtonMaterialSwaps[buttonIdx];
            foreach (var entry in swapList.entries)
            {
                var rend = entry.renderer;
                if (rend == null) continue;
                var baseMats = GetOrCapturePreviewBaseMaterials(layerData, rend);
                var newMats = CloneMaterials(baseMats);
                for (int si = 0; si < entry.materials.Count && si < newMats.Length; si++)
                {
                    if (entry.materials[si] != null)
                        newMats[si] = entry.materials[si];
                }
                rend.sharedMaterials = newMats;
            }
        }

        ForcePreviewRepaint();
    }

    private bool DrawGlobalShapeKeyTargets(SerializedProperty shapeKeyTargets, Dictionary<string, string> lang)
    {
        bool changed = false;

        for (int i = 0; i < shapeKeyTargets.arraySize; i++)
        {
            EditorGUILayout.BeginHorizontal();
            SerializedProperty prop = shapeKeyTargets.GetArrayElementAtIndex(i);

            EditorGUI.BeginChangeCheck();
            prop.objectReferenceValue = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            if (GUILayout.Button("-", GUILayout.Width(25)))
            {
                shapeKeyTargets.DeleteArrayElementAtIndex(i);
                changed = true;
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        Rect dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        tutorialShapeTargetAnchor = dropArea;
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f); 
        GUI.Box(dropArea, lang["skDragHint"], EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;

        HandleDragDrop(dropArea, (objs) => {
            foreach(var o in objs) {
                 bool exists = false;
                 for(int k=0; k<shapeKeyTargets.arraySize; k++) 
                     if(shapeKeyTargets.GetArrayElementAtIndex(k).objectReferenceValue == o) exists = true;
                 if(!exists) {
                    int idx = shapeKeyTargets.arraySize;
                    shapeKeyTargets.InsertArrayElementAtIndex(idx);
                    shapeKeyTargets.GetArrayElementAtIndex(idx).objectReferenceValue = o;
                    changed = true;
                 }
            }
        });

        return changed;
    }

    private string Localized(string en, string ko, string ja)
        => currentLanguage == Language.Korean ? ko : currentLanguage == Language.Japanese ? ja : en;

    private void InvalidateEditorCaches()
    {
        toggleMenuChoices?.Invalidate();
        shapeSyncCache.Clear();
        Repaint();
    }

    private void DrawSimpleToggleUI(DiNeMultiDresser gen)
    {
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(Localized("Independent On/Off Toggles", "독립 On/Off 토글", "独立On/Offトグル"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Localized("Switch all target objects together with one button.",
                "대상 목록의 오브젝트들을 버튼 하나로 함께 켜고 끕니다.",
                "対象リストのオブジェクトを1つのボタンで同時に切り替えます。"), MessageType.None);
            var groups = serializedObject.FindProperty("independentToggles");
            for (int i = 0; i < groups.arraySize; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string title = group.FindPropertyRelative("displayName").stringValue;
                        GUILayout.Label(new GUIContent(title, title), EditorStyles.boldLabel, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                        bool previewing = DiNeTogglePreview.IsActive(this, i);
                        Color previewColor = GUI.backgroundColor;
                        if (previewing) GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
                        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button(new GUIContent(previewing ? Localized("Stop", "종료", "終了")
                            : Localized("Preview", "미리보기", "プレビュー"),
                            previewing ? Localized("End preview and restore the original states", "미리보기를 종료하고 원래 상태로 복원", "プレビューを終了して元の状態に戻す")
                                : Localized("Preview all target objects together", "대상 오브젝트들을 함께 미리보기", "対象オブジェクトをまとめてプレビュー")),
                            GUILayout.Width(76), GUILayout.Height(24)))
                        {
                            if (previewing) DiNeTogglePreview.Clear();
                            else
                            {
                                if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                                var toggle = gen.independentToggles[i];
                                var valid = new List<GameObject>();
                                foreach (var go in toggle.targets)
                                    if (DiNeIndependentToggleEditing.CanAdd(gen, toggle, go)) valid.Add(go);
                                if (!DiNeTogglePreview.Begin(this, i, valid, toggle.defaultOn))
                                    toggleTargetStatus = Localized("Add a valid avatar object before previewing.",
                                        "미리보기할 아바타 내부 오브젝트를 먼저 추가하세요.", "プレビューするアバター内の対象を先に追加してください。");
                            }
                            if (i == 0) independentTutorial.NotifyAction(previewing ? "restore" : "preview");
                        }
                        if (i == 0) { independentTutorial.Anchor("preview", GUILayoutUtility.GetLastRect()); independentTutorial.Anchor("restore", GUILayoutUtility.GetLastRect()); independentTutorial.Anchor("state", GUILayoutUtility.GetLastRect()); }
                        GUI.backgroundColor = previewColor;
                        if (GUILayout.Button(new GUIContent("×", Localized("Remove Toggle", "토글 삭제", "トグル削除")), GUILayout.Width(24), GUILayout.Height(24)))
                        {
                            DiNeTogglePreview.ClearForOwner(this);
                            groups.DeleteArrayElementAtIndex(i);
                            break;
                        }
                        if (i == 0) independentTutorial.Anchor("delete", GUILayoutUtility.GetLastRect());
                    }
                    if (i == 0) { independentTutorial.Draw("preview"); independentTutorial.Draw("restore"); independentTutorial.Draw("delete"); }
                    DiNeTogglePreview.DrawStateControls(this, i);
                    if (i == 0)
                    {
                        if (DiNeTogglePreview.IsActive(this, i)) independentTutorial.Anchor("state", GUILayoutUtility.GetLastRect());
                        independentTutorial.Draw("state");
                    }
                    DrawToggleSettings(gen, group, i);
                    toggleMenuChoices.Draw(gen.GetAvatarDescriptor(), group.FindPropertyRelative("menuPath"), group.FindPropertyRelative("generatedMenuDestination"));
                    if (i == 0) independentTutorial.Draw("menu", GUILayoutUtility.GetLastRect());
                    GUILayout.Space(5);
                    var targets = group.FindPropertyRelative("targets");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(Localized("Objects switched together", "함께 켜고 끌 오브젝트", "同時に切り替えるオブジェクト"), EditorStyles.boldLabel);
                        GUILayout.Label(targets.arraySize.ToString(), EditorStyles.miniLabel, GUILayout.Width(28));
                    }
                    for (int j = 0; j < targets.arraySize; j++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var item = targets.GetArrayElementAtIndex(j);
                            EditorGUI.BeginChangeCheck();
                            var candidate = (GameObject)EditorGUILayout.ObjectField(item.objectReferenceValue, typeof(GameObject), true);
                            if (EditorGUI.EndChangeCheck())
                            {
                                DiNeTogglePreview.ClearForOwner(this);
                                bool duplicate = false;
                                for (int k = 0; k < targets.arraySize; k++)
                                    if (k != j && candidate != null && targets.GetArrayElementAtIndex(k).objectReferenceValue == candidate) duplicate = true;
                                if (candidate == null || (!duplicate && DiNeIndependentToggleEditing.CanAdd(gen, gen.independentToggles[i], candidate)))
                                    item.objectReferenceValue = candidate;
                                else SetToggleTargetStatus(0);
                                if (i == 0) { serializedObject.ApplyModifiedProperties(); independentTutorial.NotifyAction("targets"); }
                            }
                            if (GUILayout.Button(new GUIContent("×", Localized("Remove Object", "대상 삭제", "対象を削除")), GUILayout.Width(24), GUILayout.Height(24)))
                            {
                                DiNeTogglePreview.ClearForOwner(this);
                                item.objectReferenceValue = null;
                                targets.DeleteArrayElementAtIndex(j);
                                break;
                            }
                        }
                    }
                    GUIStyle dropStyle = cachedToggleDropStyle ?? (cachedToggleDropStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        padding = new RectOffset(10, 10, 8, 8),
                        normal = { textColor = Color.white }
                    });
                    var dropLabel = new GUIContent(Localized("Drag objects here to add", "오브젝트를 여기에 드래그해 추가", "ここにオブジェクトをドラッグして追加"));
                    Rect drop = GUILayoutUtility.GetRect(dropLabel, dropStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(48));
                    Color dropColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
                    GUI.Box(drop, dropLabel, dropStyle);
                    GUI.backgroundColor = dropColor;
                    int groupIndex = i;
                    HandleDragDrop(drop, objects => AddGroupedToggleTargets(gen, groupIndex, objects));
                    if (i == 0) independentTutorial.Draw("targets", drop);
                }
            }
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            if (GUILayout.Button(Localized("Add Independent Toggle", "독립 토글 추가", "独立トグルを追加"), GUILayout.Height(30)))
            {
                if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                DiNeIndependentToggleEditing.Create(gen);
                serializedObject.Update();
                toggleMenuChoices.Invalidate();
                independentTutorial.NotifyAction("create");
            }
            independentTutorial.Draw("create", GUILayoutUtility.GetLastRect());
            GUI.backgroundColor = previous;
            if (!string.IsNullOrEmpty(toggleTargetStatus)) EditorGUILayout.HelpBox(toggleTargetStatus, MessageType.Info);
        }
    }

    private void DrawToggleSettings(DiNeMultiDresser gen, SerializedProperty group, int index)
    {
        // Same 76px thumbnail as wardrobe items, sharing its height with three settings rows.
        Rect area = GUILayoutUtility.GetRect(0, 76, GUILayout.ExpandWidth(true));
        Rect iconRect = new Rect(area.x, area.y, 76, 76);
        var icon = group.FindPropertyRelative("icon");
        EditorGUI.BeginProperty(iconRect, new GUIContent(Localized("Icon", "아이콘", "アイコン")), icon);
        EditorGUI.BeginChangeCheck();
        var nextIcon = EditorGUI.ObjectField(iconRect, icon.objectReferenceValue, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck()) icon.objectReferenceValue = nextIcon;
        EditorGUI.EndProperty();
        if (index == 0) independentTutorial.Anchor("icon", iconRect);

        Rect row = new Rect(iconRect.xMax + 8, area.y, Mathf.Max(0, area.width - 84), 22);
        float labelWidth = EditorGUIUtility.labelWidth;
        float fieldWidth = EditorGUIUtility.fieldWidth;
        try
        {
            EditorGUIUtility.labelWidth = 70;
            EditorGUIUtility.fieldWidth = 0;
            EditorGUI.PropertyField(row, group.FindPropertyRelative("displayName"), new GUIContent(Localized("Menu Name", "메뉴 이름", "メニュー名")));
            if (index == 0) independentTutorial.Anchor("name", row);
            row.y += 27;
            var parameter = group.FindPropertyRelative("parameterName");
            var parameterLabel = new GUIContent("Bool", Localized("Bool Parameter — duplicate names receive a numeric suffix.",
                "Bool 파라미터 — 중복된 이름은 숫자를 붙여 변경합니다.", "Boolパラメーター — 重複する名前には番号を付けます。"));
            EditorGUI.BeginProperty(row, parameterLabel, parameter);
            EditorGUI.BeginChangeCheck();
            string requested = EditorGUI.DelayedTextField(row, parameterLabel, parameter.stringValue);
            if (EditorGUI.EndChangeCheck())
                parameter.stringValue = DiNeSmartToggleEditor.MakeUniqueParameterName(null, requested, null, gen, gen.independentToggles[index]);
            EditorGUI.EndProperty();
            if (index == 0) independentTutorial.Anchor("parameter", row);
            row.y += 27;
            float half = (row.width - 4) / 2;
            DrawToggleCheckbox(new Rect(row.x, row.y, half, row.height), group.FindPropertyRelative("defaultOn"),
                new GUIContent(Localized("Default ON", "기본 ON", "初期ON"), Localized("Initial state on upload; preview does not change this value.",
                    "업로드 시 기본 상태입니다. 미리보기는 이 값을 바꾸지 않습니다.", "アップロード時の初期状態です。プレビューはこの値を変更しません。")));
            DrawToggleCheckbox(new Rect(row.x + half + 4, row.y, half, row.height), group.FindPropertyRelative("saved"),
                new GUIContent(Localized("Save", "값 저장", "値を保存"), Localized("Remember the toggle value between sessions", "다음 접속에도 토글 값 저장", "次回の接続でもトグルの値を保存")));
            if (index == 0) independentTutorial.Anchor("defaults", row);
        }
        finally
        {
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUIUtility.fieldWidth = fieldWidth;
        }
        if (index == 0)
        {
            independentTutorial.Draw("icon"); independentTutorial.Draw("name");
            independentTutorial.Draw("parameter"); independentTutorial.Draw("defaults");
        }
    }

    private static void DrawToggleCheckbox(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        EditorGUI.BeginChangeCheck();
        bool value = EditorGUI.ToggleLeft(rect, label, property.boolValue);
        if (EditorGUI.EndChangeCheck()) property.boolValue = value;
        EditorGUI.EndProperty();
    }

    private void AddGroupedToggleTargets(DiNeMultiDresser gen, int index, IEnumerable<GameObject> objects)
    {
        DiNeTogglePreview.ClearForOwner(this);
        if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
        SetToggleTargetStatus(DiNeIndependentToggleEditing.AddTargets(gen, gen.independentToggles[index], objects));
        serializedObject.Update();
        if (index == 0) independentTutorial?.NotifyAction("targets");
        Repaint();
    }

    private void SetToggleTargetStatus(int added)
    {
        toggleTargetStatus = string.Format(Localized("Added {0} objects. Duplicates, objects outside the avatar and objects controlled by another toggle or outfit are skipped.",
            "오브젝트 {0}개를 추가했습니다. 중복·아바타 밖의 대상·다른 토글이나 옷장이 제어하는 대상은 건너뜁니다.",
            "{0}個の対象を追加しました。重複・アバター外・他のトグルや衣装で制御される対象はスキップします。"), added);
    }

    private void GenerateItemIcon(DiNeMultiDresser gen, int layerIndex, int buttonIndex,
        bool newName, Dictionary<string, string> lang)
    {
        if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
        var layer = gen.layers[layerIndex];
        string path = null;
        if (newName)
        {
            string suggested = DiNeMultiIconGenerator.GetIconAssetPath(layer.targets[buttonIndex].name);
            path = EditorUtility.SaveFilePanelInProject(lang["renameIcon"],
                System.IO.Path.GetFileNameWithoutExtension(suggested), "png", lang["renameIconTip"],
                System.IO.Path.GetDirectoryName(suggested));
            if (string.IsNullOrEmpty(path)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
        }

        Undo.RecordObject(gen, "Generate Multi Dresser Icon");
        DiNeMultiIconGenerator.RegenerateIcon(layer, buttonIndex, path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(gen);
        EditorUtility.SetDirty(gen);
        serializedObject.Update();
        GUIUtility.ExitGUI();
    }

    private void SyncShapeKeyData(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData)
    {
        if (shapeSyncCache.TryGetValue(layerData, out var cache) && cache.buttonCount == layerData.targets.Count &&
            cache.targets.Length == gen.shapeKeyTargets.Count && layerData.perButtonShapeKeyStates.Count == layerData.targets.Count)
        {
            bool unchanged = true;
            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                var meshTarget = gen.shapeKeyTargets[m];
                var renderer = meshTarget != null ? meshTarget.GetComponent<SkinnedMeshRenderer>() : null;
                var mesh = renderer != null ? renderer.sharedMesh : null;
                if (cache.targets[m] != meshTarget || cache.meshes[m] != mesh || cache.shapeCounts[m] != (mesh != null ? mesh.blendShapeCount : 0))
                    unchanged = false;
            }
            foreach (var button in layerData.perButtonShapeKeyStates)
                if (button.meshShapeKeys.Count != gen.shapeKeyTargets.Count) unchanged = false;
            if (unchanged) return;
        }

        while (layerData.perButtonShapeKeyStates.Count < layerData.targets.Count)
            layerData.perButtonShapeKeyStates.Add(new DiNeMultiDresser.ShapeKeyMeshList());

        foreach (var buttonState in layerData.perButtonShapeKeyStates)
        {
            while (buttonState.meshShapeKeys.Count < gen.shapeKeyTargets.Count)
                buttonState.meshShapeKeys.Add(new DiNeMultiDresser.ShapeKeyList());

            while (buttonState.meshShapeKeys.Count > gen.shapeKeyTargets.Count)
                buttonState.meshShapeKeys.RemoveAt(buttonState.meshShapeKeys.Count - 1);
        }

        cache = new ShapeSyncCache { buttonCount = layerData.targets.Count,
            targets = gen.shapeKeyTargets.ToArray(), meshes = new Mesh[gen.shapeKeyTargets.Count],
            shapeCounts = new int[gen.shapeKeyTargets.Count] };
        // Reconcile only when target/mesh data changes; idle repaints use the cache.
        for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
        {
            shapeKeyNames.Clear();
            shapeKeyNameSet.Clear();
            var meshObj = gen.shapeKeyTargets[m];
            var skinned = meshObj != null ? meshObj.GetComponent<SkinnedMeshRenderer>() : null;
            var mesh = skinned != null ? skinned.sharedMesh : null;
            cache.meshes[m] = mesh;
            cache.shapeCounts[m] = mesh != null ? mesh.blendShapeCount : 0;

            if (mesh != null)
            {
                for (int s = 0; s < mesh.blendShapeCount; s++)
                {
                    string name = mesh.GetBlendShapeName(s);
                    shapeKeyNames.Add(name);
                    shapeKeyNameSet.Add(name);
                }
            }

            foreach (var buttonState in layerData.perButtonShapeKeyStates)
            {
                var savedList = buttonState.meshShapeKeys[m].shapeKeys;
                savedShapeKeyNames.Clear();
                int retained = 0;
                for (int s = 0; s < savedList.Count; s++)
                {
                    var key = savedList[s];
                    if (!shapeKeyNameSet.Contains(key.name)) continue;
                    if (retained != s) savedList[retained] = key;
                    retained++;
                    savedShapeKeyNames.Add(key.name);
                }
                if (retained < savedList.Count)
                    savedList.RemoveRange(retained, savedList.Count - retained);

                foreach (string name in shapeKeyNames)
                {
                    if (savedShapeKeyNames.Add(name))
                        savedList.Add(new DiNeMultiDresser.ShapeKeyState { name = name, value = 0 });
                }
            }
        }
        shapeSyncCache[layerData] = cache;
    }

    private void DrawPerButtonMaterialSwapUI(DiNeMultiDresser.DresserLayer layerData, int buttonIdx, int layerIdx, Dictionary<string, string> lang)
    {
        bool tutorialItem = buttonIdx == FindTutorialOutfit(target as DiNeMultiDresser);
        while (layerData.perButtonMaterialSwaps.Count <= buttonIdx)
            layerData.perButtonMaterialSwaps.Add(new DiNeMultiDresser.MaterialSwapList());

        var swapList = layerData.perButtonMaterialSwaps[buttonIdx];

        string foldoutKey = $"DiNe_MS_{layerIdx}_{buttonIdx}";
        bool foldout = EditorPrefs.GetBool(foldoutKey, false);

        EditorGUILayout.BeginHorizontal();
        foldout = EditorGUILayout.Foldout(foldout, lang["matSwap"], true);
        Rect materialFoldoutAnchor = GUILayoutUtility.GetLastRect();
        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.7f, 0.9f, 0.7f);
        if (GUILayout.Button(lang["addMatSwap"], GUILayout.Width(60), GUILayout.Height(20)))
        {
            swapList.entries.Add(new DiNeMultiDresser.MaterialSwapEntry());
            foldout = true;
            EditorUtility.SetDirty(target);
        }
        Rect addMaterialAnchor = GUILayoutUtility.GetLastRect();
        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
        if (tutorialItem) DrawTutorialBubble(TutorialStep.Materials, addMaterialAnchor);

        if (EditorPrefs.GetBool(foldoutKey, false) != foldout) EditorPrefs.SetBool(foldoutKey, foldout);

        if (!foldout || swapList.entries.Count == 0)
        {
            if (tutorialItem)
            {
                Rect setupAnchor = swapList.entries.Count > 0 ? materialFoldoutAnchor : addMaterialAnchor;
                DrawTutorialBubble(TutorialStep.MaterialRenderer, setupAnchor);
                DrawTutorialBubble(TutorialStep.MaterialSlots, setupAnchor);
            }
            return;
        }

        EditorGUI.indentLevel++;
        for (int e = 0; e < swapList.entries.Count; e++)
        {
            var entry = swapList.entries[e];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Renderer 행
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            entry.renderer = (Renderer)EditorGUILayout.ObjectField(entry.renderer, typeof(Renderer), true);
            Rect rendererAnchor = GUILayoutUtility.GetLastRect();
            if (EditorGUI.EndChangeCheck() && entry.renderer != null)
            {
                entry.materials.Clear();
                foreach (var m in entry.renderer.sharedMaterials) entry.materials.Add(m);
                EditorUtility.SetDirty(target);
            }
            if (GUILayout.Button("−", GUILayout.Width(22)))
            {
                swapList.entries.RemoveAt(e);
                EditorUtility.SetDirty(target);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();
            if (tutorialItem && e == 0)
            {
                DrawTutorialBubble(TutorialStep.MaterialRenderer, rendererAnchor);
                if (entry.materials.Count == 0) DrawTutorialBubble(TutorialStep.MaterialSlots, rendererAnchor);
            }

            // 마테리얼 슬롯
            for (int mi = 0; mi < entry.materials.Count; mi++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"[{mi}]", GUILayout.Width(24));
                EditorGUI.BeginChangeCheck();
                entry.materials[mi] = (Material)EditorGUILayout.ObjectField(entry.materials[mi], typeof(Material), false);
                Rect materialSlotAnchor = GUILayoutUtility.GetLastRect();
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(target);
                EditorGUILayout.EndHorizontal();
                if (tutorialItem && e == 0 && mi == 0) DrawTutorialBubble(TutorialStep.MaterialSlots, materialSlotAnchor);
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private void DrawPerButtonShapeKeyUI(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData, int buttonIdx, int layerIdx, Dictionary<string, string> lang)
    {
        bool tutorialItem = buttonIdx == FindTutorialOutfit(gen);
        if (gen.shapeKeyTargets.Count == 0)
        {
            if (tutorialItem)
            {
                DrawTutorialBubble(TutorialStep.ShapeKeySettings, tutorialShapeTargetAnchor);
                DrawTutorialBubble(TutorialStep.ShapeKeyValues, tutorialShapeTargetAnchor);
            }
            return;
        }

        string foldoutKey = $"DiNe_SK_{layerIdx}_{buttonIdx}";
        bool foldout = EditorPrefs.GetBool(foldoutKey, false);
        foldout = EditorGUILayout.Foldout(foldout, lang["skSettings"]);
        Rect shapeSettingsAnchor = GUILayoutUtility.GetLastRect();
        if (tutorialItem) DrawTutorialBubble(TutorialStep.ShapeKeySettings, shapeSettingsAnchor);
        if (EditorPrefs.GetBool(foldoutKey, false) != foldout) EditorPrefs.SetBool(foldoutKey, foldout);

        if (foldout)
        {
            if (buttonIdx >= layerData.perButtonShapeKeyStates.Count) return;
            var buttonState = layerData.perButtonShapeKeyStates[buttonIdx];

            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                var mesh = gen.shapeKeyTargets[m];
                if (mesh == null) continue;
                if (m >= buttonState.meshShapeKeys.Count) continue;

                EditorGUILayout.LabelField($"[{mesh.name}]", EditorStyles.miniLabel);
                var keys = buttonState.meshShapeKeys[m].shapeKeys;

                for (int k = 0; k < keys.Count; k++)
                {
                    var key = keys[k];
                    float newVal = EditorGUILayout.Slider(key.name, key.value, 0, 100);
                    Rect shapeValueAnchor = GUILayoutUtility.GetLastRect();
                    if (newVal != key.value)
                    {
                        Undo.RecordObject(target, "Change Shape Key");
                        key.value = newVal;
                        key.everRecorded = true;
                        keys[k] = key;
                        EditorUtility.SetDirty(target);
                    }
                    if (tutorialItem) DrawTutorialBubble(TutorialStep.ShapeKeyValues, shapeValueAnchor);
                }
            }
        }
        if (tutorialItem) DrawTutorialBubble(TutorialStep.ShapeKeyValues, shapeSettingsAnchor);
    }

    private void HandleDragDrop(Rect dropArea, System.Action<List<GameObject>> onDrop)
    {
        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                List<GameObject> dropped = new List<GameObject>();
                foreach (var obj in DragAndDrop.objectReferences)
                    if (obj is GameObject go) dropped.Add(go);
                onDrop?.Invoke(dropped);
                evt.Use();
            }
        }
    }

    private void DrawHeader(string titleText)
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle titleStyle = cachedTitleStyle ?? (cachedTitleStyle = new GUIStyle(EditorStyles.label) { font = titleFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 36 });
        float iconSize = 72f;
        GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label(titleText, titleStyle, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = "";
        switch (currentLanguage)
        {
            case Language.Korean: desc = "여러 개의 의상과 액세서리를 손쉽게 켜고 끌 수 있는 FX 토글을 생성합니다."; break;
            case Language.Japanese: desc = "複数の衣装やアクセサリーを簡単に切り替えるFXトグルを生成します。"; break;
            default: desc = "Generates FX toggles to easily turn multiple clothing and accessories on/off."; break;
        }
        if (cachedDescriptionStyle == null)
            cachedDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
        GUILayout.Label(desc, cachedDescriptionStyle);

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
    }

    private void DrawLayerIconPresetButton(SerializedProperty layerIcon, string label, Texture2D presetIcon)
    {
        var previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        GUI.enabled = presetIcon != null;
        if (GUILayout.Button(label, GUILayout.Width(68), GUILayout.Height(18)))
        {
            layerIcon.objectReferenceValue = presetIcon;
        }
        GUI.enabled = true;
        GUI.backgroundColor = previousColor;
    }

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            if (cachedSelectedTabStyle == null)
                cachedSelectedTabStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold,
                    fontSize = 12, normal = { textColor = Color.white } };
            if (cachedNormalTabStyle == null)
                cachedNormalTabStyle = new GUIStyle(GUI.skin.button) { fontSize = 12,
                    normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            GUIStyle style = i == selected ? cachedSelectedTabStyle : cachedNormalTabStyle;
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
            {
                newSelected = i;
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }

}
#endif
