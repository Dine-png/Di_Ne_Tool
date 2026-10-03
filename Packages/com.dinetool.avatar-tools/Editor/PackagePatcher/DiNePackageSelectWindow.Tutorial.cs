using UnityEngine;

internal partial class DiNePackageSelectWindow
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
        if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "PackageSelection");
        guidedTutorial.Configure("ArchiveSelection", "Archive packages", "압축 안의 패키지", "圧縮内のパッケージ", new[]
        {
            DiNeTutorialStep.Optional("Rows", "Check the packages to add to the queue.", "대기 목록에 넣을 패키지를 체크하세요.", "待機一覧に入れるパッケージをチェックしてください。"),
            DiNeTutorialStep.Optional("Bulk", "Use All / None to change every checkbox.", "전체 선택·해제로 체크를 한꺼번에 바꾸세요.", "全選択・解除でチェックをまとめて変更してください。"),
            DiNeTutorialStep.Optional("Add", "Press Add to queue the checked packages.", "체크한 패키지를 대기 목록에 넣으려면 추가를 누르세요.", "チェックしたパッケージを待機一覧に入れるには、追加を押してください。"),
            DiNeTutorialStep.Optional("Cancel", "Press Cancel to close without adding packages.", "추가하지 않고 닫으려면 취소를 누르세요.", "追加せずに閉じるには、キャンセルを押してください。")
        });
    }

    private void OnDisable() => guidedTutorial?.Suspend();
}
