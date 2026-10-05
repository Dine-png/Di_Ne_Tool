using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace DiNeTool.ExpressionEditor
{
    [CustomEditor(typeof(VRCExpressionParameters))]
    public sealed class DiNeExpressionParametersEditor : DiNeExpressionInspectorBase
    {
        private const float TableMinimumWidth = 620;
        private SerializedProperty parameterProperty;
        private ReorderableList parameterList;
        private Vector2 tableScroll;
        private string search = "";
        private bool searchVisible;
        private bool focusSearch;
        private GUIStyle centered, typeHint, listButton;
        private int drawnParameterCount = -1;
        private bool restoreListFocus;
        private ParameterDropdown parameterDropdown;
        internal readonly Dictionary<string, Rect> Geometry = new Dictionary<string, Rect>();
        private VRCExpressionParameters Parameters => (VRCExpressionParameters)target;
        protected override string SdkEditorName => "VRC.SDK3.Editor.VRCExpressionParametersEditor";

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Parameters != null) FindAvatar(avatar => avatar.expressionParameters == Parameters);
            BuildParameterList();
            Undo.undoRedoPerformed += RefreshAfterUndo;
        }

        protected override void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshAfterUndo;
            base.OnDisable();
        }

        private void RefreshAfterUndo()
        {
            if (target == null) return;
            serializedObject.Update();
            BuildParameterList();
            RefreshAnimatorParameters();
        }

        protected override void DrawContents()
        {
            Geometry.Clear();
            EnsureStyles();
            DrawAvatarSelector();
            if (parameterList == null || parameterProperty == null || parameterProperty.arraySize != drawnParameterCount)
                BuildParameterList();
            if (parameterProperty == null) return;
            parameterList.draggable = DiNeExpressionUtility.CanEditAsset(target);
            if (restoreListFocus && Event.current != null)
            {
                parameterList.GrabKeyboardFocus();
                restoreListFocus = false;
            }
            HandleKeyboard();
            // The requested compact SDK+ arrangement intentionally omits the large tool identity block.
            // Horizontal scrolling preserves its single-row controls in narrow Inspector panels.
            if (EditorGUIUtility.currentViewWidth < TableMinimumWidth + 32)
            {
                tableScroll = EditorGUILayout.BeginScrollView(tableScroll, true, false,
                    GUILayout.Height(parameterList.GetHeight() + 18));
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(TableMinimumWidth));
                parameterList.DoLayoutList();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
            }
            else parameterList.DoLayoutList();

            if (HasCleanupCandidates())
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(C("Cleanup invalid, blank and duplicate parameters",
                        "유효하지 않은 항목, 빈 이름과 중복 파라미터 정리", "無効・空の名前・重複パラメータを整理"), GUILayout.ExpandWidth(true));
                    if (GUILayout.Button(C("Cleanup", "정리", "整理"), GUILayout.ExpandWidth(true)))
                        RunGuiAction(CleanupParameters);
                }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                var source = (VRCExpressionParameters)EditorGUILayout.ObjectField(C("Merge Parameters", "파라미터 병합", "パラメータを統合",
                    "Selecting an asset appends copies of all its parameters, including duplicate names.",
                    "에셋을 선택하면 중복 이름을 포함한 모든 파라미터의 복사본을 목록 끝에 추가합니다.",
                    "アセットを選択すると、重複した名前を含む全パラメータのコピーを一覧の末尾に追加します。"),
                    null, typeof(VRCExpressionParameters), false);
                Geometry["MergeField"] = GUILayoutUtility.GetLastRect();
                if (EditorGUI.EndChangeCheck() && source != null)
                    RunGuiAction(() => MergeParameters(source));
            }
            DrawMemory();
        }

        private void EnsureStyles()
        {
            if (centered != null) return;
            centered = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
            typeHint = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleRight };
            listButton = GUI.skin.FindStyle("RL FooterButton") ?? EditorStyles.miniButton;
        }

        private void BuildParameterList()
        {
            if (target == null) return;
            parameterProperty = serializedObject.FindProperty("parameters");
            if (parameterProperty == null) return;
            int selected = parameterList?.index ?? -1;
            restoreListFocus |= parameterList != null && parameterList.HasKeyboardControl();
            drawnParameterCount = parameterProperty.arraySize;
            parameterList = new ReorderableList(serializedObject, parameterProperty, true, true, true, false)
            {
                drawHeaderCallback = DrawTableHeader,
                drawElementCallback = DrawParameterRow,
                drawFooterCallback = DrawTableFooter,
                drawNoneElementCallback = rect => EditorGUI.LabelField(rect,
                    T("List is empty", "목록이 비어 있습니다", "一覧は空です")),
                onAddCallback = _ => RunGuiAction(AddParameter),
                onReorderCallback = _ =>
                {
                    if (!DiNeExpressionUtility.CanEditAsset(target)) { serializedObject.Update(); return; }
                    UpdateEmptyFlag();
                    serializedObject.ApplyModifiedProperties();
                }
            };
            parameterList.index = Math.Min(selected, parameterProperty.arraySize - 1);
        }

        private void DrawTableHeader(Rect rect)
        {
            rect.y += 1;
            rect.height = 18;
            Geometry["ParameterHeader"] = rect;
            var columns = GetHeaderColumns(rect);
            EditorGUI.LabelField(columns.Name, C("Name", "이름", "名前",
                "Must match the playable controller parameter name. Case sensitive.",
                "Playable Controller의 파라미터 이름과 일치해야 합니다. 대소문자를 구분합니다.",
                "Playable Controllerのパラメータ名と一致させてください。大文字と小文字を区別します。"), centered);
            if (searchVisible || !string.IsNullOrEmpty(search))
            {
                var field = columns.Name;
                field.width -= 20;
                GUI.SetNextControlName("DiNeParameterSearch");
                search = EditorGUI.TextField(field, search, EditorStyles.toolbarSearchField);
                if (focusSearch)
                {
                    EditorGUI.FocusTextInControl("DiNeParameterSearch");
                    focusSearch = false;
                }
                var clear = new Rect(field.xMax + 1, field.y, 19, field.height);
                if (GUI.Button(clear, C("×", "×", "×", "Clear search", "검색 지우기", "検索を消去"), EditorStyles.miniButton))
                {
                    search = "";
                    searchVisible = false;
                    GUI.FocusControl(null);
                }
            }
            else
            {
                var find = new Rect(columns.Name.center.x - 41, columns.Name.y, 18, 18);
                var icon = new GUIContent(EditorGUIUtility.IconContent("Search Icon"))
                {
                    tooltip = T("Search parameters (Ctrl+F)", "파라미터 검색 (Ctrl+F)", "パラメータを検索 (Ctrl+F)")
                };
                if (GUI.Button(find, icon, GUIStyle.none))
                {
                    searchVisible = true;
                    focusSearch = true;
                    Repaint();
                }
            }
            EditorGUI.LabelField(columns.Type, C("Type", "유형", "種類"), centered);
            EditorGUI.LabelField(columns.Default, C("Default", "기본값", "初期値"), centered);
            EditorGUI.LabelField(columns.Saved, C("Saved", "저장", "保存",
                "Keep the value when loading the avatar or changing worlds.",
                "아바타를 불러오거나 월드를 바꿀 때 값을 유지합니다.", "アバターの読み込みやワールドの変更時に値を維持します。"), centered);
            EditorGUI.LabelField(columns.Synced, C("Synced", "동기화", "同期",
                "Send the value to remote users. Synced parameters use memory.",
                "다른 사용자에게 값을 전송합니다. 동기화 파라미터는 메모리를 사용합니다.", "他のユーザーに値を送信します。同期パラメータはメモリを使用します。"), centered);
        }

        private void DrawTableFooter(Rect rect)
        {
            // Unity's native footer places the single plus button inside its right-side tab.
            Geometry["ParameterAdd"] = new Rect(rect.xMax - 39, rect.y, 25, 16);
            ReorderableList.defaultBehaviours.DrawFooter(rect, parameterList);
        }

        private void DrawParameterRow(Rect rect, int index, bool active, bool focused)
        {
            if (index < 0 || index >= parameterProperty.arraySize) return;
            var row = parameterProperty.GetArrayElementAtIndex(index);
            var name = row.FindPropertyRelative("name");
            rect.y += 1;
            rect.height = 18;
            Geometry["ParameterRow" + index] = rect;
            var type = row.FindPropertyRelative("valueType");
            var defaultValue = row.FindPropertyRelative("defaultValue");
            if (name == null || type == null || defaultValue == null || IsNullEntry(index))
            {
                EditorGUI.LabelField(rect, T("Invalid parameter", "유효하지 않은 파라미터", "無効なパラメータ"));
                var remove = GetColumns(rect, false).Delete;
                Geometry["ParameterDelete" + index] = remove;
                if (GUI.Button(remove, C("−", "−", "−", "Delete parameter", "파라미터 삭제", "パラメータを削除"), GUIStyle.none))
                    RunGuiAction(() => DeleteParameter(index));
                return;
            }
            string parameterName = name.stringValue;
            bool blank = string.IsNullOrWhiteSpace(parameterName);
            var matched = AnimatorParameters.FirstOrDefault(parameter => parameter.name == parameterName);
            bool missing = Avatar != null && matched == null && !blank;
            bool duplicate = !blank && ContainsName(parameterName, index);
            rect.height = 18;
            var columns = GetColumns(rect, missing);
            Geometry["ParameterDelete" + index] = columns.Delete;
            if (missing) Geometry["ParameterControllerAdd" + index] = columns.Add;
            using (new EditorGUI.DisabledScope(!MatchesSearch(parameterName)))
            {
                EditorGUI.PropertyField(columns.Name, name, GUIContent.none);
                GUI.Label(columns.Name, matched == null ? "(?)" : "(" + matched.type + ")", typeHint);
                var suggestions = AnimatorParameters.Where(parameter => !ContainsName(parameter.name)).ToArray();
                using (new EditorGUI.DisabledScope(suggestions.Length == 0))
                    if (GUI.Button(columns.Dropdown, GUIContent.none, EditorStyles.popup))
                    {
                        int chosenIndex = index;
                        parameterDropdown = new ParameterDropdown(suggestions,
                            T("Parameters", "파라미터", "パラメータ"), selected => ApplyAction(() => SetParameterName(chosenIndex, selected)));
                        parameterDropdown.Show(columns.Name);
                    }

                if (missing && GUI.Button(columns.Add, C("Add", "추가", "追加",
                    "Add this parameter to a playable controller.", "이 파라미터를 Playable Controller에 추가합니다.",
                    "このパラメータをPlayable Controllerに追加します。"), EditorStyles.popup))
                    ShowAddPlayableParameterMenu(parameterName, (VRCExpressionParameters.ValueType)type.intValue,
                        defaultValue.floatValue, columns.Add, RefreshAnimatorParameters);
                if (blank || duplicate || missing)
                {
                    string message = blank ? T("Blank parameter", "빈 파라미터 이름", "空のパラメータ名")
                        : duplicate ? T("Duplicate parameter name. This may cause issues.", "중복된 파라미터 이름입니다. 문제가 발생할 수 있습니다.", "パラメータ名が重複しています。問題が発生する可能性があります。")
                        : T("Not found in a playable controller of the active avatar.", "활성 아바타의 Playable Controller에서 찾지 못한 파라미터입니다.",
                            "選択中のアバターのPlayable Controllerにないパラメータです。");
                    GUI.Label(columns.Warning, new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml")) { tooltip = message });
                }
                EditorGUI.PropertyField(columns.Type, type, GUIContent.none);
                EditorGUI.BeginChangeCheck();
                float value;
                switch ((VRCExpressionParameters.ValueType)type.intValue)
                {
                    case VRCExpressionParameters.ValueType.Bool:
                        value = EditorGUI.Popup(columns.Default, defaultValue.floatValue == 0 ? 0 : 1,
                            new[] { T("False", "False", "False"), T("True", "True", "True") });
                        break;
                    default:
                        value = EditorGUI.FloatField(columns.Default, defaultValue.floatValue);
                        break;
                }
                if (EditorGUI.EndChangeCheck()) defaultValue.floatValue = value;
                DrawCenteredToggle(columns.Saved, row.FindPropertyRelative("saved"));
                DrawCenteredToggle(columns.Synced, row.FindPropertyRelative("networkSynced"));
                var remove = new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus"))
                {
                    tooltip = T("Delete parameter", "파라미터 삭제", "パラメータを削除")
                };
                if (GUI.Button(columns.Delete, remove, listButton)) RunGuiAction(() => DeleteParameter(index));
            }
            var handle = new Rect(rect.x - 20, rect.y, 20, rect.height);
            if (GUI.enabled && MatchesSearch(parameterName) && Event.current.type == EventType.ContextClick && handle.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                int chosenIndex = index;
                var menu = new GenericMenu();
                menu.AddItem(C("Duplicate", "복제", "複製"), false, () => ApplyAction(() => DuplicateParameter(chosenIndex)));
                menu.AddSeparator("");
                menu.AddItem(C("Delete", "삭제", "削除"), false, () => ApplyAction(() => DeleteParameter(chosenIndex)));
                menu.ShowAsContext();
            }
        }

        private void DrawMemory()
        {
            int cost = 0;
            for (int index = 0; index < parameterProperty.arraySize; index++)
            {
                if (IsNullEntry(index)) { cost += 8; continue; }
                var parameter = parameterProperty.GetArrayElementAtIndex(index);
                var synced = parameter.FindPropertyRelative("networkSynced");
                var type = parameter.FindPropertyRelative("valueType");
                if (synced != null && !synced.boolValue) continue;
                cost += type != null && type.intValue == (int)VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Total Memory", "전체 메모리", "合計メモリ"), centered);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(cost + " / " + VRCExpressionParameters.MAX_PARAMETER_COST);
                    if (cost > VRCExpressionParameters.MAX_PARAMETER_COST)
                        GUILayout.Label(new GUIContent(EditorGUIUtility.IconContent("console.erroricon.sml"))
                        {
                            tooltip = T("Synced memory exceeds the SDK limit.", "동기화 메모리가 SDK 한도를 초과했습니다.", "同期メモリがSDK上限を超えています。")
                        }, GUILayout.Width(18));
                    GUILayout.FlexibleSpace();
                }
                Geometry["Memory"] = GUILayoutUtility.GetLastRect();
            }
        }

        private void HandleKeyboard()
        {
            var current = Event.current;
            if (current == null || !DiNeExpressionUtility.CanEditAsset(target)) return;
            if (current.type == EventType.KeyDown && (current.control || current.command) && current.keyCode == KeyCode.F)
            {
                searchVisible = true;
                focusSearch = true;
                current.Use();
            }
            if (!parameterList.HasKeyboardControl() || EditorGUIUtility.editingTextField || parameterList.index < 0 ||
                parameterList.index >= parameterProperty.arraySize) return;
            bool duplicate = current.commandName == "Duplicate";
            bool delete = current.commandName == "Delete" || current.commandName == "SoftDelete";
            if (current.type == EventType.ValidateCommand && (duplicate || delete)) { current.Use(); return; }
            if (current.type == EventType.KeyDown)
            {
                duplicate = (current.control || current.command) && current.keyCode == KeyCode.D;
                delete = current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace;
            }
            else if (current.type != EventType.ExecuteCommand) return;
            if (!duplicate && !delete) return;
            int index = parameterList.index;
            current.Use();
            RunGuiAction(() => { if (duplicate) DuplicateParameter(index); else DeleteParameter(index); });
        }

        private bool MatchesSearch(string name)
        {
            if (string.IsNullOrEmpty(search)) return true;
            try { return Regex.IsMatch(name ?? "", search, RegexOptions.IgnoreCase); }
            catch (ArgumentException) { return (name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        private bool ContainsName(string name, int skip = -1)
        {
            for (int index = 0; index < parameterProperty.arraySize; index++)
            {
                if (index == skip) continue;
                var existing = parameterProperty.GetArrayElementAtIndex(index).FindPropertyRelative("name");
                if (existing != null && existing.stringValue == name) return true;
            }
            return false;
        }

        private bool HasCleanupCandidates()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < parameterProperty.arraySize; index++)
            {
                var name = parameterProperty.GetArrayElementAtIndex(index).FindPropertyRelative("name");
                if (name == null || IsNullEntry(index) || string.IsNullOrWhiteSpace(name.stringValue) || !names.Add(name.stringValue) ||
                    (Avatar != null && AnimatorParameters.All(parameter => parameter.name != name.stringValue))) return true;
            }
            return false;
        }

        private void AddParameter()
        {
            var current = (Parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()).ToList();
            var parameter = current.Count > 0 && current[current.Count - 1] != null
                ? DiNeExpressionUtility.CloneParameter(current[current.Count - 1]) : new VRCExpressionParameters.Parameter();
            parameter.name = GetUniqueName(parameter.name, current);
            current.Add(parameter);
            WriteParameters(current, T("Add Expression Parameter", "Expression 파라미터 추가", "Expressionパラメータを追加"));
            parameterList.index = current.Count - 1;
        }

        private void DuplicateParameter(int index)
        {
            var current = (Parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()).ToList();
            if (index < 0 || index >= current.Count || current[index] == null) return;
            var copy = DiNeExpressionUtility.CloneParameter(current[index]);
            copy.name = GetUniqueName(copy.name, current);
            current.Insert(index + 1, copy);
            WriteParameters(current, T("Duplicate Expression Parameter", "Expression 파라미터 복제", "Expressionパラメータを複製"));
            parameterList.index = index + 1;
        }

        private void DeleteParameter(int index)
        {
            var current = (Parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()).ToList();
            if (index < 0 || index >= current.Count) return;
            current.RemoveAt(index);
            WriteParameters(current, T("Delete Expression Parameter", "Expression 파라미터 삭제", "Expressionパラメータを削除"));
            parameterList.index = Math.Min(index, current.Count - 1);
        }

        private void MergeParameters(VRCExpressionParameters source)
        {
            if (source == null) return;
            var current = (Parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()).ToList();
            current.AddRange((source.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()).Select(DiNeExpressionUtility.CloneParameter));
            WriteParameters(current, T("Merge Expression Parameters", "Expression 파라미터 병합", "Expressionパラメータを統合"));
        }

        private void CleanupParameters()
        {
            RefreshAnimatorParameters();
            var known = new HashSet<string>(AnimatorParameters.Select(parameter => parameter.name), StringComparer.Ordinal);
            var retained = new List<VRCExpressionParameters.Parameter>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in Parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
            {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.name) ||
                    (Avatar != null && !known.Contains(parameter.name)) || !names.Add(parameter.name)) continue;
                retained.Add(parameter);
            }
            WriteParameters(retained, T("Clean Expression Parameters", "Expression 파라미터 정리", "Expressionパラメータを整理"));
        }

        private void SetParameterName(int index, string name)
        {
            if (!DiNeExpressionUtility.CanEditAsset(target)) return;
            serializedObject.Update();
            parameterProperty = serializedObject.FindProperty("parameters");
            if (index < 0 || index >= parameterProperty.arraySize) return;
            parameterProperty.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = name;
            serializedObject.ApplyModifiedProperties();
        }

        private void WriteParameters(List<VRCExpressionParameters.Parameter> parameters, string undoName)
        {
            if (!DiNeExpressionUtility.CanEditAsset(target)) return;
            Undo.RecordObject(Parameters, undoName);
            Parameters.parameters = parameters.ToArray();
            Parameters.isEmpty = parameters.Count == 0;
            EditorUtility.SetDirty(Parameters);
            serializedObject.Update();
            BuildParameterList();
        }

        private void UpdateEmptyFlag()
        {
            var empty = serializedObject.FindProperty("isEmpty");
            if (empty != null) empty.boolValue = parameterProperty.arraySize == 0;
        }

        private static string GetUniqueName(string original, List<VRCExpressionParameters.Parameter> parameters)
        {
            string name = original ?? "";
            var existing = new HashSet<string>(parameters.Where(parameter => parameter != null).Select(parameter => parameter.name ?? ""), StringComparer.Ordinal);
            if (!existing.Contains(name)) return name;
            int digit = name.Length;
            while (digit > 0 && char.IsDigit(name[digit - 1])) digit--;
            string stem = digit == name.Length ? name.TrimEnd() + " " : name.Substring(0, digit);
            int number = 1;
            int padding = digit == name.Length ? 1 : name.Length - digit;
            if (digit < name.Length && int.TryParse(name.Substring(digit), out int found)) number = found;
            string candidate;
            do { candidate = stem + number.ToString("D" + padding); number++; }
            while (existing.Contains(candidate));
            return candidate;
        }

        private static void DrawCenteredToggle(Rect rect, SerializedProperty property)
        {
            if (property == null) return;
            rect.x += (rect.width - 18) / 2;
            rect.width = 18;
            EditorGUI.PropertyField(rect, property, GUIContent.none);
        }

        private static Columns GetColumns(Rect rect, bool add)
        {
            var columns = new Columns();
            float right = rect.xMax;
            columns.Delete = TakeColumn(ref right, rect, 32, 4);
            columns.Synced = TakeColumn(ref right, rect, 18, 16);
            columns.Saved = TakeColumn(ref right, rect, 18, 34);
            columns.Default = TakeColumn(ref right, rect, 85, 32);
            columns.Type = TakeColumn(ref right, rect, 85, 12);
            columns.Warning = TakeColumn(ref right, rect, 18, 4);
            if (add) columns.Add = TakeColumn(ref right, rect, 55, 4);
            columns.Dropdown = TakeColumn(ref right, rect, 21, 1);
            columns.Name = new Rect(rect.x, rect.y, Math.Max(20, right - rect.x), rect.height);
            return columns;
        }

        private static Columns GetHeaderColumns(Rect rect)
        {
            var columns = new Columns();
            float right = rect.xMax;
            columns.Delete = TakeColumn(ref right, rect, 32, 4);
            columns.Synced = TakeColumn(ref right, rect, 54);
            columns.Saved = TakeColumn(ref right, rect, 54);
            columns.Default = TakeColumn(ref right, rect, 117);
            columns.Type = TakeColumn(ref right, rect, 75);
            columns.Warning = TakeColumn(ref right, rect, 48);
            columns.Name = new Rect(rect.x, rect.y, Math.Max(20, right - rect.x), rect.height);
            return columns;
        }

        private static Rect TakeColumn(ref float right, Rect row, float width, float gap = 0)
        {
            right -= width + gap;
            var column = new Rect(right, row.y, width, row.height);
            return column;
        }

        private bool IsNullEntry(int index)
        {
            var parameters = Parameters.parameters;
            return parameters != null && index >= 0 && index < parameters.Length && parameters[index] == null;
        }

        private struct Columns { internal Rect Name, Dropdown, Add, Warning, Type, Default, Saved, Synced, Delete; }

        private sealed class ParameterDropdown : AdvancedDropdown
        {
            private readonly AnimatorControllerParameter[] parameters;
            private readonly string title;
            private readonly Action<string> selected;
            internal ParameterDropdown(AnimatorControllerParameter[] parameters, string title, Action<string> selected)
                : base(new AdvancedDropdownState())
            {
                this.parameters = parameters;
                this.title = title;
                this.selected = selected;
                minimumSize = new Vector2(260, 280);
            }
            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(title);
                for (int index = 0; index < parameters.Length; index++)
                    root.AddChild(new AdvancedDropdownItem(parameters[index].name + " (" + parameters[index].type + ")") { id = index });
                return root;
            }
            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item.id >= 0 && item.id < parameters.Length) selected(parameters[item.id].name);
            }
        }
    }
}
