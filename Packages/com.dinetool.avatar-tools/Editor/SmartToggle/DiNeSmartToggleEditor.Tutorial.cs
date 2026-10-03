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
                DiNeTutorialStep.Required("avatar", "Place this object inside your avatar.", "이 오브젝트를 아바타 안에 배치하세요.", "このオブジェクトをアバター内に配置してください。", HasTutorialAvatar),
                DiNeTutorialStep.Required("name", "Enter the name shown in the menu.", "메뉴에 표시할 이름을 입력하세요.", "メニューに表示する名前を入力してください。", () => target is DiNeSmartToggle item && !string.IsNullOrWhiteSpace(item.DisplayName)),
                DiNeTutorialStep.Required("parameter", "Enter a Bool parameter name.", "Bool 파라미터 이름을 입력하세요.", "Boolパラメーター名を入力してください。", () => target is DiNeSmartToggle item && !string.IsNullOrWhiteSpace(item.ParameterName)),
                DiNeTutorialStep.Optional("default", "Choose whether this object starts on.", "처음부터 켜 둘지 선택하세요.", "初期状態でONにするか選んでください。"),
                DiNeTutorialStep.Optional("saved", "Enable this to remember the last value.", "마지막 값을 기억하려면 켜세요.", "最後の値を記憶する場合はONにしてください。"),
                DiNeTutorialStep.Optional("menu", "Choose the menu that receives this button.", "버튼을 넣을 메뉴를 선택하세요.", "ボタンを追加するメニューを選んでください。"),
                DiNeTutorialStep.Required("preview-start", "Press Preview.", "미리보기를 누르세요.", "プレビューを押してください。", () => DiNeTogglePreview.IsActive(this, 0), prerequisite: false),
                DiNeTutorialStep.Optional("preview-state", "Press Preview, then use ON and OFF to check the object.", "미리보기를 켠 뒤 ON과 OFF로 오브젝트를 확인하세요.", "プレビューを開始してONとOFFでオブジェクトを確認してください。"),
                DiNeTutorialStep.Required("preview-stop", "Press End Preview.", "미리보기 종료를 누르세요.", "プレビュー終了を押してください。", () => !DiNeTogglePreview.IsActive(this, 0), prerequisite: false),
                DiNeTutorialStep.Optional("icon", "Choose an icon for the menu button.", "메뉴 버튼에 사용할 아이콘을 선택하세요.", "メニューボタンのアイコンを選んでください。"),
                DiNeTutorialStep.Optional("icon-edit", "Press Edit Icon to adjust the image.", "이미지를 조정하려면 아이콘 편집을 누르세요.", "画像を調整する場合はアイコン編集を押してください。"),
                DiNeTutorialStep.Optional("icon-generate", "Press Generate / Reuse for an automatic icon.", "자동 아이콘이 필요하면 자동 생성 / 재사용을 누르세요.", "自動アイコンが必要な場合は自動生成 / 再利用を押してください。"),
                DiNeTutorialStep.Optional("icon-regenerate", "Press Regenerate to replace the generated icon.", "생성된 아이콘을 바꾸려면 재생성을 누르세요.", "生成済みアイコンを変更する場合は再生成を押してください。"),
                DiNeTutorialStep.Optional("automatic", "Upload or enter Play Mode when you are ready.", "준비가 되면 업로드하거나 Play Mode에 들어가세요.", "準備ができたらアップロードするかPlay Modeに入ってください。")
            };
        }
        tutorial.Configure("all", "Smart Toggle", "스마트 토글", "Smart Toggle", tutorialSteps, onStop: () => DiNeTogglePreview.ClearForOwner(this));
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
