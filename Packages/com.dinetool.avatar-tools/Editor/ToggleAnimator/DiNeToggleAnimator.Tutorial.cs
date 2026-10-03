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
            DiNeTutorialStep.Required("clips", "Select the Animation Clips you want to edit in the Project window.", "Project 창에서 편집할 Animation Clip을 선택하세요.", "Projectウィンドウで編集するAnimation Clipを選んでください。", () => _clips.Count > 0 && _clips.All(clip => clip != null)),
            DiNeTutorialStep.Optional("lock", "Enable Lock Selection to keep these clips selected.", "클립 목록을 유지하려면 선택 유지를 켜세요.", "クリップ一覧を維持する場合は選択維持をONにしてください。"),
            DiNeTutorialStep.Required("root", "Choose the scene avatar used by these clips.", "이 클립을 사용할 씬 아바타를 선택하세요.", "このクリップを使うシーンアバターを選んでください。", HasTutorialRoot),
            DiNeTutorialStep.Required("rows", "Drag an object from this avatar into the row area.", "이 아바타의 오브젝트를 행 추가 영역으로 드래그하세요.", "このアバターのオブジェクトを行追加エリアへドラッグしてください。", HasTutorialRow),
            DiNeTutorialStep.Optional("preview", "Press a clip name to preview its scene values.", "씬에서 값을 확인하려면 클립 이름을 누르세요.", "シーンで値を確認する場合はクリップ名を押してください。"),
            DiNeTutorialStep.Optional("undo-preview", "Use Undo to revert the applied scene values.", "적용한 씬 값을 되돌리려면 실행 취소를 사용하세요.", "適用したシーンの値を戻す場合は元に戻すを使ってください。"),
            DiNeTutorialStep.Optional("unset", "Add a row, then press — to save a value in the clip.", "행을 추가한 뒤 —를 눌러 클립에 값을 저장하세요.", "行を追加して—を押すとクリップに値を保存できます。"),
            DiNeTutorialStep.Optional("toggle", "Press — if needed, then use ON/OFF to save the object's state.", "필요하면 —를 누른 뒤 ON/OFF로 오브젝트 상태를 저장하세요.", "必要なら—を押してからON/OFFでオブジェクトの状態を保存してください。"),
            DiNeTutorialStep.Optional("clear", "Set an ON/OFF value, then right-click it to remove the curve.", "ON/OFF 값을 지정한 뒤 우클릭해 커브를 지우세요.", "ON/OFFの値を設定して右クリックでカーブを削除してください。"),
            DiNeTutorialStep.Optional("shape", "Select a clip with BlendShape curves, then edit a BlendShape value.", "쉐이프키 커브가 있는 클립을 선택한 뒤 값을 수정하세요.", "シェイプキーのカーブがあるクリップを選んで値を変更してください。"),
            DiNeTutorialStep.Optional("shape-clear", "Set a BlendShape value, then press its × to remove the curve.", "쉐이프키 값을 지정한 뒤 옆의 ×를 눌러 커브를 지우세요.", "シェイプキーの値を設定して横の×でカーブを削除してください。"),
            DiNeTutorialStep.Optional("delete-row", "Press the row's × to remove its curves from all selected clips.", "행의 ×를 눌러 선택한 모든 클립에서 해당 커브를 지우세요.", "行の×を押して選択中の全クリップからそのカーブを削除してください。"),
            DiNeTutorialStep.Optional("fill", "Press Fill Missing → OFF to save OFF in all unset cells.", "미지정 → OFF를 눌러 빈 셀에 OFF를 저장하세요.", "未設定→OFFを押して空のセルにOFFを保存してください。"),
            DiNeTutorialStep.Optional("smart-fill", "Press Smart Fill to fill the other clips with OFF where needed.", "Smart Fill을 눌러 필요한 다른 클립의 셀에 OFF를 채우세요.", "Smart Fillを押して必要な別クリップのセルにOFFを入れてください。"),
            DiNeTutorialStep.Optional("invert", "Press Invert All to save the opposite values.", "전체 반전을 눌러 반대 값을 저장하세요.", "全て反転を押して反対の値を保存してください。")
        };
        tutorial.Configure("all", "Toggle Animator", "토글 애니메이터", "Toggle Animator", tutorialSteps);
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
