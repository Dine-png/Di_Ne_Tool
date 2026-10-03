#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class DiNeRemoveMeshInBoxEditor
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[] tutorialSteps;
    private GUIStyle tutorialTitleStyle, tutorialDescriptionStyle;

    private void EnsureTutorial()
    {
        if (tutorial == null) tutorial = new DiNeGuidedTutorial(this, "remove-mesh-in-box");
        if (tutorialSteps == null) tutorialSteps = new[]
        {
            DiNeTutorialStep.Required("renderer", "Add this component to the mesh object you want to trim.", "잘라낼 메시 오브젝트에 이 컴포넌트를 추가하세요.", "切り取るメッシュのオブジェクトにこのコンポーネントを追加してください。", HasTutorialRenderer),
            DiNeTutorialStep.Required("boxes", "Add a box with a size greater than zero.", "크기가 0보다 큰 박스를 추가하세요.", "サイズが0より大きいボックスを追加してください。", HasTutorialBox),
            DiNeTutorialStep.Optional("mode", "Choose whether to remove polygons inside or outside the boxes.", "박스 안과 밖 중 제거할 쪽을 선택하세요.", "ボックスの内側と外側のどちらを削除するか選んでください。"),
            DiNeTutorialStep.Optional("center", "Move the box by changing Center.", "중심 값을 바꿔 박스를 이동하세요.", "中心の値を変更してボックスを移動してください。"),
            DiNeTutorialStep.Optional("size", "Adjust Size to cover the region.", "영역에 맞게 크기를 조절하세요.", "範囲に合わせてサイズを調整してください。"),
            DiNeTutorialStep.Optional("rotation", "Change Rotation to turn the box.", "회전 값을 바꿔 박스를 돌리세요.", "回転の値を変更してボックスを回してください。"),
            DiNeTutorialStep.Optional("scene", "Drag the box handles in the Scene view.", "Scene 뷰에서 박스 핸들을 드래그하세요.", "Sceneビューでボックスのハンドルをドラッグしてください。"),
            DiNeTutorialStep.Optional("add", "Press Add Box for another region.", "영역을 더 지정하려면 박스 추가를 누르세요.", "範囲を追加する場合はボックス追加を押してください。"),
            DiNeTutorialStep.Optional("delete", "Press × to remove a box you no longer need.", "필요 없는 박스는 ×를 눌러 삭제하세요.", "不要なボックスは×を押して削除してください。"),
            DiNeTutorialStep.Optional("automatic", "Enter Play Mode or build when you are ready.", "준비가 되면 Play Mode에 들어가거나 빌드하세요.", "準備ができたらPlay Modeに入るかビルドしてください。")
        };
        tutorial.Configure("all", "Remove Mesh In Box", "박스 영역 제거", "Remove Mesh In Box", tutorialSteps);
    }

    private bool HasTutorialRenderer()
    {
        var item = target as DiNeRemoveMeshInBox;
        if (item == null || EditorUtility.IsPersistent(item) || !item.gameObject.scene.IsValid()) return false;
        var skinned = item.GetComponent<SkinnedMeshRenderer>();
        if (skinned != null) return skinned.sharedMesh != null;
        var mesh = item.GetComponent<MeshFilter>();
        return item.GetComponent<MeshRenderer>() != null && mesh != null && mesh.sharedMesh != null;
    }

    private bool HasTutorialBox()
    {
        var item = target as DiNeRemoveMeshInBox;
        if (item == null || item.Boxes == null) return false;
        foreach (var box in item.Boxes)
            if (PositiveFinite(box.size.x) && PositiveFinite(box.size.y) && PositiveFinite(box.size.z)
                && Finite(box.center.x) && Finite(box.center.y) && Finite(box.center.z)
                && Finite(box.rotation.x) && Finite(box.rotation.y) && Finite(box.rotation.z) && Finite(box.rotation.w)
                && box.rotation.x * box.rotation.x + box.rotation.y * box.rotation.y + box.rotation.z * box.rotation.z + box.rotation.w * box.rotation.w > 0f) return true;
        return false;
    }

    private static bool PositiveFinite(float value) => value > 0f && Finite(value);
    private static bool Finite(float value) => !float.IsInfinity(value) && !float.IsNaN(value);
}
#endif
