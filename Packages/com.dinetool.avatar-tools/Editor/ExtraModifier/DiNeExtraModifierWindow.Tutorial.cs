using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiNeTool.ExtraModifier.Editor
{
    internal sealed partial class DiNeExtraModifierWindow
    {
        private DiNeGuidedTutorial guidedTutorial;
        private GameObject tutorialAnalyzedSource, tutorialAnalyzedTarget;

        private void OnGUI()
        {
            ConfigureTutorial();
            guidedTutorial.BeginFrame();
            try { DrawToolGUI(); guidedTutorial.Validate(); }
            catch (ExitGUIException) { guidedTutorial.AbortFrame(); throw; }
            finally { guidedTutorial.EndFrame(); }
        }

        private void ConfigureTutorial()
        {
            if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "ExtraModifier");
            if (feature == Feature.Transplant)
            {
                guidedTutorial.Configure("Transplant", "Prefab functionality", "프리팹 기능 이식", "プレハブ機能の移植", new[]
                {
                    DiNeTutorialStep.Required("Source", "Assign the original object to copy functionality from.", "기능을 가져올 원본 오브젝트를 넣으세요.", "機能をコピーする元のオブジェクトを指定してください。", () => transplantSource != null && PrefabStageUtility.GetPrefabStage(transplantSource) == null && (EditorUtility.IsPersistent(transplantSource) || TutorialSceneObject(transplantSource))),
                    DiNeTutorialStep.Required("Target", "Assign the revised model from a separate hierarchy.", "원본과 다른 계층의 수정 모델을 넣으세요.", "元と別の階層にある修正済みモデルを指定してください。", TutorialTransplantInputs),
                    DiNeTutorialStep.Optional("Rules", "Read which data will be transplanted.", "이식할 데이터의 범위를 확인하세요.", "移植するデータの範囲を確認してください。"),
                    DiNeTutorialStep.Required("Analyze", "Press Analyze to inspect matching objects.", "분석을 눌러 오브젝트 대응 상태를 확인하세요.", "分析を押してオブジェクトの対応を確認してください。", () => TutorialTransplantInputs() && transplantReport != null && transplantReport.Succeeded && tutorialAnalyzedSource == transplantSource && tutorialAnalyzedTarget == transplantTarget),
                    DiNeTutorialStep.Optional("Report", "Read the analysis counts and warnings.", "분석 개수와 경고를 확인하세요.", "分析の件数と警告を確認してください。"),
                    DiNeTutorialStep.Optional("Details", "Expand details to inspect skipped items and references.", "상세를 펼쳐 건너뛴 항목과 참조를 확인하세요.", "詳細を開いてスキップした項目と参照を確認してください。"),
                    DiNeTutorialStep.Optional("Execute", "Press Execute to transplant functionality.", "기능을 이식하려면 실행을 누르세요.", "機能を移植するには、実行を押してください。"),
                    DiNeTutorialStep.Optional("SelectResult", "Press Select Target to reveal the result.", "대상 선택을 눌러 결과 오브젝트를 확인하세요.", "対象を選択して結果のオブジェクトを確認してください。")
                });
                return;
            }
            guidedTutorial.Configure("Vrm", "VRM preparation", "VRM 준비", "VRM準備", new[]
            {
                DiNeTutorialStep.Required("Avatar", "Assign the avatar root from the Hierarchy.", "Hierarchy에서 아바타 루트를 넣으세요.", "Hierarchyからアバターのルートを指定してください。", () => TutorialSceneObject(vrmAvatar)),
                DiNeTutorialStep.Optional("WorkingCopy", "Press this button to create a working copy.", "작업 복사본을 만들려면 이 버튼을 누르세요.", "作業用コピーを作るには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("AutoOptions", "Choose the operations for automatic preparation.", "자동 처리에 포함할 작업을 고르세요.", "自動処理に含める作業を選んでください。"),
                DiNeTutorialStep.Optional("AutoApply", "Press this button to run automatic preparation.", "전체 자동 처리를 실행하려면 이 버튼을 누르세요.", "全自動処理を実行するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("MergeBones", "Press this button to merge outfit bones.", "의상 본을 병합하려면 이 버튼을 누르세요.", "衣装ボーンを統合するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("PhysMode", "Choose whether to convert, delete, or keep PhysBones.", "PhysBone을 변환·삭제·유지 중 어떻게 처리할지 고르세요.", "PhysBoneを変換・削除・維持のどれで処理するか選んでください。"),
                DiNeTutorialStep.Optional("PhysApply", physBoneMode == DiNeVrmPhysBoneMode.Keep ? "Check that PhysBones will be kept." : "Press the action below for the chosen PhysBone mode.", physBoneMode == DiNeVrmPhysBoneMode.Keep ? "PhysBone 유지 상태를 확인하세요." : "선택한 PhysBone 모드를 실행하려면 아래 버튼을 누르세요.", physBoneMode == DiNeVrmPhysBoneMode.Keep ? "PhysBoneの維持状態を確認してください。" : "選んだPhysBoneモードを実行するには、下のボタンを押してください。"),
                DiNeTutorialStep.Optional("Cleanup", "Press this button to clean up VRChat components.", "VRChat 컴포넌트를 정리하려면 이 버튼을 누르세요.", "VRChatコンポーネントを整理するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("MaterialFeatures", "Check the material features to keep.", "유지할 머티리얼 효과를 체크하세요.", "残すマテリアル効果をチェックしてください。"),
                DiNeTutorialStep.Optional("MaterialPresets", "Use Keep All / Drop All to change feature choices together.", "모두 유지·모두 제거로 효과 선택을 한꺼번에 바꾸세요.", "すべて維持・すべて破棄で効果の選択をまとめて変更してください。"),
                DiNeTutorialStep.Optional("SaveMaterials", "Choose whether to save converted materials as assets.", "변환한 머티리얼을 에셋으로 저장할지 정하세요.", "変換したマテリアルをアセットに保存するか選んでください。"),
                DiNeTutorialStep.Optional("ConvertMaterials", "Press this button to convert materials to MToon.", "머티리얼을 MToon으로 바꾸려면 이 버튼을 누르세요.", "マテリアルをMToonに変換するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("EmptyObjects", "Press this button to clean up empty objects.", "빈 오브젝트를 정리하려면 이 버튼을 누르세요.", "空のオブジェクトを整理するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Preflight", "Press Preflight to inspect export readiness.", "사전 점검을 눌러 내보내기 준비 상태를 확인하세요.", "事前チェックを押して書き出しの準備状況を確認してください。"),
                DiNeTutorialStep.Optional("FixIssues", preflightIssues != null && preflightIssues.Count > 0 ? "Press Fix beside the issue to correct it." : "Press Preflight to list issues.", preflightIssues != null && preflightIssues.Count > 0 ? "수정할 문제 옆의 고치기를 누르세요." : "문제를 확인하려면 사전 점검을 누르세요.", preflightIssues != null && preflightIssues.Count > 0 ? "修正する問題の横にある修正を押してください。" : "問題を確認するには事前チェックを押してください。"),
                DiNeTutorialStep.Optional("Freeze", "Press this button to freeze the T-Pose.", "T-Pose를 고정하려면 이 버튼을 누르세요.", "T-Poseを固定するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("MeshUtility", "Press this button to open the mesh tools.", "메시 도구를 열려면 이 버튼을 누르세요.", "メッシュツールを開くには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Export0", "Press VRM 0.x Export.", "VRM 0.x 내보내기를 누르세요.", "VRM 0.x書き出しを押してください。"),
                DiNeTutorialStep.Optional("Export1", DiNeUniVrmBridge.HasVrm1 ? "Press VRM 1.0 Export." : "Install UniVRM 1.0 to enable its exporter.", DiNeUniVrmBridge.HasVrm1 ? "VRM 1.0 내보내기를 누르세요." : "VRM 1.0 내보내기를 사용하려면 UniVRM 1.0을 설치하세요.", DiNeUniVrmBridge.HasVrm1 ? "VRM 1.0書き出しを押してください。" : "VRM 1.0書き出しを使うにはUniVRM 1.0をインストールしてください。"),
                DiNeTutorialStep.Optional("VrChatConverter", DiNeUniVrmBridge.HasVrmConverterForVrChat ? "Press this button to open the VRChat converter." : "Install the UniVRM VRChat converter to enable this action.", DiNeUniVrmBridge.HasVrmConverterForVrChat ? "VRChat 변환기를 열려면 이 버튼을 누르세요." : "이 기능을 사용하려면 UniVRM의 VRChat 변환기를 설치하세요.", DiNeUniVrmBridge.HasVrmConverterForVrChat ? "VRChat変換ツールを開くには、このボタンを押してください。" : "この機能を使うにはUniVRMのVRChat変換ツールをインストールしてください。"),
                DiNeTutorialStep.Optional("Status", "Read the operation result here.", "여기서 작업 결과를 확인하세요.", "ここで処理結果を確認してください。")
            });
        }

        private static bool TutorialSceneObject(GameObject value)
        {
            return value != null && !EditorUtility.IsPersistent(value) && value.scene.IsValid() && value.scene.isLoaded &&
                !EditorSceneManager.IsPreviewScene(value.scene) && PrefabStageUtility.GetPrefabStage(value) == null;
        }

        private bool TutorialTransplantInputs()
        {
            return transplantSource != null && TutorialSceneObject(transplantTarget) && !EditorApplication.isPlayingOrWillChangePlaymode &&
                transplantSource != transplantTarget && !transplantSource.transform.IsChildOf(transplantTarget.transform) &&
                !transplantTarget.transform.IsChildOf(transplantSource.transform);
        }
    }

    internal sealed partial class DiNeFocusEditor
    {
        private DiNeGuidedTutorial guidedTutorial;
        private void ConfigureTutorial()
        {
            if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "LegacyFocus");
            guidedTutorial.Configure("Focus", "Legacy Focus", "기존 Focus", "従来のFocus", new[]
            {
                DiNeTutorialStep.Optional("Info", "Read the current Focus component status.", "현재 Focus 컴포넌트의 상태를 확인하세요.", "現在のFocusコンポーネントの状態を確認してください。"),
                DiNeTutorialStep.Optional("Build", "Read the build-processing notice.", "빌드 적용 안내를 확인하세요.", "ビルド処理の案内を確認してください。"),
                DiNeTutorialStep.Optional("Removal", "Use Remove Component in the component menu to remove Focus.", "제거하려면 컴포넌트 메뉴의 Remove Component를 선택하세요.", "削除するには、コンポーネントメニューのRemove Componentを選んでください。")
            });
        }
        private void OnDisable() => guidedTutorial?.Suspend();
    }
}
