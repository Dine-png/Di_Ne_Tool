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
    public abstract class DiNeExpressionInspectorBase : Editor
    {
        protected VRCAvatarDescriptor Avatar;
        protected List<AnimatorControllerParameter> AnimatorParameters = new List<AnimatorControllerParameter>();
        protected string Status;
        private Editor sdkEditor;
        private VisualElement inspectorRoot;
        private IMGUIContainer contentsContainer;
        private float lockedHeight;
        private bool lastMode, lastEditable;
        private int lastLanguage = -1;
        protected abstract string SdkEditorName { get; }
        protected abstract void DrawContents();

        protected virtual void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            DiNeExpressionInspectorSettings.WarnIfCompetingInspectorPackage();
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (sdkEditor != null) DestroyImmediate(sdkEditor);
        }

        public override VisualElement CreateInspectorGUI()
        {
            inspectorRoot = new VisualElement();
            BuildInspectorRoot();
            inspectorRoot.schedule.Execute(() =>
            {
                if (target == null) return;
                if (lastMode != DiNeExpressionInspectorSettings.UseDiNeInspector ||
                    lastEditable != DiNeExpressionUtility.CanEditAsset(target) || lastLanguage != Language)
                {
                    if (lastLanguage != Language) Status = null;
                    BuildInspectorRoot();
                }
            }).Every(250);
            return inspectorRoot;
        }

        private void BuildInspectorRoot()
        {
            inspectorRoot.Clear();
            contentsContainer = null;
            if (sdkEditor != null) DestroyImmediate(sdkEditor);
            sdkEditor = null;
            lastMode = DiNeExpressionInspectorSettings.UseDiNeInspector;
            lastEditable = DiNeExpressionUtility.CanEditAsset(target);
            lastLanguage = Language;
            if (lastMode)
            {
                var contents = new IMGUIContainer(OnInspectorGUI) { name = "DiNeExpressionInspectorIMGUI" };
                contents.style.minHeight = lockedHeight;
                contentsContainer = contents;
                contents.AddManipulator(new ContextualMenuManipulator(AppendLanguageMenu));
                inspectorRoot.Add(contents);
                return;
            }
            if (!lastEditable || !HasConsistentSdkData()) { AddSerializedFallback(); return; }
            string shortName = SdkEditorName.Substring(SdkEditorName.LastIndexOf('.') + 1);
            Type sdkType = TypeCache.GetTypesDerivedFrom<Editor>().FirstOrDefault(t =>
                t.Assembly.GetName().Name == "VRC.SDK3A.Editor" && (t.FullName == SdkEditorName || t.Name == shortName));
            if (sdkType == null) { AddSerializedFallback(); return; }
            VisualElement sdkRoot = CreateSdkRoot(sdkType);
            if (sdkRoot == null) sdkRoot = new IMGUIContainer(() => sdkEditor.OnInspectorGUI());
            inspectorRoot.Add(sdkRoot);
            sdkRoot.Bind(sdkEditor.serializedObject);
        }

        private void AppendLanguageMenu(ContextualMenuPopulateEvent evt)
        {
            foreach (int language in new[] { 0, 1, 2 })
            {
                int selected = language;
                string name = new[] { "English", "한국어", "日本語" }[language];
                evt.menu.AppendAction("Di Ne/" + T("Language", "언어", "言語") + "/" + name,
                    _ => { EditorPrefs.SetInt("DiNeLang", selected); Status = null; BuildInspectorRoot(); },
                    _ => Language == selected ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        private VisualElement CreateSdkRoot(Type sdkType)
        {
            string before = EditorJsonUtility.ToJson(target);
            bool wasDirty = EditorUtility.IsDirty(target);
            var parameters = target as VRCExpressionParameters;
            bool emptyFlag = parameters != null && parameters.isEmpty;
            var menu = target as VRCExpressionsMenu;
            var menuParameters = DiNeExpressionUtility.GetMenuParameters(menu);
            VRCExpressionParameters lookupGuard = null;
            try
            {
                // Prevent SDK initialization from filling empty assets or assigning avatar lookups.
                if (parameters != null && parameters.parameters.Length == 0) parameters.isEmpty = true;
                if (menu != null && menuParameters == null)
                {
                    lookupGuard = CreateInstance<VRCExpressionParameters>();
                    lookupGuard.hideFlags = HideFlags.HideAndDontSave;
                    DiNeExpressionUtility.SetMenuParameters(menu, lookupGuard);
                }
                try { sdkEditor = CreateEditor(targets, sdkType); }
                finally
                {
                    if (parameters != null) parameters.isEmpty = emptyFlag;
                    if (lookupGuard != null) DiNeExpressionUtility.SetMenuParameters(menu, menuParameters);
                }
                sdkEditor.serializedObject.Update();
                return sdkEditor.CreateInspectorGUI();
            }
            finally
            {
                if (lookupGuard != null) DestroyImmediate(lookupGuard);
                if (EditorJsonUtility.ToJson(target) != before) EditorJsonUtility.FromJsonOverwrite(before, target);
                if (!wasDirty) EditorUtility.ClearDirty(target);
                if (sdkEditor != null) sdkEditor.serializedObject.Update();
            }
        }

        private bool HasConsistentSdkData()
        {
            if (target is VRCExpressionParameters parameters)
                return parameters.parameters != null && parameters.parameters.All(p => p != null);
            if (!(target is VRCExpressionsMenu menu)) return true;
            if (menu.controls == null || menu.controls.Count > DiNeExpressionUtility.MenuLimit) return false;
            foreach (var control in menu.controls)
            {
                if (control == null || control.parameter == null) return false;
                int axes = control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ? 2 :
                    control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? 4 :
                    control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? 1 : 0;
                if (axes == 0) continue;
                int labels = axes == 1 ? 0 : 4;
                if (control.subParameters == null || control.subParameters.Length != axes || control.subParameters.Any(p => p == null) ||
                    (labels > 0 && (control.labels == null || control.labels.Length != labels)) ||
                    (labels == 0 && control.labels != null && control.labels.Length != 0)) return false;
            }
            return true;
        }

        private void AddSerializedFallback() { inspectorRoot.Add(new IMGUIContainer(OnInspectorGUI)); }

        public override void OnInspectorGUI()
        {
            if (target == null) return;
            bool editable = DiNeExpressionUtility.CanEditAsset(target);
            if (DiNeExpressionInspectorSettings.UseDiNeInspector)
            {
                bool isMenu = target is VRCExpressionsMenu;
                DiNeEditorUI.DrawHeader(isMenu ? "Expression Menu" : "Expression Parameters", isMenu
                    ? T("Edit expression controls, submenus and puppet parameters.",
                        "표정 메뉴 항목, 하위 메뉴와 Puppet 파라미터를 편집합니다.",
                        "表情メニューの項目、サブメニュー、Puppetパラメーターを編集します。")
                    : T("Edit expression parameters, merge assets and check memory usage.",
                        "표정 파라미터를 편집하고 에셋 병합과 메모리 사용량을 확인합니다.",
                        "表情パラメーターの編集、アセットの統合、メモリ使用量の確認。"));
                GUILayout.Space(5f);
                int previousLanguage = Language;
                if (DiNeEditorUI.DrawLanguageToolbar(previousLanguage) != previousLanguage)
                {
                    Status = null;
                    Repaint();
                }
                GUILayout.Space(15f);
                serializedObject.Update();
                using (new EditorGUI.DisabledScope(!editable)) DrawContents();
                serializedObject.ApplyModifiedProperties();
                if (DiNeExpressionInspectorSettings.HasCompetingInspectorPackage)
                    EditorGUILayout.HelpBox(DiNeExpressionInspectorSettings.CompetingInspectorWarning, MessageType.Warning);
                if (!editable)
                    EditorGUILayout.HelpBox(T("Editing is disabled during Play Mode and for read-only assets.",
                        "플레이 모드와 읽기 전용 에셋에서는 편집할 수 없습니다.", "プレイモードと読み取り専用アセットでは編集できません。"), MessageType.Info);
                if (!string.IsNullOrEmpty(Status)) EditorGUILayout.HelpBox(Status, MessageType.Info);
                return;
            }
            using (new EditorGUI.DisabledScope(!editable)) DrawDefaultInspector();
            EditorGUILayout.HelpBox(!editable
                ? T("Editing is disabled during Play Mode and for read-only assets.",
                    "플레이 모드와 읽기 전용 에셋에서는 편집할 수 없습니다.", "プレイモードと読み取り専用アセットでは編集できません。")
                : T("The SDK inspector cannot display these fields safely. Showing serialized fields.",
                    "SDK Inspector로 안전하게 표시할 수 없어 직렬화 필드를 표시합니다.", "SDK Inspectorで安全に表示できないため、シリアライズされたフィールドを表示します。"), MessageType.Info);
        }


        private void OnUndoRedo()
        {
            // Let the SDK's own Undo listeners finish before replacing their editor.
            if (inspectorRoot != null && target != null)
                inspectorRoot.schedule.Execute(() => { if (this != null && target != null) BuildInspectorRoot(); });
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (inspectorRoot != null && target != null) BuildInspectorRoot();
        }

        protected void ApplyAction(Action action)
        {
            if (!DiNeExpressionUtility.CanEditAsset(target)) return;
            LockHeight();
            serializedObject.ApplyModifiedProperties();
            if (sdkEditor != null) sdkEditor.serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            action();
            serializedObject.Update();
            if (inspectorRoot != null) BuildInspectorRoot();
            Repaint();
        }

        protected void RunGuiAction(Action action)
        {
            if (!DiNeExpressionUtility.CanEditAsset(target)) return;
            LockHeight();
            serializedObject.ApplyModifiedProperties();
            action();
            serializedObject.Update();
            Repaint();
            if (Event.current != null) GUIUtility.ExitGUI();
        }

        // A shrinking inspector clamps a scrolled view and shifts every row, so repeated delete clicks miss.
        private void LockHeight()
        {
            if (contentsContainer == null || float.IsNaN(contentsContainer.layout.height)) return;
            lockedHeight = Mathf.Max(lockedHeight, contentsContainer.layout.height);
            contentsContainer.style.minHeight = lockedHeight;
        }

        private static int Language => Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
        protected static string T(string english, string korean, string japanese)
        {
            return Language == 1 ? korean : Language == 2 ? japanese : english;
        }

        protected static GUIContent C(string english, string korean, string japanese,
            string englishTip = null, string koreanTip = null, string japaneseTip = null)
        {
            return new GUIContent(T(english, korean, japanese), englishTip == null
                ? T(english, korean, japanese) : T(englishTip, koreanTip, japaneseTip));
        }

        protected void RefreshAnimatorParameters()
        {
            AnimatorParameters = GetInspectorAnimatorParameters(Avatar);
            Repaint();
        }

        internal static List<AnimatorControllerParameter> GetInspectorAnimatorParameters(VRCAvatarDescriptor avatar)
        {
            var result = new List<AnimatorControllerParameter>();
            if (avatar == null) return result;
            var controllers = (avatar.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                .Concat(avatar.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                .Select(layer => layer.animatorController)
                .Concat(avatar.GetComponentsInChildren<Animator>(true).Select(animator => animator.runtimeAnimatorController));
            var visited = new HashSet<RuntimeAnimatorController>();
            foreach (var runtime in controllers)
            {
                RuntimeAnimatorController current = runtime;
                while (current is AnimatorOverrideController wrapper && visited.Add(current)) current = wrapper.runtimeAnimatorController;
                if (!(current is AnimatorController controller) || !visited.Add(current)) continue;
                foreach (var parameter in controller.parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.name) ||
                        result.Any(existing => existing.name == parameter.name && existing.type == parameter.type)) continue;
                    result.Add(parameter);
                }
            }
            // Keep conflicting types and Trigger names visible, as in SDK+; lookup is advisory.
            return result.OrderBy(parameter => parameter.name, StringComparer.Ordinal).ThenBy(parameter => parameter.type).ToList();
        }

        protected void DrawAvatarSelector(Action changed = null)
        {
            var avatars = UnityEngine.Object.FindObjectsByType<VRCAvatarDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a => a.gameObject.scene.IsValid()).ToArray();
            using (new EditorGUILayout.HorizontalScope(DiNeEditorUI.CardStyle))
            {
                var label = C("Active Avatar", "활성 아바타", "アクティブアバター",
                    "Parameter suggestions and warnings use this avatar. Its references are preserved.",
                    "이 아바타의 파라미터로 선택과 경고를 표시합니다. 아바타 참조는 유지됩니다.",
                    "このアバターのパラメータで候補と警告を表示します。参照は維持されます。");
                if (avatars.Length == 0)
                {
                    Avatar = null;
                    AnimatorParameters.Clear();
                    EditorGUILayout.LabelField(label, C("No Avatar Descriptors found", "아바타가 없습니다", "アバターがありません"));
                    return;
                }
                int current = Array.IndexOf(avatars, Avatar);
                if (current < 0) { Avatar = avatars[0]; RefreshAnimatorParameters(); current = 0; changed?.Invoke(); }
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUILayout.Popup(label, current, avatars.Select(a => a.gameObject.name).ToArray());
                if (EditorGUI.EndChangeCheck() && selected >= 0)
                {
                    Avatar = avatars[selected];
                    RefreshAnimatorParameters();
                    changed?.Invoke();
                }
            }
        }

        protected void ShowAddPlayableParameterMenu(string name, VRCExpressionParameters.ValueType type,
            float defaultValue, Rect position, Action changed = null)
        {
            var menu = new GenericMenu();
            if (Avatar != null)
                foreach (var layer in (Avatar.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
                    .Concat(Avatar.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>()))
                {
                    RuntimeAnimatorController runtime = layer.animatorController;
                    var visited = new HashSet<RuntimeAnimatorController>();
                    while (runtime is AnimatorOverrideController wrapper && visited.Add(runtime)) runtime = wrapper.runtimeAnimatorController;
                    var controller = runtime as AnimatorController;
                    if (controller == null) continue;
                    string label = layer.type.ToString();
                    bool allowed = DiNeExpressionUtility.CanEditAsset(controller) && !controller.parameters.Any(p => p.name == name);
                    if (!allowed) { menu.AddDisabledItem(new GUIContent(label)); continue; }
                    menu.AddItem(new GUIContent(label), false, () => ApplyAction(() =>
                    {
                        if (AddPlayableParameter(controller, name, type, defaultValue)) changed?.Invoke();
                    }));
                }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(C("No editable playable controllers", "편집할 컨트롤러가 없습니다", "編集可能なコントローラーがありません"));
            menu.DropDown(position);
        }

        internal bool AddPlayableParameter(AnimatorController controller, string name,
            VRCExpressionParameters.ValueType type, float defaultValue)
        {
            if (controller == null || string.IsNullOrWhiteSpace(name) || !DiNeExpressionUtility.CanEditAsset(controller) ||
                controller.parameters.Any(p => p.name == name)) return false;
            Undo.RecordObject(controller, T("Add Animator Parameter", "Animator 파라미터 추가", "Animatorパラメータを追加"));
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = type == VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int :
                    type == VRCExpressionParameters.ValueType.Float ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool,
                defaultFloat = defaultValue, defaultInt = Mathf.RoundToInt(defaultValue), defaultBool = defaultValue != 0
            });
            EditorUtility.SetDirty(controller);
            RefreshAnimatorParameters();
            return true;
        }

        protected void FindAvatar(Func<VRCAvatarDescriptor, bool> predicate)
        {
            var avatars = UnityEngine.Object.FindObjectsByType<VRCAvatarDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a => a.gameObject.scene.IsValid() && predicate(a)).ToArray();
            Avatar = avatars.Length == 1 ? avatars[0] : null;
            RefreshAnimatorParameters();
        }

        protected static VRCExpressionParameters.ValueType ToExpressionType(AnimatorControllerParameterType type)
        {
            return type == AnimatorControllerParameterType.Float ? VRCExpressionParameters.ValueType.Float :
                type == AnimatorControllerParameterType.Int ? VRCExpressionParameters.ValueType.Int : VRCExpressionParameters.ValueType.Bool;
        }
    }
}
