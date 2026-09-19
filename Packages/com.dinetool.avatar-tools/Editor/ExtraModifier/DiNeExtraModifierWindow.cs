using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DiNeTool.ExtraModifier.Editor
{
    internal sealed class DiNeExtraModifierWindow : EditorWindow
    {
        private enum Language
        {
            English,
            Korean,
            Japanese
        }

        private enum Feature
        {
            Vrm,
            Focus
        }

        private const string LanguagePrefKey = "DiNeExtraModifier_Language";

        private static readonly Color AccentColor = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color DangerColor = new Color(0.72f, 0.32f, 0.32f);

        private static readonly string[][] UiText =
        {
            new[] { "Small avatar improvements, made simple.", "작지만 해두면 좋은 아바타 설정을 간단하게 적용합니다.", "小さいけれど便利なアバター設定を簡単に適用します。" },
            new[] { "Target Avatar", "대상 아바타", "対象アバター" },
            new[] { "Avatar", "아바타", "アバター" },
            new[] { "Select an avatar or one of its children in the Hierarchy.", "Hierarchy에서 아바타 또는 아바타의 자식을 선택하세요.", "Hierarchyでアバターまたはその子オブジェクトを選択してください。" },
            new[] { "Focus Settings", "포커스 설정", "フォーカス設定" },
            new[] { "Automatically fixes focus blur caused by excessive render queues on outfit materials during build. Body, transparent, and cutout materials are safely excluded.", "의상 머티리얼의 과도한 렌더 큐로 발생하는 포커스 블러 문제를 빌드할 때 자동 보정합니다. Body와 투명·컷아웃 계열은 안전하게 제외됩니다.", "衣装マテリアルの高すぎるレンダーキューによるフォーカスブラーをビルド時に自動補正します。Body、透明、カットアウト系は安全のため除外します。" },
            new[] { "Not Applied", "미적용", "未適用" },
            new[] { "Applied", "적용됨", "適用済み" },
            new[] { "Apply Focus", "포커스 적용", "フォーカスを適用" },
            new[] { "Focus Applied ✓", "포커스 적용 완료 ✓", "フォーカス適用済み ✓" },
            new[] { "Advanced Settings", "고급 설정", "詳細設定" },
            new[] { "Force correction (all materials above Queue 2400, including transparent materials)", "강제 보정 (투명 계열을 포함한 Queue 2400 초과 머티리얼 전체)", "強制補正（透明系を含むQueue 2400超過の全マテリアル）" },
            new[] { "Remove Focus", "포커스 제거", "フォーカスを削除" },
            new[] { "Focus is active. It will be applied automatically during play mode and avatar builds.", "포커스가 활성화되어 있습니다. 플레이 모드와 아바타 빌드 시 자동 적용됩니다.", "フォーカスは有効です。プレイモードとアバタービルド時に自動適用されます。" },
            new[] { "Focus is not applied to this avatar yet.", "이 아바타에는 아직 포커스가 적용되지 않았습니다.", "このアバターにはまだフォーカスが適用されていません。" },
            new[] { "Extra Modifier's Focus is active. No additional setup is required; it runs automatically when the avatar is built.", "Extra Modifier의 포커스가 활성화되어 있습니다. 별도 작업 없이 아바타 빌드 시 자동 적용됩니다.", "Extra Modifierのフォーカスは有効です。追加設定なしでアバタービルド時に自動適用されます。" },
            new[] { "Force Correction", "강제 보정", "強制補正" },
            new[] { "Focus", "포커스", "フォーカス" },
            new[] { "VRM", "VRM", "VRM" },
            new[] { "VRM Target", "VRM 대상", "VRM対象" },
            new[] { "Working Avatar", "작업 아바타", "作業アバター" },
            new[] { "Assign an avatar to prepare for VRM.", "VRM용으로 정리할 아바타를 지정하세요.", "VRM用に準備するアバターを指定してください。" },
            new[] { "The individual buttons modify the assigned object. Use the full auto button to work safely on an automatic copy.", "개별 버튼은 지정한 오브젝트를 직접 수정합니다. 안전하게 작업하려면 복사본을 만드는 전체 자동 처리를 사용하세요.", "個別ボタンは指定したオブジェクトを直接変更します。安全に作業するには自動コピーを作成する全自動処理を使用してください。" },
            new[] { "Create VRM Working Copy", "VRM 작업 복사본 만들기", "VRM作業コピーを作成" },
            new[] { "Full Automatic Preparation", "전체 자동 처리", "全自動処理" },
            new[] { "Creates a copy, merges outfit bones, converts PhysBones, preserves compatible constraints, and removes VRM-incompatible scripts.", "복사본을 만든 뒤 의상 본 병합, PhysBone 변환, 호환 가능한 Constraint 보존, VRM 비호환 스크립트 제거를 한 번에 진행합니다.", "コピーを作成し、衣装ボーン統合、PhysBone変換、互換Constraintの保持、VRM非互換スクリプトの削除を一括実行します。" },
            new[] { "Run All on a Copy", "복사본에 전체 자동 처리", "コピーに全自動処理" },
            new[] { "1. Merge Outfit Bones", "1. 의상 본 자동 병합", "1. 衣装ボーン自動統合" },
            new[] { "Automatically finds duplicate outfit and hair bones and nests them under the matching humanoid bones.", "중복된 의상·헤어 본을 자동 탐지하여 이름이 같은 휴머노이드 본 아래로 병합합니다.", "重複した衣装・髪ボーンを自動検出し、同名のHumanoidボーン配下へ統合します。" },
            new[] { "Merge Bones", "본 자동 병합", "ボーンを自動統合" },
            new[] { "2. Convert SpringBones", "2. 스프링본 자동 변환", "2. SpringBone自動変換" },
            new[] { "Converts every VRC PhysBone and referenced collider to UniVRM 0.x SpringBones using automatic settings.", "모든 VRC PhysBone과 연결된 Collider를 자동 설정으로 UniVRM 0.x SpringBone으로 변환합니다.", "すべてのVRC PhysBoneと参照Colliderを自動設定でUniVRM 0.x SpringBoneへ変換します。" },
            new[] { "Convert All PhysBones", "모든 PhysBone 자동 변환", "すべてのPhysBoneを自動変換" },
            new[] { "UniVRM 0.x is required for SpringBone conversion.", "SpringBone 변환에는 UniVRM 0.x가 필요합니다.", "SpringBone変換にはUniVRM 0.xが必要です。" },
            new[] { "3. Clean VRM Components", "3. VRM 컴포넌트 정리", "3. VRMコンポーネント整理" },
            new[] { "Converts static VRC Constraints to Unity Constraints where possible, then removes MA, NDMF, VRC, and other non-VRM MonoBehaviours. PhysBones are left to step 2.", "정적 VRC Constraint를 가능한 경우 Unity Constraint로 변환한 뒤 MA, NDMF, VRC 및 기타 VRM 비호환 MonoBehaviour를 제거합니다. PhysBone은 건드리지 않고 2번 단계에 맡깁니다.", "静的VRC Constraintを可能な場合はUnity Constraintへ変換し、MA、NDMF、VRC、その他VRM非互換MonoBehaviourを削除します。PhysBoneには触れず、手順2に任せます。" },
            new[] { "Clean Components", "컴포넌트 정리", "コンポーネントを整理" },
            new[] { "Result", "처리 결과", "処理結果" },
            new[] { "Ready", "준비됨", "準備完了" },
            new[] { "Working copy created.", "작업 복사본을 만들었습니다.", "作業コピーを作成しました。" },
            new[] { "Focus is temporarily sealed and does not run during play mode or avatar builds.", "포커스 기능은 임시 봉인되어 플레이 모드와 아바타 빌드에서 동작하지 않습니다.", "フォーカス機能は一時的に封印され、プレイモードとアバタービルドでは動作しません。" },
            new[] { "PhysBone Handling", "PhysBone 처리 방식", "PhysBone処理方法" },
            new[] { "Convert", "변환", "変換" },
            new[] { "Delete", "삭제", "削除" },
            new[] { "Keep", "유지", "維持" },
            new[] { "Converts PhysBones into UniVRM SpringBones. Collider groups are created on the transform each collider belongs to, and capsule colliders are approximated with spheres.", "PhysBone을 UniVRM SpringBone으로 변환합니다. 콜라이더 그룹은 각 콜라이더가 속한 트랜스폼에 생성되고, 캡슐 콜라이더는 구 여러 개로 근사합니다.", "PhysBoneをUniVRM SpringBoneへ変換します。コライダーグループは各コライダーが属するTransformに作成し、カプセルは複数の球で近似します。" },
            new[] { "Deletes PhysBones and their colliders without converting anything. Use this only when you plan to rebuild the physics by hand.", "변환 없이 PhysBone과 콜라이더를 삭제합니다. 흔들림을 직접 다시 만들 때만 사용하세요.", "変換せずPhysBoneとコライダーを削除します。揺れを手作業で作り直す場合のみ使用してください。" },
            new[] { "Leaves PhysBones untouched. The cleanup step will not remove them either.", "PhysBone을 그대로 둡니다. 컴포넌트 정리 단계에서도 삭제하지 않습니다.", "PhysBoneをそのまま残します。コンポーネント整理でも削除しません。" },
            new[] { "Delete All PhysBones", "모든 PhysBone 삭제", "すべてのPhysBoneを削除" },
            new[] { "Keep PhysBones (no action)", "PhysBone 유지 (동작 없음)", "PhysBoneを維持（処理なし）" },
            new[] { "4. Convert Materials to MToon", "4. 머티리얼 MToon 변환", "4. マテリアルをMToon変換" },
            new[] { "Replaces lilToon, Poiyomi, and Standard materials with VRM MToon. Checked data is carried over; unchecked data is dropped during conversion. MatCap and rim light are off by default because MToon computes them differently and the result usually looks worse than the original.", "lilToon, Poiyomi, Standard 계열 머티리얼을 VRM MToon으로 교체합니다. 체크한 데이터는 살려서 옮기고, 체크를 해제한 데이터는 변환하면서 버립니다. 맷캡과 림라이트는 MToon의 계산 방식이 달라 옮기면 오히려 이상해지는 경우가 많아 기본적으로 끕니다.", "lilToon、Poiyomi、Standard系マテリアルをVRM MToonへ置き換えます。チェックしたデータは引き継ぎ、外したデータは変換時に破棄します。マットキャップとリムライトはMToonの計算方式が異なり、引き継ぐとかえって崩れやすいため既定でオフです。" },
            new[] { "Convert Materials", "머티리얼 변환", "マテリアルを変換" },
            new[] { "Normal map", "노말맵", "ノーマルマップ" },
            new[] { "MatCap (sphere add)", "맷캡 (가산 스피어)", "マットキャップ（加算スフィア）" },
            new[] { "Emission", "이미시브", "エミッション" },
            new[] { "Shade color", "그림자 색", "影色" },
            new[] { "Outline", "아웃라인", "アウトライン" },
            new[] { "Rim light", "림 라이트", "リムライト" },
            new[] { "Keep All", "모두 유지", "すべて維持" },
            new[] { "Drop All", "모두 제거", "すべて破棄" },
            new[] { "MToon shader was not found. Install UniVRM to convert materials.", "MToon 셰이더를 찾을 수 없습니다. 머티리얼 변환에는 UniVRM이 필요합니다.", "MToonシェーダーが見つかりません。マテリアル変換にはUniVRMが必要です。" },
            new[] { "Save converted materials under Assets/Di Ne/VRM Materials", "변환한 머티리얼을 Assets/Di Ne/VRM Materials에 저장", "変換したマテリアルをAssets/Di Ne/VRM Materialsに保存" },
            new[] { "Include material conversion in the full automatic preparation", "전체 자동 처리에 머티리얼 변환 포함", "全自動処理にマテリアル変換を含める" },
            new[] { "UniVRM Integration", "UniVRM 연동", "UniVRM連携" },
            new[] { "This tool prepares the avatar; T-Pose freezing, mesh utilities, meta input, and the actual export are handed over to UniVRM's own windows.", "이 툴은 아바타를 준비하는 역할만 하고, T-Pose 고정·메시 유틸리티·메타 입력·실제 내보내기는 UniVRM의 창에 그대로 넘깁니다.", "このツールはアバターの準備を担当し、T-Pose固定・メッシュユーティリティ・メタ入力・実際の書き出しはUniVRM側のウィンドウに任せます。" },
            new[] { "Installed", "설치됨", "インストール済み" },
            new[] { "UniVRM was not found. Install UniVRM (VRM 0.x or VRM 1.0) to freeze T-Pose and export.", "UniVRM을 찾을 수 없습니다. T-Pose 고정과 내보내기에는 UniVRM(VRM 0.x 또는 VRM 1.0)이 필요합니다.", "UniVRMが見つかりません。T-Pose固定と書き出しにはUniVRM（VRM 0.xまたはVRM 1.0）が必要です。" },
            new[] { "Run Preflight Check", "사전 점검 실행", "事前チェックを実行" },
            new[] { "No problems found. Ready to hand off to UniVRM.", "문제가 없습니다. UniVRM으로 넘겨도 됩니다.", "問題ありません。UniVRMへ引き渡せます。" },
            new[] { "Fix", "고치기", "修正" },
            new[] { "Freeze T-Pose (UniVRM)", "T-Pose 고정 (UniVRM)", "T-Pose固定（UniVRM）" },
            new[] { "Open MeshUtility (UniVRM)", "MeshUtility 열기 (UniVRM)", "MeshUtilityを開く（UniVRM）" },
            new[] { "Open VRM 0.x Exporter (UniVRM)", "VRM 0.x 내보내기 열기 (UniVRM)", "VRM 0.x書き出しを開く（UniVRM）" },
            new[] { "Open VRM 1.0 Exporter (UniVRM)", "VRM 1.0 내보내기 열기 (UniVRM)", "VRM 1.0書き出しを開く（UniVRM）" },
            new[] { "Open VRM Converter for VRChat", "VRM Converter for VRChat 열기", "VRM Converter for VRChatを開く" },
            new[] { "UniVRM is not installed.", "UniVRM이 설치되어 있지 않습니다.", "UniVRMがインストールされていません。" },
            new[] { "The avatar root has no humanoid Animator. VRM export requires a humanoid rig.", "루트에 휴머노이드 Animator가 없습니다. VRM 내보내기에는 휴머노이드 리그가 필요합니다.", "ルートにHumanoid Animatorがありません。VRM書き出しにはHumanoidリグが必要です。" },
            new[] { "The root transform is not at the origin with unit scale. Freeze T-Pose or reset it before exporting.", "루트 트랜스폼이 원점·기본 스케일이 아닙니다. 내보내기 전에 T-Pose 고정 또는 초기화가 필요합니다.", "ルートTransformが原点・等倍ではありません。書き出し前にT-Pose固定またはリセットが必要です。" },
            new[] { "Missing scripts remain: {0}", "Missing script가 {0}개 남아 있습니다.", "Missing scriptが{0}個残っています。" },
            new[] { "PhysBones remain: {0}. Convert them to SpringBones or delete them.", "PhysBone이 {0}개 남아 있습니다. SpringBone으로 변환하거나 삭제하세요.", "PhysBoneが{0}個残っています。SpringBoneへ変換するか削除してください。" },
            new[] { "VRM-incompatible components remain: {0}", "VRM 비호환 컴포넌트가 {0}개 남아 있습니다.", "VRM非互換コンポーネントが{0}個残っています。" },
            new[] { "UniVRM cannot export {0} material(s) as-is ({1}); they would fall back to a flat material.", "UniVRM이 그대로 내보낼 수 없는 머티리얼이 {0}개 있습니다 ({1}). 변환하지 않으면 단순 머티리얼로 나갑니다.", "UniVRMがそのまま書き出せないマテリアルが{0}個あります（{1}）。変換しないと簡易マテリアルになります。" },
            new[] { "No VRMMeta yet. You can fill in the title and author in UniVRM's export window.", "아직 VRMMeta가 없습니다. UniVRM 내보내기 창에서 제목·제작자를 입력할 수 있습니다.", "まだVRMMetaがありません。UniVRMの書き出しウィンドウでタイトルや作者を入力できます。" },
            new[] { "Finish with UniVRM's Freeze T-Pose (runs last, after PhysBone conversion)", "마지막에 UniVRM의 T-Pose 고정 실행 (PhysBone 변환 이후)", "最後にUniVRMのT-Pose固定を実行（PhysBone変換の後）" },
            new[] { "{0} SpringBone(s) reference objects that are inactive or outside the export root. UniVRM blocks the export with \"is not active\" / \"is out of hierarchy\". Fixing drops those references; activate the objects first if you want them exported.", "SpringBone {0}개가 비활성이거나 내보내기 루트 밖에 있는 오브젝트를 참조합니다. UniVRM이 \"is not active\" / \"is out of hierarchy\" 오류로 내보내기를 막습니다. 고치기를 누르면 해당 참조를 제거합니다. 그 오브젝트도 내보내려면 먼저 활성화하고 다시 변환하세요.", "SpringBoneが{0}個、非アクティブまたは書き出しルート外のオブジェクトを参照しています。UniVRMは\"is not active\" / \"is out of hierarchy\"で書き出しを止めます。修正すると該当参照を削除します。書き出したい場合は先にオブジェクトを有効化してください。" },
            new[] { "5. Remove Empty Objects", "5. 빈 오브젝트 정리", "5. 空オブジェクトの整理" },
            new[] { "Removes objects that have no component other than Transform, no children, and are not referenced by anything. Humanoid bones, mesh bones, SpringBone roots, collider groups, and constraint targets are kept. Deleted names are listed in the Console.", "Transform 외에 컴포넌트가 없고, 자식도 없고, 어디에서도 참조하지 않는 오브젝트를 지웁니다. 휴머노이드 본, 메시 본, SpringBone 루트, 콜라이더 그룹, Constraint 대상은 남깁니다. 지운 이름은 콘솔에 남습니다.", "Transform以外のコンポーネントがなく、子もなく、どこからも参照されていないオブジェクトを削除します。Humanoidボーン、メッシュボーン、SpringBoneのルート、コライダーグループ、Constraint対象は残します。削除した名前はコンソールに出力します。" },
            new[] { "Remove Empty Objects", "빈 오브젝트 지우기", "空オブジェクトを削除" },
            new[] { "Remove empty objects at the end of the full automatic preparation", "전체 자동 처리 마지막에 빈 오브젝트 정리", "全自動処理の最後に空オブジェクトを整理" }
        };

        private Language language;
        private Feature feature;
        private VRCAvatarDescriptor avatar;
        private GameObject vrmAvatar;
        private string vrmStatus;
        private MessageType vrmStatusType = MessageType.Info;
        private bool advanced;
        private DiNeVrmPhysBoneMode physBoneMode = DiNeVrmPhysBoneMode.Convert;
        private DiNeVrmMaterialOptions materialOptions = DiNeVrmMaterialOptions.Default;
        private bool convertMaterialsInAuto = true;
        private bool freezeTPoseInAuto;
        private bool removeEmptyObjectsInAuto;
        private List<DiNeVrmIssue> preflightIssues;
        private Vector2 scroll;
        private Texture2D windowIcon;
        private Texture2D tabIcon;
        private Font titleFont;

        private int L => (int)language;
        private string T(int index) => UiText[index][L];

        [MenuItem("DiNe/EX/Extra Modifier", false, 103)]
        private static void Open()
        {
            var window = GetWindow<DiNeExtraModifierWindow>();
            window.minSize = new Vector2(400f, 480f);
            window.position = new Rect(window.position.x, window.position.y, 440f, 620f);
            window.Show();
            window.Focus();
        }

        private static void ApplyFromHierarchy()
        {
            var selectedAvatar = FindAvatar(Selection.activeGameObject);
            if (selectedAvatar != null)
                EnableFocus(selectedAvatar);
        }

        private static bool ValidateApplyFromHierarchy()
        {
            return FindAvatar(Selection.activeGameObject) != null;
        }

        private void OnEnable()
        {
            windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            tabIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
            titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
            titleContent = new GUIContent("Modifier", tabIcon);
            language = (Language)Mathf.Clamp(EditorPrefs.GetInt(LanguagePrefKey, (int)Language.Korean), 0, 2);
            feature = Feature.Vrm;
            TryUseSelection();
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            TryUseSelection();
            Repaint();
        }

        private void TryUseSelection()
        {
            var selectedAvatar = FindAvatar(Selection.activeGameObject);
            if (selectedAvatar != null)
            {
                avatar = selectedAvatar;
                vrmAvatar = selectedAvatar.gameObject;
            }
        }

        private void OnGUI()
        {
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            DrawHeader();
            GUILayout.Space(5f);
            DrawLanguageToolbar();
            GUILayout.Space(6f);
            DrawFeatureToolbar();
            GUILayout.Space(8f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (feature)
            {
                case Feature.Vrm:
                    DrawVrmTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            const float iconSize = 72f;
            if (windowIcon != null)
                GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
            GUILayout.Space(6f);

            GUILayout.Label("Extra Modifier", new GUIStyle(EditorStyles.label)
            {
                font = titleFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 36,
                normal = { textColor = Color.white }
            }, GUILayout.Height(iconSize));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label(T(0), new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            });
            GUILayout.Space(5f);
            EditorGUILayout.EndVertical();
        }

        private void DrawLanguageToolbar()
        {
            var next = DrawToolbar((int)language, new[] { "English", "한국어", "日本語" }, 28f);
            if (next == (int)language)
                return;

            language = (Language)next;
            EditorPrefs.SetInt(LanguagePrefKey, next);
            Repaint();
        }

        private void DrawFeatureToolbar()
        {
            feature = Feature.Vrm;
            DrawToolbar(0, new[] { T(18) }, 32f);
        }

        private void DrawAvatarSection()
        {
            SectionLabel(T(1));
            GUILayout.Space(4f);

            EditorGUILayout.BeginVertical("box");
            avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(T(2), avatar, typeof(VRCAvatarDescriptor), true);
            if (avatar == null)
                EditorGUILayout.HelpBox(T(3), MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawFocusSection()
        {
            SectionLabel(T(4));
            GUILayout.Space(4f);

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(17), new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = AccentColor }
            });
            GUILayout.Label(T(5), EditorStyles.wordWrappedLabel);
            HLine();

            var focus = avatar != null ? DiNeFocusProcessor.FindSettings(avatar) : null;
            DrawStatus(focus != null);
            GUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(avatar == null || focus != null))
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = AccentColor;
                var buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
                if (GUILayout.Button(focus == null ? T(8) : T(9), buttonStyle, GUILayout.Height(40f)))
                    focus = EnableFocus(avatar);
                GUI.backgroundColor = previousColor;
            }

            if (focus != null)
                DrawAdvancedSettings(focus);

            EditorGUILayout.EndVertical();
        }

        private void DrawStatus(bool enabled)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(enabled ? T(13) : T(14), EditorStyles.wordWrappedMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(enabled ? T(7) : T(6), new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = enabled ? AccentColor : new Color(0.75f, 0.55f, 0.4f) }
            }, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSettings(DiNeFocus focus)
        {
            GUILayout.Space(5f);
            advanced = EditorGUILayout.Foldout(advanced, T(10), true);
            if (!advanced)
                return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var forceAll = EditorGUILayout.ToggleLeft(T(11), focus.ForceAll);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(focus, "Change Focus mode");
                focus.SetForceAll(forceAll);
                EditorUtility.SetDirty(focus);
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(5f);
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = DangerColor;
            if (GUILayout.Button(T(12), GUILayout.Height(24f)))
            {
                Undo.DestroyObjectImmediate(focus);
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = previousColor;
        }

        private void DrawVrmTab()
        {
            SectionLabel(T(19));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            vrmAvatar = (GameObject)EditorGUILayout.ObjectField(T(20), vrmAvatar, typeof(GameObject), true);
            if (vrmAvatar == null)
                EditorGUILayout.HelpBox(T(21), MessageType.Info);
            else
                EditorGUILayout.HelpBox(T(22), MessageType.Warning);

            using (new EditorGUI.DisabledScope(vrmAvatar == null))
            {
                if (GUILayout.Button(T(23), GUILayout.Height(28f)))
                {
                    var copy = DiNeVrmUtility.CreateWorkingCopy(vrmAvatar);
                    if (copy != null)
                    {
                        vrmAvatar = copy;
                        SetVrmStatus(T(39), MessageType.Info);
                    }
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);
            SectionLabel(T(24));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(25), EditorStyles.wordWrappedLabel);
            var autoNeedsUniVrm = physBoneMode == DiNeVrmPhysBoneMode.Convert;
            if (autoNeedsUniVrm && !DiNeVrmUtility.IsUniVrmAvailable)
                EditorGUILayout.HelpBox(T(33), MessageType.Error);

            convertMaterialsInAuto = EditorGUILayout.ToggleLeft(T(63), convertMaterialsInAuto);
            removeEmptyObjectsInAuto = EditorGUILayout.ToggleLeft(T(89), removeEmptyObjectsInAuto);
            using (new EditorGUI.DisabledScope(!DiNeUniVrmBridge.IsAvailable(DiNeUniVrmAction.FreezeTPose)))
                freezeTPoseInAuto = EditorGUILayout.ToggleLeft(T(84), freezeTPoseInAuto);

            using (new EditorGUI.DisabledScope(vrmAvatar == null || (autoNeedsUniVrm && !DiNeVrmUtility.IsUniVrmAvailable)))
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = AccentColor;
                if (GUILayout.Button(T(26), ActionButtonStyle(), GUILayout.Height(42f)))
                {
                    var options = new DiNeVrmOptions
                    {
                        PhysBoneMode = physBoneMode,
                        ConvertMaterials = convertMaterialsInAuto && DiNeVrmMaterialConverter.IsMToonAvailable,
                        MaterialOptions = materialOptions,
                        FreezeTPose = freezeTPoseInAuto && DiNeUniVrmBridge.IsAvailable(DiNeUniVrmAction.FreezeTPose),
                        RemoveEmptyObjects = removeEmptyObjectsInAuto
                    };
                    var report = DiNeVrmUtility.RunAll(vrmAvatar, options, out var copy);
                    if (copy != null)
                        vrmAvatar = copy;
                    ShowVrmReport(report);
                }
                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);
            DrawVrmActionSection(27, 28, 29, true, () => DiNeVrmUtility.MergeOutfitBones(vrmAvatar));
            GUILayout.Space(8f);
            DrawPhysBoneSection();
            GUILayout.Space(8f);
            DrawVrmActionSection(34, 35, 36, true, () => DiNeVrmUtility.CleanupForVrm(vrmAvatar));
            GUILayout.Space(8f);
            DrawMaterialSection();
            GUILayout.Space(8f);
            DrawVrmActionSection(86, 87, 88, true, () => DiNeVrmUtility.RemoveEmptyObjects(vrmAvatar));
            GUILayout.Space(10f);
            DrawUniVrmSection();

            if (!string.IsNullOrEmpty(vrmStatus))
            {
                GUILayout.Space(10f);
                SectionLabel(T(37));
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox(vrmStatus, vrmStatusType);
            }
        }

        private void DrawPhysBoneSection()
        {
            SectionLabel(T(30));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(41), EditorStyles.miniBoldLabel);

            var modeIndex = DrawToolbar((int)physBoneMode, new[] { T(42), T(43), T(44) }, 24f);
            physBoneMode = (DiNeVrmPhysBoneMode)modeIndex;
            GUILayout.Space(4f);

            switch (physBoneMode)
            {
                case DiNeVrmPhysBoneMode.Delete:
                    GUILayout.Label(T(46), EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(vrmAvatar == null))
                    {
                        var previousColor = GUI.backgroundColor;
                        GUI.backgroundColor = DangerColor;
                        if (GUILayout.Button(T(48), GUILayout.Height(30f)))
                            ShowVrmReport(DiNeVrmUtility.RemovePhysBones(vrmAvatar));
                        GUI.backgroundColor = previousColor;
                    }
                    break;

                case DiNeVrmPhysBoneMode.Keep:
                    GUILayout.Label(T(47), EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Button(T(49), GUILayout.Height(30f));
                    break;

                default:
                    GUILayout.Label(T(45), EditorStyles.wordWrappedLabel);
                    if (!DiNeVrmUtility.IsUniVrmAvailable)
                        EditorGUILayout.HelpBox(T(33), MessageType.Error);
                    using (new EditorGUI.DisabledScope(vrmAvatar == null || !DiNeVrmUtility.IsUniVrmAvailable))
                    {
                        if (GUILayout.Button(T(32), GUILayout.Height(30f)))
                            ShowVrmReport(DiNeVrmUtility.ConvertPhysBones(vrmAvatar));
                    }
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialSection()
        {
            SectionLabel(T(50));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(51), EditorStyles.wordWrappedLabel);

            var available = DiNeVrmMaterialConverter.IsMToonAvailable;
            if (!available)
                EditorGUILayout.HelpBox(T(61), MessageType.Error);

            GUILayout.Space(4f);
            materialOptions.KeepNormalMap = EditorGUILayout.ToggleLeft(T(53), materialOptions.KeepNormalMap);
            materialOptions.KeepMatcap = EditorGUILayout.ToggleLeft(T(54), materialOptions.KeepMatcap);
            materialOptions.KeepEmission = EditorGUILayout.ToggleLeft(T(55), materialOptions.KeepEmission);
            materialOptions.KeepShadeColor = EditorGUILayout.ToggleLeft(T(56), materialOptions.KeepShadeColor);
            materialOptions.KeepOutline = EditorGUILayout.ToggleLeft(T(57), materialOptions.KeepOutline);
            materialOptions.KeepRim = EditorGUILayout.ToggleLeft(T(58), materialOptions.KeepRim);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(T(59), EditorStyles.miniButtonLeft, GUILayout.Height(20f)))
            {
                var saveAsAssets = materialOptions.SaveAsAssets;
                materialOptions = DiNeVrmMaterialOptions.Preserve;
                materialOptions.SaveAsAssets = saveAsAssets;
            }
            if (GUILayout.Button(T(60), EditorStyles.miniButtonRight, GUILayout.Height(20f)))
            {
                var saveAsAssets = materialOptions.SaveAsAssets;
                materialOptions = DiNeVrmMaterialOptions.Strip;
                materialOptions.SaveAsAssets = saveAsAssets;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4f);
            materialOptions.SaveAsAssets = EditorGUILayout.ToggleLeft(T(62), materialOptions.SaveAsAssets);

            GUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(vrmAvatar == null || !available))
            {
                if (GUILayout.Button(T(52), GUILayout.Height(30f)))
                    ShowVrmReport(DiNeVrmMaterialConverter.ConvertToMToon(vrmAvatar, materialOptions));
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawUniVrmSection()
        {
            SectionLabel(T(64));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(65), EditorStyles.wordWrappedLabel);

            var installed = DiNeUniVrmBridge.DescribeInstallation();
            if (string.IsNullOrEmpty(installed))
                EditorGUILayout.HelpBox(T(67), MessageType.Error);
            else
                GUILayout.Label($"{T(66)}: {installed}", EditorStyles.miniLabel);

            GUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(vrmAvatar == null))
            {
                if (GUILayout.Button(T(68), GUILayout.Height(26f)))
                    preflightIssues = DiNeVrmPreflight.Run(vrmAvatar);
            }

            if (preflightIssues != null)
            {
                GUILayout.Space(4f);
                if (preflightIssues.Count == 0)
                {
                    EditorGUILayout.HelpBox(T(69), MessageType.Info);
                }
                else
                {
                    Action pendingFix = null;
                    foreach (var issue in preflightIssues.ToArray())
                    {
                        var requested = DrawIssue(issue);
                        pendingFix = pendingFix ?? requested;
                    }

                    if (pendingFix != null)
                    {
                        // 레이아웃이 끝난 뒤에 실행해야 GUI 그리는 도중 컴포넌트가 사라지지 않는다.
                        EditorApplication.delayCall += () =>
                        {
                            pendingFix();
                            preflightIssues = vrmAvatar != null ? DiNeVrmPreflight.Run(vrmAvatar) : null;
                            Repaint();
                        };
                    }
                }
            }

            GUILayout.Space(6f);
            HLine();
            DrawBridgeButton(71, DiNeUniVrmAction.FreezeTPose);
            DrawBridgeButton(72, DiNeUniVrmAction.MeshUtility);
            DrawBridgeButton(73, DiNeUniVrmAction.ExportVrm0);
            if (DiNeUniVrmBridge.HasVrm1)
                DrawBridgeButton(74, DiNeUniVrmAction.ExportVrm1);
            if (DiNeUniVrmBridge.HasVrmConverterForVrChat)
                DrawBridgeButton(75, DiNeUniVrmAction.VrmConverterForVrChat);

            EditorGUILayout.EndVertical();
        }

        private void DrawBridgeButton(int textIndex, DiNeUniVrmAction action)
        {
            using (new EditorGUI.DisabledScope(vrmAvatar == null || !DiNeUniVrmBridge.IsAvailable(action)))
            {
                if (!GUILayout.Button(T(textIndex), GUILayout.Height(26f)))
                    return;

                // UniVRM 창을 여는 동작이라 레이아웃이 끝난 뒤에 호출한다.
                var target = vrmAvatar;
                EditorApplication.delayCall += () =>
                {
                    if (DiNeUniVrmBridge.Invoke(action, target, out var message))
                        preflightIssues = target != null ? DiNeVrmPreflight.Run(target) : null;
                    else
                        SetVrmStatus(message, MessageType.Error);
                    Repaint();
                };
            }
        }

        /// <summary>항목을 그리고, 사용자가 [고치기]를 눌렀으면 실행할 동작을 돌려준다.</summary>
        private Action DrawIssue(DiNeVrmIssue issue)
        {
            Action requested = null;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(DescribeIssue(issue), ToMessageType(issue.Severity));

            var fix = GetIssueFix(issue.Kind);
            using (new EditorGUI.DisabledScope(fix == null))
            {
                if (GUILayout.Button(T(70), GUILayout.Width(60f), GUILayout.Height(32f)) && fix != null)
                    requested = fix;
            }
            EditorGUILayout.EndHorizontal();
            return requested;
        }

        private string DescribeIssue(DiNeVrmIssue issue)
        {
            switch (issue.Kind)
            {
                case DiNeVrmIssueKind.UniVrmMissing: return T(76);
                case DiNeVrmIssueKind.NoHumanoid: return T(77);
                case DiNeVrmIssueKind.RootTransform: return T(78);
                case DiNeVrmIssueKind.MissingScripts: return string.Format(T(79), issue.Count);
                case DiNeVrmIssueKind.PhysBones: return string.Format(T(80), issue.Count);
                case DiNeVrmIssueKind.VrcComponents: return string.Format(T(81), issue.Count);
                case DiNeVrmIssueKind.UnsupportedShaders: return string.Format(T(82), issue.Count, issue.Detail);
                case DiNeVrmIssueKind.NoVrmMeta: return T(83);
                case DiNeVrmIssueKind.SpringBoneReferences: return string.Format(T(85), issue.Count);
                default: return issue.Kind.ToString();
            }
        }

        /// <summary>사전 점검 항목을 우리 기능으로 고치는 동작. 고칠 수 없으면 null.</summary>
        private Action GetIssueFix(DiNeVrmIssueKind kind)
        {
            switch (kind)
            {
                case DiNeVrmIssueKind.RootTransform:
                    return () =>
                    {
                        if (!DiNeUniVrmBridge.Invoke(DiNeUniVrmAction.FreezeTPose, vrmAvatar, out var message))
                            SetVrmStatus(message, MessageType.Error);
                    };
                case DiNeVrmIssueKind.MissingScripts:
                case DiNeVrmIssueKind.VrcComponents:
                    return () => ShowVrmReport(DiNeVrmUtility.CleanupForVrm(vrmAvatar));
                case DiNeVrmIssueKind.PhysBones:
                    return () => ShowVrmReport(DiNeVrmUtility.ProcessPhysBones(vrmAvatar, physBoneMode));
                case DiNeVrmIssueKind.SpringBoneReferences:
                    return () => ShowVrmReport(DiNeVrmUtility.RepairSpringBones(vrmAvatar));
                case DiNeVrmIssueKind.UnsupportedShaders:
                    return DiNeVrmMaterialConverter.IsMToonAvailable
                        ? (Action)(() => ShowVrmReport(DiNeVrmMaterialConverter.ConvertToMToon(vrmAvatar, materialOptions)))
                        : null;
                default:
                    return null;
            }
        }

        private static MessageType ToMessageType(DiNeVrmIssueSeverity severity)
        {
            switch (severity)
            {
                case DiNeVrmIssueSeverity.Error: return MessageType.Error;
                case DiNeVrmIssueSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        private void DrawVrmActionSection(int titleIndex, int descriptionIndex, int buttonIndex, bool available, Func<DiNeVrmReport> operation)
        {
            SectionLabel(T(titleIndex));
            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(T(descriptionIndex), EditorStyles.wordWrappedLabel);
            if (!available)
                EditorGUILayout.HelpBox(T(33), MessageType.Error);

            using (new EditorGUI.DisabledScope(vrmAvatar == null || !available))
            {
                if (GUILayout.Button(T(buttonIndex), GUILayout.Height(30f)))
                    ShowVrmReport(operation());
            }
            EditorGUILayout.EndVertical();
        }

        private void ShowVrmReport(DiNeVrmReport report)
        {
            if (report == null)
                return;
            if (!report.Succeeded)
            {
                var errorMessage = report.Error != null && report.Error.Contains("UniVRM") ? T(33) : report.Error;
                SetVrmStatus(errorMessage, MessageType.Error);
                return;
            }

            string message;
            switch (language)
            {
                case Language.Korean:
                    message = $"병합 본 {report.MergedBones} / SpringBone {report.SpringBones}(콜라이더 그룹 {report.SpringColliders}, 미지원 {report.SkippedColliders})" +
                              $" / PhysBone 삭제 {report.RemovedPhysBones} / 유지 {report.KeptPhysBones}" +
                              $" / Constraint 변환 {report.ConvertedConstraints} / 제거 컴포넌트 {report.RemovedComponents}" +
                              $"\n머티리얼 {report.ConvertedMaterials} (노말 {report.KeptNormalMaps}, 맷캡 {report.KeptMatcaps}, 이미시브 {report.KeptEmissions}, 아웃라인 {report.KeptOutlines}, 림 {report.KeptRims}, 버림 {report.DroppedMaterialFeatures})";
                    break;
                case Language.Japanese:
                    message = $"統合ボーン {report.MergedBones} / SpringBone {report.SpringBones}(コライダーグループ {report.SpringColliders}、非対応 {report.SkippedColliders})" +
                              $" / PhysBone削除 {report.RemovedPhysBones} / 維持 {report.KeptPhysBones}" +
                              $" / Constraint変換 {report.ConvertedConstraints} / 削除コンポーネント {report.RemovedComponents}" +
                              $"\nマテリアル {report.ConvertedMaterials}（ノーマル {report.KeptNormalMaps}、マットキャップ {report.KeptMatcaps}、エミッション {report.KeptEmissions}、アウトライン {report.KeptOutlines}、リム {report.KeptRims}、破棄 {report.DroppedMaterialFeatures}）";
                    break;
                default:
                    message = $"Merged bones {report.MergedBones} / SpringBones {report.SpringBones} (collider groups {report.SpringColliders}, unsupported {report.SkippedColliders})" +
                              $" / PhysBones deleted {report.RemovedPhysBones} / kept {report.KeptPhysBones}" +
                              $" / Converted constraints {report.ConvertedConstraints} / Removed components {report.RemovedComponents}" +
                              $"\nMaterials {report.ConvertedMaterials} (normal {report.KeptNormalMaps}, matcap {report.KeptMatcaps}, emission {report.KeptEmissions}, outline {report.KeptOutlines}, rim {report.KeptRims}, dropped {report.DroppedMaterialFeatures})";
                    break;
            }
            if (!string.IsNullOrEmpty(report.Warning))
                message += $"\n{report.Warning}";
            SetVrmStatus(message, string.IsNullOrEmpty(report.Warning) ? MessageType.Info : MessageType.Warning);
        }

        private void SetVrmStatus(string message, MessageType type)
        {
            vrmStatus = string.IsNullOrEmpty(message) ? T(38) : message;
            vrmStatusType = type;
            Repaint();
        }

        private static GUIStyle ActionButtonStyle()
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }

        private static int DrawToolbar(int selected, string[] options, float height)
        {
            EditorGUILayout.BeginHorizontal();
            var result = selected;
            for (var i = 0; i < options.Length; i++)
            {
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = i == selected ? AccentColor : InactiveColor;
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = i == selected ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
                };
                if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
                    result = i;
                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndHorizontal();
            return result;
        }

        private static void SectionLabel(string text)
        {
            GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = AccentColor }
            });
        }

        private static void HLine()
        {
            GUILayout.Space(5f);
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(5f);
        }

        private static DiNeFocus EnableFocus(VRCAvatarDescriptor targetAvatar)
        {
            if (targetAvatar == null)
                return null;

            var existing = DiNeFocusProcessor.FindSettings(targetAvatar);
            if (existing != null)
                return existing;

            var component = Undo.AddComponent<DiNeFocus>(targetAvatar.gameObject);
            EditorUtility.SetDirty(targetAvatar.gameObject);
            Selection.activeGameObject = targetAvatar.gameObject;
            Debug.Log($"[DiNe Extra Modifier] Applied Focus to '{targetAvatar.name}'.", component);
            return component;
        }

        private static VRCAvatarDescriptor FindAvatar(GameObject selected)
        {
            return selected != null ? selected.GetComponentInParent<VRCAvatarDescriptor>(true) : null;
        }

        internal static int CurrentLanguage => Mathf.Clamp(EditorPrefs.GetInt(LanguagePrefKey, (int)Language.Korean), 0, 2);
        internal static string InspectorInfo => UiText[40][CurrentLanguage];
    }

    [CustomEditor(typeof(DiNeFocus))]
    internal sealed class DiNeFocusEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(DiNeExtraModifierWindow.InspectorInfo, MessageType.Warning);
        }
    }
}
