using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlackStartX.GestureManager;
using BlackStartX.GestureManager.Data;
using BlackStartX.GestureManager.Editor.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase;

namespace DiNeTool.GestureManager
{
    [CustomEditor(typeof(DiNeGestureManager))]
    public class DiNeGestureManagerEditor : UnityEditor.Editor
    {
        // ─── Targets ──────────────────────────────────────────────────────────────
        private DiNeGestureManager Target => target as DiNeGestureManager;
        private BlackStartX.GestureManager.GestureManager Manager => Target?.GestureManager;

        // ─── UI State ─────────────────────────────────────────────────────────────
        private VisualElement _root;
        private bool _showStats;
        private DiNeAvatarStats.StatsData _stats;
        private bool _statsDirty = true;
        private Texture2D _icon;

        // ─── Language ─────────────────────────────────────────────────────────────
        private enum Language { Korean, English, Japanese }
        private Language CurrentLang
        {
            get => (Language)EditorPrefs.GetInt("DiNeLang", 0);
            set => EditorPrefs.SetInt("DiNeLang", (int)value);
        }

        // ─── Colors ───────────────────────────────────────────────────────────────
        private static readonly Color ColHeader  = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color ColSection = new Color(0.18f, 0.18f, 0.21f);
        private static readonly Color ColAccent  = new Color(0.35f, 0.65f, 1.00f);
        private static readonly Color ColGreen   = new Color(0.28f, 0.62f, 0.32f);
        private static readonly Color ColRed     = new Color(0.62f, 0.22f, 0.22f);

        // ─── CreateInspectorGUI ───────────────────────────────────────────────────
        public override VisualElement CreateInspectorGUI()
        {
            _icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
            _root = new VisualElement();
            _root.Add(new IMGUIContainer(DrawInspector));
            if (Application.isPlaying) TryAutoInitialize();
            return _root;
        }

        // ─── Auto Initialize ──────────────────────────────────────────────────────
        private void TryAutoInitialize()
        {
            var gm = Manager;
            if (gm == null || !gm.enabled || !gm.gameObject.activeInHierarchy || gm.Module != null) return;
            gm.StartCoroutine(AutoInitializeRoutine(gm));
        }

        private IEnumerator AutoInitializeRoutine(BlackStartX.GestureManager.GestureManager gm)
        {
            yield return null;
            // favourite 아바타 먼저 시도
            ModuleBase module = null;
            if (gm.settings?.favourite != null)
                module = ModuleHelper.GetModuleFor(gm.settings.favourite);
            module ??= GetBestModule();
            if (module != null) gm.SetModule(module);
        }

        private static ModuleBase GetBestModule() =>
            RefreshModuleList().FirstOrDefault(m => m.IsPerfectDesc());

        private static List<ModuleBase> RefreshModuleList() =>
            BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules =
                FindAllDescriptorObjects()
                    .Select(go => ModuleHelper.GetModuleFor(go))
                    .Where(m => m != null)
                    .ToList();

        private static IEnumerable<GameObject> FindAllDescriptorObjects() =>
            Resources.FindObjectsOfTypeAll<VRC_AvatarDescriptor>()
                .Where(d => d.hideFlags != HideFlags.NotEditable &&
                            d.hideFlags != HideFlags.HideAndDontSave &&
                            d.gameObject.scene.name != null)
                .Select(d => d.gameObject);

        // ─── Main Draw ────────────────────────────────────────────────────────────
        private void DrawInspector()
        {
            if (Manager == null) return;

            DrawHeader();
            DrawLangBar();
            GUILayout.Space(4);

            if (Manager.Module != null)
                DrawActiveModuleUI();
            else
                DrawSetupUI();

            DrawStatsToggle();
            if (_showStats) DrawStatsPanel();

            GUILayout.Space(4);
        }

        // ─── Header ───────────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = ColHeader;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var titleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize  = 22,
                normal    = { textColor = new Color(0.88f, 0.88f, 0.92f) }
            };

            var title = _icon != null
                ? new GUIContent("  DiNe Gesture", _icon)
                : new GUIContent("DiNe Gesture");

            GUILayout.Label(title, titleStyle, GUILayout.Height(38));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            var lineRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(lineRect, ColAccent);
            GUILayout.Space(3);

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = prevBg;
        }

        // ─── Language Bar ─────────────────────────────────────────────────────────
        private void DrawLangBar()
        {
            int idx = (int)CurrentLang;
            idx = GUILayout.Toolbar(idx, new[] { "한국어", "English", "日本語" }, GUILayout.Height(24));
            CurrentLang = (Language)idx;
        }

        // ─── Active Module UI ─────────────────────────────────────────────────────
        private void DrawActiveModuleUI()
        {
            // Avatar info bar
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = ColSection;
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = prevBg;

            var avatarLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.85f, 0.95f, 0.7f) } };

            GUILayout.Label("Avatar:", GUILayout.Width(52));
            GUILayout.Label(Manager.Module.Avatar?.name ?? "—", avatarLabelStyle);
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = ColRed;
            if (GUILayout.Button("✕ 해제", GUILayout.Width(60), GUILayout.Height(20)))
            {
                Manager.UnlinkModule();
                _statsDirty = true;
            }
            GUI.backgroundColor = prevBg;

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Delegate all gesture/radial/tools/debug content to the original module
            Manager.Module.EditorHeader();
            Manager.Module.EditorContent(this, _root);
        }

        // ─── Setup UI (no module active) ──────────────────────────────────────────
        private void DrawSetupUI()
        {
            bool isPlaying = EditorApplication.isPlaying;
            bool isEnabled = Manager.enabled && Manager.gameObject.activeInHierarchy;

            GUILayout.Space(6);

            // Status message
            if (!isEnabled)
            {
                DrawStatusLabel("컴포넌트가 비활성화 상태입니다", new Color(1f, 0.4f, 0.4f));
            }
            else if (!isPlaying)
            {
                DrawStatusLabel("플레이 모드에서 아바타를 제어할 수 있습니다", new Color(0.8f, 0.8f, 0.5f));
            }

            GUILayout.Space(8);

            // Play mode button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = isPlaying ? ColRed : ColGreen;
            if (GUILayout.Button(
                isPlaying ? "■  플레이 모드 종료" : "▶  플레이 모드 시작",
                GUILayout.Width(190), GUILayout.Height(34)))
            {
                if (isPlaying) EditorApplication.ExitPlaymode();
                else           EditorApplication.EnterPlaymode();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (!isPlaying) return;

            // Avatar list
            if (BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules.Count == 0)
                RefreshModuleList();

            var eligible    = BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules
                                .Where(m => m.IsValidDesc()).ToList();
            var nonEligible = BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules
                                .Where(m => !m.IsValidDesc()).ToList();

            if (BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules.Count == 0)
            {
                EditorGUILayout.HelpBox("씬에 VRCAvatarDescriptor가 없습니다.", MessageType.Warning);
            }
            else
            {
                if (eligible.Count > 0)
                {
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = ColSection;
                    EditorGUILayout.BeginVertical("box");
                    GUI.backgroundColor = prevBg;

                    GUILayout.Label(CurrentLang == Language.Korean ? "아바타 선택" :
                                    CurrentLang == Language.English ? "Select Avatar" : "アバター選択",
                        new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColAccent } });
                    GUILayout.Space(4);

                    foreach (var module in eligible)
                    {
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        GUILayout.Label(module.Name, GUILayout.ExpandWidth(true));

                        foreach (var warn in module.GetWarnings())
                            GUILayout.Label(warn, EditorStyles.miniLabel);

                        GUI.backgroundColor = ColGreen;
                        if (GUILayout.Button(CurrentLang == Language.Korean ? "설정" :
                                             CurrentLang == Language.English ? "Select" : "設定",
                                GUILayout.Width(56), GUILayout.Height(20)))
                        {
                            Manager.SetModule(module);
                            _statsDirty = true;
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndVertical();
                }

                if (nonEligible.Count > 0)
                {
                    GUILayout.Space(4);
                    GUILayout.Label(CurrentLang == Language.Korean ? "비적합 아바타" :
                                    CurrentLang == Language.English ? "Non-Eligible" : "非適格アバター",
                        EditorStyles.boldLabel);
                    foreach (var module in nonEligible)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        GUILayout.Label(module.Name, EditorStyles.boldLabel);
                        foreach (var err in module.GetErrors())
                            EditorGUILayout.HelpBox(err, MessageType.Error);
                        EditorGUILayout.EndVertical();
                    }
                }
            }

            GUILayout.Space(6);
            var prevBg2 = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.45f, 0.6f);
            if (GUILayout.Button(CurrentLang == Language.Korean ? "다시 확인" :
                                  CurrentLang == Language.English ? "Refresh" : "更新",
                    GUILayout.Height(24)))
                RefreshModuleList();
            GUI.backgroundColor = prevBg2;
        }

        // ─── Stats Toggle Button ───────────────────────────────────────────────────
        private void DrawStatsToggle()
        {
            GUILayout.Space(8);
            Separator();

            bool hasAvatar = Manager?.Module?.Avatar != null;
            GUI.enabled = hasAvatar;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _showStats ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.28f);

            string label = CurrentLang == Language.Korean ? "아바타 성능 정보" :
                           CurrentLang == Language.English ? "Avatar Performance Info" : "アバターパフォーマンス";

            if (GUILayout.Button($"{(_showStats ? "▼" : "▶")}  {label}", GUILayout.Height(26)))
            {
                _showStats = !_showStats;
                if (_showStats) _statsDirty = true;
            }
            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }

        // ─── Stats Panel ──────────────────────────────────────────────────────────
        private void DrawStatsPanel()
        {
            if (_statsDirty && Manager?.Module?.Avatar != null)
            {
                _stats      = DiNeAvatarStats.Calculate(Manager.Module.Avatar);
                _statsDirty = false;
            }

            GUILayout.Space(4);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = ColSection;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;

            // Rank row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("퍼포먼스", GUILayout.Width(120));
            GUILayout.Label(_stats.PerformanceRank, new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = _stats.RankColor }, fontSize = 13 });
            EditorGUILayout.EndHorizontal();

            Separator();

            StatRow("트라이앵글",    _stats.TriangleCount.ToString("N0"), _stats.TriColor);
            StatRow("버텍스",        _stats.VertexCount.ToString("N0"),   Color.white);
            StatRow("메쉬",          _stats.MeshCount.ToString(),         Color.white);
            StatRow("본",            _stats.BoneCount.ToString(),         Color.white);

            Separator();

            StatRow("메테리얼",      _stats.MaterialCount.ToString(),     Color.white);
            StatRow("텍스쳐",        _stats.TextureCount.ToString(),      Color.white);
            StatRow("VRAM",          FormatBytes(_stats.VRAMBytes),        _stats.VRAMColor);
            StatRow("업로드 예상",   FormatBytes(_stats.UploadSizeBytes),  new Color(0.75f, 0.75f, 0.75f));

            GUILayout.Space(4);
            GUILayout.Label("※ 실제 업로드 크기와 다를 수 있습니다",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } });

            GUILayout.Space(4);
            GUI.backgroundColor = new Color(0.35f, 0.45f, 0.6f);
            if (GUILayout.Button("새로고침", GUILayout.Height(22))) _statsDirty = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        // ─── Helper Draws ─────────────────────────────────────────────────────────
        private static void DrawStatusLabel(string text, Color color)
        {
            GUILayout.Label(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { normal = { textColor = color }, fontStyle = FontStyle.Italic });
        }

        private static void StatRow(string label, string value, Color valueColor)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            GUILayout.Label(value, new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = valueColor } });
            EditorGUILayout.EndHorizontal();
        }

        private static void Separator()
        {
            GUILayout.Space(3);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.4f, 0.6f));
            GUILayout.Space(3);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
            if (bytes >= 1024L * 1024)        return $"{bytes / (1024f * 1024f):F2} MB";
            if (bytes >= 1024L)               return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }

        // ─── Menu Item ────────────────────────────────────────────────────────────
        [MenuItem("DiNe/Avatar/Gesture Manager")]
        public static void CreateDiNeGestureManager()
        {
            // 씬에 이미 있으면 선택만
            var existing = FindObjectOfType<DiNeGestureManager>();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var go = new GameObject("DiNe Gesture Manager");
            go.AddComponent<DiNeGestureManager>(); // GestureManager 자동 추가됨 (RequireComponent)
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}
