using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Control = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace DiNeTool.ExpressionEditor
{
    [CustomEditor(typeof(VRCExpressionsMenu))]
    public sealed class DiNeExpressionMenuEditor : DiNeExpressionInspectorBase
    {
        private static Control clipboard;
        private static readonly Stack<VRCExpressionsMenu> History = new Stack<VRCExpressionsMenu>();
        private VRCExpressionParameters parameterContext;
        private VRCExpressionsMenu moveDestination;
        private int expandedIndex;
        private VRCExpressionsMenu Menu => (VRCExpressionsMenu)target;
        protected override string SdkEditorName => "VRCExpressionsMenuEditor";
        protected override string InspectorTitle => T("Menu Editor", "메뉴 편집기", "メニュー編集");
        protected override string InspectorDescription => T("Edit controls, submenus and parameter links.",
            "메뉴 항목, 서브메뉴와 파라미터 연결을 편집합니다.", "メニュー項目、サブメニューとパラメータの関連を編集します。");

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Menu == null) return;
            FindAvatar(a => DiNeExpressionUtility.ContainsMenu(a.expressionsMenu, Menu));
            parameterContext = DiNeExpressionUtility.GetMenuParameters(Menu);
            if (parameterContext == null && Avatar != null) parameterContext = Avatar.expressionParameters;
        }

        protected override void DrawContents()
        {
            SerializedProperty controls = serializedObject.FindProperty("controls");
            if (controls == null)
            {
                EditorGUILayout.HelpBox(T("This SDK menu format is unsupported.", "이 SDK 메뉴 형식은 지원하지 않습니다.",
                    "このSDKメニュー形式はサポートされていません。"), MessageType.Error);
                return;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Parameter lookup", "파라미터 조회", "パラメータ参照"), EditorStyles.boldLabel);
                var previousAvatar = Avatar;
                DrawAvatarContext();
                if (Avatar != previousAvatar && Avatar != null) parameterContext = Avatar.expressionParameters;
                parameterContext = (VRCExpressionParameters)EditorGUILayout.ObjectField(C("Parameters (lookup)", "파라미터 (조회)", "パラメータ（参照）",
                    "Used for suggestions and explicit parameter additions; avatar references stay unchanged.",
                    "이름 제안과 명시적 파라미터 추가에 사용합니다. 아바타 참조는 바꾸지 않습니다.",
                    "名前の候補と明示的なパラメータ追加に使用します。アバターの参照は変更しません。"), parameterContext, typeof(VRCExpressionParameters), false);
            }
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Controls", "메뉴 항목", "メニュー項目") + $" ({controls.arraySize}/{DiNeExpressionUtility.MenuLimit})", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(controls.arraySize >= DiNeExpressionUtility.MenuLimit))
                    {
                        if (ActionButton(C("Add control", "항목 추가", "項目を追加"), true))
                            RunAction(() =>
                            {
                                if (DiNeExpressionUtility.AddControl(Menu, new Control { name = T("New Control", "새 항목", "新規項目"),
                                    type = Control.ControlType.Toggle, value = 1, parameter = new Control.Parameter(),
                                    subParameters = Array.Empty<Control.Parameter>(), labels = Array.Empty<Control.Label>() },
                                    T("Add Menu Control", "메뉴 항목 추가", "メニュー項目を追加"))) expandedIndex = Menu.controls.Count - 1;
                            });
                        using (new EditorGUI.DisabledScope(clipboard == null))
                            if (ActionButton(C("Paste", "붙여넣기", "貼り付け")))
                                RunAction(() =>
                                {
                                    if (DiNeExpressionUtility.AddControl(Menu, clipboard, T("Paste Menu Control", "메뉴 항목 붙여넣기", "メニュー項目を貼り付け"))) expandedIndex = Menu.controls.Count - 1;
                                });
                    }
                    using (new EditorGUI.DisabledScope(History.Count == 0))
                        if (ActionButton(C("Back", "뒤로", "戻る")))
                        {
                            serializedObject.ApplyModifiedProperties();
                            while (History.Count > 0)
                            {
                                var previous = History.Pop();
                                if (previous != null && previous != Menu) { Selection.activeObject = previous; GUIUtility.ExitGUI(); }
                            }
                        }
                }
                if (controls.arraySize == 0)
                    EditorGUILayout.HelpBox(T("Add a control or paste a copied control to begin.", "항목을 추가하거나 복사한 항목을 붙여넣으세요.",
                        "項目を追加するか、コピーした項目を貼り付けてください。"), MessageType.Info);
                moveDestination = (VRCExpressionsMenu)EditorGUILayout.ObjectField(C("Move destination", "이동할 메뉴", "移動先メニュー"), moveDestination, typeof(VRCExpressionsMenu), false);
            }
            GUILayout.Space(8);
            for (int i = 0; i < controls.arraySize; i++)
            {
                SerializedProperty control = controls.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string name = control.FindPropertyRelative("name").stringValue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var icon = control.FindPropertyRelative("icon").objectReferenceValue as Texture2D;
                        if (icon != null) GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                        bool expand = EditorGUILayout.Foldout(expandedIndex == i,
                            $"{i + 1}. " + (string.IsNullOrWhiteSpace(name) ? T("Unnamed", "이름 없음", "名前なし") : name), true);
                        if (expand) expandedIndex = i;
                        else if (expandedIndex == i) expandedIndex = -1;
                    }
                    if (expandedIndex == i) DrawControl(control, i, controls.arraySize);
                }
                GUILayout.Space(4);
            }
            DrawDiagnostics();
        }

        private void DrawControl(SerializedProperty control, int index, int count)
        {
            EditorGUILayout.PropertyField(control.FindPropertyRelative("name"), C("Name", "이름", "名前"));
            EditorGUILayout.PropertyField(control.FindPropertyRelative("icon"), C("Icon", "아이콘", "アイコン"));
            SerializedProperty typeProperty = control.FindPropertyRelative("type");
            var types = new[] { Control.ControlType.Button, Control.ControlType.Toggle, Control.ControlType.SubMenu,
                Control.ControlType.TwoAxisPuppet, Control.ControlType.FourAxisPuppet, Control.ControlType.RadialPuppet };
            string[] labels = { T("Button", "버튼", "ボタン"), T("Toggle", "토글", "トグル"), T("Submenu", "서브메뉴", "サブメニュー"),
                T("Two-axis puppet", "2축 Puppet", "2軸Puppet"), T("Four-axis puppet", "4축 Puppet", "4軸Puppet"), T("Radial puppet", "Radial Puppet", "Radial Puppet") };
            var type = (Control.ControlType)typeProperty.intValue;
            int current = Array.IndexOf(types, type);
            int chosen = EditorGUILayout.Popup(C("Type", "유형", "種類"), current, labels);
            if (chosen != current && chosen >= 0)
            {
                type = types[chosen];
                typeProperty.intValue = (int)type;
                ResizePuppetFields(control, type);
            }
            DrawParameterName(control.FindPropertyRelative("parameter").FindPropertyRelative("name"), parameterContext);
            string mainName = control.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue;
            DrawControlValue(control.FindPropertyRelative("value"), mainName);
            SerializedProperty style = control.FindPropertyRelative("style");
            if (style != null) EditorGUILayout.PropertyField(style, C("Style", "스타일", "スタイル"));
            if (type == Control.ControlType.SubMenu)
            {
                SerializedProperty subMenu = control.FindPropertyRelative("subMenu");
                EditorGUILayout.PropertyField(subMenu, C("Submenu", "서브메뉴", "サブメニュー"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(subMenu.objectReferenceValue == null || subMenu.objectReferenceValue == Menu))
                        if (ActionButton(C("Open submenu", "서브메뉴 열기", "サブメニューを開く")))
                        {
                            serializedObject.ApplyModifiedProperties();
                            History.Push(Menu);
                            Selection.activeObject = subMenu.objectReferenceValue;
                            GUIUtility.ExitGUI();
                        }
                    if (ActionButton(C("Create submenu", "서브메뉴 만들기", "サブメニューを作成"))) RunAction(() => CreateSubmenu(index));
                }
            }
            int axisCount = AxisCount(type);
            if (axisCount > 0) DrawPuppetFields(control, type, axisCount);
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (ActionButton(C("Copy", "복사", "コピー")))
                {
                    serializedObject.ApplyModifiedProperties();
                    clipboard = DiNeExpressionUtility.CloneControl(Menu.controls[index]);
                    Status = T("Control copied. The submenu remains a shared reference.", "항목을 복사했습니다. 서브메뉴는 기존 참조를 공유합니다.",
                        "項目をコピーしました。サブメニューは既存の参照を共有します。");
                }
                using (new EditorGUI.DisabledScope(count >= DiNeExpressionUtility.MenuLimit))
                    if (ActionButton(C("Duplicate", "복제", "複製")))
                        RunAction(() => DiNeExpressionUtility.AddControl(Menu, Menu.controls[index], T("Duplicate Menu Control", "메뉴 항목 복제", "メニュー項目を複製")));
                if (ActionButton(C("Delete", "삭제", "削除"), destructive: true))
                    RunAction(() => DiNeExpressionUtility.RemoveControl(Menu, index, T("Delete Menu Control", "메뉴 항목 삭제", "メニュー項目を削除")));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(index == 0))
                    if (ActionButton(C("Up", "위로", "上へ")))
                        RunAction(() => { if (DiNeExpressionUtility.MoveControl(Menu, index, index - 1, T("Reorder Menu", "메뉴 순서 변경", "メニューの順序を変更"))) expandedIndex = index - 1; });
                using (new EditorGUI.DisabledScope(index == count - 1))
                    if (ActionButton(C("Down", "아래로", "下へ")))
                        RunAction(() => { if (DiNeExpressionUtility.MoveControl(Menu, index, index + 1, T("Reorder Menu", "메뉴 순서 변경", "メニューの順序を変更"))) expandedIndex = index + 1; });
                using (new EditorGUI.DisabledScope(moveDestination == null || moveDestination == Menu || !DiNeExpressionUtility.CanEditAsset(moveDestination)))
                    if (ActionButton(C("Move", "이동", "移動", "Moves this control into the destination menu without copying the submenu asset.",
                        "서브메뉴 에셋을 복제하지 않고 이 항목을 지정한 메뉴로 옮깁니다.", "サブメニューアセットを複製せず、この項目を移動先に移します。")))
                        RunAction(() => Status = DiNeExpressionUtility.MoveControlToMenu(Menu, index, moveDestination, T("Move Menu Control", "메뉴 항목 이동", "メニュー項目を移動"))
                            ? T("Control moved.", "항목을 이동했습니다.", "項目を移動しました。")
                            : T("Move cancelled. The destination is full or unavailable.", "이동하지 않았습니다. 대상 메뉴가 가득 찼거나 편집할 수 없습니다.", "移動を中止しました。移動先が満杯、または編集できません。"));
            }
        }

        private void DrawControlValue(SerializedProperty value, string name)
        {
            var parameter = parameterContext?.parameters?.FirstOrDefault(p => p != null && p.name == name);
            if (parameter != null && parameter.valueType == VRCExpressionParameters.ValueType.Bool)
            {
                EditorGUI.BeginChangeCheck();
                bool state = EditorGUILayout.Toggle(C("Value", "값", "値"), value.floatValue != 0);
                if (EditorGUI.EndChangeCheck()) value.floatValue = state ? 1 : 0;
            }
            else if (parameter != null && parameter.valueType == VRCExpressionParameters.ValueType.Int)
            {
                EditorGUI.BeginChangeCheck();
                int state = EditorGUILayout.IntField(C("Value", "값", "値"), Mathf.RoundToInt(value.floatValue));
                if (EditorGUI.EndChangeCheck()) value.floatValue = Mathf.Clamp(state, 0, 255);
            }
            else EditorGUILayout.PropertyField(value, C("Value", "값", "値"));
        }

        private static int AxisCount(Control.ControlType type)
        {
            return type == Control.ControlType.TwoAxisPuppet ? 2 : type == Control.ControlType.FourAxisPuppet ? 4 :
                type == Control.ControlType.RadialPuppet ? 1 : 0;
        }

        private static void ResizePuppetFields(SerializedProperty control, Control.ControlType type)
        {
            int count = AxisCount(type);
            SerializedProperty axes = control.FindPropertyRelative("subParameters");
            int previousAxes = axes.arraySize;
            axes.arraySize = count;
            for (int i = previousAxes; i < count; i++) axes.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = "";
            SerializedProperty labels = control.FindPropertyRelative("labels");
            int previousLabels = labels.arraySize;
            labels.arraySize = type == Control.ControlType.TwoAxisPuppet || type == Control.ControlType.FourAxisPuppet ? 4 : 0;
            for (int i = previousLabels; i < labels.arraySize; i++)
            {
                labels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = "";
                labels.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = null;
            }
        }

        private void DrawPuppetFields(SerializedProperty control, Control.ControlType type, int expected)
        {
            SerializedProperty parameters = control.FindPropertyRelative("subParameters");
            SerializedProperty labels = control.FindPropertyRelative("labels");
            int expectedLabels = type == Control.ControlType.TwoAxisPuppet || type == Control.ControlType.FourAxisPuppet ? 4 : 0;
            if (parameters.arraySize != expected || labels.arraySize != expectedLabels)
            {
                EditorGUILayout.HelpBox(T("The puppet axis or label count does not match the control type.", "Puppet 축 또는 라벨 개수가 항목 유형과 맞지 않습니다.",
                    "Puppetの軸数またはラベル数が項目の種類と一致しません。"), MessageType.Warning);
                if (ActionButton(C("Prepare puppet fields", "Puppet 필드 구성", "Puppetフィールドを構成"))) ResizePuppetFields(control, type);
            }
            for (int i = 0; i < parameters.arraySize; i++)
            {
                string axisName = type == Control.ControlType.TwoAxisPuppet
                    ? i == 0 ? T("Horizontal", "좌우", "水平") : T("Vertical", "상하", "垂直")
                    : type == Control.ControlType.FourAxisPuppet ? DirectionName(i) : T("Radial", "Radial", "Radial");
                GUILayout.Label(axisName, EditorStyles.boldLabel);
                DrawParameterName(parameters.GetArrayElementAtIndex(i).FindPropertyRelative("name"), parameterContext, true);
            }
            for (int i = 0; i < labels.arraySize; i++)
            {
                SerializedProperty label = labels.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(label.FindPropertyRelative("name"), new GUIContent(DirectionName(i)));
                EditorGUILayout.PropertyField(label.FindPropertyRelative("icon"), C("Label icon", "라벨 아이콘", "ラベルアイコン"));
            }
        }

        private static string DirectionName(int index)
        {
            return index == 0 ? T("Up", "위", "上") : index == 1 ? T("Right", "오른쪽", "右") :
                index == 2 ? T("Down", "아래", "下") : index == 3 ? T("Left", "왼쪽", "左") : T("Label", "라벨", "ラベル") + " " + (index + 1);
        }

        private void CreateSubmenu(int index)
        {
            if (!DiNeExpressionUtility.CanEditAsset(Menu) || index < 0 || index >= Menu.controls.Count) return;
            string parentPath = AssetDatabase.GetAssetPath(Menu);
            string directory = string.IsNullOrEmpty(parentPath) ? "Assets" : Path.GetDirectoryName(parentPath)?.Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject(T("Create Submenu", "서브메뉴 만들기", "サブメニューを作成"), "SubMenu", "asset",
                T("Choose where to save the new submenu.", "새 서브메뉴를 저장할 위치를 선택하세요.", "新しいサブメニューの保存先を選択してください。"), directory);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var submenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            submenu.controls = new List<Control>();
            DiNeExpressionUtility.SetMenuParameters(submenu, parameterContext);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            string undoName = T("Create Submenu", "서브메뉴 만들기", "サブメニューを作成");
            Undo.SetCurrentGroupName(undoName);
            AssetDatabase.CreateAsset(submenu, path);
            Undo.RegisterCreatedObjectUndo(submenu, undoName);
            Undo.RecordObject(Menu, undoName);
            Menu.controls[index].subMenu = submenu;
            Menu.controls[index].type = Control.ControlType.SubMenu;
            EditorUtility.SetDirty(Menu);
            Undo.CollapseUndoOperations(group);
            EditorGUIUtility.PingObject(submenu);
            Status = T("Submenu created. Undo restores the previous control.", "서브메뉴를 만들었습니다. Undo로 이전 항목을 복원할 수 있습니다.", "サブメニューを作成しました。Undoで元の項目を復元できます。");
        }

        private void DrawDiagnostics()
        {
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Diagnostics", "진단", "診断"), EditorStyles.boldLabel);
                if (Menu.controls == null || Menu.controls.Count == 0) return;
                if (Menu.controls.Count > DiNeExpressionUtility.MenuLimit)
                    EditorGUILayout.HelpBox(T("A VRChat menu supports at most 8 controls.", "VRChat 메뉴에는 최대 8개 항목을 넣을 수 있습니다.", "VRChatメニューは最大8項目に対応します。"), MessageType.Error);
                if (parameterContext == null)
                    EditorGUILayout.HelpBox(T("Choose expression parameters above to check parameter links.", "파라미터 연결을 검사하려면 위에서 Expression Parameters를 지정하세요.",
                        "パラメータの関連を確認するには、上でExpression Parametersを指定してください。"), MessageType.Info);
                foreach (var control in Menu.controls.Where(c => c != null))
                {
                    if (control.type == Control.ControlType.SubMenu && control.subMenu == null)
                        EditorGUILayout.HelpBox(T("Submenu is missing: ", "서브메뉴가 지정되지 않았습니다: ", "サブメニューが未指定です: ") + control.name, MessageType.Warning);
                    if ((control.type == Control.ControlType.Toggle || control.type == Control.ControlType.Button) && string.IsNullOrWhiteSpace(control.parameter?.name))
                        EditorGUILayout.HelpBox(T("Control has no parameter: ", "항목에 파라미터가 없습니다: ", "項目にパラメータがありません: ") + control.name, MessageType.Warning);
                    if (parameterContext != null)
                    {
                        DrawMissingParameter(control.parameter?.name, parameterContext, false);
                        if (AxisCount(control.type) > 0 && control.subParameters != null)
                            foreach (var axis in control.subParameters) DrawMissingParameter(axis?.name, parameterContext, true);
                    }
                }
            }
        }
    }
}
