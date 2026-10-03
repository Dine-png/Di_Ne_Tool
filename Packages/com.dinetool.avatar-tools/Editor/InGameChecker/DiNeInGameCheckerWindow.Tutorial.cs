using UnityEditor;
using UnityEngine;

namespace DiNeTool.InGameChecker
{
    public partial class DiNeInGameCheckerWindow
    {
        private DiNeGuidedTutorial guidedTutorial;
        private Rect tutorialSetupAnchor;

        private void OnGUI()
        {
            ConfigureTutorial();
            guidedTutorial.BeginFrame();
            try { DrawToolGUI(); guidedTutorial.Validate(); }
            finally { guidedTutorial.EndFrame(); }
        }

        private void ConfigureTutorial()
        {
            if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "InGameChecker");
            guidedTutorial.Configure("Checker", "In-Game Checker", "플레이 모드 확인", "プレイモード確認", new[]
            {
                DiNeTutorialStep.Required("PlayMode", "Enter Play Mode.", "플레이 모드를 시작하세요.", "プレイモードを開始してください。", () => EditorApplication.isPlaying),
                DiNeTutorialStep.Required("Avatar", "Press Select beside the avatar to check.", "확인할 아바타의 선택 버튼을 누르세요.", "確認するアバターの選択ボタンを押してください。", TutorialModuleIsValid),
                DiNeTutorialStep.Optional("Unlink", "Press Unlink to disconnect the avatar.", "연결을 끊으려면 해제를 누르세요.", "接続を切るには解除を押してください。"),
                DiNeTutorialStep.Optional("Menu", "Choose an expression control in the radial menu.", "원형 메뉴에서 익스프레션 조작을 고르세요.", "円形メニューでエクスプレッション操作を選んでください。"),
                DiNeTutorialStep.Optional("Options", "Press the small Di Ne icon to switch menus.", "메뉴를 전환하려면 작은 Di Ne 아이콘을 누르세요.", "メニューを切り替えるには小さなDi Neアイコンを押してください。"),
                DiNeTutorialStep.Optional("LeftGesture", "Choose a gesture for the left hand.", "왼손 제스처를 고르세요.", "左手のジェスチャーを選んでください。"),
                DiNeTutorialStep.Optional("RightGesture", "Choose a gesture for the right hand.", "오른손 제스처를 고르세요.", "右手のジェスチャーを選んでください。"),
                DiNeTutorialStep.Optional("Parameters", "Expand Expression Parameters to adjust values.", "익스프레션 파라미터를 펼쳐 값을 조절하세요.", "エクスプレッションパラメーターを開いて値を調整してください。"),
                DiNeTutorialStep.Optional("Stats", "Expand performance information to inspect the avatar.", "성능 정보를 펼쳐 아바타 수치를 확인하세요.", "パフォーマンス情報を開いてアバターの数値を確認してください。"),
                DiNeTutorialStep.Optional("Refresh", "Press Refresh to recalculate performance information.", "새로고침을 눌러 성능 정보를 다시 계산하세요.", "更新を押してパフォーマンス情報を再計算してください。", () => _showStats = true)
            });
        }

        private bool TutorialModuleIsValid()
        {
            return EditorApplication.isPlaying && _module != null && _module.Active && _module.Avatar != null &&
                !EditorUtility.IsPersistent(_module.Avatar) && _module.Avatar.scene.IsValid() && _module.IsValid(out _);
        }
    }
}
