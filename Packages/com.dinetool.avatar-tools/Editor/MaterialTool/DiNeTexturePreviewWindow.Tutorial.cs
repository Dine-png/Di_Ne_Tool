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
            DiNeTutorialStep.Optional("Zoom", "Move the zoom slider to inspect fine details or view more of the texture. This changes only the preview scale, so it does not alter resolution or VRAM usage.", "확대 슬라이더로 세부 무늬를 살펴보거나 텍스처의 더 넓은 범위를 보세요. 미리보기 배율만 바꾸므로 해상도나 VRAM 사용량은 변경되지 않습니다.", "拡大スライダーで細かい模様を確認したり、テクスチャの広い範囲を表示してください。プレビュー倍率だけが変わり、解像度やVRAM使用量は変わりません。"),
            DiNeTutorialStep.Optional("ActualSize", "Press 1:1 to display one texture pixel at one preview pixel. Use this scale to judge the texture's detail after a resolution or compression change.", "1:1을 누르면 텍스처의 한 픽셀을 미리보기의 한 픽셀로 표시합니다. 해상도·압축 변경 후 세부 표현을 확인할 때 이 배율을 사용하세요.", "1:1を押すと、テクスチャの1ピクセルをプレビューの1ピクセルで表示します。解像度・圧縮の変更後に細部を確認するための倍率です。"),
            DiNeTutorialStep.Optional("Fit", "Press Fit to return to the overview scale with the longest image side at 480 pixels. This makes a large texture easier to review as a whole after zooming in.", "맞춤을 누르면 이미지의 긴 쪽을 480픽셀로 맞추는 기본 배율로 돌아갑니다. 확대해서 살펴본 뒤 큰 텍스처의 전체 구성을 다시 확인할 수 있습니다.", "合わせるを押すと、画像の長辺を480ピクセルにする全体表示の倍率へ戻ります。拡大した後、大きなテクスチャの全体構成を確認できます。"),
            DiNeTutorialStep.Optional("Image", "Use the scrollbars when the enlarged texture extends beyond the visible area. Inspect its edges and small patterns; ordinary textures show transparency, while normal maps use an opaque preview.", "확대한 텍스처가 화면 밖으로 나가면 스크롤 막대로 이동해 가장자리와 작은 무늬를 확인하세요. 일반 텍스처는 투명도를 표시하고 노멀 맵은 불투명한 미리보기로 표시합니다.", "拡大したテクスチャが表示範囲を超えたら、スクロールバーで端や小さな模様を確認してください。通常のテクスチャは透明部分を表示し、ノーマルマップは不透明なプレビューで表示します。"),
            DiNeTutorialStep.Optional("Wheel", "Use the mouse wheel over the image to increase or decrease zoom quickly. Combine zoom with the scrollbars to inspect a specific area without changing the texture asset.", "이미지 위에서 마우스 휠을 돌려 배율을 빠르게 올리거나 내리세요. 스크롤 막대와 함께 사용하면 텍스처 에셋을 바꾸지 않고 특정 부분을 자세히 볼 수 있습니다.", "画像上でマウスホイールを回し、倍率を素早く上げ下げしてください。スクロールバーと組み合わせ、テクスチャアセットを変更せずに特定の部分を確認できます。")
        },
            overviewEn: "Inspect a texture at a larger scale before deciding how to optimize it. Open this window by double-clicking a texture thumbnail in VRAM Optimize, then use zoom, 1:1, and scrolling to check fine detail or transparency. The controls change only the display; texture import settings are edited in the Material Tool.",
            overviewKo: "텍스처를 크게 보면서 최적화할 때 유지해야 할 세부 표현과 투명도를 확인할 수 있습니다. VRAM 최적화에서 텍스처 썸네일을 두 번 클릭해 열고, 확대·1:1·스크롤로 필요한 부분을 살펴보세요. 이 창은 표시 방식만 바꾸며 텍스처 임포트 설정은 Material Tool에서 변경합니다.",
            overviewJa: "テクスチャを大きく表示し、最適化で保ちたい細部や透明部分を確認できます。VRAM最適化のテクスチャサムネイルをダブルクリックして開き、拡大・1:1・スクロールで必要な部分を見てください。このウィンドウは表示だけを変え、インポート設定はMaterial Toolで変更します。");
    }

    private void OnDisable() => guidedTutorial?.Suspend();
}
