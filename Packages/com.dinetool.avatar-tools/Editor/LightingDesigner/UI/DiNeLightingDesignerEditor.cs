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
    private static readonly Color MintActive = new Color(0.30f, 0.82f, 0.76f);

    // Light Limit Changer의 기본 흐름과 같은 범위: 밝기 + 자주 쓰는 추가 제어만 먼저 보여준다.
    private static readonly DiNeLightingControl[] SimpleAdditionalControls =
    {
        DiNeLightingControl.ColorTemperature,
        DiNeLightingControl.Saturation,
        DiNeLightingControl.Monochrome,
    };

    private static readonly DiNeLightingControl[] AdvancedControls =
    {
        DiNeLightingControl.Hue,
        DiNeLightingControl.Brightness,
        DiNeLightingControl.Gamma,
        DiNeLightingControl.Emission,
        DiNeLightingControl.LightDirection,
        DiNeLightingControl.ShadowStrength,
        DiNeLightingControl.OutlineTint,
        DiNeLightingControl.OutlineWidth,
        DiNeLightingControl.Reflectance,
    };

    private DiNeLightingDesigner _designer;
    private DiNeLightingDesignerPreset _settingsPreset;
    private Texture2D _windowIcon;
    private Font _titleFont;
    private int _settingsMode;
    private bool _showExcludes;
    private bool _showMenuPresets;
    private bool _showGroups;
    private bool _showDiagnostics = true;

    private DiNeLightingLanguage CurrentLanguage
    {
        get => DiNeLightingLocalization.CurrentLanguage;
        set => DiNeLightingLocalization.CurrentLanguage = value;
    }

    private static string T(string korean, string english, string japanese)
    {
        return DiNeLightingLocalization.T(korean, english, japanese);
    }

    private void OnEnable()
    {
        _designer = (DiNeLightingDesigner)target;
        _windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        _titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
        _designer.EnsureDefaults();
    }

    public override void OnInspectorGUI()
    {
        // EnsureDefaults는 대상 오브젝트를 직접 건드리므로 반드시 Update() 앞에서 호출해야 한다.
        // 뒤에서 부르면 ApplyModifiedProperties가 옛 배열로 덮어써 추가된 항목이 사라진다.
        _designer.EnsureDefaults();
        serializedObject.Update();

        DrawDesignerHeader();
        GUILayout.Space(5f);
        int languageIndex = DrawCustomToolbar(
            (int)CurrentLanguage,
            DiNeLightingLocalization.LanguageButtonLabels,
            35f);
        CurrentLanguage = (DiNeLightingLanguage)languageIndex;
        GUILayout.Space(15f);

        DrawSettingsPreset();
        EditorGUILayout.Space(8f);
        DrawModeTabs();
        EditorGUILayout.Space(8f);

        if (_settingsMode == 0)
            DrawSimpleSettings();
        else
            DrawAdvancedSettings();

        EditorGUILayout.Space(8f);
        DrawStatus();

        if (serializedObject.ApplyModifiedProperties())
        {
            _designer.EnsureDefaults();
            EditorUtility.SetDirty(_designer);
        }
    }

    private void DrawDesignerHeader()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            font = _titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 36
        };
        float iconSize = 72f;
        GUILayout.Label(_windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6f);
        GUILayout.Label("Lighting Designer", titleStyle, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4f);
        string description = T(
            "lilToon과 Poiyomi 아바타의 라이트 제한과 색감을 메뉴에서 손쉽게 조절합니다.",
            "Adjust lilToon and Poiyomi avatar light limits and colors from the VRChat menu.",
            "lilToonとPoiyomiアバターのライト制限と色味をVRChatメニューから調整します。");
        GUILayout.Label(description, new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
        });

        GUILayout.Space(5f);
        EditorGUILayout.EndVertical();
    }

    private static int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = i == selected
                ? new Color(0.30f, 0.82f, 0.76f)
                : new Color(0.5f, 0.5f, 0.5f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal =
                {
                    textColor = i == selected
                        ? Color.white
                        : new Color(0.8f, 0.8f, 0.8f)
                }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
                newSelected = i;
            GUI.backgroundColor = previousBackground;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }

    private void DrawSettingsPreset()
    {
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("설정 프리셋", "Settings Preset", "設定プリセット"), EditorStyles.boldLabel);
            _settingsPreset = (DiNeLightingDesignerPreset)EditorGUILayout.ObjectField(
                new GUIContent(
                    T("프리셋", "Preset", "プリセット"),
                    T(
                        "프로젝트에 저장해 다른 아바타에도 적용할 수 있는 라이팅 설정입니다.",
                        "A reusable lighting setup saved in the project for other avatars.",
                        "プロジェクトに保存し、別のアバターにも使えるライティング設定です。")),
                _settingsPreset,
                typeof(DiNeLightingDesignerPreset),
                false);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_settingsPreset == null))
                {
                    if (MintButton(T("불러오기", "Load", "読み込み"), 24f))
                        ApplySelectedPreset();

                    if (GUILayout.Button(
                        T("현재 설정으로 덮어쓰기", "Overwrite with Current", "現在の設定で上書き"),
                        GUILayout.Height(24f)))
                        OverwriteSelectedPreset();
                }
            }

            if (GUILayout.Button(
                T("새 프리셋으로 저장", "Save as New Preset", "新規プリセットとして保存"),
                GUILayout.Height(26f)))
                SaveNewPreset();

            EditorGUILayout.LabelField(
                T(
                    "렌더러 그룹과 제외 목록은 아바타마다 다르므로 프리셋 적용 시 현재 값을 유지합니다.",
                    "Renderer groups and exclusions are avatar-specific, so their current values are preserved.",
                    "レンダラーグループと除外リストはアバター固有のため、現在の値を維持します。"),
                EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void DrawModeTabs()
    {
        _settingsMode = DrawCustomToolbar(
            _settingsMode,
            new[] { T("심플 설정", "Simple", "シンプル設定"), T("심화 설정", "Advanced", "詳細設定") },
            35f);
    }

    private void DrawSimpleSettings()
    {
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("기본 동작", "General", "基本動作"), EditorStyles.boldLabel);

            var useEnable = serializedObject.FindProperty("useEnableToggle");
            EditorGUILayout.PropertyField(
                useEnable,
                new GUIContent(
                    T("켜기/끄기 토글", "Enable Toggle", "ON/OFFトグル"),
                    T(
                        "끄면 원래 머티리얼 값으로 돌아갑니다. 1 bit.",
                        "When disabled, materials return to their original values. 1 bit.",
                        "OFFにするとマテリアルの元の値に戻ります。1 bit。")));
            if (useEnable.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("enableDefaultOn"),
                        new GUIContent(T("기본 켜짐", "Default On", "デフォルトON")));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("enableSaved"),
                        new GUIContent(T("값 저장", "Saved", "値を保存")));
                }
            }
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("조명 밝기", "Lighting Brightness", "ライティング明るさ"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T(
                    "VRChat 메뉴의 슬라이더 하나로 아바타 조명 밝기를 조절합니다.",
                    "Adjust avatar lighting with a single slider in the VRChat menu.",
                    "VRChatメニューの1つのスライダーでアバターの明るさを調整します。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);

            DrawControlCard(DiNeLightingControl.LightMin);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("추가 제어", "Additional Controls", "追加制御"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T(
                    "필요한 기능만 켜면 메뉴와 파라미터도 그만큼만 생성됩니다.",
                    "Only enabled controls create menu items and parameters.",
                    "有効にした機能だけメニューとパラメーターが生成されます。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);

            foreach (var control in SimpleAdditionalControls)
                DrawControlCard(control);

            DrawTextureBakeNotice(SimpleAdditionalControls);
        }
    }

    private void DrawAdvancedSettings()
    {
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("심화 제어 항목", "Advanced Controls", "詳細制御項目"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T(
                    "색 보정, 발광, 라이트 방향, 그림자와 외곽 효과를 세부 조절합니다.",
                    "Fine-tune color correction, emission, light direction, shadows, and edge effects.",
                    "色補正、発光、ライト方向、影、輪郭効果を細かく調整します。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);

            foreach (var control in AdvancedControls)
                DrawControlCard(control);

            DrawTextureBakeNotice(AdvancedControls);
        }

        EditorGUILayout.Space(8f);
        DrawTargetOptions();
        EditorGUILayout.Space(8f);
        DrawMenuPresets();
        EditorGUILayout.Space(8f);
        DrawGroups();
    }

    private void DrawControlCard(DiNeLightingControl control)
    {
        var element = FindControlProperty(control);
        var def = DiNeLightingControlDef.Get(control);
        if (element == null || def == null) return;

        var enabled = element.FindPropertyRelative("enabled");
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            string displayName = DiNeLightingLocalization.ControlName(control);
            string tooltip = DiNeLightingLocalization.ControlTooltip(control);
            enabled.boolValue = EditorGUILayout.ToggleLeft(
                new GUIContent($"{displayName}  ({def.CostBits} bits)", tooltip),
                enabled.boolValue,
                EditorStyles.boldLabel);

            if (!enabled.boolValue) return;

            // 항목 설정은 별도 버튼 없이 활성화 즉시 노출한다.
            using (new EditorGUI.IndentLevelScope())
            {
                if (def.Kind == DiNeLightingControlKind.Radial)
                {
                    if (control == DiNeLightingControl.LightMin)
                        DrawLightingRange();

                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("initialValue"),
                        new GUIContent(
                            T("초기값", "Initial Value", "初期値"),
                            T(
                                "메뉴에 처음 들어갔을 때의 위치입니다. 0은 최소 밝기, 1은 최대 밝기입니다.",
                                "Initial menu position. 0 uses minimum brightness and 1 uses maximum brightness.",
                                "メニューの初期位置です。0で最小明るさ、1で最大明るさになります。")));
                }
                else
                {
                    var initial = element.FindPropertyRelative("initialValue");
                    initial.floatValue = EditorGUILayout.Toggle(
                        T("기본 켜짐", "Default On", "デフォルトON"),
                        initial.floatValue > 0.5f) ? 1f : 0f;
                }

                EditorGUILayout.PropertyField(
                    element.FindPropertyRelative("saved"),
                    new GUIContent(T("값 저장", "Saved", "値を保存")));

                DrawControlExtras(control);
            }
        }
    }

    private void DrawControlExtras(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.OutlineTint:
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("outlineTintFrom"),
                    new GUIContent(T("0일 때 색", "Color at 0", "0のときの色")));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("outlineTintTo"),
                    new GUIContent(T("1일 때 색", "Color at 1", "1のときの色")));
                break;

            case DiNeLightingControl.OutlineWidth:
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("outlineWidthMax"),
                    new GUIContent(T("최대 두께", "Maximum Width", "最大幅")));
                break;

            case DiNeLightingControl.Reflectance:
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("reflectanceMax"),
                    new GUIContent(T("최대 반사율", "Maximum Reflectance", "最大反射率")));
                break;

            case DiNeLightingControl.LightDirection:
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("lightDirection"),
                    new GUIContent(T("고정 방향", "Fixed Direction", "固定方向")));
                break;
        }
    }

    private void DrawLightingRange()
    {
        var maxLight = serializedObject.FindProperty("maxLightValue");
        var minLight = serializedObject.FindProperty("minLightValue");

        EditorGUILayout.PropertyField(
            maxLight,
            new GUIContent(
                T("최대 밝기", "Maximum Brightness", "最大明るさ"),
                T(
                    "VRChat 밝기 메뉴가 최댓값일 때 셰이더에 적용할 실제 밝기 수치입니다.",
                    "Actual shader brightness used at the maximum menu value.",
                    "VRChat明るさメニューが最大のときにシェーダーへ適用する実際の明るさです。")));

        EditorGUILayout.PropertyField(
            minLight,
            new GUIContent(
                T("최소 밝기", "Minimum Brightness", "最小明るさ"),
                T(
                    "VRChat 밝기 메뉴가 최솟값일 때 셰이더에 적용할 실제 밝기 수치입니다.",
                    "Actual shader brightness used at the minimum menu value.",
                    "VRChat明るさメニューが最小のときにシェーダーへ適用する実際の明るさです。")));

        if (minLight.floatValue > maxLight.floatValue)
            minLight.floatValue = maxLight.floatValue;
    }

    private void DrawTextureBakeNotice(IEnumerable<DiNeLightingControl> sectionControls)
    {
        bool needsBake = sectionControls.Any(control =>
            IsControlEnabled(control) &&
            (control == DiNeLightingControl.ColorTemperature ||
             control == DiNeLightingControl.Saturation ||
             control == DiNeLightingControl.Hue ||
             control == DiNeLightingControl.Brightness ||
             control == DiNeLightingControl.Gamma));

        if (!needsBake) return;

        EditorGUILayout.HelpBox(
            T(
                "색 보정은 업로드/플레이 시 복제한 머티리얼의 텍스처에 비파괴적으로 적용됩니다. 원본 에셋은 바뀌지 않습니다.",
                "Color correction is applied non-destructively to cloned material textures during upload and Play Mode. Original assets are unchanged.",
                "色補正はアップロード／プレイ時に複製したマテリアルのテクスチャへ非破壊で適用されます。元のアセットは変更されません。"),
            MessageType.Info);
    }

    private void DrawTargetOptions()
    {
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("대상", "Targets", "対象"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("targetShaders"),
                new GUIContent(T("대상 셰이더", "Target Shaders", "対象シェーダー")));

            _showExcludes = EditorGUILayout.Foldout(
                _showExcludes,
                T("제외할 렌더러", "Excluded Renderers", "除外するレンダラー"),
                true);
            if (_showExcludes)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("excludes"),
                        new GUIContent(T("제외 목록", "Exclusion List", "除外リスト")),
                        true);
                }
            }
        }
    }

    private void DrawMenuPresets()
    {
        var presetsProperty = serializedObject.FindProperty("presets");

        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showMenuPresets = EditorGUILayout.Foldout(
                    _showMenuPresets,
                    T(
                        $"VRChat 메뉴 프리셋 ({presetsProperty.arraySize})",
                        $"VRChat Menu Presets ({presetsProperty.arraySize})",
                        $"VRChatメニュープリセット ({presetsProperty.arraySize})"),
                    true);
                if (GUILayout.Button(T("추가", "Add", "追加"), EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(_designer, "Add Lighting Menu Preset");
                    _designer.Presets.Add(new DiNeLightingDesigner.PresetEntry
                    {
                        name = T("프리셋 ", "Preset ", "プリセット ") + (_designer.Presets.Count + 1)
                    });
                    EditorUtility.SetDirty(_designer);
                    serializedObject.Update();
                    _showMenuPresets = true;
                    return;
                }
            }

            if (!_showMenuPresets) return;

            EditorGUILayout.HelpBox(
                T(
                    "VRChat 메뉴 프리셋은 버튼 하나로 켜진 슬라이더들을 지정 값으로 옮깁니다. 비동기화 Parameter Driver를 사용하므로 추가 동기화 비트가 들지 않습니다.",
                    "A VRChat menu preset moves enabled sliders to saved values with one button. It uses unsynced Parameter Drivers, so it costs no additional synced bits.",
                    "VRChatメニュープリセットは、ボタン1つで有効なスライダーを保存値へ移動します。非同期Parameter Driverを使うため追加の同期ビットは不要です。"),
                MessageType.Info);

            var enabledControls = _designer.Controls
                .Where(setting => setting != null && setting.enabled)
                .ToList();
            if (enabledControls.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "먼저 제어 항목을 켜야 메뉴 프리셋에 넣을 수 있습니다.",
                        "Enable at least one control before adding it to a menu preset.",
                        "メニュープリセットへ追加する前に、制御項目を1つ以上有効にしてください。"),
                    MessageType.Warning);
                return;
            }

            for (int i = 0; i < presetsProperty.arraySize; i++)
            {
                var element = presetsProperty.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope("GroupBox"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(element.FindPropertyRelative("name"), GUIContent.none);
                        EditorGUILayout.PropertyField(
                            element.FindPropertyRelative("icon"),
                            GUIContent.none,
                            GUILayout.Width(64f));
                        if (GUILayout.Button(T("삭제", "Delete", "削除"), EditorStyles.miniButton, GUILayout.Width(48f)))
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
                                new GUIContent(
                                    GetControlDisplayName(setting),
                                    DiNeLightingLocalization.ControlTooltip(setting.control)),
                                included,
                                GUILayout.Width(150f));

                            using (new EditorGUI.DisabledScope(!nowIncluded))
                            {
                                float newValue = def.Kind == DiNeLightingControlKind.Toggle
                                    ? (EditorGUILayout.Toggle(value > 0.5f) ? 1f : 0f)
                                    : EditorGUILayout.Slider(value, 0f, 1f);

                                if (nowIncluded && (!included || !Mathf.Approximately(newValue, value)))
                                {
                                    serializedObject.ApplyModifiedProperties();
                                    Undo.RecordObject(_designer, "Edit Lighting Menu Preset");
                                    preset.SetValue(setting.control, newValue);
                                    EditorUtility.SetDirty(_designer);
                                    serializedObject.Update();
                                }
                            }

                            if (!nowIncluded && included)
                            {
                                serializedObject.ApplyModifiedProperties();
                                Undo.RecordObject(_designer, "Edit Lighting Menu Preset");
                                preset.Remove(setting.control);
                                EditorUtility.SetDirty(_designer);
                                serializedObject.Update();
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawGroups()
    {
        var groupsProperty = serializedObject.FindProperty("groups");

        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showGroups = EditorGUILayout.Foldout(
                    _showGroups,
                    T(
                        $"렌더러 그룹 ({groupsProperty.arraySize})",
                        $"Renderer Groups ({groupsProperty.arraySize})",
                        $"レンダラーグループ ({groupsProperty.arraySize})"),
                    true);
                if (GUILayout.Button(T("추가", "Add", "追加"), EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(_designer, "Add Lighting Group");
                    _designer.Groups.Add(new DiNeLightingDesigner.RendererGroup
                    {
                        name = T("그룹 ", "Group ", "グループ ") + (_designer.Groups.Count + 1)
                    });
                    EditorUtility.SetDirty(_designer);
                    serializedObject.Update();
                    _showGroups = true;
                    return;
                }
            }

            if (!_showGroups) return;

            EditorGUILayout.HelpBox(
                T(
                    "그룹에 넣은 렌더러는 선택한 항목만 별도의 슬라이더로 조절합니다. 따로 떼는 항목마다 동기화 파라미터 비용이 추가됩니다.",
                    "Renderers in a group use separate sliders only for selected controls. Each separated control adds synced parameter cost.",
                    "グループ内のレンダラーは、選択した項目だけ個別スライダーで調整します。分離した項目ごとに同期パラメーターのコストが追加されます。"),
                MessageType.Info);

            var enabledControls = _designer.Controls
                .Where(setting => setting != null && setting.enabled)
                .ToList();

            for (int i = 0; i < groupsProperty.arraySize; i++)
            {
                var element = groupsProperty.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope("GroupBox"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(element.FindPropertyRelative("name"), GUIContent.none);
                        EditorGUILayout.PropertyField(
                            element.FindPropertyRelative("icon"),
                            GUIContent.none,
                            GUILayout.Width(64f));
                        if (GUILayout.Button(T("삭제", "Delete", "削除"), EditorStyles.miniButton, GUILayout.Width(48f)))
                        {
                            groupsProperty.DeleteArrayElementAtIndex(i);
                            return;
                        }
                    }

                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("renderers"),
                        new GUIContent(T("렌더러", "Renderers", "レンダラー")),
                        true);

                    var group = i < _designer.Groups.Count ? _designer.Groups[i] : null;
                    if (group == null) continue;

                    EditorGUILayout.LabelField(
                        T("따로 조절할 항목", "Separately Controlled Items", "個別に調整する項目"),
                        EditorStyles.miniBoldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var setting in enabledControls)
                        {
                            var def = DiNeLightingControlDef.Get(setting.control);
                            if (def == null) continue;

                            bool separated = group.Separates(setting.control);
                            bool now = EditorGUILayout.ToggleLeft(
                                new GUIContent(
                                    $"{GetControlDisplayName(setting)}  (+{def.CostBits} bits)",
                                    DiNeLightingLocalization.ControlTooltip(setting.control)),
                                separated);
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
    }

    private void DrawStatus()
    {
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("상태", "Status", "状態"), EditorStyles.boldLabel);

            var descriptor = _designer.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "이 오브젝트 위에서 VRCAvatarDescriptor를 찾을 수 없습니다.",
                        "No VRCAvatarDescriptor was found above this object.",
                        "このオブジェクトの親にVRCAvatarDescriptorが見つかりません。"),
                    MessageType.Error);
            }
            else
            {
                int targetCount = DiNeLightingDiagnostics
                    .CollectTargetRenderers(descriptor, _designer)
                    .Count;
                EditorGUILayout.LabelField(
                    descriptor.name,
                    targetCount > 0
                        ? T($"대상 렌더러 {targetCount}개", $"{targetCount} target renderer(s)", $"対象レンダラー {targetCount}個")
                        : T("대상 렌더러 없음", "No target renderers", "対象レンダラーなし"));
                EditorGUILayout.LabelField(
                    T(
                        "플레이 모드와 업로드 시 비파괴적으로 자동 적용됩니다.",
                        "Applied automatically and non-destructively in Play Mode and on upload.",
                        "プレイモードとアップロード時に非破壊で自動適用されます。"),
                    EditorStyles.wordWrappedMiniLabel);
            }

            DrawBudget(descriptor);
            DrawDiagnostics();
        }
    }

    private void DrawBudget(VRCAvatarDescriptor descriptor)
    {
        int cost = _designer.CalculateParameterCost();
        int used = DiNeLightingDiagnostics.CalculateOtherCost(descriptor);
        int total = used + cost;
        int max = VRCExpressionParameters.MAX_PARAMETER_COST;

        var rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
        float fill = max > 0 ? Mathf.Clamp01(total / (float)max) : 0f;
        EditorGUI.ProgressBar(
            rect,
            fill,
            T(
                $"동기화 파라미터  {total} / {max} bits  (라이팅 {cost})",
                $"Synced Parameters  {total} / {max} bits  (Lighting {cost})",
                $"同期パラメーター  {total} / {max} bits  (ライティング {cost})"));

        if (_designer.Presets.Any(preset => preset != null))
        {
            EditorGUILayout.LabelField(
                T(
                    "VRChat 메뉴 프리셋은 비동기화 파라미터라 0 bit입니다.",
                    "VRChat menu presets use unsynced parameters and cost 0 bits.",
                    "VRChatメニュープリセットは非同期パラメーターのため0 bitです。"),
                EditorStyles.miniLabel);
        }
    }

    private void DrawDiagnostics()
    {
        var issues = DiNeLightingDiagnostics.Collect(_designer);
        if (issues.Count == 0)
        {
            EditorGUILayout.LabelField(
                T("확인된 문제 없음", "No issues found", "問題は見つかりませんでした"),
                EditorStyles.miniLabel);
            return;
        }

        int errors = issues.Count(issue => issue.Severity == MessageType.Error);
        int warnings = issues.Count(issue => issue.Severity == MessageType.Warning);
        string summary = errors > 0
            ? T($"문제 {errors}건 · 경고 {warnings}건", $"{errors} issue(s) · {warnings} warning(s)", $"問題 {errors}件・警告 {warnings}件")
            : T($"경고 {warnings}건", $"{warnings} warning(s)", $"警告 {warnings}件");

        _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, summary, true);
        if (!_showDiagnostics) return;

        foreach (var issue in issues.OrderBy(issue => issue.Severity == MessageType.Error ? 0 : 1))
            EditorGUILayout.HelpBox(issue.Message, issue.Severity);
    }

    private SerializedProperty FindControlProperty(DiNeLightingControl control)
    {
        var controls = serializedObject.FindProperty("controls");
        for (int i = 0; i < controls.arraySize; i++)
        {
            var element = controls.GetArrayElementAtIndex(i);
            if ((DiNeLightingControl)element.FindPropertyRelative("control").intValue == control)
                return element;
        }
        return null;
    }

    private bool IsControlEnabled(DiNeLightingControl control)
    {
        var element = FindControlProperty(control);
        return element != null && element.FindPropertyRelative("enabled").boolValue;
    }

    private static string GetControlDisplayName(DiNeLightingDesigner.ControlSetting setting)
    {
        if (setting != null && !string.IsNullOrWhiteSpace(setting.displayNameOverride))
            return setting.displayNameOverride.Trim();
        return setting == null
            ? string.Empty
            : DiNeLightingLocalization.ControlName(setting.control);
    }

    private void ApplySelectedPreset()
    {
        if (_settingsPreset == null) return;

        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(_designer, "Apply Lighting Designer Preset");
        _settingsPreset.ApplyTo(_designer);
        EditorUtility.SetDirty(_designer);
        serializedObject.Update();
    }

    private void OverwriteSelectedPreset()
    {
        if (_settingsPreset == null) return;

        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(_settingsPreset, "Save Lighting Designer Preset");
        _settingsPreset.Capture(_designer);
        EditorUtility.SetDirty(_settingsPreset);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(_settingsPreset);
        serializedObject.Update();
    }

    private void SaveNewPreset()
    {
        serializedObject.ApplyModifiedProperties();

        string path = EditorUtility.SaveFilePanelInProject(
            T(
                "라이팅 디자이너 설정 프리셋 저장",
                "Save Lighting Designer Settings Preset",
                "Lighting Designer設定プリセットを保存"),
            _designer.gameObject.name + " Lighting Preset",
            "asset",
            T(
                "다른 아바타에도 재사용할 라이팅 설정 프리셋을 저장합니다.",
                "Save a lighting settings preset that can be reused on other avatars.",
                "別のアバターでも再利用できるライティング設定プリセットを保存します。"),
            "Assets");
        if (string.IsNullOrEmpty(path))
        {
            serializedObject.Update();
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            path = AssetDatabase.GenerateUniqueAssetPath(path);

        var preset = CreateInstance<DiNeLightingDesignerPreset>();
        preset.Capture(_designer);
        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        _settingsPreset = preset;
        EditorGUIUtility.PingObject(preset);
        serializedObject.Update();
        GUIUtility.ExitGUI();
    }

    private static bool MintButton(string label, float height)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = MintActive;
        bool clicked = GUILayout.Button(label, GUILayout.Height(height));
        GUI.backgroundColor = previous;
        return clicked;
    }

}

[CustomEditor(typeof(DiNeLightingDesignerPreset))]
public sealed class DiNeLightingDesignerPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 42f);
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.36f, 0.33f));
        var title = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            normal = { textColor = Color.white }
        };
        GUI.Label(rect, "LIGHTING PRESET", title);

        EditorGUILayout.Space(8f);
        var preset = (DiNeLightingDesignerPreset)target;
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(
                DiNeLightingLocalization.T("저장된 설정", "Saved Settings", "保存済み設定"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                DiNeLightingLocalization.T("프리셋 버전", "Preset Version", "プリセットバージョン"),
                preset.FormatVersion.ToString());
            EditorGUILayout.LabelField(
                DiNeLightingLocalization.T("활성 제어 항목", "Enabled Controls", "有効な制御項目"),
                DiNeLightingLocalization.T(
                    preset.EnabledControlCount + "개",
                    preset.EnabledControlCount.ToString(),
                    preset.EnabledControlCount + "個"));
            EditorGUILayout.LabelField(
                DiNeLightingLocalization.T("VRChat 메뉴 프리셋", "VRChat Menu Presets", "VRChatメニュープリセット"),
                DiNeLightingLocalization.T(
                    preset.MenuPresetCount + "개",
                    preset.MenuPresetCount.ToString(),
                    preset.MenuPresetCount + "個"));
        }
        EditorGUILayout.HelpBox(
            DiNeLightingLocalization.T(
                "라이팅 디자이너 컴포넌트의 '설정 프리셋' 칸에서 불러오거나 현재 설정으로 덮어쓸 수 있습니다.",
                "Load this from the Lighting Designer component's Settings Preset field, or overwrite it with the current settings.",
                "Lighting Designerコンポーネントの「設定プリセット」欄から読み込むか、現在の設定で上書きできます。"),
            MessageType.Info);
    }
}
#endif
