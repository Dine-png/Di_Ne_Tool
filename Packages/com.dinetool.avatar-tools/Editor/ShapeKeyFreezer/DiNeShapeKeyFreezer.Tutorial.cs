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
            DiNeTutorialStep.Required("target", "Choose the scene object containing your BlendShape mesh.", "쉐이프키 메시가 있는 씬 오브젝트를 선택하세요.", "シェイプキーメッシュのあるシーンオブジェクトを選んでください。", HasTutorialTarget),
            DiNeTutorialStep.Required("clip", "Choose a clip with BlendShapes that match this object.", "이 오브젝트에 맞는 쉐이프키 클립을 선택하세요.", "このオブジェクトに合うシェイプキーのクリップを選んでください。", HasTutorialClip),
            DiNeTutorialStep.Optional("time", "Move the slider to choose a pose.", "슬라이더를 움직여 포즈를 선택하세요.", "スライダーを動かしてポーズを選んでください。"),
            DiNeTutorialStep.Optional("apply", "Press Save BlendShape Pose to apply the current pose.", "현재 포즈를 적용하려면 쉐이프키 저장을 누르세요.", "現在のポーズを適用する場合はシェイプキーを保存を押してください。"),
            DiNeTutorialStep.Optional("undo", "Use Undo if you want to revert the pose.", "포즈를 되돌리려면 실행 취소를 사용하세요.", "ポーズを戻す場合は元に戻すを使ってください。")
        };
        tutorial.Configure("all", "ShapeKey Freezer", "쉐이프키 고정", "ShapeKey Freezer", tutorialSteps);
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
