using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DiNeTool.InGameChecker
{
    public partial class DiNeInGameCheckerWindow : EditorWindow
    {
        // ─── Language ────────────────────────────────────────────────────────
        private enum Language { English, Korean, Japanese }
        private Language CurrentLang
        {
            get => (Language)Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
            set => EditorPrefs.SetInt("DiNeLang", (int)value);
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
            /* 35 */ new[] { "Missing Animator", "Animator 없음", "Animator がありません" },
            /* 36 */ new[] { "DiNe Options Menu", "DiNe 옵션 메뉴", "DiNeオプションメニュー" },
            /* 37 */ new[] { "Expression Menu", "익스프레션 메뉴", "エクスプレッションメニュー" },
            /* 38 */ new[] { "No Menu Configured", "설정된 메뉴가 없습니다", "メニューが設定されていません" },
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
        private static Color ColAccent => DiNeEditorUI.Mint;
        private static readonly Color ColRed     = new Color(0.60f, 0.25f, 0.25f);
        private static Color ColText => EditorStyles.label.normal.textColor;
        private static Color ColSubText => DiNeEditorUI.MutedText;

        // ─── State ────────────────────────────────────────────────────────────
        private Texture2D   _headerIcon;
        private Vector2     _scroll;
        private bool        _showStats;
        private bool        _showParams;
        private bool        _statsDirty = true;
        private DiNeAvatarStats.StatsData _stats;
        private GUIStyle statusValueStyle;
        private static GUIStyle centeredHintStyle;

        // 모듈 — GestureManager 대신 자체 모듈 사용
        private DiNeAvatarModule _module;
        private DiNeRadialMenu _radialMenu;
        private bool _isOptionMenuMode;
        private List<VRCAvatarDescriptor> _sceneAvatars = new();

        // ─── Entry ───────────────────────────────────────────────────────────
        // Legacy tool menu intentionally hidden.
        public static void ShowWindow()
        {
            var w = GetWindow<DiNeInGameCheckerWindow>("DiNe In-Game Checker");
            w.minSize = new Vector2(340, 480);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (!EditorPrefs.HasKey("DiNeLang") && EditorPrefs.HasKey("DiNeCheckerLang"))
                EditorPrefs.SetInt("DiNeLang", Mathf.Clamp(EditorPrefs.GetInt("DiNeCheckerLang", 0), 0, 2));
            _headerIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
            titleContent = new GUIContent("In-Game Checker", _headerIcon);
            ConfigureTutorial();

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            guidedTutorial?.Suspend();
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
            {
                RefreshAvatarList();
                guidedTutorial?.NotifyAction("PlayMode");
            }

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
            _radialMenu = null;
            _isOptionMenuMode = false;
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
        private void DrawToolGUI()
        {
            DrawHeader();
            DrawLangBar();
            guidedTutorial.DrawControls();
            HLine();

            guidedTutorial.BeginScrollScope();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_module is { Active: true })
            {
                DrawActiveModule();
                if (_module is { Active: true })
                {
                    HLine();
                    DrawExpressionMenuSection();
                    HLine();
                    DrawGestureControl();
                    HLine();
                    DrawParamsSection();
                }
                else DrawSetup();
            }
            else
            {
                DrawSetup();
            }

            HLine();
            DrawStatsSection();
            GUILayout.Space(10);

            EditorGUILayout.EndScrollView();
            guidedTutorial.EndScrollScope(GUILayoutUtility.GetLastRect());
        }

        // ─── Header ──────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            string description = CurrentLang switch
            {
                Language.Korean => "인게임에서 시점과 포즈가 어떻게 보이는지 에디터 환경에서 미리 검증합니다.",
                Language.Japanese => "ゲーム内での視点やポーズがどう見えるかをエディター上で事前確認します。",
                _ => "Verify how viewports and poses will look in-game directly within the Editor."
            };
            DiNeEditorUI.DrawHeader("In-Game Checker", description);
        }

        // ─── Language Bar ─────────────────────────────────────────────────────
        private void DrawLangBar()
        {
            GUILayout.Space(5f);
            DiNeEditorUI.DrawLanguageToolbar(L);
            GUILayout.Space(15f);
        }

        private int DrawCustomToolbar(int selected, string[] options, float height)
        {
            return DiNeEditorUI.DrawToolbar(selected, options, height);
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
                isPlaying ? ColRed : ColAccent,
                200, (int)DiNeEditorUI.ButtonHeight,
                () => { if (isPlaying) EditorApplication.ExitPlaymode(); else EditorApplication.EnterPlaymode(); });
            tutorialSetupAnchor = GUILayoutUtility.GetLastRect();
            guidedTutorial.Draw("PlayMode", tutorialSetupAnchor);

            if (!isPlaying) { guidedTutorial.Draw("Avatar", tutorialSetupAnchor); GUILayout.Space(DiNeEditorUI.CardSpacing); return; }

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

                    EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);
                    EditorGUILayout.BeginHorizontal();

                    GUILayout.Label(desc.gameObject.name, EditorStyles.boldLabel,
                        GUILayout.ExpandWidth(true));

                    using (new EditorGUI.DisabledScope(!hasAnimator))
                    {
                        if (DiNeEditorUI.Button(T(4), DiNeEditorUI.CompactButtonHeight, GUILayout.Width(60)))
                        {
                            DisconnectModule();
                            _module = new DiNeAvatarModule(desc);
                            _module.Connect();
                            _statsDirty = true;
                            _radialMenu = new DiNeRadialMenu();
                            _radialMenu.Init(_module);
                            guidedTutorial.NotifyAction("Avatar");
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    guidedTutorial.Anchor("Avatar", GUILayoutUtility.GetLastRect());

                    if (!hasAnimator)
                        EditorGUILayout.HelpBox(T(35), MessageType.Warning);

                    EditorGUILayout.EndVertical();
                    GUILayout.Space(2);
                }
                guidedTutorial.Draw("Avatar");
            }

            GUILayout.Space(DiNeEditorUI.CardSpacing);
            if (GUILayout.Button(T(6), GUILayout.Height(DiNeEditorUI.CompactButtonHeight)))
                RefreshAvatarList();
            if (_sceneAvatars.Count == 0) guidedTutorial.Draw("Avatar", GUILayoutUtility.GetLastRect());
        }

        // ═════════════════════════════════════════════════════════════════════
        // ACTIVE MODULE — 아바타 정보 바
        // ═════════════════════════════════════════════════════════════════════
        private void DrawActiveModule()
        {
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal(DiNeEditorUI.CardStyle);
            GUILayout.Label(T(22) + ":", EditorStyles.miniLabel, GUILayout.Width(52));
            GUILayout.Label(_module.Name, EditorStyles.boldLabel,
                GUILayout.ExpandWidth(true));

            if (GUILayout.Button(T(8), GUILayout.Width(70), GUILayout.Height(DiNeEditorUI.CompactButtonHeight)))
            {
                DisconnectModule();
                _statsDirty = true;
            }
            guidedTutorial.Anchor("Unlink", GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();
            guidedTutorial.Anchor("Avatar", GUILayoutUtility.GetLastRect());
            guidedTutorial.Draw("PlayMode", GUILayoutUtility.GetLastRect());
            guidedTutorial.Draw("Avatar");
            guidedTutorial.Draw("Unlink");
        }

        // ═════════════════════════════════════════════════════════════════════
        // RADIAL MENU
        // ═════════════════════════════════════════════════════════════════════
        private void DrawExpressionMenuSection()
        {
            if (_radialMenu == null) return;

            GUILayout.Space(DiNeEditorUI.CardSpacing);

            EditorGUILayout.BeginHorizontal();
            SectionLabel(_isOptionMenuMode ? T(36) : T(37));
            GUILayout.FlexibleSpace();

            // Options Toggle Button (DiNe Icon)
            var prevColor = GUI.color;
            GUI.color = _isOptionMenuMode ? ColAccent : new Color(0.8f, 0.8f, 0.8f, 1f);
            if (GUILayout.Button(_headerIcon, GUIStyle.none, GUILayout.Width(24), GUILayout.Height(24)))
            {
                _isOptionMenuMode = !_isOptionMenuMode;
                if (_isOptionMenuMode)
                {
                    _radialMenu.SetRootMenu(DiNeVirtualMenus.CreateOptionsMenu());
                }
                else
                {
                    _radialMenu.SetRootMenu(_module.Descriptor.expressionsMenu);
                }
            }
            guidedTutorial.Anchor("Options", GUILayoutUtility.GetLastRect());
            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();
            guidedTutorial.Draw("Options");

            GUILayout.Space(4);

            if (!_radialMenu.HasMenu)
            {
                DrawCenteredHint(T(38), ColSubText);
                guidedTutorial.Draw("Menu", GUILayoutUtility.GetLastRect());
                return;
            }

            // 메뉴를 위한 높이 300 확보
            Rect area = GUILayoutUtility.GetRect(0, 300, GUILayout.ExpandWidth(true));
            // 중앙에 300x300 Rect 만들기
            Rect centerRect = new Rect(area.x + (area.width - 300) / 2f, area.y, 300, 300);

            using (new BgColor(ColCard))
            {
                GUI.Box(new Rect(area.x, area.y - 5, area.width, 310), "", DiNeEditorUI.CardStyle);
            }

            _radialMenu.Draw(centerRect);
            guidedTutorial.Draw("Menu", centerRect);
            GUILayout.Space(10);
        }

        // ═════════════════════════════════════════════════════════════════════
        // GESTURE CONTROL
        // ═════════════════════════════════════════════════════════════════════
        private void DrawGestureControl()
        {
            GUILayout.Space(4);
            SectionLabel(T(33));
            GUILayout.Space(4);

            EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);

            // 왼손
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(23), EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label(GestureName(_module.Left), EditorStyles.label);
            EditorGUILayout.EndHorizontal();

            DrawGestureButtons(true);
            guidedTutorial.Anchor("LeftGesture", GUILayoutUtility.GetLastRect());

            GUILayout.Space(6);

            // 오른손
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(24), EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label(GestureName(_module.Right), EditorStyles.label);
            EditorGUILayout.EndHorizontal();

            DrawGestureButtons(false);
            guidedTutorial.Anchor("RightGesture", GUILayoutUtility.GetLastRect());

            EditorGUILayout.EndVertical();
            guidedTutorial.Draw("LeftGesture");
            guidedTutorial.Draw("RightGesture");
        }

        private void DrawGestureButtons(bool isLeft)
        {
            int current = isLeft ? _module.Left : _module.Right;
            for (int row = 0; row < 2; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < 4; col++)
                    {
                        int index = row * 4 + col;
                        if (DiNeEditorUI.SegmentButton(GestureName(index), index == current, DiNeEditorUI.CompactButtonHeight))
                        {
                            if (isLeft) _module.SetLeftGesture(index);
                            else _module.SetRightGesture(index);
                        }
                    }
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // EXPRESSION PARAMETERS
        // ═════════════════════════════════════════════════════════════════════
        private void DrawParamsSection()
        {
            var exprParams = _module.Descriptor.expressionParameters;
            if (exprParams?.parameters == null || exprParams.parameters.Length == 0)
            {
                guidedTutorial.Draw("Parameters", GUILayoutUtility.GetLastRect());
                return;
            }

            GUILayout.Space(4);
            if (DiNeEditorUI.SegmentButton((_showParams ? "▼  " : "▶  ") + T(34), _showParams, DiNeEditorUI.ButtonHeight))
                _showParams = !_showParams;
            guidedTutorial.Draw("Parameters", GUILayoutUtility.GetLastRect());

            if (!_showParams) return;

            GUILayout.Space(4);
            EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);

            foreach (var ep in exprParams.parameters)
            {
                if (string.IsNullOrEmpty(ep.name)) continue;
                if (!_module.Params.TryGetValue(ep.name, out var param)) continue;
                // VRC 시스템 파라미터는 제외
                if (IsSystemParam(ep.name)) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(ep.name, EditorStyles.miniLabel, GUILayout.Width(160));

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
            using (new EditorGUI.DisabledScope(!hasAvatar))
            {
                if (DiNeEditorUI.SegmentButton((_showStats ? "▼  " : "▶  ") + T(9), _showStats, DiNeEditorUI.ButtonHeight))
                {
                    _showStats = !_showStats;
                    if (_showStats) _statsDirty = true;
                }
            }
            guidedTutorial.Draw("Stats", GUILayoutUtility.GetLastRect());

            if (!_showStats) { guidedTutorial.Draw("Refresh", GUILayoutUtility.GetLastRect()); return; }

            if (_statsDirty && _module is { Avatar: not null })
            {
                _stats      = DiNeAvatarStats.Calculate(_module.Avatar);
                _statsDirty = false;
            }

            GUILayout.Space(4);
            EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(10), EditorStyles.boldLabel, GUILayout.Width(110));
            DrawStatusValue(_stats.PerformanceRank, _stats.RankColor);
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
            GUILayout.Label(T(20), EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4);

            if (GUILayout.Button(T(19), GUILayout.Height(DiNeEditorUI.CompactButtonHeight)))
                _statsDirty = true;
            guidedTutorial.Anchor("Refresh", GUILayoutUtility.GetLastRect());

            EditorGUILayout.EndVertical();
            guidedTutorial.Draw("Refresh");
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
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label(label, EditorStyles.miniLabel);
                DrawStatusValue(value, col);
            }
        }

        private void DrawStatusValue(string text, Color color)
        {
            if (statusValueStyle == null) statusValueStyle = new GUIStyle(EditorStyles.boldLabel);
            statusValueStyle.normal.textColor = color;
            GUILayout.Label(text, statusValueStyle);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private void SectionLabel(string text) =>
            GUILayout.Label(text, EditorStyles.boldLabel);

        private static void DrawCenteredHint(string text, Color color)
        {
            if (centeredHintStyle == null)
                centeredHintStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleCenter };
            centeredHintStyle.normal.textColor = color;
            GUILayout.Label(text, centeredHintStyle);
        }

        private static void DrawCenteredButton(string label, Color color, int width, int height, Action onClick)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (color == DiNeEditorUI.Mint)
                {
                    if (DiNeEditorUI.Button(label, height, GUILayout.Width(width))) onClick?.Invoke();
                }
                else
                {
                    using (new BgColor(color))
                        if (GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(height))) onClick?.Invoke();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private static void HLine()
        {
            GUILayout.Space(DiNeEditorUI.CardSpacing);
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
