using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;

namespace DiNeScreenSaver
{
    public class DiNeScreenSaver : EditorWindow
    {
        // ══════════════════════════════════════════════════════════════════════
        //  Enums
        // ══════════════════════════════════════════════════════════════════════
        private enum Lang    { English, Korean, Japanese }
        private enum ToolMode { Screenshot, Icon }

        private enum CaptureTarget { GameView, SceneView }
        private enum ResPreset
        {
            [InspectorName("FHD")]    FHD,
            [InspectorName("QHD")]    QHD,
            [InspectorName("UHD")]    UHD,
            [InspectorName("Custom")] Custom
        }
        private enum Aspect
        {
            [InspectorName("16:9")] _16_9,
            [InspectorName("9:16")] _9_16,
            [InspectorName("3:4")]  _3_4,
            [InspectorName("1:1")]  _1_1
        }
        private enum BGType { Skybox, Color, Transparent }

        // ══════════════════════════════════════════════════════════════════════
        //  Output paths
        // ══════════════════════════════════════════════════════════════════════
        private const string SCREENSHOT_ASSET_PATH = "Assets/Di Ne/ScreenShot/";
        private const string ICON_ASSET_PATH       = DiNeIconMaker.DefaultIconAssetFolder + "/";

        // ══════════════════════════════════════════════════════════════════════
        //  UI text
        // ══════════════════════════════════════════════════════════════════════
        private static readonly string[][] UI =
        {
            /* 00 */ new[] { "Target Camera",         "대상 카메라",      "対象カメラ"          },
            /* 01 */ new[] { "Resolution Settings",   "해상도 설정",      "解像度設定"           },
            /* 02 */ new[] { "Preset",                "프리셋",           "プリセット"           },
            /* 03 */ new[] { "Aspect Ratio",          "비율",             "アスペクト比"         },
            /* 04 */ new[] { "Current Size",          "현재 크기",        "現在のサイズ"         },
            /* 05 */ new[] { "Custom Size",           "커스텀 크기",      "カスタムサイズ"       },
            /* 06 */ new[] { "Background Settings",   "배경 설정",        "背景設定"             },
            /* 07 */ new[] { "Type",                  "유형",             "タイプ"               },
            /* 08 */ new[] { "Color",                 "색상",             "色"                   },
            /* 09 */ new[] { "Game View",             "게임 뷰",          "ゲームビュー"         },
            /* 10 */ new[] { "Scene View",            "씬 뷰",            "シーンビュー"         },
            /* 11 */ new[] { "Capture",               "캡처",             "キャプチャ"           },
            /* 12 */ new[] { "Open Folder",           "저장 폴더 열기",   "保存フォルダを開く"   },
            /* 13 */ new[] { "Show in Project",       "프로젝트 위치",    "プロジェクト位置"     },
            /* 14 */ new[] { "Screenshot",            "스크린샷",         "スクリーンショット"   },
            /* 15 */ new[] { "Icon",                  "아이콘",           "アイコン"             },
            /* 16 */ new[] { "Target Object",         "대상 오브젝트",    "対象オブジェクト"     },
            /* 17 */ new[] { "Output Size",           "출력 크기",        "出力サイズ"           },
            /* 18 */ new[] { "Generate Icon",         "아이콘 생성",      "アイコン生成"         },
            /* 19 */ new[] { "Left-drag: Rotate   Right-drag: Pan   Scroll: Zoom",
                             "좌클릭 드래그: 회전   우클릭 드래그: 이동   스크롤: 줌",
                             "左ドラッグ: 回転   右ドラッグ: 移動   スクロール: ズーム"    },
            /* 20 */ new[] { "Icon saved.",           "아이콘 저장 완료!","アイコンを保存しました。" },
            /* 21 */ new[] { "Assign a Target Object.", "대상 오브젝트를 지정하세요.", "対象オブジェクトを指定してください。" },
            /* 22 */ new[] { "Front",  "앞",     "前"  },
            /* 23 */ new[] { "Back",   "뒤",     "後"  },
            /* 24 */ new[] { "Left",   "왼쪽",   "左"  },
            /* 25 */ new[] { "Right",  "오른쪽", "右"  },
            /* 26 */ new[] { "Top",    "위",     "上"  },
            /* 27 */ new[] { "Bottom", "아래",   "下"  },
            /* 28 */ new[] { "Reset View", "뷰 초기화", "ビュー初期化" },
            /* 29 */ new[] { "Create Copy", "복사본 생성", "コピーを生成" },
            /* 30 */ new[] { "Visibility Effects", "가시성 효과", "視認性エフェクト" },
            /* 31 */ new[] { "Outline", "외곽선", "アウトライン" },
            /* 32 */ new[] { "Outline Color", "외곽선 색상", "アウトライン色" },
            /* 33 */ new[] { "Thickness", "두께", "太さ" },
            /* 34 */ new[] { "Forbidden Overlay", "금지 아이콘 오버레이", "禁止アイコンオーバーレイ" },
            /* 35 */ new[] { "Overlay Size", "금지 아이콘 크기", "禁止アイコンサイズ" },
            /* 36 */ new[] { "Opacity", "불투명도", "不透明度" },
            /* 37 */ new[] { "The existing icon will be overwritten.", "기존 아이콘 파일을 덮어씁니다.", "既存のアイコンを上書きします。" },
            /* 38 */ new[] { "Overwrite Icon", "기존 아이콘 덮어쓰기", "既存アイコンを上書き" },
            /* 39 */ new[] { "Send Behind Object", "오브젝트 뒤로 보내기", "オブジェクトの背面へ" },
            /* 40 */ new[] { "Bring In Front", "오브젝트 앞으로 가져오기", "オブジェクトの前面へ" },
            /* 41 */ new[] { "Position", "배치", "配置" },
            /* 42 */ new[] { "Front", "앞", "前" },
            /* 43 */ new[] { "Behind", "뒤", "後" },
            /* 44 */ new[] { "Size", "크기", "サイズ" },
            /* 45 */ new[] { "Idle Pose", "자연스러운 포즈", "自然なポーズ" },
            /* 46 */ new[] { "Applies the VRChat proxy_idle pose instead of the stiff T-pose.",
                             "뻣뻣한 T 포즈 대신 VRChat proxy_idle 포즈를 적용합니다.",
                             "硬いTポーズの代わりにVRChatのproxy_idleポーズを適用します。" },
            /* 47 */ new[] { "proxy_idle clip not found. The VRChat Avatars SDK sample assets are required.",
                             "proxy_idle 애니메이션을 찾을 수 없습니다. VRChat Avatars SDK 샘플 에셋이 필요합니다.",
                             "proxy_idleアニメーションが見つかりません。VRChat Avatars SDKのサンプルアセットが必要です。" },
            /* 48 */ new[] { "No humanoid avatar found above the target, so the pose was skipped.",
                             "대상 위쪽에 휴머노이드 아바타가 없어 포즈를 적용하지 않았습니다.",
                             "対象の上位にヒューマノイドアバターがないため、ポーズは適用されません。" },
            /* 49 */ new[] { "Game View Preview", "게임 뷰 미리보기", "ゲームビュープレビュー" },
            /* 50 */ new[] { "Left-drag: Orbit   Right/Middle-drag: Pan   Scroll: Dolly   F: Focus",
                             "좌클릭: 회전   우클릭/휠: 이동   스크롤: 전후 이동   F: 포커스",
                             "左ドラッグ: 回転   右/中ドラッグ: 移動   スクロール: 前後移動   F: フォーカス" },
            /* 51 */ new[] { "Reset Camera", "카메라 초기화", "カメラをリセット" },
            /* 52 */ new[] { "Focus Subject", "대상 포커스", "被写体にフォーカス" },
            /* 53 */ new[] { "Apply to Camera", "카메라에 적용", "カメラに適用" },
            /* 54 */ new[] { "The preview camera is virtual. Capture uses this view; apply it to move the scene camera.",
                             "미리보기 카메라는 가상 카메라입니다. 캡처에는 이 구도가 사용되며, 씬 카메라를 옮기려면 적용하세요.",
                             "プレビューカメラは仮想カメラです。撮影にはこの構図が使われ、シーンカメラを動かすには適用してください。" },
            /* 55 */ new[] { "Select a Camera to preview the Game View.",
                             "게임 뷰를 미리보려면 카메라를 선택하세요.",
                             "ゲームビューをプレビューするにはカメラを選択してください。" },
            /* 56 */ new[] { "Field of View", "시야각", "視野角" },
            /* 57 */ new[] { "Focuses the selected object, or the visible character when no suitable object is selected.",
                             "선택한 오브젝트를 포커스합니다. 적절한 선택이 없으면 화면에 보이는 캐릭터를 자동으로 찾습니다.",
                             "選択したオブジェクトにフォーカスします。適切な選択がない場合は、画面内のキャラクターを自動で探します。" },
            /* 58 */ new[] { "No renderer could be captured from the target object.",
                             "대상 오브젝트에서 캡처할 수 있는 렌더러를 찾지 못했습니다.",
                             "対象オブジェクトからキャプチャできるレンダラーが見つかりません。" }
        };
        private static readonly string[][] BG_TEXT =
        {
            new[] { "Skybox",   "스카이박스", "スカイボックス" },
            new[] { "Color",    "색상",       "色"             },
            new[] { "Transparent", "투명",   "透明"           },
        };

        private Lang     _lang = Lang.English;
        private int      L     => (int)_lang;
        private string   T(int i) => UI[i][L];
        private const string LANGUAGE_PREF_KEY = "DiNeLang";
        private const string LEGACY_LANGUAGE_PREF_KEY = "DiNeScreenSaver_Lang";
        private static int SavedLanguageIndex => Mathf.Clamp(
            EditorPrefs.GetInt(LANGUAGE_PREF_KEY,
                EditorPrefs.GetInt(LEGACY_LANGUAGE_PREF_KEY, (int)Lang.English)), 0, 2);
        private static string GetUIString(int i) => UI[i][SavedLanguageIndex];
        private string[] BgLabels() => new[] { BG_TEXT[0][L], BG_TEXT[1][L], BG_TEXT[2][L] };

        // ══════════════════════════════════════════════════════════════════════
        //  Screenshot state
        // ══════════════════════════════════════════════════════════════════════
        private ToolMode      _mode          = ToolMode.Screenshot;
        private CaptureTarget _captureTarget = CaptureTarget.GameView;
        private ResPreset     _res           = ResPreset.FHD;
        private Aspect        _aspect        = Aspect._16_9;
        private BGType        _bgType        = BGType.Skybox;
        private Color         _bgColor       = Color.white;
        private Vector2       _captureSize   = new Vector2(1920, 1080);
        private Camera        _camera;

        // Game View preview
        private Vector2       _screenshotScroll;
        private Rect          _gamePreviewRect;
        private Camera        _gamePreviewCamera;
        private GameObject    _gamePreviewCameraObject;
        private RenderTexture _gamePreviewRenderTexture;
        private Camera        _gamePreviewSource;
        private Vector3       _gamePreviewPosition;
        private Quaternion    _gamePreviewRotation = Quaternion.identity;
        private Vector3       _gamePreviewPivot;
        private float         _gamePreviewPivotDistance = 10f;
        private float         _gamePreviewFieldOfView = 60f;
        private float         _gamePreviewOrthographicSize = 5f;
        private bool          _gamePreviewStateInitialized;
        private bool          _gamePreviewNavigationChanged;
        private bool          _gamePreviewDirty = true;
        private const int     GAME_PREVIEW_MAX_SIZE = 720;
        private const float   GAME_PREVIEW_MIN_FOV = 1f;
        private const float   GAME_PREVIEW_MAX_FOV = 179f;

        private static readonly Dictionary<ResPreset, Vector2> RES_BASE = new Dictionary<ResPreset, Vector2>
        {
            { ResPreset.FHD, new Vector2(1920, 1080) },
            { ResPreset.QHD, new Vector2(2560, 1440) },
            { ResPreset.UHD, new Vector2(3840, 2160) },
        };

        // ══════════════════════════════════════════════════════════════════════
        //  Icon state
        // ══════════════════════════════════════════════════════════════════════
        private GameObject _iconTarget;
        private bool       _iconPreviewUnavailable;
        private GameObject _prevIconTarget;
        private bool       _iconOutlineEnabled = true;
        private Color      _iconOutlineColor = new Color(0.03f, 0.03f, 0.03f, 1f);
        private int        _iconOutlineSize = 4;
        private bool       _iconForbiddenOverlay;
        private float      _iconForbiddenOpacity = 1f;
        private float      _iconForbiddenScale = 0.85f;
        private bool       _iconForbiddenBehindObject = true;
        private bool       _iconIdlePose;
        private readonly List<GameObject> _iconLinkedObjects = new List<GameObject>();
        private string     _iconOverwriteAssetPath;
        private DiNeMultiDresser _iconDresser;
        private int        _iconDresserLayerIndex = -1;
        private int        _iconDresserButtonIndex = -1;
        private DiNeSmartToggle _iconSmartToggle;

        // Preview
        private Texture2D _previewTex;
        private bool      _previewDirty;
        private Scene _previewSceneCache;
        private Vector2   _iconScroll;
        private GameObject _previewRootCache;
        private GameObject _previewCameraObjectCache;
        private Camera _previewCameraCache;
        private RenderTexture _previewRenderTextureCache;
        private Bounds _previewBoundsCache;
        private int _previewCaptureLayer;
        private Vector2   _previewEuler = new Vector2(0f, 180f);  // pitch, yaw
        private Vector2   _previewPan   = Vector2.zero;            // pan offset
        private float     _zoomFactor   = 1f;
        private Rect      _previewRect;
        private const int PREVIEW_RENDER_SIZE = 256;

        // 줌 프리셋
        private static readonly float[]  ZOOM_PRESETS       = { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
        private static readonly string[] ZOOM_PRESET_LABELS = { "0.5×", "0.75×", "1×", "1.5×", "2×", "3×" };

        // 방향 프리셋 (pitch, yaw)
        private static readonly Vector2[] DIR_EULERS =
        {
            new Vector2(  0f,  180f),  // Front
            new Vector2(  0f,    0f),  // Back
            new Vector2(  0f,   90f),  // Left
            new Vector2(  0f,  -90f),  // Right
            new Vector2(-90f,  180f),  // Top
            new Vector2( 90f,  180f),  // Bottom
        };

        // 에디터 배경색에 맞는 프리뷰 배경
        private static Color PreviewBG =>
            EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f)
                : new Color(0.76f, 0.76f, 0.76f);

        // ══════════════════════════════════════════════════════════════════════
        //  Assets
        // ══════════════════════════════════════════════════════════════════════
        private Texture2D _windowIcon;
        private Texture2D _tabIcon;
        private Font      _titleFont;

        // ══════════════════════════════════════════════════════════════════════
        //  Lifecycle & Context Menu
        // ══════════════════════════════════════════════════════════════════════
        [MenuItem("DiNe/Screen Saver", false, 3)]
        public static void ShowWindow()
        {
            bool created;
            var w = GetReusableWindow(out created);
            w.minSize  = new Vector2(175, 150);
            if (created)
                w.position = new Rect(w.position.x, w.position.y, 420, 620);
            w.Show();
            w.Focus();
        }

        private static DiNeScreenSaver GetReusableWindow(out bool created)
        {
            var openWindows = Resources.FindObjectsOfTypeAll<DiNeScreenSaver>();
            if (openWindows != null && openWindows.Length > 0 && openWindows[0] != null)
            {
                created = false;
                return openWindows[0];
            }

            created = true;
            return GetWindow<DiNeScreenSaver>();
        }

        public static void OpenIconEditor(
            GameObject target,
            Texture2D existingIcon,
            DiNeMultiDresser owner,
            int layerIndex,
            int buttonIndex,
            IEnumerable<GameObject> linkedObjects)
        {
            bool created;
            var window = GetReusableWindow(out created);
            window.ReleaseIconPreviewResources();
            window.minSize = new Vector2(420, 620);
            if (created)
                window.position = new Rect(window.position.x, window.position.y, 420, 720);
            window._mode = ToolMode.Icon;
            window._iconTarget = target;
            window._prevIconTarget = target;
            window._previewEuler = new Vector2(0f, 180f);
            window._previewPan = Vector2.zero;
            window._zoomFactor = 1f;
            window._previewDirty = true;
            window._iconPreviewUnavailable = false;
            window._iconOverwriteAssetPath = existingIcon != null
                ? AssetDatabase.GetAssetPath(existingIcon).Replace('\\', '/')
                : null;
            // Multi Dresser에서 편집한 아이콘도 공용 Icons 폴더에만 저장한다.
            if (!DiNeIconMaker.CanOverwriteAsset(window._iconOverwriteAssetPath) ||
                !window._iconOverwriteAssetPath.StartsWith(
                    ICON_ASSET_PATH, System.StringComparison.OrdinalIgnoreCase))
                window._iconOverwriteAssetPath = null;

            window._iconDresser = owner;
            window._iconDresserLayerIndex = layerIndex;
            window._iconDresserButtonIndex = buttonIndex;
            window._iconSmartToggle = null;
            window._iconLinkedObjects.Clear();
            if (linkedObjects != null)
                window._iconLinkedObjects.AddRange(linkedObjects);

            window.Show();
            window.Focus();
            window.Repaint();
        }

        public static void OpenIconEditor(DiNeSmartToggle smartToggle)
        {
            if (smartToggle == null) return;
            smartToggle.EnsureDefaults();

            bool created;
            var window = GetReusableWindow(out created);
            window.ReleaseIconPreviewResources();
            window.minSize = new Vector2(420, 620);
            if (created)
                window.position = new Rect(window.position.x, window.position.y, 420, 720);
            window._mode = ToolMode.Icon;
            window._iconTarget = smartToggle.gameObject;
            window._prevIconTarget = smartToggle.gameObject;
            window._previewEuler = smartToggle.IconEuler;
            window._previewPan = smartToggle.IconPan;
            window._zoomFactor = smartToggle.IconZoom;
            window._iconOutlineEnabled = smartToggle.IconOutline;
            window._iconOutlineColor = smartToggle.IconOutlineColor;
            window._iconOutlineSize = smartToggle.IconOutlineSize;
            window._iconForbiddenOverlay = smartToggle.IconForbiddenOverlay;
            window._iconForbiddenOpacity = smartToggle.IconForbiddenOpacity;
            window._iconForbiddenScale = smartToggle.IconForbiddenScale;
            window._iconForbiddenBehindObject = smartToggle.IconForbiddenBehindObject;
            window._iconIdlePose = smartToggle.IconIdlePose;
            window._previewDirty = true;
            window._iconPreviewUnavailable = false;
            window._iconOverwriteAssetPath = smartToggle.Icon != null
                ? AssetDatabase.GetAssetPath(smartToggle.Icon)
                : null;
            if (!DiNeIconMaker.CanOverwriteAsset(window._iconOverwriteAssetPath))
                window._iconOverwriteAssetPath = null;

            window._iconDresser = null;
            window._iconDresserLayerIndex = -1;
            window._iconDresserButtonIndex = -1;
            window._iconSmartToggle = smartToggle;
            window._iconLinkedObjects.Clear();
            window.Show();
            window.Focus();
            window.Repaint();
        }

        public static void GenerateIconFromHierarchy(MenuCommand menuCommand)
        {
            GameObject target = menuCommand.context as GameObject;
            if (target == null)
            {
                Debug.LogWarning("GameObject를 선택한 후 우클릭하세요.");
                return;
            }

            GenerateIconStatic(target, null, new Vector2(0f, 180f), Vector2.zero, 1f, 256, false,
                new DiNeIconMaker.Settings { outlineEnabled = false }, null);
        }

        void OnEnable()
        {
            LoadSettings();
            AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreviewResources;
            AssemblyReloadEvents.beforeAssemblyReload += ReleasePreviewResources;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Selection.selectionChanged -= OnSelectionChangedOutsideWindow;
            Selection.selectionChanged += OnSelectionChangedOutsideWindow;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            _windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            _tabIcon    = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
            _titleFont  = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
            titleContent = new GUIContent("Screen", _tabIcon);
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreviewResources;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Selection.selectionChanged -= OnSelectionChangedOutsideWindow;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            SaveSettings();
            ReleasePreviewResources();
        }

        // 창 밖에서 다른 오브젝트를 선택하면 프리뷰용 임시 오브젝트를 바로 정리한다.
        private void OnSelectionChangedOutsideWindow()
        {
            if (_previewRootCache == null || focusedWindow == this) return;
            ReleaseIconPreviewResources(true);
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ReleaseIconPreviewResources(true);
            ReleaseGamePreviewResources();
            DiNeScreenSaverPreviewGuard.CleanupLegacyMainScenePreviews();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                ReleasePreviewResources();
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                _previewDirty = true;
                _gamePreviewDirty = true;
                Repaint();
            }
        }

        void OnFocus()
        {
            if (_camera == null)
            {
                _camera = Camera.main ?? FindObjectOfType<Camera>();
                ResetGamePreviewState();
            }

            // 포커스가 돌아오면 프리뷰용 오브젝트를 다시 만들어 렌더한다.
            if (_mode == ToolMode.Icon && _iconTarget != null)
            {
                _previewDirty = true;
                Repaint();
            }
            else if (_mode == ToolMode.Screenshot && _captureTarget == CaptureTarget.GameView)
            {
                _gamePreviewDirty = true;
                Repaint();
            }
        }

        void OnInspectorUpdate()
        {
            if (_mode != ToolMode.Screenshot || _captureTarget != CaptureTarget.GameView || _camera == null)
                return;

            // 씬 오브젝트, 조명, 플레이 모드 카메라 변경도 미리보기에 느슨하게 실시간 반영한다.
            _gamePreviewDirty = true;
            Repaint();
        }

        // 창 밖을 클릭해 포커스를 잃으면 아이콘 프리뷰용 오브젝트를 즉시 정리한다.
        // (마지막 프리뷰 텍스처는 남겨두어 창 표시는 그대로 유지)
        void OnLostFocus()
        {
            ReleaseIconPreviewResources(true);
            if (_mode == ToolMode.Icon)
                DiNeScreenSaverPreviewGuard.CleanupLegacyMainScenePreviews();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  OnGUI
        // ══════════════════════════════════════════════════════════════════════
        void OnGUI()
        {
            int sharedLanguage = SavedLanguageIndex;
            if (sharedLanguage != L)
                _lang = (Lang)sharedLanguage;

            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            // ── 헤더 ──
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float iconSize = 72f;
            GUILayout.Label(_windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
            GUILayout.Space(6);
            GUILayout.Label("Screen Saver", new GUIStyle(EditorStyles.label)
            {
                font = _titleFont, alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold, fontSize = 36,
                normal = { textColor = Color.white }
            }, GUILayout.Height(iconSize));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            string desc = L == 1 ? "유니티 에디터 화면 보호기 기능과 아이콘 생성 기능을 제공합니다."
                        : L == 2 ? "Unityエディターのスクリーンセーバーおよびアイコン生成機能を提供します。"
                                 : "Provides Unity Editor screen saver and icon generation features.";
            GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            // ── 언어 선택 ──
            Lang selectedLanguage = (Lang)DrawToolbar(L, new[] { "English", "한국어", "日本語" }, 28);
            if (selectedLanguage != _lang)
            {
                _lang = selectedLanguage;
                EditorPrefs.SetInt(LANGUAGE_PREF_KEY, (int)_lang);
            }

            GUILayout.Space(6);

            // ── 모드 선택 ──
            ToolMode previousMode = _mode;
            _mode = (ToolMode)DrawToolbar((int)_mode, new[] { T(14), T(15) }, 32);
            if (previousMode == ToolMode.Icon && _mode != ToolMode.Icon)
            {
                ReleaseIconPreviewResources();
                _gamePreviewDirty = true;
            }
            else if (previousMode != ToolMode.Icon && _mode == ToolMode.Icon)
            {
                ReleaseGamePreviewResources();
                _previewDirty = true;
            }

            GUILayout.Space(10);

            if (_mode == ToolMode.Screenshot)
            {
                _screenshotScroll = EditorGUILayout.BeginScrollView(_screenshotScroll);
                DrawScreenshotMode();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                _iconScroll = EditorGUILayout.BeginScrollView(_iconScroll);
                DrawIconMode();
                EditorGUILayout.EndScrollView();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Screenshot Mode
        // ══════════════════════════════════════════════════════════════════════
        private void DrawScreenshotMode()
        {
            // 캡처 대상 탭
            CaptureTarget previousTarget = _captureTarget;
            _captureTarget = (CaptureTarget)DrawToolbar((int)_captureTarget, new[] { T(9), T(10) }, 28);
            if (previousTarget != _captureTarget)
            {
                if (_captureTarget == CaptureTarget.GameView)
                    _gamePreviewDirty = true;
                else
                    ReleaseGamePreviewResources();
            }
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical("box");
            if (_captureTarget == CaptureTarget.GameView)
            {
                EditorGUI.BeginChangeCheck();
                Camera selectedCamera = (Camera)EditorGUILayout.ObjectField(
                    new GUIContent(T(0)), _camera, typeof(Camera), true);
                if (EditorGUI.EndChangeCheck())
                {
                    _camera = selectedCamera;
                    ReleaseGamePreviewResources();
                    ResetGamePreviewState();
                }
                HLine();
            }

            EditorGUI.BeginChangeCheck();
            DrawResolutionSettings();
            HLine();
            DrawBackgroundSettings();
            if (EditorGUI.EndChangeCheck())
                _gamePreviewDirty = true;
            EditorGUILayout.EndVertical();

            if (_captureTarget == CaptureTarget.GameView)
            {
                GUILayout.Space(8);
                DrawGameViewPreview();
            }

            GUILayout.Space(12);

            EditorGUILayout.BeginVertical("box");
            EditorGUI.BeginDisabledGroup(_captureTarget == CaptureTarget.GameView && _camera == null);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            if (GUILayout.Button(T(11), new GUIStyle(GUI.skin.button)
                { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } },
                GUILayout.Height(45)))
            {
                if (_captureTarget == CaptureTarget.GameView) CaptureGameView();
                else CaptureSceneView();
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(T(12), GUILayout.Height(25)))
                OpenFolder(SCREENSHOT_ASSET_PATH);
            if (GUILayout.Button(T(13), GUILayout.Height(25)))
                PingFolder(SCREENSHOT_ASSET_PATH);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawResolutionSettings()
        {
            EditorGUILayout.LabelField(T(1), EditorStyles.boldLabel);
            _res = (ResPreset)EditorGUILayout.EnumPopup(new GUIContent(T(2)), _res);

            if (_res != ResPreset.Custom)
            {
                _aspect = (Aspect)EditorGUILayout.EnumPopup(new GUIContent(T(3)), _aspect);
                Vector2 base_ = RES_BASE[_res];
                switch (_aspect)
                {
                    case Aspect._16_9: _captureSize = base_; break;
                    case Aspect._9_16: _captureSize = new Vector2(base_.y, base_.x); break;
                    case Aspect._3_4:  _captureSize = new Vector2(base_.y * 0.75f, base_.y); break;
                    case Aspect._1_1:  _captureSize = new Vector2(base_.y, base_.y); break;
                }
                EditorGUILayout.LabelField(T(4), $"{Mathf.Round(_captureSize.x)} x {Mathf.Round(_captureSize.y)}");
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(5));
                _captureSize = EditorGUILayout.Vector2Field("", _captureSize);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBackgroundSettings()
        {
            EditorGUILayout.LabelField(T(6), EditorStyles.boldLabel);
            _bgType = (BGType)EditorGUILayout.Popup(new GUIContent(T(7)), (int)_bgType, BgLabels());
            if (_bgType == BGType.Color)
                _bgColor = EditorGUILayout.ColorField(T(8), _bgColor);
        }

        private void DrawGameViewPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(new GUIContent(T(49), T(50)), EditorStyles.boldLabel);

            if (_camera != null && !_camera.orthographic)
            {
                EnsureGamePreviewState();
                EditorGUI.BeginChangeCheck();
                float fieldOfView = EditorGUILayout.Slider(
                    new GUIContent(T(56)), _gamePreviewFieldOfView,
                    GAME_PREVIEW_MIN_FOV, GAME_PREVIEW_MAX_FOV);
                if (EditorGUI.EndChangeCheck())
                {
                    _gamePreviewFieldOfView = fieldOfView;
                    MarkGamePreviewNavigationChanged();
                }
                GUILayout.Space(3f);
            }

            float availableWidth = Mathf.Max(position.width - 38f, 120f);
            float captureAspect = GetCaptureAspect();
            float previewHeight = Mathf.Clamp(availableWidth / Mathf.Clamp(captureAspect, 0.55f, 1.8f), 150f, 280f);
            Rect containerRect = GUILayoutUtility.GetRect(availableWidth, previewHeight, GUILayout.ExpandWidth(true));
            GUI.Box(containerRect, GUIContent.none, EditorStyles.helpBox);

            Rect paddedRect = new Rect(
                containerRect.x + 4f, containerRect.y + 4f,
                Mathf.Max(1f, containerRect.width - 8f), Mathf.Max(1f, containerRect.height - 8f));
            _gamePreviewRect = FitAspect(paddedRect, captureAspect);

            if (_bgType == BGType.Transparent && Event.current.type == EventType.Repaint)
                DrawTransparencyGrid(_gamePreviewRect);
            else if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(_gamePreviewRect, Color.black);

            HandleGamePreviewInput();

            if (_gamePreviewDirty && _camera != null && Event.current.type == EventType.Repaint)
                RenderGameViewPreview();

            if (_camera != null && _gamePreviewRenderTexture != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(_gamePreviewRect, _gamePreviewRenderTexture, ScaleMode.StretchToFill, true);
            else if (_camera == null && Event.current.type == EventType.Repaint)
                GUI.Label(_gamePreviewRect, T(55), new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    wordWrap = true
                });

            if (_camera != null && Event.current.type == EventType.Repaint)
            {
                string presetLabel = _res == ResPreset.Custom ? "Custom" : _res.ToString();
                string sizeLabel = $"{presetLabel}  {Mathf.RoundToInt(_captureSize.x)} × {Mathf.RoundToInt(_captureSize.y)}";
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                Vector2 badgeSize = badgeStyle.CalcSize(new GUIContent(sizeLabel));
                Rect badgeRect = new Rect(
                    _gamePreviewRect.xMax - badgeSize.x - 14f,
                    _gamePreviewRect.y + 8f,
                    badgeSize.x + 8f,
                    20f);
                EditorGUI.DrawRect(badgeRect, new Color(0f, 0f, 0f, 0.62f));
                GUI.Label(badgeRect, sizeLabel, badgeStyle);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_camera == null);
            if (GUILayout.Button(T(51), GUILayout.Height(24f)))
                ResetGamePreviewState();

            if (GUILayout.Button(new GUIContent(T(52), T(57)), GUILayout.Height(24f)))
                FocusGamePreviewOnSubject();

            if (GUILayout.Button(new GUIContent(T(53), T(54)), GUILayout.Height(24f)))
                ApplyGamePreviewToSourceCamera();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static Rect FitAspect(Rect outer, float aspect)
        {
            aspect = Mathf.Max(aspect, 0.01f);
            float outerAspect = outer.width / Mathf.Max(outer.height, 1f);
            if (outerAspect > aspect)
            {
                float width = outer.height * aspect;
                return new Rect(outer.center.x - width * 0.5f, outer.y, width, outer.height);
            }

            float height = outer.width / aspect;
            return new Rect(outer.x, outer.center.y - height * 0.5f, outer.width, height);
        }

        private static void DrawTransparencyGrid(Rect rect)
        {
            const float cell = 12f;
            Color light = new Color(0.58f, 0.58f, 0.58f, 1f);
            Color dark = new Color(0.40f, 0.40f, 0.40f, 1f);
            int columns = Mathf.CeilToInt(rect.width / cell);
            int rows = Mathf.CeilToInt(rect.height / cell);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Rect cellRect = new Rect(
                        rect.x + x * cell,
                        rect.y + y * cell,
                        Mathf.Min(cell, rect.xMax - (rect.x + x * cell)),
                        Mathf.Min(cell, rect.yMax - (rect.y + y * cell)));
                    EditorGUI.DrawRect(cellRect, ((x + y) & 1) == 0 ? light : dark);
                }
            }
        }

        private float GetCaptureAspect()
        {
            float width = Mathf.Max(1f, _captureSize.x);
            float height = Mathf.Max(1f, _captureSize.y);
            return width / height;
        }

        private void HandleGamePreviewInput()
        {
            Event e = Event.current;
            if (_camera == null) return;

            int controlId = GUIUtility.GetControlID(
                "DiNeGameViewPreview".GetHashCode(), FocusType.Keyboard, _gamePreviewRect);
            bool pointerInside = _gamePreviewRect.Contains(e.mousePosition);
            if (pointerInside)
                EditorGUIUtility.AddCursorRect(_gamePreviewRect, MouseCursor.Orbit);
            if (!EnsureGamePreviewState()) return;

            if (e.type == EventType.MouseDown && pointerInside && e.button >= 0 && e.button <= 2)
            {
                GUIUtility.hotControl = controlId;
                GUIUtility.keyboardControl = controlId;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                e.Use();
                return;
            }

            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == controlId && e.keyCode == KeyCode.F)
            {
                FocusGamePreviewOnSubject();
                e.Use();
                return;
            }

            bool dragging = e.type == EventType.MouseDrag && GUIUtility.hotControl == controlId;
            if (dragging && e.button == 0)
            {
                Quaternion yaw = Quaternion.AngleAxis(e.delta.x * 0.3f, Vector3.up);
                Vector3 pitchAxis = yaw * (_gamePreviewRotation * Vector3.right);
                Quaternion pitch = Quaternion.AngleAxis(-e.delta.y * 0.3f, pitchAxis);
                _gamePreviewRotation = pitch * yaw * _gamePreviewRotation;
                _gamePreviewPosition = _gamePreviewPivot -
                                       (_gamePreviewRotation * Vector3.forward) * _gamePreviewPivotDistance;
                MarkGamePreviewNavigationChanged();
                e.Use();
            }
            else if (dragging && (e.button == 1 || e.button == 2))
            {
                float unitsPerPixel;
                if (_camera.orthographic)
                    unitsPerPixel = 2f * _gamePreviewOrthographicSize / Mathf.Max(_gamePreviewRect.height, 1f);
                else
                    unitsPerPixel = 2f * _gamePreviewPivotDistance *
                                    Mathf.Tan(_gamePreviewFieldOfView * 0.5f * Mathf.Deg2Rad) /
                                    Mathf.Max(_gamePreviewRect.height, 1f);

                Vector3 right = _gamePreviewRotation * Vector3.right;
                Vector3 up = _gamePreviewRotation * Vector3.up;
                Vector3 offset = (-right * e.delta.x + up * e.delta.y) * unitsPerPixel;
                _gamePreviewPosition += offset;
                _gamePreviewPivot += offset;
                MarkGamePreviewNavigationChanged();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && pointerInside)
            {
                float scale = Mathf.Exp(e.delta.y * 0.1f);
                if (_camera.orthographic)
                {
                    _gamePreviewOrthographicSize = Mathf.Clamp(
                        _gamePreviewOrthographicSize * scale, 0.001f, 100000f);
                }
                else
                {
                    _gamePreviewPivotDistance = Mathf.Clamp(
                        _gamePreviewPivotDistance * scale, 0.01f, 100000f);
                    _gamePreviewPosition = _gamePreviewPivot -
                                           (_gamePreviewRotation * Vector3.forward) * _gamePreviewPivotDistance;
                }
                MarkGamePreviewNavigationChanged();
                e.Use();
            }
        }

        private void MarkGamePreviewNavigationChanged()
        {
            _gamePreviewNavigationChanged = true;
            _gamePreviewDirty = true;
            Repaint();
        }

        private bool EnsureGamePreviewState()
        {
            if (_camera == null) return false;
            if (!_gamePreviewStateInitialized || _gamePreviewSource != _camera)
                ResetGamePreviewState();
            return _gamePreviewStateInitialized;
        }

        private void ResetGamePreviewState()
        {
            _gamePreviewSource = _camera;
            _gamePreviewStateInitialized = _camera != null;
            _gamePreviewNavigationChanged = false;
            _gamePreviewDirty = true;

            if (_camera == null)
                return;

            _gamePreviewPosition = _camera.transform.position;
            _gamePreviewRotation = _camera.transform.rotation;
            _gamePreviewFieldOfView = Mathf.Clamp(
                _camera.fieldOfView, GAME_PREVIEW_MIN_FOV, GAME_PREVIEW_MAX_FOV);
            _gamePreviewOrthographicSize = Mathf.Max(_camera.orthographicSize, 0.001f);
            _gamePreviewPivotDistance = EstimateGamePreviewPivotDistance(_camera);
            _gamePreviewPivot = _gamePreviewPosition +
                                (_gamePreviewRotation * Vector3.forward) * _gamePreviewPivotDistance;
            Repaint();
        }

        private float EstimateGamePreviewPivotDistance(Camera source)
        {
            float near = Mathf.Max(source.nearClipPlane, 0.01f);
            float far = Mathf.Max(source.farClipPlane, near * 2f);
            Bounds subjectBounds;
            float subjectScore;
            if (TryGetSelectedGamePreviewBounds(out subjectBounds) &&
                IsBoundsNearGamePreview(subjectBounds, out subjectScore))
            {
                float subjectDepth = Vector3.Dot(
                    subjectBounds.center - _gamePreviewPosition,
                    _gamePreviewRotation * Vector3.forward);
                return Mathf.Clamp(subjectDepth, near * 2f, far * 0.9f);
            }
            if (TryFindVisibleGamePreviewCharacterBounds(out subjectBounds))
            {
                float subjectDepth = Vector3.Dot(
                    subjectBounds.center - _gamePreviewPosition,
                    _gamePreviewRotation * Vector3.forward);
                return Mathf.Clamp(subjectDepth, near * 2f, far * 0.9f);
            }

            float fallback = Mathf.Clamp(10f, near * 2f, far * 0.25f);
            Ray ray = new Ray(source.transform.position, source.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Min(far, 10000f), source.cullingMask,
                    QueryTriggerInteraction.Ignore))
                return Mathf.Max(hit.distance, near * 2f);
            return fallback;
        }

        private void SyncGamePreviewFromSourceIfNeeded()
        {
            if (_camera == null || _gamePreviewNavigationChanged) return;

            _gamePreviewPosition = _camera.transform.position;
            _gamePreviewRotation = _camera.transform.rotation;
            _gamePreviewFieldOfView = Mathf.Clamp(
                _camera.fieldOfView, GAME_PREVIEW_MIN_FOV, GAME_PREVIEW_MAX_FOV);
            _gamePreviewOrthographicSize = Mathf.Max(_camera.orthographicSize, 0.001f);
            _gamePreviewPivot = _gamePreviewPosition +
                                (_gamePreviewRotation * Vector3.forward) * _gamePreviewPivotDistance;
        }

        private void RenderGameViewPreview()
        {
            if (!EnsureGamePreviewState() || !EnsureGamePreviewResources()) return;

            SyncGamePreviewFromSourceIfNeeded();
            ConfigureGamePreviewCamera(_gamePreviewCamera);
            _gamePreviewCamera.targetTexture = _gamePreviewRenderTexture;

            try
            {
                _gamePreviewCamera.Render();
            }
            finally
            {
                _gamePreviewCamera.targetTexture = null;
                _gamePreviewDirty = false;
            }
        }

        private void ConfigureGamePreviewCamera(Camera destination)
        {
            destination.CopyFrom(_camera);
            destination.enabled = false;
            destination.transform.SetPositionAndRotation(_gamePreviewPosition, _gamePreviewRotation);
            destination.fieldOfView = _gamePreviewFieldOfView;
            destination.orthographicSize = _gamePreviewOrthographicSize;
            destination.aspect = GetCaptureAspect();
            ApplyCameraBackground(destination);
        }

        private bool EnsureGamePreviewResources()
        {
            if (_gamePreviewCameraObject == null || _gamePreviewCamera == null)
            {
                _gamePreviewCameraObject = new GameObject("_DiNe_GamePreview_Cam")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _gamePreviewCamera = _gamePreviewCameraObject.AddComponent<Camera>();
                _gamePreviewCamera.enabled = false;
            }

            GetGamePreviewRenderSize(out int width, out int height);
            if (_gamePreviewRenderTexture != null &&
                (_gamePreviewRenderTexture.width != width || _gamePreviewRenderTexture.height != height))
            {
                if (_gamePreviewRenderTexture.IsCreated())
                    _gamePreviewRenderTexture.Release();
                DestroyImmediate(_gamePreviewRenderTexture);
                _gamePreviewRenderTexture = null;
            }

            if (_gamePreviewRenderTexture == null)
            {
                _gamePreviewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "_DiNe_GamePreview_RT",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                _gamePreviewRenderTexture.Create();
            }

            return _gamePreviewCamera != null && _gamePreviewRenderTexture != null;
        }

        private void GetGamePreviewRenderSize(out int width, out int height)
        {
            float aspect = GetCaptureAspect();
            if (aspect >= 1f)
            {
                width = GAME_PREVIEW_MAX_SIZE;
                height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            }
            else
            {
                height = GAME_PREVIEW_MAX_SIZE;
                width = Mathf.Max(1, Mathf.RoundToInt(height * aspect));
            }
        }

        private void FocusGamePreviewOnSubject()
        {
            if (!EnsureGamePreviewState()) return;

            Bounds bounds;
            if (!TryGetSelectedGamePreviewBounds(out bounds) &&
                !TryFindVisibleGamePreviewCharacterBounds(out bounds))
                return;

            _gamePreviewPivot = bounds.center;
            Vector3 previewSpaceExtents = GetBoundsExtentsInGamePreviewSpace(bounds);
            float aspect = GetCaptureAspect();
            float framedHalfHeight = Mathf.Max(
                previewSpaceExtents.y,
                previewSpaceExtents.x / Mathf.Max(aspect, 0.01f));
            framedHalfHeight = Mathf.Max(framedHalfHeight, 0.05f) * 1.2f;

            if (_camera.orthographic)
            {
                _gamePreviewOrthographicSize = framedHalfHeight;
                _gamePreviewPivotDistance = Mathf.Max(
                    _gamePreviewPivotDistance, previewSpaceExtents.z * 2f, 0.1f);
            }
            else
            {
                float halfFov = Mathf.Max(_gamePreviewFieldOfView * 0.5f * Mathf.Deg2Rad, 0.001f);
                float nearPlaneMargin = Mathf.Max(_camera.nearClipPlane * 1.1f, 0.01f);
                _gamePreviewPivotDistance = framedHalfHeight / Mathf.Tan(halfFov) +
                                            previewSpaceExtents.z + nearPlaneMargin;
            }

            _gamePreviewPosition = _gamePreviewPivot -
                                   (_gamePreviewRotation * Vector3.forward) * _gamePreviewPivotDistance;
            MarkGamePreviewNavigationChanged();
        }

        private bool TryGetSelectedGamePreviewBounds(out Bounds bounds)
        {
            bounds = default(Bounds);
            GameObject selected = Selection.activeGameObject;
            if (selected == null || _camera == null || selected == _camera.gameObject ||
                !selected.scene.IsValid())
                return false;

            GameObject boundsRoot = selected;
            Animator animator = selected.GetComponentInParent<Animator>();
            if (animator != null)
                boundsRoot = animator.gameObject;
            else
            {
                Renderer parentRenderer = selected.GetComponentInParent<Renderer>();
                if (parentRenderer != null)
                    boundsRoot = parentRenderer.gameObject;
            }

            return TryCalculateGamePreviewBounds(boundsRoot, out bounds);
        }

        private bool TryFindVisibleGamePreviewCharacterBounds(out Bounds bounds)
        {
            bounds = default(Bounds);
            float bestScore = float.MaxValue;
            bool found = false;
            var checkedRoots = new HashSet<GameObject>();

            foreach (Animator animator in FindObjectsOfType<Animator>())
            {
                if (animator == null || !animator.gameObject.scene.IsValid() ||
                    !checkedRoots.Add(animator.gameObject))
                    continue;

                Bounds candidate;
                float score;
                if (!TryCalculateGamePreviewBounds(animator.gameObject, out candidate) ||
                    !IsBoundsNearGamePreview(candidate, out score) || score >= bestScore)
                    continue;

                bounds = candidate;
                bestScore = score;
                found = true;
            }

            if (found)
                return true;

            // Animator가 없는 캐릭터도 처리할 수 있도록 스킨드 메시를 보조 후보로 사용한다.
            foreach (SkinnedMeshRenderer renderer in FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (!IsUsableGamePreviewRenderer(renderer))
                    continue;

                float score;
                if (!IsBoundsNearGamePreview(renderer.bounds, out score) || score >= bestScore)
                    continue;

                bounds = renderer.bounds;
                bestScore = score;
                found = true;
            }

            return found;
        }

        private bool TryCalculateGamePreviewBounds(GameObject root, out Bounds bounds)
        {
            bounds = default(Bounds);
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!IsUsableGamePreviewRenderer(renderer))
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private bool IsUsableGamePreviewRenderer(Renderer renderer)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                   !(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer) &&
                   !(renderer is LineRenderer) &&
                   renderer.gameObject.scene.IsValid() && _camera != null &&
                   (_camera.cullingMask & (1 << renderer.gameObject.layer)) != 0;
        }

        private bool IsBoundsNearGamePreview(Bounds bounds, out float score)
        {
            score = float.MaxValue;
            Vector3 localCenter = Quaternion.Inverse(_gamePreviewRotation) *
                                  (bounds.center - _gamePreviewPosition);
            if (localCenter.z <= 0.001f)
                return false;

            float halfHeight;
            if (_camera != null && _camera.orthographic)
                halfHeight = Mathf.Max(_gamePreviewOrthographicSize, 0.001f);
            else
                halfHeight = localCenter.z * Mathf.Tan(
                    Mathf.Clamp(_gamePreviewFieldOfView, GAME_PREVIEW_MIN_FOV, GAME_PREVIEW_MAX_FOV) *
                    0.5f * Mathf.Deg2Rad);

            float halfWidth = Mathf.Max(halfHeight * GetCaptureAspect(), 0.001f);
            halfHeight = Mathf.Max(halfHeight, 0.001f);
            float normalizedX = localCenter.x / halfWidth;
            float normalizedY = localCenter.y / halfHeight;
            if (Mathf.Abs(normalizedX) > 1.25f || Mathf.Abs(normalizedY) > 1.25f)
                return false;

            score = normalizedX * normalizedX + normalizedY * normalizedY + localCenter.z * 0.000001f;
            return true;
        }

        private Vector3 GetBoundsExtentsInGamePreviewSpace(Bounds bounds)
        {
            Quaternion inverseRotation = Quaternion.Inverse(_gamePreviewRotation);
            Vector3 worldExtents = bounds.extents;
            Vector3 previewExtents = Vector3.zero;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 worldOffset = new Vector3(
                    worldExtents.x * x, worldExtents.y * y, worldExtents.z * z);
                Vector3 previewOffset = inverseRotation * worldOffset;
                previewExtents.x = Mathf.Max(previewExtents.x, Mathf.Abs(previewOffset.x));
                previewExtents.y = Mathf.Max(previewExtents.y, Mathf.Abs(previewOffset.y));
                previewExtents.z = Mathf.Max(previewExtents.z, Mathf.Abs(previewOffset.z));
            }

            return previewExtents;
        }

        private void ApplyGamePreviewToSourceCamera()
        {
            if (!EnsureGamePreviewState()) return;

            Undo.RecordObjects(new Object[] { _camera.transform, _camera }, "Apply Screenshot Preview Camera");
            _camera.transform.SetPositionAndRotation(_gamePreviewPosition, _gamePreviewRotation);
            if (_camera.orthographic)
                _camera.orthographicSize = _gamePreviewOrthographicSize;
            else
                _camera.fieldOfView = _gamePreviewFieldOfView;

            EditorUtility.SetDirty(_camera);
            EditorUtility.SetDirty(_camera.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_camera.transform);
            if (_camera.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(_camera.gameObject.scene);

            _gamePreviewNavigationChanged = false;
            _gamePreviewDirty = true;
            Repaint();
        }

        private void ReleaseGamePreviewResources()
        {
            if (_gamePreviewCamera != null)
                _gamePreviewCamera.targetTexture = null;
            if (_gamePreviewRenderTexture != null)
            {
                if (_gamePreviewRenderTexture.IsCreated())
                    _gamePreviewRenderTexture.Release();
                DestroyImmediate(_gamePreviewRenderTexture);
            }
            if (_gamePreviewCameraObject != null)
                DestroyImmediate(_gamePreviewCameraObject);

            _gamePreviewRenderTexture = null;
            _gamePreviewCamera = null;
            _gamePreviewCameraObject = null;
        }

        private void ReleasePreviewResources()
        {
            ReleaseIconPreviewResources();
            ReleaseGamePreviewResources();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Icon Mode
        // ══════════════════════════════════════════════════════════════════════
        private void DrawIconMode()
        {
            // 타겟 설정
            EditorGUILayout.BeginVertical("box");
            _iconTarget = (GameObject)EditorGUILayout.ObjectField(T(16), _iconTarget, typeof(GameObject), true);
            EditorGUILayout.EndVertical();

            // 타겟 변경 시 프리뷰 갱신
            if (_iconTarget != _prevIconTarget)
            {
                ReleaseIconPreviewResources();
                _prevIconTarget  = _iconTarget;
                _previewEuler    = new Vector2(0f, 180f);
                _previewPan      = Vector2.zero;
                _zoomFactor      = 1f;
                _previewDirty    = true;
                _iconOverwriteAssetPath = null;
                _iconDresser = null;
                _iconDresserLayerIndex = -1;
                _iconDresserButtonIndex = -1;
                _iconLinkedObjects.Clear();
                _iconPreviewUnavailable = false;
            }

            GUILayout.Space(8);

            // ── 인터랙티브 프리뷰 ──
            float previewW = position.width - 20f;
            float previewH = Mathf.Min(previewW, 240f);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = PreviewBG;
            _previewRect = GUILayoutUtility.GetRect(previewW, previewH);
            GUI.Box(_previewRect, GUIContent.none, "box");
            GUI.backgroundColor = prevBg;

            // 이벤트 처리 (프리뷰 영역 내 마우스)
            HandlePreviewInput();

            // 렌더 (Repaint 시에만)
            if (_previewDirty && _iconTarget != null &&
                !EditorApplication.isPlayingOrWillChangePlaymode && Event.current.type == EventType.Repaint)
            {
                RenderPreview();
                _previewDirty = false;
            }

            // 프리뷰 텍스처 표시
            if (_previewTex != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(_previewRect, _previewTex, ScaleMode.ScaleToFit);
            else if (Event.current.type == EventType.Repaint)
                GUI.Label(_previewRect,
                    _iconTarget == null ? T(21) : (_iconPreviewUnavailable ? T(58) : string.Empty),
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                        { fontSize = 11, wordWrap = true });

            // 힌트
            GUILayout.Label(T(19), new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { fontSize = 10, normal = { textColor = new Color(0.55f, 0.55f, 0.58f) } });

            GUILayout.Space(4);

            // 뷰 초기화 (리셋) 버튼
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var resetBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold };
            if (GUILayout.Button(T(28), resetBtnStyle, GUILayout.Width(80), GUILayout.Height(20)))
            {
                _previewEuler = new Vector2(0f, 180f);
                _zoomFactor   = 1f;
                _previewPan   = Vector2.zero;
                _previewDirty = true;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── 방향 프리셋 버튼 (6개) ──
            DrawDirectionButtons();

            GUILayout.Space(3);

            // ── 줌 프리셋 버튼 ──
            DrawZoomButtons();

            GUILayout.Space(6);

            // ── 아이들 포즈 토글 ──
            EditorGUI.BeginChangeCheck();
            bool idlePose = DrawEffectCardHeader(T(45), _iconIdlePose);
            if (EditorGUI.EndChangeCheck())
            {
                _iconIdlePose = idlePose;
                ReleaseIconPreviewResources();
                _previewDirty = true;
                Repaint();
            }
            GUILayout.Label(T(46), new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { fontSize = 10, wordWrap = true });

            if (_iconIdlePose && _iconTarget != null)
            {
                if (LoadIdleClip() == null)
                    EditorGUILayout.HelpBox(T(47), MessageType.Warning);
                else if (FindHumanoidRoot(_iconTarget) == null)
                    EditorGUILayout.HelpBox(T(48), MessageType.Info);
            }

            GUILayout.Space(8);

            // ── 가시성 효과 ──
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect effectTitleRect = EditorGUILayout.GetControlRect(false, 24f);
            EditorGUI.DrawRect(new Rect(effectTitleRect.x, effectTitleRect.yMax - 1f, effectTitleRect.width, 1f),
                new Color(0.30f, 0.82f, 0.76f, 0.65f));
            GUI.Label(effectTitleRect, T(30), new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            });

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(0));
            _iconOutlineEnabled = DrawEffectCardHeader(T(31), _iconOutlineEnabled);
            if (_iconOutlineEnabled)
            {
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 54f;
                _iconOutlineColor = EditorGUILayout.ColorField(T(8), _iconOutlineColor);
                _iconOutlineSize = EditorGUILayout.IntSlider(T(33), _iconOutlineSize, 1, 12);
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(4f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(0));
            _iconForbiddenOverlay = DrawEffectCardHeader(T(34), _iconForbiddenOverlay);
            if (_iconForbiddenOverlay)
            {
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 54f;
                _iconForbiddenScale = EditorGUILayout.Slider(T(44), _iconForbiddenScale, 0.2f, 1.2f);
                _iconForbiddenOpacity = EditorGUILayout.Slider(T(36), _iconForbiddenOpacity, 0f, 1f);
                EditorGUIUtility.labelWidth = previousLabelWidth;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(41), EditorStyles.miniLabel, GUILayout.Width(48f));
                bool frontSelected = !_iconForbiddenBehindObject;
                if (DrawPositionSegment(T(42), frontSelected, EditorStyles.miniButtonLeft))
                    _iconForbiddenBehindObject = false;
                if (DrawPositionSegment(T(43), !frontSelected, EditorStyles.miniButtonRight))
                    _iconForbiddenBehindObject = true;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                _previewDirty = true;
                Repaint();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // ── 설정 및 버튼 ──
            EditorGUILayout.BeginVertical("box");

            if (!string.IsNullOrEmpty(_iconOverwriteAssetPath))
                EditorGUILayout.HelpBox($"{T(37)}\n{_iconOverwriteAssetPath}", MessageType.Info);

            GUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(_iconTarget == null);
            
            // 동일 이름 파일이 이미 존재하는지 체크
            bool fileExists = false;
            if (_iconTarget != null)
            {
                string candidatePath = !string.IsNullOrEmpty(_iconOverwriteAssetPath)
                    ? _iconOverwriteAssetPath
                    : ICON_ASSET_PATH + _iconTarget.name + ".png";
                fileExists = File.Exists(candidatePath);
            }

            EditorGUILayout.BeginHorizontal();
            
            // 기존 덮어쓰기 생성 버튼 (메인)
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            string generateLabel = string.IsNullOrEmpty(_iconOverwriteAssetPath) ? T(18) : T(38);
            if (GUILayout.Button(generateLabel, new GUIStyle(GUI.skin.button)
                { fontSize = 14, fontStyle = FontStyle.Bold,
                  normal = { textColor = Color.white }, hover = { textColor = Color.white } },
                GUILayout.Height(38)))
            {
                GenerateCurrentIcon(false);
            }

            // 파일이 존재할 경우 복사본 생성 버튼을 우측에 추가 (가로 폭 제한 제거하여 1:1 분할, 짙은 민트색 적용)
            if (fileExists)
            {
                GUI.backgroundColor = new Color(0.18f, 0.68f, 0.62f); 
                if (GUILayout.Button(T(29), new GUIStyle(GUI.skin.button)
                    { fontSize = 14, fontStyle = FontStyle.Bold,
                      normal = { textColor = Color.white }, hover = { textColor = Color.white } },
                    GUILayout.Height(38)))
                {
                    GenerateCurrentIcon(true);
                }
            }
            
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(T(12), GUILayout.Height(25)))
                OpenFolder(ICON_ASSET_PATH);
            if (GUILayout.Button(T(13), GUILayout.Height(25)))
                PingFolder(ICON_ASSET_PATH); 
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDirectionButtons()
        {
            var dirLabels = new[] { T(22), T(23), T(24), T(25), T(26), T(27) };
            var prevBg = GUI.backgroundColor;
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                hover     = { textColor = Color.white },
                padding   = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter
            };

            float btnW = (position.width - 35f) / dirLabels.Length;

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < dirLabels.Length; i++)
            {
                bool active = (_previewEuler == DIR_EULERS[i]);
                GUI.backgroundColor = active
                    ? new Color(0.30f, 0.82f, 0.76f)
                    : new Color(0.21f, 0.21f, 0.24f);

                if (GUILayout.Button(dirLabels[i], btnStyle, GUILayout.Height(24), GUILayout.Width(btnW)))
                {
                    _previewEuler = DIR_EULERS[i];
                    _previewDirty = true;
                    Repaint();
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawEffectCardHeader(string label, bool enabled)
        {
            Rect row = EditorGUILayout.GetControlRect(false, 24f);
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            GUI.backgroundColor = enabled ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.55f, 0.55f, 0.55f);
            GUI.contentColor = Color.white;
            var style = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            string buttonLabel = enabled ? $"✓  {label}" : label;
            bool updated = GUI.Toggle(row, enabled, buttonLabel, style);
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return updated;
        }

        private static bool DrawPositionSegment(string label, bool selected, GUIStyle baseStyle)
        {
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            GUI.backgroundColor = selected ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.55f, 0.55f, 0.55f);
            GUI.contentColor = Color.white;
            var style = new GUIStyle(baseStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 10,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
            bool clicked = GUILayout.Button(label, style, GUILayout.Height(20f), GUILayout.MinWidth(34f));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return clicked;
        }

        private void DrawZoomButtons()
        {
            var prevBg = GUI.backgroundColor;
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 10,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                hover     = { textColor = Color.white },
                padding   = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter
            };

            float btnW = (position.width - 35f) / ZOOM_PRESETS.Length;

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < ZOOM_PRESETS.Length; i++)
            {
                bool active = Mathf.Approximately(_zoomFactor, ZOOM_PRESETS[i]);
                GUI.backgroundColor = active
                    ? new Color(0.30f, 0.82f, 0.76f)
                    : new Color(0.21f, 0.21f, 0.24f);

                if (GUILayout.Button(ZOOM_PRESET_LABELS[i], btnStyle, GUILayout.Height(22), GUILayout.Width(btnW)))
                {
                    _zoomFactor   = ZOOM_PRESETS[i];
                    _previewDirty = true;
                    Repaint();
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        private void HandlePreviewInput()
        {
            Event e = Event.current;
            if (!_previewRect.Contains(e.mousePosition)) return;

            EditorGUIUtility.AddCursorRect(_previewRect, MouseCursor.Orbit);

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _previewEuler.y += e.delta.x * 0.5f;
                _previewEuler.x += e.delta.y * 0.5f;
                _previewEuler.x  = Mathf.Clamp(_previewEuler.x, -85f, 85f);
                _previewDirty    = true;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && e.button == 1)
            {
                float panSpeed = 0.005f / _zoomFactor; 
                _previewPan.x -= e.delta.x * panSpeed;
                _previewPan.y += e.delta.y * panSpeed; 
                _previewDirty = true;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _zoomFactor   = Mathf.Clamp(_zoomFactor - e.delta.y * 0.06f, 0.2f, 5f);
                _previewDirty = true;
                e.Use();
                Repaint();
            }
        }

        private DiNeIconMaker.Settings GetIconEffectSettings()
        {
            return new DiNeIconMaker.Settings
            {
                outlineEnabled = _iconOutlineEnabled,
                outlineColor = _iconOutlineColor,
                outlineSize = _iconOutlineSize,
                forbiddenOverlay = _iconForbiddenOverlay,
                forbiddenOpacity = _iconForbiddenOpacity,
                forbiddenScale = _iconForbiddenScale,
                forbiddenBehindObject = _iconForbiddenBehindObject
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Idle Pose (VRChat proxy_idle)
        // ══════════════════════════════════════════════════════════════════════
        private const string IDLE_CLIP_ASSET_PATH =
            "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_idle.anim";
        private static AnimationClip _cachedIdleClip;

        private static AnimationClip LoadIdleClip()
        {
            if (_cachedIdleClip != null) return _cachedIdleClip;

            _cachedIdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IDLE_CLIP_ASSET_PATH);
            if (_cachedIdleClip != null) return _cachedIdleClip;

            // SDK 샘플을 프로젝트로 임포트해 경로가 달라진 경우를 위한 폴백.
            foreach (string guid in AssetDatabase.FindAssets("proxy_idle t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), "proxy_idle",
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;
                _cachedIdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (_cachedIdleClip != null) break;
            }
            return _cachedIdleClip;
        }

        // 대상 위쪽에서 휴머노이드 아바타 루트를 찾는다. 포즈는 이 루트에만 샘플링할 수 있다.
        private static Animator FindHumanoidRoot(GameObject target)
        {
            if (target == null) return null;
            for (Transform t = target.transform; t != null; t = t.parent)
            {
                var animator = t.GetComponent<Animator>();
                if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
                    return animator;
            }
            return null;
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == child) return string.Empty;
            var parts = new List<string>();
            for (Transform t = child; t != null && t != root; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static GameObject ResolveClone(Transform cloneRoot, Transform sourceRoot, GameObject sourceObject)
        {
            if (sourceObject == null || cloneRoot == null || sourceRoot == null) return null;
            if (sourceObject.transform == sourceRoot) return cloneRoot.gameObject;
            if (!sourceObject.transform.IsChildOf(sourceRoot)) return null;

            Transform found = cloneRoot.Find(GetRelativePath(sourceRoot, sourceObject.transform));
            return found != null ? found.gameObject : null;
        }

        private static void EnsureActiveUpTo(Transform node, Transform stopAt)
        {
            for (Transform t = node; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                if (t == stopAt) break;
            }
        }

        /// <summary>
        /// 프리뷰와 저장이 같은 결과를 내도록 캡처용 클론 계층을 만든다.
        /// 아이들 포즈가 켜져 있고 대상이 휴머노이드 아바타 아래에 있으면 아바타 전체를 복제해
        /// proxy_idle을 샘플링한 뒤, 캡처 레이어는 대상 서브트리에만 적용한다.
        /// 반환값은 바운즈 계산에 쓸 캡처 루트 목록이다.
        /// </summary>
        private static List<GameObject> BuildCaptureContent(
            GameObject root,
            GameObject target,
            IEnumerable<GameObject> linkedObjects,
            bool idlePose,
            int captureLayer)
        {
            var captureRoots = new List<GameObject>();
            Animator humanoid = idlePose ? FindHumanoidRoot(target) : null;
            AnimationClip idleClip = humanoid != null ? LoadIdleClip() : null;

            if (humanoid != null && idleClip != null)
            {
                GameObject avatarClone = Instantiate(humanoid.gameObject, root.transform);
                avatarClone.hideFlags = HideFlags.HideAndDontSave;
                avatarClone.SetActive(true);

                GameObject targetClone = ResolveClone(avatarClone.transform, humanoid.transform, target);
                if (targetClone == null)
                {
                    // 대상 클론을 찾지 못하면 아바타 전체가 찍히므로 기존 방식으로 되돌린다.
                    DestroyImmediate(avatarClone);
                }
                else
                {
                    EnsureActiveUpTo(targetClone.transform, avatarClone.transform);
                    captureRoots.Add(targetClone);

                    if (linkedObjects != null)
                    {
                        foreach (GameObject linked in linkedObjects)
                        {
                            if (linked == null || linked == target) continue;
                            GameObject linkedClone = ResolveClone(avatarClone.transform, humanoid.transform, linked);
                            if (linkedClone == null)
                            {
                                linkedClone = Instantiate(linked, root.transform);
                                linkedClone.hideFlags = HideFlags.HideAndDontSave;
                                linkedClone.SetActive(true);
                            }
                            else
                            {
                                EnsureActiveUpTo(linkedClone.transform, avatarClone.transform);
                            }
                            captureRoots.Add(linkedClone);
                        }
                    }

                    var cloneAnimator = avatarClone.GetComponent<Animator>();
                    if (cloneAnimator != null)
                        cloneAnimator.runtimeAnimatorController = null;
                    idleClip.SampleAnimation(avatarClone, 0f);
                }
            }

            if (captureRoots.Count == 0)
            {
                GameObject clone = Instantiate(target, root.transform);
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.SetActive(true);
                captureRoots.Add(clone);

                if (linkedObjects != null)
                {
                    foreach (GameObject linked in linkedObjects)
                    {
                        if (linked == null || linked == target) continue;
                        GameObject linkedClone = Instantiate(linked, root.transform);
                        linkedClone.hideFlags = HideFlags.HideAndDontSave;
                        linkedClone.SetActive(true);
                        captureRoots.Add(linkedClone);
                    }
                }
            }

            foreach (GameObject captureRoot in captureRoots)
                ChangeLayerRecursively(captureRoot, captureLayer);

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            return captureRoots;
        }

        private static bool TryGetCaptureBounds(List<GameObject> captureRoots, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (GameObject captureRoot in captureRoots)
            {
                foreach (Renderer renderer in captureRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        // 캡처 클론은 평소 렌더링을 꺼 둔 채로 두고 실제 렌더 순간에만 켠다.
        // 프리뷰 씬 생성이 실패해 일반 씬에 남더라도 씬 뷰/게임 뷰 어디에도 보이지 않는다.
        private static void SetCaptureRenderingEnabled(GameObject root, bool enabled)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                    renderer.forceRenderingOff = !enabled;
            }
        }

        private void GenerateCurrentIcon(bool createCopy)
        {
            Texture2D generated = GenerateIconStatic(
                _iconTarget,
                _iconLinkedObjects,
                _previewEuler,
                _previewPan,
                _zoomFactor,
                256,
                createCopy,
                GetIconEffectSettings(),
                _iconOverwriteAssetPath,
                _iconIdlePose);

            if (generated == null || createCopy)
                return;

            _iconOverwriteAssetPath = AssetDatabase.GetAssetPath(generated);
            if (_iconDresser != null && _iconDresserLayerIndex >= 0 && _iconDresserButtonIndex >= 0 &&
                _iconDresserLayerIndex < _iconDresser.layers.Count)
            {
                Undo.RecordObject(_iconDresser, "Edit Multi Dresser Icon");
                DiNeMultiDresser.DresserLayer layer = _iconDresser.layers[_iconDresserLayerIndex];
                while (layer.icons.Count <= _iconDresserButtonIndex)
                    layer.icons.Add(null);
                layer.icons[_iconDresserButtonIndex] = generated;
                EditorUtility.SetDirty(_iconDresser);
                AssetDatabase.SaveAssets();
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }

            if (_iconSmartToggle != null)
            {
                Undo.RecordObject(_iconSmartToggle, "Edit Smart Toggle Icon");
                _iconSmartToggle.Icon = generated;
                _iconSmartToggle.IconEuler = _previewEuler;
                _iconSmartToggle.IconPan = _previewPan;
                _iconSmartToggle.IconZoom = _zoomFactor;
                _iconSmartToggle.IconOutline = _iconOutlineEnabled;
                _iconSmartToggle.IconOutlineColor = _iconOutlineColor;
                _iconSmartToggle.IconOutlineSize = _iconOutlineSize;
                _iconSmartToggle.IconForbiddenOverlay = _iconForbiddenOverlay;
                _iconSmartToggle.IconForbiddenOpacity = _iconForbiddenOpacity;
                _iconSmartToggle.IconForbiddenScale = _iconForbiddenScale;
                _iconSmartToggle.IconForbiddenBehindObject = _iconForbiddenBehindObject;
                _iconSmartToggle.IconIdlePose = _iconIdlePose;
                EditorUtility.SetDirty(_iconSmartToggle);
                AssetDatabase.SaveAssets();
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
        }

        public static Texture2D GenerateConfiguredIcon(
            GameObject target,
            Vector2 euler,
            Vector2 pan,
            float zoom,
            DiNeIconMaker.Settings effects,
            string overwriteAssetPath,
            bool idlePose = false)
        {
            return GenerateIconStatic(
                target, null, euler, pan, zoom, 256, false, effects, overwriteAssetPath, idlePose);
        }

        private void RenderPreview()
        {
            if (_iconTarget == null) return;
            if (!EnsureIconPreviewResources())
            {
                _iconPreviewUnavailable = true;
                return;
            }
            _iconPreviewUnavailable = false;

            float dist = Mathf.Max(_previewBoundsCache.extents.magnitude * 2.5f, 0.01f);
            _previewCameraObjectCache.transform.rotation = Quaternion.Euler(_previewEuler.x, _previewEuler.y, 0f);
            Vector3 panOffset = _previewCameraObjectCache.transform.right * _previewPan.x +
                                _previewCameraObjectCache.transform.up * _previewPan.y;
            _previewCameraObjectCache.transform.position =
                (_previewBoundsCache.center + panOffset) - _previewCameraObjectCache.transform.forward * dist;
            _previewCameraCache.orthographicSize =
                Mathf.Max(_previewBoundsCache.extents.x, Mathf.Max(_previewBoundsCache.extents.y, _previewBoundsCache.extents.z)) *
                1.2f / Mathf.Max(_zoomFactor, 0.01f);

            RenderTexture previousActive = RenderTexture.active;
            try
            {
                _previewCameraCache.targetTexture = _previewRenderTextureCache;
                SetCaptureRenderingEnabled(_previewRootCache, true);
                try { _previewCameraCache.Render(); }
                finally { SetCaptureRenderingEnabled(_previewRootCache, false); }
                RenderTexture.active = _previewRenderTextureCache;
                _previewTex.ReadPixels(new Rect(0, 0, PREVIEW_RENDER_SIZE, PREVIEW_RENDER_SIZE), 0, 0);
                _previewTex.Apply(false, false);
                DiNeIconMaker.ApplyEffects(_previewTex, GetIconEffectSettings());
            }
            finally
            {
                _previewCameraCache.targetTexture = null;
                RenderTexture.active = previousActive;
            }
        }

        private bool EnsureIconPreviewResources()
        {
            if (_previewRootCache != null && _previewCameraCache != null &&
                _previewRenderTextureCache != null && _previewTex != null)
                return true;

            ReleaseIconPreviewResources();
            try
            {
                _previewCaptureLayer = DiNeIconMaker.FindAvailableCaptureLayer();
                _previewSceneCache = EditorSceneManager.NewPreviewScene();
                _previewRootCache = new GameObject("_DiNe_Preview_Root")
                    { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(_previewRootCache, _previewSceneCache);
                List<GameObject> captureRoots = BuildCaptureContent(
                    _previewRootCache, _iconTarget, _iconLinkedObjects, _iconIdlePose, _previewCaptureLayer);
                HideFromSceneViewIfNotPreviewScene(_previewRootCache);
                // 렌더 순간 외에는 항상 꺼 두어 어떤 뷰에도 노출되지 않게 한다.
                SetCaptureRenderingEnabled(_previewRootCache, false);

                if (!TryGetCaptureBounds(captureRoots, out _previewBoundsCache))
                {
                    ReleaseIconPreviewResources();
                    return false;
                }

                _previewCameraObjectCache = new GameObject("_DiNe_Preview_Cam")
                    { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(_previewCameraObjectCache, _previewSceneCache);
                _previewCameraCache = _previewCameraObjectCache.AddComponent<Camera>();
                // Camera.Render()는 카메라가 지정한 씬만 렌더하므로, 격리된 캡처 복제본과
                // 카메라가 같은 프리뷰 씬을 바라보게 해야 한다.
                _previewCameraCache.scene = _previewSceneCache;
                DiNeIconMaker.AddPreviewLight(_previewCameraObjectCache.transform, _previewCaptureLayer);
                _previewCameraCache.enabled = false;
                _previewCameraCache.clearFlags = CameraClearFlags.SolidColor;
                _previewCameraCache.backgroundColor = Color.clear;
                _previewCameraCache.cullingMask = 1 << _previewCaptureLayer;
                _previewCameraCache.orthographic = true;
                _previewCameraCache.nearClipPlane = 0.001f;
                _previewCameraCache.farClipPlane = 10000f;
                _previewCameraCache.allowHDR = false;
                _previewCameraCache.allowMSAA = false;

                _previewRenderTextureCache = new RenderTexture(
                    PREVIEW_RENDER_SIZE, PREVIEW_RENDER_SIZE, 24, RenderTextureFormat.ARGB32)
                {
                    name = "_DiNe_Preview_RT",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                _previewRenderTextureCache.Create();
                _previewTex = new Texture2D(
                    PREVIEW_RENDER_SIZE, PREVIEW_RENDER_SIZE, TextureFormat.ARGB32, false)
                {
                    name = "_DiNe_Preview_Texture",
                    hideFlags = HideFlags.HideAndDontSave
                };
                return true;
            }
            catch
            {
                ReleaseIconPreviewResources();
                throw;
            }
        }

        private void ReleaseIconPreviewResources()
        {
            ReleaseIconPreviewResources(false);
        }

        private void ReleaseIconPreviewResources(bool keepPreviewTexture)
        {
            if (_previewCameraCache != null)
                _previewCameraCache.targetTexture = null;
            if (!keepPreviewTexture && _previewTex != null)
                DestroyImmediate(_previewTex);
            if (_previewRenderTextureCache != null)
            {
                if (_previewRenderTextureCache.IsCreated())
                    _previewRenderTextureCache.Release();
                DestroyImmediate(_previewRenderTextureCache);
            }
            if (_previewCameraObjectCache != null)
                DestroyImmediate(_previewCameraObjectCache);
            if (_previewRootCache != null)
                DestroyImmediate(_previewRootCache);
            if (_previewSceneCache.IsValid())
                EditorSceneManager.ClosePreviewScene(_previewSceneCache);

            if (!keepPreviewTexture)
                _previewTex = null;
            _previewRenderTextureCache = null;
            _previewCameraCache = null;
            _previewCameraObjectCache = null;
            _previewRootCache = null;
            _previewSceneCache = default;
        }

        internal static void ReleaseAllIconPreviewResources()
        {
            foreach (DiNeScreenSaver window in Resources.FindObjectsOfTypeAll<DiNeScreenSaver>())
            {
                if (window != null)
                    window.ReleasePreviewResources();
            }
        }

        private static Texture2D GenerateIconStatic(
            GameObject target,
            IEnumerable<GameObject> linkedObjects,
            Vector2 euler,
            Vector2 pan,
            float zoom,
            int outputSize,
            bool autoRename,
            DiNeIconMaker.Settings effects,
            string overwriteAssetPath,
            bool idlePose = false)
        {
            if (target == null) return null;
            EnsureDir(ICON_ASSET_PATH);

            GameObject root   = null;
            GameObject camObj = null;
            RenderTexture rt  = null;
            Scene previewScene = default;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                int captureLayer = DiNeIconMaker.FindAvailableCaptureLayer();
                previewScene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("_DiNe_Icon_Root") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, previewScene);
                List<GameObject> captureRoots =
                    BuildCaptureContent(root, target, linkedObjects, idlePose, captureLayer);
                HideFromSceneViewIfNotPreviewScene(root);
                SetCaptureRenderingEnabled(root, false);

                Bounds bounds;
                if (!TryGetCaptureBounds(captureRoots, out bounds))
                {
                    Debug.LogWarning("[DiNe Icon] No renderers found.");
                    return null;
                }

                camObj = new GameObject("_DiNe_Icon_Cam") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(camObj, previewScene);
                var cam = camObj.AddComponent<Camera>();
                cam.scene = previewScene;
                DiNeIconMaker.AddPreviewLight(camObj.transform, captureLayer);
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.cullingMask     = 1 << captureLayer;
                cam.orthographic    = true;
                cam.nearClipPlane   = 0.001f;
                cam.farClipPlane    = 10000f;

                float dist = bounds.extents.magnitude * 2.5f;
                camObj.transform.rotation = Quaternion.Euler(euler.x, euler.y, 0f);
                
                Vector3 panOffset = camObj.transform.right * pan.x + camObj.transform.up * pan.y;
                camObj.transform.position = (bounds.center + panOffset) - camObj.transform.forward * dist;
                
                cam.orthographicSize      = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.2f / zoom;

                const int RENDER_SIZE = 2048;
                rt = new RenderTexture(RENDER_SIZE, RENDER_SIZE, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                using (DiNeIconMaker.IsolateSceneRenderers(root))
                {
                    SetCaptureRenderingEnabled(root, true);
                    try { cam.Render(); }
                    finally { SetCaptureRenderingEnabled(root, false); }
                }

                RenderTexture.active = rt;
                var raw = new Texture2D(RENDER_SIZE, RENDER_SIZE, TextureFormat.ARGB32, false);
                raw.ReadPixels(new Rect(0, 0, RENDER_SIZE, RENDER_SIZE), 0, 0);
                raw.Apply();
                cam.targetTexture = null;
                RenderTexture.active = previousActive;

                // 오토 크롭
                int effectMargin = effects != null && effects.outlineEnabled
                    ? Mathf.Clamp(effects.outlineSize, 1, 12) + 2
                    : 2;
                var cropped = AutoCrop(raw, outputSize, effectMargin);
                DestroyImmediate(raw);
                DiNeIconMaker.ApplyEffects(cropped, effects ?? new DiNeIconMaker.Settings());

                // 파일 이름 및 복사본 넘버링 처리
                string filePath = DiNeIconMaker.CanOverwriteAsset(overwriteAssetPath)
                    ? overwriteAssetPath.Replace('\\', '/')
                    : DiNeIconMaker.GetDefaultIconAssetPath(target.name);
                string directoryPath = Path.GetDirectoryName(filePath)?.Replace('\\', '/') ?? ICON_ASSET_PATH.TrimEnd('/');
                string baseName = Path.GetFileNameWithoutExtension(filePath);

                if (autoRename && File.Exists(filePath))
                {
                    int counter = 2;
                    string candidate;
                    do
                    {
                        candidate = $"{directoryPath}/{baseName}_{counter}.png";
                        counter++;
                    }
                    while (File.Exists(candidate));
                    filePath = candidate;
                }

                Directory.CreateDirectory(directoryPath);
                File.WriteAllBytes(filePath, cropped.EncodeToPNG());
                DestroyImmediate(cropped);

                AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
                if (importer != null)
                {
                    importer.alphaIsTransparency = true;
                    importer.textureType         = TextureImporterType.Sprite;
                    importer.mipmapEnabled       = false;
                    importer.npotScale           = TextureImporterNPOTScale.None;
                    importer.maxTextureSize      = 256;
                    importer.SaveAndReimport();
                }

                // 에셋을 선택 상태로 만들고 하이라이트 표시
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
                Debug.Log($"[DiNe Icon] {GetUIString(20)} → {filePath}");
                return asset;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rt     != null) DestroyImmediate(rt);
                if (camObj != null) DestroyImmediate(camObj);
                if (root   != null) DestroyImmediate(root);
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        // ── 오토 크롭: 불투명 픽셀 범위를 찾아 정사각형으로 잘라낸 후 리사이즈 ──
        private static Texture2D AutoCrop(Texture2D src, int targetSize, int margin)
        {
            int minX = src.width, maxX = 0, minY = src.height, maxY = 0;
            var pixels = src.GetPixels32();
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                    if (pixels[y * src.width + x].a > 0)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }

            if (maxX < minX) { minX = 0; maxX = src.width - 1; minY = 0; maxY = src.height - 1; }

            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            int sq = Mathf.Max(cw, ch);

            var square = new Texture2D(sq, sq, TextureFormat.ARGB32, false);
            var clear  = new Color32[sq * sq];
            square.SetPixels32(clear);
            square.SetPixels(sq / 2 - cw / 2, sq / 2 - ch / 2, cw, ch, src.GetPixels(minX, minY, cw, ch));
            square.Apply();

            margin = Mathf.Clamp(margin, 0, targetSize / 4);
            int contentSize = targetSize - margin * 2;
            Texture2D resized = ResizeTex(square, contentSize);
            var output = new Texture2D(targetSize, targetSize, TextureFormat.ARGB32, false);
            output.SetPixels32(new Color32[targetSize * targetSize]);
            output.SetPixels(margin, margin, contentSize, contentSize, resized.GetPixels());
            output.Apply();
            DestroyImmediate(resized);
            return output;
        }

        private static Texture2D ResizeTex(Texture2D src, int size)
        {
            var dst = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var rt  = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(src, rt);
            dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            dst.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(src);
            return dst;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Screenshot Capture
        // ══════════════════════════════════════════════════════════════════════
        private void CaptureGameView()
        {
            if (!EnsureGamePreviewState())
            {
                Debug.LogError("[DiNe] Game View camera is not assigned.");
                return;
            }

            EnsureDir(SCREENSHOT_ASSET_PATH);
            SyncGamePreviewFromSourceIfNeeded();

            int w = Mathf.Max(1, (int)Mathf.Round(_captureSize.x));
            int h = Mathf.Max(1, (int)Mathf.Round(_captureSize.y));
            TextureFormat format = _bgType == BGType.Transparent ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            Texture2D tex = null;
            RenderTexture rt = null;
            GameObject captureCameraObject = null;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                tex = new Texture2D(w, h, format, false);
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                // 실제 저장에는 카메라 GameObject 전체를 복제해 후처리/SRP 추가 데이터도 유지한다.
                Camera captureCamera = Instantiate(_camera);
                captureCameraObject = captureCamera.gameObject;
                captureCameraObject.name = "_DiNe_GameCapture_Cam";
                captureCameraObject.hideFlags = HideFlags.HideAndDontSave;
                foreach (Camera camera in captureCameraObject.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in captureCameraObject.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
                ConfigureGamePreviewCamera(captureCamera);
                captureCamera.targetTexture = rt;
                RenderTexture.active = rt;
                captureCamera.Render();
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                captureCamera.targetTexture = null;
                SaveScreenshot(tex, w, h);
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rt != null)
                {
                    if (rt.IsCreated()) rt.Release();
                    DestroyImmediate(rt);
                }
                if (tex != null) DestroyImmediate(tex);
                if (captureCameraObject != null) DestroyImmediate(captureCameraObject);
            }
        }

        private void CaptureSceneView()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) { Debug.LogError("[DiNe] Scene View not active."); return; }

            EnsureDir(SCREENSHOT_ASSET_PATH);
            int w = (int)_captureSize.x, h = (int)_captureSize.y;
            var orig_rt  = RenderTexture.active;
            var orig_cf  = sv.camera.clearFlags;
            var orig_bg  = sv.camera.backgroundColor;

            var rt = new RenderTexture(w, h, 24);
            sv.camera.targetTexture = rt;
            ApplyCameraBackground(sv.camera);
            sv.Repaint();
            sv.camera.Render();
            RenderTexture.active = rt;

            var format = _bgType == BGType.Transparent ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            var tex = new Texture2D(w, h, format, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            sv.camera.targetTexture = null;
            sv.camera.clearFlags    = orig_cf;
            sv.camera.backgroundColor = orig_bg;
            RenderTexture.active    = orig_rt;

            SaveScreenshot(tex, w, h);
            DestroyImmediate(tex);
            DestroyImmediate(rt);
        }

        private void ApplyCameraBackground(Camera cam)
        {
            switch (_bgType)
            {
                case BGType.Skybox:
                    cam.clearFlags      = CameraClearFlags.Skybox;
                    cam.backgroundColor = Color.white;
                    break;
                case BGType.Color:
                    cam.clearFlags      = CameraClearFlags.SolidColor;
                    cam.backgroundColor = _bgColor;
                    break;
                case BGType.Transparent:
                    cam.clearFlags      = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.clear;
                    break;
            }
        }

        private void SaveScreenshot(Texture2D tex, int w, int h)
        {
            string path = Application.dataPath.Replace("Assets", "") + SCREENSHOT_ASSET_PATH;
            string fileName = System.DateTime.Now.ToString($"yyyy-MM-dd_HH-mm-ss ({w}x{h})") + ".png";
            string fullPath = path + fileName;
            
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.Refresh();

            // 에셋을 선택 상태로 만들고 하이라이트 표시
            string assetPath = SCREENSHOT_ASSET_PATH + fileName;
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Utilities
        // ══════════════════════════════════════════════════════════════════════
        private static void ChangeLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                ChangeLayerRecursively(child.gameObject, layer);
        }

        // 프리뷰 씬 생성이 실패해 일반 씬에 남는 경우에도 씬 뷰에 보이지 않도록 한다.
        private static void HideFromSceneViewIfNotPreviewScene(GameObject root)
        {
            if (root == null) return;
            Scene scene = root.scene;
            if (!scene.IsValid() || EditorSceneManager.IsPreviewScene(scene)) return;

            SceneVisibilityManager.instance.Hide(root, true);
            SceneVisibilityManager.instance.DisablePicking(root, true);
        }

        private static void EnsureDir(string assetPath)
        {
            string abs = Application.dataPath.Replace("Assets", "") + assetPath;
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        private static void OpenFolder(string assetPath)
        {
            EnsureDir(assetPath);
            string absPath = Application.dataPath.Replace("Assets", "") + assetPath;
            Application.OpenURL("file://" + absPath);
        }

        // 폴더 안쪽으로 진입하도록 변경
        private static void PingFolder(string assetPath)
        {
            EnsureDir(assetPath);
            AssetDatabase.Refresh();
            var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath.TrimEnd('/'));
            if (obj != null) 
            { 
                Selection.activeObject = obj; 
                EditorGUIUtility.PingObject(obj); 
                
                // 폴더를 더블클릭한 것과 동일한 효과를 주어 내부로 진입
                AssetDatabase.OpenAsset(obj);
            }
        }

        private static void HLine()
        {
            GUILayout.Space(5);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(5);
        }

        private int DrawToolbar(int selected, string[] options, float height)
        {
            EditorGUILayout.BeginHorizontal();
            int result = selected;
            for (int i = 0; i < options.Length; i++)
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = i == selected ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f);
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 12,
                    fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                    normal    = { textColor = i == selected ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
                };
                if (GUILayout.Button(options[i], style, GUILayout.Height(height))) result = i;
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Settings persistence
        // ══════════════════════════════════════════════════════════════════════
        private void SaveSettings()
        {
            EditorPrefs.SetInt(LANGUAGE_PREF_KEY,              (int)_lang);
            EditorPrefs.SetInt("DiNeScreenSaver_Mode",        (int)_mode);
            EditorPrefs.SetInt("DiNeScreenSaver_Target",      (int)_captureTarget);
            EditorPrefs.SetInt("DiNeScreenSaver_Res",         (int)_res);
            EditorPrefs.SetInt("DiNeScreenSaver_Aspect",      (int)_aspect);
            EditorPrefs.SetInt("DiNeScreenSaver_BG",          (int)_bgType);
            EditorPrefs.SetString("DiNeScreenSaver_BGColor",  ColorUtility.ToHtmlStringRGBA(_bgColor));
            EditorPrefs.SetFloat("DiNeScreenSaver_CapW",      _captureSize.x);
            EditorPrefs.SetFloat("DiNeScreenSaver_CapH",      _captureSize.y);
            EditorPrefs.SetBool("DiNeScreenSaver_IconOutline", _iconOutlineEnabled);
            EditorPrefs.SetString("DiNeScreenSaver_IconOutlineColor", ColorUtility.ToHtmlStringRGBA(_iconOutlineColor));
            EditorPrefs.SetInt("DiNeScreenSaver_IconOutlineSize", _iconOutlineSize);
            EditorPrefs.SetBool("DiNeScreenSaver_IconForbidden", _iconForbiddenOverlay);
            EditorPrefs.SetFloat("DiNeScreenSaver_IconForbiddenOpacity", _iconForbiddenOpacity);
            EditorPrefs.SetFloat("DiNeScreenSaver_IconForbiddenScale", _iconForbiddenScale);
            EditorPrefs.SetBool("DiNeScreenSaver_IconForbiddenBehind", _iconForbiddenBehindObject);
            EditorPrefs.SetBool("DiNeScreenSaver_IconIdlePose", _iconIdlePose);
            
            EditorPrefs.SetFloat("DiNe_IconPitch", _previewEuler.x);
            EditorPrefs.SetFloat("DiNe_IconYaw",   _previewEuler.y);
            EditorPrefs.SetFloat("DiNe_IconZoom",  _zoomFactor);
            EditorPrefs.SetFloat("DiNe_IconPanX",  _previewPan.x);
            EditorPrefs.SetFloat("DiNe_IconPanY",  _previewPan.y);
        }

        private void LoadSettings()
        {
            _lang          = (Lang)SavedLanguageIndex;
            _mode          = (ToolMode)EditorPrefs.GetInt("DiNeScreenSaver_Mode",  0);
            _captureTarget = (CaptureTarget)EditorPrefs.GetInt("DiNeScreenSaver_Target", 0);
            _res           = (ResPreset)EditorPrefs.GetInt("DiNeScreenSaver_Res",  0);
            _aspect        = (Aspect)EditorPrefs.GetInt("DiNeScreenSaver_Aspect",  0);
            _bgType        = (BGType)EditorPrefs.GetInt("DiNeScreenSaver_BG",      0);
            if (EditorPrefs.HasKey("DiNeScreenSaver_BGColor"))
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("DiNeScreenSaver_BGColor"), out _bgColor);
            _captureSize.x = EditorPrefs.GetFloat("DiNeScreenSaver_CapW", 1920);
            _captureSize.y = EditorPrefs.GetFloat("DiNeScreenSaver_CapH", 1080);
            _iconOutlineEnabled = EditorPrefs.GetBool("DiNeScreenSaver_IconOutline", true);
            if (EditorPrefs.HasKey("DiNeScreenSaver_IconOutlineColor"))
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("DiNeScreenSaver_IconOutlineColor"), out _iconOutlineColor);
            _iconOutlineSize = EditorPrefs.GetInt("DiNeScreenSaver_IconOutlineSize", 4);
            _iconForbiddenOverlay = EditorPrefs.GetBool("DiNeScreenSaver_IconForbidden", false);
            _iconForbiddenOpacity = EditorPrefs.GetFloat("DiNeScreenSaver_IconForbiddenOpacity", 1f);
            _iconForbiddenScale = EditorPrefs.GetFloat("DiNeScreenSaver_IconForbiddenScale", 0.85f);
            _iconForbiddenBehindObject = EditorPrefs.GetBool("DiNeScreenSaver_IconForbiddenBehind", true);
            _iconIdlePose = EditorPrefs.GetBool("DiNeScreenSaver_IconIdlePose", false);
            const string behindDefaultMigrationKey = "DiNeScreenSaver_IconForbiddenBehind_DefaultTrue_Migrated";
            if (!EditorPrefs.GetBool(behindDefaultMigrationKey, false))
            {
                _iconForbiddenBehindObject = true;
                EditorPrefs.SetBool(behindDefaultMigrationKey, true);
            }
            const string overlayScaleMigrationKey = "DiNeScreenSaver_IconForbiddenScale_085_Migrated";
            if (!EditorPrefs.GetBool(overlayScaleMigrationKey, false))
            {
                if (Mathf.Approximately(_iconForbiddenScale, 0.7f))
                    _iconForbiddenScale = 0.85f;
                EditorPrefs.SetBool(overlayScaleMigrationKey, true);
            }
            
            _previewEuler.x = EditorPrefs.GetFloat("DiNe_IconPitch", 0f);
            _previewEuler.y = EditorPrefs.GetFloat("DiNe_IconYaw", 180f);
            _zoomFactor     = EditorPrefs.GetFloat("DiNe_IconZoom",   1f);
            _previewPan.x   = EditorPrefs.GetFloat("DiNe_IconPanX",   0f);
            _previewPan.y   = EditorPrefs.GetFloat("DiNe_IconPanY",   0f);
        }
    }

    [InitializeOnLoad]
    public sealed class DiNeScreenSaverPreviewGuard : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => -10000;

        static DiNeScreenSaverPreviewGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += CleanupLegacyMainScenePreviews;
        }

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar)
            {
                DiNeScreenSaver.ReleaseAllIconPreviewResources();
                CleanupLegacyMainScenePreviews();
            }

            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            DiNeScreenSaver.ReleaseAllIconPreviewResources();
            CleanupLegacyMainScenePreviews();
        }

        internal static void CleanupLegacyMainScenePreviews()
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject == null || EditorUtility.IsPersistent(gameObject) ||
                    !gameObject.scene.IsValid() || EditorSceneManager.IsPreviewScene(gameObject.scene))
                    continue;

                bool isLegacyPreview = gameObject.name == "_DiNe_Preview_Root" ||
                                       gameObject.name == "_DiNe_Preview_Cam" ||
                                       gameObject.name == "_DiNe_Icon_Root" ||
                                       gameObject.name == "_DiNe_Icon_Cam" ||
                                       gameObject.name == "_DiNe_GamePreview_Cam" ||
                                       gameObject.name == "_DiNe_GameCapture_Cam" ||
                                       gameObject.name == "DiNe_IconRoot" ||
                                       gameObject.name == "DiNe_IconCamera";
                // 이름이 충분히 고유하므로 hideFlags가 풀린 잔여물까지 함께 정리한다.
                // (아바타 하위로 잘못 들어간 경우에도 업로드 전에 확실히 제거되도록)
                if (isLegacyPreview)
                    Object.DestroyImmediate(gameObject);
            }
        }
    }
}
