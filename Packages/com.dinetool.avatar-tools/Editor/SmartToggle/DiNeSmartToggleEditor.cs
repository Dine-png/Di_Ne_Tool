#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[CustomEditor(typeof(DiNeSmartToggle))]
public sealed class DiNeSmartToggleEditor : Editor
{
    private static readonly Color Mint = new Color(0.10f, 0.36f, 0.33f);
    private static readonly Color MintActive = new Color(0.12f, 0.46f, 0.42f);
    private static readonly Color DarkPanel = new Color(0.18f, 0.19f, 0.19f);

    private SerializedProperty displayName;
    private SerializedProperty parameterName;
    private SerializedProperty defaultOn;
    private SerializedProperty saved;
    private SerializedProperty menuPlacement;
    private SerializedProperty groupName;
    private SerializedProperty icon;

    [MenuItem("GameObject/Di Ne/Smart Toggle", false, 20)]
    private static void AddSmartToggle(MenuCommand command)
    {
        var gameObject = command.context as GameObject;
        if (gameObject == null || gameObject.GetComponent<DiNeSmartToggle>() != null)
            return;

        var component = Undo.AddComponent<DiNeSmartToggle>(gameObject);
        component.DisplayName = gameObject.name;
        component.ParameterName = MakeUniqueParameterName(gameObject);
        component.DefaultOn = gameObject.activeSelf;
        component.EnsureDefaults();
        EditorUtility.SetDirty(component);
        Selection.activeGameObject = gameObject;

        EditorApplication.delayCall += () =>
        {
            if (component == null || component.Icon != null) return;
            DiNeSmartToggleGenerator.EnsureIcon(component);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        };
    }

    [MenuItem("GameObject/Di Ne/Smart Toggle", true)]
    private static bool ValidateAddSmartToggle(MenuCommand command)
    {
        var gameObject = command.context as GameObject;
        return gameObject != null && gameObject.GetComponent<DiNeSmartToggle>() == null;
    }

    private static string MakeUniqueParameterName(GameObject targetObject)
    {
        string baseName = DiNeSmartToggle.BuildDefaultParameterName(targetObject.name);
        var avatar = targetObject.GetComponentInParent<VRCAvatarDescriptor>();
        if (avatar == null) return baseName;

        var used = avatar.GetComponentsInChildren<DiNeSmartToggle>(true)
            .Where(item => item != null)
            .Select(item => item.ParameterName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet();
        string candidate = baseName;
        for (int suffix = 2; used.Contains(candidate); suffix++)
            candidate = baseName + "_" + suffix;
        return candidate;
    }

    private void OnEnable()
    {
        displayName = serializedObject.FindProperty("displayName");
        parameterName = serializedObject.FindProperty("parameterName");
        defaultOn = serializedObject.FindProperty("defaultOn");
        saved = serializedObject.FindProperty("saved");
        menuPlacement = serializedObject.FindProperty("menuPlacement");
        groupName = serializedObject.FindProperty("groupName");
        icon = serializedObject.FindProperty("icon");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var smartToggle = (DiNeSmartToggle)target;

        DrawSmartToggleHeader();
        EditorGUILayout.Space(6f);
        DrawIconCard(smartToggle);
        EditorGUILayout.Space(8f);
        DrawSettings();
        EditorGUILayout.Space(8f);
        DrawMenuPlacement();
        EditorGUILayout.Space(8f);
        DrawStatus(smartToggle);

        if (serializedObject.ApplyModifiedProperties())
        {
            smartToggle.EnsureDefaults();
            EditorUtility.SetDirty(smartToggle);
        }
    }

    private static void DrawSmartToggleHeader()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 42f);
        EditorGUI.DrawRect(rect, Mint);
        var title = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            normal = { textColor = Color.white }
        };
        GUI.Label(rect, "SMART TOGGLE", title);
    }

    private void DrawIconCard(DiNeSmartToggle smartToggle)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("메뉴 아이콘", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(160f, 200f, 160f, 200f);
            previewRect.width = Mathf.Min(200f, previewRect.width);
            previewRect.x += (EditorGUIUtility.currentViewWidth - previewRect.width - 24f) * 0.5f;
            EditorGUI.DrawRect(previewRect, DarkPanel);
            if (icon.objectReferenceValue != null)
                GUI.DrawTexture(previewRect, (Texture)icon.objectReferenceValue, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(previewRect, "아이콘 생성 대기", CenteredWhiteLabel());

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MintButton("아이콘 편집", 30f))
                    DiNeScreenSaver.DiNeScreenSaver.OpenIconEditor(smartToggle);
                if (GUILayout.Button("자동 생성", GUILayout.Height(30f), GUILayout.Width(86f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    DiNeSmartToggleGenerator.RegenerateIcon(smartToggle);
                    serializedObject.Update();
                }
            }
            EditorGUILayout.PropertyField(icon, new GUIContent("직접 지정"));
        }
    }

    private void DrawSettings()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("토글 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(displayName, new GUIContent("메뉴 이름"));
            EditorGUILayout.PropertyField(parameterName, new GUIContent("Bool 파라미터"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(defaultOn, new GUIContent("기본 ON"));
                EditorGUILayout.PropertyField(saved, new GUIContent("값 저장"));
            }
        }
    }

    private void DrawMenuPlacement()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("메뉴 위치", EditorStyles.boldLabel);
            int placement = menuPlacement.enumValueIndex;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (SegmentButton("최상단에 노출", placement == 0)) placement = 0;
                if (SegmentButton("토글끼리 묶기", placement == 1)) placement = 1;
            }
            menuPlacement.enumValueIndex = placement;
            if (placement == 1)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.PropertyField(groupName, new GUIContent("새 메뉴 이름"));
                EditorGUILayout.HelpBox("같은 메뉴 이름을 사용한 스마트 토글이 하나의 하위 메뉴로 묶입니다.", MessageType.Info);
            }
        }
    }

    private static void DrawStatus(DiNeSmartToggle smartToggle)
    {
        var avatar = smartToggle.GetComponentInParent<VRCAvatarDescriptor>();
        if (avatar == null)
            EditorGUILayout.HelpBox("이 오브젝트 위에서 VRCAvatarDescriptor를 찾을 수 없습니다.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox($"{avatar.gameObject.name} 아바타의 플레이 모드 및 업로드용 메뉴에 자동 적용됩니다.", MessageType.None);
    }

    private static bool MintButton(string label, float height)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = MintActive;
        bool clicked = GUILayout.Button(label, GUILayout.Height(height));
        GUI.backgroundColor = previous;
        return clicked;
    }

    private static bool SegmentButton(string label, bool selected)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = selected ? MintActive : new Color(0.34f, 0.35f, 0.35f);
        var style = new GUIStyle(GUI.skin.button)
        {
            fixedHeight = 30f,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
            active = { textColor = Color.white },
            focused = { textColor = Color.white }
        };
        bool clicked = GUILayout.Button(label, style);
        GUI.backgroundColor = previous;
        return clicked;
    }

    private static GUIStyle CenteredWhiteLabel()
    {
        return new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
    }
}
#endif
