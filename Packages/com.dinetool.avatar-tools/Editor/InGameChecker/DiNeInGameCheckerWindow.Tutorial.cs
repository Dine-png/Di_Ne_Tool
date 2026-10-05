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
                DiNeTutorialStep.Required("PlayMode", "Play Mode runs the avatar's animation layers so expressions and gestures can be tested in Unity. Enter Play Mode.", "플레이 모드에서 아바타 애니메이션 레이어를 실행해 Unity 안에서 표정·제스처를 시험할 수 있습니다. 플레이 모드를 시작하세요.", "プレイモードでアバターのアニメーションレイヤーを動かし、Unity内で表情やジェスチャーを試せます。 プレイモードを開始してください。", () => EditorApplication.isPlaying),
                DiNeTutorialStep.Required("Avatar", "Selecting connects the checker to one scene avatar with a valid VRCAvatarDescriptor. Press Select beside the avatar to check.", "선택하면 유효한 VRCAvatarDescriptor가 있는 씬 아바타 하나를 검사기에 연결합니다. 확인할 아바타의 선택 버튼을 누르세요.", "選択すると有効なVRCAvatarDescriptorを持つシーン内のアバター1体をチェッカーに接続します。 確認するアバターの選択ボタンを押してください。", TutorialModuleIsValid),
                DiNeTutorialStep.Optional("Unlink", "Unlink stops controlling the currently connected avatar so you can switch to another. Press Unlink to disconnect the avatar.", "해제하면 현재 아바타의 제어를 끝내 다른 아바타로 전환할 수 있습니다. 연결을 끊으려면 해제를 누르세요.", "解除すると現在のアバターの操作を終了し、別のアバターへ切り替えられます。 接続を切るには解除を押してください。"),
                DiNeTutorialStep.Optional("Menu", "The radial menu uses the avatar's Expressions Menu to test controls such as outfit toggles and expression sliders. Choose an expression control in the radial menu.", "원형 메뉴는 아바타의 Expressions Menu를 사용해 의상 토글·표정 슬라이더 같은 조작을 시험합니다. 원형 메뉴에서 익스프레션 조작을 고르세요.", "円形メニューはアバターのExpressions Menuを使い、衣装切り替えや表情スライダーなどを試します。 円形メニューでエクスプレッション操作を選んでください。"),
                DiNeTutorialStep.Optional("Options", "The Di Ne options menu exposes extra test controls; the icon switches between it and the avatar's menu. Press the small Di Ne icon to switch menus.", "Di Ne 옵션 메뉴는 추가 시험 조작을 제공하며 아이콘으로 아바타 메뉴와 전환합니다. 메뉴를 전환하려면 작은 Di Ne 아이콘을 누르세요.", "Di Neオプションメニューは追加のテスト操作を提供し、アイコンでアバターのメニューと切り替えます。 メニューを切り替えるには小さなDi Neアイコンを押してください。"),
                DiNeTutorialStep.Optional("LeftGesture", "Changing the left-hand gesture tests animations and expressions driven by GestureLeft. Choose a gesture for the left hand.", "왼손 제스처 변경으로 GestureLeft에 연결된 애니메이션·표정을 시험합니다. 왼손 제스처를 고르세요.", "左手のジェスチャー変更でGestureLeftに連動するアニメーションや表情を試します。 左手のジェスチャーを選んでください。"),
                DiNeTutorialStep.Optional("RightGesture", "Changing the right-hand gesture tests GestureRight and how both hand gestures combine. Choose a gesture for the right hand.", "오른손 제스처 변경으로 GestureRight와 양손 제스처 조합의 동작을 시험합니다. 오른손 제스처를 고르세요.", "右手のジェスチャー変更でGestureRightと両手の組み合わせの動作を試します。 右手のジェスチャーを選んでください。"),
                DiNeTutorialStep.Optional("Parameters", "Direct parameter controls help test a toggle or numeric value even when its menu control is hard to reach. Expand Expression Parameters to adjust values.", "파라미터 직접 조작으로 메뉴에서 찾기 어려운 토글·숫자 값도 시험할 수 있습니다. 익스프레션 파라미터를 펼쳐 값을 조절하세요.", "パラメーターを直接操作すると、メニューから探しにくい切り替えや数値も試せます。 エクスプレッションパラメーターを開いて値を調整してください。"),
                DiNeTutorialStep.Optional("Stats", "Performance information shows mesh, material and texture counts plus memory and upload-size estimates for review. Expand performance information to inspect the avatar.", "성능 정보에서 메쉬·머티리얼·텍스처 개수와 메모리·업로드 크기 추정치를 검토할 수 있습니다. 성능 정보를 펼쳐 아바타 수치를 확인하세요.", "パフォーマンス情報ではメッシュ・マテリアル・テクスチャの数と、メモリ・アップロードサイズの推定値を確認できます。 パフォーマンス情報を開いてアバターの数値を確認してください。"),
                DiNeTutorialStep.Optional("Refresh", "Recalculation updates the displayed counts and estimates after the avatar's contents have changed. Press Refresh to recalculate performance information.", "아바타 구성이 바뀐 뒤 다시 계산하면 표시되는 개수·추정치가 갱신됩니다. 새로고침을 눌러 성능 정보를 다시 계산하세요.", "アバターの構成を変えた後に再計算すると、表示する件数や推定値を更新できます。 更新を押してパフォーマンス情報を再計算してください。", () => _showStats = true)
            },
                overviewEn: "Test a VRChat avatar's expressions and hand gestures in Unity Play Mode before upload. Connect a scene avatar, operate its radial Expressions Menu or parameter controls, and check whether outfit toggles and expressions respond. Performance information also shows counts and memory/upload-size estimates; it is a local test and the estimates may differ from actual upload results.",
                overviewKo: "업로드 전에 Unity 플레이 모드에서 VRChat 아바타의 표정과 손 제스처를 시험합니다. 씬 아바타를 연결해 원형 Expressions Menu나 파라미터를 조작하고 의상 토글·표정이 반응하는지 확인할 수 있습니다. 성능 정보에서는 개수와 메모리·업로드 크기 추정치도 보여주며, 로컬 시험과 추정치는 실제 업로드 결과와 다를 수 있습니다.",
                overviewJa: "アップロード前にUnityのプレイモードでVRChatアバターの表情や手のジェスチャーを試します。シーン内のアバターを接続し、円形Expressions Menuやパラメーターを操作して衣装切り替え・表情の反応を確認できます。パフォーマンス情報では件数とメモリ・アップロードサイズの推定値も表示します。ローカルテストと推定値は実際のアップロード結果と異なる場合があります。");
        }

        private bool TutorialModuleIsValid()
        {
            return EditorApplication.isPlaying && _module != null && _module.Active && _module.Avatar != null &&
                !EditorUtility.IsPersistent(_module.Avatar) && _module.Avatar.scene.IsValid() && _module.IsValid(out _);
        }
    }
}
