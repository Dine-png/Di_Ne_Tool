#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class DiNeRemoveMeshInBoxEditor
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[] tutorialSteps;

    private void EnsureTutorial()
    {
        if (tutorial == null) tutorial = new DiNeGuidedTutorial(this, "remove-mesh-in-box");
        if (tutorialSteps == null) tutorialSteps = new[]
        {
            DiNeTutorialStep.Required("renderer", "This component trims the mesh on the same object, for example body polygons hidden beneath clothing. Add it to an object with a SkinnedMeshRenderer or a MeshRenderer and MeshFilter.", "이 컴포넌트는 같은 오브젝트의 메시에서 의상 아래에 가려진 몸 같은 영역을 제거합니다. SkinnedMeshRenderer 또는 MeshRenderer와 MeshFilter가 있는 오브젝트에 추가하세요.", "このコンポーネントは同じ対象のメッシュから、衣装の下に隠れた体などの範囲を削除します。SkinnedMeshRenderer、またはMeshRendererとMeshFilterがある対象に追加してください。", HasTutorialRenderer),
            DiNeTutorialStep.Required("boxes", "Boxes define the mesh regions used by the removal test. Add a box and give all three size axes positive values so it covers the intended region.", "박스는 메시 제거 판정에 사용할 영역을 정합니다. 박스를 추가하고 세 축의 크기를 모두 0보다 크게 지정해 필요한 영역을 덮으세요.", "ボックスでメッシュ削除の判定範囲を決めます。ボックスを追加し、サイズの3軸をすべて正の値にして必要な範囲を覆ってください。", HasTutorialBox),
            DiNeTutorialStep.Optional("mode", "ON removes triangles whose three vertices are inside the boxed regions; OFF keeps those triangles and removes the rest. Choose whether you want to remove a hidden region or keep only the selected region.", "ON은 세 꼭짓점이 모두 박스 영역 안에 있는 삼각형을 제거하고, OFF는 그 삼각형만 남깁니다. 가려진 부분을 제거할지 지정한 영역만 남길지 선택하세요.", "ONは3頂点がすべてボックス範囲内にある三角形を削除し、OFFはその三角形だけを残します。隠れた部分を削除するか、選んだ範囲だけを残すか決めてください。"),
            DiNeTutorialStep.Optional("center", "Center positions the box relative to this renderer's local coordinates. Move it onto the region to trim; skinned meshes are tested using their stored bind-pose vertices.", "중심은 Renderer의 로컬 좌표를 기준으로 박스 위치를 정합니다. 제거할 곳으로 이동하세요. 스킨드 메시의 판정은 저장된 바인드 포즈의 꼭짓점을 기준으로 합니다.", "中心はRendererのローカル座標を基準にボックスの位置を決めます。切り取る範囲へ移動してください。スキンドメッシュは保存されたバインドポーズの頂点で判定します。"),
            DiNeTutorialStep.Optional("size", "Size controls how much of the mesh the box covers on each axis. Adjust it carefully at the edges: removal drops whole triangles rather than cutting new edges through them.", "크기는 각 축에서 박스가 덮는 메시 범위를 정합니다. 경계에서는 새 단면을 만드는 대신 삼각형 전체를 제거하므로 범위를 세밀하게 조절하세요.", "サイズは各軸でボックスが覆うメッシュ範囲を決めます。新しい切り口を作るのではなく三角形全体を削除するため、境界付近を丁寧に調整してください。"),
            DiNeTutorialStep.Optional("rotation", "Rotation aligns the box with an angled part, such as an arm or leg. Turn the box to cover that region without including nearby visible polygons.", "회전은 팔·다리처럼 기울어진 부위에 박스 방향을 맞춥니다. 주변의 보여야 할 폴리곤까지 포함하지 않도록 방향을 조절하세요.", "回転で腕や脚など傾いた部位にボックスの向きを合わせます。周囲の表示すべきポリゴンを含めないように調整してください。"),
            DiNeTutorialStep.Optional("scene", "Scene handles let you position and resize the region while looking at the mesh. Drag them in the Scene view and check the box boundary against the intended removal area.", "Scene 핸들로 메시를 보면서 영역의 위치와 크기를 조절할 수 있습니다. Scene 뷰에서 드래그하고 박스 경계가 제거할 부위에 맞는지 확인하세요.", "Sceneハンドルでメッシュを見ながら範囲の位置とサイズを調整できます。Sceneビューでドラッグし、境界が削除する部位に合っているか確認してください。"),
            DiNeTutorialStep.Optional("add", "Multiple boxes can cover separate regions on the same mesh. Press Add Box for another area, such as the other arm or leg.", "여러 박스로 같은 메시의 떨어진 영역들을 지정할 수 있습니다. 반대쪽 팔이나 다리처럼 추가할 부위가 있으면 박스 추가를 누르세요.", "複数のボックスで同じメッシュの離れた範囲を指定できます。反対側の腕や脚などを追加する場合はボックス追加を押してください。"),
            DiNeTutorialStep.Optional("delete", "The × removes a box from the region configuration. Delete any box that covers a part you no longer want processed.", "×는 제거 영역 설정에서 해당 박스를 지웁니다. 더 이상 처리하지 않을 부위를 덮고 있는 박스를 삭제하세요.", "×は範囲設定からそのボックスを削除します。処理する必要がなくなった部位を覆うボックスを削除してください。"),
            DiNeTutorialStep.Optional("automatic", "NDMF applies these regions to a copied mesh during avatar build or Apply on Play. Check the processed avatar to confirm the hidden polygons are gone and visible parts remain intact.", "NDMF는 아바타 빌드나 Apply on Play에서 복사한 메시에 이 영역 설정을 적용합니다. 처리된 아바타에서 가려진 폴리곤이 제거되고 보여야 할 부위가 유지되는지 확인하세요.", "NDMFはアバタービルドやApply on Playでコピーしたメッシュに範囲設定を適用します。処理後のアバターで隠れたポリゴンが削除され、表示する部位が保たれているか確認してください。")
        };
        tutorial.Configure("all", "Remove Mesh In Box", "박스 영역 제거", "Remove Mesh In Box", tutorialSteps,
            overviewEn: "Remove Mesh In Box removes mesh triangles selected by one or more boxes, for example body polygons hidden beneath clothing. You can remove the boxed region or keep only that region. NDMF processes a copied mesh at avatar build or Apply on Play, preserving the original mesh asset; the tool removes whole triangles instead of making new cut surfaces.",
            overviewKo: "Remove Mesh In Box는 하나 이상의 박스로 지정한 메시의 삼각형을 제거합니다. 의상 아래에 가려진 몸을 지우거나 지정한 영역만 남길 때 사용합니다. NDMF가 아바타 빌드나 Apply on Play에서 복사한 메시를 처리해 원본 메시 에셋을 보존하며, 새 단면을 만들지 않고 삼각형 전체를 제거합니다.",
            overviewJa: "Remove Mesh In Boxは1つ以上のボックスで指定したメッシュの三角形を削除します。衣装の下に隠れた体を消したり、指定範囲だけを残すときに使います。NDMFがアバタービルドやApply on Playでコピーしたメッシュを処理して元のアセットを保ち、新しい切り口を作らず三角形全体を削除します。");
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
