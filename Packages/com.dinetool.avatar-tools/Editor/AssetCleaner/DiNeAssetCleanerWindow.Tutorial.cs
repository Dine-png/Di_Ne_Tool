using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DiNeTool.AssetCleaner
{
    public partial class DiNeAssetCleanerWindow
    {
        private DiNeGuidedTutorial guidedTutorial;
        private string[] tutorialAnalyzedScenes;

        private void OnGUI()
        {
            ConfigureTutorial();
            guidedTutorial.BeginFrame();
            try { DrawToolGUI(); guidedTutorial.Validate(); }
            finally { guidedTutorial.EndFrame(); }
        }

        private void ConfigureTutorial()
        {
            if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "AssetCleaner");
            guidedTutorial.Configure("Cleaner", "Asset Cleaner", "에셋 정리", "アセット整理", new[]
            {
                DiNeTutorialStep.Required("Scenes", "Selected scenes define what is in use; their referenced materials, textures, menu icons and other dependencies are kept. Select every scene whose assets you want to keep.", "선택한 씬을 기준으로 사용 여부를 판단하며 연결된 머티리얼·텍스처·메뉴 아이콘 등의 의존 에셋을 보존합니다. 에셋을 보존할 씬을 모두 선택하세요.", "選択したシーンを基準に使用状況を判定し、参照されるマテリアル・テクスチャ・メニューアイコンなどを保持します。 アセットを残すシーンをすべて選んでください。", () => _scenes.Any(s => s.Selected && AssetDatabase.LoadAssetAtPath<SceneAsset>(s.Path) != null)),
                DiNeTutorialStep.Optional("Filters", "Filters decide which asset categories can become cleanup candidates and which folders are excluded. Open the cleanup filters.", "필터는 정리 후보로 검사할 에셋 종류와 검사에서 제외할 폴더를 정합니다. 정리 필터를 펼치세요.", "フィルターは整理候補に含めるアセットの種類と、検査から除外するフォルダーを決めます。 整理フィルターを開いてください。"),
                DiNeTutorialStep.Optional("Categories", "Only checked types are considered, so you can inspect textures alone or keep reusable presets out of the list. Check the asset types to inspect.", "체크한 종류만 후보로 보므로 텍스처만 검사하거나 재사용 프리셋을 목록에서 제외할 수 있습니다. 검사할 에셋 종류를 체크하세요.", "チェックした種類だけを候補にするため、テクスチャだけを調べたり再利用するプリセットを一覧から外したりできます。 調べるアセットの種類をチェックしてください。", () => _filterFoldout = true),
                DiNeTutorialStep.Optional("EmptyFolders", "Including empty folders lets you find folder structure left behind after assets have been removed. Choose whether to include empty folders.", "빈 폴더를 포함하면 에셋을 지운 뒤 남은 폴더 구조도 찾을 수 있습니다. 빈 폴더도 후보에 포함할지 정하세요.", "空のフォルダーを含めると、アセット削除後に残ったフォルダー構造も探せます。 空のフォルダーも候補に含めるか選んでください。", () => _filterFoldout = true),
                DiNeTutorialStep.Optional("ProtectedFolders", "Protect reusable libraries and assets loaded by names or external systems, whose use may not be visible in scene references. Add folders that must be kept.", "재사용 라이브러리와 이름·외부 시스템으로 불러오는 에셋은 씬 참조에서 사용 여부가 드러나지 않을 수 있으므로 보호하세요. 꼭 남길 폴더를 보호 폴더에 추가하세요.", "再利用するライブラリや名前・外部システムで読み込むアセットはシーンの参照だけでは使用状況が分からない場合があるため、保護してください。 残すフォルダーを保護対象に追加してください。", () => _filterFoldout = true),
                DiNeTutorialStep.Required("Analyze", "Analysis lists assets not referenced by your selected scenes; it does not delete them or prove they are unused in every workflow. Press Analyze to find unused candidates.", "분석은 선택한 씬에서 참조하지 않는 에셋을 보여주며 삭제하거나 모든 용도에서 미사용임을 확정하지는 않습니다. 분석을 눌러 미사용 후보를 찾으세요.", "分析は選択したシーンから参照されないアセットを表示します。削除は行わず、すべての用途で未使用と確定するものでもありません。 分析を押して未使用候補を探してください。", TutorialAnalysisIsCurrent),
                DiNeTutorialStep.Optional("RootFolders", "This narrows the tree to top-level Assets folders that contain no assets used by the selected scenes. Use this button to list completely unused root folders.", "선택한 씬에서 쓰는 에셋이 없는 Assets 최상위 폴더만 트리에 표시하도록 범위를 좁힙니다. 완전히 미사용인 최상위 폴더만 보려면 이 버튼을 누르세요.", "選択したシーンで使うアセットを含まないAssets直下のフォルダーだけにツリーを絞り込みます。 完全に未使用な最上位フォルダーだけを見るには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Selection", "Checked items are the ones the cleanup action will remove, so you can retain candidates you still want. Check the candidates to clean up.", "체크한 항목만 정리 작업으로 제거하므로 아직 필요한 후보는 체크를 해제해 남길 수 있습니다. 정리할 후보를 체크하세요.", "整理処理で取り除くのはチェックした項目だけなので、残したい候補はチェックを外して保持できます。 整理する候補をチェックしてください。"),
                DiNeTutorialStep.Optional("Expand", "The folder tree groups candidates by location so you can review a package or library together. Use Expand / Collapse to inspect the folder tree.", "폴더 트리는 후보를 위치별로 묶어 패키지·라이브러리 단위로 검토할 수 있게 합니다. 펼치기·접기로 폴더 안을 살펴보세요.", "フォルダーツリーは候補を保存場所ごとにまとめ、パッケージやライブラリ単位で確認できます。 開く・閉じるでフォルダーの中を確認してください。"),
                DiNeTutorialStep.Optional("Locate", "Locating an item reveals its actual project location for checking its contents before cleanup. Press the locate button to reveal a candidate in Project.", "찾기는 후보의 실제 프로젝트 위치를 표시해 정리 전에 내용을 확인하게 합니다. 찾기 버튼으로 후보를 Project 창에서 확인하세요.", "検索は候補の実際の保存場所を表示し、整理前に内容を確認できるようにします。 検索ボタンで候補をProjectウィンドウに表示してください。"),
                DiNeTutorialStep.Optional("Preview", "Supported file previews help identify a candidate by appearance instead of its filename alone. Hover over a file to see its preview.", "지원하는 파일은 미리보기로 볼 수 있어 이름만으로 구분하기 어려운 후보를 확인할 수 있습니다. 파일에 마우스를 올려 미리보기를 확인하세요.", "対応するファイルはプレビューを表示でき、名前だけでは分かりにくい候補を見た目で確認できます。 ファイルにカーソルを合わせてプレビューを確認してください。"),
                DiNeTutorialStep.Optional("Trash", "This moves checked candidates to the operating system trash, freeing them from the project after confirmation. Press this button to send checked items to the trash.", "확인 후 체크한 후보를 운영체제 휴지통으로 이동해 프로젝트에서 제거합니다. 체크한 항목을 휴지통으로 보내려면 이 버튼을 누르세요.", "確認後、チェックした候補をOSのごみ箱へ移動し、プロジェクトから取り除きます。 チェックした項目をごみ箱へ送るには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("EmptyCleanup", "This removes empty folder trees while keeping protected folders. Press this button to clean up empty folders.", "보호 폴더는 유지하면서 빈 폴더 구조를 제거합니다. 빈 폴더만 정리하려면 이 버튼을 누르세요.", "保護フォルダーを保持しながら空のフォルダー構造を取り除きます。 空のフォルダーを整理するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Status", "The result summarizes what analysis or cleanup found and reports failures that need review. Read the analysis result here.", "결과는 분석·정리에서 찾은 내용을 요약하고 확인이 필요한 실패를 표시합니다. 여기서 분석 결과를 확인하세요.", "結果は分析・整理で見つかった内容をまとめ、確認が必要な失敗を表示します。 ここで分析結果を確認してください。")
            },
                overviewEn: "Find project assets that are not referenced by the scenes you choose, then review them in a folder tree before sending selected items to the operating system trash. Use it to remove leftover imported textures, materials or models. Keep every scene you still use selected and protect reusable libraries or assets loaded by names or external systems, because scene references alone do not cover every use.",
                overviewKo: "선택한 씬에서 참조하지 않는 프로젝트 에셋을 찾아 폴더 트리에서 검토하고, 고른 항목을 운영체제 휴지통으로 보냅니다. 가져온 뒤 남은 텍스처·머티리얼·모델 등을 정리할 때 사용합니다. 씬 참조만으로 모든 용도를 알 수는 없으므로 사용하는 씬을 모두 선택하고 재사용 라이브러리나 이름·외부 시스템으로 불러오는 에셋은 보호 폴더로 지정하세요.",
                overviewJa: "選択したシーンから参照されないプロジェクトアセットを探し、フォルダーツリーで確認してから選んだ項目をOSのごみ箱へ送ります。読み込み後に残ったテクスチャ・マテリアル・モデルなどの整理に使います。シーンの参照だけではすべての用途を把握できないため、使用するシーンをすべて選び、再利用するライブラリや名前・外部システムで読み込むアセットは保護フォルダーに指定してください。");
        }

        private bool TutorialAnalysisIsCurrent()
        {
            return _analyzed && _root != null && tutorialAnalyzedScenes != null &&
                tutorialAnalyzedScenes.SequenceEqual(_scenes.Where(s => s.Selected).Select(s => s.Path).OrderBy(p => p, StringComparer.Ordinal));
        }

        private void OnDisable() => guidedTutorial?.Suspend();
    }
}
