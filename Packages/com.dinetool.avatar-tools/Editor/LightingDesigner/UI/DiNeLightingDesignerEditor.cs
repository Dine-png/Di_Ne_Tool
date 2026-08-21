#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

[CustomEditor(typeof(DiNeLightingDesigner))]
public sealed class DiNeLightingDesignerEditor : Editor
{
    private static readonly Dictionary<DiNeLightingControl, bool> Foldouts = new Dictionary<DiNeLightingControl, bool>();

    private DiNeLightingDesigner _designer;
    private bool _showAdvanced;
    private bool _showExcludes;
    private bool _showPresets;
    private bool _showGroups;
    private bool _showDiagnostics = true;

    private void OnEnable()
    {
        _designer = (DiNeLightingDesigner)target;
        _designer.EnsureDefaults();
    }

    public override void OnInspectorGUI()
    {
        // EnsureDefaults는 대상 오브젝트를 직접 건드리므로 반드시 Update() 앞에서 호출해야 한다.
        // 뒤에서 부르면 ApplyModifiedProperties가 옛 배열로 덮어써 추가된 항목이 사라진다.
        _designer.EnsureDefaults();
        serializedObject.Update();

        DrawHeaderBox();
        EditorGUILayout.Space();
        DrawBudget();
        DrawDiagnostics();
        EditorGUILayout.Space();
        DrawMenuSettings();
        EditorGUILayout.Space();
        DrawControls();
        EditorGUILayout.Space();
        DrawPresets();
        EditorGUILayout.Space();
        DrawGroups();
        EditorGUILayout.Space();
        DrawAdvanced();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeaderBox()
    {
        EditorGUILayout.LabelField("라이팅 디자이너", EditorStyles.boldLabel);

        var descriptor = _designer.GetComponentInParent<VRCAvatarDescriptor>();
        if (descriptor == null)
            return;   // 진단 패널이 오류로 안내한다.

        EditorGUILayout.HelpBox(
            $"'{descriptor.name}'에 업로드 시 자동으로 설치됩니다. Modular Avatar는 필요 없습니다.",
            MessageType.Info);

        int targetCount = DiNeLightingDiagnostics.CollectTargetRenderers(descriptor, _designer).Count;
        if (targetCount > 0)
            EditorGUILayout.LabelField("대상 렌더러", targetCount + "개");
    }

    private void DrawBudget()
    {
        var descriptor = _designer.GetComponentInParent<VRCAvatarDescriptor>();
        int cost = _designer.CalculateParameterCost();
        int used = DiNeLightingDiagnostics.CalculateOtherCost(descriptor);
        int total = used + cost;
        int max = VRCExpressionParameters.MAX_PARAMETER_COST;

        // 예산 막대. 초과분은 진단 패널이 오류로 다시 짚어준다.
        var rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
        float fill = max > 0 ? Mathf.Clamp01(total / (float)max) : 0f;
        EditorGUI.ProgressBar(rect, fill,
            $"동기화 파라미터  {total} / {max} bits   (라이팅 디자이너 {cost})");

        if (_designer.Presets.Any(preset => preset != null))
        {
            EditorGUILayout.LabelField(
                "프리셋은 비동기화 파라미터 + Parameter Driver로 동작해 0 bit입니다.",
                EditorStyles.miniLabel);
        }
    }

    private void DrawDiagnostics()
    {
        var issues = DiNeLightingDiagnostics.Collect(_designer);
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("확인된 문제 없음.", MessageType.Info);
            return;
        }

        int errors = issues.Count(issue => issue.Severity == MessageType.Error);
        int warnings = issues.Count(issue => issue.Severity == MessageType.Warning);

        string summary = errors > 0
            ? $"문제 {errors}건 · 경고 {warnings}건"
            : $"경고 {warnings}건";

        _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, summary, true);
        if (!_showDiagnostics)
            return;

        // 오류를 먼저 보여준다.
        foreach (var issue in issues.OrderBy(issue => issue.Severity == MessageType.Error ? 0 : 1))
            EditorGUILayout.HelpBox(issue.Message, issue.Severity);
    }

    private void DrawMenuSettings()
    {
        EditorGUILayout.LabelField("메뉴", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("menuName"), new GUIContent("메뉴 이름"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("menuIcon"), new GUIContent("메뉴 아이콘"));

            var useEnable = serializedObject.FindProperty("useEnableToggle");
            EditorGUILayout.PropertyField(useEnable, new GUIContent("켜기/끄기 토글", "끄면 원래 머티리얼 값으로 돌아갑니다. 1 bit."));
            if (useEnable.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDefaultOn"), new GUIContent("기본 켜짐"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("enableSaved"), new GUIContent("값 저장"));
                }
            }
        }
    }

    private void DrawControls()
    {
        EditorGUILayout.LabelField("제어 항목", EditorStyles.boldLabel);

        var controlsProperty = serializedObject.FindProperty("controls");

        for (int i = 0; i < controlsProperty.arraySize; i++)
        {
            var element = controlsProperty.GetArrayElementAtIndex(i);
            var controlProperty = element.FindPropertyRelative("control");
            var control = (DiNeLightingControl)controlProperty.intValue;
            var def = DiNeLightingControlDef.Get(control);
            if (def == null) continue;

            var enabledProperty = element.FindPropertyRelative("enabled");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    enabledProperty.boolValue = EditorGUILayout.ToggleLeft(
                        new GUIContent($"{def.DisplayName}  ({def.CostBits} bits)", def.Tooltip),
                        enabledProperty.boolValue,
                        EditorStyles.boldLabel);

                    if (enabledProperty.boolValue)
                    {
                        Foldouts.TryGetValue(control, out bool expanded);
                        Foldouts[control] = GUILayout.Toggle(expanded, "설정", EditorStyles.miniButton, GUILayout.Width(48f));
                    }
                }

                if (!enabledProperty.boolValue)
                    continue;

                EditorGUILayout.LabelField(
                    "지원 셰이더", DiNeShaderProfile.DescribeSupport(control), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    "파라미터", DiNeLightingDesigner.BuildParameterName(control), EditorStyles.miniLabel);

                if (!Foldouts.TryGetValue(control, out bool open) || !open)
                    continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    if (def.Kind == DiNeLightingControlKind.Radial)
                    {
                        EditorGUILayout.PropertyField(
                            element.FindPropertyRelative("initialValue"),
                            new GUIContent("초기값", "메뉴에 처음 들어갔을 때의 위치."));
                    }
                    else
                    {
                        var initial = element.FindPropertyRelative("initialValue");
                        initial.floatValue = EditorGUILayout.Toggle("기본 켜짐", initial.floatValue > 0.5f) ? 1f : 0f;
                    }

                    EditorGUILayout.PropertyField(element.FindPropertyRelative("saved"), new GUIContent("값 저장"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("displayNameOverride"), new GUIContent("표시 이름"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("icon"), new GUIContent("아이콘"));

                    DrawControlExtras(control);
                }
            }
        }
    }

    /// <summary>항목별로 추가 설정이 필요한 것들.</summary>
    private void DrawControlExtras(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
            case DiNeLightingControl.LightMax:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minLightValue"), new GUIContent("범위 하한"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxLightValue"), new GUIContent("범위 상한"));
                break;

            case DiNeLightingControl.OutlineTint:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineTintFrom"), new GUIContent("0일 때 색"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineTintTo"), new GUIContent("1일 때 색"));
                break;

            case DiNeLightingControl.OutlineWidth:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineWidthMax"), new GUIContent("최대 두께"));
                break;

            case DiNeLightingControl.Reflectance:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectanceMax"), new GUIContent("최대 반사율"));
                break;

            case DiNeLightingControl.LightDirection:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDirection"), new GUIContent("고정 방향"));
                break;

            case DiNeLightingControl.ColorTemperature:
            case DiNeLightingControl.Saturation:
            case DiNeLightingControl.Hue:
            case DiNeLightingControl.Brightness:
            case DiNeLightingControl.Gamma:
                EditorGUILayout.HelpBox(
                    "이 항목을 켜면 업로드 시 머티리얼을 복제해 색 보정을 텍스처에 구워 넣습니다(원본 에셋은 그대로).\n" +
                    "lilToon과 Poiyomi 모두 각 셰이더 본체와 동일한 수식으로 굽기 때문에, 슬라이더 중앙이 원본 색이 됩니다.",
                    MessageType.Info);
                break;
        }
    }

    private void DrawPresets()
    {
        var presetsProperty = serializedObject.FindProperty("presets");

        using (new EditorGUILayout.HorizontalScope())
        {
            _showPresets = EditorGUILayout.Foldout(_showPresets, $"프리셋 ({presetsProperty.arraySize})", true);
            if (GUILayout.Button("추가", EditorStyles.miniButton, GUILayout.Width(48f)))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(_designer, "Add Lighting Preset");
                _designer.Presets.Add(new DiNeLightingDesigner.PresetEntry
                {
                    name = "프리셋 " + (_designer.Presets.Count + 1)
                });
                EditorUtility.SetDirty(_designer);
                serializedObject.Update();
                _showPresets = true;
                return;
            }
        }

        if (!_showPresets) return;

        EditorGUILayout.HelpBox(
            "프리셋은 버튼을 누르면 슬라이더들을 지정한 값으로 옮깁니다. VRC Parameter Driver로 동작하고 " +
            "프리셋 파라미터는 동기화하지 않기 때문에 동기화 비트를 추가로 쓰지 않습니다.\n" +
            "누른 뒤에도 슬라이더를 이어서 직접 조절할 수 있습니다.",
            MessageType.Info);

        var enabledControls = _designer.Controls.Where(setting => setting != null && setting.enabled).ToList();
        if (enabledControls.Count == 0)
        {
            EditorGUILayout.HelpBox("먼저 제어 항목을 켜야 프리셋에 넣을 수 있습니다.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < presetsProperty.arraySize; i++)
        {
            var element = presetsProperty.GetArrayElementAtIndex(i);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("name"), GUIContent.none);
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("icon"), GUIContent.none, GUILayout.Width(64f));
                    if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(40f)))
                    {
                        presetsProperty.DeleteArrayElementAtIndex(i);
                        return;
                    }
                }

                var preset = i < _designer.Presets.Count ? _designer.Presets[i] : null;
                if (preset == null) continue;

                foreach (var setting in enabledControls)
                {
                    var def = DiNeLightingControlDef.Get(setting.control);
                    if (def == null) continue;

                    bool included = preset.TryGetValue(setting.control, out float value);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool nowIncluded = EditorGUILayout.ToggleLeft(
                            new GUIContent(def.DisplayName, def.Tooltip), included, GUILayout.Width(150f));

                        using (new EditorGUI.DisabledScope(!nowIncluded))
                        {
                            float newValue = def.Kind == DiNeLightingControlKind.Toggle
                                ? (EditorGUILayout.Toggle(value > 0.5f) ? 1f : 0f)
                                : EditorGUILayout.Slider(value, 0f, 1f);

                            if (nowIncluded && (!included || !Mathf.Approximately(newValue, value)))
                            {
                                serializedObject.ApplyModifiedProperties();
                                Undo.RecordObject(_designer, "Edit Lighting Preset");
                                preset.SetValue(setting.control, newValue);
                                EditorUtility.SetDirty(_designer);
                                serializedObject.Update();
                            }
                        }

                        if (!nowIncluded && included)
                        {
                            serializedObject.ApplyModifiedProperties();
                            Undo.RecordObject(_designer, "Edit Lighting Preset");
                            preset.Remove(setting.control);
                            EditorUtility.SetDirty(_designer);
                            serializedObject.Update();
                        }
                    }
                }
            }
        }
    }

    private void DrawGroups()
    {
        var groupsProperty = serializedObject.FindProperty("groups");

        using (new EditorGUILayout.HorizontalScope())
        {
            _showGroups = EditorGUILayout.Foldout(_showGroups, $"렌더러 그룹 ({groupsProperty.arraySize})", true);
            if (GUILayout.Button("추가", EditorStyles.miniButton, GUILayout.Width(48f)))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(_designer, "Add Lighting Group");
                _designer.Groups.Add(new DiNeLightingDesigner.RendererGroup
                {
                    name = "그룹 " + (_designer.Groups.Count + 1)
                });
                EditorUtility.SetDirty(_designer);
                serializedObject.Update();
                _showGroups = true;
                return;
            }
        }

        if (!_showGroups) return;

        EditorGUILayout.HelpBox(
            "그룹에 넣은 렌더러는 선택한 항목에 한해 별도의 슬라이더로 조절됩니다(예: 얼굴만 밝게). " +
            "선택하지 않은 항목은 공용 슬라이더를 그대로 따라갑니다.\n" +
            "항목을 따로 뗄 때마다 파라미터가 그만큼 더 듭니다.",
            MessageType.Info);

        var enabledControls = _designer.Controls.Where(setting => setting != null && setting.enabled).ToList();

        for (int i = 0; i < groupsProperty.arraySize; i++)
        {
            var element = groupsProperty.GetArrayElementAtIndex(i);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("name"), GUIContent.none);
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("icon"), GUIContent.none, GUILayout.Width(64f));
                    if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(40f)))
                    {
                        groupsProperty.DeleteArrayElementAtIndex(i);
                        return;
                    }
                }

                EditorGUILayout.PropertyField(element.FindPropertyRelative("renderers"), new GUIContent("렌더러"), true);

                var group = i < _designer.Groups.Count ? _designer.Groups[i] : null;
                if (group == null) continue;

                EditorGUILayout.LabelField("따로 조절할 항목", EditorStyles.miniBoldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var setting in enabledControls)
                    {
                        var def = DiNeLightingControlDef.Get(setting.control);
                        if (def == null) continue;

                        bool separated = group.Separates(setting.control);
                        bool now = EditorGUILayout.ToggleLeft(
                            new GUIContent($"{def.DisplayName}  (+{def.CostBits} bits)", def.Tooltip), separated);

                        if (now == separated) continue;

                        serializedObject.ApplyModifiedProperties();
                        Undo.RecordObject(_designer, "Edit Lighting Group");
                        if (now) group.separateControls.Add(setting.control);
                        else group.separateControls.Remove(setting.control);
                        EditorUtility.SetDirty(_designer);
                        serializedObject.Update();
                    }
                }
            }
        }
    }

    private void DrawAdvanced()
    {
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "고급 설정", true);
        if (!_showAdvanced) return;

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetShaders"), new GUIContent("대상 셰이더"));

            var overwrite = serializedObject.FindProperty("overwriteDefaultLightMinMax");
            EditorGUILayout.PropertyField(overwrite,
                new GUIContent("기본 밝기 덮어쓰기", "끄면 끄기 상태에서 머티리얼 원래의 라이트 상/하한으로 돌아갑니다."));

            if (overwrite.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultMinLightValue"), new GUIContent("끄기 상태 최소 밝기"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultMaxLightValue"), new GUIContent("끄기 상태 최대 밝기"));
            }

            _showExcludes = EditorGUILayout.Foldout(_showExcludes, "제외할 렌더러", true);
            if (_showExcludes)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("excludes"), new GUIContent("제외 목록"), true);
        }
    }

}
#endif
