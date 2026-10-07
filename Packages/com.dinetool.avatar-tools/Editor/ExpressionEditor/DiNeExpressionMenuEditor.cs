using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Control = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace DiNeTool.ExpressionEditor
{
    // Independently implemented against Unity and SDK data APIs. No third-party code/assets are bundled.
    [CustomEditor(typeof(VRCExpressionsMenu))]
    public sealed class DiNeExpressionMenuEditor : DiNeExpressionInspectorBase, IHasCustomMenu
    {
        private const string CompactPreference = "DiNeExpressionMenuCompact";
        private const string ClipboardPrefix = "DiNeExpressionControl:";
        private static readonly List<VRCExpressionsMenu> Visited = new List<VRCExpressionsMenu>();
        private static int visitedIndex = -1;
        private static Control clipboard;
        private static string clipboardText;
        private static VRCExpressionsMenu movingSource;
        private static Control movingControl;
        private ReorderableList controlsList;
        private GUIStyle richName, typeHint, centered, letterButton;
        internal readonly Dictionary<string, Rect> GuiRects = new Dictionary<string, Rect>();
        private VRCExpressionsMenu Menu => target as VRCExpressionsMenu;
        private VRCExpressionParameters Parameters => Avatar != null ? Avatar.expressionParameters : DiNeExpressionUtility.GetMenuParameters(Menu);
        private bool Compact => EditorPrefs.GetBool(CompactPreference, false);
        protected override string SdkEditorName => "VRCExpressionsMenuEditor";

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Menu == null) return;
            FindAvatar(avatar => DiNeExpressionUtility.ContainsMenu(avatar.expressionsMenu, Menu));
            RecordVisit();
            CreateList();
        }

        protected override void DrawContents()
        {
            if (Event.current.type == EventType.Repaint) GuiRects.Clear();
            EnsureStyles();
            if (controlsList == null) CreateList();
            if (controlsList == null) return;
            HandleShortcuts();
            DrawNavigation();
            DrawAvatarSelector();
            if (controlsList.count > 0 && controlsList.index < 0) controlsList.index = 0;
            if (controlsList.index >= controlsList.count) controlsList.index = controlsList.count - 1;
            controlsList.draggable = DiNeExpressionUtility.CanEditAsset(Menu);
            controlsList.DoLayoutList();
            var property = SelectedProperty();
            DrawMain(property);
            if (!Compact) { GUILayout.Space(4); DrawMainParameter(property); }
            if (property == null) return;
            var type = (Control.ControlType)property.FindPropertyRelative("type").intValue;
            if (type == Control.ControlType.SubMenu) DrawSubmenu(property);
            else if (AxisCount(type) > 0) DrawPuppet(property, type);
        }

        private void EnsureStyles()
        {
            if (richName != null) return;
            richName = new GUIStyle(EditorStyles.label) { richText = true };
            typeHint = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Italic };
            typeHint.normal.textColor = EditorStyles.centeredGreyMiniLabel.normal.textColor;
            centered = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
            letterButton = new GUIStyle(EditorStyles.miniButton) { richText = true, padding = new RectOffset(1, 1, 0, 0) };
        }

        private void CreateList()
        {
            var controls = serializedObject.FindProperty("controls");
            if (controls == null) return;
            int previous = controlsList?.index ?? 0;
            controlsList = new ReorderableList(serializedObject, controls, true, true, true, false)
            {
                drawHeaderCallback = DrawListHeader,
                drawElementCallback = DrawListRow,
                onCanAddCallback = _ => controls.arraySize < DiNeExpressionUtility.MenuLimit && DiNeExpressionUtility.CanEditAsset(Menu),
                onAddCallback = _ => RunGuiAction(() =>
                {
                    var control = new Control
                    {
                        name = T("New Control", "새 항목", "新規項目"), type = Control.ControlType.Toggle, value = 1,
                        parameter = new Control.Parameter(), subParameters = Array.Empty<Control.Parameter>(), labels = Array.Empty<Control.Label>()
                    };
                    if (DiNeExpressionUtility.AddControl(Menu, control, T("Add Menu Control", "메뉴 항목 추가", "メニュー項目を追加")))
                        controlsList.index = Menu.controls.Count - 1;
                })
            };
            controlsList.index = previous;
        }

        private void DrawListHeader(Rect rect)
        {
            Remember("ControlsHeader", rect);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, Mathf.Max(0, rect.width - 94), rect.height),
                T("Controls", "메뉴 항목", "メニュー項目") + $" ({controlsList.count} / 8)");
            float x = rect.xMax - 88;
            Control selected = SelectedControl();
            DrawIconButton(new Rect(x, rect.y, 18, 18), "SaveActive", C("Copy", "복사", "コピー"), selected != null,
                () => RunGuiAction(() => CopyControl(selected)), "Copy");
            x += 23;
            DrawIconButton(new Rect(x, rect.y, 18, 18), "Clipboard", C("Paste", "붙여넣기", "貼り付け"), ReadClipboard() != null,
                () => ShowPaste(selected), "Paste");
            x += 23;
            bool shift = Event.current.shift;
            DrawIconButton(new Rect(x, rect.y, 18, 18), "TreeEditor.Duplicate", C("Duplicate", "복제", "複製"),
                selected != null && controlsList.count < DiNeExpressionUtility.MenuLimit,
                () => RunGuiAction(() => DuplicateControl(selected, shift)), "Duplicate");
            x += 23;
            bool moving = movingSource != null && movingControl != null;
            DrawIconButton(new Rect(x, rect.y, 18, 18), moving ? "DefaultSorting" : "MoveTool",
                moving ? C("Place", "배치", "配置") : C("Move", "이동", "移動"),
                moving ? (movingSource == Menu || controlsList.count < DiNeExpressionUtility.MenuLimit) : selected != null,
                () => RunGuiAction(() => { if (moving) PlaceControl(selected, shift); else BeginMove(selected); }), "MovePlace");
        }

        private void DrawListRow(Rect rect, int index, bool active, bool focused)
        {
            if (index < 0 || index >= controlsList.serializedProperty.arraySize) return;
            var row = new Rect(rect.x, rect.y, Mathf.Max(0, rect.width - 34), 18);
            var remove = new Rect(rect.xMax - 30, rect.y, 30, 18);
            Remember("ControlsRow" + index, rect);
            Remember("ControlsDelete" + index, remove);
            var captured = Menu.controls != null && index < Menu.controls.Count ? Menu.controls[index] : null;
            if (captured == null)
            {
                GUI.Label(row, T("[Missing control]", "[항목 없음]", "[項目なし]"), typeHint);
                Control[] before = Menu.controls?.ToArray();
                DrawIconButton(remove, "Toolbar Minus", C("Delete", "삭제", "削除"), before != null,
                    () => RunGuiAction(() => DeleteNullControl(index, before)), "Delete" + index);
                if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    var menu = new GenericMenu();
                    menu.AddItem(C("Delete", "삭제", "削除"), false,
                        () => ApplyAction(() => DeleteNullControl(index, before)));
                    menu.ShowAsContext();
                }
                return;
            }
            var property = controlsList.serializedProperty.GetArrayElementAtIndex(index);
            var name = property.FindPropertyRelative("name").stringValue;
            var type = (Control.ControlType)property.FindPropertyRelative("type").intValue;
            GUI.Label(row, type.ToString(), typeHint);
            GUI.Label(row, string.IsNullOrEmpty(name) ? T("[Unnamed]", "[이름 없음]", "[名前なし]") : name, richName);
            DrawIconButton(remove, "Toolbar Minus", C("Delete", "삭제", "削除"), captured != null,
                () => RunGuiAction(() => DeleteControl(captured)), "Delete" + index);
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.clickCount == 2 && row.Contains(evt.mousePosition) &&
                type == Control.ControlType.SubMenu)
            {
                evt.Use();
                RunGuiAction(() => OpenSubmenu(captured));
            }
            if (evt.type == EventType.ContextClick && row.Contains(evt.mousePosition))
            {
                evt.Use();
                controlsList.index = index;
                controlsList.GrabKeyboardFocus();
                ShowControlContext(captured);
            }
        }

        private void DrawIconButton(Rect rect, string icon, GUIContent content, bool enabled, Action action, string key)
        {
            Remember(key, rect);
            var image = EditorGUIUtility.IconContent(icon);
            var label = new GUIContent(image.image, content.tooltip);
            if (label.image == null) label.text = content.text;
            using (new EditorGUI.DisabledScope(!enabled))
                if (GUI.Button(rect, label, GUIStyle.none)) action();
        }

        private void RecordVisit()
        {
            Visited.RemoveAll(menu => menu == null);
            if (visitedIndex >= 0 && visitedIndex < Visited.Count && Visited[visitedIndex] == Menu) return;
            int firstForward = Mathf.Clamp(visitedIndex + 1, 0, Visited.Count);
            if (firstForward < Visited.Count) Visited.RemoveRange(firstForward, Visited.Count - firstForward);
            Visited.Add(Menu);
            visitedIndex = Visited.Count - 1;
        }

        private void DrawNavigation()
        {
            using (new EditorGUILayout.HorizontalScope(DiNeEditorUI.CardStyle))
            {
                using (new EditorGUI.DisabledScope(visitedIndex <= 0))
                {
                    if (GUILayout.Button("<<", GUILayout.Width(30))) Navigate(0);
                    if (GUILayout.Button("<", GUILayout.Width(24))) Navigate(visitedIndex - 1);
                }
                if (GUILayout.Button(Menu.name, centered, GUILayout.ExpandWidth(true))) EditorGUIUtility.PingObject(Menu);
                using (new EditorGUI.DisabledScope(visitedIndex < 0 || visitedIndex >= Visited.Count - 1))
                {
                    if (GUILayout.Button(">", GUILayout.Width(24))) Navigate(visitedIndex + 1);
                    if (GUILayout.Button(">>", GUILayout.Width(30))) Navigate(Visited.Count - 1);
                }
            }
            Remember("MenuHistory", GUILayoutUtility.GetLastRect());
        }

        private void Navigate(int index)
        {
            if (index < 0 || index >= Visited.Count || Visited[index] == null) return;
            RunGuiAction(() => { visitedIndex = index; Selection.activeObject = Visited[index]; });
        }

        private SerializedProperty SelectedProperty()
        {
            int index = controlsList.index;
            return Menu.controls != null && index >= 0 && index < Menu.controls.Count && Menu.controls[index] != null &&
                index < controlsList.serializedProperty.arraySize
                ? controlsList.serializedProperty.GetArrayElementAtIndex(index) : null;
        }

        private Control SelectedControl()
        {
            int index = controlsList.index;
            return Menu.controls != null && index >= 0 && index < Menu.controls.Count ? Menu.controls[index] : null;
        }

        private int IndexOf(Control captured)
        {
            return captured == null || Menu == null ? -1 : Menu.controls?.FindIndex(control => ReferenceEquals(control, captured)) ?? -1;
        }

        private void DrawMain(SerializedProperty property)
        {
            bool compact = Compact;
            float helpHeight = !compact && property != null ? Mathf.Max(42,
                EditorStyles.helpBox.CalcHeight(new GUIContent(HelpText(property)), Mathf.Max(80, EditorGUIUtility.currentViewWidth - 48))) : 42;
            Rect outer = GUILayoutUtility.GetRect(0, compact ? 66 : 105 + helpHeight, GUILayout.ExpandWidth(true));
            Remember("ControlMain", outer);
            GUI.Box(outer, GUIContent.none, EditorStyles.helpBox);
            Rect area = Inset(outer, 4);
            if (property == null)
            {
                GUI.Label(area, T("Select or add a control.", "항목을 선택하거나 추가하세요.", "項目を選択するか追加してください。"), centered);
                return;
            }
            float iconSize = compact ? 58 : 96;
            Rect iconRect = new Rect(area.xMax - iconSize, area.y, iconSize, iconSize);
            Rect content = new Rect(area.x, area.y, Mathf.Max(30, area.width - iconSize - 4), area.height);
            if (compact)
            {
                float half = content.width * .5f;
                DrawName(new Rect(content.x, content.y, half - 3, 18), property, false);
                DrawType(new Rect(content.x + half, content.y, half - 20, 18), property, false);
                GUI.Label(new Rect(content.xMax - 18, content.y, 18, 18),
                    new GUIContent(EditorGUIUtility.IconContent("_Help").image, HelpText(property)));
                DrawParameterValue(new Rect(content.x, content.y + 21, content.width, 18), property, false);
                DrawNameStyle(new Rect(content.x, content.y + 42, content.width, 18), property, false);
            }
            else
            {
                DrawName(new Rect(content.x, content.y, content.width, 21), property, true);
                DrawType(new Rect(content.x, content.y + 24, content.width, 21), property, true);
                DrawNameStyle(new Rect(content.x, content.y + 48, content.width, 21), property, true);
                EditorGUI.HelpBox(new Rect(area.x, iconRect.yMax + 3, area.width, helpHeight), HelpText(property), MessageType.Info);
            }
            var icon = property.FindPropertyRelative("icon");
            EditorGUI.BeginChangeCheck();
            var selectedIcon = EditorGUI.ObjectField(iconRect, string.Empty, icon.objectReferenceValue, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) icon.objectReferenceValue = selectedIcon;
        }

        private void DrawName(Rect rect, SerializedProperty control, bool labeled)
        {
            if (labeled) rect = FieldArea(rect, T("Name", "이름", "名前"));
            EditorGUI.PropertyField(rect, control.FindPropertyRelative("name"), GUIContent.none);
        }

        private void DrawType(Rect rect, SerializedProperty control, bool labeled)
        {
            if (labeled) rect = FieldArea(rect, T("Type", "유형", "種類"));
            var type = control.FindPropertyRelative("type");
            var types = new[] { Control.ControlType.Button, Control.ControlType.Toggle, Control.ControlType.SubMenu,
                Control.ControlType.TwoAxisPuppet, Control.ControlType.FourAxisPuppet, Control.ControlType.RadialPuppet };
            string[] labels = { T("Button", "버튼", "ボタン"), T("Toggle", "토글", "トグル"), T("Sub Menu", "서브메뉴", "サブメニュー"),
                T("Two Axis Puppet", "2축 Puppet", "2軸Puppet"), T("Four Axis Puppet", "4축 Puppet", "4軸Puppet"),
                T("Radial Puppet", "Radial Puppet", "Radial Puppet") };
            int current = Array.IndexOf(types, (Control.ControlType)type.intValue);
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUI.Popup(rect, current, labels);
            if (!EditorGUI.EndChangeCheck() || chosen < 0) return;
            type.intValue = (int)types[chosen];
            PreparePuppetFields(control, types[chosen]);
        }

        private void DrawNameStyle(Rect rect, SerializedProperty control, bool labeled)
        {
            if (labeled) rect = FieldArea(rect, T("Style", "스타일", "スタイル"));
            var name = control.FindPropertyRelative("name");
            string text = name.stringValue ?? "";
            bool bold = text.Contains("<b>") && text.Contains("</b>");
            bool italic = text.Contains("<i>") && text.Contains("</i>");
            Color color = Color.white;
            var match = Regex.Match(text, "<color=(#[0-9a-fA-F]{6,8})>");
            if (match.Success) ColorUtility.TryParseHtmlString(match.Groups[1].Value, out color);
            Rect colorRect = new Rect(rect.x, rect.y, Mathf.Max(20, rect.width - 48), rect.height);
            EditorGUI.BeginChangeCheck();
            color = EditorGUI.ColorField(colorRect, GUIContent.none, color, true, false, false);
            if (EditorGUI.EndChangeCheck())
                text = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + Regex.Replace(text, "</?color[^>]*>", "", RegexOptions.IgnoreCase) + "</color>";
            EditorGUI.BeginChangeCheck();
            bold = GUI.Toggle(new Rect(rect.xMax - 45, rect.y, 21, rect.height), bold,
                new GUIContent("<b>b</b>", T("Bold", "굵게", "太字")), letterButton);
            if (EditorGUI.EndChangeCheck()) text = SetTag(text, "b", bold);
            EditorGUI.BeginChangeCheck();
            italic = GUI.Toggle(new Rect(rect.xMax - 21, rect.y, 21, rect.height), italic,
                new GUIContent("<i>i</i>", T("Italic", "기울임", "斜体")), letterButton);
            if (EditorGUI.EndChangeCheck()) text = SetTag(text, "i", italic);
            if (text != name.stringValue) name.stringValue = text;
        }

        private static string SetTag(string text, string tag, bool enabled)
        {
            string plain = Regex.Replace(text, "</?" + tag + ">", "", RegexOptions.IgnoreCase);
            return enabled ? "<" + tag + ">" + plain + "</" + tag + ">" : plain;
        }

        private void DrawMainParameter(SerializedProperty control)
        {
            Rect outer = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            Remember("ControlParameter", outer);
            GUI.Box(outer, GUIContent.none, EditorStyles.helpBox);
            if (control != null) DrawParameterValue(Inset(outer, 4), control, true);
        }

        private void DrawParameterValue(Rect rect, SerializedProperty control, bool labeled)
        {
            var type = (Control.ControlType)control.FindPropertyRelative("type").intValue;
            if (SelectedControl()?.parameter == null) return;
            var name = control.FindPropertyRelative("parameter")?.FindPropertyRelative("name");
            if (name == null) return;
            Rect selector = new Rect(rect.x, rect.y, Mathf.Max(10, rect.width - 53), rect.height);
            string oldName = name.stringValue;
            DrawParameter(selector, name, labeled ? T("Parameter", "파라미터", "パラメータ") : "",
                type == Control.ControlType.Button || type == Control.ControlType.Toggle, false);
            var parameter = Parameters?.parameters?.FirstOrDefault(item => item != null && item.name == name.stringValue);
            var value = control.FindPropertyRelative("value");
            Rect valueRect = new Rect(rect.xMax - 50, rect.y, 50, rect.height);
            if (parameter != null && parameter.valueType == VRCExpressionParameters.ValueType.Bool)
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUI.TextField(valueRect, "");
                if (name.stringValue != oldName) value.floatValue = 1;
                return;
            }
            EditorGUI.BeginChangeCheck();
            float changed = parameter != null && parameter.valueType == VRCExpressionParameters.ValueType.Int
                ? EditorGUI.IntField(valueRect, Mathf.RoundToInt(value.floatValue)) : EditorGUI.FloatField(valueRect, value.floatValue);
            if (EditorGUI.EndChangeCheck())
                value.floatValue = parameter?.valueType == VRCExpressionParameters.ValueType.Int ? Mathf.Clamp(changed, 0, 255) :
                    parameter?.valueType == VRCExpressionParameters.ValueType.Float ? Mathf.Clamp(changed, -1, 1) : Mathf.Clamp(changed, -1, 255);
        }

        private void DrawParameter(Rect rect, SerializedProperty name, string label, bool required, bool floatOnly, float labelWidth = -1)
        {
            if (!string.IsNullOrEmpty(label)) rect = FieldArea(rect, label, labelWidth);
            var parameters = Parameters;
            var current = parameters?.parameters?.FirstOrDefault(item => item != null && item.name == name.stringValue);
            bool missing = !string.IsNullOrEmpty(name.stringValue) && (current == null ||
                (floatOnly && current.valueType != VRCExpressionParameters.ValueType.Float));
            bool warning = parameters == null || missing || (required && string.IsNullOrEmpty(name.stringValue));
            float extra = warning ? 18 : 0;
            if (parameters != null && missing) extra += 43;
            Rect text = new Rect(rect.x, rect.y, Mathf.Max(10, rect.width - 22 - extra), rect.height);
            Rect dropdown = new Rect(text.xMax + 2, rect.y, 20, rect.height);
            EditorGUI.PropertyField(text, name, GUIContent.none);
            var choices = parameters?.parameters?.Where(item => item != null && !string.IsNullOrEmpty(item.name) &&
                (!floatOnly || item.valueType == VRCExpressionParameters.ValueType.Float)).ToArray()
                ?? Array.Empty<VRCExpressionParameters.Parameter>();
            var labels = new[] { T("None", "없음", "なし") }.Concat(choices.Select(item => item.name)).ToArray();
            int selected = Array.FindIndex(choices, item => item.name == name.stringValue) + 1;
            using (new EditorGUI.DisabledScope(parameters == null))
            {
                EditorGUI.BeginChangeCheck();
                int next = EditorGUI.Popup(dropdown, selected, labels);
                if (EditorGUI.EndChangeCheck()) name.stringValue = next == 0 ? "" : choices[next - 1].name;
            }
            if (parameters != null && missing)
            {
                Rect add = new Rect(dropdown.xMax + 2, rect.y, 41, rect.height);
                using (new EditorGUI.DisabledScope(!DiNeExpressionUtility.CanEditAsset(parameters) || current != null))
                    if (GUI.Button(add, C("Add", "추가", "追加"), EditorStyles.miniButton))
                        ShowAddParameter(parameters, name.stringValue, floatOnly, SelectedControl());
            }
            if (warning)
                GUI.Label(new Rect(rect.xMax - 18, rect.y, 18, rect.height),
                    new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image,
                        parameters == null ? T("The active avatar has no expression parameters.", "선택한 아바타에 Expression Parameters가 없습니다.", "選択したアバターにExpression Parametersがありません。") :
                        T("The parameter is missing or has an incompatible type.", "파라미터가 없거나 유형이 맞지 않습니다.", "パラメータが存在しないか、種類が一致しません。")));
        }

        private void ShowAddParameter(VRCExpressionParameters parameters, string name, bool floatOnly, Control captured)
        {
            if (floatOnly)
            {
                RunGuiAction(() => AddMissingParameter(parameters, name, VRCExpressionParameters.ValueType.Float, captured));
                return;
            }
            var menu = new GenericMenu();
            var types = new[] { VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Float, VRCExpressionParameters.ValueType.Bool };
            foreach (var type in types)
            {
                var chosen = type;
                menu.AddItem(new GUIContent(type.ToString()), false,
                    () => ApplyAction(() => AddMissingParameter(parameters, name, chosen, captured)));
            }
            menu.ShowAsContext();
        }

        private void AddMissingParameter(VRCExpressionParameters parameters, string name,
            VRCExpressionParameters.ValueType type, Control captured)
        {
            int index = IndexOf(captured);
            if (index < 0 || !DiNeExpressionUtility.CanEditAsset(Menu) || !DiNeExpressionUtility.CanEditAsset(parameters) ||
                string.IsNullOrWhiteSpace(name) || Parameters != parameters || !ReferencesName(Menu.controls[index], name) ||
                (parameters.parameters?.Any(parameter => parameter != null && parameter.name == name) ?? false)) return;
            var added = new VRCExpressionParameters.Parameter
            {
                name = name, valueType = type, defaultValue = 0, saved = true, networkSynced = true
            };
            var next = (parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
                .Concat(new[] { DiNeExpressionUtility.CloneParameter(added) }).ToArray();
            // Match the direct menu-editor Add workflow: allow the author to finish wiring a
            // control even above the SDK sync budget. Budget diagnostics remain advisory here.
            Undo.RecordObject(parameters, T("Add Expression Parameter", "Expression 파라미터 추가", "Expressionパラメータを追加"));
            parameters.parameters = next;
            parameters.isEmpty = false;
            EditorUtility.SetDirty(parameters);
            Status = null;
        }

        private static bool ReferencesName(Control control, string name)
        {
            return control.parameter?.name == name || (control.subParameters?.Any(axis => axis != null && axis.name == name) ?? false);
        }

        private void DrawSubmenu(SerializedProperty control)
        {
            using (new EditorGUILayout.HorizontalScope(DiNeEditorUI.CardStyle))
            {
                var submenu = control.FindPropertyRelative("subMenu");
                EditorGUILayout.PropertyField(submenu, C("Submenu", "서브메뉴", "サブメニュー"));
                var captured = SelectedControl();
                if (submenu.objectReferenceValue == null)
                {
                    if (GUILayout.Button(C("New", "새로", "新規"), GUILayout.Width(40)))
                        RunGuiAction(() => CreateSubmenu(captured));
                    GUILayout.Label(new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image,
                        T("The submenu is empty.", "서브메뉴가 비어 있습니다.", "サブメニューが未指定です。")), GUILayout.Width(18));
                }
                else
                {
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Folder Icon").image,
                        T("Open submenu", "서브메뉴 열기", "サブメニューを開く")), GUIStyle.none, GUILayout.Width(18)))
                        RunGuiAction(() => OpenSubmenu(captured));
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image,
                        T("Clear submenu", "서브메뉴 해제", "サブメニューを解除")), GUIStyle.none, GUILayout.Width(18)))
                        submenu.objectReferenceValue = null;
                }
            }
        }

        private void DrawPuppet(SerializedProperty control, Control.ControlType type)
        {
            int expected = AxisCount(type);
            var axes = control.FindPropertyRelative("subParameters");
            var labels = control.FindPropertyRelative("labels");
            int labelCount = expected == 1 ? 0 : 4;
            var selected = SelectedControl();
            bool missingEntry = selected?.subParameters == null || selected.subParameters.Any(axis => axis == null) ||
                (labelCount > 0 && selected.labels == null);
            if (axes.arraySize != expected || labels.arraySize != labelCount || missingEntry)
            {
                EditorGUILayout.HelpBox(T("The puppet fields do not match this control type.", "Puppet 필드가 항목 유형과 맞지 않습니다.", "Puppetフィールドが項目の種類と一致しません。"), MessageType.Warning);
                if (GUILayout.Button(C("Prepare puppet fields", "Puppet 필드 구성", "Puppetフィールドを構成")))
                    RunGuiAction(() => RepairPuppetFields(selected, type));
                return;
            }
            using (new EditorGUILayout.VerticalScope(DiNeEditorUI.CardStyle))
            {
                if (expected != 1)
                {
                    if (!Compact) GUILayout.Label(T("Axis Parameters", "축 파라미터", "軸パラメータ"), centered);
                    else
                    {
                        Rect header = EditorGUILayout.GetControlRect(false, 18);
                        GUI.Label(new Rect(header.x, header.y, header.width * .5f, header.height),
                            T("Axis Parameters", "축 파라미터", "軸パラメータ"), centered);
                        GUI.Label(new Rect(header.center.x, header.y, header.width * .5f, header.height),
                            expected == 2 ? T("Name −    Name +", "이름 −    이름 +", "名前 −    名前 +") : T("Name", "이름", "名前"), centered);
                    }
                }
                for (int axis = 0; axis < expected; axis++)
                {
                    string title = type == Control.ControlType.TwoAxisPuppet ? axis == 0
                        ? T("Horizontal", "좌우", "水平") : T("Vertical", "상하", "垂直") :
                        expected == 1 ? T("Rotation", "회전", "回転") : Direction(axis);
                    if (Compact && expected > 1)
                    {
                        Rect field = EditorGUILayout.GetControlRect(false, 18);
                        // At a normal Inspector width, keep the short axis selector on the left
                        // and devote most of the row to label names. Narrow Inspectors rebalance
                        // the two areas so the selector, warning and inline Add remain usable.
                        float room = Mathf.InverseLerp(340, 680, field.width);
                        float axisWidth = Mathf.Lerp(.46f, .315f, room) * field.width;
                        float labelStart = Mathf.Lerp(.50f, .43f, room) * field.width;
                        Rect parameter = new Rect(field.x, field.y, axisWidth, field.height);
                        Rect customization = new Rect(field.x + labelStart, field.y, field.width - labelStart, field.height);
                        DrawParameter(parameter, axes.GetArrayElementAtIndex(axis).FindPropertyRelative("name"), title, true, true, field.width * .17f);
                        if (expected == 2)
                        {
                            int first = axis == 0 ? 0 : 2;
                            Rect negative = new Rect(customization.x, customization.y, customization.width * .5f - 2, customization.height);
                            Rect positive = new Rect(customization.center.x, customization.y, customization.width * .5f, customization.height);
                            DrawCompactLabel(negative, labels.GetArrayElementAtIndex(first), axis == 0 ? Direction(3) : Direction(2));
                            DrawCompactLabel(positive, labels.GetArrayElementAtIndex(first + 1), axis == 0 ? Direction(1) : Direction(0));
                        }
                        else DrawCompactLabel(customization, labels.GetArrayElementAtIndex(axis), Direction(axis));
                    }
                    else DrawParameter(EditorGUILayout.GetControlRect(false, 18),
                        axes.GetArrayElementAtIndex(axis).FindPropertyRelative("name"), title, true, true);
                }
            }
            Remember("ControlAxes", GUILayoutUtility.GetLastRect());
            if (expected == 1 || Compact) return;
            using (new EditorGUILayout.VerticalScope(DiNeEditorUI.CardStyle))
            {
                GUILayout.Label(T("Customization", "사용자 지정", "カスタマイズ"), centered);
                for (int index = 0; index < labels.arraySize; index++)
                {
                    var label = labels.GetArrayElementAtIndex(index);
                    using (new EditorGUILayout.HorizontalScope(DiNeEditorUI.CardStyle))
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            using (new EditorGUI.DisabledScope(true))
                                EditorGUILayout.TextField(T("Axis", "축", "軸"), Direction(index));
                            EditorGUILayout.PropertyField(label.FindPropertyRelative("name"), C("Name", "이름", "名前"));
                        }
                        label.FindPropertyRelative("icon").objectReferenceValue = EditorGUILayout.ObjectField(
                            label.FindPropertyRelative("icon").objectReferenceValue, typeof(Texture2D), false, GUILayout.Width(58), GUILayout.Height(58));
                    }
                }
            }
            Remember("ControlLabels", GUILayoutUtility.GetLastRect());
        }

        private void DrawCompactLabel(Rect rect, SerializedProperty label, string title)
        {
            var name = label.FindPropertyRelative("name");
            Rect text = new Rect(rect.x, rect.y, Mathf.Max(12, rect.width - 31), rect.height);
            EditorGUI.PropertyField(text, name, GUIContent.none);
            if (string.IsNullOrEmpty(name.stringValue)) GUI.Label(text, title, EditorStyles.centeredGreyMiniLabel);
            EditorGUI.PropertyField(new Rect(rect.xMax - 28, rect.y, 28, rect.height), label.FindPropertyRelative("icon"), GUIContent.none);
        }

        private static int AxisCount(Control.ControlType type)
        {
            return type == Control.ControlType.TwoAxisPuppet ? 2 : type == Control.ControlType.FourAxisPuppet ? 4 : type == Control.ControlType.RadialPuppet ? 1 : 0;
        }

        private static void PreparePuppetFields(SerializedProperty control, Control.ControlType type)
        {
            int count = AxisCount(type);
            var axes = control.FindPropertyRelative("subParameters");
            int previousAxes = axes.arraySize;
            axes.arraySize = count;
            for (int i = previousAxes; i < count; i++) axes.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = "";
            var labels = control.FindPropertyRelative("labels");
            int previousLabels = labels.arraySize;
            labels.arraySize = count == 2 || count == 4 ? 4 : 0;
            for (int i = previousLabels; i < labels.arraySize; i++)
            {
                labels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = "";
                labels.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = null;
            }
        }

        private void RepairPuppetFields(Control captured, Control.ControlType type)
        {
            int index = IndexOf(captured);
            if (index < 0 || !DiNeExpressionUtility.CanEditAsset(Menu)) return;
            var next = DiNeExpressionUtility.CloneControl(captured);
            int axes = AxisCount(type);
            var oldAxes = next.subParameters ?? Array.Empty<Control.Parameter>();
            next.subParameters = Enumerable.Range(0, axes)
                .Select(axis => axis < oldAxes.Length ? oldAxes[axis] ?? new Control.Parameter() : new Control.Parameter()).ToArray();
            var oldLabels = next.labels ?? Array.Empty<Control.Label>();
            next.labels = Enumerable.Range(0, axes == 2 || axes == 4 ? 4 : 0)
                .Select(label => label < oldLabels.Length ? oldLabels[label] : new Control.Label()).ToArray();
            Undo.RecordObject(Menu, T("Prepare Puppet Fields", "Puppet 필드 구성", "Puppetフィールドを構成"));
            var controls = new List<Control>(Menu.controls);
            controls[index] = next;
            Menu.controls = controls;
            EditorUtility.SetDirty(Menu);
        }

        private void HandleShortcuts()
        {
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && movingControl != null)
            {
                movingSource = null; movingControl = null; evt.Use(); return;
            }
            if (!controlsList.HasKeyboardControl() || EditorGUIUtility.editingTextField) return;
            string command = evt.commandName;
            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) command = "Delete";
                else if (evt.control || evt.command)
                    command = evt.keyCode == KeyCode.C ? "Copy" : evt.keyCode == KeyCode.V ? "Paste" :
                        evt.keyCode == KeyCode.X ? "Cut" : evt.keyCode == KeyCode.D ? "Duplicate" : "";
                else return;
            }
            else if (evt.type != EventType.ValidateCommand && evt.type != EventType.ExecuteCommand) return;
            if (!new[] { "Copy", "Paste", "Cut", "Duplicate", "Delete", "SoftDelete" }.Contains(command)) return;
            bool validate = evt.type == EventType.ValidateCommand;
            bool shift = evt.shift;
            evt.Use();
            if (validate) return;
            var captured = SelectedControl();
            RunGuiAction(() =>
            {
                switch (command)
                {
                    case "Copy": CopyControl(captured); break;
                    case "Cut": BeginMove(captured); break;
                    case "Duplicate": DuplicateControl(captured, shift); break;
                    case "Delete": case "SoftDelete": DeleteControl(captured); break;
                    case "Paste": if (movingControl != null) PlaceControl(captured, shift); else PasteControl(captured, true); break;
                }
            });
        }

        private void ShowControlContext(Control captured)
        {
            var menu = new GenericMenu();
            bool full = controlsList.count >= DiNeExpressionUtility.MenuLimit;
            menu.AddItem(C("Cut", "잘라내기", "切り取り"), false, () => ApplyAction(() => BeginMove(captured)));
            menu.AddItem(C("Copy", "복사", "コピー"), false, () => ApplyAction(() => CopyControl(captured)));
            if (ReadClipboard() == null) menu.AddDisabledItem(C("Paste", "붙여넣기", "貼り付け"));
            else
            {
                menu.AddItem(C("Paste/Values", "붙여넣기/값", "貼り付け/値"), false, () => ApplyAction(() => PasteControl(captured, false)));
                AddMenuItem(menu, C("Paste/As New", "붙여넣기/새 항목", "貼り付け/新規項目"), !full,
                    () => ApplyAction(() => PasteControl(captured, true)));
            }
            menu.AddSeparator("");
            bool shift = Event.current.shift;
            AddMenuItem(menu, C("Duplicate", "복제", "複製"), !full, () => ApplyAction(() => DuplicateControl(captured, shift)));
            menu.AddItem(C("Delete", "삭제", "削除"), false, () => ApplyAction(() => DeleteControl(captured)));
            menu.ShowAsContext();
        }

        private void ShowPaste(Control captured)
        {
            var menu = new GenericMenu();
            AddMenuItem(menu, C("Paste values", "값 붙여넣기", "値を貼り付け"), captured != null,
                () => ApplyAction(() => PasteControl(captured, false)));
            AddMenuItem(menu, C("Insert as new", "새 항목으로 삽입", "新規項目として挿入"), controlsList.count < DiNeExpressionUtility.MenuLimit,
                () => ApplyAction(() => PasteControl(captured, true)));
            menu.ShowAsContext();
        }

        private static void AddMenuItem(GenericMenu menu, GUIContent content, bool enabled, Action action)
        {
            if (enabled) menu.AddItem(content, false, () => action());
            else menu.AddDisabledItem(content);
        }

        private void CopyControl(Control captured)
        {
            int index = IndexOf(captured);
            if (index < 0) return;
            clipboard = DiNeExpressionUtility.CloneControl(Menu.controls[index]);
            clipboardText = ClipboardPrefix + JsonUtility.ToJson(clipboard);
            EditorGUIUtility.systemCopyBuffer = clipboardText;
        }

        private static Control ReadClipboard()
        {
            string text = EditorGUIUtility.systemCopyBuffer ?? "";
            if (!text.StartsWith(ClipboardPrefix, StringComparison.Ordinal)) return null;
            if (clipboard != null && text == clipboardText) return clipboard;
            try { return JsonUtility.FromJson<Control>(text.Substring(ClipboardPrefix.Length)); }
            catch (ArgumentException) { return null; }
        }

        private void PasteControl(Control captured, bool asNew)
        {
            if (!DiNeExpressionUtility.CanEditAsset(Menu)) return;
            var copied = ReadClipboard();
            if (copied == null) return;
            int index = IndexOf(captured);
            if (captured != null && index < 0) return;
            string undo = T("Paste Menu Control", "메뉴 항목 붙여넣기", "メニュー項目を貼り付け");
            if (!asNew)
            {
                if (index < 0) return;
                var next = new List<Control>(Menu.controls);
                next[index] = DiNeExpressionUtility.CloneControl(copied);
                Undo.RecordObject(Menu, undo);
                Menu.controls = next;
                EditorUtility.SetDirty(Menu);
                controlsList.index = index;
            }
            else InsertControl(copied, index < 0 ? Menu.controls?.Count ?? 0 : index + 1, undo);
        }

        private void DuplicateControl(Control captured, bool shift)
        {
            int index = IndexOf(captured);
            if (index < 0) return;
            var duplicate = DiNeExpressionUtility.CloneControl(Menu.controls[index]);
            duplicate.name = IncrementSuffix(duplicate.name);
            if (!shift && duplicate.parameter != null &&
                (duplicate.type == Control.ControlType.Button || duplicate.type == Control.ControlType.Toggle))
            {
                var parameter = Parameters?.parameters?.FirstOrDefault(item => item != null && item.name == duplicate.parameter?.name);
                if (parameter?.valueType == VRCExpressionParameters.ValueType.Bool)
                    duplicate.parameter.name = IncrementSuffix(duplicate.parameter.name);
                else if (parameter != null && Mathf.Approximately(duplicate.value, Mathf.Round(duplicate.value))) duplicate.value += 1;
            }
            InsertControl(duplicate, index + 1, T("Duplicate Menu Control", "메뉴 항목 복제", "メニュー項目を複製"));
        }

        private static string IncrementSuffix(string name)
        {
            var match = Regex.Match(name ?? "", "[0-9]+$");
            if (!match.Success || !int.TryParse(match.Value, out int value) || value == int.MaxValue) return name;
            return name.Substring(0, match.Index) + (value + 1).ToString(new string('0', match.Length));
        }

        private void InsertControl(Control control, int index, string undo)
        {
            if (!DiNeExpressionUtility.CanEditAsset(Menu)) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undo);
            try
            {
                if (!DiNeExpressionUtility.AddControl(Menu, control, undo)) return;
                int last = Menu.controls.Count - 1;
                index = Mathf.Clamp(index, 0, last);
                if (index != last) DiNeExpressionUtility.MoveControl(Menu, last, index, undo);
                controlsList.index = index;
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        private void DeleteControl(Control captured)
        {
            int index = IndexOf(captured);
            if (index < 0) return;
            if (DiNeExpressionUtility.RemoveControl(Menu, index, T("Delete Menu Control", "메뉴 항목 삭제", "メニュー項目を削除")))
                controlsList.index = Mathf.Clamp(index - 1, -1, Menu.controls.Count - 1);
        }

        private void DeleteNullControl(int index, Control[] before)
        {
            if (before == null || Menu.controls == null || before.Length != Menu.controls.Count || index < 0 ||
                index >= Menu.controls.Count || Menu.controls[index] != null) return;
            // A null slot has no identity. Reject stale callbacks if any surrounding item changed.
            for (int item = 0; item < before.Length; item++)
                if (!ReferenceEquals(before[item], Menu.controls[item])) return;
            if (DiNeExpressionUtility.RemoveControl(Menu, index, T("Delete Menu Control", "메뉴 항목 삭제", "メニュー項目を削除")))
                controlsList.index = Mathf.Clamp(index - 1, -1, Menu.controls.Count - 1);
        }

        private void BeginMove(Control captured)
        {
            if (!DiNeExpressionUtility.CanEditAsset(Menu) || IndexOf(captured) < 0) return;
            movingSource = Menu;
            movingControl = captured;
        }

        private void PlaceControl(Control destinationControl, bool returnToSource)
        {
            var source = movingSource;
            var captured = movingControl;
            if (source == null || captured == null || !DiNeExpressionUtility.CanEditAsset(source) ||
                !DiNeExpressionUtility.CanEditAsset(Menu)) return;
            int sourceIndex = source.controls?.FindIndex(control => ReferenceEquals(control, captured)) ?? -1;
            if (sourceIndex < 0) { movingSource = null; movingControl = null; return; }
            int destinationIndex = IndexOf(destinationControl);
            if (destinationControl != null && destinationIndex < 0) return;
            int insertion = destinationIndex < 0 ? Menu.controls?.Count ?? 0 : destinationIndex + 1;
            if (source != Menu && (Menu.controls?.Count ?? 0) >= DiNeExpressionUtility.MenuLimit) return;
            string undo = T("Move Menu Control", "메뉴 항목 이동", "メニュー項目を移動");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undo);
            try
            {
                if (source == Menu)
                {
                    if (sourceIndex < insertion) insertion--;
                    insertion = Mathf.Clamp(insertion, 0, Menu.controls.Count - 1);
                    if (insertion != sourceIndex && !DiNeExpressionUtility.MoveControl(Menu, sourceIndex, insertion, undo)) return;
                }
                else
                {
                    if (!DiNeExpressionUtility.MoveControlToMenu(source, sourceIndex, Menu, undo)) return;
                    int last = Menu.controls.Count - 1;
                    insertion = Mathf.Clamp(insertion, 0, last);
                    if (insertion != last) DiNeExpressionUtility.MoveControl(Menu, last, insertion, undo);
                }
                controlsList.index = insertion;
                movingSource = null; movingControl = null;
                if (returnToSource) Selection.activeObject = source;
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        private void OpenSubmenu(Control captured)
        {
            int index = IndexOf(captured);
            if (index < 0 || Menu.controls[index].subMenu == null || Menu.controls[index].subMenu == Menu) return;
            Selection.activeObject = Menu.controls[index].subMenu;
        }

        private void CreateSubmenu(Control captured)
        {
            int index = IndexOf(captured);
            if (index < 0 || !DiNeExpressionUtility.CanEditAsset(Menu)) return;
            string path = AssetDatabase.GetAssetPath(Menu);
            string title = Regex.Replace(captured.name ?? Menu.name, "<[^>]*>", "");
            foreach (char invalid in Path.GetInvalidFileNameChars()) title = title.Replace(invalid, '_');
            if (string.IsNullOrWhiteSpace(title)) title = "SubMenu";
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                path = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(path).Replace('\\', '/') + "/" + title + " Menu.asset");
            else path = EditorUtility.SaveFilePanelInProject(T("Create Submenu", "서브메뉴 만들기", "サブメニューを作成"), title + " Menu", "asset",
                T("Choose where to save the submenu.", "서브메뉴를 저장할 위치를 선택하세요.", "サブメニューの保存先を選択してください。"));
            index = IndexOf(captured);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || index < 0 ||
                !DiNeExpressionUtility.CanEditAsset(Menu)) return;
            var submenu = CreateInstance<VRCExpressionsMenu>();
            submenu.controls = new List<Control>();
            DiNeExpressionUtility.SetMenuParameters(submenu, Parameters);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            string undo = T("Create Submenu", "서브메뉴 만들기", "サブメニューを作成");
            Undo.SetCurrentGroupName(undo);
            AssetDatabase.CreateAsset(submenu, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(submenu, undo);
            Undo.RecordObject(Menu, undo);
            Menu.controls[index].subMenu = submenu;
            EditorUtility.SetDirty(Menu);
            Undo.CollapseUndoOperations(group);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(C("Compact Mode", "간결한 모드", "コンパクトモード"), Compact, () =>
            {
                EditorPrefs.SetBool(CompactPreference, !Compact);
                Repaint();
            });
        }

        private static string Direction(int index)
        {
            return index == 0 ? T("Up", "위", "上") : index == 1 ? T("Right", "오른쪽", "右") :
                index == 2 ? T("Down", "아래", "下") : T("Left", "왼쪽", "左");
        }

        private static string HelpText(SerializedProperty property)
        {
            var type = (Control.ControlType)property.FindPropertyRelative("type").intValue;
            switch (type)
            {
                case Control.ControlType.Button:
                    return T("Click or hold to activate.\nSets the parameter to Value while active.\nReleases it to zero when inactive.",
                        "클릭하거나 길게 눌러 실행합니다.\n실행 중 파라미터를 값으로 설정합니다.\n해제하면 0으로 되돌립니다.",
                        "クリックまたは長押しで実行します。\n実行中はパラメータを値に設定します。\n解除すると0に戻します。");
                case Control.ControlType.Toggle:
                    return T("Click to switch on or off.\nSets the parameter to Value while on.\nSwitches it to zero when off.",
                        "클릭하여 켜거나 끕니다.\n켜면 파라미터를 값으로 설정합니다.\n끄면 0으로 되돌립니다.",
                        "クリックでオンとオフを切り替えます。\nオンの間はパラメータを値に設定します。\nオフにすると0に戻します。");
                case Control.ControlType.SubMenu:
                    return T("Opens another expression menu.\nSets the parameter to Value on opening.\nReturns it to zero on closing.",
                        "다른 Expression Menu를 엽니다.\n열면 파라미터를 값으로 설정합니다.\n닫으면 0으로 되돌립니다.",
                        "別のExpression Menuを開きます。\n開くとパラメータを値に設定します。\n閉じると0に戻します。");
                case Control.ControlType.TwoAxisPuppet:
                    return T("Moves two Float parameters with a joystick (-1 to 1).\nOpening sets the main parameter to Value.\nClosing returns it to zero.",
                        "조이스틱으로 두 Float 파라미터를 조절합니다 (-1~1).\n열면 기본 파라미터를 값으로 설정합니다.\n닫으면 0으로 되돌립니다.",
                        "ジョイスティックで2つのFloatを調整します (-1〜1)。\n開くと主パラメータを値に設定します。\n閉じると0に戻します。");
                case Control.ControlType.FourAxisPuppet:
                    return T("Moves four Float parameters with a joystick (0 to 1).\nOpening sets the main parameter to Value.\nClosing returns it to zero.",
                        "조이스틱으로 네 Float 파라미터를 조절합니다 (0~1).\n열면 기본 파라미터를 값으로 설정합니다.\n닫으면 0으로 되돌립니다.",
                        "ジョイスティックで4つのFloatを調整します (0〜1)。\n開くと主パラメータを値に設定します。\n閉じると0に戻します。");
                default:
                    return T("Adjusts a Float parameter with rotation (0 to 1).\nOpening sets the main parameter to Value.\nClosing returns it to zero.",
                        "회전으로 Float 파라미터를 조절합니다 (0~1).\n열면 기본 파라미터를 값으로 설정합니다.\n닫으면 0으로 되돌립니다.",
                        "回転でFloatパラメータを調整します (0〜1)。\n開くと主パラメータを値に設定します。\n閉じると0に戻します。");
            }
        }

        private static Rect Inset(Rect rect, float padding)
        {
            return new Rect(rect.x + padding, rect.y + padding, Mathf.Max(0, rect.width - padding * 2), Mathf.Max(0, rect.height - padding * 2));
        }

        private static Rect FieldArea(Rect rect, string label, float labelWidth = -1)
        {
            // Follow the Inspector label width so these fields line up with the Avatar and Submenu rows.
            float width = labelWidth < 0 ? Mathf.Clamp(EditorGUIUtility.labelWidth - 3, 40, rect.width * .45f) : Mathf.Min(labelWidth, rect.width - 25);
            GUI.Label(new Rect(rect.x, rect.y, width, rect.height), label);
            return new Rect(rect.x + width + 3, rect.y, Mathf.Max(10, rect.width - width - 3), rect.height);
        }

        private void Remember(string key, Rect rect)
        {
            if (Event.current.type == EventType.Repaint) GuiRects[key] = rect;
        }
    }
}
