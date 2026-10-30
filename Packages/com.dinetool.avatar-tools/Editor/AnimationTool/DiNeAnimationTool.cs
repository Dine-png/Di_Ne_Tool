using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class DiNeAnimationTool : EditorWindow
{
    private static Color Mint => DiNeEditorUI.Mint;
    [SerializeField] private GameObject targetAvatarRoot;
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private float clipTime;
    [SerializeField] private int selectedTab;
    [SerializeField] private Vector2 scrollPosition;
    private GameObject previousAvatar;
    private Texture2D tabIcon;
    private AnimationClip transientPreviewClip;
    private string status = "";
    private bool statusIsError;
    private readonly Dictionary<Transform, PoseSnapshot> originalTransforms = new Dictionary<Transform, PoseSnapshot>();
    private readonly Dictionary<SkinnedMeshRenderer, float[]> originalShapes = new Dictionary<SkinnedMeshRenderer, float[]>();
    private bool hasSnapshot;
    private DiNeGuidedTutorial _tutorial;
    private Dictionary<string, DiNeTutorialStep[]> tutorialCourses;
    private struct PoseSnapshot { public Vector3 Position, Scale; public Quaternion Rotation; }

    private int LanguageIndex => Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
    private string Tr(string en, string ko, string ja) => LanguageIndex == 1 ? ko : LanguageIndex == 2 ? ja : en;

    [MenuItem("DiNe/Animation Tool", false, 2)]
    private static void OpenFromMenu() => ShowWindow();

    public static DiNeAnimationTool ShowWindow(GameObject avatar = null)
    {
        DiNeAnimationTool window = GetWindow<DiNeAnimationTool>();
        window.minSize = new Vector2(420f, 520f);
        if (avatar != null)
        {
            window.targetAvatarRoot = avatar;
            window.RefreshAvatar();
        }
        window.Show();
        return window;
    }

    private void OnEnable()
    {
        tabIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
        titleContent = new GUIContent("Animation Tool", tabIcon);
        minSize = new Vector2(420f, 520f);
        RefreshAvatar();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.projectChanged += OnProjectChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseSessionPreview;
    }

    private void OnDisable()
    {
        _tutorial?.Suspend();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.projectChanged -= OnProjectChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSessionPreview;
        ReleaseSessionPreview();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        ReleaseSessionPreview();
        Repaint();
    }

    private void OnProjectChanged() { ResetRepairAnalysis(); Repaint(); }
    private void OnUndoRedo() { EndFxPreview(); ResetRepairAnalysis(); Repaint(); }

    private void OnGUI()
    {
        DrawHeader();
        GUILayout.Space(5f);
        int previousLanguage = LanguageIndex;
        int lang = DiNeEditorUI.DrawLanguageToolbar(previousLanguage);
        if (lang != previousLanguage) { status = ""; repairStatus = null; Repaint(); }
        GUILayout.Space(15f);
        int tab = DrawToolbar(selectedTab, new[] { Tr("Pose", "포즈", "ポーズ"),
            Tr("Expressions / FX", "표정·제스처", "表情・ジェスチャー"),
            Tr("Repair", "연결 복구", "接続修復") }, 35);
        selectedTab = tab;
        GUILayout.Space(DiNeEditorUI.CardSpacing);
        BeginTutorialFrame();
        try
        {
            _tutorial.DrawControls();
            GUILayout.Space(DiNeEditorUI.CardSpacing);
            DrawAvatarCard();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(Tr("Exit Play Mode to edit and preview animations.",
                    "애니메이션 편집과 미리보기를 하려면 플레이 모드를 종료하세요.",
                    "アニメーションの編集とプレビューには再生モードを終了してください。"), MessageType.Info);
                return;
            }
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            _tutorial.BeginScrollScope();
            if (selectedTab == 0) DrawPreviewGUI();
            else if (selectedTab == 1) DrawExpressionGUI();
            else DrawRepairGUI();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusIsError ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.EndScrollView();
            _tutorial.EndScrollScope(GUILayoutUtility.GetLastRect());
        }
        catch (ExitGUIException) { _tutorial.AbortFrame(); throw; }
        finally { _tutorial.EndFrame(); }
    }

    private void DrawHeader()
    {
        DiNeEditorUI.DrawHeader("Animation Tool", Tr(
            "Apply poses, create expressions and repair animation links.",
            "포즈를 적용하고 표정을 만들며 애니메이션 연결을 복구합니다.",
            "ポーズの適用、表情の作成、アニメーション接続の修復。"));
    }

    private int DrawToolbar(int selected, string[] labels, int height)
    {
        return DiNeEditorUI.DrawToolbar(selected, labels, height);
    }

    private void BeginCard(string title)
    {
        EditorGUILayout.BeginVertical(DiNeEditorUI.CardStyle);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        GUILayout.Space(3f);
    }
    private void EndCard() { EditorGUILayout.EndVertical(); GUILayout.Space(DiNeEditorUI.CardSpacing); }
    private bool PrimaryButton(string label, int height = 30)
    {
        return DiNeEditorUI.Button(label, height);
    }
    private void SetStatus(string message, bool error = false) { status = message; statusIsError = error; Repaint(); }

    private void DrawAvatarCard()
    {
        BeginCard(Tr("Avatar", "아바타", "アバター"));
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(new GUIContent(Tr("Avatar Root", "아바타 루트", "アバタールート"),
            Tr("Animation paths are relative to this object.", "애니메이션 경로는 이 오브젝트를 기준으로 해석됩니다.", "このオブジェクトを基準にアニメーションパスを解釈します。")),
            targetAvatarRoot, typeof(GameObject), true);
        TutorialAnchor("avatar");
        if (EditorGUI.EndChangeCheck() || previousAvatar != targetAvatarRoot) RefreshAvatar();
        using (new EditorGUI.DisabledScope(targetAvatarRoot == null))
            if (GUILayout.Button(new GUIContent("↺", Tr("Refresh avatar and capture its current pose.", "아바타를 새로고침하고 현재 포즈를 기록합니다.", "アバターを更新し現在のポーズを記録します。")), GUILayout.Width(28), GUILayout.Height(DiNeEditorUI.CompactButtonHeight))) RefreshAvatar();
        EditorGUILayout.EndHorizontal();
        TutorialDraw("avatar");
        if (targetAvatarRoot == null) EditorGUILayout.HelpBox(Tr("Assign an avatar to preview and edit animations.", "아바타를 지정해 애니메이션을 미리 보고 편집하세요.", "アバターを指定してアニメーションをプレビュー・編集します。"), MessageType.Info);
        EndCard();
    }

    private void RefreshAvatar()
    {
        previousAvatar = targetAvatarRoot;
        ReleaseSessionPreview();
        TakeSnapshot();
        RefreshBodySmr();
        ResetRepairAnalysis();
        status = "";
        Repaint();
    }

    private void PreviewClip(AnimationClip clip)
    {
        if (animationClip != clip) ReleaseTransientPreviewClip();
        animationClip = clip;
        clipTime = 0f;
        selectedTab = 0;
        Repaint();
    }

    // The caller transfers ownership of a nonpersistent repaired clip to this window.
    private void PreviewTransientClip(AnimationClip ownedClip)
    {
        if (ownedClip == null || AssetDatabase.Contains(ownedClip))
            throw new ArgumentException("A transient preview requires a nonpersistent clip.", nameof(ownedClip));
        if (ownedClip == transientPreviewClip) { PreviewClip(ownedClip); return; }
        ReleaseTransientPreviewClip();
        PreviewClip(ownedClip);
        transientPreviewClip = ownedClip;
        ownedClip.hideFlags = HideFlags.HideAndDontSave;
    }

    private void DrawPreviewGUI()
    {
        BeginCard(Tr("Animation", "애니메이션", "アニメーション"));
        EditorGUI.BeginChangeCheck();
        AnimationClip newClip = (AnimationClip)EditorGUILayout.ObjectField(Tr("Animation Clip", "애니메이션 클립", "アニメーションクリップ"), animationClip, typeof(AnimationClip), false);
        TutorialAnchor("animation-clip");
        if (EditorGUI.EndChangeCheck())
        {
            if (animationClip != newClip) ReleaseTransientPreviewClip();
            animationClip = newClip; clipTime = 0f;
        }
        TutorialDraw("animation-clip");
        using (new EditorGUI.DisabledScope(targetAvatarRoot == null || animationClip == null))
        {
            clipTime = EditorGUILayout.Slider(clipTime, 0f, animationClip != null ? animationClip.length : 1f);
            TutorialAnchor("animation-time");
            TutorialDraw("animation-time");
            EditorGUILayout.LabelField(animationClip != null ? $"{clipTime:F3}s / {animationClip.length:F3}s" : "", EditorStyles.centeredGreyMiniLabel);
        }
        EndCard();
        BeginCard(Tr("Apply to Scene", "씬에 적용", "シーンに適用"));
        using (new EditorGUI.DisabledScope(!CanApply() || animationClip == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Tr("Shape Keys", "쉐이프키", "シェイプキー"), GUILayout.Height(DiNeEditorUI.ButtonHeight))) ApplyShapeKeys();
            if (GUILayout.Button(Tr("Pose", "포즈", "ポーズ"), GUILayout.Height(DiNeEditorUI.ButtonHeight))) ApplyPose();
            if (PrimaryButton(Tr("Apply Both", "모두 적용", "両方を適用"))) ApplyBoth();
            EditorGUILayout.EndHorizontal();
            TutorialAnchor("animation-apply");
            TutorialDraw("animation-apply");
        }
        using (new EditorGUI.DisabledScope(!CanApply() || !hasSnapshot))
            if (GUILayout.Button(Tr("Restore Captured Pose and Shape Keys", "기록한 포즈·쉐이프키로 복원", "記録したポーズ・シェイプキーに復元"), GUILayout.Height(DiNeEditorUI.ButtonHeight))) RestoreToOriginal();
        TutorialAnchor("animation-restore");
        TutorialDraw("animation-restore");
        if (targetAvatarRoot != null && EditorUtility.IsPersistent(targetAvatarRoot))
            EditorGUILayout.HelpBox(Tr("To apply a pose, select an avatar instance in a scene.", "포즈를 적용하려면 씬의 아바타 인스턴스를 선택하세요.", "ポーズを適用するにはシーン内のアバターを選択してください。"), MessageType.Info);
        EndCard();
    }

    private bool CanApply() => targetAvatarRoot != null && !EditorUtility.IsPersistent(targetAvatarRoot) && targetAvatarRoot.scene.IsValid()
        && !EditorSceneManager.IsPreviewScene(targetAvatarRoot.scene) && !EditorApplication.isPlayingOrWillChangePlaymode;

    private void ReleaseTransientPreviewClip()
    {
        if (transientPreviewClip == null) return;
        if (animationClip == transientPreviewClip) animationClip = null;
        DestroyImmediate(transientPreviewClip);
        transientPreviewClip = null;
    }

    private void ReleaseSessionPreview()
    {
        ReleaseTransientPreviewClip();
    }

    private void TakeSnapshot()
    {
        originalShapes.Clear(); originalTransforms.Clear(); hasSnapshot = targetAvatarRoot != null;
        if (!hasSnapshot) return;
        foreach (Transform transform in targetAvatarRoot.GetComponentsInChildren<Transform>(true))
            originalTransforms.Add(transform, new PoseSnapshot { Position = transform.localPosition, Rotation = transform.localRotation, Scale = transform.localScale });
        foreach (SkinnedMeshRenderer renderer in targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            float[] weights = new float[renderer.sharedMesh.blendShapeCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = renderer.GetBlendShapeWeight(i);
            originalShapes.Add(renderer, weights);
        }
    }

    private void ApplyShapeKeys() { ApplySample(false, true); }
    private void ApplyPose() { ApplySample(true, false); }
    private void ApplyBoth() { ApplySample(true, true); }
    private void ApplySample(bool pose, bool shapes)
    {
        if (!CanApply() || animationClip == null) return;
        // Sample first; failed sampling cannot leave a partly edited source avatar.
        using (var sample = new DiNeAnimationPreview())
        {
            sample.Sample(targetAvatarRoot, animationClip, clipTime);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Animation Tool Apply Pose / Shape Keys");
            if (pose)
                foreach (Transform source in targetAvatarRoot.GetComponentsInChildren<Transform>(true))
                {
                    Transform copy = sample.GetTransform(source);
                    if (copy == null) continue;
                    Undo.RecordObject(source, "Animation Tool Apply Pose");
                    source.localPosition = copy.localPosition; source.localRotation = copy.localRotation; source.localScale = copy.localScale;
                    MarkChanged(source);
                }
            if (shapes)
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(animationClip))
                {
                    if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;
                    Transform transform = string.IsNullOrEmpty(binding.path) ? targetAvatarRoot.transform : targetAvatarRoot.transform.Find(binding.path);
                    SkinnedMeshRenderer renderer = transform != null ? transform.GetComponent<SkinnedMeshRenderer>() : null;
                    if (renderer == null || renderer.sharedMesh == null) continue;
                    int index = renderer.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11));
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                    if (index < 0 || curve == null) continue;
                    Undo.RecordObject(renderer, "Animation Tool Apply Shape Keys");
                    renderer.SetBlendShapeWeight(index, curve.Evaluate(clipTime));
                    MarkChanged(renderer);
                }
            Undo.CollapseUndoOperations(group);
        }
        SceneView.RepaintAll();
        SetStatus(Tr("Applied to the scene avatar. Undo is available.", "씬 아바타에 적용했습니다. 실행 취소로 되돌릴 수 있습니다.", "シーンのアバターに適用しました。元に戻す操作が利用できます。"));
    }

    private void RestoreToOriginal()
    {
        if (!CanApply() || !hasSnapshot) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Animation Tool Restore Captured State");
        foreach (var entry in originalTransforms)
        {
            if (entry.Key == null || !entry.Key.IsChildOf(targetAvatarRoot.transform)) continue;
            Undo.RecordObject(entry.Key, "Animation Tool Restore Pose");
            entry.Key.localPosition = entry.Value.Position; entry.Key.localRotation = entry.Value.Rotation; entry.Key.localScale = entry.Value.Scale;
            MarkChanged(entry.Key);
        }
        foreach (var entry in originalShapes)
        {
            if (entry.Key == null || entry.Key.sharedMesh == null || !entry.Key.transform.IsChildOf(targetAvatarRoot.transform)) continue;
            Undo.RecordObject(entry.Key, "Animation Tool Restore Shape Keys");
            for (int i = 0; i < Mathf.Min(entry.Value.Length, entry.Key.sharedMesh.blendShapeCount); i++) entry.Key.SetBlendShapeWeight(i, entry.Value[i]);
            MarkChanged(entry.Key);
        }
        Undo.CollapseUndoOperations(group);
        SceneView.RepaintAll();
        SetStatus(Tr("Restored the captured pose and shape weights.", "기록한 포즈와 쉐이프키 값을 복원했습니다.", "記録したポーズとシェイプキー値を復元しました。"));
    }

    private static void MarkChanged(Component component)
    {
        EditorUtility.SetDirty(component);
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        if (component.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(component.gameObject.scene))
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private void TutorialAnchor(string id) => _tutorial?.Anchor(id, GUILayoutUtility.GetLastRect());
    private void TutorialDraw(params string[] ids)
    {
        if (_tutorial != null) foreach (string id in ids) _tutorial.Draw(id);
    }
    private void BeginTutorialFrame()
    {
        if (_tutorial == null) _tutorial = new DiNeGuidedTutorial(this, "animation-tool");
        if (tutorialCourses == null)
        {
            DiNeTutorialStep avatar = DiNeTutorialStep.Required("avatar", "The avatar root is the starting point for finding the bones and meshes named in a clip. Assign the avatar you want to pose, edit expressions for, or repair clips for.", "아바타 루트는 클립에 기록된 본과 메시를 찾는 기준입니다. 포즈를 적용하거나 표정을 만들거나 클립 연결을 복구할 아바타를 지정하세요.", "アバタールートを基準に、クリップに記録されたボーンやメッシュを探します。ポーズの適用、表情の作成、クリップの接続修復を行うアバターを指定してください。", () => targetAvatarRoot != null);
            tutorialCourses = new Dictionary<string, DiNeTutorialStep[]>
            {
                ["preview"] = new[] { avatar,
                    DiNeTutorialStep.Required("animation-clip", "A clip supplies the pose and shape weights to apply. Assign the .anim file whose pose or expression you want to bring onto this avatar.", "클립에 담긴 포즈와 쉐이프키 값을 가져옵니다. 이 아바타에 적용할 포즈나 표정이 담긴 .anim 파일을 지정하세요.", "クリップのポーズとシェイプキー値を取り出します。このアバターに適用したいポーズや表情の.animファイルを指定してください。", () => animationClip != null),
                    DiNeTutorialStep.Optional("animation-time", "Choose the time of the frame to apply. Scrubbing never edits the scene avatar.", "적용할 프레임의 시간을 선택하세요. 시간을 조절해도 씬 아바타는 바뀌지 않습니다.", "適用するフレームの時間を選びます。時間を動かしてもシーンのアバターは変わりません。"),
                    DiNeTutorialStep.Optional("animation-apply", "Apply shape weights, pose transforms, or both. Only this explicit action edits the scene and supports Undo.", "쉐이프키, 포즈 또는 둘 다 적용하세요. 이 적용 동작만 씬을 편집하며 실행 취소를 지원합니다.", "シェイプキー、ポーズ、または両方を適用します。この操作だけがシーンを編集し、元に戻す操作に対応します。"),
                    DiNeTutorialStep.Optional("animation-restore", "Restore the state captured when the avatar was assigned or refreshed.", "아바타 지정 또는 새로고침 시 기록한 상태로 복원할 수 있습니다.", "アバター指定・更新時に記録した状態へ復元できます。") },
                ["expression"] = new[] { avatar,
                    DiNeTutorialStep.Required("expression-mesh", "Expressions are built by combining a mesh's shape keys, such as eye and mouth shapes. Choose the face mesh whose expression you want to edit.", "눈과 입 모양 같은 메시의 쉐이프키를 조합해 표정을 만듭니다. 표정을 편집할 얼굴 메시를 선택하세요.", "目や口の形など、メッシュのシェイプキーを組み合わせて表情を作ります。表情を編集する顔のメッシュを選択してください。", () => _bodySmr != null && _bodySmr.sharedMesh != null && _bodySmr.sharedMesh.blendShapeCount > 0),
                    DiNeTutorialStep.Optional("expression-clip", "Load an existing clip's shape weights, or create a new expression.", "기존 클립의 쉐이프키 값을 불러오거나 새 표정을 만드세요.", "既存クリップのキー値を読み込むか、新しい表情を作成します。"),
                    DiNeTutorialStep.Optional("expression-fx", "Load gesture clips into the sliders, then return to your working expression. Replace writes a new expression clip into the chosen FX slot.", "제스처 클립의 값을 슬라이더로 불러왔다가 작업 중인 표정으로 돌아올 수 있습니다. 교체는 새 표정 클립을 선택한 FX 슬롯에 연결합니다.", "ジェスチャーの値をスライダーに読み込み、編集中の表情へ戻れます。差し替えは新規表情クリップを選択したFXスロットへ接続します。"),
                    DiNeTutorialStep.Optional("expression-shapes", "Combine eye, mouth and other shape weights to make an expression, such as a smile. Adjust the sliders, apply them to the scene to check the face, and save when the expression is ready.", "눈·입 등의 쉐이프키 값을 조합해 웃는 얼굴 같은 표정을 만듭니다. 슬라이더를 조절하고 씬에 적용해 얼굴을 확인한 뒤 원하는 표정이 되면 저장하세요.", "目や口などのシェイプキー値を組み合わせて、笑顔などの表情を作ります。スライダーを調整し、シーンに適用して顔を確認し、表情ができたら保存してください。"),
                    DiNeTutorialStep.Optional("expression-save", "Save a new .anim, or overwrite an editable clip. Zero gesture keys prevent overlapping expressions.", "새 .anim으로 저장하거나 편집 가능한 클립을 덮어쓰세요. 제스처의 0값 키는 표정 겹침을 방지합니다.", "新しい.animとして保存するか編集可能なクリップを上書きします。ジェスチャーの0値キーは表情の重なりを防ぎます。") },
                ["repair"] = new[] { avatar,
                    DiNeTutorialStep.Optional("repair-input", "Use this when a clip stops working after moving an object or using it on another avatar. Add individual clips, a controller, or the avatar's FX clips to inspect their recorded connections together.", "오브젝트를 옮기거나 다른 아바타의 클립을 가져온 뒤 애니메이션이 작동하지 않을 때 사용합니다. 개별 클립·컨트롤러·아바타 FX 클립을 추가해 기록된 연결을 함께 검사하세요.", "オブジェクトの移動や別のアバターのクリップの利用で、アニメーションが動かなくなったときに使います。個別のクリップ、コントローラー、アバターのFXクリップを追加し、記録された接続をまとめて調べてください。"),
                    DiNeTutorialStep.Optional("repair-inspect", "Inspect connections to find missing paths, components, shape keys and material slots.", "연결을 검사해 없는 경로·컴포넌트·쉐이프키·머티리얼 슬롯을 찾으세요.", "接続を検査して欠けたパス、コンポーネント、キー、マテリアルスロットを見つけます。"),
                    DiNeTutorialStep.Optional("repair-mapping", "Choose replacement objects and properties. Reused connections are repaired together, and conflicting destinations must be resolved.", "새 오브젝트와 속성을 지정하세요. 여러 클립의 같은 연결은 함께 복구하며 대상 충돌은 해결해야 합니다.", "新しいオブジェクトとプロパティを指定します。同じ接続はまとめて修復し、対象の競合は解決する必要があります。"),
                    DiNeTutorialStep.Optional("repair-save", "Save repaired copies and the reusable mapping table, then preview the saved result.", "복구본과 재사용할 대응표를 저장한 뒤 저장된 결과를 미리 보세요.", "修復したコピーと再利用できる対応表を保存し、保存結果をプレビューします。") }
            };
        }
        string course = selectedTab == 0 ? "preview" : selectedTab == 1 ? "expression" : "repair";
        string[] names = selectedTab == 0 ? new[] { "Pose", "포즈", "ポーズ" }
            : selectedTab == 1 ? new[] { "Expressions / FX", "표정·제스처", "表情・ジェスチャー" }
            : new[] { "Repair", "연결 복구", "接続修復" };
        string[] overview = selectedTab == 0 ? new[] {
            "Animation Tool takes the pose from a chosen frame of a clip. Use it to set up a pose for a picture. You can apply the sampled pose, shape weights, or both to the scene avatar and restore its captured state.",
            "Animation Tool은 클립에서 원하는 프레임의 포즈를 가져오는 툴입니다. 촬영용 포즈를 잡을 때 사용합니다. 선택한 포즈·쉐이프키 또는 둘 다 씬 아바타에 적용하고, 기록해 둔 상태로 복원할 수 있습니다.",
            "Animation Toolは、クリップの好きなフレームのポーズを取り出すツールです。撮影用のポーズ作りに使えます。選択したポーズ、シェイプキー、または両方をシーンのアバターに適用し、記録した状態へ復元できます。" }
            : selectedTab == 1 ? new[] {
                "Create avatar expressions by combining face shape keys. You can start from an existing expression, load FX gesture clips, and save the result as a .anim file. Use Replace to connect a new expression clip to a chosen FX gesture slot.",
                "얼굴 쉐이프키를 조합해 아바타 표정을 만드는 기능입니다. 기존 표정이나 FX 제스처 표정을 불러와 수정하고, 완성한 표정을 .anim 파일로 저장할 수 있습니다. 교체 기능을 쓰면 새 표정 클립을 선택한 FX 제스처 슬롯에 연결합니다.",
                "顔のシェイプキーを組み合わせてアバターの表情を作る機能です。既存の表情やFXジェスチャーの表情を読み込んで編集し、完成した表情を.animファイルに保存できます。差し替えでは、新しい表情クリップを選択したFXジェスチャースロットへ接続します。" }
            : new[] {
                "Repair animation connections that no longer match the avatar after an object was moved or renamed. Inspect multiple clips for missing objects, components, shape keys and material slots, then map them to valid replacements. Save repaired clip copies and a reusable mapping table to check and reuse the result.",
                "오브젝트를 옮기거나 이름을 바꾼 뒤 아바타와 맞지 않게 된 애니메이션 연결을 복구하는 기능입니다. 여러 클립에서 찾을 수 없는 오브젝트·컴포넌트·쉐이프키·마테리얼 슬롯을 검사하고 올바른 대상으로 연결할 수 있습니다. 복구한 클립 복사본과 재사용할 대응표를 저장해 결과를 확인하고 다시 활용합니다.",
                "オブジェクトの移動や名前変更で、アバターと合わなくなったアニメーションの接続を修復する機能です。複数のクリップから見つからないオブジェクト、コンポーネント、シェイプキー、マテリアルスロットを調べ、有効な対象へ対応付けます。修復したクリップのコピーと再利用できる対応表を保存し、結果の確認や再利用ができます。" };
        _tutorial.Configure(course, names[0], names[1], names[2], tutorialCourses[course],
            overviewEn: overview[0], overviewKo: overview[1], overviewJa: overview[2]);
        _tutorial.BeginFrame();
    }
}
