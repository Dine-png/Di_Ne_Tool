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

        // FHD 기준 1/6 크기에 텍스처 비율 적용
        const float toolbarH = 32f;
        const float maxArea  = 1920f / 6f; // 기준 변 길이 320px

        float aspect = (float)texture.width / texture.height;
        float winW, winH;
        if (aspect >= 1f)
        {
            winW = maxArea;
            winH = maxArea / aspect + toolbarH;
        }
        else
        {
            winH = maxArea + toolbarH;
            winW = maxArea * aspect;
        }

        win._zoom = maxArea / Mathf.Max(texture.width, texture.height);
        win.minSize = new Vector2(120, 120);
        win.position = new Rect(
            (Screen.currentResolution.width  - winW) * 0.5f,
            (Screen.currentResolution.height - winH) * 0.5f,
            winW, winH
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
