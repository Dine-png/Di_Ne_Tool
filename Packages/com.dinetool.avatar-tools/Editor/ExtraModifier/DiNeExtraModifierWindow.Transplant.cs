using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DiNeTool.ExtraModifier.Editor
{
    internal sealed partial class DiNeExtraModifierWindow
    {
        [SerializeField] private GameObject transplantSource;
        [SerializeField] private GameObject transplantTarget;
        private DiNePrefabTransplantUtility.Report transplantReport;
        private bool transplantExecuted;
        private bool transplantShowDetails;
        private bool transplantExecutionPending;
        private GameObject transplantQueuedSource;
        private GameObject transplantQueuedTarget;

        private static readonly Dictionary<string, string[]> TransplantText = new Dictionary<string, string[]>
        {
            { "Tab", new[] { "Copy & Paste", "필살 복사붙여넣기술", "必殺コピー＆ペースト" } },
            { "Inputs", new[] { "Original Prefab → Edited FBX", "기존 프리팹 → 수정한 FBX", "元のPrefab → 編集したFBX" } },
            { "Description", new[] { "Transfers Modular Avatar, PhysBones, colliders, constraints and added objects directly onto the second object. The original prefab is preserved.", "두 번째 오브젝트에 모듈러 아바타, 피직스본, 콜라이더, 컨스트레인트와 추가 오브젝트를 직접 이식합니다. 기존 프리팹은 보존됩니다.", "2番目のオブジェクトにModular Avatar、PhysBone、コライダー、Constraintと追加オブジェクトを直接移植します。元のPrefabは保持されます。" } },
            { "Source", new[] { "Original Prefab", "기존 프리팹", "元のPrefab" } },
            { "SourceTip", new[] { "The outfit or avatar root containing the settings to transfer. Scene objects and prefab assets are supported.", "설정을 가져올 의상 또는 아바타의 루트입니다. 씬 오브젝트와 프리팹 에셋을 사용할 수 있습니다.", "設定を引き継ぐ衣装またはアバターのルートです。シーンオブジェクトとPrefabアセットを指定できます。" } },
            { "Target", new[] { "Edited FBX in Scene", "수정한 FBX (씬 오브젝트)", "編集したFBX（シーン）" } },
            { "TargetTip", new[] { "Assign the edited model's root from the Hierarchy. Existing object placement, names, parents, meshes and renderers are preserved. Drag a Project model into the scene first.", "Hierarchy에서 수정 모델의 루트를 지정하세요. 기존 오브젝트의 배치·이름·부모·메시·렌더러를 유지합니다. Project의 모델 에셋은 먼저 씬에 넣으세요.", "Hierarchyで編集済みモデルのルートを指定してください。既存オブジェクトの配置・名前・親・メッシュ・Rendererを保持します。Projectのモデルアセットは先にシーンへ配置してください。" } },
            { "Empty", new[] { "Assign the original prefab and the edited FBX root from the Hierarchy as the second object.", "기존 프리팹과 Hierarchy에 있는 수정 FBX의 루트를 두 번째 오브젝트로 지정하세요.", "元のPrefabと、Hierarchyにある編集済みFBXのルートを2番目のオブジェクトとして指定してください。" } },
            { "Rules", new[] { "Existing FBX objects keep their position, rotation and scale. Only newly added objects inherit the original prefab's local placement under the corresponding parent. Meshes and renderers are preserved, and deleted mesh objects are not recreated. Undo restores the target's previous state.", "FBX에 이미 있는 오브젝트의 위치·회전·크기는 유지합니다. 새로 추가하는 오브젝트에만 대응하는 부모 기준으로 기존 프리팹의 배치를 복사합니다. 메시·렌더러를 유지하고 삭제된 메시 오브젝트는 다시 만들지 않습니다. Undo로 대상의 이전 상태를 복원합니다.", "FBXに既にあるオブジェクトの位置・回転・スケールを保持します。新しく追加するオブジェクトにのみ、対応する親を基準に元のPrefabのローカル配置をコピーします。メッシュとRendererを保持し、削除されたメッシュオブジェクトは再作成しません。Undoで対象を以前の状態に戻せます。" } },
            { "AnimationHelp", new[] { "Animation paths and blendshape names in existing controller assets are kept as-is. Bindings to merged or deleted meshes may need manual repair.", "기존 컨트롤러 에셋의 애니메이션 경로와 쉐이프키 이름은 그대로 유지됩니다. 합치거나 삭제한 메시의 연결은 직접 수정해야 할 수 있습니다.", "既存ControllerアセットのアニメーションパスとBlendShape名はそのまま保持します。統合・削除したメッシュのBindingは手動修正が必要な場合があります。" } },
            { "Analyze", new[] { "Check Transfer", "이식 사전 점검", "移植を事前確認" } },
            { "AnalyzeTip", new[] { "Checks object matching, components and references without creating or modifying anything.", "아무것도 만들거나 수정하지 않고 오브젝트 대응, 컴포넌트, 참조를 점검합니다.", "何も作成・変更せず、オブジェクトの対応、コンポーネント、参照を確認します。" } },
            { "Execute", new[] { "Transfer to the Second Object", "두 번째 오브젝트에 필살 이식", "2番目のオブジェクトに必殺移植" } },
            { "ExecuteTip", new[] { "Transfers the original settings directly onto the assigned second object. The original prefab is preserved; Undo restores the target's previous state.", "지정한 두 번째 오브젝트에 기존 설정을 직접 이식합니다. 기존 프리팹은 보존되며 Undo로 대상의 이전 상태를 복원합니다.", "指定した2番目のオブジェクトに元の設定を直接移植します。元のPrefabは保持され、Undoで対象を以前の状態に戻せます。" } },
            { "Result", new[] { "Transfer Result", "이식 결과", "移植結果" } },
            { "Ready", new[] { "Check the transfer, then apply it to the second object.", "사전 점검 후 두 번째 오브젝트에 이식할 수 있습니다.", "事前確認後、2番目のオブジェクトに移植できます。" } },
            { "Analyzed", new[] { "Check complete. Review any skipped items before transferring.", "사전 점검 완료. 이식 전에 건너뛰는 항목을 확인하세요.", "事前確認が完了しました。移植前にスキップする項目を確認してください。" } },
            { "Completed", new[] { "Transfer complete. The second object is selected in the Hierarchy.", "이식 완료. Hierarchy에서 두 번째 오브젝트를 선택했습니다.", "移植が完了しました。Hierarchyで2番目のオブジェクトを選択しました。" } },
            { "TargetRemoved", new[] { "The target no longer exists. Assign a scene object to transfer again.", "대상 오브젝트가 없어졌습니다. 다시 이식하려면 씬 오브젝트를 지정하세요.", "対象オブジェクトが存在しません。再度移植するにはシーンオブジェクトを指定してください。" } },
            { "Failed", new[] { "Transfer could not be completed. Check the details below; the original prefab is preserved and the target changes were rolled back.", "이식을 완료하지 못했습니다. 아래 내용을 확인하세요. 기존 프리팹은 보존되며 대상의 변경은 되돌렸습니다.", "移植を完了できませんでした。以下の詳細を確認してください。元のPrefabは保持され、対象の変更は取り消されました。" } },
            { "Summary", new[] { "Matched objects: {0} · Added objects: {1}\nComponents: {2} · Skipped mesh objects: {3}\nSkipped components: {4} · Cleared references: {5}", "대응 오브젝트: {0} · 추가 오브젝트: {1}\n컴포넌트: {2} · 제외한 메시 오브젝트: {3}\n제외한 컴포넌트: {4} · 해제한 참조: {5}", "対応オブジェクト: {0} · 追加オブジェクト: {1}\nコンポーネント: {2} · 除外したメッシュオブジェクト: {3}\n除外したコンポーネント: {4} · 解除した参照: {5}" } },
            { "TargetResult", new[] { "Transferred Object", "이식한 오브젝트", "移植したオブジェクト" } },
            { "SelectTarget", new[] { "Select Target", "대상 선택", "対象を選択" } },
            { "SelectTargetTip", new[] { "Select and ping the second object in the Hierarchy.", "Hierarchy에서 두 번째 오브젝트를 선택하고 표시합니다.", "Hierarchyで2番目のオブジェクトを選択して表示します。" } },
            { "Details", new[] { "Matching and Reference Details ({0})", "오브젝트 대응·참조 상세 ({0})", "オブジェクト対応・参照の詳細 ({0})" } },
            { "UnknownDiagnostic", new[] { "This item needs review. See the Console for technical details.", "확인이 필요한 항목입니다. 기술적인 상세 내용은 Console에서 확인하세요.", "確認が必要な項目です。技術的な詳細はConsoleで確認してください。" } },
            { "MissingSource", new[] { "Assign the original prefab.", "기존 프리팹을 지정하세요.", "元のPrefabを指定してください。" } },
            { "MissingTarget", new[] { "Assign the edited FBX.", "수정한 FBX를 지정하세요.", "編集したFBXを指定してください。" } },
            { "SameInput", new[] { "The original and edited model must be different objects.", "기존 프리팹과 수정 모델은 서로 다른 오브젝트여야 합니다.", "元のPrefabと編集したモデルには別のオブジェクトを指定してください。" } },
            { "NestedInputs", new[] { "Choose independent roots; one input cannot contain the other.", "서로 독립된 루트를 지정하세요. 한 입력이 다른 입력의 부모나 자식이면 사용할 수 없습니다.", "独立したルートを指定してください。一方がもう一方の親または子では使用できません。" } },
            { "InvalidScene", new[] { "Assign a target from an open regular scene.", "열려 있는 일반 씬의 대상을 지정하세요.", "開いている通常のシーンから対象を指定してください。" } },
            { "TargetAssetInput", new[] { "Drag the edited FBX or prefab into the scene, then assign that Hierarchy object as the second input.", "수정한 FBX나 프리팹을 먼저 씬에 넣은 뒤, Hierarchy의 오브젝트를 두 번째 입력으로 지정하세요.", "編集したFBXまたはPrefabを先にシーンへ配置し、Hierarchyのオブジェクトを2番目の入力として指定してください。" } },
            { "PrefabStageInput", new[] { "Exit Prefab Mode. The original can be a scene object or prefab asset; the target must be a scene object.", "Prefab Mode를 종료하세요. 기존 원본은 씬 오브젝트나 프리팹 에셋을, 대상은 씬 오브젝트를 지정합니다.", "Prefab Modeを終了してください。元データはシーンオブジェクトまたはPrefabアセット、対象はシーンオブジェクトを指定します。" } },
            { "Playing", new[] { "Stop Play Mode before transferring.", "플레이 모드를 종료한 뒤 이식하세요.", "再生モードを停止してから移植してください。" } },
            { "MissingScript", new[] { "A missing script cannot be copied. Restore its package if needed.", "Missing Script는 복사할 수 없습니다. 필요하면 해당 패키지를 복구하세요.", "Missing Scriptはコピーできません。必要であれば該当パッケージを復元してください。" } },
            { "DuplicatePathAmbiguous", new[] { "Duplicate names make this hierarchy match ambiguous. Skipped to avoid transferring onto the wrong object.", "같은 이름이 중복되어 계층 대응을 확정할 수 없습니다. 다른 오브젝트에 이식하지 않도록 건너뜁니다.", "同名の重複で階層の対応を確定できません。誤ったオブジェクトへの移植を避けるためスキップします。" } },
            { "AmbiguousName", new[] { "Several objects share this name; automatic matching was skipped.", "이름이 같은 오브젝트가 여러 개라 자동 대응을 건너뜁니다.", "同名のオブジェクトが複数あるため自動対応をスキップします。" } },
            { "NameMatched", new[] { "Matched by a unique name because the hierarchy changed. Review the parent and placement.", "계층이 바뀌어 유일한 이름으로 대응했습니다. 부모와 배치를 확인하세요.", "階層が変更されたため一意な名前で対応しました。親と配置を確認してください。" } },
            { "MissingMeshObject", new[] { "This old mesh object is absent from the edited FBX and will not be recreated.", "수정 FBX에 없는 기존 메시 오브젝트는 다시 만들지 않습니다.", "編集したFBXに存在しない元のメッシュオブジェクトは再作成しません。" } },
            { "UnmappedParent", new[] { "The parent could not be matched. This child is skipped to preserve hierarchy placement.", "부모를 대응할 수 없어 기존 계층 배치를 유지할 수 없는 자식은 건너뜁니다.", "親を対応できないため、元の階層配置を保てない子はスキップします。" } },
            { "UnsafeRequiredComponent", new[] { "This component requires mesh or renderer data and cannot be safely added here.", "이 컴포넌트는 메시나 렌더러를 필요로 하여 여기에 안전하게 추가할 수 없습니다.", "このコンポーネントはメッシュまたはRendererを必要とするため安全に追加できません。" } },
            { "UnsafeRequiredTransform", new[] { "This component requires a different Transform type and is skipped to protect the edited rig.", "다른 종류의 트랜스폼을 필요로 하는 컴포넌트는 수정 리그를 보호하기 위해 건너뜁니다.", "異なるTransformの種類を必要とするコンポーネントは編集済みリグを保護するためスキップします。" } },
            { "MissingInternalReference", new[] { "An original internal reference has no counterpart and will be cleared. Review the copied component.", "기존 내부 참조에 대응하는 대상이 없어 참조를 해제합니다. 이식된 컴포넌트를 확인하세요.", "元の内部参照に対応する対象がないため参照を解除します。移植されたコンポーネントを確認してください。" } },
            { "AvatarObjectPathRemapped", new[] { "A Modular Avatar object path is reconnected to the corresponding target object.", "모듈러 아바타의 오브젝트 경로를 대응하는 대상 오브젝트로 다시 연결합니다.", "Modular Avatarのオブジェクトパスを対応する対象オブジェクトに再接続します。" } },
            { "AvatarObjectPathUnresolved", new[] { "A Modular Avatar path inside the original root has no target counterpart. The reference is cleared; review this component.", "기존 루트 안의 모듈러 아바타 경로에 대응하는 대상이 없습니다. 참조를 해제하므로 이 컴포넌트를 확인하세요.", "元のルート内のModular Avatarパスに対応する対象がありません。参照を解除するため、このコンポーネントを確認してください。" } },
            { "AvatarObjectPathOutsideAvatar", new[] { "The object reference is reconnected, but the target is outside an avatar. Check this Modular Avatar reference after placing it under the avatar.", "오브젝트 참조는 다시 연결했지만 대상이 아바타 밖에 있습니다. 아바타 아래에 넣은 뒤 모듈러 아바타 참조를 확인하세요.", "オブジェクト参照は再接続しましたが、対象がアバターの外にあります。アバターの下に配置した後、Modular Avatarの参照を確認してください。" } },
            { "MeshReferenceExcluded", new[] { "An old mesh reference is replaced with the matching edited mesh, or cleared when no unique match exists.", "기존 메시 참조는 대응하는 수정 메시로 교체하고, 대상을 확정할 수 없으면 해제합니다.", "元のメッシュ参照は対応する編集済みメッシュに置き換え、一意に対応できなければ解除します。" } },
            { "MeshReferenceRetargeted", new[] { "An old mesh reference is reconnected to the corresponding edited mesh.", "기존 메시 참조를 대응하는 수정 메시로 다시 연결합니다.", "元のメッシュ参照を対応する編集済みメッシュに再接続します。" } },
            { "ExternalReference", new[] { "A reference outside the original root is retained. Check that it is valid for this outfit or avatar.", "기존 루트 밖의 참조를 유지합니다. 이 의상이나 아바타에서 유효한지 확인하세요.", "元のルート外への参照を保持します。この衣装またはアバターで有効か確認してください。" } },
            { "AnimatorAvatarPreserved", new[] { "The edited FBX's Animator Avatar is retained to use its rig.", "수정 FBX의 리그를 사용하도록 Animator의 Avatar를 유지합니다.", "編集したFBXのリグを使うためAnimatorのAvatarを保持します。" } },
            { "AnimatorAvatarMissing", new[] { "The edited model has no Animator Avatar for this rig. Check its Rig import settings before using the target.", "수정 모델에 이 리그의 Animator Avatar가 없습니다. 대상을 사용하기 전에 모델의 Rig 임포트 설정을 확인하세요.", "編集したモデルにこのリグのAnimator Avatarがありません。対象を使用する前にモデルのRigインポート設定を確認してください。" } },
            { "TransformTypeMismatch", new[] { "The Transform types differ; the edited model's type is retained. Check this object's placement.", "트랜스폼 종류가 달라 수정 모델의 종류를 유지합니다. 이 오브젝트의 배치를 확인하세요.", "Transformの種類が異なるため編集したモデルの種類を保持します。このオブジェクトの配置を確認してください。" } },
            { "ComponentCreationFailed", new[] { "A component could not be copied. See the Console for its technical error.", "컴포넌트를 복사할 수 없습니다. 기술적인 오류는 Console에서 확인하세요.", "コンポーネントをコピーできません。技術的なエラーはConsoleで確認してください。" } },
            { "ExecutionFailed", new[] { "An error interrupted the transfer; the target changes were rolled back. See the Console for details.", "오류로 이식이 중단되어 대상의 변경을 되돌렸습니다. 상세 내용은 Console에서 확인하세요.", "エラーで移植が中断され、対象の変更を取り消しました。詳細はConsoleで確認してください。" } },
            { "AnalysisFailed", new[] { "The original settings could not be read. See the Console for details.", "기존 설정을 읽을 수 없습니다. 상세 내용은 Console에서 확인하세요.", "元の設定を読み取れません。詳細はConsoleで確認してください。" } }
        };

        private string TT(string key)
        {
            return TransplantText.TryGetValue(key, out var values) ? values[L] : TransplantText["UnknownDiagnostic"][L];
        }

        private void OnTransplantUndoRedo()
        {
            CancelQueuedTransplant();
            // A direct target survives Undo; its previous report no longer describes the scene.
            transplantReport = null;
            transplantExecuted = false;
            Repaint();
        }

        private void QueueTransplantExecution()
        {
            if (transplantExecutionPending) return;
            transplantQueuedSource = transplantSource;
            transplantQueuedTarget = transplantTarget;
            transplantExecutionPending = true;
            // Component callbacks and Undo rollback can invalidate the active IMGUI layout.
            // Finish drawing this event before touching the hierarchy or changing selection.
            EditorApplication.delayCall += ExecuteQueuedTransplant;
        }

        private void CancelQueuedTransplant()
        {
            EditorApplication.delayCall -= ExecuteQueuedTransplant;
            transplantExecutionPending = false;
            transplantQueuedSource = null;
            transplantQueuedTarget = null;
        }

        private void ExecuteQueuedTransplant()
        {
            if (!transplantExecutionPending || this == null) return;
            var source = transplantQueuedSource;
            var target = transplantQueuedTarget;
            CancelQueuedTransplant();
            try
            {
                transplantReport = DiNePrefabTransplantUtility.Execute(source, target);
                transplantExecuted = true;
                transplantShowDetails = HasTransplantWarnings();
                if (transplantReport.ResultRoot != null)
                {
                    Selection.activeGameObject = transplantReport.ResultRoot;
                    EditorGUIUtility.PingObject(transplantReport.ResultRoot);
                }
            }
            finally
            {
                if (this != null) Repaint();
            }
        }

        private void DrawTransplantTab()
        {
            EditorGUILayout.BeginVertical("box");
            SectionLabel(TT("Inputs"));
            GUILayout.Space(4f);
            GUILayout.Label(TT("Description"), EditorStyles.wordWrappedLabel);
            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(transplantExecutionPending))
            {
                EditorGUI.BeginChangeCheck();
                var source = DrawTransplantInput("Source", "SourceTip", transplantSource);
                var target = DrawTransplantInput("Target", "TargetTip", transplantTarget);
                if (EditorGUI.EndChangeCheck() || source != transplantSource || target != transplantTarget)
                {
                    transplantSource = source;
                    transplantTarget = target;
                    transplantReport = null;
                    transplantExecuted = false;
                    guidedTutorial.NotifyAction("Source");
                    guidedTutorial.NotifyAction("Target");
                }
            }
            GUILayout.Space(5f);
            EditorGUILayout.HelpBox(TT("Rules"), MessageType.Info);
            guidedTutorial.Anchor("Rules", GUILayoutUtility.GetLastRect());
            GUILayout.Label(TT("AnimationHelp"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            guidedTutorial.Draw("Source");
            guidedTutorial.Draw("Target");
            guidedTutorial.Draw("Rules");

            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");
            SectionLabel(TT("Tab"));
            GUILayout.Space(4f);
            if (transplantSource == null || transplantTarget == null)
                EditorGUILayout.HelpBox(TT("Empty"), MessageType.Info);
            if (transplantTarget != null && EditorUtility.IsPersistent(transplantTarget))
                EditorGUILayout.HelpBox(TT("TargetAssetInput"), MessageType.Warning);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox(TT("Playing"), MessageType.Warning);
            using (new EditorGUI.DisabledScope(transplantExecutionPending || transplantSource == null || transplantTarget == null ||
                                               EditorUtility.IsPersistent(transplantTarget) ||
                                               EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(new GUIContent(TT("Analyze"), TT("AnalyzeTip")), GUILayout.Height(30f)))
                {
                    transplantReport = DiNePrefabTransplantUtility.Analyze(transplantSource, transplantTarget);
                    tutorialAnalyzedSource = transplantSource;
                    tutorialAnalyzedTarget = transplantTarget;
                    transplantExecuted = false;
                    transplantShowDetails = HasTransplantWarnings();
                    guidedTutorial.NotifyAction("Analyze");
                }
                guidedTutorial.Anchor("Analyze", GUILayoutUtility.GetLastRect());
                GUILayout.Space(4f);
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = AccentColor;
                if (GUILayout.Button(new GUIContent(TT("Execute"), TT("ExecuteTip")), ActionButtonStyle(), GUILayout.Height(30f)))
                    QueueTransplantExecution();
                guidedTutorial.Anchor("Execute", GUILayoutUtility.GetLastRect());
                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndVertical();
            guidedTutorial.Draw("Analyze");
            guidedTutorial.Draw("Execute");

            GUILayout.Space(8f);
            DrawTransplantReport();
            guidedTutorial.Draw("Report");
            guidedTutorial.Draw("Details");
            guidedTutorial.Draw("SelectResult");
        }

        private GameObject DrawTransplantInput(string labelKey, string tooltipKey, GameObject value)
        {
            var input = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(TT(labelKey), TT(tooltipKey)), value, typeof(GameObject), true);
            guidedTutorial.Anchor(labelKey, GUILayoutUtility.GetLastRect());
            return input;
        }

        private bool HasTransplantWarnings()
        {
            if (transplantReport == null)
                return false;
            foreach (var diagnostic in transplantReport.Diagnostics)
                if (diagnostic.Severity != DiNePrefabTransplantUtility.DiagnosticSeverity.Info)
                    return true;
            return false;
        }

        private void DrawTransplantReport()
        {
            EditorGUILayout.BeginVertical("box");
            SectionLabel(TT("Result"));
            GUILayout.Space(4f);
            if (transplantReport == null)
            {
                EditorGUILayout.HelpBox(TT("Ready"), MessageType.Info);
                var emptyReportRect = GUILayoutUtility.GetLastRect();
                guidedTutorial.Anchor("Report", emptyReportRect);
                guidedTutorial.Anchor("Details", emptyReportRect);
                guidedTutorial.Anchor("SelectResult", emptyReportRect);
                EditorGUILayout.EndVertical();
                return;
            }

            var status = !transplantReport.Succeeded ? "Failed" : transplantExecuted
                ? (transplantReport.ResultRoot != null ? "Completed" : "TargetRemoved") : "Analyzed";
            var type = !transplantReport.Succeeded ? MessageType.Error : HasTransplantWarnings() ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(TT(status), type);
            var reportStatusRect = GUILayoutUtility.GetLastRect();
            guidedTutorial.Anchor("Report", reportStatusRect);
            guidedTutorial.Anchor("Details", reportStatusRect);
            guidedTutorial.Anchor("SelectResult", reportStatusRect);
            if (transplantReport.Succeeded)
                GUILayout.Label(string.Format(TT("Summary"), transplantReport.MatchedObjects, transplantReport.CreatedObjects,
                    transplantReport.CopiedComponents, transplantReport.SkippedMeshObjects,
                    transplantReport.SkippedComponents, transplantReport.ClearedReferences), EditorStyles.wordWrappedLabel);

            if (transplantReport.ResultRoot != null)
            {
                GUILayout.Space(5f);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(TT("TargetResult"), transplantReport.ResultRoot, typeof(GameObject), true);
                if (GUILayout.Button(new GUIContent(TT("SelectTarget"), TT("SelectTargetTip")), GUILayout.Height(24f)))
                {
                    Selection.activeGameObject = transplantReport.ResultRoot;
                    EditorGUIUtility.PingObject(transplantReport.ResultRoot);
                }
                guidedTutorial.Anchor("SelectResult", GUILayoutUtility.GetLastRect());
            }

            if (transplantReport.Diagnostics.Count > 0)
            {
                GUILayout.Space(5f);
                transplantShowDetails = EditorGUILayout.Foldout(transplantShowDetails,
                    string.Format(TT("Details"), transplantReport.Diagnostics.Count), true);
                guidedTutorial.Anchor("Details", GUILayoutUtility.GetLastRect());
                if (transplantShowDetails)
                {
                    foreach (var diagnostic in transplantReport.Diagnostics)
                    {
                        var diagnosticType = diagnostic.Severity == DiNePrefabTransplantUtility.DiagnosticSeverity.Error ? MessageType.Error :
                            diagnostic.Severity == DiNePrefabTransplantUtility.DiagnosticSeverity.Warning ? MessageType.Warning : MessageType.Info;
                        var message = TT(diagnostic.Code);
                        if (!string.IsNullOrEmpty(diagnostic.Path))
                            message += "\n" + diagnostic.Path;
                        EditorGUILayout.HelpBox(message, diagnosticType);
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
