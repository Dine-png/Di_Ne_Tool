#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public sealed partial class DiNeLightingDesignerEditor
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[][] tutorialCourses;
    private int tutorialCourse;
    private static readonly string[] TutorialCourseIds = { "brightness", "additional", "advanced", "targets-groups", "menu-presets", "settings-presets" };
    private static readonly string[] TutorialCourseEn = { "Brightness", "Additional Controls", "Advanced Controls", "Targets and Groups", "Menu Presets", "Settings Presets" };
    private static readonly string[] TutorialCourseKo = { "밝기", "추가 제어", "고급 제어", "대상과 그룹", "메뉴 프리셋", "설정 프리셋" };
    private static readonly string[] TutorialCourseJa = { "明るさ", "追加制御", "詳細制御", "対象とグループ", "メニュープリセット", "設定プリセット" };

    private void EnsureTutorial()
    {
        if (tutorial == null)
        {
            tutorial = new DiNeGuidedTutorial(this, "lighting-designer");
            tutorialCourse = Mathf.Clamp(SessionState.GetInt("DiNe.LightingDesigner.TutorialCourse." + target.GetInstanceID(), 0), 0, 5);
        }
        if (tutorialCourses != null) return;
        tutorialCourses = new DiNeTutorialStep[6][];
        tutorialCourses[0] = new[]
        {
            DiNeTutorialStep.Required("avatar", "Place this component inside your avatar.", "이 컴포넌트를 아바타 안에 배치하세요.", "このコンポーネントをアバター内に配置してください。", HasTutorialAvatar),
            DiNeTutorialStep.Required("targets", "Use a supported material on at least one avatar renderer.", "아바타 렌더러 하나 이상에 지원 머티리얼을 사용하세요.", "アバターのレンダラー1つ以上で対応マテリアルを使ってください。", HasTutorialTargets),
            DiNeTutorialStep.Required(ControlTutorialId(DiNeLightingControl.LightMin, "enabled"), "Enable Lighting Brightness.", "조명 밝기를 켜세요.", "ライティング明るさをONにしてください。", () => _designer != null && _designer.IsEnabled(DiNeLightingControl.LightMin), onEnter: () => _settingsMode = 0),
            DiNeTutorialStep.Optional("max-light", "Set Maximum Brightness after enabling Lighting Brightness.", "조명 밝기를 켠 뒤 최대 밝기를 설정하세요.", "ライティング明るさをONにして最大明るさを設定してください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("min-light", "Set Minimum Brightness after enabling Lighting Brightness.", "조명 밝기를 켠 뒤 최소 밝기를 설정하세요.", "ライティング明るさをONにして最小明るさを設定してください。", () => _settingsMode = 0),
            InitialStep(DiNeLightingControl.LightMin, 0), SavedStep(DiNeLightingControl.LightMin, 0),
            DiNeTutorialStep.Optional("master", "Enable the ON/OFF toggle if you want one.", "켜기/끄기 토글이 필요하면 켜세요.", "ON/OFFトグルが必要な場合はONにしてください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("master-default", "Enable the ON/OFF toggle, then choose its initial state.", "켜기/끄기 토글을 켠 뒤 기본 켜짐을 선택하세요.", "ON/OFFトグルを有効にして初期状態を選んでください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("master-saved", "Enable the ON/OFF toggle, then choose whether to save its value.", "켜기/끄기 토글을 켠 뒤 값을 저장할지 선택하세요.", "ON/OFFトグルを有効にして値を保存するか選んでください。", () => _settingsMode = 0),
            DiNeTutorialStep.Optional("automatic", "Upload or enter Play Mode when you are ready.", "준비가 되면 업로드하거나 Play Mode에 들어가세요.", "準備ができたらアップロードするかPlay Modeに入ってください。")
        };
        tutorialCourses[1] = ControlCourse(SimpleAdditionalControls, 0);
        var advanced = new List<DiNeTutorialStep>(ControlCourse(AdvancedControls, 1));
        advanced.Add(DiNeTutorialStep.Optional("light-direction", "Enable Light Direction, then choose its direction.", "라이트 방향을 켠 뒤 방향을 지정하세요.", "ライト方向をONにして方向を指定してください。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("outline-from", "Enable Outline Tint, then choose the color at 0.", "외곽선 색을 켠 뒤 0일 때 색을 선택하세요.", "輪郭色をONにして0のときの色を選んでください。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("outline-to", "Enable Outline Tint, then choose the color at 1.", "외곽선 색을 켠 뒤 1일 때 색을 선택하세요.", "輪郭色をONにして1のときの色を選んでください。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("outline-width", "Enable Outline Width, then set its maximum.", "외곽선 두께를 켠 뒤 최대 두께를 설정하세요.", "輪郭幅をONにして最大幅を設定してください。", () => _settingsMode = 1));
        advanced.Add(DiNeTutorialStep.Optional("reflectance-max", "Enable Reflectance, then set its maximum.", "반사율을 켠 뒤 최대 반사율을 설정하세요.", "反射率をONにして最大反射率を設定してください。", () => _settingsMode = 1));
        tutorialCourses[2] = advanced.ToArray();
        tutorialCourses[3] = new[]
        {
            DiNeTutorialStep.Optional("shaders", "Choose which shaders to include.", "적용할 셰이더를 선택하세요.", "適用するシェーダーを選んでください。", () => _settingsMode = 1),
            DiNeTutorialStep.Optional("excludes", "Open Excluded Renderers and add objects to skip.", "제외할 렌더러를 열고 제외 대상을 넣으세요.", "除外するレンダラーを開いて除外対象を追加してください。", () => { _settingsMode = 1; _showExcludes = true; }),
            DiNeTutorialStep.Optional("group-add", "Press Add to create a renderer group.", "렌더러 그룹을 만들려면 추가를 누르세요.", "レンダラーグループを作成する場合は追加を押してください。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-name", "Add or open a group, then enter its name.", "그룹을 추가하거나 연 뒤 이름을 입력하세요.", "グループを追加または開いて名前を入力してください。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-icon", "Add or open a group, then choose its icon.", "그룹을 추가하거나 연 뒤 아이콘을 선택하세요.", "グループを追加または開いてアイコンを選んでください。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-renderers", "Add or open a group, then assign its renderers.", "그룹을 추가하거나 연 뒤 렌더러를 지정하세요.", "グループを追加または開いてレンダラーを指定してください。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-separate", "Enable a control, then select it for the group's separate slider.", "제어 항목을 켠 뒤 그룹에서 따로 조절할 항목을 선택하세요.", "制御項目をONにしてグループで個別に調整する項目を選んでください。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("group-delete", "Press Delete on a group you no longer need.", "필요 없는 그룹은 삭제를 누르세요.", "不要なグループは削除を押してください。", OpenTutorialGroups),
            DiNeTutorialStep.Optional("budget", "Check the parameter budget before adding more controls.", "제어 항목을 더 켜기 전에 파라미터 용량을 확인하세요.", "制御項目を追加する前にパラメーター容量を確認してください。", () => _settingsMode = 1),
            DiNeTutorialStep.Optional("diagnostics", "Check the diagnostic messages.", "진단 메시지를 확인하세요.", "診断メッセージを確認してください。", () => _showDiagnostics = true)
        };
        tutorialCourses[4] = new[]
        {
            DiNeTutorialStep.Optional("menu-add", "Press Add to create a menu preset.", "메뉴 프리셋을 만들려면 추가를 누르세요.", "メニュープリセットを作成する場合は追加を押してください。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-name", "Add or open a preset, then enter its menu name.", "프리셋을 추가하거나 연 뒤 메뉴 이름을 입력하세요.", "プリセットを追加または開いてメニュー名を入力してください。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-icon", "Add or open a preset, then choose its icon.", "프리셋을 추가하거나 연 뒤 아이콘을 선택하세요.", "プリセットを追加または開いてアイコンを選んでください。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-include", "Enable a control, then check it in the preset.", "제어 항목을 켠 뒤 프리셋에 포함할 항목을 체크하세요.", "制御項目をONにしてプリセットに含める項目をチェックしてください。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-value", "Include a control, then choose its preset value.", "항목을 프리셋에 포함한 뒤 값을 지정하세요.", "項目をプリセットに含めて値を指定してください。", OpenTutorialMenuPresets),
            DiNeTutorialStep.Optional("menu-delete", "Press Delete on a preset you no longer need.", "필요 없는 프리셋은 삭제를 누르세요.", "不要なプリセットは削除を押してください。", OpenTutorialMenuPresets)
        };
        tutorialCourses[5] = new[]
        {
            DiNeTutorialStep.Optional("settings-select", "Choose a settings preset from the list.", "목록에서 설정 프리셋을 선택하세요.", "一覧から設定プリセットを選んでください。"),
            DiNeTutorialStep.Optional("settings-load", "Select a preset, then press Load.", "프리셋을 선택한 뒤 불러오기를 누르세요.", "プリセットを選んで読み込みを押してください。"),
            DiNeTutorialStep.Optional("settings-save", "Press Save as New Preset to save these settings.", "이 설정을 저장하려면 새 프리셋으로 저장을 누르세요.", "この設定を保存する場合は新規プリセットとして保存を押してください。"),
            DiNeTutorialStep.Optional("settings-overwrite", "Select a preset, then press Overwrite with Current to replace it.", "프리셋을 선택한 뒤 덮어쓰기를 눌러 현재 설정으로 바꾸세요.", "プリセットを選んで現在の設定で上書きを押してください。")
        };
    }

    private void DrawTutorialControls()
    {
        string[] names = CurrentLanguage == DiNeLightingLanguage.Korean ? TutorialCourseKo
            : CurrentLanguage == DiNeLightingLanguage.Japanese ? TutorialCourseJa : TutorialCourseEn;
        int selected = EditorGUILayout.Popup(T("튜토리얼", "Tutorial", "チュートリアル"), tutorialCourse, names);
        if (selected != tutorialCourse)
        {
            tutorialCourse = selected;
            SessionState.SetInt("DiNe.LightingDesigner.TutorialCourse." + target.GetInstanceID(), selected);
        }
        tutorial.Configure(TutorialCourseIds[tutorialCourse], TutorialCourseEn[tutorialCourse], TutorialCourseKo[tutorialCourse], TutorialCourseJa[tutorialCourse], tutorialCourses[tutorialCourse]);
        tutorial.DrawControls();
    }

    private DiNeTutorialStep[] ControlCourse(DiNeLightingControl[] controls, int mode)
    {
        var steps = new List<DiNeTutorialStep>();
        foreach (var control in controls)
        {
            var names = TutorialControlNames(control);
            steps.Add(DiNeTutorialStep.Optional(ControlTutorialId(control, "enabled"), "Enable " + names[0] + " if you need it.", names[1] + " 항목이 필요하면 켜세요.", names[2] + "が必要な場合はONにしてください。", () => _settingsMode = mode));
            steps.Add(InitialStep(control, mode));
            steps.Add(SavedStep(control, mode));
        }
        return steps.ToArray();
    }

    private DiNeTutorialStep InitialStep(DiNeLightingControl control, int mode) => DiNeTutorialStep.Optional(ControlTutorialId(control, "initial"), "Enable this control, then choose its initial value.", "이 항목을 켠 뒤 초기값을 선택하세요.", "この項目をONにして初期値を選んでください。", () => _settingsMode = mode);
    private DiNeTutorialStep SavedStep(DiNeLightingControl control, int mode) => DiNeTutorialStep.Optional(ControlTutorialId(control, "saved"), "Enable this control, then choose whether to save its value.", "이 항목을 켠 뒤 값을 저장할지 선택하세요.", "この項目をONにして値を保存するか選んでください。", () => _settingsMode = mode);
    private static string ControlTutorialId(DiNeLightingControl control, string field) => "control-" + control + "-" + field;
    private void OpenTutorialGroups() { _settingsMode = 1; _showGroups = true; }
    private void OpenTutorialMenuPresets() { _settingsMode = 1; _showMenuPresets = true; }
    private bool HasTutorialAvatar() => _designer != null && !EditorUtility.IsPersistent(_designer)
        && _designer.gameObject.scene.IsValid() && _designer.GetComponentInParent<VRCAvatarDescriptor>(true) != null;
    private bool HasTutorialTargets() => HasTutorialAvatar() && DiNeLightingDiagnostics.CollectTargetRenderers(_designer.GetComponentInParent<VRCAvatarDescriptor>(true), _designer).Count > 0;

    private void TutorialProperty(SerializedProperty property, GUIContent label, string id, bool includeChildren = false)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, label, includeChildren);
        if (EditorGUI.EndChangeCheck()) tutorial.NotifyAction(id);
        tutorial.Draw(id, GUILayoutUtility.GetLastRect());
    }

    private void TutorialFallback(Rect rect, params string[] ids)
    {
        foreach (string id in ids) tutorial.Draw(id, rect);
    }

    private void TutorialControlFallback(DiNeLightingControl control, Rect rect)
    {
        TutorialFallback(rect, ControlTutorialId(control, "initial"), ControlTutorialId(control, "saved"));
        if (control == DiNeLightingControl.LightMin) TutorialFallback(rect, "max-light", "min-light");
        if (control == DiNeLightingControl.OutlineTint) TutorialFallback(rect, "outline-from", "outline-to");
        if (control == DiNeLightingControl.OutlineWidth) tutorial.Draw("outline-width", rect);
        if (control == DiNeLightingControl.Reflectance) tutorial.Draw("reflectance-max", rect);
        if (control == DiNeLightingControl.LightDirection) tutorial.Draw("light-direction", rect);
    }

    private static string[] TutorialControlNames(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.ColorTemperature: return new[] { "Color Temperature", "색온도", "色温度" };
            case DiNeLightingControl.Saturation: return new[] { "Saturation", "채도", "彩度" };
            case DiNeLightingControl.Monochrome: return new[] { "Monochrome", "흑백", "モノクロ" };
            case DiNeLightingControl.Hue: return new[] { "Hue", "색상", "色相" };
            case DiNeLightingControl.Brightness: return new[] { "Brightness", "밝기", "明るさ" };
            case DiNeLightingControl.Gamma: return new[] { "Gamma", "감마", "ガンマ" };
            case DiNeLightingControl.Emission: return new[] { "Emission", "발광", "発光" };
            case DiNeLightingControl.LightDirection: return new[] { "Light Direction", "라이트 방향", "ライト方向" };
            case DiNeLightingControl.ShadowStrength: return new[] { "Shadow Strength", "그림자 강도", "影の強さ" };
            case DiNeLightingControl.OutlineTint: return new[] { "Outline Tint", "외곽선 색", "輪郭色" };
            case DiNeLightingControl.OutlineWidth: return new[] { "Outline Width", "외곽선 두께", "輪郭幅" };
            case DiNeLightingControl.Reflectance: return new[] { "Reflectance", "반사율", "反射率" };
            default: return new[] { "Lighting Brightness", "조명 밝기", "ライティング明るさ" };
        }
    }
}

public sealed partial class DiNeLightingDesignerPresetEditor
{
    private DiNeGuidedTutorial presetTutorial;
    private DiNeTutorialStep[] presetTutorialSteps;
    private Texture2D presetBrandIcon;
    private GUIStyle presetTitleStyle, presetDescriptionStyle, presetSelectedStyle, presetNormalStyle;

    private void EnsurePresetTutorial()
    {
        if (presetTutorial == null) presetTutorial = new DiNeGuidedTutorial(this, "lighting-preset");
        if (presetTutorialSteps == null) presetTutorialSteps = new[]
        {
            DiNeTutorialStep.Optional("version", "Check the saved preset version.", "저장된 프리셋 버전을 확인하세요.", "保存済みプリセットのバージョンを確認してください。"),
            DiNeTutorialStep.Optional("controls", "Check how many controls this preset enables.", "이 프리셋에서 켜는 제어 항목 수를 확인하세요.", "このプリセットで有効になる制御項目数を確認してください。"),
            DiNeTutorialStep.Optional("menu-presets", "Check how many menu presets are included.", "포함된 메뉴 프리셋 수를 확인하세요.", "含まれるメニュープリセット数を確認してください。"),
            DiNeTutorialStep.Optional("load", "Choose this preset in Lighting Designer, then press Load.", "Lighting Designer에서 이 프리셋을 선택한 뒤 불러오기를 누르세요.", "Lighting Designerでこのプリセットを選び、読み込みを押してください。")
        };
        presetTutorial.Configure("inspect", "Lighting Preset", "라이팅 프리셋", "Lighting Preset", presetTutorialSteps);
    }

    private void DrawPresetHeader()
    {
        if (presetTitleStyle == null)
        {
            presetBrandIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            presetTitleStyle = new GUIStyle(EditorStyles.label)
            { font = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf"), fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            presetDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.8f, .8f, .8f) } };
            presetSelectedStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            presetNormalStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(.8f, .8f, .8f) } };
        }
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = new Color(.9f, .9f, .9f);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(presetBrandIcon, GUILayout.Width(72f), GUILayout.Height(72f));
                GUILayout.Space(6f);
                GUILayout.Label("Lighting Preset", presetTitleStyle, GUILayout.Height(72f));
                GUILayout.FlexibleSpace();
            }
            GUILayout.Label(DiNeLightingLocalization.T("저장된 라이팅 설정을 확인합니다.", "Inspect saved lighting settings.", "保存済みライティング設定を確認します。"), presetDescriptionStyle);
        }
        GUI.backgroundColor = previous;
        GUILayout.Space(5f);
        int selected = (int)DiNeLightingLocalization.CurrentLanguage;
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < DiNeLightingLocalization.LanguageButtonLabels.Length; i++)
            {
                GUI.backgroundColor = selected == i ? new Color(.30f, .82f, .76f) : new Color(.5f, .5f, .5f);
                if (GUILayout.Button(DiNeLightingLocalization.LanguageButtonLabels[i], selected == i ? presetSelectedStyle : presetNormalStyle, GUILayout.Height(35f)))
                    DiNeLightingLocalization.CurrentLanguage = (DiNeLightingLanguage)i;
                GUI.backgroundColor = previous;
            }
        }
        GUILayout.Space(15f);
    }
}
#endif
