#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class DiNeShapeKeyFreezer
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[] tutorialSteps;
    private Vector2 tutorialScroll;

    private void EnsureTutorial()
    {
        if (tutorial == null) tutorial = new DiNeGuidedTutorial(this, "shape-key-freezer");
        if (tutorialSteps == null) tutorialSteps = new[]
        {
            DiNeTutorialStep.Required("target", "The target root is where the clip's object paths are resolved. Choose the scene avatar or object whose hierarchy matches the clip and contains the BlendShape mesh.", "대상 루트는 클립에 기록된 오브젝트 경로를 찾는 기준입니다. 클립과 계층 구조가 일치하고 쉐이프키 메시를 포함한 씬 아바타나 오브젝트를 지정하세요.", "対象ルートはクリップに記録されたオブジェクトのパスを探す基準です。クリップと階層が一致し、シェイプキーメッシュを含むシーンアバターやオブジェクトを指定してください。", HasTutorialTarget),
            DiNeTutorialStep.Required("clip", "The clip supplies the BlendShape values for a facial expression or other mesh pose. Select a clip whose object paths and BlendShape names match this target.", "클립에서 표정이나 메시 형태를 만드는 쉐이프키 값을 가져옵니다. 오브젝트 경로와 쉐이프키 이름이 대상과 일치하는 클립을 선택하세요.", "クリップから表情などのメッシュ形状を作るシェイプキー値を読み取ります。オブジェクトのパスとシェイプキー名が対象と一致するクリップを選んでください。", HasTutorialClip),
            DiNeTutorialStep.Optional("time", "The slider selects which moment of the clip to sample. Moving it immediately applies the matching BlendShape weights to the scene so you can choose the desired expression.", "슬라이더는 클립에서 값을 가져올 시점을 정합니다. 움직이면 일치하는 쉐이프키 값이 씬에 바로 반영되므로 원하는 표정을 골라 보세요.", "スライダーでクリップから読み取る時点を選びます。動かすと一致するシェイプキー値がシーンにすぐ反映されるので、使いたい表情を選んでください。"),
            DiNeTutorialStep.Optional("apply", "Save BlendShape Pose applies the selected moment's weights to the scene renderers. Press it to keep that expression on the object, then save the scene if you want to retain the change.", "쉐이프키 저장은 선택한 시점의 값을 씬 Renderer에 적용합니다. 눌러서 해당 표정을 오브젝트에 유지하고, 변경을 남기려면 씬을 저장하세요.", "シェイプキーを保存は選んだ時点の値をシーンのRendererに適用します。押して表情を対象に保持し、変更を残す場合はシーンを保存してください。"),
            DiNeTutorialStep.Optional("undo", "Both slider sampling and Save Pose change the scene's BlendShape weights with Undo support. Use Undo to step back through those changes if you want the previous expression.", "슬라이더 조절과 포즈 저장으로 바뀐 씬의 쉐이프키 값은 실행 취소할 수 있습니다. 이전 표정으로 돌아가려면 실행 취소로 적용한 변경을 되돌리세요.", "スライダーの操作とポーズ保存によるシーンのシェイプキー変更は元に戻せます。前の表情に戻す場合は、元に戻すで適用した変更を取り消してください。")
        };
        tutorial.Configure("all", "ShapeKey Freezer", "쉐이프키 고정", "ShapeKey Freezer", tutorialSteps,
            overviewEn: "ShapeKey Freezer takes the BlendShape values at a chosen moment in an Animation Clip and applies them to a scene object. Use it to keep a facial expression or mesh shape without playing the whole animation. The slider applies values immediately, and Save Pose applies the selected values again; bone transforms and the source clip stay unchanged.",
            overviewKo: "ShapeKey Freezer는 Animation Clip의 원하는 시점에서 쉐이프키 값을 가져와 씬 오브젝트에 적용합니다. 애니메이션 전체를 재생하지 않고 특정 표정이나 메시 형태를 유지할 때 사용합니다. 슬라이더를 움직이면 값이 즉시 반영되고 포즈 저장은 선택한 값을 다시 적용합니다. 본 Transform과 원본 클립은 유지됩니다.",
            overviewJa: "ShapeKey FreezerはAnimation Clipの選んだ時点からシェイプキー値を取り出し、シーンの対象に適用します。アニメーション全体を再生せずに、特定の表情やメッシュ形状を保持するときに使います。スライダーで値をすぐ反映し、ポーズ保存で選んだ値を再適用します。ボーンのTransformと元のクリップは維持されます。");
    }

    private bool HasTutorialTarget()
    {
        if (targetObject == null || EditorUtility.IsPersistent(targetObject) || !targetObject.scene.IsValid()) return false;
        foreach (var renderer in targetObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0) return true;
        return false;
    }

    private bool HasTutorialClip()
    {
        if (!HasTutorialTarget() || animationClip == null) return false;
        foreach (var binding in AnimationUtility.GetCurveBindings(animationClip))
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith("blendShape.")) continue;
            var child = string.IsNullOrEmpty(binding.path) ? targetObject.transform : targetObject.transform.Find(binding.path);
            var renderer = child != null ? child.GetComponent<SkinnedMeshRenderer>() : null;
            if (renderer != null && renderer.sharedMesh != null
                && renderer.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring("blendShape.".Length)) >= 0
                && AnimationUtility.GetEditorCurve(animationClip, binding) != null) return true;
        }
        return false;
    }
}
#endif
