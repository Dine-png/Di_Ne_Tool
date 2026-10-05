#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public sealed partial class DiNeSmartToggleEditor
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[] tutorialSteps;

    private void EnsureTutorial()
    {
        if (tutorial == null) tutorial = new DiNeGuidedTutorial(this, "smart-toggle");
        if (tutorialSteps == null)
        {
            tutorialSteps = new[]
            {
                DiNeTutorialStep.Required("avatar", "Smart Toggle switches the object carrying this component, such as glasses or a hat. Place that object under your scene avatar's VRCAvatarDescriptor.", "Smart Toggle은 이 컴포넌트가 붙은 안경·모자 같은 오브젝트를 켜고 끕니다. 대상 오브젝트를 씬 아바타의 VRCAvatarDescriptor 아래에 배치하세요.", "Smart Toggleはこのコンポーネントを付けた眼鏡や帽子などを切り替えます。対象をシーンアバターのVRCAvatarDescriptorの下に配置してください。", HasTutorialAvatar),
                DiNeTutorialStep.Required("name", "This is the label you will see on the VRChat Expressions button. Enter a recognizable name such as Glasses.", "VRChat Expressions 메뉴에서 보일 버튼 이름입니다. 안경처럼 무엇을 켜는지 알 수 있는 이름을 입력하세요.", "VRChatのExpressionsボタンに表示される名前です。眼鏡など、切り替える対象が分かる名前を入力してください。", () => target is DiNeSmartToggle item && !string.IsNullOrWhiteSpace(item.DisplayName)),
                DiNeTutorialStep.Required("parameter", "The Bool parameter stores this switch's ON/OFF value and connects the menu to its animation. Use the prefilled name or enter your own; conflicting names receive a numeric suffix.", "Bool 파라미터는 ON/OFF 값을 저장하고 메뉴와 애니메이션을 연결합니다. 자동 입력된 이름을 쓰거나 직접 입력하세요. 다른 설정과 겹치면 숫자가 붙습니다.", "BoolパラメーターはON/OFFを保持し、メニューとアニメーションをつなぎます。自動入力の名前を使うか入力してください。重複する名前には番号が付きます。", () => target is DiNeSmartToggle item && !string.IsNullOrWhiteSpace(item.ParameterName)),
                DiNeTutorialStep.Optional("default", "Default ON sets the generated toggle's initial state. Enable it if the accessory should start visible, or disable it if the user should turn it on from the menu.", "기본 ON은 생성되는 토글의 초기 상태를 정합니다. 처음부터 액세서리가 보이게 하려면 켜고, 메뉴에서 직접 켜게 하려면 끄세요.", "初期ONは生成されるトグルの初期状態を決めます。アクセサリーを最初から表示する場合はON、メニューから表示する場合はOFFにしてください。"),
                DiNeTutorialStep.Optional("saved", "Save Value lets VRChat remember the last toggle value when the avatar is loaded again. Enable it to keep your accessory choice between uses.", "값 저장을 켜면 아바타를 다시 불러왔을 때 VRChat이 마지막 토글 값을 기억합니다. 이전에 선택한 액세서리 상태를 유지하려면 켜세요.", "値を保存をONにすると、アバターを再読み込みしたときにVRChatが最後の値を記憶します。前回選んだアクセサリーの状態を維持する場合に使います。"),
                DiNeTutorialStep.Optional("menu", "Menu Placement decides where the new Expressions button appears. Choose the root or a submenu, for example an Accessories menu.", "메뉴 위치는 새 Expressions 버튼이 들어갈 곳을 정합니다. 최상위 메뉴나 액세서리 같은 하위 메뉴를 선택하세요.", "メニュー配置で新しいExpressionsボタンの追加先を決めます。ルートやアクセサリーなどのサブメニューを選んでください。"),
                DiNeTutorialStep.Required("preview-start", "Preview lets you check visibility in the scene before building. Press Preview to start from the Default ON setting.", "미리보기로 빌드 전에 씬에서 표시 상태를 확인할 수 있습니다. 미리보기를 누르면 기본 ON 설정으로 시작합니다.", "プレビューでビルド前にシーンの表示状態を確認できます。プレビューを押すと初期ON設定で開始します。", () => DiNeTogglePreview.IsActive(this, 0), prerequisite: false),
                DiNeTutorialStep.Optional("preview-state", "ON and OFF temporarily switch this object while previewing. Try both to check the result; these controls keep the Default ON setting unchanged.", "미리보기의 ON·OFF는 이 오브젝트를 임시로 켜고 끕니다. 양쪽 상태를 확인해 보세요. 기본 ON 설정은 그대로 유지됩니다.", "プレビューのON・OFFでこのオブジェクトを一時的に切り替えます。両方を確認してください。初期ON設定は維持されます。"),
                DiNeTutorialStep.Required("preview-stop", "End Preview restores the object's active state from before previewing. Press it when you have checked both states.", "미리보기 종료는 오브젝트를 미리보기 전의 활성 상태로 복원합니다. 두 상태를 확인한 뒤 눌러 주세요.", "プレビュー終了で開始前の有効状態に戻します。両方の状態を確認したら押してください。", () => !DiNeTogglePreview.IsActive(this, 0), prerequisite: false),
                DiNeTutorialStep.Optional("icon", "The icon helps you recognize the toggle in the Expressions menu. Assign an image that represents this object, or use automatic capture below.", "아이콘은 Expressions 메뉴에서 토글을 알아보기 쉽게 해줍니다. 대상을 나타내는 이미지를 넣거나 아래의 자동 촬영 기능을 쓰세요.", "アイコンはExpressionsメニューでトグルを見つけやすくします。対象を表す画像を指定するか、下の自動撮影を使ってください。"),
                DiNeTutorialStep.Optional("icon-edit", "Edit Icon opens the capture editor for this object. Adjust the view, framing and outline to make a readable menu image.", "아이콘 편집은 이 오브젝트의 촬영 편집기를 엽니다. 시점·구도·외곽선을 조절해 메뉴에서 알아보기 쉬운 이미지를 만드세요.", "アイコン編集でこのオブジェクトの撮影エディターを開きます。視点・構図・輪郭を調整し、メニューで見やすい画像を作ってください。"),
                DiNeTutorialStep.Optional("icon-generate", "Generate / Reuse supplies a captured icon when you need one. It keeps an existing icon so you can reuse the current image.", "자동 생성 / 재사용은 필요한 경우 대상을 촬영해 아이콘을 만듭니다. 기존 아이콘이 있으면 현재 이미지를 재사용합니다.", "自動生成 / 再利用は必要なときに対象を撮影してアイコンを作ります。既存のアイコンがある場合はその画像を再利用します。"),
                DiNeTutorialStep.Optional("icon-regenerate", "Regenerate captures a new icon and replaces the current one. Use it after changing the object's appearance or capture settings.", "재생성은 새로 촬영한 아이콘으로 현재 이미지를 교체합니다. 오브젝트의 모습이나 촬영 설정을 바꾼 뒤 사용하세요.", "再生成は撮り直したアイコンで現在の画像を置き換えます。対象の見た目や撮影設定を変更した後に使ってください。"),
                DiNeTutorialStep.Optional("automatic", "Upload or enter Play Mode to generate the menu button, Bool parameter and ON/OFF animation automatically. The configuration is applied to the temporary avatar used for that session.", "업로드하거나 Play Mode에 들어가면 메뉴 버튼·Bool 파라미터·ON/OFF 애니메이션이 자동으로 생성됩니다. 설정은 해당 세션에서 사용하는 임시 아바타에 적용됩니다.", "アップロードやPlay Mode開始時にメニューボタン・Boolパラメーター・ON/OFFアニメーションを自動生成します。設定はそのセッション用の一時アバターに適用されます。")
            };
        }
        tutorial.Configure("all", "Smart Toggle", "스마트 토글", "Smart Toggle", tutorialSteps, onStop: () => DiNeTogglePreview.ClearForOwner(this),
            overviewEn: "Smart Toggle creates a VRChat Expressions ON/OFF button for the object carrying this component, such as glasses, a hat or an accessory. Set its starting state, saved value, menu location and icon, then check visibility in the scene. The required parameter and animation are generated automatically on upload or in Play Mode.",
            overviewKo: "Smart Toggle은 이 컴포넌트가 붙은 안경·모자·액세서리를 켜고 끄는 VRChat Expressions 버튼을 만듭니다. 초기 상태, 값 저장, 메뉴 위치와 아이콘을 설정하고 씬에서 표시 상태를 미리 확인할 수 있습니다. 필요한 파라미터와 애니메이션은 업로드나 Play Mode에서 자동으로 생성됩니다.",
            overviewJa: "Smart Toggleはこのコンポーネントを付けた眼鏡・帽子・アクセサリーを切り替えるVRChat Expressionsボタンを作ります。初期状態、値の保存、メニュー配置、アイコンを設定し、シーンで表示を確認できます。必要なパラメーターとアニメーションはアップロードやPlay Mode開始時に自動生成されます。");
    }

    private bool HasTutorialAvatar()
    {
        var item = target as DiNeSmartToggle;
        return item != null && !EditorUtility.IsPersistent(item) && item.gameObject.scene.IsValid()
            && item.GetComponentInParent<VRCAvatarDescriptor>(true) != null;
    }

    private void TutorialProperty(SerializedProperty property, GUIContent label, string id)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, label);
        if (EditorGUI.EndChangeCheck()) tutorial.NotifyAction(id);
        tutorial.Draw(id, GUILayoutUtility.GetLastRect());
    }
}
#endif
