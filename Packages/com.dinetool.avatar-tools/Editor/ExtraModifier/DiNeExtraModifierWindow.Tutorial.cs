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
                    DiNeTutorialStep.Required("Source", "The source contains settings to recover after editing a model, such as Modular Avatar, PhysBones, colliders and constraints. Assign the original object to copy functionality from.", "원본에는 모델 수정 후 다시 붙일 Modular Avatar·PhysBone·콜라이더·Constraint 등의 설정이 들어 있습니다. 기능을 가져올 원본 오브젝트를 넣으세요.", "元データにはモデル編集後に引き継ぐModular Avatar・PhysBone・コライダー・Constraintなどの設定が入っています。 機能をコピーする元のオブジェクトを指定してください。", () => transplantSource != null && PrefabStageUtility.GetPrefabStage(transplantSource) == null && (EditorUtility.IsPersistent(transplantSource) || TutorialSceneObject(transplantSource))),
                    DiNeTutorialStep.Required("Target", "The target receives those settings while retaining its edited meshes and existing object placement. Assign the revised model from a separate hierarchy.", "대상은 수정한 메쉬와 기존 오브젝트 배치를 유지하면서 원본의 설정을 받습니다. 원본과 다른 계층의 수정 모델을 넣으세요.", "対象は編集したメッシュと既存オブジェクトの配置を保持し、元データの設定を受け取ります。 元と別の階層にある修正済みモデルを指定してください。", TutorialTransplantInputs),
                    DiNeTutorialStep.Optional("Rules", "Transferred components and added non-mesh objects are remapped to the target; animation paths may need manual repair after mesh renaming or merging. Read which data will be transplanted.", "컴포넌트·추가 비메쉬 오브젝트를 대상에 맞춰 옮기며 메쉬 이름 변경·병합 뒤 애니메이션 경로는 직접 수정해야 할 수 있습니다. 이식할 데이터의 범위를 확인하세요.", "コンポーネントと追加の非メッシュオブジェクトを対象へ対応させて移し、メッシュ名変更・統合後のアニメーションパスは手動修正が必要な場合があります。 移植するデータの範囲を確認してください。"),
                    DiNeTutorialStep.Required("Analyze", "Analysis compares source and target hierarchies and previews which components can transfer without changing the target. Press Analyze to inspect matching objects.", "분석은 원본·대상 계층을 비교해 대상을 변경하지 않고 옮길 수 있는 컴포넌트를 미리 확인합니다. 분석을 눌러 오브젝트 대응 상태를 확인하세요.", "分析は元データと対象の階層を比較し、対象を変更せずに移植可能なコンポーネントを事前確認します。 分析を押してオブジェクトの対応を確認してください。", () => TutorialTransplantInputs() && transplantReport != null && transplantReport.Succeeded && tutorialAnalyzedSource == transplantSource && tutorialAnalyzedTarget == transplantTarget),
                    DiNeTutorialStep.Optional("Report", "Counts show matched and added objects, copied components and skipped data so you can judge the planned transfer. Read the analysis counts and warnings.", "대응·추가 오브젝트와 복사할 컴포넌트·제외 데이터를 보여줘 예정된 이식을 판단할 수 있습니다. 분석 개수와 경고를 확인하세요.", "対応・追加するオブジェクト、コピーするコンポーネント、除外データの件数から移植内容を判断できます。 分析の件数と警告を確認してください。"),
                    DiNeTutorialStep.Optional("Details", "Detailed warnings identify ambiguous names and missing target references that could leave a transferred feature incomplete. Expand details to inspect skipped items and references.", "상세 경고는 이름 중복이나 대응 참조 누락처럼 이식된 기능이 불완전해질 수 있는 부분을 짚어줍니다. 상세를 펼쳐 건너뛴 항목과 참조를 확인하세요.", "詳細警告は名前の重複や対応先の参照不足など、移植した機能が不完全になる可能性のある箇所を示します。 詳細を開いてスキップした項目と参照を確認してください。"),
                    DiNeTutorialStep.Optional("Execute", "Execute edits the assigned target directly and keeps the source; Undo can restore the target's previous state. Press Execute to transplant functionality.", "실행은 지정한 대상을 직접 수정하며 원본은 보존하고 Undo로 대상의 이전 상태를 되돌릴 수 있습니다. 기능을 이식하려면 실행을 누르세요.", "実行は指定した対象を直接変更し、元データは保持します。Undoで対象を以前の状態へ戻せます。 機能を移植するには、実行を押してください。"),
                    DiNeTutorialStep.Optional("SelectResult", "The result is the same target object with transferred settings, ready for checking the components and references. Press Select Target to reveal the result.", "결과는 설정이 이식된 같은 대상 오브젝트이며 컴포넌트·참조를 확인할 수 있습니다. 대상 선택을 눌러 결과 오브젝트를 확인하세요.", "結果は設定を移植した同じ対象オブジェクトで、コンポーネントや参照を確認できます。 対象を選択して結果のオブジェクトを確認してください。")
                },
                    overviewEn: "Restore an outfit or avatar's prefab functionality after editing its model, such as an FBX revised in a modeling app. Transfer Modular Avatar settings, PhysBones, colliders, constraints and added non-mesh objects onto the assigned revised scene object while retaining its edited meshes and existing placement. Analyze matches and unresolved references first, then execute; the source is preserved and Undo can restore the target.",
                    overviewKo: "모델링 앱에서 FBX를 수정한 뒤 의상·아바타 프리팹의 기능을 다시 붙일 때 사용합니다. Modular Avatar 설정·PhysBone·콜라이더·Constraint·추가 비메쉬 오브젝트를 수정한 씬 오브젝트에 이식하며 수정 메쉬와 기존 배치를 유지합니다. 먼저 대응 관계와 해결되지 않은 참조를 분석한 뒤 실행하며, 원본은 보존하고 Undo로 대상의 이전 상태를 복원할 수 있습니다.",
                    overviewJa: "モデリングアプリでFBXを編集した後などに、衣装・アバターのPrefab機能を付け直すために使います。Modular Avatar設定・PhysBone・コライダー・Constraint・追加の非メッシュオブジェクトを、編集したメッシュと既存の配置を保持したまま指定したシーンオブジェクトへ移植します。先に対応関係と未解決の参照を分析してから実行します。元データは保持され、Undoで対象を以前の状態へ戻せます。");
                return;
            }
            guidedTutorial.Configure("Vrm", "VRM preparation", "VRM 준비", "VRM準備", new[]
            {
                DiNeTutorialStep.Required("Avatar", "This avatar is the input for VRM preparation; individual actions modify the assigned object. Assign the avatar root from the Hierarchy.", "이 아바타가 VRM 준비의 입력이며 개별 작업 버튼은 지정한 오브젝트를 수정합니다. Hierarchy에서 아바타 루트를 넣으세요.", "このアバターがVRM準備の入力になり、個別の処理ボタンは指定したオブジェクトを変更します。 Hierarchyからアバターのルートを指定してください。", () => TutorialSceneObject(vrmAvatar)),
                DiNeTutorialStep.Optional("WorkingCopy", "A working copy duplicates the avatar in the scene and selects it for preparation, keeping the original available. Press this button to create a working copy.", "작업 복사본은 씬에서 아바타를 복제하고 준비 대상으로 선택해 원본을 남겨둡니다. 작업 복사본을 만들려면 이 버튼을 누르세요.", "作業用コピーはシーンでアバターを複製して準備対象に選択し、元データを残します。 作業用コピーを作るには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("AutoOptions", "Automatic preparation can include material conversion, T-Pose freezing and empty-object cleanup in addition to the main conversion steps. Choose the operations for automatic preparation.", "자동 준비에는 기본 변환 단계 외에 머티리얼 변환·T-Pose 고정·빈 오브젝트 정리를 포함할 수 있습니다. 자동 처리에 포함할 작업을 고르세요.", "自動準備には基本の変換手順に加え、マテリアル変換・T-Pose固定・空オブジェクト整理を含められます。 自動処理に含める作業を選んでください。"),
                DiNeTutorialStep.Optional("AutoApply", "The automatic action creates an avatar copy and runs the selected preparation steps on it. Press this button to run automatic preparation.", "자동 처리는 아바타 복사본을 만든 뒤 선택한 준비 단계를 그 복사본에 실행합니다. 전체 자동 처리를 실행하려면 이 버튼을 누르세요.", "自動処理はアバターのコピーを作成し、選択した準備手順をそのコピーへ実行します。 全自動処理を実行するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("MergeBones", "Merging places duplicate outfit and hair bones under matching humanoid bones to organize the export hierarchy. Press this button to merge outfit bones.", "본 병합은 중복 의상·헤어 본을 대응하는 휴머노이드 본 아래로 모아 내보낼 계층을 정리합니다. 의상 본을 병합하려면 이 버튼을 누르세요.", "ボーン統合は重複する衣装や髪のボーンを対応するHumanoidボーン以下へまとめ、書き出す階層を整理します。 衣装ボーンを統合するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("PhysMode", "Convert recreates supported motion with UniVRM 0.x SpringBones, Delete removes it, and Keep leaves the original PhysBones. Choose whether to convert, delete, or keep PhysBones.", "변환은 지원하는 흔들림을 UniVRM 0.x SpringBone으로 만들고, 삭제는 제거하며, 유지는 원본 PhysBone을 남깁니다. PhysBone을 변환·삭제·유지 중 어떻게 처리할지 고르세요.", "変換は対応する揺れをUniVRM 0.x SpringBoneで作り直し、削除は取り除き、維持は元のPhysBoneを残します。 PhysBoneを変換・削除・維持のどれで処理するか選んでください。"),
                DiNeTutorialStep.Optional("PhysApply", physBoneMode == DiNeVrmPhysBoneMode.Keep ? "Keep leaves PhysBones unchanged, including during component cleanup. Check that this matches your export preparation plan." : "This executes the selected conversion or deletion on the assigned avatar's PhysBones and colliders. Press the action below for the chosen mode.", physBoneMode == DiNeVrmPhysBoneMode.Keep ? "유지는 컴포넌트 정리에서도 PhysBone을 그대로 남깁니다. 내보내기 준비 계획에 맞는지 확인하세요." : "지정한 아바타의 PhysBone·콜라이더에 선택한 변환 또는 삭제를 실행합니다. 아래에서 선택한 모드의 작업 버튼을 누르세요.", physBoneMode == DiNeVrmPhysBoneMode.Keep ? "維持はコンポーネント整理でもPhysBoneをそのまま残します。書き出しの準備方針に合っているか確認してください。" : "指定したアバターのPhysBone・コライダーに、選択した変換または削除を実行します。下にある選択モードの処理ボタンを押してください。"),
                DiNeTutorialStep.Optional("Cleanup", "Cleanup converts supported static constraints and removes VRM-incompatible scripts, including VRChat and Modular Avatar components. Press this button to clean up VRChat components.", "정리는 지원하는 정적 Constraint를 변환하고 VRChat·Modular Avatar 등 VRM 비호환 스크립트를 제거합니다. VRChat 컴포넌트를 정리하려면 이 버튼을 누르세요.", "整理は対応する静的Constraintを変換し、VRChatやModular AvatarなどVRM非互換のスクリプトを削除します。 VRChatコンポーネントを整理するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("MaterialFeatures", "Checked effects such as normal maps and emission are carried into MToon where supported; unchecked effects are dropped. Check the material features to keep.", "노말맵·이미시브 등 체크한 효과는 지원 범위에서 MToon으로 옮기며 체크를 해제한 효과는 버립니다. 유지할 머티리얼 효과를 체크하세요.", "ノーマルマップやエミッションなどチェックした効果は対応範囲でMToonへ移し、外した効果は破棄します。 残すマテリアル効果をチェックしてください。"),
                DiNeTutorialStep.Optional("MaterialPresets", "These presets change all effect choices before conversion, useful for keeping details or simplifying the result. Use Keep All / Drop All to change feature choices together.", "효과 선택을 변환 전에 한꺼번에 바꿔 세부 효과를 남기거나 결과를 단순하게 만들 때 사용합니다. 모두 유지·모두 제거로 효과 선택을 한꺼번에 바꾸세요.", "変換前に効果の選択をまとめて変更し、細かな効果を残すか結果を簡略化する際に使います。 すべて維持・すべて破棄で効果の選択をまとめて変更してください。"),
                DiNeTutorialStep.Optional("SaveMaterials", "Asset saving keeps converted MToon materials under Assets/Di Ne/VRM Materials for reuse. Choose whether to save converted materials as assets.", "에셋으로 저장하면 변환된 MToon 머티리얼을 Assets/Di Ne/VRM Materials에 보관해 재사용할 수 있습니다. 변환한 머티리얼을 에셋으로 저장할지 정하세요.", "アセット保存は変換したMToonマテリアルをAssets/Di Ne/VRM Materialsに保存し、再利用できるようにします。 変換したマテリアルをアセットに保存するか選んでください。"),
                DiNeTutorialStep.Optional("ConvertMaterials", "Conversion replaces supported lilToon, Poiyomi and Standard materials with MToon and applies your chosen effect preservation. Press this button to convert materials to MToon.", "변환은 지원하는 lilToon·Poiyomi·Standard 머티리얼을 MToon으로 교체하고 선택한 효과 유지 설정을 적용합니다. 머티리얼을 MToon으로 바꾸려면 이 버튼을 누르세요.", "変換は対応するlilToon・Poiyomi・StandardマテリアルをMToonへ置き換え、選択した効果保持設定を適用します。 マテリアルをMToonに変換するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("EmptyObjects", "This removes unreferenced leaf objects with only a Transform while retaining required bones and referenced objects. Press this button to clean up empty objects.", "Transform만 가진 미참조 말단 오브젝트를 지우고 필요한 본·참조된 오브젝트는 유지합니다. 빈 오브젝트를 정리하려면 이 버튼을 누르세요.", "Transformだけを持つ参照されていない末端オブジェクトを削除し、必要なボーンや参照されたオブジェクトは保持します。 空のオブジェクトを整理するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Preflight", "Preflight checks the humanoid rig, remaining scripts, materials and references that may block VRM export. Press Preflight to inspect export readiness.", "사전 점검은 휴머노이드 리그·남은 스크립트·머티리얼·참조 등 VRM 내보내기를 막을 수 있는 문제를 확인합니다. 사전 점검을 눌러 내보내기 준비 상태를 확인하세요.", "事前チェックはHumanoidリグ・残ったスクリプト・マテリアル・参照など、VRM書き出しを妨げる可能性のある問題を確認します。 事前チェックを押して書き出しの準備状況を確認してください。"),
                DiNeTutorialStep.Optional("FixIssues", preflightIssues != null && preflightIssues.Count > 0 ? "Fix applies the correction described for that issue; some fixes remove invalid components or references. Read the issue, then press its Fix button if appropriate." : "The issue list explains export blockers and any available automatic corrections. Press Preflight to list issues.", preflightIssues != null && preflightIssues.Count > 0 ? "고치기는 해당 문제의 보정 내용을 적용하며 일부 보정은 잘못된 컴포넌트·참조를 제거합니다. 문제 설명을 읽고 필요한 항목의 고치기를 누르세요." : "문제 목록에서 내보내기를 막는 원인과 가능한 자동 보정을 확인할 수 있습니다. 사전 점검을 눌러 문제를 표시하세요.", preflightIssues != null && preflightIssues.Count > 0 ? "修正はその問題の補正を適用し、一部の補正は無効なコンポーネントや参照を取り除きます。説明を読んで必要な項目の修正を押してください。" : "問題一覧で書き出しを妨げる原因と利用可能な自動補正を確認できます。事前チェックを押して問題を表示してください。"),
                DiNeTutorialStep.Optional("Freeze", "UniVRM's T-Pose freezing prepares pose and transforms for export and should run after PhysBone conversion. Press this button to freeze the T-Pose.", "UniVRM의 T-Pose 고정은 내보낼 포즈·Transform을 정리하며 PhysBone 변환 뒤 실행해야 합니다. T-Pose를 고정하려면 이 버튼을 누르세요.", "UniVRMのT-Pose固定は書き出し用の姿勢・Transformを整え、PhysBone変換後に実行します。 T-Poseを固定するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("MeshUtility", "UniVRM's mesh utility offers further mesh processing before you finish export. Press this button to open the mesh tools.", "UniVRM의 메시 유틸리티에서 내보내기 전에 추가 메시 처리를 할 수 있습니다. 메시 도구를 열려면 이 버튼을 누르세요.", "UniVRMのメッシュユーティリティで書き出し前に追加のメッシュ処理を行えます。 メッシュツールを開くには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Export0", "This opens UniVRM's VRM 0.x exporter, where you enter metadata and perform the actual file export. Press VRM 0.x Export.", "UniVRM의 VRM 0.x 내보내기 창을 열어 메타 정보를 입력하고 실제 파일을 내보냅니다. VRM 0.x 내보내기를 누르세요.", "UniVRMのVRM 0.x書き出しウィンドウを開き、メタ情報の入力と実際のファイル書き出しを行います。 VRM 0.x書き出しを押してください。"),
                DiNeTutorialStep.Optional("Export1", DiNeUniVrmBridge.HasVrm1 ? "This opens UniVRM's exporter for the VRM 1.0 format, where the actual export is configured. Press VRM 1.0 Export." : "VRM 1.0 export is handed to UniVRM's own exporter. Install UniVRM 1.0 to enable it.", DiNeUniVrmBridge.HasVrm1 ? "실제 내보내기를 설정하는 UniVRM의 VRM 1.0 형식 내보내기 창을 엽니다. VRM 1.0 내보내기를 누르세요." : "VRM 1.0 파일 내보내기는 UniVRM의 전용 창에서 진행합니다. 사용하려면 UniVRM 1.0을 설치하세요.", DiNeUniVrmBridge.HasVrm1 ? "実際の書き出しを設定するUniVRMのVRM 1.0形式の書き出しウィンドウを開きます。VRM 1.0書き出しを押してください。" : "VRM 1.0ファイルの書き出しはUniVRM専用のウィンドウで行います。利用するにはUniVRM 1.0をインストールしてください。"),
                DiNeTutorialStep.Optional("VrChatConverter", DiNeUniVrmBridge.HasVrmConverterForVrChat ? "This hands a VRM avatar to the installed UniVRM converter for VRChat preparation. Press this button to open the converter and review its options." : "VRChat conversion is an optional handoff to a separate UniVRM converter. Install the UniVRM VRChat converter to enable this action.", DiNeUniVrmBridge.HasVrmConverterForVrChat ? "VRM 아바타를 VRChat용으로 준비하는 설치된 UniVRM 변환기로 넘깁니다. 버튼을 눌러 변환기를 열고 옵션을 확인하세요." : "VRChat 변환은 별도의 UniVRM 변환기로 넘기는 선택 작업입니다. 이 기능을 사용하려면 UniVRM의 VRChat 변환기를 설치하세요.", DiNeUniVrmBridge.HasVrmConverterForVrChat ? "VRMアバターをVRChat用に準備する、インストール済みのUniVRM変換ツールへ渡します。ボタンを押して変換ツールを開き、設定を確認してください。" : "VRChat変換は別のUniVRM変換ツールへ渡す任意の処理です。利用するにはUniVRMのVRChat変換ツールをインストールしてください。"),
                DiNeTutorialStep.Optional("Status", "The result reports what changed or why an operation could not run, helping you choose the next preparation step. Read the operation result here.", "결과는 변경된 내용이나 실행하지 못한 이유를 표시해 다음 준비 단계를 정할 수 있게 합니다. 여기서 작업 결과를 확인하세요.", "結果は変更内容や処理できなかった理由を表示し、次の準備手順を選べるようにします。 ここで処理結果を確認してください。")
            },
                overviewEn: "Prepare a VRChat-style avatar for VRM export by merging outfit bones, converting PhysBones to UniVRM 0.x SpringBones, cleaning incompatible components and converting supported materials to MToon. Full automatic preparation creates a copy; individual actions modify the assigned object, so create a working copy first when keeping the original. Inspect export blockers with Preflight, then hand T-Pose freezing and the actual export to the installed UniVRM tools.",
                overviewKo: "VRChat용 아바타를 VRM으로 내보낼 수 있게 의상 본 병합·PhysBone의 UniVRM 0.x SpringBone 변환·비호환 컴포넌트 정리·지원 머티리얼의 MToon 변환을 진행합니다. 전체 자동 처리는 복사본을 만들지만 개별 작업은 지정한 오브젝트를 수정하므로 원본을 남길 때는 먼저 작업 복사본을 만드세요. 사전 점검으로 내보내기 문제를 확인한 뒤 T-Pose 고정과 실제 파일 내보내기를 설치된 UniVRM 도구로 이어갑니다.",
                overviewJa: "VRChat向けアバターのVRM書き出しに向けて、衣装ボーン統合・PhysBoneのUniVRM 0.x SpringBone変換・非互換コンポーネント整理・対応マテリアルのMToon変換を行います。全自動処理はコピーを作成しますが、個別処理は指定オブジェクトを変更するため、元データを残す場合は先に作業コピーを作成してください。事前チェックで問題を確認し、T-Pose固定と実際の書き出しをインストール済みのUniVRMツールへ引き継ぎます。");
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
                DiNeTutorialStep.Optional("Info", "Focus was designed to correct focus blur from high material render queues, but the feature is currently sealed. Read the current Focus component status.", "Focus는 머티리얼의 높은 렌더 큐로 생기는 포커스 블러를 보정하는 기능이지만 현재는 봉인되어 있습니다. 현재 Focus 컴포넌트의 상태를 확인하세요.", "Focusはマテリアルの高いレンダーキューによるフォーカスブラーを補正する機能ですが、現在は封印されています。 現在のFocusコンポーネントの状態を確認してください。"),
                DiNeTutorialStep.Optional("Build", "The current sealed component performs no correction in Play Mode or avatar builds. Read the build-processing notice.", "현재 봉인된 컴포넌트는 플레이 모드·아바타 빌드에서 보정을 실행하지 않습니다. 빌드 적용 안내를 확인하세요.", "現在の封印されたコンポーネントは、プレイモードやアバタービルドで補正処理を行いません。 ビルド処理の案内を確認してください。"),
                DiNeTutorialStep.Optional("Removal", "Removing this legacy component cleans up the unused Focus setup on the avatar. Use Remove Component in the component menu to remove Focus.", "이 기존 컴포넌트를 제거하면 아바타에서 사용하지 않는 Focus 설정을 정리할 수 있습니다. 제거하려면 컴포넌트 메뉴의 Remove Component를 선택하세요.", "この従来のコンポーネントを削除すると、アバター上の使用していないFocus設定を整理できます。 削除するには、コンポーネントメニューのRemove Componentを選んでください。")
            },
                overviewEn: "This is the legacy Focus component for correcting focus blur associated with excessive outfit material render queues. The feature is currently sealed and performs no correction in Play Mode or avatar builds. This inspector explains that status and how to remove the unused component.",
                overviewKo: "의상 머티리얼의 과도한 렌더 큐로 생기는 포커스 블러를 보정하던 기존 Focus 컴포넌트입니다. 현재는 기능이 봉인되어 플레이 모드·아바타 빌드에서 보정을 실행하지 않습니다. 이 인스펙터에서 상태와 사용하지 않는 컴포넌트를 제거하는 방법을 안내합니다.",
                overviewJa: "衣装マテリアルの高すぎるレンダーキューによるフォーカスブラーを補正する従来のFocusコンポーネントです。現在は機能が封印され、プレイモードやアバタービルドで補正処理を行いません。このInspectorでは状態と使用していないコンポーネントの削除方法を案内します。");
        }
        private void OnDisable() => guidedTutorial?.Suspend();
    }
}
