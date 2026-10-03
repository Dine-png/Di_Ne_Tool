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
            DiNeTutorialStep.Required("Folder", "Enter the folder name for organizing imports.", "가져온 패키지를 정리할 폴더 이름을 입력하세요.", "読み込んだパッケージを整理するフォルダー名を入力してください。", () => !string.IsNullOrWhiteSpace(targetFolderName) && string.Equals(GetSafeFolderName(targetFolderName), targetFolderName.Trim(), StringComparison.Ordinal)),
            DiNeTutorialStep.Optional("BrowseFiles", "Press this button to choose a package or archive.", "패키지나 압축 파일을 고르려면 이 버튼을 누르세요.", "パッケージや圧縮ファイルを選ぶには、このボタンを押してください。"),
            DiNeTutorialStep.Optional("BrowseFolder", "Press this button to find packages in a folder.", "폴더 안의 패키지를 찾으려면 이 버튼을 누르세요.", "フォルダー内のパッケージを探すには、このボタンを押してください。"),
            DiNeTutorialStep.Required("Queue", "Drop a package or archive here.", "패키지나 압축 파일을 여기에 놓으세요.", "パッケージや圧縮ファイルをここに置いてください。", () => foundPackages.Any(TutorialHasPackageSource)),
            DiNeTutorialStep.Optional("Rows", "Check the packages to import.", "가져올 패키지를 체크하세요.", "読み込むパッケージをチェックしてください。"),
            DiNeTutorialStep.Optional("Bulk", "Use All / None to change every checkbox.", "전체 선택·해제로 체크 상태를 한꺼번에 바꾸세요.", "全選択・解除でチェックをまとめて変更してください。"),
            DiNeTutorialStep.Optional("Remove", "Press the cross to remove a row from the queue.", "대기 목록에서 항목을 빼려면 ✕를 누르세요.", "待機一覧から項目を外すには✕を押してください。"),
            DiNeTutorialStep.Optional("Import", "Press Import to import the checked packages.", "체크한 패키지를 가져오려면 가져오기를 누르세요.", "チェックしたパッケージを読み込むには、読み込みを押してください。"),
            DiNeTutorialStep.Optional("Progress", "Read the progress bar and result badges.", "진행 막대와 결과 표시를 확인하세요.", "進行バーと結果表示を確認してください。")
        });
    }

    private static bool TutorialHasPackageSource(PackageItem item)
    {
        return item != null && (File.Exists(item.SourcePath) || File.Exists(item.CachedTempPath));
    }
}
