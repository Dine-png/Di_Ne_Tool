using UnityEngine;
using UnityEditor;

public partial class DiNeTexturePreviewWindow : EditorWindow
{
    private Texture _texture;
    private Vector2 _scroll;
    private float   _zoom = 1f;
    private bool    _isNormalMap;
    private const float ChromeHeight = 210f;

    public static void Open(Texture texture)
    {
        var win = CreateInstance<DiNeTexturePreviewWindow>();
        win.titleContent = new GUIContent(texture.name, DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png"));
        win._texture = texture;

        // FHD 기준 1/4 크기 (320 * 1.5 = 480px) 에 텍스처 비율 적용
        const float maxSide  = 480f;

        float zoom = maxSide / Mathf.Max(texture.width, texture.height);
        float imgW = texture.width  * zoom;
        float imgH = texture.height * zoom;

        // 노멀맵 여부 판단 (알파 투명 처리 방식 결정에 사용)
        string assetPath = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        win._isNormalMap = importer != null && importer.textureType == TextureImporterType.NormalMap;

        win._zoom    = zoom;
        win.minSize  = new Vector2(420, 320);
        win.position = new Rect(
            (Screen.currentResolution.width - Mathf.Max(imgW, win.minSize.x)) * 0.5f,
            (Screen.currentResolution.height - imgH - ChromeHeight) * 0.5f,
            Mathf.Max(imgW, win.minSize.x), Mathf.Max(imgH + ChromeHeight, win.minSize.y)
        );

        win.ShowUtility();
    }

    void DrawToolGUI()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (_texture == null) { Close(); return; }

            DiNeEditorUI.DrawHeader("Texture Preview", PreviewText(
                "Inspect texture detail and transparency at the desired scale.",
                "원하는 배율로 텍스처의 세부 표현과 투명도를 확인합니다.",
                "希望の倍率でテクスチャの細部と透明部分を確認します。"));
            GUILayout.Space(5f);
            DiNeEditorUI.DrawLanguageToolbar(Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2));
            GUILayout.Space(15f);

            // ── 상단 정보바 ──
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"{_texture.name}   {_texture.width} × {_texture.height}",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 }, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();

            // 줌 슬라이더
            GUILayout.Label(PreviewText("Zoom", "확대", "拡大"), GUILayout.Width(36));
            _zoom = GUILayout.HorizontalSlider(_zoom, 0.1f, 4f, GUILayout.Width(80));
            guidedTutorial.Anchor("Zoom", GUILayoutUtility.GetLastRect());
            if (GUILayout.Button("1:1", EditorStyles.toolbarButton, GUILayout.Width(28)))
                { _zoom = 1f; ResizeToZoom(); }
            guidedTutorial.Anchor("ActualSize", GUILayoutUtility.GetLastRect());
            if (GUILayout.Button(PreviewText("Fit", "맞춤", "合わせる"), EditorStyles.toolbarButton, GUILayout.Width(48)))
                { _zoom = 480f / Mathf.Max(_texture.width, _texture.height); ResizeToZoom(); }
            guidedTutorial.Anchor("Fit", GUILayoutUtility.GetLastRect());

            EditorGUILayout.EndHorizontal();
            guidedTutorial.DrawControls();
            guidedTutorial.Draw("Zoom");
            guidedTutorial.Draw("ActualSize");
            guidedTutorial.Draw("Fit");

            // ── 텍스처 표시 ──
            float drawW = _texture.width  * _zoom;
            float drawH = _texture.height * _zoom;

            guidedTutorial.BeginScrollScope();
            _scroll = EditorGUILayout.BeginScrollView(_scroll,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            Rect texRect = GUILayoutUtility.GetRect(drawW, drawH,
                GUILayout.Width(drawW), GUILayout.Height(drawH));

            // 노멀맵은 DrawPreviewTexture (RGB 불투명), 일반 텍스처는 DrawTextureTransparent (알파 포함)
            if (_isNormalMap)
                EditorGUI.DrawPreviewTexture(texRect, _texture, null, ScaleMode.StretchToFill);
            else
                EditorGUI.DrawTextureTransparent(texRect, _texture, ScaleMode.StretchToFill);

            EditorGUILayout.EndScrollView();
            guidedTutorial.EndScrollScope(GUILayoutUtility.GetLastRect());
            var tutorialImageViewport = GUILayoutUtility.GetLastRect();
            guidedTutorial.Draw("Image", tutorialImageViewport);
            guidedTutorial.Draw("Wheel", tutorialImageViewport);

            // 마우스 휠 줌
            if (Event.current.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom - Event.current.delta.y * 0.05f, 0.1f, 4f);
                Event.current.Use();
                Repaint();
            }
        }
    }

    private void ResizeToZoom()
    {
        if (_texture == null) return;
        float imgW = _texture.width  * _zoom;
        float imgH = _texture.height * _zoom;
        position = new Rect(position.x, position.y, Mathf.Max(imgW, minSize.x), Mathf.Max(imgH + ChromeHeight, minSize.y));
    }
}
