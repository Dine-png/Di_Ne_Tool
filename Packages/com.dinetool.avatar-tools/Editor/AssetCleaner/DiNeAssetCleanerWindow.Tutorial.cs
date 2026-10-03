using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DiNeTool.AssetCleaner
{
    public partial class DiNeAssetCleanerWindow
    {
        private DiNeGuidedTutorial guidedTutorial;
        private string[] tutorialAnalyzedScenes;

        private void OnGUI()
        {
            ConfigureTutorial();
            guidedTutorial.BeginFrame();
            try { DrawToolGUI(); guidedTutorial.Validate(); }
            finally { guidedTutorial.EndFrame(); }
        }

        private void ConfigureTutorial()
        {
            if (guidedTutorial == null) guidedTutorial = new DiNeGuidedTutorial(this, "AssetCleaner");
            guidedTutorial.Configure("Cleaner", "Asset Cleaner", "에셋 정리", "アセット整理", new[]
            {
                DiNeTutorialStep.Required("Scenes", "Select every scene whose assets you want to keep.", "에셋을 보존할 씬을 모두 선택하세요.", "アセットを残すシーンをすべて選んでください。", () => _scenes.Any(s => s.Selected && AssetDatabase.LoadAssetAtPath<SceneAsset>(s.Path) != null)),
                DiNeTutorialStep.Optional("Filters", "Open the cleanup filters.", "정리 필터를 펼치세요.", "整理フィルターを開いてください。"),
                DiNeTutorialStep.Optional("Categories", "Check the asset types to inspect.", "검사할 에셋 종류를 체크하세요.", "調べるアセットの種類をチェックしてください。", () => _filterFoldout = true),
                DiNeTutorialStep.Optional("EmptyFolders", "Choose whether to include empty folders.", "빈 폴더도 후보에 포함할지 정하세요.", "空のフォルダーも候補に含めるか選んでください。", () => _filterFoldout = true),
                DiNeTutorialStep.Optional("ProtectedFolders", "Add folders that must be kept.", "꼭 남길 폴더를 보호 폴더에 추가하세요.", "残すフォルダーを保護対象に追加してください。", () => _filterFoldout = true),
                DiNeTutorialStep.Required("Analyze", "Press Analyze to find unused candidates.", "분석을 눌러 미사용 후보를 찾으세요.", "分析を押して未使用候補を探してください。", TutorialAnalysisIsCurrent),
                DiNeTutorialStep.Optional("RootFolders", "Use this button to list completely unused root folders.", "완전히 미사용인 최상위 폴더만 보려면 이 버튼을 누르세요.", "完全に未使用な最上位フォルダーだけを見るには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Selection", "Check the candidates to clean up.", "정리할 후보를 체크하세요.", "整理する候補をチェックしてください。"),
                DiNeTutorialStep.Optional("Expand", "Use Expand / Collapse to inspect the folder tree.", "펼치기·접기로 폴더 안을 살펴보세요.", "開く・閉じるでフォルダーの中を確認してください。"),
                DiNeTutorialStep.Optional("Locate", "Press the locate button to reveal a candidate in Project.", "찾기 버튼으로 후보를 Project 창에서 확인하세요.", "検索ボタンで候補をProjectウィンドウに表示してください。"),
                DiNeTutorialStep.Optional("Preview", "Hover over a file to see its preview.", "파일에 마우스를 올려 미리보기를 확인하세요.", "ファイルにカーソルを合わせてプレビューを確認してください。"),
                DiNeTutorialStep.Optional("Trash", "Press this button to send checked items to the trash.", "체크한 항목을 휴지통으로 보내려면 이 버튼을 누르세요.", "チェックした項目をごみ箱へ送るには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("EmptyCleanup", "Press this button to clean up empty folders.", "빈 폴더만 정리하려면 이 버튼을 누르세요.", "空のフォルダーを整理するには、このボタンを押してください。"),
                DiNeTutorialStep.Optional("Status", "Read the analysis result here.", "여기서 분석 결과를 확인하세요.", "ここで分析結果を確認してください。")
            });
        }

        private bool TutorialAnalysisIsCurrent()
        {
            return _analyzed && _root != null && tutorialAnalyzedScenes != null &&
                tutorialAnalyzedScenes.SequenceEqual(_scenes.Where(s => s.Selected).Select(s => s.Path).OrderBy(p => p, StringComparer.Ordinal));
        }

        private void OnDisable() => guidedTutorial?.Suspend();
    }
}
