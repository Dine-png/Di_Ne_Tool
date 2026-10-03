using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DiNeTool.ExpressionEditor
{
    // Authored for Di Ne Tool against the public Unity and VRChat SDK APIs.
    // No VRCSDK+ implementation or assets are included.
    public abstract class DiNeExpressionInspectorBase : Editor
    {
        protected VRCAvatarDescriptor Avatar;
        protected List<AnimatorControllerParameter> AnimatorParameters = new List<AnimatorControllerParameter>();
        protected string Status;
        protected static readonly Color Mint = new Color(0.30f, 0.82f, 0.76f, 1f);
        private Texture2D brandIcon;
        private Font titleFont;
        private GUIStyle titleStyle, descriptionStyle, selectedButton, ordinaryButton;
        private Editor sdkEditor;
        private VisualElement inspectorRoot;
        private bool lastMode;
        private bool lastEditable;
        private int lastLanguage = -1;

        protected abstract string SdkEditorName { get; }
        protected abstract string InspectorTitle { get; }
        protected abstract string InspectorDescription { get; }
        protected abstract void DrawContents();

        protected virtual void OnEnable()
        {
            brandIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            DiNeExpressionInspectorSettings.WarnIfCompetingInspectorPackage();
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (sdkEditor != null) DestroyImmediate(sdkEditor);
        }

        public override VisualElement CreateInspectorGUI()
        {
            inspectorRoot = new VisualElement();
            BuildInspectorRoot();
            inspectorRoot.schedule.Execute(() =>
            {
                if (lastMode != DiNeExpressionInspectorSettings.UseDiNeInspector) BuildInspectorRoot();
                else if (target != null && lastEditable != DiNeExpressionUtility.CanEditAsset(target)) BuildInspectorRoot();
            }).Every(250);
            return inspectorRoot;
        }

        private void BuildInspectorRoot()
        {
            inspectorRoot.Clear();
            if (sdkEditor != null) DestroyImmediate(sdkEditor);
            sdkEditor = null;
            lastMode = DiNeExpressionInspectorSettings.UseDiNeInspector;
            lastEditable = DiNeExpressionUtility.CanEditAsset(target);
            if (lastMode)
            {
                inspectorRoot.Add(new IMGUIContainer(OnInspectorGUI));
                return;
            }
            if (!lastEditable)
            {
                // SDK OnEnable can initialize defaults. Do not instantiate it for a locked asset.
                inspectorRoot.Add(new IMGUIContainer(() =>
                {
                    using (new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
                    EditorGUILayout.HelpBox(T("Editing is disabled during Play Mode and for read-only assets.",
                        "플레이 모드와 읽기 전용 에셋에서는 편집할 수 없습니다.", "プレイモードと読み取り専用アセットでは編集できません。"), MessageType.Info);
                }));
                return;
            }
            string shortName = SdkEditorName.Substring(SdkEditorName.LastIndexOf('.') + 1);
            Type sdkType = TypeCache.GetTypesDerivedFrom<Editor>().FirstOrDefault(t =>
                t.Assembly.GetName().Name == "VRC.SDK3A.Editor" && (t.FullName == SdkEditorName || t.Name == shortName));
            if (sdkType != null)
            {
                sdkEditor = CreateEditor(targets, sdkType);
                VisualElement sdkRoot = sdkEditor.CreateInspectorGUI();
                if (sdkRoot != null)
                {
                    sdkRoot.Bind(sdkEditor.serializedObject);
                    inspectorRoot.Add(sdkRoot);
                    return;
                }
                inspectorRoot.Add(new IMGUIContainer(() => sdkEditor.OnInspectorGUI()));
                return;
            }
            // A future SDK may rename its editors. The serialized fallback stays usable.
            inspectorRoot.Add(new IMGUIContainer(() =>
            {
                EditorGUILayout.HelpBox(T("The SDK inspector is unavailable. Showing serialized fields.",
                    "SDK Inspector를 찾지 못해 직렬화 필드를 표시합니다.", "SDK Inspectorが見つからないため、シリアライズされたフィールドを表示します。"), MessageType.Warning);
                DrawDefaultInspector();
            }));
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (inspectorRoot != null && target != null) BuildInspectorRoot();
        }

        public override void OnInspectorGUI()
        {
            if (target == null) return;
            int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
            if (lastLanguage != language) { Status = null; lastLanguage = language; }
            EnsureStyles();
            DrawHeader();
            if (DiNeExpressionInspectorSettings.HasCompetingInspectorPackage)
                EditorGUILayout.HelpBox(DiNeExpressionInspectorSettings.CompetingInspectorWarning, MessageType.Warning);
            serializedObject.Update();
            bool editable = DiNeExpressionUtility.CanEditAsset(target);
            using (new EditorGUI.DisabledScope(!editable)) DrawContents();
            serializedObject.ApplyModifiedProperties();
            if (!editable)
                EditorGUILayout.HelpBox(T("Editing is disabled during Play Mode and for read-only assets. Copy a package asset into Assets to edit it.",
                    "플레이 모드와 읽기 전용 에셋에서는 편집할 수 없습니다. 패키지 에셋은 Assets에 복사해 편집하세요.",
                    "プレイモードと読み取り専用アセットでは編集できません。パッケージのアセットはAssetsにコピーして編集してください。"), MessageType.Info);
            if (!string.IsNullOrEmpty(Status)) EditorGUILayout.HelpBox(Status, MessageType.Info);
        }

        protected static string T(string english, string korean, string japanese)
        {
            int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
            return language == 1 ? korean : language == 2 ? japanese : english;
        }

        protected static GUIContent C(string english, string korean, string japanese,
            string englishTip = null, string koreanTip = null, string japaneseTip = null)
        {
            string text = T(english, korean, japanese);
            return new GUIContent(text, englishTip == null ? text : T(englishTip, koreanTip, japaneseTip));
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(EditorStyles.label)
            {
                font = titleFont, fontSize = 36, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
            descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.80f, 0.80f, 0.80f, 1f) }
            };
            selectedButton = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold, normal = { textColor = Color.white }
            };
            ordinaryButton = new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = new Color(0.80f, 0.80f, 0.80f, 1f) }
            };
        }

        private void DrawHeader()
        {
            Color background = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(brandIcon, GUILayout.Width(72), GUILayout.Height(72));
                    GUILayout.Space(6);
                    GUILayout.Label(InspectorTitle, titleStyle, GUILayout.MinHeight(72), GUILayout.ExpandWidth(true));
                }
                GUILayout.Label(InspectorDescription, descriptionStyle);
            }
            GUI.backgroundColor = background;
            GUILayout.Space(5);
            using (new EditorGUILayout.HorizontalScope())
            {
                int current = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
                string[] languages = { "English", "한국어", "日本語" };
                for (int i = 0; i < languages.Length; i++)
                {
                    GUI.backgroundColor = i == current ? Mint : new Color(0.50f, 0.50f, 0.50f, 1f);
                    if (GUILayout.Button(new GUIContent(languages[i], languages[i]),
                        i == current ? selectedButton : ordinaryButton, GUILayout.Height(35)))
                    {
                        EditorPrefs.SetInt("DiNeLang", i);
                        Status = null;
                        Repaint();
                    }
                }
            }
            GUI.backgroundColor = background;
            GUILayout.Space(15);
        }

        protected static bool ActionButton(GUIContent content, bool primary = false, bool destructive = false)
        {
            Color previous = GUI.backgroundColor;
            if (primary) GUI.backgroundColor = Mint;
            if (destructive) GUI.backgroundColor = new Color(0.85f, 0.40f, 0.40f, 1f);
            bool pressed = GUILayout.Button(content, GUILayout.Height(24));
            GUI.backgroundColor = previous;
            return pressed;
        }

        protected void RunAction(Action action)
        {
            serializedObject.ApplyModifiedProperties();
            action();
            serializedObject.Update();
            Repaint();
            GUIUtility.ExitGUI();
        }

        protected void DrawAvatarContext()
        {
            EditorGUI.BeginChangeCheck();
            Avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(C("Avatar (lookup)", "아바타 (조회)", "アバター（参照）",
                "Reads Animator parameters without changing avatar references.", "아바타 참조를 변경하지 않고 Animator 파라미터를 조회합니다.",
                "アバターの参照を変更せずAnimatorパラメータを参照します。"), Avatar, typeof(VRCAvatarDescriptor), true);
            if (EditorGUI.EndChangeCheck()) RefreshAnimatorParameters();
            if (ActionButton(C("Refresh Animator list", "Animator 목록 새로고침", "Animatorリストを更新"))) RefreshAnimatorParameters();
        }

        protected void RefreshAnimatorParameters()
        {
            AnimatorParameters = DiNeExpressionUtility.GetAnimatorParameters(Avatar);
            Repaint();
        }

        protected void FindAvatar(Func<VRCAvatarDescriptor, bool> predicate)
        {
            var avatars = UnityEngine.Object.FindObjectsByType<VRCAvatarDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a => a.gameObject.scene.IsValid() && predicate(a)).ToArray();
            Avatar = avatars.Length == 1 ? avatars[0] : null;
            RefreshAnimatorParameters();
        }

        protected void DrawParameterName(SerializedProperty name, VRCExpressionParameters expressionParameters, bool floatsOnly = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(name, C("Parameter", "파라미터", "パラメータ"));
                if (GUILayout.Button(C("Select", "선택", "選択", "Select from expression or Animator parameters.",
                    "Expression 또는 Animator 파라미터에서 선택합니다.", "ExpressionまたはAnimatorパラメータから選択します。"), GUILayout.Width(54), GUILayout.Height(20)))
                {
                    string propertyPath = name.propertyPath;
                    var choices = new SortedSet<string>(StringComparer.Ordinal);
                    if (expressionParameters != null && expressionParameters.parameters != null)
                        foreach (var parameter in expressionParameters.parameters)
                            if (parameter != null && !string.IsNullOrWhiteSpace(parameter.name) &&
                                (!floatsOnly || parameter.valueType == VRCExpressionParameters.ValueType.Float)) choices.Add(parameter.name);
                    foreach (var parameter in AnimatorParameters)
                        if (!floatsOnly || parameter.type == AnimatorControllerParameterType.Float) choices.Add(parameter.name);
                    var menu = new GenericMenu();
                    menu.AddItem(C("None", "없음", "なし"), string.IsNullOrEmpty(name.stringValue), () => SetParameterName(propertyPath, ""));
                    foreach (string choice in choices)
                    {
                        string captured = choice;
                        menu.AddItem(new GUIContent(captured), name.stringValue == captured, () => SetParameterName(propertyPath, captured));
                    }
                    menu.ShowAsContext();
                }
            }
        }

        private void SetParameterName(string propertyPath, string value)
        {
            if (!DiNeExpressionUtility.CanEditAsset(target)) return;
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null) return;
            property.stringValue = value;
            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        protected static VRCExpressionParameters.ValueType ToExpressionType(AnimatorControllerParameterType type)
        {
            return type == AnimatorControllerParameterType.Float ? VRCExpressionParameters.ValueType.Float :
                type == AnimatorControllerParameterType.Int ? VRCExpressionParameters.ValueType.Int : VRCExpressionParameters.ValueType.Bool;
        }

        protected void DrawMissingParameter(string name, VRCExpressionParameters parameters, bool floatRequired)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var existing = parameters?.parameters?.FirstOrDefault(p => p != null && p.name == name);
            if (existing != null)
            {
                if (floatRequired && existing.valueType != VRCExpressionParameters.ValueType.Float)
                    EditorGUILayout.HelpBox(T("Puppet axes require Float parameters: ", "Puppet 축에는 Float 파라미터가 필요합니다: ",
                        "Puppetの軸にはFloatパラメータが必要です: ") + name, MessageType.Error);
                return;
            }
            EditorGUILayout.HelpBox(T("Missing from the selected expression parameters: ", "선택한 Expression Parameters에 없는 이름: ",
                "選択したExpression Parametersに存在しない名前: ") + name, MessageType.Warning);
            using (new EditorGUI.DisabledScope(parameters == null || !DiNeExpressionUtility.CanEditAsset(parameters)))
            {
                if (ActionButton(C("Add missing parameter", "누락된 파라미터 추가", "不足パラメータを追加")))
                    RunAction(() =>
                    {
                        var animator = AnimatorParameters.FirstOrDefault(p => p.name == name);
                        var type = floatRequired ? VRCExpressionParameters.ValueType.Float : animator != null ? ToExpressionType(animator.type) : VRCExpressionParameters.ValueType.Bool;
                        Status = DiNeExpressionUtility.AddParameter(parameters, name, type, T("Add Expression Parameter", "Expression 파라미터 추가", "Expressionパラメータを追加"))
                            ? T("Parameter added. Avatar references were preserved.", "파라미터를 추가했습니다. 아바타 참조는 보존됩니다.", "パラメータを追加しました。アバターの参照は維持されます。")
                            : T("Could not add the parameter. Check duplicates, access and memory budget.", "파라미터를 추가하지 못했습니다. 중복, 편집 권한과 메모리 한도를 확인하세요.", "追加できませんでした。重複、編集権限とメモリ上限を確認してください。 ");
                    });
            }
        }
    }
}
