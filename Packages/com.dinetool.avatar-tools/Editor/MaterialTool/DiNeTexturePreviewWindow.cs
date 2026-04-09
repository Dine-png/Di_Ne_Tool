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

        // FHD 기준 1/4 크기 (320 * 1.5 = 480px) 에 텍스처 비율 적용
        const float toolbarH = 22f;
        const float maxSide  = 480f;

        float zoom = maxSide / Mathf.Max(texture.width, texture.height);
        float imgW = texture.width  * zoom;
        float imgH = texture.height * zoom;

        win._zoom    = zoom;
        win.minSize  = new Vector2(120, 120);
        win.maxSize  = new Vector2(imgW + 2, imgH + toolbarH + 2);
        win.position = new Rect(
            (Screen.currentResolution.width  - imgW) * 0.5f,
            (Screen.currentResolution.height - imgH - toolbarH) * 0.5f,
            imgW, imgH + toolbarH
        );

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
            { _zoom = 1f; ResizeToZoom(); }
        if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(28)))
            { _zoom = 480f / Mathf.Max(_texture.width, _texture.height); ResizeToZoom(); }

        EditorGUILayout.EndHorizontal();

        // ── 텍스처 표시 ──
        float drawW = _texture.width  * _zoom;
        float drawH = _texture.height * _zoom;

        _scroll = EditorGUILayout.BeginScrollView(_scroll,
            GUILayout.Width(position.width), GUILayout.Height(position.height - 22f));

        Rect texRect = GUILayoutUtility.GetRect(drawW, drawH,
            GUILayout.Width(drawW), GUILayout.Height(drawH));

        EditorGUI.DrawTextureTransparent(texRect, _texture, ScaleMode.StretchToFill);

        EditorGUILayout.EndScrollView();

        // 마우스 휠 줌
        if (Event.current.type == EventType.ScrollWheel)
        {
            _zoom = Mathf.Clamp(_zoom - Event.current.delta.y * 0.05f, 0.1f, 4f);
            Event.current.Use();
            Repaint();
        }
    }

    private void ResizeToZoom()
    {
        if (_texture == null) return;
        const float toolbarH = 22f;
        float imgW = _texture.width  * _zoom;
        float imgH = _texture.height * _zoom;
        maxSize = new Vector2(imgW + 2, imgH + toolbarH + 2);
        position = new Rect(position.x, position.y, imgW, imgH + toolbarH);
    }
}
