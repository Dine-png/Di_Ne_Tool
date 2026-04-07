using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace DiNeScreenSaver
{
    public class DiNeScreenSaver : EditorWindow
    {
        private enum LanguagePreset { English, Korean, Japanese }
        private LanguagePreset language = LanguagePreset.English;

        private enum CaptureTarget { GameView, SceneView }
        private CaptureTarget captureTarget = CaptureTarget.GameView;

        private enum resPreset
        {
            [InspectorName("FHD")] FHD,
            [InspectorName("QHD")] QHD,
            [InspectorName("UHD")] UHD,
            [InspectorName("Custom")] Custom
        }
        private resPreset res = resPreset.FHD;

        private enum Aspect
        {
            [InspectorName("16:9")] _16_9,
            [InspectorName("9:16")] _9_16,
            [InspectorName("3:4")] _3_4,
            [InspectorName("1:1")] _1_1
        }
        private Aspect aspectRatio = Aspect._16_9;

        private Camera camera;

        private enum BGType { Skybox, Color, Transparent }
        private BGType bgType = BGType.Skybox;
        private Color bgColor = Color.white;

        private Dictionary<resPreset, Vector2> resBaseSize = new Dictionary<resPreset, Vector2>
        {
            { resPreset.FHD, new Vector2(1920, 1080) },
            { resPreset.QHD, new Vector2(2560, 1440) },
            { resPreset.UHD, new Vector2(3840, 2160) }
        };

        private Vector2 captureSize = new Vector2(1920, 1080);

        private string[] UI_TEXT;
        private string[] BG_TYPE_TEXT;
        private Texture2D windowIcon;
        private Texture2D tabIcon;
        private Font      titleFont;

        // 경로 관리를 위한 상수 추가
        private const string RELATIVE_PATH = "Assets/Di Ne/ScreenSaver/ScreenShot/";

        [MenuItem("DiNe/EX/Screen Saver")]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow<DiNeScreenSaver>("DiNe Screen Saver");
            window.minSize = new Vector2(175, 150);
            window.position = new Rect(window.position.x, window.position.y, 420, 600); // 버튼 추가로 높이 약간 증가
        }

        void OnEnable()
        {
            LoadSettings();
            SetLanguage(language);
            windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
            tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
            titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
            titleContent = new GUIContent("Screen Saver", tabIcon);
        }

        void OnDisable()
        {
            SaveSettings();
        }

        void OnFocus()
        {
            if (camera == null)
            {
                camera = Camera.main;
                if (camera == null)
                {
                    camera = FindObjectOfType<Camera>();
                }
            }
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
            float iconSize = windowIcon != null ? windowIcon.height * 2f / 3f : 48;
            GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
            GUILayout.Space(6);
            GUILayout.Label("Screen Saver", titleStyle, GUILayout.Height(iconSize));
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            string desc = "";
            switch (language)
            {
                case LanguagePreset.Korean: desc = "유니티 에디터 화면 보호기 기능과 바탕화면 위젯 기능을 제공합니다."; break;
                case LanguagePreset.Japanese: desc = "Unityエディターのスクリーンセーバーおよびウィジェット機能を提供します。"; break;
                default: desc = "Provides Unity Editor screen saver and desktop widget features."; break;
            }
            GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel) 
                { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
            int currentLanguageIndex = (int)language;
            string[] languageButtons = { "English", "한국어", "日本語" };
            var prevLangBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            int newLanguageIndex = GUILayout.Toolbar(currentLanguageIndex, languageButtons, GUILayout.Height(30));
            GUI.backgroundColor = prevLangBg;
            if (newLanguageIndex != currentLanguageIndex)
            {
                language = (LanguagePreset)newLanguageIndex;
                SetLanguage(language);
            }
            GUILayout.Space(10);
            
            int currentTargetIndex = (int)captureTarget;
            string[] targetButtons = { UI_TEXT[9], UI_TEXT[10] };
            int newTargetIndex = GUILayout.Toolbar(currentTargetIndex, targetButtons, GUILayout.Height(30));
            if (newTargetIndex != currentTargetIndex)
            {
                captureTarget = (CaptureTarget)newTargetIndex;
            }
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical("box");
            
            if (captureTarget == CaptureTarget.GameView)
            {
                DrawGameViewGUI();
            }
            else
            {
                DrawSceneViewGUI();
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // === 캡처 버튼 영역 ===
            EditorGUILayout.BeginVertical("box");
            
            EditorGUI.BeginDisabledGroup(captureTarget == CaptureTarget.GameView && camera == null);

            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 20;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;

            if (GUILayout.Button(UI_TEXT[11], buttonStyle, GUILayout.Height(45)))
            {
                if (captureTarget == CaptureTarget.GameView)
                {
                    CaptureGameView();
                }
                else
                {
                    CaptureSceneView();
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            EditorGUI.EndDisabledGroup();

            // === [추가됨] 폴더 열기 및 선택 버튼 영역 ===
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(UI_TEXT[12], GUILayout.Height(25))) // 폴더 열기
            {
                OpenSaveFolder();
            }

            if (GUILayout.Button(UI_TEXT[13], GUILayout.Height(25))) // 프로젝트에서 보기
            {
                SelectInProject();
            }

            EditorGUILayout.EndHorizontal();
            // ==========================================

            EditorGUILayout.EndVertical();
        }

        void DrawGameViewGUI()
        {
            camera = (Camera)EditorGUILayout.ObjectField(new GUIContent(UI_TEXT[0]), camera, typeof(Camera), true);

            GuiLine(1, 10);

            DrawResolutionSettings();

            GuiLine(1, 10);

            DrawBackgroundSettings();
        }

        void DrawSceneViewGUI()
        {
            DrawResolutionSettings();
            GuiLine(1, 10);
            DrawBackgroundSettings();
        }

        void DrawResolutionSettings()
        {
            EditorGUILayout.LabelField(UI_TEXT[1], EditorStyles.boldLabel);
            res = (resPreset)EditorGUILayout.EnumPopup(new GUIContent(UI_TEXT[2]), res);

            if (res != resPreset.Custom)
            {
                aspectRatio = (Aspect)EditorGUILayout.EnumPopup(new GUIContent(UI_TEXT[3]), aspectRatio);

                Vector2 baseSize = resBaseSize[res];
                switch (aspectRatio)
                {
                    case Aspect._16_9:
                        captureSize = baseSize;
                        break;
                    case Aspect._9_16:
                        captureSize = new Vector2(baseSize.y, baseSize.x);
                        break;
                    case Aspect._3_4:
                        captureSize = new Vector2(baseSize.y * 3f / 4f, baseSize.y);
                        break;
                    case Aspect._1_1:
                        captureSize = new Vector2(baseSize.y, baseSize.y);
                        break;
                }
                EditorGUILayout.LabelField(UI_TEXT[4], $"{Mathf.Round(captureSize.x)} x {Mathf.Round(captureSize.y)}");
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(UI_TEXT[5]);
                captureSize = EditorGUILayout.Vector2Field("", captureSize);
                EditorGUILayout.EndHorizontal();
            }
        }
        
        void DrawBackgroundSettings()
        {
            EditorGUILayout.LabelField(UI_TEXT[6], EditorStyles.boldLabel);
            
            int bgTypeIndex = (int)bgType;
            bgTypeIndex = EditorGUILayout.Popup(new GUIContent(UI_TEXT[7]), bgTypeIndex, BG_TYPE_TEXT);
            bgType = (BGType)bgTypeIndex;

            if (bgType == BGType.Color)
            {
                bgColor = EditorGUILayout.ColorField(UI_TEXT[8], bgColor);
            }
        }

        void GuiLine(int i_height = 1, int padding = 5)
        {
            GUILayout.Space(padding);
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(padding);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetInt("DiNeScreenSaver_Language", (int)language);
            EditorPrefs.SetInt("DiNeScreenSaver_CaptureTarget", (int)captureTarget);
            EditorPrefs.SetInt("DiNeScreenSaver_ResolutionPreset", (int)res);
            EditorPrefs.SetInt("DiNeScreenSaver_AspectRatio", (int)aspectRatio);
            EditorPrefs.SetInt("DiNeScreenSaver_BackgroundType", (int)bgType);
            EditorPrefs.SetString("DiNeScreenSaver_BackgroundColor", ColorUtility.ToHtmlStringRGBA(bgColor));
            EditorPrefs.SetFloat("DiNeScreenSaver_CaptureSizeX", captureSize.x);
            EditorPrefs.SetFloat("DiNeScreenSaver_CaptureSizeY", captureSize.y);
        }

        private void LoadSettings()
        {
            if (EditorPrefs.HasKey("DiNeScreenSaver_Language"))
            {
                language = (LanguagePreset)EditorPrefs.GetInt("DiNeScreenSaver_Language");
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_CaptureTarget"))
            {
                captureTarget = (CaptureTarget)EditorPrefs.GetInt("DiNeScreenSaver_CaptureTarget");
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_ResolutionPreset"))
            {
                res = (resPreset)EditorPrefs.GetInt("DiNeScreenSaver_ResolutionPreset");
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_AspectRatio"))
            {
                aspectRatio = (Aspect)EditorPrefs.GetInt("DiNeScreenSaver_AspectRatio");
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_BackgroundType"))
            {
                bgType = (BGType)EditorPrefs.GetInt("DiNeScreenSaver_BackgroundType");
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_BackgroundColor"))
            {
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("DiNeScreenSaver_BackgroundColor"), out bgColor);
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_CaptureSizeX"))
            {
                captureSize.x = EditorPrefs.GetFloat("DiNeScreenSaver_CaptureSizeX");
            }
            if (EditorPrefs.HasKey("DiNeScreenSaver_CaptureSizeY"))
            {
                captureSize.y = EditorPrefs.GetFloat("DiNeScreenSaver_CaptureSizeY");
            }
        }

        private void SetLanguage(LanguagePreset lang)
        {
            // 배열 크기를 늘려서 12, 13번 인덱스(버튼 텍스트)를 추가했습니다.
            switch (lang)
            {
                case LanguagePreset.Korean:
                    UI_TEXT = new string[] { "대상 카메라", "해상도 설정", "프리셋", "비율", "현재 크기", "커스텀 크기", "배경 설정", "유형", "색상", "게임 뷰", "씬 뷰", "캡처", "저장 폴더 열기", "프로젝트 위치" };
                    BG_TYPE_TEXT = new string[] { "스카이박스", "색상", "투명" };
                    break;
                case LanguagePreset.Japanese:
                    UI_TEXT = new string[] { "対象カメラ", "解像度設定", "プリセット", "アスペクト比", "現在のサイズ", "カスタムサイズ", "背景設定", "タイプ", "色", "ゲームビュー", "シーンビュー", "キャプチャ", "保存フォルダを開く", "プロジェクト位置" };
                    BG_TYPE_TEXT = new string[] { "スカイボックス", "色", "透明" };
                    break;
                case LanguagePreset.English:
                default:
                    UI_TEXT = new string[] { "Target Camera", "Resolution Settings", "Preset", "Aspect Ratio", "Current Size", "Custom Size", "Background Settings", "Type", "Color", "Game View", "Scene View", "Capture", "Open Folder", "Show in Project" };
                    BG_TYPE_TEXT = new string[] { "Skybox", "Color", "Transparent" };
                    break;
            }
        }

        // [추가됨] 저장 폴더 경로 가져오기 및 생성
        private string GetSafePath()
        {
            string path = Application.dataPath + "/Di Ne/ScreenSaver/ScreenShot/";
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        // [추가됨] 파일 탐색기로 열기
        private void OpenSaveFolder()
        {
            string path = GetSafePath();
            EditorUtility.RevealInFinder(path);
        }

        // [추가됨] 프로젝트 뷰에서 선택하기
        private void SelectInProject()
        {
            // 폴더가 없으면 생성
            GetSafePath(); 
            AssetDatabase.Refresh();

            string projectPath = RELATIVE_PATH.TrimEnd('/'); // 끝의 슬래시 제거
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(projectPath);
            
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                Debug.LogWarning("Folder not found in Project view: " + projectPath);
            }
        }

        private void CaptureGameView()
        {
            Camera captureCamera = Instantiate<Camera>(camera);
            int captureWidth = (int)Mathf.Round(captureSize.x);
            int captureHeight = (int)Mathf.Round(captureSize.y);
            
            // 경로 로직을 공통 함수 사용으로 변경할 수도 있지만, 기존 코드를 최대한 유지했습니다.
            string path = Application.dataPath + "/Di Ne/ScreenSaver/ScreenShot/";
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                Directory.CreateDirectory(path);
            }
            string name = path + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss (" + captureSize.x + "x" + captureSize.y + ")") + ".png";
            TextureFormat format = (bgType == BGType.Transparent) ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            Texture2D screenShot = new Texture2D(captureWidth, captureHeight, format, false);
            RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);
            RenderTexture.active = rt;
            captureCamera.targetTexture = rt;
            if (bgType == BGType.Skybox)
            {
                captureCamera.clearFlags = CameraClearFlags.Skybox;
                captureCamera.backgroundColor = Color.white;
            }
            else if (bgType == BGType.Color)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = bgColor;
            }
            else if (bgType == BGType.Transparent)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = Color.clear;
            }
            captureCamera.Render();
            screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenShot.Apply();
            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(name, bytes);
            AssetDatabase.Refresh();
            DestroyImmediate(captureCamera.gameObject);
        }
        
        private void CaptureSceneView()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogError("Scene View is not active. Cannot capture.");
                return;
            }
            
            RenderTexture originalRT = RenderTexture.active;
            
            RenderTexture tempRT = new RenderTexture((int)captureSize.x, (int)captureSize.y, 24);
            sceneView.camera.targetTexture = tempRT;
            
            CameraClearFlags originalClearFlags = sceneView.camera.clearFlags;
            Color originalBGColor = sceneView.camera.backgroundColor;
            
            if (bgType == BGType.Skybox)
            {
                sceneView.camera.clearFlags = CameraClearFlags.Skybox;
                sceneView.camera.backgroundColor = Color.white;
            }
            else if (bgType == BGType.Color)
            {
                sceneView.camera.clearFlags = CameraClearFlags.SolidColor;
                sceneView.camera.backgroundColor = bgColor;
            }
            else if (bgType == BGType.Transparent)
            {
                sceneView.camera.clearFlags = CameraClearFlags.SolidColor;
                sceneView.camera.backgroundColor = Color.clear;
            }
            
            sceneView.Repaint();
            sceneView.camera.Render();
            
            RenderTexture.active = tempRT;
            
            Texture2D screenShot = new Texture2D(tempRT.width, tempRT.height, TextureFormat.RGB24, false);
            if (bgType == BGType.Transparent)
            {
                screenShot = new Texture2D(tempRT.width, tempRT.height, TextureFormat.ARGB32, false);
            }
            
            screenShot.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            screenShot.Apply();
            
            sceneView.camera.targetTexture = null;
            sceneView.camera.clearFlags = originalClearFlags;
            sceneView.camera.backgroundColor = originalBGColor;
            RenderTexture.active = originalRT;
            
            byte[] bytes = screenShot.EncodeToPNG();
            
            string path = Application.dataPath + "/Di Ne/ScreenSaver/ScreenShot/";
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                Directory.CreateDirectory(path);
            }
            string name = path + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss (" + captureSize.x + "x" + captureSize.y + ")") + ".png";
            
            File.WriteAllBytes(name, bytes);
            
            AssetDatabase.Refresh();
            DestroyImmediate(screenShot);
            DestroyImmediate(tempRT);
        }
    }
}