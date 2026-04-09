#if UNITY_EDITOR
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
