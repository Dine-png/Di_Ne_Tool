using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public partial class DiNeOpticoreEditor
{
    private DiNeGuidedTutorial tutorial;
    private DiNeTutorialStep[] tutorialSteps;
    private Rect tutorialSectionRect;

    private void EnsureTutorial()
    {
        if (tutorial == null) tutorial = new DiNeGuidedTutorial(this, "opticore");
        if (tutorialSteps == null) tutorialSteps = new[]
        {
            DiNeTutorialStep.Required("ndmf", "Install NDMF before setting up Opticore.", "Opticore를 설정하기 전에 NDMF를 설치하세요.", "Opticoreを設定する前にNDMFをインストールしてください。", HasRequiredNDMF),
            DiNeTutorialStep.Required("avatar", "Place this component inside your avatar.", "이 컴포넌트를 아바타 안에 배치하세요.", "このコンポーネントをアバター内に配置してください。", HasTutorialAvatar),
            DiNeTutorialStep.Required("modules", "Enable at least one optimization option.", "최적화 항목을 하나 이상 켜세요.", "最適化項目を1つ以上ONにしてください。", HasTutorialModule, onEnter: () => _showGeometry = true),
            DiNeTutorialStep.Optional("_optimizeMeshes", "Choose whether to optimize meshes.", "메시를 최적화할지 선택하세요.", "メッシュを最適化するか選んでください。", () => _showGeometry = true),
            DiNeTutorialStep.Optional("_optimizeMaterials", "Choose whether to clean up material slots.", "머티리얼 슬롯을 정리할지 선택하세요.", "マテリアルスロットを整理するか選んでください。", () => _showMaterials = true),
            DiNeTutorialStep.Optional("material-tool", "Open Material Tool to adjust textures.", "텍스처를 조정하려면 Material Tool을 여세요.", "テクスチャを調整する場合はMaterial Toolを開いてください。", () => _showMaterials = true),
            DiNeTutorialStep.Optional("_optimizeRigAndBones", "Choose whether to optimize the rig and bones.", "리그와 본을 최적화할지 선택하세요.", "リグとボーンを最適化するか選んでください。", () => _showRig = true),
            DiNeTutorialStep.Optional("_optimizePhysBones", "Choose whether to clean up PhysBones.", "PhysBone을 정리할지 선택하세요.", "PhysBoneを整理するか選んでください。", () => _showRig = true),
            DiNeTutorialStep.Optional("_optimizeAnimator", "Check the availability badge for Animator optimization.", "Animator 최적화의 사용 가능 표시를 확인하세요.", "Animator最適化の利用状況を確認してください。", () => _showRig = true),
            DiNeTutorialStep.Optional("_removeUnusedObjects", "Choose whether to remove unused objects at build.", "빌드 시 미사용 오브젝트를 정리할지 선택하세요.", "ビルド時に未使用オブジェクトを整理するか選んでください。", () => _showCleanup = true),
            DiNeTutorialStep.Optional("_preserveAvatarBehavior", "Keep this enabled to preserve avatar behavior.", "아바타 동작을 유지하려면 켜 두세요.", "アバターの動作を維持する場合はONのままにしてください。", () => _showCleanup = true),
            DiNeTutorialStep.Optional("_experimentalMode", "Enable Experimental Mode only if you need its extra options.", "추가 항목이 필요할 때만 실험 모드를 켜세요.", "追加項目が必要な場合だけ実験モードをONにしてください。", () => _showCleanup = true),
            DiNeTutorialStep.Optional("automatic", "Upload or enter Play Mode when you are ready.", "준비가 되면 업로드하거나 Play Mode에 들어가세요.", "準備ができたらアップロードするかPlay Modeに入ってください。")
        };
        tutorial.Configure("all", "Opticore", "Opticore", "Opticore", tutorialSteps);
    }

    private bool HasTutorialAvatar() => target is DiNeOpticore item && !EditorUtility.IsPersistent(item)
        && item.gameObject.scene.IsValid() && item.GetComponentInParent<VRCAvatarDescriptor>(true) != null;

    private bool HasTutorialModule() => _optimizeMeshes.boolValue || _optimizeMaterials.boolValue
        || _optimizeRigAndBones.boolValue || _optimizePhysBones.boolValue || _removeUnusedObjects.boolValue;

    private void TutorialCollapsed(params string[] ids)
    {
        foreach (string id in ids) tutorial.Draw(id, tutorialSectionRect);
    }
}
