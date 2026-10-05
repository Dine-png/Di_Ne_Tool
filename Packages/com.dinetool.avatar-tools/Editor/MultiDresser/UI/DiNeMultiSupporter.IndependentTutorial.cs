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
            DiNeTutorialStep.Required("avatar", "The avatar root defines which objects and Expressions menu this group can use. Assign your scene avatar's root before adding targets.", "아바타 루트는 그룹이 사용할 오브젝트와 Expressions 메뉴의 범위를 정합니다. 대상을 넣기 전에 씬 아바타의 루트를 지정하세요.", "アバターのルートで、このグループが使う対象とExpressionsメニューの範囲を決めます。対象を追加する前にシーンアバターのルートを指定してください。", () => HasTutorialAvatar(target as DiNeMultiDresser)),
            DiNeTutorialStep.Required("create", "One independent toggle switches all of its targets together, separately from wardrobe choices. Click Add Independent Toggle to create a group such as Hat and Hair Accessories.", "독립 토글 하나는 등록한 대상을 함께 켜고 끄며 옷장 선택과 별도로 동작합니다. 독립 토글 추가를 눌러 모자와 머리 장식 같은 그룹을 만드세요.", "独立トグル1つで登録した対象をまとめて切り替え、衣装の選択とは別に使えます。独立トグルを追加を押し、帽子と髪飾りなどのグループを作ってください。", () => ToggleTutorialData() != null),
            DiNeTutorialStep.Optional("name", "This name appears on the group's Expressions button. Use a label that tells you which objects it switches, such as Head Accessories.", "이 이름은 그룹의 Expressions 버튼에 표시됩니다. 머리 장식처럼 어떤 오브젝트를 켜는지 알 수 있는 이름을 입력하세요.", "この名前はグループのExpressionsボタンに表示されます。頭のアクセサリーなど、対象が分かる名前を入力してください。"),
            DiNeTutorialStep.Optional("parameter", "One Bool parameter connects the button to every object in this group. Use the prefilled name or enter a unique name; conflicts receive a numeric suffix.", "Bool 파라미터 하나가 버튼과 그룹의 모든 대상을 연결합니다. 자동 입력된 이름을 쓰거나 고유한 이름을 입력하세요. 중복 이름에는 숫자가 붙습니다.", "1つのBoolパラメーターでボタンとグループ内の全対象をつなぎます。自動入力の名前を使うか固有の名前を入力してください。重複する名前には番号が付きます。"),
            DiNeTutorialStep.Required("targets", "These objects will all turn ON or OFF together. Drag accessories from the avatar into this area; multiple objects can be added at once.", "여기에 등록한 오브젝트는 모두 함께 켜지고 꺼집니다. 아바타의 액세서리를 드래그하세요. 여러 오브젝트를 한 번에 넣을 수도 있습니다.", "ここに登録した対象はすべて一緒にON/OFFになります。アバターのアクセサリーをドラッグしてください。複数の対象を一度に追加できます。", HasToggleTutorialTargets),
            DiNeTutorialStep.Required("preview", "Preview checks the entire group's visibility in the scene before building. Click Preview to start with its Default ON setting.", "미리보기로 빌드 전에 그룹 전체의 표시 상태를 씬에서 확인할 수 있습니다. 미리보기를 누르면 기본 ON 설정으로 시작합니다.", "プレビューでビルド前にグループ全体の表示をシーンで確認できます。プレビューを押すと初期ON設定で開始します。", () => DiNeTogglePreview.IsActive(this, 0), false),
            DiNeTutorialStep.Optional("state", "ON and OFF temporarily switch every target together. Check that all intended accessories respond; the group's saved settings remain unchanged.", "ON·OFF는 등록한 대상을 모두 임시로 전환합니다. 의도한 액세서리가 함께 바뀌는지 확인하세요. 그룹에 저장한 설정은 유지됩니다.", "ON・OFFで登録した全対象を一時的に切り替えます。必要なアクセサリーが一緒に変わることを確認してください。グループの保存済み設定は維持されます。"),
            DiNeTutorialStep.Required("restore", "Stop restores each target's active state from before previewing. Click it after checking ON and OFF to return the scene to its original state.", "종료는 각 오브젝트를 미리보기 전의 활성 상태로 복원합니다. ON·OFF를 확인한 뒤 눌러 씬을 원래 상태로 되돌리세요.", "終了で各対象をプレビュー開始前の有効状態に戻します。ON・OFFの確認後に押し、シーンを元の状態に戻してください。", () => !DiNeTogglePreview.IsActive(this, 0), false),
            DiNeTutorialStep.Optional("defaults", "Default ON sets the group's starting state, and Save Value lets VRChat remember its last value when reloading the avatar. Choose whether these accessories start visible and whether your choice should persist.", "기본 ON은 그룹의 초기 상태를 정하고, 값 저장은 아바타를 다시 불러올 때 VRChat이 마지막 값을 기억하게 합니다. 처음부터 보이게 할지, 선택을 유지할지 정하세요.", "初期ONはグループの初期状態を決め、値を保存はアバター再読み込み時にVRChatが最後の値を記憶する設定です。最初から表示するか、選択を維持するか決めてください。"),
            DiNeTutorialStep.Optional("menu", "Menu Placement decides which Expressions menu contains this group's button. Choose a root or submenu location that keeps related switches easy to find.", "메뉴 위치는 그룹 버튼을 넣을 Expressions 메뉴를 정합니다. 관련 토글을 찾기 쉽도록 최상위 메뉴나 하위 메뉴를 선택하세요.", "メニュー配置でグループのボタンを追加するExpressionsメニューを決めます。関連するスイッチが見つけやすいルートやサブメニューを選んでください。"),
            DiNeTutorialStep.Optional("icon", "The icon represents the whole group in the menu. Drop in an image that makes these accessories easy to recognize.", "아이콘은 메뉴에서 그룹 전체를 나타냅니다. 어떤 액세서리 그룹인지 알아보기 쉬운 이미지를 넣으세요.", "アイコンはメニューでグループ全体を表します。アクセサリーのグループが分かる画像を指定してください。"),
            DiNeTutorialStep.Optional("delete", "The × beside a toggle removes that group from the configuration, leaving its target objects in the avatar. Remaining groups generate their buttons and animations automatically on upload or in Play Mode.", "토글 옆의 ×는 설정에서 그룹을 삭제하며 대상 오브젝트는 아바타에 남습니다. 남아 있는 그룹의 버튼과 애니메이션은 업로드나 Play Mode에서 자동 생성됩니다.", "トグル横の×で設定からグループを削除します。対象オブジェクトはアバターに残ります。残ったグループのボタンとアニメーションはアップロードやPlay Mode開始時に自動生成されます。")
        };
        independentTutorial.Configure("independent", "Independent Toggles", "독립 토글", "独立トグル", independentTutorialSteps, onStop: () => DiNeTogglePreview.ClearForOwner(this),
            overviewEn: "Independent Toggles add VRChat Expressions switches that work separately from wardrobe choices. Put several objects, such as a hat and matching hair accessories, into one group to turn them ON/OFF together. Each group has its own starting state, saved value, menu location and icon; its button and animations are generated automatically on upload or in Play Mode.",
            overviewKo: "독립 토글은 옷장 선택과 별도로 켜고 끌 수 있는 VRChat Expressions 스위치를 만듭니다. 모자와 어울리는 머리 장식처럼 여러 오브젝트를 한 그룹에 넣어 함께 ON/OFF할 수 있습니다. 그룹마다 초기 상태, 값 저장, 메뉴 위치와 아이콘을 설정하며 버튼과 애니메이션은 업로드나 Play Mode에서 자동 생성됩니다.",
            overviewJa: "独立トグルは衣装選択とは別に切り替えられるVRChat Expressionsスイッチを作ります。帽子とそれに合う髪飾りなど、複数の対象を1つのグループに入れてまとめてON/OFFにできます。グループごとに初期状態、値の保存、メニュー配置、アイコンを設定し、ボタンとアニメーションはアップロードやPlay Mode開始時に自動生成されます。");
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
