using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class ArmatureScalerEditor
{
    private DiNeGuidedTutorial _tutorial;
    private Dictionary<string, DiNeTutorialStep[]> _tutorialCourses;

    private void BeginTutorialFrame()
    {
        if (_tutorial == null) _tutorial = new DiNeGuidedTutorial(this, "avi-editor");
        if (_tutorialCourses == null) CreateTutorialCourses();
        string course = currentMode == EditorMode.Armature ? "armature" : currentMode == EditorMode.Extra ? "extra" : _skeSubMode == 0 ? "create" : _skeModifySubMode == 0 ? "scale" : "replace";
        string[] names = course == "armature" ? new[] { "Armature", "아마추어", "アーマチュア" } : course == "extra" ? new[] { "PhysBone", "PhysBone", "PhysBone" } : course == "create" ? new[] { "Create Shape Key", "쉐이프키 만들기", "シェイプキー作成" } : course == "scale" ? new[] { "Scale Shape Key", "쉐이프키 배율", "シェイプキー倍率" } : new[] { "Replace Shape Key", "쉐이프키 교체", "シェイプキー置換" };
        _tutorial.Configure(course, names[0], names[1], names[2], _tutorialCourses[course]);
        _tutorial.BeginFrame();
    }

    private void CreateTutorialCourses()
    {
        var avatar = DiNeTutorialStep.Required("avatar", "Assign the avatar root.", "아바타 루트를 지정하세요.", "アバタールートを指定してください。", () => targetAvatarRoot != null);
        var refresh = DiNeTutorialStep.Optional("refresh", "Use Refresh after changing the avatar's setup.", "아바타 구성을 바꿨다면 새로고침을 누르세요.", "アバターの構成を変更したら更新を押してください。");
        var preview = new[]
        {
            DiNeTutorialStep.Optional("head-rotate", "Drag the preview to rotate the view.", "미리보기를 드래그해 시점을 회전하세요.", "プレビューをドラッグして視点を回転してください。"),
            DiNeTutorialStep.Optional("head-zoom", "Scroll in the preview to zoom.", "미리보기에서 마우스 휠로 확대·축소하세요.", "プレビュー内でホイールを回してズームしてください。"),
            DiNeTutorialStep.Optional("head-pan", "Alt-drag the preview to move the view.", "Alt를 누르고 미리보기를 드래그해 시점을 이동하세요.", "Altを押しながらプレビューをドラッグして視点を移動してください。"),
            DiNeTutorialStep.Optional("head-reset", "Use the small reset button to reset the view.", "작은 초기화 버튼으로 시점을 초기화하세요.", "小さいリセットボタンで視点をリセットしてください。"),
        };
        var mesh = new[]
        {
            DiNeTutorialStep.Optional("avatar", "Assign the avatar root, or assign its mesh below.", "아바타 루트를 지정하거나 아래에 메쉬를 직접 지정하세요.", "アバタールートを指定するか、下にメッシュを直接指定してください。"),
            refresh,
            DiNeTutorialStep.Required("mesh", "Assign a mesh with shape keys.", "쉐이프키가 있는 메쉬를 지정하세요.", "シェイプキーのあるメッシュを指定してください。", () => _skeSmr != null && _skeSmr.sharedMesh != null && _skeSmr.sharedMesh.blendShapeCount > 0),
        }.Concat(preview).Concat(new[] { DiNeTutorialStep.Optional("shape-search", "Type a shape key name to filter the list.", "쉐이프키 이름을 입력해 목록을 검색하세요.", "シェイプキー名を入力して一覧を検索してください。") }).ToArray();
        _tutorialCourses = new Dictionary<string, DiNeTutorialStep[]>
        {
            ["armature"] = new[]
            {
                DiNeTutorialStep.Required("avatar", "Assign an avatar with mapped humanoid bones.", "휴머노이드 본이 있는 아바타를 지정하세요.", "ヒューマノイドボーンのあるアバターを指定してください。", () => targetAvatarRoot != null && boneMapping != null && boneMapping.Values.Any(b => b != null)),
                refresh,
                DiNeTutorialStep.Optional("direct-mode", "Choose Direct Bone Editing.", "직접 뼈 조정을 선택하세요.", "ボーン直接調整を選んでください。", () => { armatureEditMode = ArmatureEditMode.DirectTransform; LoadCurrentValues(); }),
                DiNeTutorialStep.Required("bone", "Click a bone in the body map to edit it.", "신체 그림에서 조절할 본을 클릭하세요.", "身体図で調整するボーンをクリックしてください。", () => selectedPart != HumanoidBodyPart.None && TryGetLiveBoneTransform(GetBoneType(selectedPart), out Transform bone) && bone != null),
                DiNeTutorialStep.Optional("scale-uniform", "Enter a uniform scale for the selected bone.", "선택한 본의 전체 크기를 입력하세요.", "選択ボーンの全体サイズを入力してください。"),
                DiNeTutorialStep.Optional("scale-vector", "Set the X, Y and Z scale separately here.", "X·Y·Z 크기를 각각 조절하세요.", "X・Y・Zのサイズを個別に調整してください。"),
                DiNeTutorialStep.Optional("position", "Set the selected bone's position.", "선택한 본의 위치를 조절하세요.", "選択ボーンの位置を調整してください。"),
                DiNeTutorialStep.Optional("rotation", "For a rotatable bone, set its rotation here.", "회전할 수 있는 본은 여기서 회전을 조절하세요.", "回転できるボーンはここで回転を調整してください。"),
                DiNeTutorialStep.Optional("reset-scales", "Use Reset Scales when you want to reset the direct bone sizes.", "직접 조정한 본 크기를 초기화하려면 크기 초기화를 누르세요.", "直接調整したボーンサイズをリセットするにはサイズリセットを押してください。"),
                DiNeTutorialStep.Optional("ma-mode", "Choose MA Scale Adjustment.", "MA 비율 조정을 선택하세요.", "MA比率調整を選んでください。", () => armatureEditMode = ArmatureEditMode.ModularAvatarScale),
                DiNeTutorialStep.Optional("ma-add", "Select a bone, then add its MA Scale Adjuster if it has none.", "본을 선택하고 Adjuster가 없으면 추가하세요.", "ボーンを選び、Adjusterがなければ追加してください。"),
                DiNeTutorialStep.Optional("ma-scale-uniform", "With an Adjuster added, enter its uniform scale.", "Adjuster를 추가한 뒤 전체 비율을 입력하세요.", "Adjusterを追加したら全体比率を入力してください。"),
                DiNeTutorialStep.Optional("ma-scale-vector", "Set the Adjuster's X, Y and Z scales separately.", "Adjuster의 X·Y·Z 비율을 각각 조절하세요.", "AdjusterのX・Y・Z比率を個別に調整してください。"),
                DiNeTutorialStep.Optional("ma-children", "Choose whether to adjust child positions with the scale.", "비율 변경에 맞춰 자식 위치도 조정할지 선택하세요.", "比率変更に合わせて子の位置も調整するか選んでください。"),
                DiNeTutorialStep.Optional("ma-remove", "Use Remove only when you want to remove the selected bone's Adjuster.", "선택한 본의 Adjuster를 없애려면 제거를 누르세요.", "選択ボーンのAdjusterを削除する場合は削除を押してください。"),
                DiNeTutorialStep.Optional("preset", "Select an armature preset.", "아마추어 프리셋을 선택하세요.", "アーマチュアプリセットを選んでください。"),
                DiNeTutorialStep.Optional("preset-load", "Load Entire Preset applies all saved adjustments.", "프리셋 전체 불러오기로 저장된 조정값을 모두 적용하세요.", "プリセット全体を読み込むで保存した調整値を全て適用できます。"),
                DiNeTutorialStep.Optional("preset-scale", "Use Bone Scale Only to load just the direct bone scales.", "기본 크기만으로 직접 본 크기만 불러오세요.", "ボーンサイズのみで直接のボーンサイズだけを読み込めます。"),
                DiNeTutorialStep.Optional("preset-rotation", "Use Rotation Only to load only rotations.", "회전만을 눌러 회전값만 불러오세요.", "回転のみで回転値だけを読み込めます。"),
                DiNeTutorialStep.Optional("preset-position", "Use Position Only to load only positions.", "위치만을 눌러 위치값만 불러오세요.", "位置のみで位置値だけを読み込めます。"),
                DiNeTutorialStep.Optional("preset-ma", "Use MA Scale Adjuster Only to load only the MA adjustments.", "MA Scale Adjuster만 불러오기로 MA 조정값만 적용하세요.", "MA Scale Adjusterのみ読み込むでMA調整値だけを適用できます。"),
                DiNeTutorialStep.Optional("preset-save", "Save Both as New Preset saves the current direct and MA adjustments.", "두 조정값을 새 프리셋으로 저장을 눌러 현재 값을 저장하세요.", "両方の調整値を新規プリセットとして保存で現在値を保存できます。"),
                DiNeTutorialStep.Optional("preset-delete", "Use Delete only when you want to remove the selected preset.", "선택한 프리셋이 필요 없으면 삭제를 누르세요.", "選択プリセットが不要な場合は削除を押してください。"),
            },
            ["create"] = mesh.Concat(new[]
            {
                DiNeTutorialStep.Optional("mix-add", "Click Add beside each shape key to include in the mix.", "혼합할 쉐이프키 옆의 추가를 누르세요.", "混ぜるシェイプキーの横にある追加を押してください。"),
                DiNeTutorialStep.Optional("mix-weight", "Adjust each mixed shape key's weight.", "혼합 목록에서 각 쉐이프키의 비율을 조절하세요.", "混合一覧で各シェイプキーの比率を調整してください。"),
                DiNeTutorialStep.Optional("mix-remove", "Use − to remove a shape key from the mix.", "−를 눌러 혼합 목록에서 쉐이프키를 빼세요.", "−で混合一覧からシェイプキーを外してください。"),
                DiNeTutorialStep.Optional("mix-preview", "Use Update Preview to check the mix.", "미리보기 갱신으로 혼합 결과를 확인하세요.", "プレビュー更新で混合結果を確認してください。"),
                DiNeTutorialStep.Optional("mix-restore", "Use Restore to clear the mix preview.", "원본 복원으로 혼합 미리보기를 초기화하세요.", "元に戻すで混合プレビューをリセットしてください。"),
                DiNeTutorialStep.Optional("shape-name", "Enter the new shape key's name.", "새 쉐이프키 이름을 입력하세요.", "新しいシェイプキー名を入力してください。"),
                DiNeTutorialStep.Optional("shape-create", "Create Shape Key saves the mix as a new shape key.", "쉐이프키 생성으로 혼합 결과를 새 쉐이프키로 저장하세요.", "シェイプキー生成で混合結果を新しいシェイプキーとして保存できます。"),
            }).ToArray(),
            ["scale"] = mesh.Concat(new[]
            {
                DiNeTutorialStep.Optional("shape-select", "Click the shape key to adjust and preview it.", "조절할 쉐이프키를 클릭해 선택하고 미리보세요.", "調整するシェイプキーをクリックして選択・プレビューしてください。"),
                DiNeTutorialStep.Optional("shape-scale", "Set the new scale percentage.", "새 배율을 퍼센트로 조절하세요.", "新しい倍率をパーセントで調整してください。"),
                DiNeTutorialStep.Optional("shape-preview-weight", "Use Preview Weight to choose how strongly to preview the shape key.", "미리보기 강도로 쉐이프키를 얼마나 적용해 볼지 조절하세요.", "プレビュー強度でシェイプキーの表示量を調整してください。"),
                DiNeTutorialStep.Optional("shape-apply-scale", "Apply Scale saves the scale change to a mesh copy.", "배율 적용으로 변경한 배율을 메쉬 복사본에 저장하세요.", "倍率を適用で変更した倍率をメッシュのコピーに保存できます。"),
                DiNeTutorialStep.Optional("shape-reset-preview", "Restore Preview resets the preview values.", "미리보기 초기화로 미리보기 값을 되돌리세요.", "プレビューをリセットでプレビュー値を戻してください。"),
            }).ToArray(),
            ["replace"] = mesh.Concat(new[]
            {
                DiNeTutorialStep.Optional("shape-select", "Click the shape key you want to replace.", "교체할 쉐이프키를 클릭하세요.", "差し替えるシェイプキーをクリックしてください。"),
                DiNeTutorialStep.Optional("mix-add", "Use + to add shape keys to the replacement mix.", "+를 눌러 교체할 혼합 목록에 쉐이프키를 추가하세요.", "+で差し替える混合一覧にシェイプキーを追加してください。"),
                DiNeTutorialStep.Optional("mix-weight", "Adjust each mixed shape key's weight.", "혼합 목록에서 각 쉐이프키의 비율을 조절하세요.", "混合一覧で各シェイプキーの比率を調整してください。"),
                DiNeTutorialStep.Optional("mix-remove", "Use − to remove a shape key from the replacement mix.", "−를 눌러 교체할 혼합 목록에서 쉐이프키를 빼세요.", "−で差し替える混合一覧からシェイプキーを外してください。"),
                DiNeTutorialStep.Optional("mix-preview", "Update Preview shows the replacement mix.", "미리보기 갱신으로 교체할 혼합 결과를 확인하세요.", "プレビュー更新で差し替える混合結果を確認してください。"),
                DiNeTutorialStep.Optional("mix-restore", "Restore clears the replacement preview.", "원본 복원으로 교체 미리보기를 초기화하세요.", "元に戻すで差し替えプレビューをリセットしてください。"),
                DiNeTutorialStep.Optional("shape-replace", "Apply Mix Replacement saves the mix to the selected shape key in a mesh copy.", "믹스로 교체 적용을 눌러 선택한 쉐이프키를 메쉬 복사본에서 교체하세요.", "ミックスで差し替えを適用で選択シェイプキーをメッシュのコピーで差し替えられます。"),
            }).ToArray(),
            ["extra"] = new[]
            {
                avatar, refresh,
                DiNeTutorialStep.Optional("grab-on", "Use Enable All in Grabbing to allow grabbing.", "잡기의 모두 켜기로 잡기를 허용하세요.", "つかむのすべてオンでつかむ操作を許可できます。"),
                DiNeTutorialStep.Optional("grab-off", "Use Disable All in Grabbing to disable grabbing.", "잡기의 모두 끄기로 잡기를 끄세요.", "つかむのすべてオフでつかむ操作を無効にできます。"),
                DiNeTutorialStep.Optional("pose-on", "Use Enable All in Pose Lock to allow pose locking.", "포즈 고정의 모두 켜기로 고정을 허용하세요.", "ポーズ固定のすべてオンで固定を許可できます。"),
                DiNeTutorialStep.Optional("pose-off", "Use Disable All in Pose Lock to disable pose locking.", "포즈 고정의 모두 끄기로 고정을 끄세요.", "ポーズ固定のすべてオフで固定を無効にできます。"),
                DiNeTutorialStep.Optional("collision-on", "Use Enable All in Player Collider Response to enable player collisions.", "플레이어 콜라이더 반응의 모두 켜기로 반응을 켜세요.", "プレイヤーコライダー反応のすべてオンで反応を有効にできます。"),
                DiNeTutorialStep.Optional("collision-off", "Use Disable All in Player Collider Response to disable player collisions.", "플레이어 콜라이더 반응의 모두 끄기로 반응을 끄세요.", "プレイヤーコライダー反応のすべてオフで反応を無効にできます。"),
            },
        };
    }

    private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
    private void TutorialAnchor(string id, Rect rect) => _tutorial?.Anchor(id, rect);
    private void TutorialDraw(params string[] ids) { foreach (string id in ids) _tutorial?.Draw(id); }
    private void TutorialNotify(string id) => _tutorial?.NotifyAction(id);
}
