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
            DiNeTutorialStep.Required("ndmf", "NDMF runs Opticore's automatic avatar build processing. Install it so the selected optimization options can be applied during upload.", "NDMF는 Opticore의 자동 아바타 빌드 처리를 실행합니다. 선택한 최적화가 업로드에 적용될 수 있도록 먼저 설치하세요.", "NDMFがOpticoreの自動アバタービルド処理を実行します。選んだ最適化をアップロード時に適用できるよう、先にインストールしてください。", HasRequiredNDMF),
            DiNeTutorialStep.Required("avatar", "Opticore processes the avatar containing this component. Place it under the scene avatar's VRCAvatarDescriptor to define the optimization target.", "Opticore는 이 컴포넌트를 포함한 아바타를 처리합니다. 씬 아바타의 VRCAvatarDescriptor 아래에 배치해 최적화 대상을 지정하세요.", "Opticoreはこのコンポーネントを含むアバターを処理します。シーンアバターのVRCAvatarDescriptorの下に配置し、最適化対象を決めてください。", HasTutorialAvatar),
            DiNeTutorialStep.Required("modules", "Each option selects a different cleanup pass, such as meshes, materials, bones or PhysBones. Enable at least one available option for the part you want to reduce.", "각 항목은 메시·머티리얼·본·PhysBone 등 서로 다른 부분을 정리합니다. 줄이고 싶은 부분의 사용 가능한 최적화 항목을 하나 이상 켜세요.", "各項目はメッシュ・マテリアル・ボーン・PhysBoneなど別々の部分を整理します。負荷を減らしたい部分の利用可能な項目を1つ以上ONにしてください。", HasTutorialModule, onEnter: () => _showGeometry = true),
            DiNeTutorialStep.Optional("_optimizeMeshes", "Mesh optimization freezes eligible BlendShapes, removes collapsed triangles and merges compatible skinned meshes. Enable it to reduce mesh and renderer overhead, then check the resulting appearance in Play Mode.", "메시 최적화는 가능한 쉐이프키를 고정하고 겹친 꼭짓점의 삼각형을 정리하며 호환되는 스킨드 메시를 병합합니다. 메시와 Renderer 부담을 줄이려면 켜고 Play Mode에서 외형을 확인하세요.", "メッシュ最適化は適用可能なシェイプキーを固定し、頂点が重なった三角形を整理して互換性のあるスキンドメッシュを結合します。メッシュやRendererの負荷を減らす場合はONにし、Play Modeで見た目を確認してください。", () => _showGeometry = true),
            DiNeTutorialStep.Optional("_optimizeMaterials", "Material optimization removes extra or duplicate slots, empty submeshes and unused material properties. Enable it to reduce redundant material setup while preserving the materials still used for rendering.", "머티리얼 최적화는 여분·중복 슬롯, 빈 서브메시와 미사용 머티리얼 속성을 정리합니다. 렌더링에 필요한 머티리얼을 유지하면서 중복 구성을 줄이려면 켜세요.", "マテリアル最適化は余分・重複スロット、空のサブメッシュ、未使用のマテリアルプロパティを整理します。描画に必要なマテリアルを保ち、重複した構成を減らす場合はONにしてください。", () => _showMaterials = true),
            DiNeTutorialStep.Optional("material-tool", "Texture resolution and compression are configured in Material Tool. Open it if texture memory needs attention as well as Opticore's material-slot cleanup.", "텍스처 해상도와 압축은 Material Tool에서 설정합니다. Opticore의 슬롯 정리와 함께 텍스처 메모리도 조절할 필요가 있으면 여세요.", "テクスチャの解像度と圧縮はMaterial Toolで設定します。Opticoreのスロット整理に加え、テクスチャメモリも調整したい場合に開いてください。", () => _showMaterials = true),
            DiNeTutorialStep.Optional("_optimizeRigAndBones", "Rig cleanup removes unreferenced leaf bones and collapses eligible intermediate bones. Enable it to simplify the hierarchy while protecting required renderer and humanoid references.", "리그 정리는 참조되지 않는 끝 본을 지우고 정리 가능한 중간 본을 합칩니다. 필요한 Renderer·휴머노이드 참조를 보호하면서 계층을 단순하게 만들려면 켜세요.", "リグ整理は未参照の末端ボーンを削除し、整理可能な中間ボーンをまとめます。必要なRenderer・Humanoid参照を保護しながら階層を簡素化する場合はONにしてください。", () => _showRig = true),
            DiNeTutorialStep.Optional("_optimizePhysBones", "PhysBone cleanup removes invalid or duplicate references and merges compatible duplicate colliders. Enable it to reduce unnecessary physics setup, then check hair and accessory motion in Play Mode.", "PhysBone 정리는 잘못된·중복 참조를 제거하고 호환되는 중복 콜라이더를 합칩니다. 불필요한 물리 설정을 줄이려면 켜고 Play Mode에서 머리카락과 액세서리의 움직임을 확인하세요.", "PhysBone整理は無効・重複参照を削除し、互換性のある重複コライダーをまとめます。不要な物理設定を減らす場合はONにし、Play Modeで髪やアクセサリーの揺れを確認してください。", () => _showRig = true),
            DiNeTutorialStep.Optional("_optimizeAnimator", "Animator optimization is currently marked Soon and does not run in this version. Check the badge when choosing available modules; the other optimization options can be used independently.", "Animator 최적화는 현재 예정으로 표시되며 이 버전에서는 실행되지 않습니다. 사용할 항목을 고를 때 상태 표시를 확인하세요. 다른 최적화 항목은 각각 사용할 수 있습니다.", "Animator最適化は現在「予定」で、このバージョンでは実行されません。使う項目を選ぶ際に表示を確認してください。ほかの最適化はそれぞれ利用できます。", () => _showRig = true),
            DiNeTutorialStep.Optional("_removeUnusedObjects", "Unused-object cleanup removes eligible empty hierarchy objects and missing-script remnants from the processed avatar. Enable it to reduce leftover structure after the other passes.", "미사용 오브젝트 정리는 처리할 아바타에서 정리 가능한 빈 계층 오브젝트와 Missing Script 잔여물을 제거합니다. 다른 최적화 뒤에 남은 불필요한 구조를 줄이려면 켜세요.", "未使用オブジェクト整理は処理用アバターから整理可能な空の階層対象やMissing Scriptの残りを削除します。ほかの処理後に残る不要な構造を減らす場合はONにしてください。", () => _showCleanup = true),
            DiNeTutorialStep.Optional("_preserveAvatarBehavior", "Preserve Avatar Behavior protects referenced and animated transforms during cleanup. Keep it enabled when you want the optimizer to retain objects needed by existing avatar behavior.", "아바타 동작 유지는 정리 중 참조되거나 애니메이션되는 Transform을 보호합니다. 기존 아바타 동작에 필요한 오브젝트를 유지하려면 켜 두세요.", "アバターの振る舞い維持は整理中に参照・アニメーションされるTransformを保護します。既存のアバター動作に必要な対象を保つ場合はONのままにしてください。", () => _showCleanup = true),
            DiNeTutorialStep.Optional("_experimentalMode", "Experimental Mode permits additional merges, including skinned meshes with BlendShapes or BlendShape animation. Enable it only when you need those passes and can check expressions and toggles on the processed avatar.", "실험 모드는 쉐이프키가 있거나 쉐이프키가 애니메이션되는 스킨드 메시 등 추가 병합을 허용합니다. 그 처리가 필요하고 처리된 아바타의 표정과 토글을 확인할 수 있을 때 켜세요.", "実験モードはシェイプキーやそのアニメーションを持つスキンドメッシュなどの追加結合を許可します。その処理が必要で、処理後の表情やトグルを確認できる場合にONにしてください。", () => _showCleanup = true),
            DiNeTutorialStep.Optional("automatic", "Upload or enter Play Mode to apply enabled options to a temporary avatar copy automatically. Check appearance, expressions and movement on that copy before relying on the optimized result.", "업로드나 Play Mode 진입 시 켠 항목이 임시 아바타 복사본에 자동 적용됩니다. 최적화 결과를 사용하기 전에 복사본의 외형·표정·움직임을 확인하세요.", "アップロードやPlay Mode開始時にONにした項目を一時アバターコピーへ自動適用します。最適化結果を使う前に、コピーの見た目・表情・動きを確認してください。")
        };
        tutorial.Configure("all", "Opticore", "Opticore", "Opticore", tutorialSteps,
            overviewEn: "Opticore automatically cleans up VRChat avatars to reduce unnecessary mesh, material, bone and PhysBone work. Choose the passes you need, such as compatible mesh merging, duplicate material-slot cleanup or unused-bone removal. Enabled passes run on a temporary avatar copy during upload or in Play Mode, where you can check the result before use.",
            overviewKo: "Opticore는 VRChat 아바타를 자동으로 정리해 불필요한 메시·머티리얼·본·PhysBone 부담을 줄입니다. 호환되는 메시 병합, 중복 머티리얼 슬롯 정리, 미사용 본 제거처럼 필요한 항목을 선택해 사용할 수 있습니다. 켠 항목은 업로드나 Play Mode에서 임시 아바타 복사본에 적용되므로 결과를 확인한 뒤 사용할 수 있습니다.",
            overviewJa: "OpticoreはVRChatアバターを自動整理し、不要なメッシュ・マテリアル・ボーン・PhysBoneの負荷を減らします。互換性のあるメッシュ結合、重複マテリアルスロット整理、未使用ボーン削除など必要な処理を選べます。ONにした処理はアップロードやPlay Modeで一時アバターコピーに適用され、結果を確認できます。");
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
