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
        string[] overview = course == "armature" ? new[]
        {
            "Adjust avatar proportions such as head size, arm length and body-part placement. Direct Bone Editing changes the scene bones immediately; MA Scale Adjustment stores settings in Modular Avatar components for avatar builds. Save a preset to reuse all or part of the setup.",
            "머리 크기·팔 길이·신체 부위 배치처럼 아바타의 체형을 조절합니다. 직접 뼈 조정은 씬의 본을 바로 바꾸고, MA 비율 조정은 아바타 빌드에 사용할 값을 Modular Avatar 컴포넌트에 저장합니다. 프리셋으로 저장하면 전체 또는 일부 조정을 다시 사용할 수 있습니다.",
            "頭の大きさ・腕の長さ・身体各部の配置など、アバターの体型を調整します。ボーン直接調整はシーンのボーンをその場で変更し、MA比率調整はアバタービルド用の値をModular Avatarコンポーネントに保存します。プリセットに保存すると、設定の全体または一部を再利用できます。"
        } : course == "extra" ? new[]
        {
            "Change grabbing, pose locking and player collider response across the assigned avatar's PhysBones at once. For example, make all hair and clothing ungrabbable without editing each component. These buttons change the avatar's existing PhysBone settings; they do not create new physics.",
            "지정한 아바타의 PhysBone 전체에서 잡기·포즈 고정·플레이어 콜라이더 반응을 한꺼번에 바꿉니다. 예를 들어 컴포넌트를 하나씩 열지 않고 머리카락·의상 전체를 잡지 못하게 설정할 수 있습니다. 기존 PhysBone의 설정을 바꾸며 새 물리 동작을 만들지는 않습니다.",
            "指定したアバターのPhysBone全体で、つかむ操作・ポーズ固定・プレイヤーコライダー反応を一括変更します。例えばコンポーネントを1つずつ開かずに、髪や衣装をすべてつかめない設定にできます。既存のPhysBone設定を変更する機能で、新しい物理動作は作成しません。"
        } : course == "create" ? new[]
        {
            "Combine existing shape keys into a new expression, such as a smile mixed with narrowed eyes. Preview each source's contribution, give the mix a unique name, then create it. The tool saves a new mesh asset with the added shape key and assigns it to the selected renderer while keeping the original mesh asset.",
            "기존 쉐이프키를 합쳐 웃음과 가늘어진 눈 같은 새 표정을 만듭니다. 각 원본의 비율을 조절해 미리보고, 중복되지 않는 이름을 정한 뒤 생성합니다. 새 쉐이프키를 추가한 메쉬 에셋을 저장해 선택한 렌더러에 배정하며 원본 메쉬 에셋은 보존합니다.",
            "既存のシェイプキーを組み合わせ、笑顔と細めた目などの新しい表情を作ります。各元データの比率をプレビューし、重複しない名前を付けて生成します。シェイプキーを追加した新しいメッシュアセットを保存して選択Rendererへ割り当て、元のメッシュアセットは保持します。"
        } : course == "scale" ? new[]
        {
            "Make an existing shape key's deformation stronger or weaker, for example reducing an exaggerated smile. The scale percentage changes the deformation itself, while Preview Weight lets you check its appearance at different weights. Apply saves and assigns a new mesh copy to the selected renderer; the original mesh asset is preserved.",
            "기존 쉐이프키의 변형을 강하게 또는 약하게 만들어 과한 웃음 등을 줄입니다. 배율은 변형 자체를 바꾸고, 미리보기 강도는 서로 다른 적용량에서 결과를 확인하게 합니다. 적용하면 새 메쉬 복사본을 저장해 선택한 렌더러에 배정하며 원본 메쉬 에셋은 보존합니다.",
            "既存のシェイプキーの変形を強く・弱くして、例えば大きすぎる笑顔を控えめにします。倍率は変形自体を変更し、プレビュー強度では異なる適用量で結果を確認できます。適用すると新しいメッシュコピーを保存して選択Rendererへ割り当て、元のメッシュアセットは保持します。"
        } : new[]
        {
            "Replace an existing shape key's deformation with a mix of other shape keys. This lets you redefine an expression while retaining its name and position in the list. Preview the mix, then apply it to a new mesh copy assigned to the selected renderer; the original mesh asset is preserved.",
            "기존 쉐이프키의 변형 내용을 다른 쉐이프키의 혼합으로 교체합니다. 이름과 목록 순서를 유지하면서 표정의 모양을 다시 정할 수 있습니다. 혼합을 미리본 뒤 적용하면 새 메쉬 복사본을 저장해 선택한 렌더러에 배정하며 원본 메쉬 에셋은 보존합니다.",
            "既存のシェイプキーの変形内容を、ほかのシェイプキーの混合へ差し替えます。名前と一覧の順番を保ったまま表情の形を作り直せます。混合をプレビューして適用すると新しいメッシュコピーを保存して選択Rendererへ割り当て、元のメッシュアセットは保持します。"
        };
        _tutorial.Configure(course, names[0], names[1], names[2], _tutorialCourses[course],
            overviewEn: overview[0], overviewKo: overview[1], overviewJa: overview[2]);
        _tutorial.BeginFrame();
    }

    private void CreateTutorialCourses()
    {
        var avatar = DiNeTutorialStep.Required("avatar", "The assigned root determines which avatar bones, meshes and PhysBones the tool can edit. Assign the avatar root.", "지정한 루트 아래의 본·메쉬·PhysBone이 이 도구의 작업 대상이 됩니다. 아바타 루트를 지정하세요.", "指定したルート以下のボーン・メッシュ・PhysBoneがこのツールの編集対象になります。 アバタールートを指定してください。", () => targetAvatarRoot != null);
        var refresh = DiNeTutorialStep.Optional("refresh", "Refreshing rescans the assigned avatar so newly added or replaced components appear in the tool. Use Refresh after changing the avatar's setup.", "새로고침은 지정한 아바타를 다시 검사해 새로 추가하거나 교체한 구성을 도구에 반영합니다. 아바타 구성을 바꿨다면 새로고침을 누르세요.", "更新すると指定したアバターを再検査し、追加・差し替えた構成をツールに反映します。 アバターの構成を変更したら更新を押してください。");
        var preview = new[]
        {
            DiNeTutorialStep.Optional("head-rotate", "Viewing a deformation from several angles helps catch changes that are hidden from the front. Drag the preview to rotate the view.", "여러 각도에서 보면 정면에서 보이지 않는 표정·형태 변화를 확인할 수 있습니다. 미리보기를 드래그해 시점을 회전하세요.", "角度を変えると正面では見えない表情や形状の変化を確認できます。 プレビューをドラッグして視点を回転してください。"),
            DiNeTutorialStep.Optional("head-zoom", "A closer view makes small changes around the eyes and mouth easier to inspect. Scroll in the preview to zoom.", "확대하면 눈·입 주변의 작은 변형을 자세히 확인할 수 있습니다. 미리보기에서 마우스 휠로 확대·축소하세요.", "拡大すると目や口の周囲の小さな変形を詳しく確認できます。 プレビュー内でホイールを回してズームしてください。"),
            DiNeTutorialStep.Optional("head-pan", "Moving the preview lets you center the part you are inspecting without changing the avatar. Alt-drag the preview to move the view.", "미리보기 이동으로 검사할 부위를 화면 중앙에 맞출 수 있으며 아바타 위치는 바꾸지 않습니다. Alt를 누르고 미리보기를 드래그해 시점을 이동하세요.", "プレビューを移動すると確認したい部分を中央に置けます。アバターの位置は変わりません。 Altを押しながらプレビューをドラッグして視点を移動してください。"),
            DiNeTutorialStep.Optional("head-reset", "Resetting the view restores the preview framing so you can continue from its initial angle. Use the small reset button to reset the view.", "시점 초기화는 미리보기 구도를 되돌려 처음 각도에서 다시 확인할 수 있게 합니다. 작은 초기화 버튼으로 시점을 초기화하세요.", "視点をリセットすると初期の構図に戻り、最初の角度から再確認できます。 小さいリセットボタンで視点をリセットしてください。"),
        };
        var mesh = new[]
        {
            DiNeTutorialStep.Optional("avatar", "The assigned root determines which avatar bones, meshes and PhysBones the tool can edit. Assign the avatar root, or assign its mesh below.", "지정한 루트 아래의 본·메쉬·PhysBone이 이 도구의 작업 대상이 됩니다. 아바타 루트를 지정하거나 아래에 메쉬를 직접 지정하세요.", "指定したルート以下のボーン・メッシュ・PhysBoneがこのツールの編集対象になります。 アバタールートを指定するか、下にメッシュを直接指定してください。"),
            refresh,
            DiNeTutorialStep.Required("mesh", "Shape keys are mesh deformations used for expressions and body adjustments; only the selected mesh is edited. Assign a mesh with shape keys.", "쉐이프키는 표정이나 체형을 바꾸는 메쉬 변형 데이터이며 선택한 메쉬만 편집합니다. 쉐이프키가 있는 메쉬를 지정하세요.", "シェイプキーは表情や体型を変えるメッシュの変形データで、選択したメッシュだけを編集します。 シェイプキーのあるメッシュを指定してください。", () => _skeSmr != null && _skeSmr.sharedMesh != null && _skeSmr.sharedMesh.blendShapeCount > 0),
        }.Concat(preview).Concat(new[] { DiNeTutorialStep.Optional("shape-search", "Filtering helps find an expression in a long list, such as a smile or eye-close shape key. Type a shape key name to filter the list.", "검색은 긴 목록에서 웃음·눈 감기 같은 표정 쉐이프키를 찾는 데 사용합니다. 쉐이프키 이름을 입력해 목록을 검색하세요.", "検索は長い一覧から笑顔や目閉じなどの表情シェイプキーを探すために使います。 シェイプキー名を入力して一覧を検索してください。") }).ToArray();
        _tutorialCourses = new Dictionary<string, DiNeTutorialStep[]>
        {
            ["armature"] = new[]
            {
                DiNeTutorialStep.Required("avatar", "The assigned root determines which avatar bones, meshes and PhysBones the tool can edit. Assign an avatar with mapped humanoid bones.", "지정한 루트 아래의 본·메쉬·PhysBone이 이 도구의 작업 대상이 됩니다. 휴머노이드 본이 있는 아바타를 지정하세요.", "指定したルート以下のボーン・メッシュ・PhysBoneがこのツールの編集対象になります。 ヒューマノイドボーンのあるアバターを指定してください。", () => targetAvatarRoot != null && boneMapping != null && boneMapping.Values.Any(b => b != null)),
                refresh,
                DiNeTutorialStep.Optional("direct-mode", "Direct editing changes the scene avatar's bone Transforms immediately, for example to adjust head size or arm length. Choose Direct Bone Editing.", "직접 조정은 씬 아바타의 본 Transform을 바로 바꿔 머리 크기나 팔 길이 등을 조절합니다. 직접 뼈 조정을 선택하세요.", "直接調整はシーン内のボーンTransformをその場で変更し、頭の大きさや腕の長さなどを調整します。 ボーン直接調整を選んでください。", () => { armatureEditMode = ArmatureEditMode.DirectTransform; LoadCurrentValues(); }),
                DiNeTutorialStep.Required("bone", "The body map selects the bone whose size, position or rotation you want to change. Click a bone in the body map to edit it.", "신체 그림은 크기·위치·회전을 바꿀 본을 선택하는 지도입니다. 신체 그림에서 조절할 본을 클릭하세요.", "身体図はサイズ・位置・回転を変更するボーンを選ぶためのマップです。 身体図で調整するボーンをクリックしてください。", () => selectedPart != HumanoidBodyPart.None && TryGetLiveBoneTransform(GetBoneType(selectedPart), out Transform bone) && bone != null),
                DiNeTutorialStep.Optional("scale-uniform", "Uniform scale changes all three axes together, such as making the head larger while keeping its proportions. Enter a uniform scale for the selected bone.", "전체 크기는 세 축을 함께 바꾸므로 비율을 유지하면서 머리를 크게 만드는 데 사용할 수 있습니다. 선택한 본의 전체 크기를 입력하세요.", "全体サイズは3軸をまとめて変更するため、比率を保ったまま頭を大きくするなどの調整に使えます。 選択ボーンの全体サイズを入力してください。"),
                DiNeTutorialStep.Optional("scale-vector", "Separate axis scales change the selected part's proportions, such as making it wider or longer. Set the X, Y and Z scale separately here.", "축별 크기는 선택한 부위를 넓히거나 길게 만드는 등 형태의 비율을 바꿉니다. X·Y·Z 크기를 각각 조절하세요.", "軸ごとのサイズは、選択した部分を広げる・長くするなど形状の比率を変えます。 X・Y・Zのサイズを個別に調整してください。"),
                DiNeTutorialStep.Optional("position", "Position changes move the bone relative to its parent, which can change a body part's placement. Set the selected bone's position.", "위치 조정은 부모 기준으로 본을 이동하므로 신체 부위의 배치를 바꿀 수 있습니다. 선택한 본의 위치를 조절하세요.", "位置調整は親を基準にボーンを移動し、身体の各部の配置を変えられます。 選択ボーンの位置を調整してください。"),
                DiNeTutorialStep.Optional("rotation", "Rotation changes the angle of supported bones, such as the pose of a shoulder or hand. For a rotatable bone, set its rotation here.", "회전 조정은 지원하는 본의 각도를 바꿔 어깨·손 등의 자세를 조절합니다. 회전할 수 있는 본은 여기서 회전을 조절하세요.", "回転調整は対応するボーンの角度を変更し、肩や手などの姿勢を調整します。 回転できるボーンはここで回転を調整してください。"),
                DiNeTutorialStep.Optional("reset-scales", "This resets direct bone scale adjustments together; use it when you want to redo the size balance. Use Reset Scales when you want to reset the direct bone sizes.", "직접 조정한 본 크기를 함께 초기화하므로 신체 크기 균형을 다시 잡을 때 사용합니다. 직접 조정한 본 크기를 초기화하려면 크기 초기화를 누르세요.", "直接調整したボーンサイズをまとめてリセットし、身体のサイズバランスをやり直す際に使います。 直接調整したボーンサイズをリセットするにはサイズリセットを押してください。"),
                DiNeTutorialStep.Optional("ma-mode", "MA Scale Adjustment stores size changes in Modular Avatar Scale Adjusters for processing when the avatar is built. Choose MA Scale Adjustment.", "MA 비율 조정은 크기 변경을 Modular Avatar Scale Adjuster에 저장해 아바타 빌드 시 처리하게 합니다. MA 비율 조정을 선택하세요.", "MA比率調整はサイズ変更をModular Avatar Scale Adjusterに保存し、アバタービルド時に処理します。 MA比率調整を選んでください。", () => armatureEditMode = ArmatureEditMode.ModularAvatarScale),
                DiNeTutorialStep.Optional("ma-add", "A Scale Adjuster component is needed to store a bone's Modular Avatar scale settings. Select a bone, then add its MA Scale Adjuster if it has none.", "본의 Modular Avatar 비율 설정을 저장하려면 Scale Adjuster 컴포넌트가 필요합니다. 본을 선택하고 Adjuster가 없으면 추가하세요.", "ボーンのModular Avatar比率設定を保存するにはScale Adjusterコンポーネントが必要です。 ボーンを選び、Adjusterがなければ追加してください。"),
                DiNeTutorialStep.Optional("ma-scale-uniform", "This sets one multiplier for all axes in the selected bone's Scale Adjuster. With an Adjuster added, enter its uniform scale.", "선택한 본의 Scale Adjuster에 세 축이 함께 사용하는 배율을 설정합니다. Adjuster를 추가한 뒤 전체 비율을 입력하세요.", "選択したボーンのScale Adjusterに、全軸共通の倍率を設定します。 Adjusterを追加したら全体比率を入力してください。"),
                DiNeTutorialStep.Optional("ma-scale-vector", "Axis multipliers let the Scale Adjuster stretch a part differently in each direction. Set the Adjuster's X, Y and Z scales separately.", "축별 배율은 Scale Adjuster가 부위를 방향마다 다르게 늘리도록 설정합니다. Adjuster의 X·Y·Z 비율을 각각 조절하세요.", "軸別の倍率を使うとScale Adjusterで部位を方向ごとに異なる比率へ調整できます。 AdjusterのX・Y・Z比率を個別に調整してください。"),
                DiNeTutorialStep.Optional("ma-children", "Adjusting child positions changes how attached child bones are spaced when the scale is applied. Choose whether to adjust child positions with the scale.", "자식 위치 조정은 배율 적용 시 연결된 자식 본의 간격도 함께 바꿀지 정합니다. 비율 변경에 맞춰 자식 위치도 조정할지 선택하세요.", "子の位置調整は、倍率適用時に接続された子ボーンの間隔も変更するかを決めます。 比率変更に合わせて子の位置も調整するか選んでください。"),
                DiNeTutorialStep.Optional("ma-remove", "Removing the Adjuster removes that component's scale settings from the selected bone. Use Remove only when you want to remove the selected bone's Adjuster.", "Adjuster 제거는 선택한 본에서 해당 컴포넌트의 비율 설정을 없앱니다. 선택한 본의 Adjuster를 없애려면 제거를 누르세요.", "Adjusterを削除すると、選択したボーンからそのコンポーネントの比率設定を取り除きます。 選択ボーンのAdjusterを削除する場合は削除を押してください。"),
                DiNeTutorialStep.Optional("preset", "Presets let you reuse a saved body proportion setup instead of entering every adjustment again. Select an armature preset.", "프리셋은 저장한 체형 조정을 다시 사용해 모든 값을 반복 입력하지 않게 해줍니다. 아마추어 프리셋을 선택하세요.", "プリセットを使うと保存した体型調整を再利用でき、各値を再入力せずに済みます。 アーマチュアプリセットを選んでください。"),
                DiNeTutorialStep.Optional("preset-load", "The entire preset combines its saved bone sizes, positions, rotations and MA settings on this avatar. Load Entire Preset applies all saved adjustments.", "전체 불러오기는 저장된 본 크기·위치·회전과 MA 설정을 이 아바타에 함께 적용합니다. 프리셋 전체 불러오기로 저장된 조정값을 모두 적용하세요.", "全体読み込みは保存されたボーンサイズ・位置・回転とMA設定をこのアバターへまとめて適用します。 プリセット全体を読み込むで保存した調整値を全て適用できます。"),
                DiNeTutorialStep.Optional("preset-scale", "Loading only sizes lets you reuse the preset's proportions while retaining other kinds of adjustments. Use Bone Scale Only to load just the direct bone scales.", "크기만 불러오면 다른 종류의 조정은 유지하면서 프리셋의 신체 비율을 재사용할 수 있습니다. 기본 크기만으로 직접 본 크기만 불러오세요.", "サイズだけを読み込むと、ほかの種類の調整を保持したままプリセットの体型比率を再利用できます。 ボーンサイズのみで直接のボーンサイズだけを読み込めます。"),
                DiNeTutorialStep.Optional("preset-rotation", "Loading only rotations lets you reuse bone angles without applying the preset's sizes or positions. Use Rotation Only to load only rotations.", "회전만 불러오면 프리셋의 크기·위치를 적용하지 않고 본 각도를 재사용합니다. 회전만을 눌러 회전값만 불러오세요.", "回転だけを読み込むと、プリセットのサイズや位置を適用せずにボーンの角度を再利用できます。 回転のみで回転値だけを読み込めます。"),
                DiNeTutorialStep.Optional("preset-position", "Loading only positions lets you reuse bone placement without applying the preset's sizes or rotations. Use Position Only to load only positions.", "위치만 불러오면 프리셋의 크기·회전을 적용하지 않고 본 배치를 재사용합니다. 위치만을 눌러 위치값만 불러오세요.", "位置だけを読み込むと、プリセットのサイズや回転を適用せずにボーンの配置を再利用できます。 位置のみで位置値だけを読み込めます。"),
                DiNeTutorialStep.Optional("preset-ma", "This reuses the preset's Modular Avatar scale setup without loading direct Transform adjustments. Use MA Scale Adjuster Only to load only the MA adjustments.", "이 기능은 직접 Transform 조정을 불러오지 않고 프리셋의 Modular Avatar 비율 설정을 재사용합니다. MA Scale Adjuster만 불러오기로 MA 조정값만 적용하세요.", "この機能は直接のTransform調整を読み込まずに、プリセットのModular Avatar比率設定を再利用します。 MA Scale Adjusterのみ読み込むでMA調整値だけを適用できます。"),
                DiNeTutorialStep.Optional("preset-save", "A new preset keeps both adjustment methods so you can reproduce the current proportions later. Save Both as New Preset saves the current direct and MA adjustments.", "새 프리셋은 두 조정 방식을 함께 저장해 현재 체형을 나중에 다시 적용할 수 있게 합니다. 두 조정값을 새 프리셋으로 저장을 눌러 현재 값을 저장하세요.", "新しいプリセットには両方の調整方法を保存でき、現在の体型を後から再現できます。 両方の調整値を新規プリセットとして保存で現在値を保存できます。"),
                DiNeTutorialStep.Optional("preset-delete", "Deleting a preset removes the saved setup from the preset list. Use Delete only when you want to remove the selected preset.", "프리셋 삭제는 저장된 조정 구성을 프리셋 목록에서 제거합니다. 선택한 프리셋이 필요 없으면 삭제를 누르세요.", "プリセットの削除は、保存した調整設定をプリセット一覧から取り除きます。 選択プリセットが不要な場合は削除を押してください。"),
            },
            ["create"] = mesh.Concat(new[]
            {
                DiNeTutorialStep.Optional("mix-add", "A mix combines existing deformations, for example a smile with narrowed eyes for one expression. Click Add beside each shape key to include in the mix.", "혼합은 웃음과 가늘어진 눈처럼 기존 변형을 합쳐 하나의 표정을 만드는 데 사용합니다. 혼합할 쉐이프키 옆의 추가를 누르세요.", "混合は笑顔と細めた目など既存の変形を組み合わせ、1つの表情を作るために使います。 混ぜるシェイプキーの横にある追加を押してください。"),
                DiNeTutorialStep.Optional("mix-weight", "Each weight controls how much that source deformation contributes to the combined expression. Adjust each mixed shape key's weight.", "각 비율은 해당 원본 변형이 합친 표정에 얼마나 반영되는지 정합니다. 혼합 목록에서 각 쉐이프키의 비율을 조절하세요.", "各比率は元の変形が組み合わせた表情にどれだけ反映されるかを決めます。 混合一覧で各シェイプキーの比率を調整してください。"),
                DiNeTutorialStep.Optional("mix-remove", "Removing an entry excludes it from the mix and keeps the original shape key in the mesh. Use − to remove a shape key from the mix.", "혼합 항목을 빼면 조합에서만 제외되며 메쉬의 원본 쉐이프키는 남습니다. −를 눌러 혼합 목록에서 쉐이프키를 빼세요.", "混合項目を外すと組み合わせからだけ除外され、メッシュ内の元のシェイプキーは残ります。 −で混合一覧からシェイプキーを外してください。"),
                DiNeTutorialStep.Optional("mix-preview", "The preview lets you inspect the combined face before saving a mesh edit. Use Update Preview to check the mix.", "미리보기로 메쉬 편집을 저장하기 전에 합친 표정을 확인할 수 있습니다. 미리보기 갱신으로 혼합 결과를 확인하세요.", "プレビューでメッシュ編集を保存する前に、組み合わせた表情を確認できます。 プレビュー更新で混合結果を確認してください。"),
                DiNeTutorialStep.Optional("mix-restore", "Restoring ends the temporary mix preview so you can compare it with the original display. Use Restore to clear the mix preview.", "복원은 임시 혼합 미리보기를 끝내 원래 표시와 비교할 수 있게 합니다. 원본 복원으로 혼합 미리보기를 초기화하세요.", "元に戻すと一時的な混合プレビューを終了し、元の表示と比較できます。 元に戻すで混合プレビューをリセットしてください。"),
                DiNeTutorialStep.Optional("shape-name", "The new name identifies the combined expression in the mesh's shape key list and must be unique. Enter the new shape key's name.", "새 이름은 메쉬의 쉐이프키 목록에서 합친 표정을 구분하며 기존 이름과 달라야 합니다. 새 쉐이프키 이름을 입력하세요.", "新しい名前はメッシュのシェイプキー一覧で合成した表情を識別するためのもので、既存の名前と異なる必要があります。 新しいシェイプキー名を入力してください。"),
                DiNeTutorialStep.Optional("shape-create", "The mix is added alongside existing shape keys in a new mesh asset, which is assigned to the selected renderer. Create Shape Key saves the mix as a new shape key.", "기존 쉐이프키를 유지한 새 메쉬 에셋에 혼합 결과를 추가하고 선택한 렌더러에 배정합니다. 쉐이프키 생성으로 혼합 결과를 새 쉐이프키로 저장하세요.", "既存のシェイプキーを残した新しいメッシュアセットに混合結果を追加し、選択したRendererへ割り当てます。 シェイプキー生成で混合結果を新しいシェイプキーとして保存できます。"),
            }).ToArray(),
            ["scale"] = mesh.Concat(new[]
            {
                DiNeTutorialStep.Optional("shape-select", "Selecting a row chooses the existing deformation you will scale or replace; other shape keys remain separate. Click the shape key to adjust and preview it.", "목록 선택은 배율을 바꾸거나 교체할 기존 변형을 정하며 다른 쉐이프키는 별도로 유지됩니다. 조절할 쉐이프키를 클릭해 선택하고 미리보세요.", "一覧から選ぶと倍率変更・差し替えする既存の変形を指定でき、ほかのシェイプキーは個別に保持されます。 調整するシェイプキーをクリックして選択・プレビューしてください。"),
                DiNeTutorialStep.Optional("shape-scale", "The percentage changes the deformation itself: 200% doubles its movement and 50% halves it. Set the new scale percentage.", "배율은 변형 자체를 바꾸며 200%는 움직임을 두 배, 50%는 절반으로 만듭니다. 새 배율을 퍼센트로 조절하세요.", "倍率は変形自体を変更し、200%なら動きが2倍、50%なら半分になります。 新しい倍率をパーセントで調整してください。"),
                DiNeTutorialStep.Optional("shape-preview-weight", "Preview Weight changes how much of the selected shape key you display to compare its scaled result. Use Preview Weight to choose how strongly to preview the shape key.", "미리보기 강도는 선택한 쉐이프키의 표시량을 조절해 배율 변경 결과를 비교하게 합니다. 미리보기 강도로 쉐이프키를 얼마나 적용해 볼지 조절하세요.", "プレビュー強度は選択シェイプキーの表示量を調整し、倍率変更の結果を比較するために使います。 プレビュー強度でシェイプキーの表示量を調整してください。"),
                DiNeTutorialStep.Optional("shape-apply-scale", "The saved copy becomes the selected renderer's mesh; other renderers using the original mesh keep their asset. Apply Scale saves the scale change to a mesh copy.", "저장한 복사본은 선택한 렌더러의 메쉬가 되며 원본 메쉬를 쓰는 다른 렌더러는 원래 에셋을 유지합니다. 배율 적용으로 변경한 배율을 메쉬 복사본에 저장하세요.", "保存したコピーを選択Rendererへ割り当て、元のメッシュを使うほかのRendererは元のアセットを保持します。 倍率を適用で変更した倍率をメッシュのコピーに保存できます。"),
                DiNeTutorialStep.Optional("shape-reset-preview", "Restoring returns temporary preview values so you can compare against the original expression. Restore Preview resets the preview values.", "초기화는 임시 미리보기 값을 되돌려 원래 표정과 비교할 수 있게 합니다. 미리보기 초기화로 미리보기 값을 되돌리세요.", "リセットは一時的なプレビュー値を戻し、元の表情と比較できるようにします。 プレビューをリセットでプレビュー値を戻してください。"),
            }).ToArray(),
            ["replace"] = mesh.Concat(new[]
            {
                DiNeTutorialStep.Optional("shape-select", "Selecting a row chooses the existing deformation you will scale or replace; other shape keys remain separate. Click the shape key you want to replace.", "목록 선택은 배율을 바꾸거나 교체할 기존 변형을 정하며 다른 쉐이프키는 별도로 유지됩니다. 교체할 쉐이프키를 클릭하세요.", "一覧から選ぶと倍率変更・差し替えする既存の変形を指定でき、ほかのシェイプキーは個別に保持されます。 差し替えるシェイプキーをクリックしてください。"),
                DiNeTutorialStep.Optional("mix-add", "A mix combines existing deformations, for example a smile with narrowed eyes for one expression. Use + to add shape keys to the replacement mix.", "혼합은 웃음과 가늘어진 눈처럼 기존 변형을 합쳐 하나의 표정을 만드는 데 사용합니다. +를 눌러 교체할 혼합 목록에 쉐이프키를 추가하세요.", "混合は笑顔と細めた目など既存の変形を組み合わせ、1つの表情を作るために使います。 +で差し替える混合一覧にシェイプキーを追加してください。"),
                DiNeTutorialStep.Optional("mix-weight", "Each weight controls how much that source deformation contributes to the combined expression. Adjust each mixed shape key's weight.", "각 비율은 해당 원본 변형이 합친 표정에 얼마나 반영되는지 정합니다. 혼합 목록에서 각 쉐이프키의 비율을 조절하세요.", "各比率は元の変形が組み合わせた表情にどれだけ反映されるかを決めます。 混合一覧で各シェイプキーの比率を調整してください。"),
                DiNeTutorialStep.Optional("mix-remove", "Removing an entry excludes it from the mix and keeps the original shape key in the mesh. Use − to remove a shape key from the replacement mix.", "혼합 항목을 빼면 조합에서만 제외되며 메쉬의 원본 쉐이프키는 남습니다. −를 눌러 교체할 혼합 목록에서 쉐이프키를 빼세요.", "混合項目を外すと組み合わせからだけ除外され、メッシュ内の元のシェイプキーは残ります。 −で差し替える混合一覧からシェイプキーを外してください。"),
                DiNeTutorialStep.Optional("mix-preview", "The preview lets you inspect the combined face before saving a mesh edit. Update Preview shows the replacement mix.", "미리보기로 메쉬 편집을 저장하기 전에 합친 표정을 확인할 수 있습니다. 미리보기 갱신으로 교체할 혼합 결과를 확인하세요.", "プレビューでメッシュ編集を保存する前に、組み合わせた表情を確認できます。 プレビュー更新で差し替える混合結果を確認してください。"),
                DiNeTutorialStep.Optional("mix-restore", "Restoring ends the temporary mix preview so you can compare it with the original display. Restore clears the replacement preview.", "복원은 임시 혼합 미리보기를 끝내 원래 표시와 비교할 수 있게 합니다. 원본 복원으로 교체 미리보기를 초기화하세요.", "元に戻すと一時的な混合プレビューを終了し、元の表示と比較できます。 元に戻すで差し替えプレビューをリセットしてください。"),
                DiNeTutorialStep.Optional("shape-replace", "Replacement keeps the selected shape key's name and index but changes its deformation to your mix. Apply Mix Replacement saves the mix to the selected shape key in a mesh copy.", "교체는 선택한 쉐이프키의 이름·순서를 유지하고 변형 내용을 설정한 혼합 결과로 바꿉니다. 믹스로 교체 적용을 눌러 선택한 쉐이프키를 메쉬 복사본에서 교체하세요.", "差し替えは選択したシェイプキーの名前・順番を保持し、変形内容を設定した混合結果へ変更します。 ミックスで差し替えを適用で選択シェイプキーをメッシュのコピーで差し替えられます。"),
            }).ToArray(),
            ["extra"] = new[]
            {
                avatar, refresh,
                DiNeTutorialStep.Optional("grab-on", "Grabbing lets VRChat users grab PhysBone-driven hair or clothing; this enables it across the avatar. Use Enable All in Grabbing to allow grabbing.", "잡기는 VRChat에서 PhysBone으로 움직이는 머리카락·의상을 손으로 잡는 기능이며 아바타 전체에 허용합니다. 잡기의 모두 켜기로 잡기를 허용하세요.", "つかむ操作はVRChatでPhysBoneの髪や衣装を手でつかむ機能で、アバター全体に許可します。 つかむのすべてオンでつかむ操作を許可できます。"),
                DiNeTutorialStep.Optional("grab-off", "This stops grabbing across the avatar's PhysBones, such as when you want hair or clothing to be ungrabbable. Use Disable All in Grabbing to disable grabbing.", "머리카락·의상을 잡지 못하게 하고 싶을 때 아바타 전체 PhysBone의 잡기를 끕니다. 잡기의 모두 끄기로 잡기를 끄세요.", "髪や衣装をつかめないようにしたい場合に、アバター全体のPhysBoneのつかむ操作を無効にします。 つかむのすべてオフでつかむ操作を無効にできます。"),
                DiNeTutorialStep.Optional("pose-on", "Pose Lock lets a grabbed PhysBone stay in a posed state rather than immediately returning. Use Enable All in Pose Lock to allow pose locking.", "포즈 고정은 잡은 PhysBone이 바로 돌아오지 않고 잡아둔 자세를 유지하게 하는 기능입니다. 포즈 고정의 모두 켜기로 고정을 허용하세요.", "ポーズ固定は、つかんだPhysBoneがすぐに戻らず、その姿勢を保てるようにする機能です。 ポーズ固定のすべてオンで固定を許可できます。"),
                DiNeTutorialStep.Optional("pose-off", "Disabling Pose Lock prevents the avatar's PhysBones from retaining a manually locked pose. Use Disable All in Pose Lock to disable pose locking.", "포즈 고정을 끄면 아바타의 PhysBone이 손으로 고정한 자세를 유지하지 않게 합니다. 포즈 고정의 모두 끄기로 고정을 끄세요.", "ポーズ固定を無効にすると、アバターのPhysBoneが手で固定した姿勢を保持しないようにします。 ポーズ固定のすべてオフで固定を無効にできます。"),
                DiNeTutorialStep.Optional("collision-on", "Player collider response makes PhysBones react to VRChat player colliders when they touch. Use Enable All in Player Collider Response to enable player collisions.", "플레이어 콜라이더 반응은 VRChat 플레이어의 콜라이더가 닿았을 때 PhysBone이 반응하게 합니다. 플레이어 콜라이더 반응의 모두 켜기로 반응을 켜세요.", "プレイヤーコライダー反応は、VRChatプレイヤーのコライダーが触れたときにPhysBoneを反応させます。 プレイヤーコライダー反応のすべてオンで反応を有効にできます。"),
                DiNeTutorialStep.Optional("collision-off", "This disables response to player colliders across the avatar without removing its PhysBones. Use Disable All in Player Collider Response to disable player collisions.", "PhysBone을 삭제하지 않고 아바타 전체에서 플레이어 콜라이더에 대한 반응을 끕니다. 플레이어 콜라이더 반응의 모두 끄기로 반응을 끄세요.", "PhysBoneを削除せず、アバター全体のプレイヤーコライダーへの反応を無効にします。 プレイヤーコライダー反応のすべてオフで反応を無効にできます。"),
            },
        };
    }

    private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
    private void TutorialAnchor(string id, Rect rect) => _tutorial?.Anchor(id, rect);
    private void TutorialDraw(params string[] ids) { foreach (string id in ids) _tutorial?.Draw(id); }
    private void TutorialNotify(string id) => _tutorial?.NotifyAction(id);
}
