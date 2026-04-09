using UnityEngine;
using UnityEditor;

public class DiNeTexturePreviewWindow : EditorWindow
{
    private Texture _texture;
    private Vector2 _scroll;
    private float   _zoom = 1f;

    public static void Open(Texture texture)
    {
        var win = CreateInstance<DiNeTexturePreviewWindow>();
        win.titleContent = new GUIContent(texture.name);
        win._texture = texture;

        // FHD 기준 1/6 크기의 창 (약 320×320)
        const float toolbarH = 32f;
        float winW = 1920f / 6f;           // 320
        float winH = 1080f / 6f + toolbarH; // 180 + 32 = 212 → 정사각 느낌으로 winW 사용
        winH = winW + toolbarH;            // 320 + 32 = 352

        win.minSize = new Vector2(160, 160);
        win.position = new Rect(
            (Screen.currentResolution.width  - winW) * 0.5f,
            (Screen.currentResolution.height - winH) * 0.5f,
            winW, winH
        );

        // 초기 줌을 창에 맞게 설정
        win._zoom = Mathf.Min(winW / texture.width, (winH - toolbarH) / texture.height);

        win.ShowUtility();
    }

    void OnGUI()
    {
        if (_texture == null) { Close(); return; }

        // ── 상단 정보바 ──
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"{_texture.name}   {_texture.width} × {_texture.height}",
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.FlexibleSpace();

        // 줌 슬라이더
        GUILayout.Label("Zoom", GUILayout.Width(36));
        _zoom = GUILayout.HorizontalSlider(_zoom, 0.1f, 4f, GUILayout.Width(80));
        if (GUILayout.Button("1:1", EditorStyles.toolbarButton, GUILayout.Width(28)))
            _zoom = 1f;
        if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(28)))
            _zoom = Mathf.Min((position.width) / _texture.width, (position.height - 32f) / _texture.height);

        EditorGUILayout.EndHorizontal();

        // ── 텍스처 표시 ──
        float drawW = _texture.width  * _zoom;
        float drawH = _texture.height * _zoom;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        Rect texRect = GUILayoutUtility.GetRect(drawW, drawH);

        // 체커보드 배경 (알파 확인용)
        EditorGUI.DrawTextureTransparent(texRect, _texture, ScaleMode.ScaleToFit);

        EditorGUILayout.EndScrollView();

        // 마우스 휠 줌
        if (Event.current.type == EventType.ScrollWheel && position.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)))
        {
            _zoom = Mathf.Clamp(_zoom - Event.current.delta.y * 0.05f, 0.1f, 4f);
            Event.current.Use();
            Repaint();
        }
    }
}
