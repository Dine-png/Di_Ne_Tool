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
            DiNeTutorialStep.Optional("Rows", "An archive may contain several unitypackages; select only the contents you want to pass to Package Patcher. Check the packages to add to the queue.", "압축 파일에는 여러 unitypackage가 있을 수 있으므로 Package Patcher로 넘길 내용만 고릅니다. 대기 목록에 넣을 패키지를 체크하세요.", "圧縮ファイルには複数のunitypackageが含まれる場合があるため、Package Patcherへ渡すものだけを選びます。 待機一覧に入れるパッケージをチェックしてください。"),
            DiNeTutorialStep.Optional("Bulk", "Bulk selection is useful for taking every package in the archive or starting with none selected. Use All / None to change every checkbox.", "전체 선택·해제는 압축 안의 모든 패키지를 고르거나 선택을 비우고 다시 고를 때 사용합니다. 전체 선택·해제로 체크를 한꺼번에 바꾸세요.", "全選択・解除は、圧縮内の全パッケージを選ぶか、選択を空にして選び直す際に使います。 全選択・解除でチェックをまとめて変更してください。"),
            DiNeTutorialStep.Optional("Add", "Adding passes selected entries to the main window's queue; import starts later from that window. Press Add to queue the checked packages.", "추가는 선택한 항목을 기본 창의 대기 목록으로 넘기며 실제 가져오기는 그 창에서 실행합니다. 체크한 패키지를 대기 목록에 넣으려면 추가를 누르세요.", "追加は選択項目をメインウィンドウの待機一覧へ渡し、実際の読み込みはそのウィンドウから行います。 チェックしたパッケージを待機一覧に入れるには、追加を押してください。"),
            DiNeTutorialStep.Optional("Cancel", "Cancel leaves the main import queue unchanged for this archive selection. Press Cancel to close without adding packages.", "취소하면 이번 압축 파일 선택 내용을 기본 가져오기 대기 목록에 반영하지 않습니다. 추가하지 않고 닫으려면 취소를 누르세요.", "キャンセルすると今回の圧縮ファイルの選択をメインの読み込み待機一覧へ反映しません。 追加せずに閉じるには、キャンセルを押してください。")
        },
            overviewEn: "Choose which unitypackages inside an archive should be added to Package Patcher's import queue. For example, keep only the avatar version or optional add-on you need from a bundle. Add passes the selection to the main window, where you review the queue and start the import.",
            overviewKo: "압축 파일 안의 unitypackage 중 Package Patcher의 가져오기 대기 목록에 넣을 항목을 고릅니다. 예를 들어 묶음 상품에서 필요한 아바타 버전이나 추가 구성만 선택할 수 있습니다. 추가하면 기본 창으로 선택을 넘기며, 그 창에서 대기 목록을 확인하고 실제 가져오기를 실행합니다.",
            overviewJa: "圧縮ファイル内のunitypackageから、Package Patcherの読み込み待機一覧へ追加するものを選びます。例えばセット商品の中から必要なアバターの版や追加データだけを選べます。追加すると選択をメインウィンドウへ渡し、そこで待機一覧を確認して実際の読み込みを開始します。");
    }

    private void OnDisable() => guidedTutorial?.Suspend();
}
