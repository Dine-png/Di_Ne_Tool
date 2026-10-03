using UnityEditor;
using UnityEngine;

public partial class DiNeTexturePreviewWindow
{
    private DiNeGuidedTutorial guidedTutorial;
    private string PreviewText(string en, string ko, string ja)
    {
        return new[] { en, ko, ja }[Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2)];
    }

    private void OnGUI()
    {
        ConfigureTutorial();
        guidedTutorial.BeginFrame();
        try { DrawToolGUI(); guidedTutorial.Validate(); }
        finally { guidedTutorial.EndFrame(); }
    }

    private void ConfigureTutorial()
    {
        if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "TexturePreview");
        guidedTutorial.Configure("Preview", "Texture preview", "텍스처 미리보기", "テクスチャープレビュー", new[]
        {
            DiNeTutorialStep.Optional("Zoom", "Move the zoom slider to change the preview scale.", "확대 슬라이더로 미리보기 배율을 조절하세요.", "拡大スライダーでプレビューの倍率を変えてください。"),
            DiNeTutorialStep.Optional("ActualSize", "Press 1:1 to view the texture at its original scale.", "원래 배율로 보려면 1:1을 누르세요.", "元の倍率で表示するには1:1を押してください。"),
            DiNeTutorialStep.Optional("Fit", "Press Fit to resize the preview to its usual size.", "기본 크기로 맞추려면 맞춤을 누르세요.", "通常のサイズに合わせるには合わせるを押してください。"),
            DiNeTutorialStep.Optional("Image", "Use the scrollbar to inspect the enlarged image.", "스크롤 막대로 확대된 이미지를 살펴보세요.", "スクロールバーで拡大した画像を確認してください。"),
            DiNeTutorialStep.Optional("Wheel", "Use the mouse wheel over the image to adjust zoom.", "이미지 위에서 마우스 휠로 배율을 조절하세요.", "画像上でマウスホイールを回して倍率を変えてください。")
        });
    }

    private void OnDisable() => guidedTutorial?.Suspend();
}
