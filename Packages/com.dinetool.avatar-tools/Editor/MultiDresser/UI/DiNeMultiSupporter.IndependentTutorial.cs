#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class DiNeMultiSupporter
{
    private DiNeGuidedTutorial independentTutorial;
    private DiNeTutorialStep[] independentTutorialSteps;

    private void ConfigureIndependentTutorial()
    {
        if (independentTutorial == null) independentTutorial = new DiNeGuidedTutorial(this, "MultiDresser");
        if (independentTutorialSteps == null) independentTutorialSteps = new[]
        {
            DiNeTutorialStep.Required("avatar", "Assign the avatar root.", "아바타 루트를 넣으세요.", "アバターのルートを指定してください。", () => HasTutorialAvatar(target as DiNeMultiDresser)),
            DiNeTutorialStep.Required("create", "Click Add Independent Toggle.", "독립 토글 추가를 누르세요.", "独立トグルを追加を押してください。", () => ToggleTutorialData() != null),
            DiNeTutorialStep.Optional("name", "Enter the button name.", "버튼 이름을 입력하세요.", "ボタン名を入力してください。"),
            DiNeTutorialStep.Optional("parameter", "Enter a unique Bool name.", "겹치지 않는 Bool 이름을 입력하세요.", "重複しないBool名を入力してください。"),
            DiNeTutorialStep.Required("targets", "Drag avatar objects into this list.", "아바타 안의 오브젝트를 여기에 넣으세요.", "アバター内の対象をここにドラッグしてください。", HasToggleTutorialTargets),
            DiNeTutorialStep.Required("preview", "Click Preview.", "미리보기를 누르세요.", "プレビューを押してください。", () => DiNeTogglePreview.IsActive(this, 0), false),
            DiNeTutorialStep.Optional("state", "Try the preview ON and OFF controls.", "미리보기의 ON·OFF를 눌러보세요.", "プレビューのON・OFFを試してください。"),
            DiNeTutorialStep.Required("restore", "Click Stop to restore the preview.", "종료를 눌러 미리보기를 되돌리세요.", "終了を押してプレビューを元に戻してください。", () => !DiNeTogglePreview.IsActive(this, 0), false),
            DiNeTutorialStep.Optional("defaults", "Choose Default ON and Save.", "기본 ON과 값 저장을 설정하세요.", "初期ONと値を保存を設定してください。"),
            DiNeTutorialStep.Optional("menu", "Choose the menu location.", "메뉴 위치를 선택하세요.", "メニューの位置を選んでください。"),
            DiNeTutorialStep.Optional("icon", "Drop an image into the icon slot.", "아이콘 칸에 이미지를 넣으세요.", "アイコン欄に画像を指定してください。"),
            DiNeTutorialStep.Optional("delete", "Use × beside a toggle to remove it.", "토글 옆의 ×로 삭제할 수 있어요.", "トグル横の×で削除できます。")
        };
        independentTutorial.Configure("independent", "Independent Toggles", "독립 토글", "独立トグル", independentTutorialSteps, onStop: () => DiNeTogglePreview.ClearForOwner(this));
    }

    private DiNeMultiDresser.IndependentToggle ToggleTutorialData()
    {
        var gen = target as DiNeMultiDresser;
        return gen != null && gen.independentToggles != null && gen.independentToggles.Count > 0 ? gen.independentToggles[0] : null;
    }

    private bool HasToggleTutorialTargets()
    {
        var gen = target as DiNeMultiDresser;
        var toggle = ToggleTutorialData();
        if (toggle == null || toggle.targets == null) return false;
        foreach (var obj in toggle.targets)
            if (DiNeIndependentToggleEditing.CanAdd(gen, toggle, obj)) return true;
        return false;
    }
}
#endif
