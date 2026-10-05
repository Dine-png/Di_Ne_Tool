using System;
using System.IO;
using System.Linq;
using UnityEngine;

public partial class DiNePackagePatcher
{
    private DiNeGuidedTutorial guidedTutorial;

    private void OnGUI()
    {
        ConfigureTutorial();
        guidedTutorial.BeginFrame();
        try { DrawToolGUI(); guidedTutorial.Validate(); }
        finally { guidedTutorial.EndFrame(); }
    }

    private void ConfigureTutorial()
    {
        if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "PackagePatcher");
        guidedTutorial.Configure("Packages", "Package Patcher", "패키지 정리", "パッケージ整理", new[]
        {
            DiNeTutorialStep.Required("Folder", "Newly imported asset roots are organized under Assets followed by this folder name, keeping imports together. Enter the folder name for organizing imports.", "새로 가져온 에셋의 최상위 폴더를 Assets 아래의 이 이름 폴더에 모아 정리합니다. 가져온 패키지를 정리할 폴더 이름을 입력하세요.", "新しく読み込んだアセットの最上位フォルダーを、Assets以下のこの名前のフォルダーへまとめて整理します。 読み込んだパッケージを整理するフォルダー名を入力してください。", () => !string.IsNullOrWhiteSpace(targetFolderName) && string.Equals(GetSafeFolderName(targetFolderName), targetFolderName.Trim(), StringComparison.Ordinal)),
            DiNeTutorialStep.Optional("BrowseFiles", "You can queue unitypackages directly or find unitypackages inside supported archives. Press this button to choose a package or archive.", "unitypackage를 직접 대기 목록에 넣거나 지원하는 압축 파일 안의 unitypackage를 찾을 수 있습니다. 패키지나 압축 파일을 고르려면 이 버튼을 누르세요.", "unitypackageを直接待機一覧に入れるか、対応する圧縮ファイル内のunitypackageを探せます。 パッケージや圧縮ファイルを選ぶには、このボタンを押してください。"),
            DiNeTutorialStep.Optional("BrowseFolder", "Folder search gathers packages for a batch, useful when several purchased assets are stored together. Press this button to find packages in a folder.", "폴더 검색은 함께 보관한 여러 구매 에셋처럼 패키지를 일괄로 모아 가져올 때 사용합니다. 폴더 안의 패키지를 찾으려면 이 버튼을 누르세요.", "フォルダー検索は購入した複数のアセットなど、一緒に保存したパッケージをまとめて読み込む際に使います。 フォルダー内のパッケージを探すには、このボタンを押してください。"),
            DiNeTutorialStep.Required("Queue", "The queue collects import sources; adding one prepares it for selection before import starts. Drop a package or archive here.", "대기 목록은 가져올 원본 파일을 모으며 추가한 뒤 실제 가져오기 전에 선택할 수 있습니다. 패키지나 압축 파일을 여기에 놓으세요.", "待機一覧は読み込み元のファイルを集め、追加後に実際の読み込み前に選択できます。 パッケージや圧縮ファイルをここに置いてください。", () => foundPackages.Any(TutorialHasPackageSource)),
            DiNeTutorialStep.Optional("Rows", "Only checked rows are imported, so one batch can include just the assets you need. Check the packages to import.", "체크한 행만 가져오므로 모은 파일 중 필요한 에셋만 골라 일괄 처리할 수 있습니다. 가져올 패키지를 체크하세요.", "チェックした行だけを読み込むため、集めたファイルから必要なアセットだけをまとめて処理できます。 読み込むパッケージをチェックしてください。"),
            DiNeTutorialStep.Optional("Bulk", "Bulk selection lets you include or exclude the whole queue before fine-tuning individual rows. Use All / None to change every checkbox.", "전체 선택은 대기 목록을 한꺼번에 포함·제외한 뒤 개별 행을 조절할 때 사용합니다. 전체 선택·해제로 체크 상태를 한꺼번에 바꾸세요.", "一括選択は待機一覧をまとめて含める・除外してから、個別の行を調整する際に使います。 全選択・解除でチェックをまとめて変更してください。"),
            DiNeTutorialStep.Optional("Remove", "Removing a queue row excludes that source from this batch and keeps the original file on disk. Press the cross to remove a row from the queue.", "목록에서 빼면 이번 일괄 작업에서만 제외되며 디스크의 원본 파일은 남습니다. 대기 목록에서 항목을 빼려면 ✕를 누르세요.", "待機一覧から外すと今回の一括処理からだけ除外され、ディスク上の元ファイルは残ります。 待機一覧から項目を外すには✕を押してください。"),
            DiNeTutorialStep.Optional("Import", "Import processes checked packages in sequence, then organizes their newly added asset roots into your chosen folder. Press Import to import the checked packages.", "체크한 패키지를 순서대로 가져온 뒤 새로 추가된 에셋의 최상위 폴더를 지정한 폴더로 정리합니다. 체크한 패키지를 가져오려면 가져오기를 누르세요.", "チェックしたパッケージを順番に読み込み、新しく追加されたアセットの最上位フォルダーを指定先へ整理します。 チェックしたパッケージを読み込むには、読み込みを押してください。"),
            DiNeTutorialStep.Optional("Progress", "Per-package results distinguish completed imports from errors so you can identify a batch item to retry. Read the progress bar and result badges.", "패키지별 결과에서 완료와 오류를 구분해 다시 처리할 항목을 찾을 수 있습니다. 진행 막대와 결과 표시를 확인하세요.", "パッケージごとの結果で完了とエラーを区別し、再処理が必要な項目を見つけられます。 進行バーと結果表示を確認してください。")
        },
            overviewEn: "Batch-import unitypackages and organize their newly added asset roots under one folder in Assets. Add package files, search a download folder, or select packages found inside supported archives. Choose which queued packages to import, then review each result after the sequential imports and folder organization.",
            overviewKo: "여러 unitypackage를 일괄로 가져오고 새로 추가된 에셋의 최상위 폴더를 Assets 아래 한 폴더에 모아 정리합니다. 패키지 파일을 넣거나 다운로드 폴더를 검색하고, 지원하는 압축 파일 안의 패키지를 선택할 수 있습니다. 대기 목록에서 가져올 항목을 고른 뒤 순차 가져오기와 폴더 정리 결과를 확인합니다.",
            overviewJa: "複数のunitypackageを一括読み込みし、新しく追加されたアセットの最上位フォルダーをAssets以下の1つのフォルダーへまとめて整理します。パッケージファイルの追加、ダウンロードフォルダーの検索、対応する圧縮内のパッケージ選択ができます。待機一覧から読み込む項目を選び、順番に読み込んだ後でフォルダー整理と各結果を確認します。");
    }

    private static bool TutorialHasPackageSource(PackageItem item)
    {
        return item != null && (File.Exists(item.SourcePath) || File.Exists(item.CachedTempPath));
    }
}
