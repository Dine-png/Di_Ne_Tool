using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DiNeTool.ExpressionEditor
{
    [CustomEditor(typeof(VRCExpressionParameters))]
    public sealed class DiNeExpressionParametersEditor : DiNeExpressionInspectorBase
    {
        private string search = "";
        private string newName = "NewParameter";
        private VRCExpressionParameters.ValueType newType = VRCExpressionParameters.ValueType.Bool;
        private VRCExpressionParameters mergeSource;
        private VRCExpressionParameters Parameters => (VRCExpressionParameters)target;
        protected override string SdkEditorName => "VRC.SDK3.Editor.VRCExpressionParametersEditor";
        protected override string InspectorTitle => T("Parameters", "파라미터", "パラメータ");
        protected override string InspectorDescription => T("Edit parameters, check memory and merge missing names.",
            "파라미터를 편집하고 사용량을 확인하며 누락된 이름을 병합합니다.", "パラメータを編集し、使用量を確認して不足している名前を統合します。");

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Parameters != null) FindAvatar(a => a.expressionParameters == Parameters);
        }

        protected override void DrawContents()
        {
            SerializedProperty parameters = serializedObject.FindProperty("parameters");
            if (parameters == null)
            {
                EditorGUILayout.HelpBox(T("This SDK parameter format is unsupported.", "이 SDK 파라미터 형식은 지원하지 않습니다.",
                    "このSDKパラメータ形式はサポートされていません。"), MessageType.Error);
                return;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Animator lookup", "Animator 조회", "Animator参照"), EditorStyles.boldLabel);
                DrawAvatarContext();
            }
            GUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Add parameter", "파라미터 추가", "パラメータを追加"), EditorStyles.boldLabel);
                newName = EditorGUILayout.TextField(C("Name", "이름", "名前"), newName);
                newType = (VRCExpressionParameters.ValueType)EditorGUILayout.EnumPopup(C("Type", "유형", "種類"), newType);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
                        if (ActionButton(C("Add", "추가", "追加"), true))
                            RunAction(() => Status = DiNeExpressionUtility.AddParameter(Parameters, newName, newType,
                                T("Add Expression Parameter", "Expression 파라미터 추가", "Expressionパラメータを追加"))
                                ? T("Parameter added.", "파라미터를 추가했습니다.", "パラメータを追加しました。")
                                : T("Could not add the parameter. Check duplicate names and memory budget.", "추가하지 못했습니다. 이름 중복과 메모리 한도를 확인하세요.", "追加できませんでした。名前の重複とメモリ上限を確認してください。"));
                    using (new EditorGUI.DisabledScope(AnimatorParameters.Count == 0))
                        if (ActionButton(C("Choose from Animator", "Animator에서 선택", "Animatorから選択")))
                        {
                            var choices = new GenericMenu();
                            foreach (var parameter in AnimatorParameters)
                            {
                                var captured = parameter;
                                choices.AddItem(new GUIContent(parameter.name + " (" + parameter.type + ")"), false, () =>
                                {
                                    newName = captured.name;
                                    newType = ToExpressionType(captured.type);
                                    Repaint();
                                });
                            }
                            choices.ShowAsContext();
                        }
                }
            }
            GUILayout.Space(8);
            search = EditorGUILayout.TextField(C("Search", "검색", "検索"), search);
            GUILayout.Space(4);
            bool visible = false;
            for (int i = 0; i < parameters.arraySize; i++)
            {
                SerializedProperty parameter = parameters.GetArrayElementAtIndex(i);
                string name = parameter.FindPropertyRelative("name").stringValue;
                if (!string.IsNullOrEmpty(search) && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                visible = true;
                DrawParameter(parameter, i, parameters.arraySize);
                GUILayout.Space(4);
            }
            if (!visible)
                EditorGUILayout.HelpBox(parameters.arraySize == 0
                    ? T("No parameters. Add a name above; this inspector keeps an empty asset empty.", "파라미터가 없습니다. 위에서 이름을 추가하세요. 빈 에셋에 기본 항목을 자동으로 넣지 않습니다.",
                        "パラメータがありません。上で名前を追加してください。空のアセットに既定の項目を自動追加しません。")
                    : T("No matching parameters.", "검색 결과가 없습니다.", "一致するパラメータがありません。"), MessageType.Info);
            GUILayout.Space(8);
            DrawMerge();
            GUILayout.Space(8);
            DrawDiagnostics();
        }

        private void DrawParameter(SerializedProperty parameter, int index, int count)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Parameter ", "파라미터 ", "パラメータ ") + (index + 1), EditorStyles.boldLabel);
                DrawParameterName(parameter.FindPropertyRelative("name"), Parameters);
                SerializedProperty type = parameter.FindPropertyRelative("valueType");
                EditorGUILayout.PropertyField(type, C("Type", "유형", "種類"));
                SerializedProperty value = parameter.FindPropertyRelative("defaultValue");
                var valueType = (VRCExpressionParameters.ValueType)type.intValue;
                EditorGUI.BeginChangeCheck();
                if (valueType == VRCExpressionParameters.ValueType.Bool)
                {
                    bool state = EditorGUILayout.Toggle(C("Default", "기본값", "初期値"), value.floatValue != 0);
                    if (EditorGUI.EndChangeCheck()) value.floatValue = state ? 1 : 0;
                }
                else if (valueType == VRCExpressionParameters.ValueType.Int)
                {
                    int state = EditorGUILayout.IntField(C("Default", "기본값", "初期値"), Mathf.RoundToInt(value.floatValue));
                    if (EditorGUI.EndChangeCheck()) value.floatValue = Mathf.Clamp(state, 0, 255);
                }
                else
                {
                    float state = EditorGUILayout.FloatField(C("Default", "기본값", "初期値"), value.floatValue);
                    if (EditorGUI.EndChangeCheck()) value.floatValue = Mathf.Clamp(state, -1, 1);
                }
                EditorGUILayout.PropertyField(parameter.FindPropertyRelative("saved"), C("Saved", "저장", "保存",
                    "Keep this value when changing avatars or worlds.", "아바타나 월드를 바꿔도 값을 유지합니다.", "アバターやワールドを変更しても値を維持します。"));
                SerializedProperty synced = parameter.FindPropertyRelative("networkSynced");
                if (synced != null) EditorGUILayout.PropertyField(synced, C("Synced", "동기화", "同期",
                    "Send this parameter to other players; synced parameters consume memory budget.", "다른 플레이어에게 값을 전송합니다. 동기화 파라미터는 메모리 한도를 사용합니다.",
                    "他のプレイヤーに値を送信します。同期するパラメータはメモリ上限を使用します。"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(index == 0))
                        if (ActionButton(C("Up", "위로", "上へ"))) RunAction(() => MoveParameter(index, index - 1));
                    using (new EditorGUI.DisabledScope(index == count - 1))
                        if (ActionButton(C("Down", "아래로", "下へ"))) RunAction(() => MoveParameter(index, index + 1));
                    if (ActionButton(C("Delete", "삭제", "削除"), destructive: true)) RunAction(() => RemoveParameter(index));
                }
            }
        }

        private void MoveParameter(int from, int to)
        {
            serializedObject.Update();
            serializedObject.FindProperty("parameters").MoveArrayElement(from, to);
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveParameter(int index)
        {
            serializedObject.Update();
            var parameters = serializedObject.FindProperty("parameters");
            parameters.DeleteArrayElementAtIndex(index);
            var empty = serializedObject.FindProperty("isEmpty");
            if (empty != null) empty.boolValue = parameters.arraySize == 0;
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMerge()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Merge and cleanup", "병합과 정리", "統合と整理"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(T("Merge adds missing names. Existing names and conflicting duplicates are preserved.",
                    "병합은 누락된 이름만 추가합니다. 기존 이름과 설정이 다른 중복은 보존합니다.", "統合は不足している名前だけを追加します。既存の名前と設定が異なる重複は維持します。"), EditorStyles.wordWrappedMiniLabel);
                mergeSource = (VRCExpressionParameters)EditorGUILayout.ObjectField(C("Source", "가져올 에셋", "統合元"), mergeSource, typeof(VRCExpressionParameters), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(mergeSource == null || mergeSource == Parameters))
                        if (ActionButton(C("Merge missing", "누락 항목 병합", "不足項目を統合"), true))
                            RunAction(() =>
                            {
                                int count = DiNeExpressionUtility.MergeParameters(Parameters, mergeSource, T("Merge Expression Parameters", "Expression 파라미터 병합", "Expressionパラメータを統合"));
                                Status = count < 0 ? T("Merge cancelled: memory budget exceeded. Neither asset was changed.", "메모리 한도 초과로 병합하지 않았습니다. 두 에셋 모두 보존됩니다.",
                                    "メモリ上限を超えたため統合を中止しました。両方のアセットは維持されます。")
                                    : string.Format(T("Added {0} parameters. Existing values were kept.", "{0}개 파라미터를 추가했습니다. 기존 값은 유지됩니다.", "{0}個のパラメータを追加しました。既存の値は維持されます。"), count);
                            });
                    if (ActionButton(C("Clean duplicates", "중복 정리", "重複を整理")))
                        RunAction(() =>
                        {
                            int count = DiNeExpressionUtility.CleanupParameters(Parameters, T("Clean Expression Parameters", "Expression 파라미터 정리", "Expressionパラメータを整理"));
                            Status = string.Format(T("Removed {0} empty or identical duplicate entries. Conflicting settings were kept.",
                                "빈 이름 또는 설정이 같은 중복 {0}개를 정리했습니다. 충돌하는 설정은 보존됩니다.", "空の名前または同一設定の重複を{0}個削除しました。競合する設定は維持されます。"), count);
                        });
                }
            }
        }

        private void DrawDiagnostics()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(T("Diagnostics", "진단", "診断"), EditorStyles.boldLabel);
                int cost = Parameters.parameters == null ? 0 : Parameters.CalcTotalCost();
                int limit = VRCExpressionParameters.MAX_PARAMETER_COST;
                Rect bar = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(bar, Mathf.Clamp01((float)cost / limit), T("Synced memory", "동기화 사용량", "同期使用量") + $": {cost}/{limit} bit");
                if (cost > limit) EditorGUILayout.HelpBox(T("Synced parameter memory exceeds the SDK limit.", "동기화 파라미터 사용량이 SDK 한도를 초과했습니다.", "同期パラメータの使用量がSDK上限を超えています。"), MessageType.Error);
                var parameters = Parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>();
                if (parameters.Any(p => p == null || string.IsNullOrWhiteSpace(p.name)))
                    EditorGUILayout.HelpBox(T("Empty parameter names found. Cleanup can remove them.", "빈 파라미터 이름이 있습니다. 중복 정리로 제거할 수 있습니다.",
                        "空のパラメータ名があります。重複整理で削除できます。"), MessageType.Warning);
                foreach (var duplicate in parameters.Where(p => p != null && !string.IsNullOrWhiteSpace(p.name)).GroupBy(p => p.name).Where(g => g.Count() > 1))
                    EditorGUILayout.HelpBox(T("Duplicate name (conflicting settings are preserved): ", "중복 이름 (설정 충돌은 보존됨): ", "重複した名前（競合する設定は維持）: ") + duplicate.Key, MessageType.Warning);
                foreach (var parameter in parameters.Where(p => p != null && !string.IsNullOrWhiteSpace(p.name)))
                {
                    var animator = AnimatorParameters.FirstOrDefault(p => p.name == parameter.name);
                    if (Avatar != null && animator == null)
                        EditorGUILayout.HelpBox(T("Not found in the selected avatar's supported Animator parameters: ", "선택한 아바타의 지원되는 Animator 파라미터에서 찾지 못한 이름: ",
                            "選択したアバターの対応Animatorパラメータで見つからない名前: ") + parameter.name, MessageType.Warning);
                    else if (animator != null && ToExpressionType(animator.type) != parameter.valueType)
                        EditorGUILayout.HelpBox(T("Animator type differs: ", "Animator 유형이 다릅니다: ", "Animatorの種類が異なります: ") + parameter.name, MessageType.Warning);
                    bool validDefault = !float.IsNaN(parameter.defaultValue) && !float.IsInfinity(parameter.defaultValue) &&
                        (parameter.valueType == VRCExpressionParameters.ValueType.Bool ? parameter.defaultValue == 0 || parameter.defaultValue == 1 :
                            parameter.valueType == VRCExpressionParameters.ValueType.Int ? parameter.defaultValue >= 0 && parameter.defaultValue <= 255 && parameter.defaultValue == Mathf.Floor(parameter.defaultValue) :
                            parameter.defaultValue >= -1 && parameter.defaultValue <= 1);
                    if (!validDefault) EditorGUILayout.HelpBox(T("Default value is outside the supported range: ", "기본값이 지원 범위를 벗어났습니다: ", "初期値が対応範囲外です: ") + parameter.name, MessageType.Warning);
                }
            }
        }
    }
}
