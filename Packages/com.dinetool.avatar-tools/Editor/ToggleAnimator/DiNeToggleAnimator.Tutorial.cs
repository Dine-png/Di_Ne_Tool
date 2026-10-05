using UnityEditor;
using UnityEngine;
using System.Linq;

public partial class DiNeToggleAnimator
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[] tutorialSteps;

    private void EnsureTutorial()
    {
        if (tutorial == null) tutorial = new DiNeGuidedTutorial(this, "toggle-animator");
        if (tutorialSteps == null) tutorialSteps = new[]
        {
            DiNeTutorialStep.Required("clips", "Each selected Animation Clip becomes a column in the editing table. Select your outfit or accessory clips in Project to compare and edit their object states together.", "선택한 Animation Clip 하나마다 편집 표의 열이 생깁니다. Project에서 의상·액세서리 클립들을 선택해 오브젝트 상태를 나란히 비교하고 수정하세요.", "選んだAnimation Clipごとに編集表の列が作られます。Projectで衣装やアクセサリーのクリップを選び、対象の状態を並べて比較・編集してください。", () => _clips.Count > 0 && _clips.All(clip => clip != null)),
            DiNeTutorialStep.Optional("lock", "Lock Selection keeps the current clip columns while you select objects elsewhere. Enable it before selecting or dragging avatar objects from Hierarchy.", "선택 유지를 켜면 다른 오브젝트를 선택해도 현재 클립 열이 유지됩니다. Hierarchy에서 아바타 오브젝트를 선택하거나 드래그하기 전에 켜세요.", "選択維持をONにすると、別の対象を選んでも現在のクリップ列を保持します。Hierarchyでアバターの対象を選択・ドラッグする前にONにしてください。"),
            DiNeTutorialStep.Required("root", "The avatar root lets the tool match clip paths to real scene objects and show the result. Choose the scene avatar whose hierarchy these clips animate.", "아바타 루트는 클립 경로를 실제 씬 오브젝트와 연결해 결과를 보여주는 기준입니다. 선택한 클립이 사용하는 계층 구조의 씬 아바타를 지정하세요.", "アバターのルートを基準に、クリップのパスをシーンの対象と対応させて結果を表示します。選んだクリップが使う階層を持つシーンアバターを指定してください。", HasTutorialRoot),
            DiNeTutorialStep.Required("rows", "Each row represents an object's active state or a BlendShape already found in the selected clips. Drag an avatar object here to add an ON/OFF row for it across all columns.", "행 하나는 오브젝트의 활성 상태 또는 선택한 클립에 있는 쉐이프키 값을 나타냅니다. 아바타 오브젝트를 여기에 드래그하면 모든 클립에서 편집할 ON/OFF 행이 생깁니다.", "各行は対象の有効状態、または選んだクリップ内のシェイプキー値を表します。アバターの対象をここにドラッグすると、全列で編集できるON/OFF行を追加します。", HasTutorialRow),
            DiNeTutorialStep.Optional("preview", "Clicking a clip name applies its defined table values to the scene avatar. Use it to compare the visibility and expression stored in each clip.", "클립 이름을 누르면 표에 지정된 해당 클립의 값이 씬 아바타에 적용됩니다. 클립마다 저장된 표시 상태와 표정을 비교할 때 사용하세요.", "クリップ名を押すと、その列に設定された値をシーンアバターに適用します。各クリップの表示状態や表情を比較するときに使ってください。"),
            DiNeTutorialStep.Optional("undo-preview", "Preview changes scene values and records them for Undo. Use Undo to revert the applied changes; repeated previews may require several Undo operations.", "미리보기는 씬의 값을 바꾸고 실행 취소 기록을 남깁니다. 적용한 값을 되돌리려면 실행 취소를 사용하세요. 여러 번 미리봤다면 여러 번 되돌려야 할 수 있습니다.", "プレビューはシーンの値を変更し、元に戻す履歴を記録します。適用した値を戻すには元に戻すを使ってください。複数回のプレビューでは複数回戻す場合があります。"),
            DiNeTutorialStep.Optional("unset", "A — cell means this clip has no curve for that row, so it does not set that property. Click — on an object row to add an ON value to that clip.", "—는 해당 행의 커브가 없어 이 클립이 그 값을 지정하지 않는다는 뜻입니다. 오브젝트 행의 —를 누르면 해당 클립에 ON 값이 추가됩니다.", "—はその行のカーブがなく、このクリップではその値を指定しない状態です。対象の行で—を押すと、そのクリップにON値を追加します。"),
            DiNeTutorialStep.Optional("toggle", "ON/OFF cells define whether this object is visible when the clip is used. Click a cell to switch its value; the clip is saved immediately and the scene shows the result.", "ON/OFF 셀은 클립을 사용할 때 해당 오브젝트를 켤지 정합니다. 셀을 눌러 값을 바꾸면 클립이 즉시 저장되고 씬에도 결과가 반영됩니다.", "ON/OFFセルでクリップ使用時に対象を表示するか決めます。セルを押して切り替えるとクリップをすぐ保存し、シーンにも結果を反映します。"),
            DiNeTutorialStep.Optional("clear", "Right-clicking an ON/OFF cell removes that object's active-state curve from this clip. The cell returns to —, letting other animation or scene settings control the property.", "ON/OFF 셀을 우클릭하면 해당 클립에서 오브젝트 활성 상태 커브를 지웁니다. 셀이 —로 돌아가며 다른 애니메이션이나 씬 설정이 그 값을 제어할 수 있습니다.", "ON/OFFセルを右クリックすると、そのクリップの有効状態カーブを削除します。セルは—に戻り、ほかのアニメーションやシーン設定で値を制御できます。"),
            DiNeTutorialStep.Optional("shape", "BlendShape rows appear when the selected clips contain those curves. Edit a value to store a constant expression or shape weight in that clip and check it in the scene.", "선택한 클립에 쉐이프키 커브가 있으면 해당 행이 표시됩니다. 값을 수정해 클립에 일정한 표정·형태 값을 저장하고 씬에서 확인하세요.", "選んだクリップにシェイプキーカーブがあると対応する行が表示されます。値を編集して一定の表情・形状の値をクリップに保存し、シーンで確認してください。"),
            DiNeTutorialStep.Optional("shape-clear", "The × beside a BlendShape value removes only that cell's curve from its clip. Use it when this clip should leave the BlendShape to other animation.", "쉐이프키 값 옆의 ×는 해당 셀의 커브만 클립에서 지웁니다. 이 클립에서 그 쉐이프키를 지정하지 않고 다른 애니메이션에 맡길 때 사용하세요.", "シェイプキー値の横の×は、そのセルのカーブだけをクリップから削除します。このクリップでは値を指定せず、ほかのアニメーションに任せるときに使ってください。"),
            DiNeTutorialStep.Optional("delete-row", "The × on a row removes that property's curves from every selected clip. Use it only when the whole clip set should stop controlling that object state or BlendShape.", "행의 ×는 선택한 모든 클립에서 해당 속성의 커브를 지웁니다. 클립 전체에서 그 오브젝트 상태나 쉐이프키 제어를 제거할 때 사용하세요.", "行の×は選んだ全クリップからそのプロパティのカーブを削除します。クリップ全体でその有効状態やシェイプキーの制御を外す場合に使ってください。"),
            DiNeTutorialStep.Optional("fill", "Fill Missing → OFF writes zero into every — cell, including unset BlendShapes. Use it to make all selected clips explicitly switch unused objects OFF and reset unspecified shape values.", "미지정 → OFF는 쉐이프키를 포함한 모든 — 셀에 0을 저장합니다. 선택한 클립마다 미지정 오브젝트를 명시적으로 끄고 쉐이프키를 0으로 지정할 때 사용하세요.", "未設定→OFFはシェイプキーを含む全ての—セルに0を保存します。各クリップで未指定の対象を明示的にOFFにし、シェイプキーを0にするときに使ってください。"),
            DiNeTutorialStep.Optional("smart-fill", "Smart Fill works on rows that have a positive value in at least one clip. It fills unset cells in those rows with zero, useful for keeping an outfit ON in one clip and OFF in the others.", "Smart Fill은 한 클립에서라도 양수 값이 있는 행을 대상으로 합니다. 그 행의 빈 셀을 0으로 채워 한 클립에서 켠 의상을 다른 클립에서는 끄도록 만들 때 유용합니다.", "Smart Fillはどれかのクリップに正の値がある行を対象にします。その行の未設定セルを0で埋め、あるクリップでONにした衣装をほかではOFFにするときに便利です。"),
            DiNeTutorialStep.Optional("invert", "Invert All swaps every defined object's ON/OFF value across the selected clips. For BlendShapes, positive values become 0 and zero becomes 100; unset cells remain unset.", "전체 반전은 선택한 클립에 지정된 모든 오브젝트의 ON/OFF를 뒤집습니다. 쉐이프키는 양수가 0으로, 0이 100으로 바뀌며 미지정 셀은 유지됩니다.", "全て反転は選んだクリップで設定済みの対象のON/OFFを反転します。シェイプキーは正の値が0に、0が100になり、未設定セルは維持されます。")
        };
        tutorial.Configure("all", "Toggle Animator", "토글 애니메이터", "Toggle Animator", tutorialSteps,
            overviewEn: "Toggle Animator edits object ON/OFF states and BlendShape values across several Animation Clips in one table. Use it to make outfit or accessory clips turn the right objects on and off, or assign fixed expression values. Cell edits write constant curves into the selected clip assets immediately, and preview applies their values to the scene avatar.",
            overviewKo: "Toggle Animator는 여러 Animation Clip의 오브젝트 ON/OFF와 쉐이프키 값을 하나의 표에서 편집합니다. 의상·액세서리 클립마다 필요한 오브젝트를 켜고 끄거나 일정한 표정 값을 지정할 때 사용합니다. 셀을 수정하면 선택한 클립 에셋에 고정값 커브가 즉시 저장되고, 미리보기로 씬 아바타에 값을 적용해 확인할 수 있습니다.",
            overviewJa: "Toggle Animatorは複数のAnimation Clipの対象ON/OFFとシェイプキー値を1つの表で編集します。衣装・アクセサリーのクリップごとに必要な対象を切り替えたり、一定の表情値を設定するときに使います。セルを編集すると選んだクリップアセットに定数カーブをすぐ保存し、プレビューでシーンアバターに値を適用できます。");
    }

    private bool HasTutorialRoot() => _avatarRoot != null && !EditorUtility.IsPersistent(_avatarRoot) && _avatarRoot.scene.IsValid();

    private bool HasTutorialRow()
    {
        if (!HasTutorialRoot()) return false;
        foreach (var row in _rows)
        {
            var child = string.IsNullOrEmpty(row.path) ? _avatarRoot.transform : _avatarRoot.transform.Find(row.path);
            if (child == null) continue;
            if (row.propName == "m_IsActive") return true;
            var renderer = child.GetComponent<SkinnedMeshRenderer>();
            if (row.IsBlendShape && renderer != null && renderer.sharedMesh != null
                && renderer.sharedMesh.GetBlendShapeIndex(row.propName.Substring("blendShape.".Length)) >= 0) return true;
        }
        return false;
    }
}
