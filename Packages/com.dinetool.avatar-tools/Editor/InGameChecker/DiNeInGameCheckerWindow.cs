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

namespace DiNeTool.InGameChecker
{
    public class DiNeInGameCheckerWindow : EditorWindow
    {
        // ─── Language ────────────────────────────────────────────────────────────
        private enum Language { English, Korean, Japanese }
        private Language CurrentLang
        {
            get => (Language)EditorPrefs.GetInt("DiNeCheckerLang", 0);
            set => EditorPrefs.SetInt("DiNeCheckerLang", (int)value);
        }
        private int L => (int)CurrentLang;

        private static readonly string[][] UI_TEXT =
        {
            /* 00 */ new[] { "Avatar can be controlled in Play Mode",          "플레이 모드에서 아바타를 제어할 수 있습니다",    "プレイモードでアバターを操作できます"             },
            /* 01 */ new[] { "▶   Enter Play Mode",                            "▶   플레이 모드 시작",                          "▶   プレイモード開始"                             },
            /* 02 */ new[] { "■   Exit Play Mode",                             "■   플레이 모드 종료",                          "■   プレイモード終了"                             },
            /* 03 */ new[] { "Select Avatar",                                  "아바타 선택",                                    "アバター選択"                                     },
            /* 04 */ new[] { "Select",                                         "선택",                                          "選択"                                             },
            /* 05 */ new[] { "Non-Eligible",                                   "비적합 아바타",                                  "非適格アバター"                                   },
            /* 06 */ new[] { "Refresh",                                        "새로고침",                                      "更新"                                             },
            /* 07 */ new[] { "No VRCAvatarDescriptor in scene",                "씬에 VRCAvatarDescriptor가 없습니다",            "シーンにVRCAvatarDescriptorがありません"           },
            /* 08 */ new[] { "✕  Unlink",                                      "✕  해제",                                       "✕  解除"                                          },
            /* 09 */ new[] { "Avatar Performance Info",                        "아바타 성능 정보",                               "アバターパフォーマンス情報"                       },
            /* 10 */ new[] { "Performance",                                    "퍼포먼스",                                      "パフォーマンス"                                   },
            /* 11 */ new[] { "Triangles",                                      "트라이앵글",                                    "トライアングル"                                   },
            /* 12 */ new[] { "Vertices",                                       "버텍스",                                        "バーテックス"                                     },
            /* 13 */ new[] { "Meshes",                                         "메쉬",                                          "メッシュ"                                         },
            /* 14 */ new[] { "Bones",                                          "본",                                            "ボーン"                                           },
            /* 15 */ new[] { "Materials",                                      "머티리얼",                                      "マテリアル"                                       },
            /* 16 */ new[] { "Textures",                                       "텍스쳐",                                        "テクスチャー"                                     },
            /* 17 */ new[] { "VRAM",                                           "VRAM",                                          "VRAM"                                             },
            /* 18 */ new[] { "Est. Upload",                                    "업로드 예상",                                   "推定アップロード"                                 },
            /* 19 */ new[] { "Refresh",                                        "새로고침",                                      "更新"                                             },
            /* 20 */ new[] { "※ May differ from actual upload size",           "※ 실제 업로드 크기와 다를 수 있습니다",         "※ 実際のアップロードサイズと異なる場合があります" },
            /* 21 */ new[] { "Component is disabled",                          "컴포넌트가 비활성화 상태입니다",                 "コンポーネントが無効です"                         },
            /* 22 */ new[] { "Avatar",                                         "아바타",                                        "アバター"                                         },
            /* 23 */ new[] { "Left Hand",                                      "왼손",                                          "左手"                                             },
            /* 24 */ new[] { "Right Hand",                                     "오른손",                                        "右手"                                             },
            /* 25 */ new[] { "Fist",                                           "주먹",                                          "グー"                                             },
            /* 26 */ new[] { "Open",                                           "펼치기",                                        "パー"                                             },
            /* 27 */ new[] { "FingerPoint",                                    "검지",                                          "指差し"                                           },
            /* 28 */ new[] { "Victory",                                        "브이",                                          "ピース"                                           },
            /* 29 */ new[] { "Rock&Roll",                                      "락앤롤",                                        "ロック"                                           },
            /* 30 */ new[] { "Gun",                                            "핑거건",                                        "ピストル"                                         },
            /* 31 */ new[] { "ThumbsUp",                                       "엄지척",                                        "サムズアップ"                                     },
        };
        private string T(int i) => UI_TEXT[i][L];

        // ─── Colors ───────────────────────────────────────────────────────────────
        private static readonly Color ColBg      = new Color(0.17f, 0.17f, 0.19f);
        private static readonly Color ColCard    = new Color(0.21f, 0.21f, 0.24f);
        private static readonly Color ColAccent  = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColGreen   = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColRed     = new Color(0.60f, 0.25f, 0.25f);
        private static readonly Color ColBlue    = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColText    = new Color(0.88f, 0.88f, 0.92f);
        private static readonly Color ColSubText = new Color(0.58f, 0.58f, 0.63f);
        private static readonly Color ColLine    = new Color(0.30f, 0.30f, 0.35f, 0.8f);

        // ─── State ────────────────────────────────────────────────────────────────
        private Texture2D   _icon;        // 탭 아이콘 (마스코트)
        private Texture2D   _headerIcon;  // 헤더 로고 (DiNe.png)
        private Font        _titleFont;
        private Vector2     _scroll;
        private bool        _showStats;
        private bool        _statsDirty = true;
        private DiNeAvatarStats.StatsData _stats;

        // GestureManager references
        private DiNeInGameChecker                    _wrapper;
        private BlackStartX.GestureManager.GestureManager _gm;
        private Editor                                _gmEditor; // for Module.EditorContent()

        // ─── Entry ───────────────────────────────────────────────────────────────
        [MenuItem("DiNe/Avatar/In-Game Checker")]
        public static void ShowWindow()
        {
            var w = GetWindow<DiNeInGameCheckerWindow>("DiNe In-Game Checker");
            w.minSize = new Vector2(340, 480);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _icon       = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
            _headerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
            _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
            titleContent = new GUIContent("In-Game Checker", _headerIcon);

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyChanged     += OnHierarchyChanged;

            FindOrCreateManager();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.hierarchyChanged     -= OnHierarchyChanged;
            DestroyGMEditor();
        }

        private void OnDestroy()
        {
            // 윈도우 닫을 때 에디트 모드면 씬 오브젝트 정리
            if (!Application.isPlaying && _wrapper != null)
                DestroyImmediate(_wrapper.gameObject);
        }

        private void Update()
        {
            // 모듈이 활성화 중일 때 Repaint — 파라미터 실시간 반영
            if (_gm?.Module != null && Application.isPlaying)
                Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                FindOrCreateManager();
                EditorApplication.delayCall += TryAutoInit;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _statsDirty = true;
                DestroyGMEditor();
            }
            Repaint();
        }

        private void OnHierarchyChanged()
        {
            FindOrCreateManager();
            Repaint();
        }

        // ─── Manager Lifecycle ────────────────────────────────────────────────────
        private void FindOrCreateManager()
        {
            _wrapper = FindObjectOfType<DiNeInGameChecker>();

            if (_wrapper == null)
            {
                var go = new GameObject("DiNe InGame Checker") { hideFlags = HideFlags.HideInHierarchy };
                _wrapper = go.AddComponent<DiNeInGameChecker>();
            }

            _gm = _wrapper.Core;
            RefreshGMEditor();
        }

        private void RefreshGMEditor()
        {
            DestroyGMEditor();
            if (_gm != null)
                _gmEditor = Editor.CreateEditor(_gm);
        }

        private void DestroyGMEditor()
        {
            if (_gmEditor != null) { DestroyImmediate(_gmEditor); _gmEditor = null; }
        }

        private void TryAutoInit()
        {
            if (_gm == null || _gm.Module != null) return;
            _gm.StartCoroutine(AutoInitRoutine());
        }

        private IEnumerator AutoInitRoutine()
        {
            yield return null;
            ModuleBase module = null;
            if (_gm.settings?.favourite != null)
                module = ModuleHelper.GetModuleFor(_gm.settings.favourite);
            module ??= RefreshModuleList().FirstOrDefault(m => m.IsPerfectDesc());
            if (module != null)
            {
                _gm.SetModule(module);
                _statsDirty = true;
            }
        }

        private static List<ModuleBase> RefreshModuleList() =>
            BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules =
                Resources.FindObjectsOfTypeAll<VRC_AvatarDescriptor>()
                    .Where(d => d.hideFlags != HideFlags.NotEditable &&
                                d.hideFlags != HideFlags.HideAndDontSave &&
                                d.gameObject.scene.name != null)
                    .Select(d => ModuleHelper.GetModuleFor(d.gameObject))
                    .Where(m => m != null)
                    .ToList();

        // ═════════════════════════════════════════════════════════════════════════
        // OnGUI
        // ═════════════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            DrawHeader();
            DrawLangBar();
            HLine();

            if (_gm != null && _gm.Module != null)
            {
                // 모듈 UI는 절대 좌표를 사용하므로 ScrollView 밖에서 렌더링
                DrawActiveModule();

                HLine();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawStatsSection();
                GUILayout.Space(10);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawSetup();
                HLine();
                DrawStatsSection();
                GUILayout.Space(10);
                EditorGUILayout.EndScrollView();
            }
        }

        // ─── Header ───────────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var titleStyle = new GUIStyle(EditorStyles.label)
            {
                font      = _titleFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize  = 36,
                normal    = { textColor = Color.white }
            };
            float iconSize = _icon != null ? _icon.height * 2f / 3f : 48;
            GUILayout.Label(_icon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
            GUILayout.Space(6);
            GUILayout.Label("In-Game Checker", titleStyle, GUILayout.Height(iconSize));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            string desc = "";
            switch (CurrentLang)
            {
                case Language.Korean: desc = "인게임에서 시점과 포즈가 어떻게 보이는지 에디터 환경에서 미리 검증합니다."; break;
                case Language.Japanese: desc = "ゲーム内での視点やポーズがどう見えるかをエディター上で事前確認します。"; break;
                default: desc = "Verify how viewports and poses will look in-game directly within the Editor."; break;
            }
            GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel) 
                { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        // ─── Language Bar ─────────────────────────────────────────────────────────
        private void DrawLangBar()
        {
            int idx = L;
            idx = DrawCustomToolbar(idx, new[] { "English", "한국어", "日本語" }, 26);
            CurrentLang = (Language)idx;
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

        // ═════════════════════════════════════════════════════════════════════════
        // SETUP
        // ═════════════════════════════════════════════════════════════════════════
        private void DrawSetup()
        {
            bool isPlaying = EditorApplication.isPlaying;

            GUILayout.Space(12);

            // 상태 힌트
            if (!isPlaying)
                DrawCenteredHint(T(0), ColSubText);

            GUILayout.Space(10);

            // Play / Stop 버튼
            DrawCenteredButton(
                isPlaying ? T(2) : T(1),
                isPlaying ? ColRed : ColGreen,
                200, 36,
                () => { if (isPlaying) EditorApplication.ExitPlaymode(); else EditorApplication.EnterPlaymode(); });

            if (!isPlaying) { GUILayout.Space(8); return; }

            GUILayout.Space(14);
            HLine();
            GUILayout.Space(6);

            // 아바타 목록
            if (BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules.Count == 0)
                RefreshModuleList();

            var modules     = BlackStartX.GestureManager.GestureManager.LastCheckedActiveModules;
            var eligible    = modules.Where(m => m.IsValidDesc()).ToList();
            var nonEligible = modules.Where(m => !m.IsValidDesc()).ToList();

            if (modules.Count == 0)
            {
                DrawCenteredHint(T(7), new Color(1f, 0.65f, 0.3f));
            }
            else
            {
                if (eligible.Count > 0)
                {
                    SectionLabel(T(3));
                    GUILayout.Space(4);

                    foreach (var module in eligible)
                    {
                        using (new BgColor(ColCard))
                        {
                            EditorGUILayout.BeginVertical("box");
                            EditorGUILayout.BeginHorizontal();

                            GUILayout.Label(module.Name, new GUIStyle(EditorStyles.boldLabel)
                                { fontSize = 12, normal = { textColor = new Color(0.80f, 0.95f, 0.70f) } },
                                GUILayout.ExpandWidth(true));

                            using (new BgColor(ColGreen))
                            {
                                if (GUILayout.Button(T(4), GUILayout.Width(60), GUILayout.Height(22)))
                                {
                                    _gm.SetModule(module);
                                    _statsDirty = true;
                                }
                            }

                            EditorGUILayout.EndHorizontal();
                            foreach (var w in module.GetWarnings())
                                GUILayout.Label(w, new GUIStyle(EditorStyles.miniLabel)
                                    { normal = { textColor = ColSubText } });
                            EditorGUILayout.EndVertical();
                        }
                        GUILayout.Space(2);
                    }
                }

                if (nonEligible.Count > 0)
                {
                    GUILayout.Space(6);
                    SectionLabel(T(5));
                    GUILayout.Space(4);

                    foreach (var module in nonEligible)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        GUILayout.Label(module.Name, EditorStyles.boldLabel);
                        foreach (var err in module.GetErrors())
                            EditorGUILayout.HelpBox(err, MessageType.Error);
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(2);
                    }
                }
            }

            GUILayout.Space(8);
            using (new BgColor(ColBlue))
                if (GUILayout.Button(T(6), GUILayout.Height(26)))
                    RefreshModuleList();
        }

        // ═════════════════════════════════════════════════════════════════════════
        // ACTIVE MODULE
        // ═════════════════════════════════════════════════════════════════════════
        // 제스처 이름 목록 (UI_TEXT 25~31에 대응)
        private static readonly string[] GestureNames = { "Fist", "Open", "FingerPoint", "Victory", "Rock&Roll", "Gun", "ThumbsUp" };

        private void DrawActiveModule()
        {
            GUILayout.Space(4);

            // 아바타 정보 바
            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(T(22) + ":", new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = ColSubText } }, GUILayout.Width(52));
                GUILayout.Label(_gm.Module.Avatar?.name ?? "—", new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 12, normal = { textColor = new Color(0.80f, 0.95f, 0.70f) } },
                    GUILayout.ExpandWidth(true));

                using (new BgColor(ColAccent))
                {
                    if (GUILayout.Button(T(8), GUILayout.Width(70), GUILayout.Height(20)))
                    {
                        _gm.UnlinkModule();
                        _statsDirty = true;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(4);

            // ─── 제스처 리스트 (번역 + 테두리) ───
            DrawGestureList();

            GUILayout.Space(4);

            // 원본 모듈에 UI 위임 (라디알 메뉴, 툴, 디버그 등)
            if (_gmEditor != null)
            {
                _gm.Module.EditorHeader();
                _gm.Module.EditorContent(_gmEditor, rootVisualElement);
            }
        }

        private void DrawGestureList()
        {
            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginVertical("box");

                // 헤더
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(23), new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = ColAccent } },
                    GUILayout.ExpandWidth(true));
                GUILayout.Label(T(24), new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = ColAccent } },
                    GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(2);

                // 제스처 항목
                for (int i = 0; i < GestureNames.Length; i++)
                {
                    string translated = T(25 + i);
                    string original   = GestureNames[i];
                    string label      = (L == 0) ? original : $"{translated}  ({original})";

                    EditorGUILayout.BeginHorizontal();

                    // 왼손
                    using (new BgColor(new Color(0.25f, 0.25f, 0.28f)))
                    {
                        EditorGUILayout.BeginVertical("box");
                        GUILayout.Label(label, new GUIStyle(EditorStyles.label)
                            { alignment = TextAnchor.MiddleCenter, fontSize = 11,
                              normal = { textColor = ColText } },
                            GUILayout.Height(20), GUILayout.ExpandWidth(true));
                        EditorGUILayout.EndVertical();
                    }

                    GUILayout.Space(2);

                    // 오른손
                    using (new BgColor(new Color(0.25f, 0.25f, 0.28f)))
                    {
                        EditorGUILayout.BeginVertical("box");
                        GUILayout.Label(label, new GUIStyle(EditorStyles.label)
                            { alignment = TextAnchor.MiddleCenter, fontSize = 11,
                              normal = { textColor = ColText } },
                            GUILayout.Height(20), GUILayout.ExpandWidth(true));
                        EditorGUILayout.EndVertical();
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // STATS
        // ═════════════════════════════════════════════════════════════════════════
        private void DrawStatsSection()
        {
            bool hasAvatar = _gm?.Module?.Avatar != null;
            GUI.enabled = hasAvatar;

            using (new BgColor(_showStats ? ColAccent : ColCard))
            {
                if (GUILayout.Button((_showStats ? "▼  " : "▶  ") + T(9), GUILayout.Height(28)))
                {
                    _showStats = !_showStats;
                    if (_showStats) _statsDirty = true;
                }
            }
            GUI.enabled = true;

            if (!_showStats) return;

            if (_statsDirty && _gm?.Module?.Avatar != null)
            {
                _stats      = DiNeAvatarStats.Calculate(_gm.Module.Avatar);
                _statsDirty = false;
            }

            GUILayout.Space(4);
            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginVertical("box");

                // 퍼포먼스 랭크
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(10), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = ColAccent } }, GUILayout.Width(110));
                GUILayout.Label(_stats.PerformanceRank, new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 13, normal = { textColor = _stats.RankColor } });
                EditorGUILayout.EndHorizontal();

                HLine();

                // 2열 그리드
                DrawStatGrid(new[]
                {
                    (T(11), _stats.TriangleCount.ToString("N0"), _stats.TriColor),
                    (T(12), _stats.VertexCount.ToString("N0"),   ColText),
                    (T(13), _stats.MeshCount.ToString(),         ColText),
                    (T(14), _stats.BoneCount.ToString(),         ColText),
                });
                HLine();
                DrawStatGrid(new[]
                {
                    (T(15), _stats.MaterialCount.ToString(),    ColText),
                    (T(16), _stats.TextureCount.ToString(),     ColText),
                    (T(17), FormatBytes(_stats.VRAMBytes),      _stats.VRAMColor),
                    (T(18), FormatBytes(_stats.UploadSizeBytes), ColSubText),
                });

                GUILayout.Space(4);
                GUILayout.Label(T(20), new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = ColSubText }, fontSize = 9 });
                GUILayout.Space(4);

                using (new BgColor(ColBlue))
                    if (GUILayout.Button(T(19), GUILayout.Height(24)))
                        _statsDirty = true;

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStatGrid((string label, string value, Color col)[] rows)
        {
            for (int i = 0; i < rows.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawStatCell(rows[i].label, rows[i].value, rows[i].col);
                if (i + 1 < rows.Length)
                    DrawStatCell(rows[i + 1].label, rows[i + 1].value, rows[i + 1].col);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawStatCell(string label, string value, Color col)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label(label, new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = ColSubText } });
            GUILayout.Label(value, new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = col } });
            EditorGUILayout.EndVertical();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────
        private void SectionLabel(string text) =>
            GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 11, normal = { textColor = ColAccent } });

        private static void DrawCenteredHint(string text, Color color)
        {
            GUILayout.Label(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { fontStyle = FontStyle.Italic, fontSize = 11, wordWrap = true,
                  normal = { textColor = color } });
        }

        private static void DrawCenteredButton(string label, Color color, int width, int height, System.Action onClick)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new BgColor(color))
                if (GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(height)))
                    onClick?.Invoke();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void HLine()
        {
            GUILayout.Space(4);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, ColLine);
            GUILayout.Space(4);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
            if (bytes >= 1024L * 1024)        return $"{bytes / (1024f * 1024f):F2} MB";
            if (bytes >= 1024L)               return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }

        private readonly struct BgColor : System.IDisposable
        {
            private readonly Color _prev;
            public BgColor(Color c) { _prev = GUI.backgroundColor; GUI.backgroundColor = c; }
            public void Dispose() => GUI.backgroundColor = _prev;
        }
    }
}
