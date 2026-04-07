using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DiNeTool.GestureManager
{
    public class DiNeGestureWindow : EditorWindow
    {
        // ─── Language ────────────────────────────────────────────────────────────
        private enum Language { Korean, English, Japanese }
        private Language CurrentLang
        {
            get => (Language)EditorPrefs.GetInt("DiNeLang", 0);
            set => EditorPrefs.SetInt("DiNeLang", (int)value);
        }

        private static readonly string[][] UI_TEXT =
        {
            /* 00 */ new[] { "아바타 선택",                    "Select Avatar",              "アバター選択"               },
            /* 01 */ new[] { "씬에 아바타 없음",               "No avatar in scene",         "シーンにアバターなし"        },
            /* 02 */ new[] { "제스쳐",                         "Gesture",                    "ジェスチャー"               },
            /* 03 */ new[] { "이모트",                         "Emote",                      "エモート"                   },
            /* 04 */ new[] { "파라미터",                       "Parameters",                 "パラメーター"               },
            /* 05 */ new[] { "정보",                           "Info",                       "情報"                       },
            /* 06 */ new[] { "왼손",                           "Left Hand",                  "左手"                       },
            /* 07 */ new[] { "오른손",                         "Right Hand",                 "右手"                       },
            /* 08 */ new[] { "플레이 모드에서만 사용 가능",    "Available in Play Mode only", "プレイモードでのみ使用可能"  },
            /* 09 */ new[] { "새로고침",                       "Refresh",                    "更新"                       },
            /* 10 */ new[] { "파라미터 없음",                  "No parameters",              "パラメーターなし"           },
            /* 11 */ new[] { "트라이앵글",                     "Triangles",                  "トライアングル"             },
            /* 12 */ new[] { "버텍스",                         "Vertices",                   "バーテックス"               },
            /* 13 */ new[] { "메테리얼",                       "Materials",                  "マテリアル"                 },
            /* 14 */ new[] { "본",                             "Bones",                      "ボーン"                     },
            /* 15 */ new[] { "VRAM",                           "VRAM",                       "VRAM"                       },
            /* 16 */ new[] { "메쉬",                           "Meshes",                     "メッシュ"                   },
            /* 17 */ new[] { "퍼포먼스",                       "Performance",                "パフォーマンス"             },
            /* 18 */ new[] { "텍스쳐",                         "Textures",                   "テクスチャー"               },
            /* 19 */ new[] { "업로드 예상 용량",               "Est. Upload Size",           "推定アップロードサイズ"      },
            /* 20 */ new[] { "미러 제스쳐",                    "Mirror Gestures",            "ミラージェスチャー"          },
            /* 21 */ new[] { "파라미터 검색",                  "Filter parameters",          "パラメーター検索"           },
            /* 22 */ new[] { "이모트 중지",                    "Stop Emote",                 "エモート停止"               },
            /* 23 */ new[] { "아바타 없음",                    "No avatar",                  "アバターなし"               },
        };

        private string T(int i) => UI_TEXT[i][(int)CurrentLang];

        // ─── Constants ────────────────────────────────────────────────────────────
        private static readonly string[] GestureNames =
            { "Neutral", "Fist", "Open", "Point", "Victory", "Rock", "Gun", "Thumbs Up" };

        private static readonly string[] EmoteNames =
            { "Wave", "Clap", "Point", "Cheer", "Dance", "Back Flip", "Sad Kick", "Die" };

        private static readonly Color ColAccent   = new Color(0.35f, 0.65f, 1.00f);
        private static readonly Color ColSelected = new Color(0.20f, 0.50f, 1.00f);
        private static readonly Color ColEmote    = new Color(0.90f, 0.55f, 0.20f);
        private static readonly Color ColBtn      = new Color(0.28f, 0.28f, 0.32f);
        private static readonly Color ColHeader   = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color ColSection  = new Color(0.18f, 0.18f, 0.21f);

        // ─── State ────────────────────────────────────────────────────────────────
        private VRCAvatarDescriptor[] _avatars = System.Array.Empty<VRCAvatarDescriptor>();
        private int    _avatarIndex;
        private int    _tab;
        private Vector2 _scroll;
        private Texture2D _icon;

        // Gesture
        private int  _gestureLeft;
        private int  _gestureRight;
        private bool _mirrorGesture;

        // Emote
        private int _activeEmote;

        // Params
        private string _paramFilter = "";

        // Stats
        private DiNeAvatarStats.StatsData _stats;
        private bool _statsDirty = true;

        // ─── Entry ───────────────────────────────────────────────────────────────
        [MenuItem("DiNe/Avatar/Gesture Manager")]
        public static void ShowWindow()
        {
            var w = GetWindow<DiNeGestureWindow>("DiNe Gesture");
            w.minSize = new Vector2(340, 520);
            w.position = new Rect(w.position.x, w.position.y, 400, 720);
        }

        private void OnEnable()
        {
            _icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dinetool.avatar-tools/Assets/DiNe.png");
            RefreshAvatars();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyChanged     += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.hierarchyChanged     -= OnHierarchyChanged;
            if (Application.isPlaying) ResetGestureState();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshAvatars();
                _statsDirty = true;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ResetGestureState();
                _statsDirty = true;
            }
        }

        private void OnHierarchyChanged()
        {
            RefreshAvatars();
            _statsDirty = true;
        }

        void Update()
        {
            // Keep parameter view live during play mode
            if (Application.isPlaying && _tab == 2)
                Repaint();
        }

        // ─── Core Helpers ─────────────────────────────────────────────────────────
        private void RefreshAvatars()
        {
            _avatars = FindObjectsOfType<VRCAvatarDescriptor>();
            if (_avatarIndex >= _avatars.Length) _avatarIndex = 0;
            _statsDirty = true;
            Repaint();
        }

        private VRCAvatarDescriptor SelectedAvatar =>
            _avatars != null && _avatars.Length > 0 && _avatarIndex < _avatars.Length
                ? _avatars[_avatarIndex] : null;

        private Animator SelectedAnimator => SelectedAvatar?.GetComponent<Animator>();

        private void ResetGestureState()
        {
            var anim = SelectedAnimator;
            if (anim != null)
            {
                anim.SetInteger("GestureLeft",  0);
                anim.SetInteger("GestureRight", 0);
                anim.SetInteger("Emote",        0);
            }
            _gestureLeft  = 0;
            _gestureRight = 0;
            _activeEmote  = 0;
        }

        // ─── OnGUI ────────────────────────────────────────────────────────────────
        void OnGUI()
        {
            DrawHeader();
            DrawLangBar();
            DrawAvatarBar();

            if (SelectedAvatar == null)
            {
                GUILayout.Space(16);
                DrawCenteredLabel(T(1), new Color(0.7f, 0.7f, 0.7f));
                return;
            }

            GUILayout.Space(4);
            DrawTabBar();
            GUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawGestureTab(); break;
                case 1: DrawEmoteTab();   break;
                case 2: DrawParamsTab();  break;
                case 3: DrawStatsTab();   break;
            }
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        // ─── Header ───────────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = ColHeader;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var style = new GUIStyle(EditorStyles.label)
            {
                alignment  = TextAnchor.MiddleCenter,
                fontStyle  = FontStyle.Bold,
                fontSize   = 22,
                normal     = { textColor = new Color(0.88f, 0.88f, 0.92f) }
            };

            GUIContent title = _icon != null
                ? new GUIContent("  DiNe Gesture", _icon)
                : new GUIContent("DiNe Gesture");

            GUILayout.Label(title, style, GUILayout.Height(38));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Thin accent line under header
            GUILayout.Space(4);
            var lineRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(lineRect, ColAccent);

            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        // ─── Language Bar ─────────────────────────────────────────────────────────
        private void DrawLangBar()
        {
            int idx = (int)CurrentLang;
            idx = GUILayout.Toolbar(idx, new[] { "한국어", "English", "日本語" }, GUILayout.Height(24));
            CurrentLang = (Language)idx;
        }

        // ─── Avatar Selector ──────────────────────────────────────────────────────
        private void DrawAvatarBar()
        {
            GUILayout.Space(4);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = ColSection;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(0), new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColAccent }
            }, GUILayout.Width(90));

            if (_avatars != null && _avatars.Length > 0)
            {
                var names = _avatars.Select(a => a.gameObject.name).ToArray();
                int prev = _avatarIndex;
                _avatarIndex = EditorGUILayout.Popup(_avatarIndex, names);
                if (prev != _avatarIndex) _statsDirty = true;
            }
            else
            {
                GUILayout.Label(T(23), EditorStyles.miniLabel);
            }

            GUI.backgroundColor = new Color(0.35f, 0.45f, 0.6f);
            if (GUILayout.Button(T(9), GUILayout.Width(64), GUILayout.Height(18)))
                RefreshAvatars();
            GUI.backgroundColor = prevBg;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ─── Tab Bar ──────────────────────────────────────────────────────────────
        private void DrawTabBar()
        {
            _tab = GUILayout.Toolbar(_tab,
                new[] { T(2), T(3), T(4), T(5) },
                GUILayout.Height(28));
        }

        // ═════════════════════════════════════════════════════════════════════════
        // GESTURE TAB
        // ═════════════════════════════════════════════════════════════════════════
        private void DrawGestureTab()
        {
            bool play = Application.isPlaying;

            if (!play) DrawPlayModeHint();

            // Mirror toggle
            EditorGUILayout.BeginHorizontal();
            _mirrorGesture = EditorGUILayout.Toggle(_mirrorGesture, GUILayout.Width(16));
            GUILayout.Label(T(20), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUI.enabled = play;

            DrawSectionBox(() =>
            {
                EditorGUILayout.LabelField(T(6), new GUIStyle(EditorStyles.boldLabel)
                    { normal = { textColor = new Color(0.6f, 0.85f, 1f) } });
                GUILayout.Space(4);
                DrawGestureGrid(ref _gestureLeft, "GestureLeft");
            });

            GUILayout.Space(6);

            DrawSectionBox(() =>
            {
                EditorGUILayout.LabelField(T(7), new GUIStyle(EditorStyles.boldLabel)
                    { normal = { textColor = new Color(1f, 0.75f, 0.5f) } });
                GUILayout.Space(4);
                DrawGestureGrid(ref _gestureRight, "GestureRight");
            });

            GUI.enabled = true;
        }

        private void DrawGestureGrid(ref int current, string paramName)
        {
            for (int row = 0; row < 2; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < 4; col++)
                {
                    int idx = row * 4 + col;
                    bool sel = current == idx;
                    GUI.backgroundColor = sel ? ColSelected : ColBtn;

                    if (GUILayout.Button(GestureNames[idx], GUILayout.Height(30)))
                    {
                        current = sel ? 0 : idx;
                        SetGesture(paramName, current);

                        if (_mirrorGesture)
                        {
                            string mirror = paramName == "GestureLeft" ? "GestureRight" : "GestureLeft";
                            if (paramName == "GestureLeft")  _gestureRight = current;
                            else                             _gestureLeft  = current;
                            SetGesture(mirror, current);
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                if (row == 0) GUILayout.Space(3);
            }
        }

        private void SetGesture(string param, int value)
        {
            var anim = SelectedAnimator;
            if (anim != null) anim.SetInteger(param, value);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // EMOTE TAB
        // ═════════════════════════════════════════════════════════════════════════
        private void DrawEmoteTab()
        {
            bool play = Application.isPlaying;
            if (!play) DrawPlayModeHint();

            GUI.enabled = play;

            DrawSectionBox(() =>
            {
                for (int row = 0; row < 2; row++)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int col = 0; col < 4; col++)
                    {
                        int idx = row * 4 + col + 1; // Emote values 1-8
                        bool sel = _activeEmote == idx;
                        GUI.backgroundColor = sel ? ColEmote : ColBtn;

                        if (GUILayout.Button(EmoteNames[idx - 1], GUILayout.Height(40)))
                        {
                            _activeEmote = sel ? 0 : idx;
                            SetEmote(_activeEmote);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    if (row == 0) GUILayout.Space(4);
                }
            });

            GUILayout.Space(8);

            // Stop button
            GUI.enabled = play && _activeEmote != 0;
            GUI.backgroundColor = new Color(0.65f, 0.2f, 0.2f);
            if (GUILayout.Button(T(22), GUILayout.Height(30)))
            {
                _activeEmote = 0;
                SetEmote(0);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void SetEmote(int emote)
        {
            var anim = SelectedAnimator;
            if (anim != null) anim.SetInteger("Emote", emote);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // PARAMETERS TAB
        // ═════════════════════════════════════════════════════════════════════════
        private void DrawParamsTab()
        {
            bool play = Application.isPlaying;
            if (!play) DrawPlayModeHint();

            var anim = SelectedAnimator;
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                DrawCenteredLabel(T(10), new Color(0.6f, 0.6f, 0.6f));
                return;
            }

            // Filter bar
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🔍", GUILayout.Width(18));
            _paramFilter = EditorGUILayout.TextField(_paramFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                _paramFilter = "";
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            GUI.enabled = play;

            var parameters = anim.parameters;
            string filter = _paramFilter.ToLower();

            bool any = false;
            DrawSectionBox(() =>
            {
                foreach (var p in parameters)
                {
                    if (!string.IsNullOrEmpty(filter) && !p.name.ToLower().Contains(filter)) continue;
                    any = true;
                    DrawParamRow(anim, p);
                    SeparatorLine(1, 2);
                }
                if (!any)
                    EditorGUILayout.LabelField(T(10), EditorStyles.centeredGreyMiniLabel);
            });

            GUI.enabled = true;
        }

        private void DrawParamRow(Animator anim, AnimatorControllerParameter p)
        {
            EditorGUILayout.BeginHorizontal();

            // Type badge
            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = ParamTypeColor(p.type) }
            };
            GUILayout.Label(ParamTypeShort(p.type), badgeStyle, GUILayout.Width(28));

            GUILayout.Label(p.name, GUILayout.MinWidth(100));

            switch (p.type)
            {
                case AnimatorControllerParameterType.Bool:
                    bool b = anim.GetBool(p.name);
                    bool nb = EditorGUILayout.Toggle(b, GUILayout.Width(18));
                    if (nb != b) anim.SetBool(p.name, nb);
                    break;

                case AnimatorControllerParameterType.Int:
                    int iv = anim.GetInteger(p.name);
                    int ni = EditorGUILayout.IntField(iv, GUILayout.Width(60));
                    if (ni != iv) anim.SetInteger(p.name, ni);
                    break;

                case AnimatorControllerParameterType.Float:
                    float fv = anim.GetFloat(p.name);
                    float nf = EditorGUILayout.Slider(fv, 0f, 1f);
                    if (!Mathf.Approximately(nf, fv)) anim.SetFloat(p.name, nf);
                    break;

                case AnimatorControllerParameterType.Trigger:
                    GUI.backgroundColor = new Color(0.6f, 0.35f, 0.7f);
                    if (GUILayout.Button("Fire", EditorStyles.miniButton, GUILayout.Width(48)))
                        anim.SetTrigger(p.name);
                    GUI.backgroundColor = Color.white;
                    break;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string ParamTypeShort(AnimatorControllerParameterType t)
        {
            switch (t)
            {
                case AnimatorControllerParameterType.Bool:    return "B";
                case AnimatorControllerParameterType.Int:     return "I";
                case AnimatorControllerParameterType.Float:   return "F";
                case AnimatorControllerParameterType.Trigger: return "T";
                default: return "?";
            }
        }

        private static Color ParamTypeColor(AnimatorControllerParameterType t)
        {
            switch (t)
            {
                case AnimatorControllerParameterType.Bool:    return new Color(0.4f, 0.9f, 0.5f);
                case AnimatorControllerParameterType.Int:     return new Color(0.4f, 0.7f, 1.0f);
                case AnimatorControllerParameterType.Float:   return new Color(1.0f, 0.85f, 0.3f);
                case AnimatorControllerParameterType.Trigger: return new Color(0.85f, 0.4f, 1.0f);
                default: return Color.white;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // STATS TAB
        // ═════════════════════════════════════════════════════════════════════════
        private void DrawStatsTab()
        {
            if (_statsDirty)
            {
                _stats      = DiNeAvatarStats.Calculate(SelectedAvatar.gameObject);
                _statsDirty = false;
            }

            // ── Performance rank card ──
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.13f, 0.13f, 0.16f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(17), new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = ColAccent }, fontSize = 12 }, GUILayout.Width(100));
            GUILayout.Label(_stats.PerformanceRank, new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = _stats.RankColor }, fontSize = 14 });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);

            // ── Geometry ──
            DrawSectionBox(() =>
            {
                SectionHeader("Geometry");
                GUILayout.Space(4);
                StatRow(T(11),  _stats.TriangleCount.ToString("N0"), _stats.TriColor);
                SeparatorLine();
                StatRow(T(12),  _stats.VertexCount.ToString("N0"),   Color.white);
                SeparatorLine();
                StatRow(T(16),  _stats.MeshCount.ToString(),         Color.white);
                SeparatorLine();
                StatRow(T(14),  _stats.BoneCount.ToString(),         Color.white);
            });

            GUILayout.Space(6);

            // ── Textures & Materials ──
            DrawSectionBox(() =>
            {
                SectionHeader("Textures & Materials");
                GUILayout.Space(4);
                StatRow(T(13),  _stats.MaterialCount.ToString(),     Color.white);
                SeparatorLine();
                StatRow(T(18),  _stats.TextureCount.ToString(),      Color.white);
                SeparatorLine();
                StatRow(T(15),  FormatBytes(_stats.VRAMBytes),       _stats.VRAMColor);
            });

            GUILayout.Space(6);

            // ── Upload estimate ──
            DrawSectionBox(() =>
            {
                SectionHeader("Upload");
                GUILayout.Space(4);
                StatRow(T(19), FormatBytes(_stats.UploadSizeBytes), new Color(0.75f, 0.75f, 0.75f));
                GUILayout.Space(2);
                GUILayout.Label("* 실제 업로드 크기와 다를 수 있습니다",
                    new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } });
            });

            GUILayout.Space(8);

            // Refresh
            GUI.backgroundColor = new Color(0.35f, 0.45f, 0.6f);
            if (GUILayout.Button(T(9), GUILayout.Height(28)))
                _statsDirty = true;
            GUI.backgroundColor = Color.white;
        }

        private void StatRow(string label, string value, Color valueColor)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150));
            GUILayout.Label(value, new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = valueColor } });
            EditorGUILayout.EndHorizontal();
        }

        // ─── Shared UI Helpers ────────────────────────────────────────────────────
        private void DrawSectionBox(System.Action content)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = ColSection;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;
            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

        private void DrawPlayModeHint()
        {
            EditorGUILayout.BeginHorizontal();
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
                normal    = { textColor = new Color(0.75f, 0.75f, 0.4f) }
            };
            GUILayout.Label(T(8), style);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawCenteredLabel(string text, Color color)
        {
            GUILayout.Label(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { normal = { textColor = color } });
        }

        private void SectionHeader(string text)
        {
            EditorGUILayout.LabelField(text, new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.65f, 0.65f, 0.7f) } });
        }

        private void SeparatorLine(int height = 1, int padding = 3)
        {
            GUILayout.Space(padding);
            var rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.4f, 0.6f));
            GUILayout.Space(padding);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
            if (bytes >= 1024L * 1024)        return $"{bytes / (1024f * 1024f):F2} MB";
            if (bytes >= 1024L)               return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }
    }
}
