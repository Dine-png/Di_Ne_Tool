using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public partial class DiNeAnimationTool
{
    [SerializeField] private RuntimeAnimatorController repairController;
    [SerializeField] private List<AnimationClip> repairClips = new List<AnimationClip>();
    [SerializeField] private List<DiNeAnimationRepairCore.BindingMapping> repairMappings = new List<DiNeAnimationRepairCore.BindingMapping>();
    [SerializeField] private List<AnimationClip> repairSavedClips = new List<AnimationClip>();
    [SerializeField] private bool repairShowVerified;
    private AnimationClip repairClipToAdd;
    private List<DiNeAnimationRepairCore.BindingGroup> repairGroups;
    private DiNeAnimationRepairCore.RepairPlan repairPlan;
    private string repairStatus;
    private bool repairStatusError;
    [SerializeField] private string repairOutputFolder;

    [Serializable]
    private sealed class RepairSaveReport
    {
        public int Version = 1;
        public List<RepairSavedClip> Clips = new List<RepairSavedClip>();
        public List<RepairRemainingConnection> RemainingConnections = new List<RepairRemainingConnection>();
    }

    [Serializable]
    private sealed class RepairSavedClip
    {
        public string SourceAssetPath;
        public string SourceGuid;
        public string SourceClipName;
        public string RepairedAssetPath;
    }

    [Serializable]
    private sealed class RepairRemainingConnection
    {
        public string Path;
        public string Property;
        public string Component;
        public string Status;
        public string[] Clips;
    }

    private void ResetRepairAnalysis()
    {
        repairGroups = null;
        repairPlan = null;
        repairStatus = null;
    }

    private void DrawRepairGUI()
    {
        BeginCard(Tr("Animations to inspect", "검사할 애니메이션", "検査するアニメーション"));
        EditorGUILayout.LabelField(Tr("Add clips or collect the clips used by a controller. The same connection is grouped across clips.",
            "클립을 추가하거나 컨트롤러가 사용하는 클립을 모으세요. 여러 클립의 같은 연결은 함께 표시됩니다.",
            "クリップを追加するか、コントローラーのクリップを収集します。同じ接続はクリップ間でまとめて表示します。"), EditorStyles.wordWrappedLabel);
        EditorGUI.BeginChangeCheck();
        repairController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(Tr("Controller", "컨트롤러", "コントローラー"), repairController, typeof(RuntimeAnimatorController), false);
        if (EditorGUI.EndChangeCheck()) ResetRepairAnalysis();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!repairController))
                if (GUILayout.Button(Tr("Collect controller clips", "컨트롤러 클립 모으기", "コントローラーから収集"), GUILayout.Height(24))) AddRepairClips(DiNeAnimationRepairCore.CollectClips(repairController));
            using (new EditorGUI.DisabledScope(!targetAvatarRoot))
                if (GUILayout.Button(Tr("Collect avatar FX clips", "아바타 FX 클립 모으기", "アバターのFXから収集"), GUILayout.Height(24))) CollectRepairAvatarFX();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            repairClipToAdd = (AnimationClip)EditorGUILayout.ObjectField(Tr("Add clip", "클립 추가", "クリップを追加"), repairClipToAdd, typeof(AnimationClip), false);
            using (new EditorGUI.DisabledScope(!repairClipToAdd))
                if (GUILayout.Button(Tr("Add", "추가", "追加"), GUILayout.Width(60), GUILayout.Height(24)))
                { AddRepairClips(new[] { repairClipToAdd }); repairClipToAdd = null; }
        }
        Rect dropRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, Tr("Drop animation clips or controllers here", "애니메이션 클립 또는 컨트롤러를 여기에 놓으세요", "アニメーションクリップ・コントローラーをここにドロップ"), EditorStyles.helpBox);
        HandleRepairDrop(dropRect);
        for (int i = 0; i < repairClips.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                AnimationClip replacement = (AnimationClip)EditorGUILayout.ObjectField(repairClips[i], typeof(AnimationClip), false);
                if (EditorGUI.EndChangeCheck()) { repairClips[i] = replacement; ResetRepairAnalysis(); }
                using (new EditorGUI.DisabledScope(!repairClips[i]))
                    if (GUILayout.Button(Tr("Preview", "미리보기", "プレビュー"), GUILayout.Width(75), GUILayout.Height(24))) PreviewClip(repairClips[i]);
                if (GUILayout.Button(Tr("Remove", "제외", "除外"), GUILayout.Width(60), GUILayout.Height(24)))
                { repairClips.RemoveAt(i); ResetRepairAnalysis(); i--; }
            }
        }
        GUILayout.Space(5);
        using (new EditorGUI.DisabledScope(!targetAvatarRoot || !repairClips.Any(c => c) || EditorApplication.isPlayingOrWillChangePlaymode))
            if (PrimaryButton(Tr("Inspect connections", "연결 검사", "接続を検査"))) AnalyzeRepairClips();
        TutorialAnchor("repair-inspect");
        TutorialDraw("repair-inspect");
        EndCard();
        TutorialAnchor("repair-input");
        TutorialDraw("repair-input");

        BeginCard(Tr("Reusable mappings", "대응표 저장·불러오기", "対応表の保存・読み込み"));
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!repairMappings.Any(m => m != null && m.Enabled)))
                if (GUILayout.Button(Tr("Save mapping table", "대응표 저장", "対応表を保存"), GUILayout.Height(24))) SaveRepairProfile();
            if (GUILayout.Button(Tr("Load mapping table", "대응표 불러오기", "対応表を読み込む"), GUILayout.Height(24))) LoadRepairProfile();
            using (new EditorGUI.DisabledScope(!repairMappings.Any(m => m != null && m.Enabled)))
                if (GUILayout.Button(Tr("Reset", "초기화", "リセット"), GUILayout.Height(24)))
                {
                    repairMappings.Clear();
                    if (repairGroups != null) foreach (var group in repairGroups) GetRepairMapping(group);
                    RefreshRepairPlan();
                    SetRepairStatus(Tr("Mapping table reset. Source animations are unchanged.", "대응표를 초기화했습니다. 원본 애니메이션은 유지됩니다.", "対応表をリセットしました。元のアニメーションは保持されます。"));
                }
        }
        EndCard();

        if (repairGroups != null) DrawRepairMappings();
        if (repairSavedClips.Any(c => c)) DrawRepairOutputs();
        if (!string.IsNullOrEmpty(repairStatus)) EditorGUILayout.HelpBox(repairStatus, repairStatusError ? MessageType.Error : MessageType.Info);
    }

    private void CollectRepairAvatarFX()
    {
        var descriptor = targetAvatarRoot ? targetAvatarRoot.GetComponent<VRCAvatarDescriptor>() : null;
        RuntimeAnimatorController controller = null;
        if (descriptor && descriptor.customizeAnimationLayers && descriptor.baseAnimationLayers != null)
            foreach (VRCAvatarDescriptor.CustomAnimLayer layer in descriptor.baseAnimationLayers)
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault) { controller = layer.animatorController; break; }
        if (!controller)
        {
            SetRepairStatus(Tr("This avatar has no custom FX controller. Add clips or a controller above.",
                "이 아바타에 사용자 지정 FX 컨트롤러가 없습니다. 위에서 클립이나 컨트롤러를 추가하세요.",
                "このアバターにはカスタムFXコントローラーがありません。上でクリップかコントローラーを追加してください。"));
            return;
        }
        repairController = controller;
        AddRepairClips(DiNeAnimationRepairCore.CollectClips(controller));
    }

    private void AddRepairClips(IEnumerable<AnimationClip> clips)
    {
        int before = repairClips.Count;
        foreach (AnimationClip clip in clips) if (clip && !repairClips.Contains(clip)) repairClips.Add(clip);
        ResetRepairAnalysis();
        SetRepairStatus(string.Format(Tr("Added {0} clips.", "클립 {0}개를 추가했습니다.", "{0}個のクリップを追加しました。"), repairClips.Count - before));
    }

    private void HandleRepairDrop(Rect rect)
    {
        Event evt = Event.current;
        if (!rect.Contains(evt.mousePosition) || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)) return;
        UnityEngine.Object[] accepted = DragAndDrop.objectReferences.Where(o => o is AnimationClip || o is RuntimeAnimatorController).ToArray();
        if (accepted.Length == 0) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            var clips = new List<AnimationClip>();
            foreach (UnityEngine.Object item in accepted)
            {
                if (item is AnimationClip clip) clips.Add(clip);
                else if (item is RuntimeAnimatorController controller) clips.AddRange(DiNeAnimationRepairCore.CollectClips(controller));
            }
            AddRepairClips(clips);
        }
        evt.Use();
    }

    private void AnalyzeRepairClips()
    {
        try
        {
            repairGroups = DiNeAnimationRepairCore.Analyze(targetAvatarRoot, repairClips);
            foreach (DiNeAnimationRepairCore.BindingGroup group in repairGroups) GetRepairMapping(group);
            RefreshRepairPlan();
            SetRepairStatus(string.Format(Tr("Inspected {0} clips. Original clips and avatar references are preserved.",
                "클립 {0}개를 검사했습니다. 원본 클립과 아바타 참조는 유지됩니다.",
                "{0}個のクリップを検査しました。元のクリップとアバターの参照は保持されます。"), repairClips.Where(c => c).Distinct().Count()));
        }
        catch (Exception exception) { SetRepairStatus(Tr("Inspection failed: ", "검사 실패: ", "検査に失敗しました：") + exception.Message, true); }
    }

    private void DrawRepairMappings()
    {
        BeginCard(Tr("Connection replacements", "연결 대응", "接続の対応"));
        int problems = repairGroups.Count(g => g.Status != DiNeAnimationRepairCore.BindingStatus.Valid);
        EditorGUILayout.LabelField(string.Format(Tr("{0} connections · {1} require attention", "연결 {0}개 · 확인이 필요한 연결 {1}개", "接続 {0}件・確認が必要な接続 {1}件"), repairGroups.Count, problems), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(Tr("Choose replacements for the connections you want to repair. Candidate names are suggestions; no match is selected automatically.",
            "복구할 연결의 새 대상을 선택하세요. 후보 이름은 추천이며 자동으로 확정하지 않습니다.",
            "修復する接続の新しい対象を選択してください。候補名は提案であり、自動選択しません。"), EditorStyles.wordWrappedLabel);
        repairShowVerified = EditorGUILayout.ToggleLeft(Tr("Include valid connections", "정상 연결도 표시", "正常な接続も表示"), repairShowVerified);
        // Sharing only during this GUI pass avoids per-row hierarchy scans without stale editor state.
        var inspection = new DiNeAnimationRepairCore.InspectionContext(targetAvatarRoot);
        foreach (DiNeAnimationRepairCore.BindingGroup group in repairGroups)
        {
            DiNeAnimationRepairCore.BindingMapping mapping = GetRepairMapping(group);
            if (!repairShowVerified && group.Status == DiNeAnimationRepairCore.BindingStatus.Valid && !mapping.Enabled) continue;
            GUILayout.Space(5);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawRepairMapping(group, mapping, inspection);
        }
        if (repairGroups.Count == 0)
            EditorGUILayout.HelpBox(Tr("These clips contain no editable value or object-reference curves.", "이 클립에는 편집 가능한 값 또는 오브젝트 참조 커브가 없습니다.", "これらのクリップには編集可能な値・オブジェクト参照カーブがありません。"), MessageType.Info);
        else if (problems == 0 && !repairShowVerified && !repairMappings.Any(m => m.Enabled))
            EditorGUILayout.HelpBox(Tr("All inspectable connections are valid. Enable valid connections to move or rename them intentionally.",
                "검사 가능한 연결이 모두 정상입니다. 의도적으로 경로나 이름을 바꾸려면 정상 연결도 표시하세요.",
                "検査できる接続はすべて正常です。パスや名前を変更する場合は正常な接続も表示してください。"), MessageType.Info);
        EndCard();
        TutorialAnchor("repair-mapping");
        TutorialDraw("repair-mapping");
        DrawRepairPlan();
    }

    private void DrawRepairMapping(DiNeAnimationRepairCore.BindingGroup group, DiNeAnimationRepairCore.BindingMapping mapping,
        DiNeAnimationRepairCore.InspectionContext inspection)
    {
        string sourceName = RepairPathLabel(group.Binding.path) + "  /  " + RepairPropertyLabel(group.Binding.propertyName);
        EditorGUILayout.LabelField(new GUIContent(sourceName, (group.Binding.type?.Name ?? "") + "\n" + string.Join("\n", group.Clips.Select(c => c.name))), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(RepairStatusLabel(group.Status) + " · " + string.Format(Tr("{0} clips", "클립 {0}개", "{0}個のクリップ"), group.Clips.Count), EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        mapping.Enabled = EditorGUILayout.ToggleLeft(Tr("Use this replacement", "이 대응 적용", "この対応を適用"), mapping.Enabled);
        if (EditorGUI.EndChangeCheck()) RefreshRepairPlan();

        string path = mapping.TargetPath ?? "";
        Transform target = string.IsNullOrEmpty(path) ? targetAvatarRoot?.transform : targetAvatarRoot?.transform.Find(path);
        EditorCurveBinding destination = group.Binding;
        destination.path = path;
        destination.propertyName = mapping.TargetProperty ?? "";
        if (inspection.ValidateBinding(destination) == DiNeAnimationRepairCore.BindingStatus.AmbiguousPath) target = null;
        EditorGUI.BeginChangeCheck();
        GameObject chosen = (GameObject)EditorGUILayout.ObjectField(Tr("New object", "새 오브젝트", "新しいオブジェクト"), target ? target.gameObject : null, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            if (chosen && targetAvatarRoot && (chosen == targetAvatarRoot || chosen.transform.IsChildOf(targetAvatarRoot.transform)))
            {
                mapping.TargetPath = AnimationUtility.CalculateTransformPath(chosen.transform, targetAvatarRoot.transform);
                mapping.Enabled = true;
                RefreshRepairPlan();
            }
            else if (chosen)
                SetRepairStatus(Tr("Choose an object inside the selected avatar.", "선택한 아바타 안의 오브젝트를 지정하세요.", "選択したアバター内のオブジェクトを指定してください。"), true);
        }
        if (group.CandidatePaths.Count > 0)
        {
            string[] choices = new[] { Tr("Choose a candidate…", "후보 선택…", "候補を選択…") }.Concat(group.CandidatePaths.Select(RepairPathLabel)).ToArray();
            int choice = EditorGUILayout.Popup(Tr("Candidates", "추천 대상", "候補"), 0, choices);
            if (choice > 0) { mapping.TargetPath = group.CandidatePaths[choice - 1]; mapping.Enabled = true; RefreshRepairPlan(); }
        }
        EditorGUI.BeginChangeCheck();
        string editedPath = EditorGUILayout.TextField(new GUIContent(Tr("New path", "새 경로", "新しいパス"), Tr("An empty path targets the avatar root.", "빈 경로는 아바타 루트를 가리킵니다.", "空のパスはアバターのルートを対象にします。")), mapping.TargetPath ?? "");
        if (EditorGUI.EndChangeCheck()) { mapping.TargetPath = editedPath; mapping.Enabled = true; RefreshRepairPlan(); }

        destination.path = mapping.TargetPath ?? "";
        destination.propertyName = mapping.TargetProperty ?? group.Binding.propertyName;
        List<string> propertyChoices = inspection.GetPropertyChoices(destination);
        if (propertyChoices.Count > 0)
        {
            string[] choices = new[] { Tr("Choose a property…", "속성 선택…", "プロパティを選択…") }.Concat(propertyChoices.Select(RepairPropertyLabel)).ToArray();
            int choice = EditorGUILayout.Popup(Tr("Available properties", "사용 가능한 속성", "使用できるプロパティ"), 0, choices);
            if (choice > 0) { mapping.TargetProperty = propertyChoices[choice - 1]; mapping.Enabled = true; RefreshRepairPlan(); }
        }
        EditorGUI.BeginChangeCheck();
        string property = EditorGUILayout.TextField(Tr("New property", "새 속성", "新しいプロパティ"), mapping.TargetProperty ?? "");
        if (EditorGUI.EndChangeCheck()) { mapping.TargetProperty = property; mapping.Enabled = true; RefreshRepairPlan(); }
        if (mapping.Enabled)
        {
            destination.propertyName = mapping.TargetProperty ?? "";
            var status = inspection.ValidateBinding(destination);
            EditorGUILayout.LabelField(Tr("Replacement: ", "대응 결과: ", "対応結果：") + RepairStatusLabel(status), EditorStyles.miniLabel);
        }
    }

    private DiNeAnimationRepairCore.BindingMapping GetRepairMapping(DiNeAnimationRepairCore.BindingGroup group)
    {
        string typeName = group.Binding.type?.AssemblyQualifiedName ?? "";
        var mapping = repairMappings.FirstOrDefault(m => m != null && m.SourcePath == group.Binding.path && m.SourceProperty == group.Binding.propertyName
            && m.TypeName == typeName && m.IsObjectReference == group.IsObjectReference);
        if (mapping == null)
        {
            mapping = DiNeAnimationRepairCore.MappingFor(group.Binding, group.IsObjectReference, group.Binding.path, group.Binding.propertyName);
            mapping.Enabled = false;
            repairMappings.Add(mapping);
        }
        return mapping;
    }

    private void RefreshRepairPlan()
    {
        if (repairGroups != null) repairPlan = DiNeAnimationRepairCore.Plan(targetAvatarRoot, repairClips, repairMappings);
    }

    private void DrawRepairPlan()
    {
        if (repairPlan == null) return;
        BeginCard(Tr("Repair result", "복구 결과", "修復結果"));
        EditorGUILayout.LabelField(string.Format(Tr("{0} replacements in {1} clips · {2} connections remain unresolved", "클립 {1}개의 연결 {0}개 변경 · 미해결 연결 {2}개", "{1}個のクリップの接続{0}件を変更・未解決{2}件"), repairPlan.ChangedBindingCount, repairPlan.ChangedClipCount, repairPlan.Remaining.Count), EditorStyles.wordWrappedLabel);
        if (repairPlan.Collisions.Count > 0)
        {
            EditorGUILayout.HelpBox(Tr("Several curves would overwrite the same destination. Change their mappings before saving.",
                "여러 커브가 같은 대상으로 겹칩니다. 저장 전에 대응을 변경하세요.",
                "複数のカーブが同じ対象に重なります。保存前に対応を変更してください。"), MessageType.Error);
            foreach (var collision in repairPlan.Collisions.Take(8))
                EditorGUILayout.LabelField(collision.Clip.name + "  /  " + RepairPathLabel(collision.Destination.path) + "  /  " + RepairPropertyLabel(collision.Destination.propertyName), EditorStyles.wordWrappedMiniLabel);
        }
        if (repairPlan.InvalidMappings.Count > 0)
            EditorGUILayout.HelpBox(Tr("Some enabled replacements cannot be verified. Choose a valid target or turn those replacements off.",
                "적용 중인 대응 중 확인되지 않은 대상이 있습니다. 정상 대상을 선택하거나 해당 대응을 끄세요.",
                "適用中の対応に検証できない対象があります。正常な対象を選ぶか、その対応を無効にしてください。"), MessageType.Error);
        if (repairPlan.Remaining.Count > 0)
            EditorGUILayout.HelpBox(Tr("Unresolved and unverified curves are kept unchanged. Humanoid and custom properties may need playback verification.",
                "미해결·검증 불확실 커브는 그대로 유지됩니다. Humanoid와 사용자 정의 속성은 재생 확인이 필요할 수 있습니다.",
                "未解決・未検証のカーブはそのまま保持します。Humanoidやカスタムプロパティは再生確認が必要な場合があります。"), MessageType.Info);
        using (new EditorGUI.DisabledScope(!repairPlan.CanSave || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            foreach (AnimationClip source in repairPlan.ChangedClips.Where(c => c))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(source.name, GUILayout.MinWidth(60));
                    if (GUILayout.Button(Tr("Original", "원본", "元のクリップ"), GUILayout.Height(24))) PreviewClip(source);
                    if (GUILayout.Button(Tr("Repaired preview", "복구본 미리보기", "修復後のプレビュー"), GUILayout.Height(24)))
                    {
                        try { PreviewTransientClip(DiNeAnimationRepairCore.CreateRepairedClip(source, targetAvatarRoot, repairMappings)); }
                        catch (Exception exception) { SetRepairStatus(Tr("Could not preview the repaired copy: ", "복구본 미리보기 실패: ", "修復後のコピーをプレビューできませんでした：") + exception.Message, true); }
                    }
                }
        }
        using (new EditorGUI.DisabledScope(!repairPlan.CanSave || EditorApplication.isPlayingOrWillChangePlaymode))
            if (PrimaryButton(Tr("Save repaired copies…", "복구본 저장…", "修復したコピーを保存…"))) SaveRepairedClips();
        TutorialAnchor("repair-save");
        TutorialDraw("repair-save");
        EndCard();
    }

    private void SaveRepairedClips()
    {
        string selectedFolder = EditorUtility.OpenFolderPanel(Tr("Choose output folder inside Assets", "Assets 안의 저장 폴더 선택", "Assets内の保存先フォルダーを選択"), Application.dataPath, "");
        if (string.IsNullOrEmpty(selectedFolder)) return;
        string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string absoluteFolder = Path.GetFullPath(selectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!absoluteFolder.Equals(assetsRoot, StringComparison.OrdinalIgnoreCase) && !absoluteFolder.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            SetRepairStatus(Tr("Choose an existing folder inside this project's Assets folder.", "현재 프로젝트의 Assets 안에 있는 폴더를 선택하세요.", "このプロジェクトのAssets内にあるフォルダーを選択してください。"), true);
            return;
        }
        string parent = "Assets" + absoluteFolder.Substring(assetsRoot.Length).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        { SetRepairStatus(Tr("The output folder is not available to Unity yet. Refresh the project and try again.", "Unity에서 저장 폴더를 찾지 못했습니다. 프로젝트를 새로고침한 뒤 다시 시도하세요.", "Unityで保存先フォルダーが見つかりません。プロジェクトを更新して再試行してください。"), true); return; }
        SaveRepairedClipsToFolder(parent);
    }

    // Kept separate from the folder picker so the complete save workflow can be verified in an isolated editor.
    private void SaveRepairedClipsToFolder(string parent)
    {
        if (string.IsNullOrEmpty(parent) || (parent != "Assets" && !parent.StartsWith("Assets/", StringComparison.Ordinal))
            || parent.Split('/').Any(segment => segment == ".." || segment == ".") || !AssetDatabase.IsValidFolder(parent))
        {
            SetRepairStatus(Tr("Choose an existing folder inside this project's Assets folder.", "현재 프로젝트의 Assets 안에 있는 폴더를 선택하세요.", "このプロジェクトのAssets内にあるフォルダーを選択してください。"), true);
            return;
        }
        var prepared = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        repairSavedClips.Clear();
        try
        {
            // Re-read the current source clips and hierarchy immediately before copying.
            repairPlan = DiNeAnimationRepairCore.Plan(targetAvatarRoot, repairClips, repairMappings);
            if (!repairPlan.CanSave) throw new InvalidOperationException(Tr("Reinspect the connections and resolve invalid replacements before saving.", "연결을 다시 검사하고 잘못된 대응을 해결한 뒤 저장하세요.", "接続を再検査し、不正な対応を解決してから保存してください。"));
            foreach (AnimationClip source in repairClips.Where(c => c).Distinct())
            {
                var clipPlan = DiNeAnimationRepairCore.Plan(targetAvatarRoot, new[] { source }, repairMappings);
                if (clipPlan.ChangedBindingCount > 0) prepared.Add(new KeyValuePair<AnimationClip, AnimationClip>(source, DiNeAnimationRepairCore.CreateRepairedClip(source, targetAvatarRoot, repairMappings)));
            }
            string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/AnimationRepair_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string folderGuid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            if (string.IsNullOrEmpty(folderGuid)) throw new IOException(Tr("Could not create the output folder.", "저장 폴더를 만들지 못했습니다.", "保存先フォルダーを作成できませんでした。"));
            repairOutputFolder = folder;
            var report = new RepairSaveReport();
            foreach (var pair in prepared)
            {
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeRepairFilename(pair.Key.name) + "_Repaired.anim");
                AssetDatabase.CreateAsset(pair.Value, path);
                AssetDatabase.SaveAssetIfDirty(pair.Value);
                if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(path)) throw new IOException(Tr("A saved clip could not be read back: ", "저장한 클립을 다시 읽지 못했습니다: ", "保存したクリップを読み戻せませんでした：") + path);
                repairSavedClips.Add(pair.Value);
                string sourcePath = AssetDatabase.GetAssetPath(pair.Key);
                report.Clips.Add(new RepairSavedClip { SourceAssetPath = sourcePath, SourceGuid = AssetDatabase.AssetPathToGUID(sourcePath),
                    SourceClipName = pair.Key.name, RepairedAssetPath = path });
            }
            string mappingPath = folder + "/MappingTable.json";
            string physicalPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", mappingPath));
            File.WriteAllText(physicalPath, DiNeAnimationRepairCore.ToProfileJson(repairMappings.Where(m => m != null && m.Enabled)), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(mappingPath);
            var remaining = DiNeAnimationRepairCore.Analyze(targetAvatarRoot, repairSavedClips);
            int unresolved = remaining.Count(g => g.Status != DiNeAnimationRepairCore.BindingStatus.Valid);
            foreach (var group in remaining.Where(g => g.Status != DiNeAnimationRepairCore.BindingStatus.Valid))
                report.RemainingConnections.Add(new RepairRemainingConnection { Path = group.Binding.path, Property = group.Binding.propertyName,
                    Component = group.Binding.type?.FullName ?? "", Status = group.Status.ToString(), Clips = group.Clips.Select(c => AssetDatabase.GetAssetPath(c)).ToArray() });
            string reportPath = folder + "/RepairReport.json";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", reportPath)), JsonUtility.ToJson(report, true), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(reportPath);
            SetRepairStatus(string.Format(Tr("Saved {0} repaired clips. {1} unresolved or unverified connections remain. Originals and avatar references were preserved.",
                "복구본 {0}개를 저장했습니다. 미해결·검증 불확실 연결은 {1}개입니다. 원본과 아바타 참조는 유지했습니다.",
                "修復したクリップ{0}個を保存しました。未解決・未検証の接続は{1}件です。元のクリップとアバターの参照は保持されました。"), repairSavedClips.Count, unresolved));
        }
        catch (Exception exception)
        {
            SetRepairStatus(Tr("Saving stopped: ", "저장이 중단되었습니다: ", "保存が中断しました：") + exception.Message
                + (repairSavedClips.Count > 0 ? "\n" + Tr("Completed copies remain in the output folder.", "완료된 복구본은 출력 폴더에 남아 있습니다.", "完成したコピーは出力フォルダーに残っています。") : ""), true);
        }
        finally
        {
            foreach (var pair in prepared) if (pair.Value && !AssetDatabase.Contains(pair.Value)) DestroyImmediate(pair.Value);
        }
    }

    private void DrawRepairOutputs()
    {
        BeginCard(Tr("Saved copies", "저장된 복구본", "保存したコピー"));
        foreach (AnimationClip clip in repairSavedClips.Where(c => c))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);
                if (GUILayout.Button(Tr("Preview", "미리보기", "プレビュー"), GUILayout.Width(75), GUILayout.Height(24))) PreviewClip(clip);
            }
        if (GUILayout.Button(Tr("Show output folder", "저장 폴더 보기", "保存先フォルダーを表示"), GUILayout.Height(24)))
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(repairOutputFolder ?? "");
            if (folder) EditorGUIUtility.PingObject(folder);
        }
        EndCard();
    }

    private void SaveRepairProfile()
    {
        string path = EditorUtility.SaveFilePanel(Tr("Save mapping table", "대응표 저장", "対応表を保存"), Application.dataPath, "AnimationMapping", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, DiNeAnimationRepairCore.ToProfileJson(repairMappings.Where(m => m != null && m.Enabled)), new System.Text.UTF8Encoding(false));
            SetRepairStatus(Tr("Mapping table saved.", "대응표를 저장했습니다.", "対応表を保存しました。"));
        }
        catch (Exception exception) { SetRepairStatus(Tr("Could not save the mapping table: ", "대응표 저장 실패: ", "対応表を保存できませんでした：") + exception.Message, true); }
    }

    private void LoadRepairProfile()
    {
        string path = EditorUtility.OpenFilePanel(Tr("Load mapping table", "대응표 불러오기", "対応表を読み込む"), Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            repairMappings = DiNeAnimationRepairCore.FromProfileJson(File.ReadAllText(path));
            if (repairGroups != null) foreach (var group in repairGroups) GetRepairMapping(group);
            RefreshRepairPlan();
            SetRepairStatus(Tr("Mapping table loaded. Destinations are validated against the selected avatar before saving.",
                "대응표를 불러왔습니다. 저장 전에 선택한 아바타에서 대상이 검증됩니다.",
                "対応表を読み込みました。保存前に選択したアバターで対象を検証します。"));
        }
        catch (Exception exception) { SetRepairStatus(Tr("Could not load the mapping table: ", "대응표 불러오기 실패: ", "対応表を読み込めませんでした：") + exception.Message, true); }
    }

    private string RepairPathLabel(string path) => string.IsNullOrEmpty(path) ? Tr("Avatar root", "아바타 루트", "アバタールート") : path;

    private string RepairPropertyLabel(string property)
    {
        if (property != null && property.StartsWith("blendShape.", StringComparison.Ordinal)) return property.Substring("blendShape.".Length);
        const string materialPrefix = "m_Materials.Array.data[";
        if (property != null && property.StartsWith(materialPrefix, StringComparison.Ordinal) && property.EndsWith("]", StringComparison.Ordinal))
            return Tr("Material slot ", "머티리얼 슬롯 ", "マテリアルスロット ") + property.Substring(materialPrefix.Length, property.Length - materialPrefix.Length - 1);
        if (property == "m_IsActive") return Tr("Active state", "활성 상태", "アクティブ状態");
        return property ?? "";
    }

    private string RepairStatusLabel(DiNeAnimationRepairCore.BindingStatus status)
    {
        switch (status)
        {
            case DiNeAnimationRepairCore.BindingStatus.Valid: return Tr("Valid", "정상", "正常");
            case DiNeAnimationRepairCore.BindingStatus.MissingPath: return Tr("Object path missing", "오브젝트 경로 없음", "オブジェクトのパスなし");
            case DiNeAnimationRepairCore.BindingStatus.AmbiguousPath: return Tr("Duplicate object path", "같은 경로의 오브젝트가 여러 개", "同じパスのオブジェクトが複数");
            case DiNeAnimationRepairCore.BindingStatus.MissingComponent: return Tr("Component missing", "대상 컴포넌트 없음", "対象コンポーネントなし");
            case DiNeAnimationRepairCore.BindingStatus.MissingBlendShape: return Tr("Blend shape missing", "쉐이프키 없음", "ブレンドシェイプなし");
            case DiNeAnimationRepairCore.BindingStatus.MissingMaterialSlot: return Tr("Material slot missing", "머티리얼 슬롯 없음", "マテリアルスロットなし");
            case DiNeAnimationRepairCore.BindingStatus.UnverifiedHumanoid: return Tr("Humanoid / Animator: playback check required", "Humanoid / Animator: 재생 확인 필요", "Humanoid / Animator：再生確認が必要");
            case DiNeAnimationRepairCore.BindingStatus.IncompatibleCurveType: return Tr("Curve and property types do not match", "커브와 속성의 종류가 다름", "カーブとプロパティの種類が不一致");
            default: return Tr("Property could not be verified", "속성 검증 불확실", "プロパティを検証できません");
        }
    }

    private void SetRepairStatus(string text, bool error = false) { repairStatus = text; repairStatusError = error; }

    private static string SafeRepairFilename(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "Animation" : name.Trim();
    }
}
