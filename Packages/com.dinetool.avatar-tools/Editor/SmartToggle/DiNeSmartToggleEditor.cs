#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[CustomEditor(typeof(DiNeSmartToggle))]
public sealed partial class DiNeSmartToggleEditor : Editor
{
    private static readonly Color Mint = new Color(0.30f, 0.82f, 0.76f);
    private static readonly string[] Languages = { "English", "한국어", "日本語" };
    private SerializedProperty displayName, parameterName, defaultOn, saved, menuPlacement, groupName, icon;
    private DiNeToggleMenuChoices menuChoices;
    private Texture2D brandIcon;
    private Font titleFont;
    private GUIStyle titleStyle, descriptionStyle, selectedStyle, normalStyle;
    private int Language => Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
    private string T(string en, string ko, string ja) => Language == 1 ? ko : Language == 2 ? ja : en;

    public static DiNeSmartToggle CreateToggle(GameObject go, DiNeSmartToggle.MenuPlacement placement,
        string category, string parameter, DiNeMultiDresser dresser = null)
    {
        if (go == null || go.GetComponent<DiNeSmartToggle>() != null) return null;
        string resolvedParameter = MakeUniqueParameterName(go, parameter, null, dresser);
        var component = Undo.AddComponent<DiNeSmartToggle>(go);
        Undo.RecordObject(component, "Configure Smart Toggle");
        component.DisplayName = go.name;
        component.ParameterName = resolvedParameter;
        component.DefaultOn = go.activeSelf;
        component.Placement = placement;
        component.Dresser = null;
        if (!string.IsNullOrWhiteSpace(category)) component.GroupName = category;
        component.EnsureDefaults();
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(component);
        EditorApplication.delayCall += () =>
        {
            if (component == null || component.Icon != null) return;
            DiNeSmartToggleGenerator.EnsureIcon(component);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        };
        return component;
    }

    public static string MakeUniqueParameterName(GameObject go, string requested = null,
        DiNeSmartToggle owner = null, DiNeMultiDresser dresser = null, DiNeMultiDresser.IndependentToggle groupOwner = null)
    {
        string baseName = string.IsNullOrWhiteSpace(requested)
            ? DiNeSmartToggle.BuildDefaultParameterName(go != null ? go.name : "Toggle") : requested.Trim();
        var avatar = go != null ? go.GetComponentInParent<VRCAvatarDescriptor>(true) : null;
        if (avatar == null && dresser != null) avatar = dresser.GetAvatarDescriptor();
        if (avatar == null) return baseName;
        var used = avatar.GetComponentsInChildren<DiNeSmartToggle>(true)
            .Where(item => item != null && item != owner && !string.IsNullOrWhiteSpace(item.ParameterName))
            .Select(item => item.ParameterName).ToHashSet();
        if (avatar.expressionParameters?.parameters != null)
            foreach (var parameter in avatar.expressionParameters.parameters)
                if (parameter != null) used.Add(parameter.name);
        var fx = DiNeMultiDresser.GetAvatarFxController(avatar) as UnityEditor.Animations.AnimatorController;
        if (fx != null) foreach (var parameter in fx.parameters) used.Add(parameter.name);
        foreach (var avatarDresser in avatar.GetComponentsInChildren<DiNeMultiDresser>(true))
        {
            if (avatarDresser.animatorController != null)
                foreach (var parameter in avatarDresser.animatorController.parameters) used.Add(parameter.name);
            foreach (var group in avatarDresser.independentToggles)
                if (group != null && group != groupOwner && !string.IsNullOrWhiteSpace(group.parameterName)) used.Add(group.parameterName);
            foreach (var layer in avatarDresser.layers)
                if (layer != null) used.Add("DiNe/MultiDresser/" + (string.IsNullOrEmpty(layer.layerName) ? "Layer" : layer.layerName));
        }
        string candidate = baseName;
        for (int suffix = 2; used.Contains(candidate); suffix++) candidate = baseName + "_" + suffix;
        return candidate;
    }

    private void OnEnable()
    {
        menuChoices = new DiNeToggleMenuChoices();
        displayName = serializedObject.FindProperty("displayName");
        parameterName = serializedObject.FindProperty("parameterName");
        defaultOn = serializedObject.FindProperty("defaultOn");
        saved = serializedObject.FindProperty("saved");
        menuPlacement = serializedObject.FindProperty("menuPlacement");
        groupName = serializedObject.FindProperty("groupName");
        icon = serializedObject.FindProperty("icon");
        brandIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
    }

    private void OnDisable()
    {
        tutorial?.Suspend();
        DiNeTogglePreview.ClearForOwner(this);
        menuChoices?.Dispose();
    }

    public override void OnInspectorGUI()
    {
        EnsureTutorial();
        tutorial.BeginFrame();
        try { DrawInspectorContent(); }
        finally { tutorial.EndFrame(); }
    }

    private void DrawInspectorContent()
    {
        serializedObject.Update();
        var toggle = (DiNeSmartToggle)target;
        EnsureStyles();
        DrawSmartToggleHeader();
        GUILayout.Space(5);
        int language = Language;
        int nextLanguage = DrawSegments(language, Languages, 35);
        if (nextLanguage != language) EditorPrefs.SetInt("DiNeLang", nextLanguage);
        GUILayout.Space(15);
        tutorial.DrawControls();

        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(T("Toggle Settings", "토글 설정", "トグル設定"), EditorStyles.boldLabel);
                bool previewing = DiNeTogglePreview.IsActive(this, 0);
                Color previous = GUI.backgroundColor;
                if (previewing) GUI.backgroundColor = Mint;
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(toggle)))
                if (GUILayout.Button(previewing ? T("End Preview", "미리보기 종료", "プレビュー終了")
                    : T("Preview", "미리보기", "プレビュー"), GUILayout.Height(24)))
                {
                    if (previewing) DiNeTogglePreview.Clear();
                    else
                    {
                        serializedObject.ApplyModifiedProperties();
                        DiNeTogglePreview.Begin(this, 0, new[] { toggle.gameObject }, toggle.DefaultOn);
                    }
                    tutorial.NotifyAction(previewing ? "preview-stop" : "preview-start");
                }
                tutorial.Anchor("preview-start", GUILayoutUtility.GetLastRect());
                tutorial.Anchor("preview-stop", GUILayoutUtility.GetLastRect());
                tutorial.Anchor("preview-state", GUILayoutUtility.GetLastRect());
                GUI.backgroundColor = previous;
            }
            tutorial.Draw("preview-start");
            tutorial.Draw("preview-stop");
            DiNeTogglePreview.DrawStateControls(this, 0);
            if (DiNeTogglePreview.IsActive(this, 0)) tutorial.Draw("preview-state", GUILayoutUtility.GetLastRect());
            else tutorial.Draw("preview-state");
            EditorGUILayout.HelpBox(T("This object gets one On/Off button using a Bool parameter.",
                "이 오브젝트를 켜고 끄는 Bool 파라미터와 단일 토글 버튼을 만듭니다.",
                "このオブジェクトを切り替えるBoolパラメーターと単一のトグルボタンを作成します。"), MessageType.None);
            TutorialProperty(displayName, new GUIContent(T("Menu Name", "메뉴 이름", "メニュー名")), "name");
            if (DrawParameterName(parameterName, toggle, null)) tutorial.NotifyAction("parameter");
            tutorial.Draw("parameter", GUILayoutUtility.GetLastRect());
            TutorialProperty(defaultOn, new GUIContent(T("Default On", "기본 ON", "初期ON")), "default");
            TutorialProperty(saved, new GUIContent(T("Save Value", "값 저장", "値を保存")), "saved");
        }
        GUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("Menu Placement", "메뉴 위치", "メニュー配置"), EditorStyles.boldLabel);
            if (menuChoices.Draw(toggle.GetComponentInParent<VRCAvatarDescriptor>(true),
                serializedObject.FindProperty("menuPath"), serializedObject.FindProperty("generatedMenuDestination"),
                (DiNeSmartToggle.MenuPlacement)menuPlacement.enumValueIndex, groupName.stringValue))
                menuPlacement.enumValueIndex = (int)DiNeSmartToggle.MenuPlacement.ExistingMenu;
            tutorial.Draw("menu", GUILayoutUtility.GetLastRect());
        }
        GUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope("GroupBox"))
        {
            GUILayout.Label(T("Menu Icon", "메뉴 아이콘", "メニューアイコン"), EditorStyles.boldLabel);
            TutorialProperty(icon, new GUIContent(T("Icon", "아이콘", "アイコン")), "icon");
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Mint;
            if (GUILayout.Button(T("Edit Icon", "아이콘 편집", "アイコン編集"), GUILayout.Height(30)))
            {
                serializedObject.ApplyModifiedProperties();
                DiNeScreenSaver.DiNeScreenSaver.OpenIconEditor(toggle);
                tutorial.NotifyAction("icon-edit");
            }
            tutorial.Draw("icon-edit", GUILayoutUtility.GetLastRect());
            GUI.backgroundColor = previous;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(T("Generate / Reuse", "자동 생성 / 재사용", "自動生成 / 再利用"), GUILayout.Height(30)))
                {
                    serializedObject.ApplyModifiedProperties();
                    DiNeSmartToggleGenerator.EnsureIcon(toggle);
                    serializedObject.Update();
                    tutorial.NotifyAction("icon-generate");
                }
                tutorial.Anchor("icon-generate", GUILayoutUtility.GetLastRect());
                if (GUILayout.Button(T("Regenerate", "재생성", "再生成"), GUILayout.Height(30)))
                {
                    serializedObject.ApplyModifiedProperties();
                    DiNeSmartToggleGenerator.RegenerateIcon(toggle);
                    serializedObject.Update();
                    tutorial.NotifyAction("icon-regenerate");
                }
                tutorial.Anchor("icon-regenerate", GUILayoutUtility.GetLastRect());
            }
            tutorial.Draw("icon-generate");
            tutorial.Draw("icon-regenerate");
        }
        if (serializedObject.ApplyModifiedProperties())
        {
            menuChoices.Invalidate();
            Undo.RecordObject(toggle, "Normalize Smart Toggle Settings");
            toggle.EnsureDefaults();
            PrefabUtility.RecordPrefabInstancePropertyModifications(toggle);
            EditorUtility.SetDirty(toggle);
        }
        GUILayout.Space(8);
        var avatar = toggle.GetComponentInParent<VRCAvatarDescriptor>(true);
        EditorGUILayout.HelpBox(avatar == null
            ? T("Place this object under a VRCAvatarDescriptor.", "VRCAvatarDescriptor가 있는 아바타 안에 배치하세요.", "VRCAvatarDescriptorのあるアバター内に配置してください。")
            : T("Applied automatically on upload and in Play Mode. Preview does not change the Default On setting.",
                "별도 적용 없이 업로드와 Play Mode에서 자동 적용됩니다. 미리보기는 기본 ON 설정을 바꾸지 않습니다.",
                "アップロードとPlay Modeで自動適用されます。プレビューは初期ON設定を変更しません。"),
            avatar == null ? MessageType.Warning : MessageType.Info);
        Rect statusRect = GUILayoutUtility.GetLastRect();
        tutorial.Draw("avatar", statusRect);
        tutorial.Draw("automatic", statusRect);
    }

    public static bool DrawParameterName(SerializedProperty parameter, DiNeSmartToggle toggle, DiNeMultiDresser dresser)
    {
        int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
        string label = language == 1 ? "Bool 파라미터" : language == 2 ? "Boolパラメーター" : "Bool Parameter";
        string tooltip = language == 1 ? "이름을 자동으로 채우고, 중복이면 숫자를 붙입니다. 직접 입력한 이름도 중복 검사합니다."
            : language == 2 ? "名前は自動入力され、重複時は番号を付けます。入力した名前も重複を確認します。"
            : "Names are prefilled automatically. Conflicting names receive a numeric suffix, including manually entered names.";
        EditorGUI.BeginChangeCheck();
        string requested = EditorGUILayout.DelayedTextField(new GUIContent(label, tooltip), parameter.stringValue);
        if (!EditorGUI.EndChangeCheck()) return false;
        parameter.stringValue = MakeUniqueParameterName(toggle.gameObject, requested, toggle, dresser);
        return true;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(EditorStyles.label) { font = titleFont, fontSize = 36,
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12,
            alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
        selectedStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        normalStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
    }

    private void DrawSmartToggleHeader()
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(brandIcon, GUILayout.Width(72), GUILayout.Height(72));
                GUILayout.Space(6);
                GUILayout.Label("Smart Toggle", titleStyle, GUILayout.Height(72));
                GUILayout.FlexibleSpace();
            }
            GUILayout.Label(T("Create an On/Off button for the selected object.", "선택한 오브젝트의 On/Off 버튼을 간단하게 만듭니다.",
                "選択したオブジェクトのOn/Offボタンを簡単に作成します。"), descriptionStyle);
        }
        GUI.backgroundColor = previous;
    }

    private int DrawSegments(int selected, string[] labels, float height)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < labels.Length; i++)
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = selected == i ? Mint : new Color(0.5f, 0.5f, 0.5f);
                if (GUILayout.Button(labels[i], selected == i ? selectedStyle : normalStyle, GUILayout.Height(height))) selected = i;
                GUI.backgroundColor = previous;
            }
        }
        return selected;
    }
}
#endif
