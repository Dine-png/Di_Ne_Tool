using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DiNeTool.InGameChecker
{
    public class DiNeInGameCheckerWindow : EditorWindow
    {
        // ─── Language ────────────────────────────────────────────────────────
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
            /* 25 */ new[] { "Idle",                                           "기본",                                          "アイドル"                                         },
            /* 26 */ new[] { "Fist",                                           "주먹",                                          "グー"                                             },
            /* 27 */ new[] { "Open",                                           "펼치기",                                        "パー"                                             },
            /* 28 */ new[] { "FingerPoint",                                    "검지",                                          "指差し"                                           },
            /* 29 */ new[] { "Victory",                                        "브이",                                          "ピース"                                           },
            /* 30 */ new[] { "Rock&Roll",                                      "락앤롤",                                        "ロック"                                           },
            /* 31 */ new[] { "Gun",                                            "핑거건",                                        "ピストル"                                         },
            /* 32 */ new[] { "ThumbsUp",                                       "엄지척",                                        "サムズアップ"                                     },
            /* 33 */ new[] { "Gesture Control",                                "제스처 컨트롤",                                  "ジェスチャーコントロール"                         },
            /* 34 */ new[] { "Expression Parameters",                          "익스프레션 파라미터",                            "エクスプレッションパラメータ"                     },
        };
        private string T(int i) => UI_TEXT[i][L];

        // ─── Gesture names (index 0~7) ───────────────────────────────────────
        private string GestureName(int i) => i switch
        {
            0 => T(25), 1 => T(26), 2 => T(27), 3 => T(28),
            4 => T(29), 5 => T(30), 6 => T(31), 7 => T(32),
            _ => "?"
        };

        // ─── Colors ───────────────────────────────────────────────────────────
        private static readonly Color ColCard    = new Color(0.21f, 0.21f, 0.24f);
        private static readonly Color ColAccent  = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColGreen   = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColRed     = new Color(0.60f, 0.25f, 0.25f);
        private static readonly Color ColBlue    = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColText    = new Color(0.88f, 0.88f, 0.92f);
        private static readonly Color ColSubText = new Color(0.58f, 0.58f, 0.63f);
        private static readonly Color ColLine    = new Color(0.30f, 0.30f, 0.35f, 0.8f);

        // ─── State ────────────────────────────────────────────────────────────
        private Texture2D   _icon;
        private Texture2D   _headerIcon;
        private Font        _titleFont;
        private Vector2     _scroll;
        private bool        _showStats;
        private bool        _showParams;
        private bool        _statsDirty = true;
        private DiNeAvatarStats.StatsData _stats;

        // 모듈 — GestureManager 대신 자체 모듈 사용
        private DiNeAvatarModule _module;
        private List<VRCAvatarDescriptor> _sceneAvatars = new();

        // ─── Entry ───────────────────────────────────────────────────────────
        [MenuItem("DiNe/Avatar/In-Game Checker")]
        public static void ShowWindow()
        {
            var w = GetWindow<DiNeInGameCheckerWindow>("DiNe In-Game Checker");
            w.minSize = new Vector2(340, 480);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _icon       = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
            _headerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
            _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
            titleContent = new GUIContent("In-Game Checker", _headerIcon);

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            DisconnectModule();
        }

        private void OnDestroy() => DisconnectModule();

        private void Update()
        {
            if (_module is { Active: true })
            {
                _module.OnUpdate();
                Repaint();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                RefreshAvatarList();

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                DisconnectModule();
                _statsDirty = true;
            }
            Repaint();
        }

        private void DisconnectModule()
        {
            _module?.Disconnect();
            _module = null;
        }

        private void RefreshAvatarList()
        {
            _sceneAvatars = Resources.FindObjectsOfTypeAll<VRCAvatarDescriptor>()
                .Where(d => d.gameObject.scene.name != null &&
                            d.gameObject.activeInHierarchy)
                .ToList();
        }

        // ═════════════════════════════════════════════════════════════════════
        // OnGUI
        // ═════════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            DrawHeader();
            DrawLangBar();
            HLine();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_module is { Active: true })
            {
                DrawActiveModule();
                HLine();
                DrawGestureControl();
                HLine();
                DrawParamsSection();
            }
            else
            {
                DrawSetup();
            }

            HLine();
            DrawStatsSection();
            GUILayout.Space(10);

            EditorGUILayout.EndScrollView();
        }

        // ─── Header ──────────────────────────────────────────────────────────
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
            string desc = CurrentLang switch
            {
                Language.Korean  => "인게임에서 시점과 포즈가 어떻게 보이는지 에디터 환경에서 미리 검증합니다.",
                Language.Japanese => "ゲーム内での視点やポーズがどう見えるかをエディター上で事前確認します。",
                _                => "Verify how viewports and poses will look in-game directly within the Editor."
            };
            GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        // ─── Language Bar ─────────────────────────────────────────────────────
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
                GUI.backgroundColor = (i == selected) ? ColAccent : new Color(0.5f, 0.5f, 0.5f, 1f);
                GUIStyle style = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = (i == selected) ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = 12,
                    normal = { textColor = (i == selected) ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
                };
                if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
                    newSelected = i;
                GUI.backgroundColor = prevBg;
            }
            EditorGUILayout.EndHorizontal();
            return newSelected;
        }

        // ═════════════════════════════════════════════════════════════════════
        // SETUP
        // ═════════════════════════════════════════════════════════════════════
        private void DrawSetup()
        {
            bool isPlaying = EditorApplication.isPlaying;

            GUILayout.Space(12);

            if (!isPlaying)
                DrawCenteredHint(T(0), ColSubText);

            GUILayout.Space(10);

            DrawCenteredButton(
                isPlaying ? T(2) : T(1),
                isPlaying ? ColRed : ColGreen,
                200, 36,
                () => { if (isPlaying) EditorApplication.ExitPlaymode(); else EditorApplication.EnterPlaymode(); });

            if (!isPlaying) { GUILayout.Space(8); return; }

            GUILayout.Space(14);
            HLine();
            GUILayout.Space(6);

            if (_sceneAvatars.Count == 0)
                RefreshAvatarList();

            // null 참조 정리
            _sceneAvatars.RemoveAll(d => d == null);

            if (_sceneAvatars.Count == 0)
            {
                DrawCenteredHint(T(7), new Color(1f, 0.65f, 0.3f));
            }
            else
            {
                SectionLabel(T(3));
                GUILayout.Space(4);

                foreach (var desc in _sceneAvatars)
                {
                    if (desc == null) continue;
                    bool hasAnimator = desc.GetComponent<Animator>() != null;

                    using (new BgColor(ColCard))
                    {
                        EditorGUILayout.BeginVertical("box");
                        EditorGUILayout.BeginHorizontal();

                        GUILayout.Label(desc.gameObject.name, new GUIStyle(EditorStyles.boldLabel)
                            { fontSize = 12, normal = { textColor = hasAnimator ? new Color(0.80f, 0.95f, 0.70f) : ColSubText } },
                            GUILayout.ExpandWidth(true));

                        GUI.enabled = hasAnimator;
                        using (new BgColor(ColGreen))
                        {
                            if (GUILayout.Button(T(4), GUILayout.Width(60), GUILayout.Height(22)))
                            {
                                DisconnectModule();
                                _module = new DiNeAvatarModule(desc);
                                _module.Connect();
                                _statsDirty = true;
                            }
                        }
                        GUI.enabled = true;

                        EditorGUILayout.EndHorizontal();

                        if (!hasAnimator)
                            GUILayout.Label("Missing Animator", new GUIStyle(EditorStyles.miniLabel)
                                { normal = { textColor = new Color(1f, 0.5f, 0.3f) } });

                        EditorGUILayout.EndVertical();
                    }
                    GUILayout.Space(2);
                }
            }

            GUILayout.Space(8);
            using (new BgColor(ColBlue))
                if (GUILayout.Button(T(6), GUILayout.Height(26)))
                    RefreshAvatarList();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ACTIVE MODULE — 아바타 정보 바
        // ═════════════════════════════════════════════════════════════════════
        private void DrawActiveModule()
        {
            GUILayout.Space(4);

            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(T(22) + ":", new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = ColSubText } }, GUILayout.Width(52));
                GUILayout.Label(_module.Name, new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 12, normal = { textColor = new Color(0.80f, 0.95f, 0.70f) } },
                    GUILayout.ExpandWidth(true));

                using (new BgColor(ColAccent))
                {
                    if (GUILayout.Button(T(8), GUILayout.Width(70), GUILayout.Height(20)))
                    {
                        DisconnectModule();
                        _statsDirty = true;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // GESTURE CONTROL
        // ═════════════════════════════════════════════════════════════════════
        private void DrawGestureControl()
        {
            GUILayout.Space(4);
            SectionLabel(T(33));
            GUILayout.Space(4);

            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginVertical("box");

                // 왼손
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(23), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = ColAccent } }, GUILayout.Width(80));
                GUILayout.Label(GestureName(_module.Left), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = ColText } });
                EditorGUILayout.EndHorizontal();

                DrawGestureButtons(true);

                GUILayout.Space(6);

                // 오른손
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(24), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = ColAccent } }, GUILayout.Width(80));
                GUILayout.Label(GestureName(_module.Right), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = ColText } });
                EditorGUILayout.EndHorizontal();

                DrawGestureButtons(false);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawGestureButtons(bool isLeft)
        {
            int current = isLeft ? _module.Left : _module.Right;

            // 2줄 × 4개 버튼
            for (int row = 0; row < 2; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < 4; col++)
                {
                    int idx = row * 4 + col;
                    bool isActive = (idx == current);
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = isActive ? ColAccent : new Color(0.35f, 0.35f, 0.38f);

                    var btnStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 10,
                        fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                        normal = { textColor = isActive ? Color.white : ColSubText }
                    };

                    if (GUILayout.Button(GestureName(idx), btnStyle, GUILayout.Height(24)))
                    {
                        if (isLeft) _module.SetLeftGesture(idx);
                        else        _module.SetRightGesture(idx);
                    }
                    GUI.backgroundColor = prevBg;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // EXPRESSION PARAMETERS
        // ═════════════════════════════════════════════════════════════════════
        private void DrawParamsSection()
        {
            var exprParams = _module.Descriptor.expressionParameters;
            if (exprParams?.parameters == null || exprParams.parameters.Length == 0) return;

            GUILayout.Space(4);
            using (new BgColor(_showParams ? ColAccent : ColCard))
            {
                if (GUILayout.Button((_showParams ? "▼  " : "▶  ") + T(34), GUILayout.Height(28)))
                    _showParams = !_showParams;
            }

            if (!_showParams) return;

            GUILayout.Space(4);
            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginVertical("box");

                foreach (var ep in exprParams.parameters)
                {
                    if (string.IsNullOrEmpty(ep.name)) continue;
                    if (!_module.Params.TryGetValue(ep.name, out var param)) continue;
                    // VRC 시스템 파라미터는 제외
                    if (IsSystemParam(ep.name)) continue;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(ep.name, new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = ColText } }, GUILayout.Width(160));

                    switch (ep.valueType)
                    {
                        case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool:
                        {
                            bool val = param.BoolValue();
                            bool newVal = EditorGUILayout.Toggle(val, GUILayout.Width(20));
                            if (newVal != val) param.Set(newVal ? 1f : 0f);
                            break;
                        }
                        case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int:
                        {
                            int val = param.IntValue();
                            int newVal = EditorGUILayout.IntField(val, GUILayout.Width(60));
                            if (newVal != val) param.Set(newVal);
                            break;
                        }
                        case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float:
                        {
                            float val = param.FloatValue();
                            float newVal = EditorGUILayout.Slider(val, -1f, 1f);
                            if (Math.Abs(newVal - val) > 0.001f) param.Set(newVal);
                            break;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static bool IsSystemParam(string name)
        {
            return name is "VRCEmote" or "VRCFaceBlendH" or "VRCFaceBlendV"
                or "GestureLeft" or "GestureRight" or "GestureLeftWeight" or "GestureRightWeight"
                or "Viseme" or "Voice" or "Upright" or "AngularY"
                or "VelocityX" or "VelocityY" or "VelocityZ" or "VelocityMagnitude"
                or "Grounded" or "Seated" or "AFK" or "IsLocal" or "IsOnFriendsList"
                or "InStation" or "MuteSelf" or "TrackingType" or "AvatarVersion"
                or "VRMode" or "IsAnimatorEnabled" or "ScaleFactor" or "ScaleFactorInverse"
                or "EyeHeightAsMeters" or "EyeHeightAsPercent";
        }

        // ═════════════════════════════════════════════════════════════════════
        // STATS
        // ═════════════════════════════════════════════════════════════════════
        private void DrawStatsSection()
        {
            bool hasAvatar = _module is { Active: true, Avatar: not null };
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

            if (_statsDirty && _module is { Avatar: not null })
            {
                _stats      = DiNeAvatarStats.Calculate(_module.Avatar);
                _statsDirty = false;
            }

            GUILayout.Space(4);
            using (new BgColor(ColCard))
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(T(10), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = ColAccent } }, GUILayout.Width(110));
                GUILayout.Label(_stats.PerformanceRank, new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 13, normal = { textColor = _stats.RankColor } });
                EditorGUILayout.EndHorizontal();

                HLine();

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

        // ─── Helpers ─────────────────────────────────────────────────────────
        private void SectionLabel(string text) =>
            GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 11, normal = { textColor = ColAccent } });

        private static void DrawCenteredHint(string text, Color color)
        {
            GUILayout.Label(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { fontStyle = FontStyle.Italic, fontSize = 11, wordWrap = true,
                  normal = { textColor = color } });
        }

        private static void DrawCenteredButton(string label, Color color, int width, int height, Action onClick)
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

        private readonly struct BgColor : IDisposable
        {
            private readonly Color _prev;
            public BgColor(Color c) { _prev = GUI.backgroundColor; GUI.backgroundColor = c; }
            public void Dispose() => GUI.backgroundColor = _prev;
        }
    }
}
