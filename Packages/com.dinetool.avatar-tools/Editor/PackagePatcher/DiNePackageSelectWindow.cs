using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 압축 파일 하나에서 .unitypackage 가 여러 개 나왔을 때, 목록에 넣기 전에
/// 무엇을 넣을지 고르게 하는 모달 창. 닫힐 때까지 호출 측을 막는다.
/// </summary>
internal class DiNePackageSelectWindow : EditorWindow
{
    private static readonly Color ColMint = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColSub  = new Color(0.40f, 0.55f, 0.70f);

    // 모달이 닫히면 창 인스턴스는 파괴되므로 결과는 정적 필드로 돌려준다.
    private static bool s_confirmed;

    private string   header;
    private string   progress; // "선택 창 1 / 3 · 2개 더 남음" (창이 하나뿐이면 null)
    private string[] names;
    private string[] subs;
    private bool[]   selected;
    private string   allText, noneText, addText, cancelText;
    private Vector2  scroll;

    /// <summary>
    /// selected 배열을 직접 고쳐 쓴다. "추가"를 누르면 true, 취소·닫기면 false.
    /// OnGUI 도중에 부르면 GUI 스택이 꼬이므로 delayCall 등 GUI 밖에서 호출할 것.
    /// </summary>
    public static bool Show(string title, string header, string progress, string[] names, string[] subs, bool[] selected,
                            string allText, string noneText, string addText, string cancelText)
    {
        s_confirmed = false;

        var w = CreateInstance<DiNePackageSelectWindow>();
        w.titleContent = new GUIContent(title);
        w.header     = header;
        w.progress   = progress;
        w.names      = names;
        w.subs       = subs;
        w.selected   = selected;
        w.allText    = allText;
        w.noneText   = noneText;
        w.addText    = addText;
        w.cancelText = cancelText;

        float height = Mathf.Clamp(names.Length * 42f + 150f + (string.IsNullOrEmpty(progress) ? 0f : 26f), 300f, 640f);
        var   size   = new Vector2(480f, height);
        var   main   = EditorGUIUtility.GetMainWindowPosition();
        w.minSize  = new Vector2(380f, 260f);
        w.position = new Rect(main.x + (main.width - size.x) * 0.5f, main.y + (main.height - size.y) * 0.5f, size.x, size.y);

        w.ShowModalUtility();
        return s_confirmed;
    }

    /// <summary>칸 왼쪽에 큰 체크박스용으로 비워 두는 폭.</summary>
    public const float ToggleColumnWidth = 30f;

    /// <summary>
    /// 이미 그려진 칸(row)의 왼쪽에 큰 체크박스를 얹는다. 칸 높이는 건드리지 않고,
    /// 칸 높이 전체 × ToggleColumnWidth 를 클릭 영역으로 써서 누르기 쉽게 한다.
    /// </summary>
    public static bool RowToggle(Rect row, bool value)
    {
        var hit = new Rect(row.x, row.y, ToggleColumnWidth + 4f, row.height);

        float size = Mathf.Floor(Mathf.Min(22f, row.height - 6f));
        var box = new Rect(Mathf.Round(hit.x + (hit.width - size) * 0.5f),
                           Mathf.Round(hit.y + (hit.height - size) * 0.5f), size, size);

        if (Event.current.type == EventType.Repaint)
        {
            bool hover = hit.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(box, value ? ColMint : hover ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.42f, 0.42f, 0.42f));
            var inner = new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f);
            EditorGUI.DrawRect(inner, value ? new Color(0.20f, 0.62f, 0.57f) : new Color(0.13f, 0.13f, 0.13f));
            if (value)
                GUI.Label(box, "✓", new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(size * 0.72f),
                      padding = new RectOffset(0, 0, 0, 0), normal = { textColor = Color.white } });
        }

        EditorGUIUtility.AddCursorRect(hit, MouseCursor.Link);
        if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
        {
            value = !value;
            GUI.changed = true;
        }
        return value;
    }

    void OnGUI()
    {
        if (names == null) { Close(); return; }

        GUILayout.Space(6);
        if (!string.IsNullOrEmpty(progress))
        {
            Rect bar = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, new Color(0.16f, 0.30f, 0.28f));
            GUI.Label(bar, progress, new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = ColMint } });
            GUILayout.Space(4);
        }
        EditorGUILayout.LabelField(header, new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 });
        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        int selCount = selected.Count(s => s);
        EditorGUILayout.LabelField($"{selCount} / {names.Length}", EditorStyles.boldLabel);
        if (GUILayout.Button(allText,  EditorStyles.miniButtonLeft,  GUILayout.Width(48))) for (int i = 0; i < selected.Length; i++) selected[i] = true;
        if (GUILayout.Button(noneText, EditorStyles.miniButtonRight, GUILayout.Width(48))) for (int i = 0; i < selected.Length; i++) selected[i] = false;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < names.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = selected[i] ? new Color(0.18f, 0.28f, 0.26f) : new Color(0.20f, 0.20f, 0.20f);
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = prevBg;

            // 체크박스 자리만 비워 두고, 칸이 다 그려진 뒤 칸 높이 전체를 클릭 영역으로 쓴다.
            GUILayout.Space(ToggleColumnWidth);
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            // 이름을 눌러도 토글되게 한다.
            if (GUILayout.Button(names[i], new GUIStyle(EditorStyles.label)
                    { fontSize = 11, clipping = TextClipping.Clip,
                      normal = { textColor = selected[i] ? Color.white : new Color(0.6f, 0.6f, 0.6f) } },
                    GUILayout.ExpandWidth(true)))
                selected[i] = !selected[i];
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(subs[i]))
                GUILayout.Label($"  ↳  {subs[i]}", new GUIStyle(EditorStyles.miniLabel)
                    { clipping = TextClipping.Clip, normal = { textColor = ColSub } });

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            selected[i] = RowToggle(GUILayoutUtility.GetLastRect(), selected[i]);
            GUILayout.Space(1);
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(cancelText, GUILayout.Height(32), GUILayout.Width(110)))
        {
            s_confirmed = false;
            Close();
            GUIUtility.ExitGUI();
        }

        EditorGUI.BeginDisabledGroup(selCount == 0);
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = selCount > 0 ? ColMint : Color.gray;
        if (GUILayout.Button($"{addText}  ({selCount})", new GUIStyle(GUI.skin.button)
                { fontSize = 12, fontStyle = FontStyle.Bold,
                  normal = { textColor = Color.white }, hover = { textColor = Color.white } },
                GUILayout.Height(32)))
        {
            s_confirmed = true;
            Close();
            GUIUtility.ExitGUI();
        }
        GUI.backgroundColor = prev;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);
    }
}
