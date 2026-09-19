#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class DiNeHierarchyToggle
{
    // ── DiNe Brand Colors ──
    private static readonly Color ColMint     = new Color(0.30f, 0.82f, 0.76f);  // Active
    private static readonly Color ColDark     = new Color(0.21f, 0.21f, 0.24f);  // Inactive
    private static readonly Color ColTextOn   = Color.white;
    private static readonly Color ColTextOff  = new Color(0.50f, 0.50f, 0.53f);

    private const float BTN_SIZE    = 14f;
    private const float BTN_MARGIN  = 4f;  // 우측 끝에서의 여백
    private const float LABEL_OFFSET = 18f; // Unity hierarchy icon (16 px) + text gap (2 px).

    private static readonly List<MonoBehaviour> Components = new List<MonoBehaviour>();
    private static readonly GUIContent NameContent = new GUIContent();
    private static readonly MethodInfo HasWindowFocus = typeof(EditorGUIUtility).GetMethod(
        "HasCurrentWindowKeyFocus", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly System.Type GuiViewType = typeof(Editor).Assembly.GetType("UnityEditor.GUIView");
    private static readonly PropertyInfo CurrentGuiView = GuiViewType?.GetProperty("current");
    private static readonly PropertyInfo MouseOverGuiView = GuiViewType?.GetProperty("mouseOverView");
    private static GUIStyle nameStyle;
    private static GUIStyle selectionStyle;
    private static bool styleIsProSkin;
    private static Color hierarchyBackground;
    private static Color hierarchyHover;
    private static float prefabGutterWidth;

    static DiNeHierarchyToggle()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        var obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null) return;

        // 버튼을 하이어라키 창 우측 끝에 배치
        var btnRect = new Rect(
            selectionRect.xMax - BTN_SIZE - BTN_MARGIN,
            selectionRect.y + (selectionRect.height - BTN_SIZE) * 0.5f,
            BTN_SIZE,
            BTN_SIZE
        );

        DrawToolName(obj, instanceID, selectionRect, btnRect.xMin - 2f);

        bool isActive = obj.activeSelf;

        var prevBg    = GUI.backgroundColor;
        var prevColor = GUI.color;

        GUI.backgroundColor = isActive ? ColMint : ColDark;

        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize   = 9,
            fontStyle  = FontStyle.Bold,
            padding    = new RectOffset(0, 0, 0, 0),
            alignment  = TextAnchor.MiddleCenter,
        };
        style.normal.textColor  = isActive ? ColTextOn : ColTextOff;
        style.hover.textColor   = Color.white;
        style.active.textColor  = Color.white;

        string label = isActive ? "●" : "○";

        if (GUI.Button(btnRect, label, style))
            ToggleActive(obj, !isActive);

        GUI.backgroundColor = prevBg;
        GUI.color           = prevColor;
    }

    private static void DrawToolName(GameObject obj, int instanceID, Rect selectionRect, float textRight)
    {
        // UI standard: this only styles existing names; headers, new controls,
        // translations and serialized/Undo changes are not applicable.
        if (Event.current.type != EventType.Repaint) return;

        // Leave native rename and drag/drop feedback in charge while interacting.
        bool selected = Selection.Contains(instanceID);
        if ((selected && EditorGUIUtility.editingTextField) || DragAndDrop.objectReferences.Length > 0)
            return;
        if (!IsToolObject(obj)) return;

        EnsureNameStyle();
        var labelRect = selectionRect;
        labelRect.xMin += LABEL_OFFSET;
        if (textRight <= labelRect.xMin) return;

        bool focused = HasWindowFocus != null
            ? (bool)HasWindowFocus.Invoke(null, null)
            : EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType().Name == "SceneHierarchyWindow";
        var rowRect = new Rect(0f, selectionRect.y, selectionRect.xMax + prefabGutterWidth, selectionRect.height);
        bool mouseInThisView = CurrentGuiView != null && MouseOverGuiView != null
            ? Equals(CurrentGuiView.GetValue(null), MouseOverGuiView.GetValue(null))
            : EditorWindow.mouseOverWindow != null && EditorWindow.mouseOverWindow.GetType().Name == "SceneHierarchyWindow";
        bool hovered = mouseInThisView && rowRect.Contains(Event.current.mousePosition);

        // Unity reserves the prefab arrow gutter in every callback, but ordinary
        // rows can still have native text there when the name is very long.
        var backgroundRect = labelRect;
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(obj)) backgroundRect.xMax += prefabGutterWidth;

        var previousColor = GUI.color;
        var previousBackground = GUI.backgroundColor;
        try
        {
            // The callback runs after Unity's label. Clear only its text area to avoid
            // double lettering, keeping the native icon, foldout and prefab arrow intact.
            EditorGUI.DrawRect(backgroundRect, hierarchyBackground);
            if (hovered) EditorGUI.DrawRect(backgroundRect, hierarchyHover);
            if (selected) selectionStyle.Draw(backgroundRect, false, false, true, focused);

            GUI.color = Color.white;
            labelRect.xMax = textRight;
            NameContent.text = obj.name;
            var textColor = ColMint;
            if (!obj.activeInHierarchy) textColor.a = 0.5f;
            nameStyle.normal.textColor = textColor;
            nameStyle.Draw(labelRect, NameContent, false, false, false, false);
        }
        finally
        {
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackground;
        }
    }

    private static bool IsToolObject(GameObject obj)
    {
        // Direct package components only: renamed tools still match, but their parents,
        // targets and temporary editor preview markers do not become branded tool rows.
        obj.GetComponents(Components);
        bool isTool = false;
        foreach (var component in Components)
        {
            if (component != null && component.GetType().Assembly == typeof(DiNeMultiDresser).Assembly)
            {
                isTool = true;
                break;
            }
        }
        Components.Clear();
        return isTool;
    }

    private static void EnsureNameStyle()
    {
        if (nameStyle != null && styleIsProSkin == EditorGUIUtility.isProSkin) return;

        styleIsProSkin = EditorGUIUtility.isProSkin;
        nameStyle = new GUIStyle("TV Line")
        {
            fontStyle = FontStyle.Bold,
            richText = false,
            clipping = TextClipping.Clip,
        };
        nameStyle.padding.left = 0;
        selectionStyle = new GUIStyle("TV Selection");
        var prefabArrowStyle = new GUIStyle("ArrowNavigationRight");
        prefabGutterWidth = prefabArrowStyle.fixedWidth + prefabArrowStyle.margin.horizontal;

        // Match Unity's skin instead of introducing a Di Ne row-background palette.
        float background = styleIsProSkin ? 0.22f : 0.76f;
        hierarchyBackground = new Color(background, background, background, 1f);
        hierarchyHover = Color.clear;
        try
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var backgroundValue = typeof(EditorGUIUtility).GetField("kViewBackgroundColor", flags)?.GetValue(null);
            var colorConversion = backgroundValue?.GetType().GetMethod("op_Implicit", flags);
            if (colorConversion != null)
                hierarchyBackground = (Color)colorConversion.Invoke(null, new[] { backgroundValue });

            var nativeStyles = typeof(Editor).Assembly.GetType("UnityEditor.GameObjectTreeViewGUI+GameObjectStyles");
            var hoverValue = nativeStyles?.GetField("hoveredBackgroundColor", flags)?.GetValue(null);
            if (hoverValue is Color hoverColor) hierarchyHover = hoverColor;
        }
        catch (System.Exception)
        {
            // Other Unity versions can fall back to the standard editor background.
        }
    }

    private static void ToggleActive(GameObject clicked, bool newState)
    {
        // Shift + 다중 선택 시 선택된 오브젝트 전체에 적용
        var selected = Selection.gameObjects;
        bool bulk = Event.current.shift
                    && selected.Length > 1
                    && System.Array.IndexOf(selected, clicked) >= 0;

        if (bulk)
        {
            Undo.RecordObjects(selected, "DiNe Toggle Active");
            foreach (var obj in selected)
                obj.SetActive(newState);
        }
        else
        {
            Undo.RecordObject(clicked, "DiNe Toggle Active");
            clicked.SetActive(newState);
        }
    }
}
#endif
