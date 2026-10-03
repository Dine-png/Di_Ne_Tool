using UnityEngine;

public partial class DiNeMaterialTool
{
    private DiNeGuidedTutorial _tutorial;
    private DiNeTutorialStep[][] _tutorialCourses;

    private void BeginTutorialFrame()
    {
        if (_tutorial == null) _tutorial = new DiNeGuidedTutorial(this, "material-tool");
        if (_tutorialCourses == null)
        {
            var target = DiNeTutorialStep.Required("target", "Assign the object whose materials you want to use.", "마테리얼을 사용할 오브젝트를 지정하세요.", "マテリアルを使うオブジェクトを指定してください。", () => _targetObject != null);
            var children = DiNeTutorialStep.Optional("children", "Choose whether to include child objects.", "자식 오브젝트 포함 여부를 선택하세요.", "子オブジェクトを含めるか選んでください。");
            var inactive = DiNeTutorialStep.Optional("inactive", "Choose whether to include inactive objects.", "비활성 오브젝트 포함 여부를 선택하세요.", "非アクティブなオブジェクトを含めるか選んでください。");
            _tutorialCourses = new[]
            {
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("preview", "Keep Preview Mode on to check the result before applying.", "미리 확인하려면 미리보기 모드를 켜세요.", "適用前に確認するにはプレビューモードをオンにしてください。"),
                    DiNeTutorialStep.Optional("refresh", "Refresh to scan this object's materials again.", "새로고침을 눌러 마테리얼을 다시 스캔하세요.", "更新を押してマテリアルを再スキャンしてください。"),
                    DiNeTutorialStep.Optional("library-scan", "Scan Project to refresh the preset library.", "프로젝트 스캔을 눌러 프리셋 목록을 갱신하세요.", "プロジェクトスキャンでプリセット一覧を更新してください。"),
                    DiNeTutorialStep.Optional("library-category", "Choose a preset category.", "프리셋 카테고리를 선택하세요.", "プリセットのカテゴリを選んでください。"),
                    DiNeTutorialStep.Optional("library-search", "Type a preset name to find it. Use × to clear the search.", "프리셋 이름을 검색하세요. ×를 누르면 검색을 지웁니다.", "プリセット名を検索してください。×で検索を消せます。"),
                    DiNeTutorialStep.Optional("library-preset", "Click the preset you want to use.", "사용할 프리셋을 클릭하세요.", "使うプリセットをクリックしてください。"),
                    DiNeTutorialStep.Optional("preset-materials", "Check the materials to apply the preset to. Use All or Clear for the list.", "프리셋을 적용할 마테리얼을 체크하세요. 전체 선택·해제로 목록을 바꿉니다.", "適用するマテリアルをチェックしてください。全て選択・解除で一覧を変更できます。"),
                    DiNeTutorialStep.Optional("material-ping", "Click Ping to locate a material in the Project window.", "Ping을 눌러 프로젝트에서 마테리얼을 찾으세요.", "PingでProjectウィンドウのマテリアルを確認してください。"),
                    DiNeTutorialStep.Optional("preset-apply", "Click Preview Check to review, or switch to Apply Mode and click Apply Preset.", "미리보기 확인을 누르세요. 적용하려면 적용 모드에서 프리셋 적용을 누릅니다.", "プレビュー確認を押してください。適用するには適用モードでプリセット適用を押します。"),
                },
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("preview", "Turn Preview Mode on to review the removal list.", "제거 목록을 확인하려면 미리보기 모드를 켜세요.", "削除一覧を確認するにはプレビューモードをオンにしてください。"),
                    DiNeTutorialStep.Optional("refresh", "Refresh to scan the materials again.", "새로고침을 눌러 마테리얼을 다시 스캔하세요.", "更新でマテリアルを再スキャンしてください。"),
                    DiNeTutorialStep.Optional("diet-sections", "Check the sections to clean. Select All and Deselect All change every section.", "정리할 섹션을 체크하세요. 전체 선택·해제로 모든 섹션을 바꿉니다.", "整理するセクションをチェックしてください。全て選択・解除で全セクションを変更できます。"),
                    DiNeTutorialStep.Optional("diet-materials", "Check the materials to clean and expand them to view their textures.", "정리할 마테리얼을 체크하고 펼쳐 텍스처를 확인하세요.", "整理するマテリアルをチェックし、展開してテクスチャを確認してください。"),
                    DiNeTutorialStep.Optional("material-ping", "Click Ping to locate the material.", "Ping을 눌러 마테리얼을 찾으세요.", "Pingでマテリアルを確認してください。"),
                    DiNeTutorialStep.Optional("diet-remove", "Use Remove Textures Only to keep the feature settings. Preview Mode shows the removal list.", "텍스처만 제거하려면 텍스쳐만 제거를 누르세요. 미리보기 모드에서는 목록을 확인합니다.", "機能設定を残すにはテクスチャのみ削除を押してください。プレビューモードでは一覧を確認できます。"),
                    DiNeTutorialStep.Optional("diet-disable", "Use Remove + Disable to also turn the selected features off.", "선택한 기능도 끄려면 제거 + 기능 끄기를 누르세요.", "選んだ機能もオフにするには削除＋機能を無効化を押してください。"),
                },
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("refresh", "Refresh to scan textures and their VRAM usage.", "새로고침을 눌러 텍스처와 VRAM 사용량을 확인하세요.", "更新でテクスチャとVRAM使用量を確認してください。"),
                    DiNeTutorialStep.Optional("vram-select", "Check textures to change together. Use All or Clear for the list.", "함께 변경할 텍스처를 체크하세요. 전체·해제로 목록을 바꿉니다.", "まとめて変更するテクスチャをチェックしてください。全体・解除で一覧を変更できます。"),
                    DiNeTutorialStep.Optional("vram-bulk-format", "Enable Format and choose the compression for selected textures.", "압축을 체크하고 선택 텍스처의 압축 방식을 고르세요.", "形式をオンにし、選択テクスチャの圧縮方式を選んでください。"),
                    DiNeTutorialStep.Optional("vram-bulk-size", "Enable Size and choose the maximum resolution.", "해상도를 체크하고 최대 해상도를 고르세요.", "解像度をオンにし、最大解像度を選んでください。"),
                    DiNeTutorialStep.Optional("vram-bulk-apply", "Apply updates the checked textures with these settings.", "적용을 누르면 체크한 텍스처를 이 설정으로 변경합니다.", "適用でチェックしたテクスチャをこの設定に変更できます。"),
                    DiNeTutorialStep.Optional("vram-thumbnail", "Click a thumbnail to locate the texture; double-click to preview it.", "썸네일을 클릭하면 텍스처를 찾고, 두 번 클릭하면 미리봅니다.", "サムネイルをクリックすると場所を確認でき、ダブルクリックでプレビューできます。"),
                    DiNeTutorialStep.Optional("vram-format", "Use a texture's Format list to change only its compression.", "텍스처의 압축 목록에서 개별 압축 방식을 변경하세요.", "テクスチャの形式一覧で個別の圧縮方式を変更してください。"),
                    DiNeTutorialStep.Optional("vram-size", "Use a texture's Size list to change only its maximum resolution.", "텍스처의 해상도 목록에서 개별 최대 해상도를 변경하세요.", "テクスチャの解像度一覧で個別の最大解像度を変更してください。"),
                    DiNeTutorialStep.Optional("vram-references", "Expand Objects or Mats to locate where the texture is used.", "오브젝트·마테리얼 목록을 펼쳐 사용 위치를 확인하세요.", "オブジェクト・マテリアル一覧を展開し、使用場所を確認してください。"),
                    DiNeTutorialStep.Optional("vram-optimize", "Use Optimize on a texture to apply its suggested changes.", "텍스처의 최적화를 누르면 제안된 변경을 적용합니다.", "テクスチャの最適化で提案された変更を適用できます。"),
                    DiNeTutorialStep.Optional("vram-optimize-all", "Optimize All applies the suggested changes to all eligible textures.", "전체 최적화를 누르면 가능한 텍스처에 제안된 변경을 적용합니다.", "全て最適化で対象テクスチャに提案された変更を適用できます。"),
                },
                new[]
                {
                    target, children, inactive,
                    DiNeTutorialStep.Optional("refresh", "Refresh to scan materials again.", "새로고침을 눌러 마테리얼을 다시 스캔하세요.", "更新でマテリアルを再スキャンしてください。"),
                    DiNeTutorialStep.Optional("bulk-group", "Choose the shader group to edit when several groups are available.", "여러 그룹이 있으면 조절할 쉐이더 그룹을 선택하세요.", "複数ある場合は調整するシェーダーグループを選んでください。"),
                    DiNeTutorialStep.Optional("bulk-list", "Expand Materials, then check the materials to edit together.", "마테리얼 목록을 펼치고 함께 조절할 항목을 체크하세요.", "マテリアル一覧を展開し、まとめて調整する項目をチェックしてください。", () => _bulkListFoldout = true),
                    DiNeTutorialStep.Optional("material-ping", "Click the small Ping button to locate a material.", "작은 Ping 버튼을 눌러 마테리얼을 찾으세요.", "小さいPingボタンでマテリアルの場所を確認してください。"),
                    DiNeTutorialStep.Optional("bulk-properties", "Change a field here to update every checked material. Use Ctrl+Z to undo.", "여기서 값을 바꾸면 체크한 마테리얼에 함께 적용됩니다. Ctrl+Z로 되돌립니다.", "ここで値を変更するとチェックしたマテリアルに適用されます。Ctrl+Zで戻せます。"),
                },
            };
        }
        var names = new[] { new[] { "Preset Apply", "프리셋 적용", "プリセット適用" }, new[] { "Diet", "다이어트", "ダイエット" }, new[] { "VRAM Optimize", "VRAM 최적화", "VRAM最適化" }, new[] { "Bulk Edit", "일괄 조절", "一括調整" } };
        int course = (int)_mode;
        _tutorial.Configure(_mode.ToString(), names[course][0], names[course][1], names[course][2], _tutorialCourses[course]);
        _tutorial.BeginFrame();
    }

    private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
    private void TutorialAnchor(string id, Rect rect) => _tutorial?.Anchor(id, rect);
    private void TutorialDraw(params string[] ids) { foreach (string id in ids) _tutorial?.Draw(id); }
    private void TutorialNotify(string id) => _tutorial?.NotifyAction(id);
}
