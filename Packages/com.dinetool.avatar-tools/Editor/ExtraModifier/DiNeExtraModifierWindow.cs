using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DiNeTool.ExtraModifier.Editor
{
    internal sealed class DiNeExtraModifierWindow : EditorWindow
    {
        private enum Language
        {
            English,
            Korean,
            Japanese
        }

        private enum Feature
        {
            Vrm,
            Focus
        }

        private const string LanguagePrefKey = "DiNeExtraModifier_Language";

        private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color DangerColor = new Color(0.72f, 0.32f, 0.32f);

        private static readonly string[][] UiText =
        {
            new[] { "Small avatar improvements, made simple.", "작지만 해두면 좋은 아바타 설정을 간단하게 적용합니다.", "小さいけれど便利なアバター設定を簡単に適用します。" },
            new[] { "Target Avatar", "대상 아바타", "対象アバター" },
            new[] { "Avatar", "아바타", "アバター" },
            new[] { "Select an avatar or one of its children in the Hierarchy.", "Hierarchy에서 아바타 또는 아바타의 자식을 선택하세요.", "Hierarchyでアバターまたはその子オブジェクトを選択してください。" },
            new[] { "Focus Settings", "포커스 설정", "フォーカス設定" },
            new[] { "Automatically fixes focus blur caused by excessive render queues on outfit materials during build. Body, transparent, and cutout materials are safely excluded.", "의상 머티리얼의 과도한 렌더 큐로 발생하는 포커스 블러 문제를 빌드할 때 자동 보정합니다. Body와 투명·컷아웃 계열은 안전하게 제외됩니다.", "衣装マテリアルの高すぎるレンダーキューによるフォーカスブラーをビルド時に自動補正します。Body、透明、カットアウト系は安全のため除外します。" },
            new[] { "Not Applied", "미적용", "未適用" },
            new[] { "Applied", "적용됨", "適用済み" },
            new[] { "Apply Focus", "포커스 적용", "フォーカスを適用" },
            new[] { "Focus Applied ✓", "포커스 적용 완료 ✓", "フォーカス適用済み ✓" },
            new[] { "Advanced Settings", "고급 설정", "詳細設定" },
            new[] { "Force correction (all materials above Queue 2400, including transparent materials)", "강제 보정 (투명 계열을 포함한 Queue 2400 초과 머티리얼 전체)", "強制補正（透明系を含むQueue 2400超過の全マテリアル）" },
            new[] { "Remove Focus", "포커스 제거", "フォーカスを削除" },
            new[] { "Focus is active. It will be applied automatically during play mode and avatar builds.", "포커스가 활성화되어 있습니다. 플레이 모드와 아바타 빌드 시 자동 적용됩니다.", "フォーカスは有効です。プレイモードとアバタービルド時に自動適用されます。" },
            new[] { "Focus is not applied to this avatar yet.", "이 아바타에는 아직 포커스가 적용되지 않았습니다.", "このアバターにはまだフォーカスが適用されていません。" },
            new[] { "Extra Modifier's Focus is active. No additional setup is required; it runs automatically when the avatar is built.", "Extra Modifier의 포커스가 활성화되어 있습니다. 별도 작업 없이 아바타 빌드 시 자동 적용됩니다.", "Extra Modifierのフォーカスは有効です。追加設定なしでアバタービルド時に自動適用されます。" },
            new[] { "Force Correction", "강제 보정", "強制補正" },
            new[] { "Focus", "포커스", "フォーカス" },
            new[] { "VRM", "VRM", "VRM" },
            new[] { "VRM Target", "VRM 대상", "VRM対象" },
            new[] { "Working Avatar", "작업 아바타", "作業アバター" },
            new[] { "Assign an avatar to prepare for VRM.", "VRM용으로 정리할 아바타를 지정하세요.", "VRM用に準備するアバターを指定してください。" },
            new[] { "The individual buttons modify the assigned object. Use the full auto button to work safely on an automatic copy.", "개별 버튼은 지정한 오브젝트를 직접 수정합니다. 안전하게 작업하려면 복사본을 만드는 전체 자동 처리를 사용하세요.", "個別ボタンは指定したオブジェクトを直接変更します。安全に作業するには自動コピーを作成する全自動処理を使用してください。" },
            new[] { "Create VRM Working Copy", "VRM 작업 복사본 만들기", "VRM作業コピーを作成" },
            new[] { "Full Automatic Preparation", "전체 자동 처리", "全自動処理" },
            new[] { "Creates a copy, merges outfit bones, converts PhysBones, preserves compatible constraints, and removes VRM-incompatible scripts.", "복사본을 만든 뒤 의상 본 병합, PhysBone 변환, 호환 가능한 Constraint 보존, VRM 비호환 스크립트 제거를 한 번에 진행합니다.", "コピーを作成し、衣装ボーン統合、PhysBone変換、互換Constraintの保持、VRM非互換スクリプトの削除を一括実行します。" },
            new[] { "Run All on a Copy", "복사본에 전체 자동 처리", "コピーに全自動処理" },
            new[] { "1. Merge Outfit Bones", "1. 의상 본 자동 병합", "1. 衣装ボーン自動統合" },
            new[] { "Automatically finds duplicate outfit and hair bones and nests them under the matching humanoid bones.", "중복된 의상·헤어 본을 자동 탐지하여 이름이 같은 휴머노이드 본 아래로 병합합니다.", "重複した衣装・髪ボーンを自動検出し、同名のHumanoidボーン配下へ統合します。" },
            new[] { "Merge Bones", "본 자동 병합", "ボーンを自動統合" },
            new[] { "2. Convert SpringBones", "2. 스프링본 자동 변환", "2. SpringBone自動変換" },
            new[] { "Converts every VRC PhysBone and referenced collider to UniVRM 0.x SpringBones using automatic settings.", "모든 VRC PhysBone과 연결된 Collider를 자동 설정으로 UniVRM 0.x SpringBone으로 변환합니다.", "すべてのVRC PhysBoneと参照Colliderを自動設定でUniVRM 0.x SpringBoneへ変換します。" },
            new[] { "Convert All PhysBones", "모든 PhysBone 자동 변환", "すべてのPhysBoneを自動変換" },
            new[] { "UniVRM 0.x is required for SpringBone conversion.", "SpringBone 변환에는 UniVRM 0.x가 필요합니다.", "SpringBone変換にはUniVRM 0.xが必要です。" },
            new[] { "3. Clean VRM Components", "3. VRM 컴포넌트 정리", "3. VRMコンポーネント整理" },
            new[] { "Converts static VRC Constraints to Unity Constraints where possible, then removes MA, NDMF, VRC, and other non-VRM MonoBehaviours.", "정적 VRC Constraint를 가능한 경우 Unity Constraint로 변환한 뒤 MA, NDMF, VRC 및 기타 VRM 비호환 MonoBehaviour를 제거합니다.", "静的VRC Constraintを可能な場合はUnity Constraintへ変換し、MA、NDMF、VRC、その他VRM非互換MonoBehaviourを削除します。" },
            new[] { "Clean Components", "컴포넌트 정리", "コンポーネントを整理" },
            new[] { "Result", "처리 결과", "処理結果" },
            new[] { "Ready", "준비됨", "準備完了" },
            new[] { "Working copy created.", "작업 복사본을 만들었습니다.", "作業コピーを作成しました。" },
            new[] { "Focus is temporarily sealed and does not run during play mode or avatar builds.", "포커스 기능은 임시 봉인되어 플레이 모드와 아바타 빌드에서 동작하지 않습니다.", "フォーカス機能は一時的に封印され、プレイモードとアバタービルドでは動作しません。" }
        };

        private Language language;
        private Feature feature;
        private VRCAvatarDescriptor avatar;
        private GameObject vrmAvatar;
        private string vrmStatus;
        private MessageType vrmStatusType = MessageType.Info;
        private bool advanced;
        private Vector2 scroll;
        private Texture2D windowIcon;
        private Texture2D tabIcon;
        private Font titleFont;

        private int L => (int)language;
        private string T(int index) => UiText[index][L];

        [MenuItem("DiNe/EX/Extra Modifier", false, 103)]
        private static void Open()
        {
            var window = GetWindow<DiNeExtraModifierWindow>();
            window.minSize = new Vector2(400f, 480f);
            window.position = new Rect(window.position.x, window.position.y, 440f, 620f);
            window.Show();
            window.Focus();
        }

        private static void ApplyFromHierarchy()
        {
            var selectedAvatar = FindAvatar(Selection.activeGameObject);
            if (selectedAvatar != null)
                EnableFocus(selectedAvatar);
        }

        private static bool ValidateApplyFromHierarchy()
        {
            return FindAvatar(Selection.activeGameObject) != null;
        }

        private void OnEnable()
        {
            windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            tabIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
            titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
            titleContent = new GUIContent("Modifier", tabIcon);
            language = (Language)Mathf.Clamp(EditorPrefs.GetInt(LanguagePrefKey, (int)Language.Korean), 0, 2);
            feature = Feature.Vrm;
            TryUseSelection();
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            TryUseSelection();
            Repaint();
        }

        private void TryUseSelection()
        {
            var selectedAvatar = FindAvatar(Selection.activeGameObject);
            if (selectedAvatar != null)
            {
                avatar = selectedAvatar;
                vrmAvatar = selectedAvatar.gameObject;
            }
        }

        private void OnGUI()
        {
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            DrawHeader();
            GUILayout.Space(5f);
            DrawLanguageToolbar();
            GUILayout.Space(6f);
            DrawFeatureToolbar();
            GUILayout.Space(8f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (feature)
            {
                case Feature.Vrm:
                    DrawVrmTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            const float iconSize = 72f;
            if (windowIcon != null)
                GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
            GUILayout.Space(6f);

            GUILayout.Label("Extra Modifier", new GUIStyle(EditorStyles.label)
            {
                font = titleFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 36,
                normal = { textColor = Color.white }
            }, GUILayout.Height(iconSize));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label(T(0), new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            });
            GUILayout.Space(5f);
            EditorGUILayout.EndVertical();
        }

        private void DrawLanguageToolbar()
        {
            var next = DrawToolbar((int)language, new[] { "English", "한국어", "日本語" }, 28f);
            if (next == (int)language)
                return;

            language = (Language)next;
            EditorPrefs.SetInt(LanguagePrefKey, next);
            Repaint();
        }

        private void DrawFeatureToolbar()
        {
            feature = Feature.Vrm;
            DrawToolbar(0, new[] { T(18) }, 32f);
        }

        private void DrawAvatarSection()
        {
            SectionLabel(T(1));
            GUILayout.Space(4f);

            EditorGUILayout.BeginVertical("box");
            avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(T(2), avatar, typeof(VRCAvatarDescriptor), true);
            if (avatar == null)
                EditorGUILayout.HelpBox(T(3), MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawFocusSection()
        {
            SectionLabel(T(4));
            GUILayout.Space(4f);

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(17), new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = AccentColor }
            });
            GUILayout.Label(T(5), EditorStyles.wordWrappedLabel);
            HLine();

            var focus = avatar != null ? DiNeFocusProcessor.FindSettings(avatar) : null;
            DrawStatus(focus != null);
            GUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(avatar == null || focus != null))
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = AccentColor;
                var buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
                if (GUILayout.Button(focus == null ? T(8) : T(9), buttonStyle, GUILayout.Height(40f)))
                    focus = EnableFocus(avatar);
                GUI.backgroundColor = previousColor;
            }

            if (focus != null)
                DrawAdvancedSettings(focus);

            EditorGUILayout.EndVertical();
        }

        private void DrawStatus(bool enabled)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(enabled ? T(13) : T(14), EditorStyles.wordWrappedMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(enabled ? T(7) : T(6), new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = enabled ? AccentColor : new Color(0.75f, 0.55f, 0.4f) }
            }, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSettings(DiNeFocus focus)
        {
            GUILayout.Space(5f);
            advanced = EditorGUILayout.Foldout(advanced, T(10), true);
            if (!advanced)
                return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var forceAll = EditorGUILayout.ToggleLeft(T(11), focus.ForceAll);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(focus, "Change Focus mode");
                focus.SetForceAll(forceAll);
                EditorUtility.SetDirty(focus);
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(5f);
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = DangerColor;
            if (GUILayout.Button(T(12), GUILayout.Height(24f)))
            {
                Undo.DestroyObjectImmediate(focus);
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = previousColor;
        }

        private void DrawVrmTab()
        {
            SectionLabel(T(19));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            vrmAvatar = (GameObject)EditorGUILayout.ObjectField(T(20), vrmAvatar, typeof(GameObject), true);
            if (vrmAvatar == null)
                EditorGUILayout.HelpBox(T(21), MessageType.Info);
            else
                EditorGUILayout.HelpBox(T(22), MessageType.Warning);

            using (new EditorGUI.DisabledScope(vrmAvatar == null))
            {
                if (GUILayout.Button(T(23), GUILayout.Height(28f)))
                {
                    var copy = DiNeVrmUtility.CreateWorkingCopy(vrmAvatar);
                    if (copy != null)
                    {
                        vrmAvatar = copy;
                        SetVrmStatus(T(39), MessageType.Info);
                    }
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);
            SectionLabel(T(24));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(25), EditorStyles.wordWrappedLabel);
            if (!DiNeVrmUtility.IsUniVrmAvailable)
                EditorGUILayout.HelpBox(T(33), MessageType.Error);

            using (new EditorGUI.DisabledScope(vrmAvatar == null || !DiNeVrmUtility.IsUniVrmAvailable))
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = AccentColor;
                if (GUILayout.Button(T(26), ActionButtonStyle(), GUILayout.Height(42f)))
                {
                    var report = DiNeVrmUtility.RunAll(vrmAvatar, out var copy);
                    if (copy != null)
                        vrmAvatar = copy;
                    ShowVrmReport(report);
                }
                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);
            DrawVrmActionSection(27, 28, 29, true, () => DiNeVrmUtility.MergeOutfitBones(vrmAvatar));
            GUILayout.Space(8f);
            DrawVrmActionSection(30, 31, 32, DiNeVrmUtility.IsUniVrmAvailable, () => DiNeVrmUtility.ConvertPhysBones(vrmAvatar));
            GUILayout.Space(8f);
            DrawVrmActionSection(34, 35, 36, true, () => DiNeVrmUtility.CleanupForVrm(vrmAvatar));

            if (!string.IsNullOrEmpty(vrmStatus))
            {
                GUILayout.Space(10f);
                SectionLabel(T(37));
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox(vrmStatus, vrmStatusType);
            }
        }

        private void DrawVrmActionSection(int titleIndex, int descriptionIndex, int buttonIndex, bool available, Func<DiNeVrmReport> operation)
        {
            SectionLabel(T(titleIndex));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(descriptionIndex), EditorStyles.wordWrappedLabel);
            if (!available)
                EditorGUILayout.HelpBox(T(33), MessageType.Error);

            using (new EditorGUI.DisabledScope(vrmAvatar == null || !available))
            {
                if (GUILayout.Button(T(buttonIndex), GUILayout.Height(30f)))
                    ShowVrmReport(operation());
            }
            EditorGUILayout.EndVertical();
        }

        private void ShowVrmReport(DiNeVrmReport report)
        {
            if (report == null)
                return;
            if (!report.Succeeded)
            {
                var errorMessage = report.Error != null && report.Error.Contains("UniVRM") ? T(33) : report.Error;
                SetVrmStatus(errorMessage, MessageType.Error);
                return;
            }

            string message;
            switch (language)
            {
                case Language.Korean:
                    message = $"병합 본 {report.MergedBones} / SpringBone {report.SpringBones} / Constraint 변환 {report.ConvertedConstraints} / 제거 컴포넌트 {report.RemovedComponents}";
                    break;
                case Language.Japanese:
                    message = $"統合ボーン {report.MergedBones} / SpringBone {report.SpringBones} / Constraint変換 {report.ConvertedConstraints} / 削除コンポーネント {report.RemovedComponents}";
                    break;
                default:
                    message = $"Merged bones {report.MergedBones} / SpringBones {report.SpringBones} / Converted constraints {report.ConvertedConstraints} / Removed components {report.RemovedComponents}";
                    break;
            }
            SetVrmStatus(message, MessageType.Info);
        }

        private void SetVrmStatus(string message, MessageType type)
        {
            vrmStatus = string.IsNullOrEmpty(message) ? T(38) : message;
            vrmStatusType = type;
            Repaint();
        }

        private static GUIStyle ActionButtonStyle()
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }

        private static int DrawToolbar(int selected, string[] options, float height)
        {
            EditorGUILayout.BeginHorizontal();
            var result = selected;
            for (var i = 0; i < options.Length; i++)
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = i == selected ? AccentColor : InactiveColor;
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = i == selected ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
                };
                if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
                    result = i;
                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndHorizontal();
            return result;
        }

        private static void SectionLabel(string text)
        {
            GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = AccentColor }
            });
        }

        private static void HLine()
        {
            GUILayout.Space(5f);
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(5f);
        }

        private static DiNeFocus EnableFocus(VRCAvatarDescriptor targetAvatar)
        {
            if (targetAvatar == null)
                return null;

            var existing = DiNeFocusProcessor.FindSettings(targetAvatar);
            if (existing != null)
                return existing;

            var component = Undo.AddComponent<DiNeFocus>(targetAvatar.gameObject);
            EditorUtility.SetDirty(targetAvatar.gameObject);
            Selection.activeGameObject = targetAvatar.gameObject;
            Debug.Log($"[DiNe Extra Modifier] Applied Focus to '{targetAvatar.name}'.", component);
            return component;
        }

        private static VRCAvatarDescriptor FindAvatar(GameObject selected)
        {
            return selected != null ? selected.GetComponentInParent<VRCAvatarDescriptor>(true) : null;
        }

        internal static int CurrentLanguage => Mathf.Clamp(EditorPrefs.GetInt(LanguagePrefKey, (int)Language.Korean), 0, 2);
        internal static string InspectorInfo => UiText[40][CurrentLanguage];
    }

    [CustomEditor(typeof(DiNeFocus))]
    internal sealed class DiNeFocusEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(DiNeExtraModifierWindow.InspectorInfo, MessageType.Warning);
        }
    }
}
