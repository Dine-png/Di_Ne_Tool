using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

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
        private const string ICON_ASSET_PATH       = "Assets/Di Ne/Icons/";

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
            /* 19 */ new[] { "Left-drag: Rotate   Scroll: Zoom",
                             "좌클릭 드래그: 회전   스크롤: 줌",
                             "左ドラッグ: 回転   スクロール: ズーム"    },
            /* 20 */ new[] { "Icon saved.",           "아이콘 저장 완료!","アイコンを保存しました。" },
            /* 21 */ new[] { "Assign a Target Object.", "대상 오브젝트를 지정하세요.", "対象オブジェクトを指定してください。" },
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
        private GameObject _prevIconTarget;
        private int        _iconSizeIdx = 3; // default 256
        private static readonly int[] ICON_SIZES = { 32, 64, 128, 256, 512 };
        private static readonly string[] ICON_SIZE_LABELS = { "32", "64", "128", "256", "512" };

        // Preview
        private Texture2D _previewTex;
        private bool      _previewDirty;
        private Vector2   _previewEuler = new Vector2(15f, 180f);  // pitch, yaw
        private float     _zoomFactor   = 1f;
        private Rect      _previewRect;
        private const int PREVIEW_RENDER_SIZE = 256;
        private const int CAPTURE_LAYER       = 21;

        // ══════════════════════════════════════════════════════════════════════
        //  Assets
        // ══════════════════════════════════════════════════════════════════════
        private Texture2D _windowIcon;
        private Texture2D _tabIcon;
        private Font      _titleFont;

        // ══════════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════════
        [MenuItem("DiNe/EX/Screen Saver", false, 101)]
        public static void ShowWindow()
        {
            var w = GetWindow<DiNeScreenSaver>();
            w.minSize  = new Vector2(175, 150);
            w.position = new Rect(w.position.x, w.position.y, 420, 620);
        }

        void OnEnable()
        {
            LoadSettings();
            _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
            _tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
            _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
            titleContent = new GUIContent("Screen", _tabIcon);
        }

        void OnDisable()
        {
            SaveSettings();
            if (_previewTex != null) DestroyImmediate(_previewTex);
        }

        void OnFocus()
        {
            if (_camera == null)
            {
                _camera = Camera.main ?? FindObjectOfType<Camera>();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  OnGUI
        // ══════════════════════════════════════════════════════════════════════
        void OnGUI()
        {
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
            _lang = (Lang)DrawToolbar(L, new[] { "English", "한국어", "日本語" }, 28);

            GUILayout.Space(6);

            // ── 모드 선택 ──
            _mode = (ToolMode)DrawToolbar((int)_mode, new[] { T(14), T(15) }, 32);

            GUILayout.Space(10);

            if (_mode == ToolMode.Screenshot)
                DrawScreenshotMode();
            else
                DrawIconMode();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Screenshot Mode
        // ══════════════════════════════════════════════════════════════════════
        private void DrawScreenshotMode()
        {
            // 캡처 대상 탭
            _captureTarget = (CaptureTarget)DrawToolbar((int)_captureTarget, new[] { T(9), T(10) }, 28);
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical("box");
            if (_captureTarget == CaptureTarget.GameView)
            {
                _camera = (Camera)EditorGUILayout.ObjectField(new GUIContent(T(0)), _camera, typeof(Camera), true);
                HLine();
            }
            DrawResolutionSettings();
            HLine();
            DrawBackgroundSettings();
            EditorGUILayout.EndVertical();

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
                _prevIconTarget  = _iconTarget;
                _previewEuler    = new Vector2(15f, 180f);
                _zoomFactor      = 1f;
                _previewDirty    = true;
            }

            GUILayout.Space(8);

            // ── 인터랙티브 프리뷰 ──
            float previewW = position.width - 20f;
            float previewH = Mathf.Min(previewW, 240f);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.13f, 0.13f, 0.15f);
            _previewRect = GUILayoutUtility.GetRect(previewW, previewH);
            GUI.Box(_previewRect, GUIContent.none, "box");
            GUI.backgroundColor = prevBg;

            // 이벤트 처리 (프리뷰 영역 내 마우스)
            HandlePreviewInput();

            // 렌더 (Repaint 시에만)
            if (_previewDirty && _iconTarget != null && Event.current.type == EventType.Repaint)
            {
                RenderPreview();
                _previewDirty = false;
            }

            // 프리뷰 텍스처 표시
            if (_previewTex != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(_previewRect, _previewTex, ScaleMode.ScaleToFit);
            else if (_iconTarget == null && Event.current.type == EventType.Repaint)
                GUI.Label(_previewRect, T(21), new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    { fontSize = 11 });

            // 힌트
            GUILayout.Label(T(19), new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { fontSize = 10, normal = { textColor = new Color(0.55f, 0.55f, 0.58f) } });

            GUILayout.Space(8);

            // ── 설정 및 버튼 ──
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(17), GUILayout.Width(80));
            _iconSizeIdx = EditorGUILayout.Popup(_iconSizeIdx, ICON_SIZE_LABELS);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(_iconTarget == null);
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            if (GUILayout.Button(T(18), new GUIStyle(GUI.skin.button)
                { fontSize = 14, fontStyle = FontStyle.Bold,
                  normal = { textColor = Color.white }, hover = { textColor = Color.white } },
                GUILayout.Height(38)))
            {
                GenerateIcon();
            }
            GUI.backgroundColor = prevBg;
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

        private void HandlePreviewInput()
        {
            Event e = Event.current;
            if (!_previewRect.Contains(e.mousePosition)) return;

            EditorGUIUtility.AddCursorRect(_previewRect, MouseCursor.Orbit);

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _previewEuler.y += e.delta.x * 0.5f;
                _previewEuler.x -= e.delta.y * 0.5f;
                _previewEuler.x  = Mathf.Clamp(_previewEuler.x, -85f, 85f);
                _previewDirty    = true;
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

        private void RenderPreview()
        {
            if (_iconTarget == null) return;

            if (_previewTex != null) { DestroyImmediate(_previewTex); _previewTex = null; }

            GameObject root   = null;
            GameObject camObj = null;
            RenderTexture rt  = null;

            try
            {
                root = new GameObject("_DiNe_Preview_Root");
                var clone = Instantiate(_iconTarget, root.transform);
                clone.SetActive(true);
                ChangeLayerRecursively(root, CAPTURE_LAYER);

                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.updateWhenOffscreen = true;

                var allRenderers = root.GetComponentsInChildren<Renderer>(true);
                if (allRenderers.Length == 0) return;

                Bounds bounds = allRenderers[0].bounds;
                foreach (var r in allRenderers) bounds.Encapsulate(r.bounds);

                camObj = new GameObject("_DiNe_Preview_Cam");
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.13f, 0.15f, 1f);
                cam.cullingMask     = 1 << CAPTURE_LAYER;
                cam.orthographic    = true;
                cam.nearClipPlane   = 0.001f;
                cam.farClipPlane    = 10000f;

                float dist = bounds.extents.magnitude * 2.5f;
                camObj.transform.rotation = Quaternion.Euler(_previewEuler.x, _previewEuler.y, 0f);
                camObj.transform.position = bounds.center - camObj.transform.forward * dist;

                float orthoSize       = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.2f / _zoomFactor;
                cam.orthographicSize  = orthoSize;

                rt = new RenderTexture(PREVIEW_RENDER_SIZE, PREVIEW_RENDER_SIZE, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                _previewTex = new Texture2D(PREVIEW_RENDER_SIZE, PREVIEW_RENDER_SIZE, TextureFormat.ARGB32, false);
                _previewTex.ReadPixels(new Rect(0, 0, PREVIEW_RENDER_SIZE, PREVIEW_RENDER_SIZE), 0, 0);
                _previewTex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;
            }
            finally
            {
                if (rt     != null) DestroyImmediate(rt);
                if (camObj != null) DestroyImmediate(camObj);
                if (root   != null) DestroyImmediate(root);
            }
        }

        private void GenerateIcon()
        {
            if (_iconTarget == null) return;

            int outputSize = ICON_SIZES[_iconSizeIdx];
            EnsureDir(ICON_ASSET_PATH);

            GameObject root   = null;
            GameObject camObj = null;
            RenderTexture rt  = null;

            try
            {
                root = new GameObject("_DiNe_Icon_Root");
                var clone = Instantiate(_iconTarget, root.transform);
                clone.SetActive(true);
                ChangeLayerRecursively(root, CAPTURE_LAYER);

                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.updateWhenOffscreen = true;

                var allRenderers = root.GetComponentsInChildren<Renderer>(true);
                if (allRenderers.Length == 0) { Debug.LogWarning("[DiNe Icon] No renderers found."); return; }

                Bounds bounds = allRenderers[0].bounds;
                foreach (var r in allRenderers) bounds.Encapsulate(r.bounds);

                camObj = new GameObject("_DiNe_Icon_Cam");
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.cullingMask     = 1 << CAPTURE_LAYER;
                cam.orthographic    = true;
                cam.nearClipPlane   = 0.001f;
                cam.farClipPlane    = 10000f;

                float dist = bounds.extents.magnitude * 2.5f;
                camObj.transform.rotation = Quaternion.Euler(_previewEuler.x, _previewEuler.y, 0f);
                camObj.transform.position = bounds.center - camObj.transform.forward * dist;
                cam.orthographicSize      = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.2f / _zoomFactor;

                const int RENDER_SIZE = 2048;
                rt = new RenderTexture(RENDER_SIZE, RENDER_SIZE, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var raw = new Texture2D(RENDER_SIZE, RENDER_SIZE, TextureFormat.ARGB32, false);
                raw.ReadPixels(new Rect(0, 0, RENDER_SIZE, RENDER_SIZE), 0, 0);
                raw.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;

                // 오토 크롭
                var cropped = AutoCrop(raw, outputSize);
                DestroyImmediate(raw);

                string filePath = ICON_ASSET_PATH + _iconTarget.name + ".png";
                File.WriteAllBytes(filePath, cropped.EncodeToPNG());
                DestroyImmediate(cropped);

                AssetDatabase.Refresh();
                var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
                if (importer != null)
                {
                    importer.alphaIsTransparency = true;
                    importer.textureType         = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }

                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(filePath));
                Debug.Log($"[DiNe Icon] {T(20)} → {filePath}");
            }
            finally
            {
                if (rt     != null) DestroyImmediate(rt);
                if (camObj != null) DestroyImmediate(camObj);
                if (root   != null) DestroyImmediate(root);
            }
        }

        // ── 오토 크롭: 불투명 픽셀 범위를 찾아 정사각형으로 잘라낸 후 리사이즈 ──
        private static Texture2D AutoCrop(Texture2D src, int targetSize)
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

            return ResizeTex(square, targetSize);
        }

        private static Texture2D ResizeTex(Texture2D src, int size)
        {
            var dst = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var rt  = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            Graphics.Blit(src, rt);
            dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            dst.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(src);
            return dst;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Screenshot Capture
        // ══════════════════════════════════════════════════════════════════════
        private void CaptureGameView()
        {
            EnsureDir(SCREENSHOT_ASSET_PATH);
            var captureCamera = Instantiate(_camera);
            int w = (int)Mathf.Round(_captureSize.x), h = (int)Mathf.Round(_captureSize.y);
            var format = _bgType == BGType.Transparent ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            var tex = new Texture2D(w, h, format, false);
            var rt  = new RenderTexture(w, h, 24);
            RenderTexture.active    = rt;
            captureCamera.targetTexture = rt;
            ApplyCameraBackground(captureCamera);
            captureCamera.Render();
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            SaveScreenshot(tex, w, h);
            DestroyImmediate(captureCamera.gameObject);
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
            string name = path + System.DateTime.Now.ToString($"yyyy-MM-dd_HH-mm-ss ({w}x{h})") + ".png";
            File.WriteAllBytes(name, tex.EncodeToPNG());
            AssetDatabase.Refresh();
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

        private static void EnsureDir(string assetPath)
        {
            string abs = Application.dataPath.Replace("Assets", "") + assetPath;
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        private static void OpenFolder(string assetPath)
        {
            EnsureDir(assetPath);
            EditorUtility.RevealInFinder(Application.dataPath.Replace("Assets", "") + assetPath);
        }

        private static void PingFolder(string assetPath)
        {
            EnsureDir(assetPath);
            AssetDatabase.Refresh();
            var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath.TrimEnd('/'));
            if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
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
            EditorPrefs.SetInt("DiNeScreenSaver_Lang",        (int)_lang);
            EditorPrefs.SetInt("DiNeScreenSaver_Mode",        (int)_mode);
            EditorPrefs.SetInt("DiNeScreenSaver_Target",      (int)_captureTarget);
            EditorPrefs.SetInt("DiNeScreenSaver_Res",         (int)_res);
            EditorPrefs.SetInt("DiNeScreenSaver_Aspect",      (int)_aspect);
            EditorPrefs.SetInt("DiNeScreenSaver_BG",          (int)_bgType);
            EditorPrefs.SetString("DiNeScreenSaver_BGColor",  ColorUtility.ToHtmlStringRGBA(_bgColor));
            EditorPrefs.SetFloat("DiNeScreenSaver_CapW",      _captureSize.x);
            EditorPrefs.SetFloat("DiNeScreenSaver_CapH",      _captureSize.y);
            EditorPrefs.SetInt("DiNeScreenSaver_IconSize",    _iconSizeIdx);
            EditorPrefs.SetFloat("DiNeScreenSaver_EulerX",   _previewEuler.x);
            EditorPrefs.SetFloat("DiNeScreenSaver_EulerY",   _previewEuler.y);
            EditorPrefs.SetFloat("DiNeScreenSaver_Zoom",     _zoomFactor);
        }

        private void LoadSettings()
        {
            _lang          = (Lang)EditorPrefs.GetInt("DiNeScreenSaver_Lang",      (int)Lang.English);
            _mode          = (ToolMode)EditorPrefs.GetInt("DiNeScreenSaver_Mode",  0);
            _captureTarget = (CaptureTarget)EditorPrefs.GetInt("DiNeScreenSaver_Target", 0);
            _res           = (ResPreset)EditorPrefs.GetInt("DiNeScreenSaver_Res",  0);
            _aspect        = (Aspect)EditorPrefs.GetInt("DiNeScreenSaver_Aspect",  0);
            _bgType        = (BGType)EditorPrefs.GetInt("DiNeScreenSaver_BG",      0);
            if (EditorPrefs.HasKey("DiNeScreenSaver_BGColor"))
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("DiNeScreenSaver_BGColor"), out _bgColor);
            _captureSize.x = EditorPrefs.GetFloat("DiNeScreenSaver_CapW", 1920);
            _captureSize.y = EditorPrefs.GetFloat("DiNeScreenSaver_CapH", 1080);
            _iconSizeIdx   = EditorPrefs.GetInt("DiNeScreenSaver_IconSize", 3);
            _previewEuler.x = EditorPrefs.GetFloat("DiNeScreenSaver_EulerX", 15f);
            _previewEuler.y = EditorPrefs.GetFloat("DiNeScreenSaver_EulerY", 180f);
            _zoomFactor     = EditorPrefs.GetFloat("DiNeScreenSaver_Zoom",   1f);
        }
    }
}
