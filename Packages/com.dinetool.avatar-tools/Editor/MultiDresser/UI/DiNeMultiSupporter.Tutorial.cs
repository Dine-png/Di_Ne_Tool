#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public partial class DiNeMultiSupporter
{
    // Session-only guidance; changing lesson order resets older saved progress.
    private const int TutorialSessionVersion = 2;
    private enum TutorialStep
    {
        Welcome, Avatar, Category, DefaultState, DefaultOutfit, DefaultEmpty, Outfit,
        Menu, Icon, RegenerateIcon, Preview, RestorePreview, ShapeKeys, ShapeKeySettings,
        ShapeKeyValues, LinkedObjects, Materials, MaterialRenderer, MaterialSlots,
        Categories, CategorySwitch, IndependentToggles, AutoApply, Complete
    }

    private bool tutorialActive;
    private bool tutorialFocusPending;
    private bool tutorialStepWasComplete;
    private bool tutorialBubbleDrawn;
    private bool tutorialVisibilityKnown;
    private bool tutorialWasExpanded;
    private Rect tutorialShapeTargetAnchor;
    private TutorialStep tutorialStep;
    private int pendingTutorialStep = -1;
    private bool tutorialPreviewWasStarted;
    private int tutorialPreviewLayer = -1, tutorialPreviewButton = -1;
    private string TutorialSessionKey => $"DiNe.MultiDresser.Tutorial.{target.GetInstanceID()}";
    private const int TutorialStepCount = (int)TutorialStep.Complete + 1;
    private bool OwnsTutorial => target != null &&
        SessionState.GetInt(TutorialSessionKey + ".Owner", 0) == GetInstanceID();
    private bool TutorialExpanded
    {
        get => EditorPrefs.GetBool("DiNe.Tutorial.MultiDresser.Expanded", true);
        set
        {
            if (TutorialExpanded == value) return;
            EditorPrefs.SetBool("DiNe.Tutorial.MultiDresser.Expanded", value);
            SynchronizeTutorialVisibility();
        }
    }

    private void SynchronizeTutorialVisibility()
    {
        bool expanded = TutorialExpanded;
        if (tutorialVisibilityKnown && tutorialWasExpanded == expanded) return;
        tutorialVisibilityKnown = true;
        tutorialWasExpanded = expanded;
        EditorApplication.delayCall -= FlushTutorialTransition;
        pendingTutorialStep = -1;
        DiNeTutorialBubble.ClearFrame();
        if (expanded && tutorialActive)
        {
            CaptureTutorialEntryState();
            tutorialFocusPending = true;
        }
        Repaint();
    }

    private string TutorialOverview => Localized(
        "Multi Dresser creates a VRChat Expressions wardrobe with one outfit choice per category. An outfit can switch matching accessories, body BlendShapes and materials together. Preview the combinations in the scene; menu buttons and animations are generated automatically on upload or in Play Mode.",
        "Multi Dresser는 카테고리마다 의상을 하나씩 선택하는 VRChat Expressions 옷장을 만듭니다. 의상을 바꿀 때 액세서리·바디 쉐이프키·머티리얼도 함께 바꿀 수 있습니다. 씬에서 조합을 미리 확인하고, 업로드나 Play Mode에서 메뉴 버튼과 애니메이션을 자동 생성합니다.",
        "Multi Dresserはカテゴリーごとに衣装を1つ選ぶVRChat Expressionsの衣装メニューを作ります。衣装と一緒にアクセサリー・体のシェイプキー・マテリアルを切り替えられます。シーンで組み合わせを確認し、アップロードやPlay Modeでボタンとアニメーションを自動生成します。");

    private static bool IsRequiredTutorialStep(TutorialStep step)
    {
        return step == TutorialStep.Avatar || step == TutorialStep.Category ||
               step == TutorialStep.Outfit || step == TutorialStep.Preview ||
               step == TutorialStep.RestorePreview;
    }

    private void RestoreTutorial()
    {
        int saved = SessionState.GetInt(TutorialSessionKey + ".Version", 0) == TutorialSessionVersion
            ? SessionState.GetInt(TutorialSessionKey, -1) : -1;
        tutorialActive = saved >= 0 && saved < TutorialStepCount;
        if (tutorialActive)
        {
            var owner = EditorUtility.InstanceIDToObject(SessionState.GetInt(TutorialSessionKey + ".Owner", 0));
            if (owner != null && owner != this)
            {
                tutorialActive = false;
                return;
            }
            SessionState.SetInt(TutorialSessionKey + ".Owner", GetInstanceID());
        }
        tutorialStep = tutorialActive ? (TutorialStep)saved : TutorialStep.Welcome;
        tutorialFocusPending = tutorialActive;
        selectedLayerIndex = Mathf.Max(0, SessionState.GetInt(TutorialSessionKey + ".Layer", 0));
        // OnDisable/domain reload restores the scene preview. Re-run the actual
        // preview action rather than letting that lifecycle cleanup complete a lesson.
        if (tutorialStep == TutorialStep.RestorePreview) tutorialStep = TutorialStep.Preview;
        CaptureTutorialEntryState();
        if (tutorialActive) SaveTutorial();
    }

    private void SaveTutorial()
    {
        if (!OwnsTutorial) return;
        SessionState.SetInt(TutorialSessionKey + ".Version", TutorialSessionVersion);
        SessionState.SetInt(TutorialSessionKey, tutorialActive ? (int)tutorialStep : -1);
        SessionState.SetInt(TutorialSessionKey + ".Layer", selectedLayerIndex);
    }

    private void SuspendTutorial()
    {
        EditorApplication.delayCall -= FlushTutorialTransition;
        pendingTutorialStep = -1;
        if (target != null) SaveTutorial();
    }

    private void StartTutorial()
    {
        EditorApplication.delayCall -= FlushTutorialTransition;
        pendingTutorialStep = -1;
        tutorialActive = true;
        SessionState.SetInt(TutorialSessionKey + ".Owner", GetInstanceID());
        tutorialStep = TutorialStep.Welcome;
        tutorialFocusPending = true;
        tutorialPreviewWasStarted = false;
        tutorialPreviewLayer = tutorialPreviewButton = -1;
        SelectContentTab(0);
        CaptureTutorialEntryState();
        SaveTutorial();
        Repaint();
    }

    private void StopTutorial()
    {
        DiNeTutorialBubble.ClearFrame();
        EditorApplication.delayCall -= FlushTutorialTransition;
        pendingTutorialStep = -1;
        tutorialActive = false;
        tutorialPreviewWasStarted = false;
        tutorialPreviewLayer = tutorialPreviewButton = -1;
        ClearPreview();
        SaveTutorial();
        Repaint();
    }

    private void QueueTutorialStep(TutorialStep step)
    {
        if (!tutorialActive || !TutorialExpanded || pendingTutorialStep >= 0) return;
        DiNeTutorialBubble.ClearFrame();
        pendingTutorialStep = (int)step;
        // A bubble can sit inside an item/change-check scope. Keep the current
        // layout intact until every scope and serialized edit has been closed.
        EditorApplication.delayCall -= FlushTutorialTransition;
        EditorApplication.delayCall += FlushTutorialTransition;
        Repaint();
    }

    private void FlushTutorialTransition()
    {
        EditorApplication.delayCall -= FlushTutorialTransition;
        int next = pendingTutorialStep;
        pendingTutorialStep = -1;
        if (!tutorialActive || !TutorialExpanded || !OwnsTutorial || next < 0 || next >= TutorialStepCount) return;
        if (next == (int)tutorialStep + 1 && IsRequiredTutorialStep(tutorialStep) &&
            !CanCompleteTutorialAction(tutorialStep)) return;
        tutorialStep = (TutorialStep)next;
        tutorialFocusPending = true;
        CaptureTutorialEntryState();
        SaveTutorial();
        Repaint();
    }

    private void CaptureTutorialEntryState()
    {
        tutorialStepWasComplete = IsRequiredTutorialStep(tutorialStep) &&
            tutorialStep != TutorialStep.RestorePreview && CanCompleteTutorialAction(tutorialStep);
    }

    private bool CanAdvanceTutorialBubble()
    {
        if (!tutorialActive || !TutorialExpanded) return false;
        return !IsRequiredTutorialStep(tutorialStep) ||
            (tutorialStep != TutorialStep.RestorePreview && tutorialStepWasComplete &&
             CanCompleteTutorialAction(tutorialStep));
    }

    private void RememberTutorialPreview()
    {
        tutorialPreviewWasStarted = true;
        tutorialPreviewLayer = previewLayerIndex;
        tutorialPreviewButton = previewButtonIndex;
    }

    private void AdvanceOptionalTutorial()
    {
        if (!CanAdvanceTutorialBubble()) return;
        if (tutorialStep == TutorialStep.Complete) StopTutorial();
        else
        {
            if (tutorialStep == TutorialStep.Preview) RememberTutorialPreview();
            QueueTutorialStep(tutorialStep + 1);
        }
    }

    private void NotifyTutorialAction(TutorialStep step)
    {
        if (step == TutorialStep.Avatar) independentTutorial?.NotifyAction("avatar");
        if (!tutorialActive || !TutorialExpanded || tutorialStep != step || !IsRequiredTutorialStep(step)) return;
        if (!CanCompleteTutorialAction(step)) return;
        if (step == TutorialStep.Preview) RememberTutorialPreview();
        QueueTutorialStep(step + 1);
    }

    private bool HasTutorialAvatar(DiNeMultiDresser gen)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (gen == null || gen.rootTransform == null || EditorUtility.IsPersistent(gen.rootTransform)) return false;
        var descriptor = gen.GetAvatarDescriptor();
        return descriptor != null && descriptor.transform == gen.rootTransform &&
               gen.ValidateAssignment(out _);
    }

    private bool HasTutorialCategory(DiNeMultiDresser gen)
    {
        if (gen == null || selectedLayerIndex < 0 || selectedLayerIndex >= gen.layers.Count) return false;
        var layer = gen.layers[selectedLayerIndex];
        if (layer == null || string.IsNullOrWhiteSpace(layer.layerName)) return false;
        string name = layer.layerName.Trim();
        for (int i = 0; i < gen.layers.Count; i++)
            if (i != selectedLayerIndex && gen.layers[i] != null &&
                string.Equals(name, gen.layers[i].layerName?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool IsTutorialObject(DiNeMultiDresser gen, GameObject obj)
    {
        return gen != null && gen.rootTransform != null && obj != null &&
               !EditorUtility.IsPersistent(obj) && obj.transform != gen.rootTransform &&
               obj.transform.IsChildOf(gen.rootTransform);
    }

    private int FindTutorialOutfit(DiNeMultiDresser gen)
    {
        if (!tutorialActive || gen == null || selectedLayerIndex < 0 || selectedLayerIndex >= gen.layers.Count)
            return -1;
        var layer = gen.layers[selectedLayerIndex];
        if (layer == null) return -1;
        for (int i = 1; i < layer.targets.Count; i++)
            if (IsTutorialObject(gen, layer.targets[i])) return i;
        return -1;
    }

    private bool IsTutorialPreview(int layer, int button)
    {
        return tutorialActive && tutorialStep == TutorialStep.RestorePreview &&
               tutorialPreviewWasStarted && tutorialPreviewLayer == layer && tutorialPreviewButton == button;
    }

    private bool CanCompleteTutorialAction(TutorialStep step)
    {
        var gen = target as DiNeMultiDresser;
        if (!HasTutorialAvatar(gen)) return false;
        if (step == TutorialStep.Avatar) return true;
        if (!HasTutorialCategory(gen)) return false;
        if (step == TutorialStep.Category) return true;
        if (FindTutorialOutfit(gen) < 1) return false;
        if (step == TutorialStep.Outfit) return true;
        if (step == TutorialStep.Preview)
            return activePreviewOwnerId != 0 && previewLayerIndex == selectedLayerIndex && previewButtonIndex > 0 &&
                   previewButtonIndex < gen.layers[selectedLayerIndex].targets.Count &&
                    IsTutorialObject(gen, gen.layers[selectedLayerIndex].targets[previewButtonIndex]) &&
                    previewOriginalObjectStates.ContainsKey(gen.layers[selectedLayerIndex].targets[previewButtonIndex]);
        return step == TutorialStep.RestorePreview && tutorialPreviewWasStarted &&
               previewLayerIndex < 0 && previewButtonIndex < 0 && activePreviewOwnerId == 0 &&
               previewRestoreActions.Count == 0 && previewOriginalObjectStates.Count == 0 &&
               previewOriginalShapeWeights.Count == 0 && previewBaseMaterials.Count == 0;
    }

    private void ValidateTutorialProgress()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!tutorialActive || !TutorialExpanded || pendingTutorialStep >= 0 || tutorialStep <= TutorialStep.Avatar) return;
        var gen = target as DiNeMultiDresser;
        if (!HasTutorialAvatar(gen)) QueueTutorialStep(TutorialStep.Avatar);
        else if (tutorialStep > TutorialStep.Category && !HasTutorialCategory(gen))
            QueueTutorialStep(TutorialStep.Category);
        else if (tutorialStep > TutorialStep.Outfit && tutorialStep <= TutorialStep.RestorePreview && FindTutorialOutfit(gen) < 1)
            QueueTutorialStep(TutorialStep.Outfit);
        else if (tutorialStep == TutorialStep.RestorePreview &&
                 (!tutorialPreviewWasStarted || previewLayerIndex != tutorialPreviewLayer ||
                  previewButtonIndex != tutorialPreviewButton))
        {
            tutorialPreviewWasStarted = false;
            QueueTutorialStep(TutorialStep.Preview);
        }
    }

    private void DrawTutorialControls()
    {
        SynchronizeTutorialVisibility();
        tutorialBubbleDrawn = false;
        if (tutorialActive && !OwnsTutorial)
        {
            DiNeTutorialBubble.ClearFrame();
            tutorialActive = false;
            pendingTutorialStep = -1;
            EditorApplication.delayCall -= FlushTutorialTransition;
        }
        Color previous = GUI.backgroundColor;
        bool previousChanged = GUI.changed, previousEnabled = GUI.enabled;
        try
        {
            GUI.enabled = true;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            TutorialExpanded = EditorGUILayout.Foldout(TutorialExpanded, new GUIContent(
                Localized("Tutorial", "튜토리얼", "チュートリアル"),
                Localized("Collapse or expand the instructions. Collapsing pauses guidance and keeps your current step.",
                    "튜토리얼 안내를 접거나 펼칩니다. 접어 두면 현재 단계를 유지한 채 안내를 잠시 숨깁니다.",
                    "案内を折りたたむ・開く操作です。折りたたむと現在のステップを保ったまま案内を一時的に隠します。")),
                true, EditorStyles.foldoutHeader);
            if (!TutorialExpanded)
            {
                EditorGUILayout.EndVertical();
                GUILayout.Space(8);
                return;
            }
            if (tutorialActive)
                EditorGUILayout.LabelField($"{(int)tutorialStep + 1} / {TutorialStepCount}", EditorStyles.miniLabel);
            else
            {
                EditorGUILayout.LabelField(TutorialOverview, EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
            }
            EditorGUILayout.BeginHorizontal();
            if (!tutorialActive)
            {
                GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
                if (GUILayout.Button(new GUIContent(Localized("Start guided tutorial", "말풍선 튜토리얼 시작", "吹き出しチュートリアルを開始"),
                        Localized("Build and preview a wardrobe using your current avatar.", "현재 아바타로 옷장을 구성하고 미리보는 방법을 안내합니다.", "現在のアバターで衣装メニューの設定とプレビューを学びます。")), GUILayout.Height(30)))
                    EditorApplication.delayCall += () => { if (this != null && target != null && TutorialExpanded) StartTutorial(); };
            }
            else
            {
                if (GUILayout.Button(new GUIContent(Localized("Restart", "처음부터", "最初から"),
                        Localized("Return to the first explanation.", "첫 설명부터 다시 시작합니다.", "最初の説明に戻ります。")), GUILayout.Height(24)))
                    QueueTutorialStep(TutorialStep.Welcome);
                if (GUILayout.Button(new GUIContent(Localized("End tutorial", "튜토리얼 종료", "終了"),
                        Localized("End guidance and restore the current preview.", "안내를 종료하고 현재 미리보기를 복원합니다.", "案内を終了して現在のプレビューを元に戻します。")), GUILayout.Height(24)))
                    EditorApplication.delayCall += () => { if (this != null && target != null && TutorialExpanded) StopTutorial(); };
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            Rect anchor = GUILayoutUtility.GetLastRect();
            DrawTutorialBubble(TutorialStep.Welcome, anchor);
            DrawTutorialBubble(TutorialStep.Complete, anchor);
            GUILayout.Space(8);
        }
        finally { GUI.backgroundColor = previous; GUI.changed = previousChanged; GUI.enabled = previousEnabled; }
    }

    private void DrawTutorialNavigation(Rect anchor)
    {
        if (!tutorialActive || !TutorialExpanded) return;
        if (tutorialStep == TutorialStep.IndependentToggles)
            DrawTutorialBubble(TutorialStep.IndependentToggles, anchor);
        else if (selectedContentTab != 0 && tutorialStep > TutorialStep.Avatar && tutorialStep < TutorialStep.AutoApply)
            DiNeTutorialBubble.Draw(anchor, Localized("Return to Wardrobe", "옷장 탭으로 돌아가기", "衣装タブに戻る"),
                Localized("Select Wardrobe to continue.", "옷장 탭을 선택하세요.", "衣装タブを選んでください。"), "", false);
    }

    private void DrawTutorialBubble(TutorialStep step, Rect anchor)
    {
        if (!tutorialActive || !TutorialExpanded || tutorialStep != step || tutorialBubbleDrawn) return;
        tutorialBubbleDrawn = true;
        GetTutorialCopy(step, out string title, out string body);
        var gen = target as DiNeMultiDresser;
        if (!IsRequiredTutorialStep(step) && step >= TutorialStep.Menu && step <= TutorialStep.MaterialSlots &&
            step != TutorialStep.ShapeKeys && FindTutorialOutfit(gen) < 1)
            body += "\n\n" + Localized("Add an outfit to try this setting, or click to continue.", "이 설정을 사용하려면 의상을 추가하거나 말풍선을 눌러 계속하세요.", "この設定を試す場合は衣装を追加するか、クリックして次へ進んでください。");
        else if ((step == TutorialStep.ShapeKeySettings || step == TutorialStep.ShapeKeyValues) &&
                 gen != null && gen.shapeKeyTargets.Count == 0)
            body += "\n\n" + Localized("Add a body mesh here to try these values, or click to continue.", "값을 조절하려면 여기에 바디 메쉬를 추가하거나 말풍선을 눌러 계속하세요.", "値を試す場合はここに体メッシュを追加するか、クリックして次へ進んでください。");
        else if ((step == TutorialStep.MaterialRenderer || step == TutorialStep.MaterialSlots) &&
                 gen != null && selectedLayerIndex >= 0 && selectedLayerIndex < gen.layers.Count)
        {
            int outfit = FindTutorialOutfit(gen);
            var swaps = gen.layers[selectedLayerIndex].perButtonMaterialSwaps;
            if (outfit >= 0 && (outfit >= swaps.Count || swaps[outfit] == null || swaps[outfit].entries.Count == 0))
                body += "\n\n" + Localized("Press Add, then assign the object's Renderer.",
                    "추가를 누른 뒤 오브젝트의 Renderer를 지정하세요.",
                    "追加を押し、オブジェクトのRendererを指定してください。");
        }
        bool canClick = CanAdvanceTutorialBubble();
        string hint = IsRequiredTutorialStep(step)
            ? canClick
                ? Localized("Already set. Click to continue.", "설정 완료. 말풍선을 눌러 계속하세요.", "設定済みです。クリックで次へ。")
                : Localized("Complete this action to continue.", "이 동작을 완료하면 넘어갑니다.", "この操作を完了すると進みます。")
            : step == TutorialStep.Complete
                ? Localized("Click this bubble to finish.", "말풍선을 클릭하면 튜토리얼을 마칩니다.", "吹き出しをクリックすると終了します。")
                : Localized("Click this bubble to continue.", "말풍선을 클릭하면 다음 설명으로 넘어갑니다.", "吹き出しをクリックすると次の説明に進みます。");
        if (DiNeTutorialBubble.Draw(anchor, $"{(int)step + 1} / {TutorialStepCount} · {title}", body, hint,
                canClick)) AdvanceOptionalTutorial();
        if (pendingTutorialStep >= 0) DiNeTutorialBubble.ClearFrame();
        if (tutorialFocusPending && pendingTutorialStep < 0 && Event.current.type == EventType.Repaint)
        {
            // Scroll only on entry to a step; manual scrolling remains available.
            bool changed = GUI.changed;
            Rect bubble = DiNeTutorialBubble.LastRect;
            GUI.ScrollTo(Rect.MinMaxRect(Mathf.Min(anchor.xMin, bubble.xMin), Mathf.Min(anchor.yMin, bubble.yMin),
                Mathf.Max(anchor.xMax, bubble.xMax), Mathf.Max(anchor.yMax, bubble.yMax)));
            GUI.changed = changed;
            tutorialFocusPending = false;
        }
    }

    private Rect DrawTutorialCategoryAction(DiNeMultiDresser gen)
    {
        if (!tutorialActive || !TutorialExpanded || tutorialStep != TutorialStep.Category || tutorialStepWasComplete) return default(Rect);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            bool confirmed = GUILayout.Button(new GUIContent(Localized("Confirm category", "카테고리 확인", "カテゴリーを確認"),
                    Localized("Confirm a nonempty, unique category name.", "비어 있지 않고 중복되지 않는 카테고리 이름을 확인합니다.", "空白でなく重複しないカテゴリー名を確認します。")), GUILayout.Height(24));
            Rect anchor = GUILayoutUtility.GetLastRect();
            if (confirmed)
            {
                if (serializedObject.ApplyModifiedProperties()) toggleMenuChoices?.Invalidate();
                NotifyTutorialAction(TutorialStep.Category);
            }
            return anchor;
        }
    }

    private void DrawTutorialOutfitActions(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layer)
    {
        if (!tutorialActive || !TutorialExpanded || (tutorialStep != TutorialStep.DefaultEmpty && tutorialStep != TutorialStep.Outfit)) return;
        if (layer.targets.Count == 0)
        {
            if (GUILayout.Button(new GUIContent(Localized("Use an all-OFF default", "모두 OFF인 기본 상태 만들기", "すべてOFFの初期状態を作成"),
                    Localized("Add an empty default slot, then drag the outfit that should create a menu button.",
                        "빈 기본 상태 칸을 만든 다음, 메뉴 버튼으로 사용할 의상을 드래그하세요.",
                        "空の初期状態を作成してから、メニューボタン用の衣装をドラッグします。")), GUILayout.Height(24)))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(gen, "Add Multi Dresser Default State");
                layer.EnsureSize(1);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gen);
                EditorUtility.SetDirty(gen);
                serializedObject.Update();
            }
            DrawTutorialBubble(TutorialStep.DefaultEmpty, GUILayoutUtility.GetLastRect());
        }
    }

    private void DrawTutorialItemFallback(DiNeMultiDresser gen, Rect anchor)
    {
        if (!TutorialExpanded || FindTutorialOutfit(gen) >= 1) return;
        DrawTutorialBubble(TutorialStep.Menu, anchor);
        DrawTutorialBubble(TutorialStep.Icon, anchor);
        DrawTutorialBubble(TutorialStep.RegenerateIcon, anchor);
        DrawTutorialBubble(TutorialStep.ShapeKeySettings, anchor);
        DrawTutorialBubble(TutorialStep.ShapeKeyValues, anchor);
        DrawTutorialBubble(TutorialStep.LinkedObjects, anchor);
        DrawTutorialBubble(TutorialStep.Materials, anchor);
        DrawTutorialBubble(TutorialStep.MaterialRenderer, anchor);
        DrawTutorialBubble(TutorialStep.MaterialSlots, anchor);
    }

    private void DrawTutorialPreviewBubble(DiNeMultiDresser gen, int layer, int button, Rect anchor)
    {
        if (!tutorialActive || !TutorialExpanded) return;
        int guidedButton = CanCompleteTutorialAction(TutorialStep.Preview) ? previewButtonIndex : FindTutorialOutfit(gen);
        if (tutorialStep == TutorialStep.Preview && button == guidedButton)
            DrawTutorialBubble(TutorialStep.Preview, anchor);
        else if (IsTutorialPreview(layer, button))
            DrawTutorialBubble(TutorialStep.RestorePreview, anchor);
    }

    private void GetTutorialCopy(TutorialStep step, out string title, out string body)
    {
        switch (step)
        {
            case TutorialStep.Welcome:
                title = Localized("Start", "시작", "開始");
                body = TutorialOverview;
                return;
            case TutorialStep.Avatar:
                title = Localized("Avatar", "아바타", "アバター");
                body = Localized("Avatar Root defines the objects and Expressions menu this wardrobe can use. Assign your scene avatar, or press ↺ to find it from this component.", "아바타 Root는 옷장이 사용할 오브젝트와 Expressions 메뉴의 범위를 정합니다. 씬 아바타를 지정하거나 ↺를 눌러 이 컴포넌트의 아바타를 찾으세요.", "Avatar Rootで衣装メニューが使う対象とExpressionsメニューの範囲を決めます。シーンアバターを指定するか、↺でこのコンポーネントのアバターを探してください。");
                return;
            case TutorialStep.Category:
                title = Localized("Category name", "카테고리 이름", "カテゴリー名");
                body = Localized("A category groups outfits that replace one another, such as Tops or Shoes. Enter a unique name to identify this group in the menu.", "카테고리는 상의·신발처럼 서로 교체할 의상들을 묶습니다. 메뉴에서 그룹을 구분할 수 있도록 다른 카테고리와 겹치지 않는 이름을 입력하세요.", "カテゴリーはトップスや靴など、互いに切り替える衣装をまとめます。メニューで区別できるよう、ほかと重複しない名前を入力してください。");
                return;
            case TutorialStep.DefaultState:
                title = Localized("First slot", "첫 칸", "最初の枠");
                body = Localized("The first slot is this category's default state and creates no outfit button. Use it for the base outfit, or leave its target empty for an all-OFF default.", "첫 칸은 카테고리의 기본 상태이며 의상 버튼이 생성되지 않습니다. 기본 의상을 넣거나 대상을 비워 모두 OFF인 기본 상태로 사용할 수 있습니다.", "最初の枠はカテゴリーの初期状態で、衣装ボタンは作りません。基本の衣装を入れるか、対象を空にしてすべてOFFの初期状態にできます。");
                return;
            case TutorialStep.DefaultOutfit:
                title = Localized("Default outfit", "기본 의상", "初期衣装");
                body = Localized("The default outfit returns when this category's selected outfit button is turned off. Drag that base outfit into the first Target Object field, or continue to use an empty default.", "선택한 의상 버튼을 끄면 이 카테고리는 기본 의상으로 돌아갑니다. 첫 대상 오브젝트 칸에 기본 의상을 넣거나, 빈 기본 상태를 쓰려면 계속하세요.", "選んだ衣装ボタンをOFFにすると、このカテゴリーは基本の衣装に戻ります。最初の対象欄に基本衣装を入れるか、空の初期状態を使う場合は次へ進んでください。");
                return;
            case TutorialStep.DefaultEmpty:
                title = Localized("Empty default", "빈 기본 상태", "空の初期状態");
                body = Localized("An empty default turns all registered outfits in this category OFF when no outfit is selected. Leave the first target empty; if no slot exists, press Use an all-OFF default.", "빈 기본 상태는 선택한 의상이 없을 때 이 카테고리의 등록 의상을 모두 끕니다. 첫 대상을 비워 두세요. 칸이 없다면 모두 OFF인 기본 상태 만들기를 누르세요.", "空の初期状態では、衣装が選ばれていないときにこのカテゴリーの登録衣装をすべてOFFにします。最初の対象を空にしてください。枠がなければすべてOFFの初期状態を作成を押します。");
                return;
            case TutorialStep.Outfit:
                title = Localized("Add an outfit", "의상 추가", "衣装を追加");
                body = Localized("Slots after the default become selectable outfit buttons, and selecting one switches the other outfits in this category OFF. Drag an outfit from the avatar's Hierarchy here to add it after the default slot.", "기본 칸 다음의 의상은 메뉴 버튼이 되며 하나를 선택하면 같은 카테고리의 다른 의상은 꺼집니다. 아바타의 Hierarchy에서 의상을 드래그해 기본 칸 뒤에 추가하세요.", "初期枠の後の衣装はメニューボタンになり、1つを選ぶと同じカテゴリーのほかの衣装はOFFになります。アバターのHierarchyから衣装をドラッグして初期枠の後に追加してください。");
                return;
            case TutorialStep.Menu:
                title = Localized("Menu name", "메뉴 이름", "メニュー名");
                body = Localized("This label appears on the outfit's VRChat Expressions button. Enter a recognizable name such as Summer Shirt so you can find it while wearing the avatar.", "이 이름은 의상의 VRChat Expressions 버튼에 표시됩니다. 아바타를 사용할 때 쉽게 찾을 수 있도록 여름 셔츠처럼 알아보기 쉬운 이름을 입력하세요.", "この名前は衣装のVRChat Expressionsボタンに表示されます。アバター使用時に見つけやすいよう、夏用シャツなど分かりやすい名前を入力してください。");
                return;
            case TutorialStep.Icon:
                title = Localized("Edit icon", "아이콘 편집", "アイコン編集");
                body = Localized("The icon shows which outfit a menu button selects. Press Edit Icon to adjust the capture view, framing and outline for a clear image.", "아이콘은 메뉴 버튼이 선택할 의상을 보여줍니다. 아이콘 편집을 눌러 촬영 시점·구도·외곽선을 조절하고 알아보기 쉬운 이미지를 만드세요.", "アイコンはボタンで選ぶ衣装を表します。アイコン編集を押し、撮影の視点・構図・輪郭を調整して見やすい画像を作ってください。");
                return;
            case TutorialStep.RegenerateIcon:
                title = Localized("Update icon", "아이콘 갱신", "アイコンを更新");
                body = Localized("Regenerate Icon replaces the image with a fresh capture of this outfit. Use it after changing the outfit's appearance or its capture settings.", "아이콘 재생성은 이 의상을 새로 촬영한 이미지로 교체합니다. 의상 외형이나 촬영 설정을 바꾼 뒤 사용하세요.", "アイコン再生成はこの衣装を撮り直した画像で置き換えます。衣装の見た目や撮影設定を変更した後に使ってください。");
                return;
            case TutorialStep.Preview:
                title = Localized("Preview", "미리보기", "プレビュー");
                body = Localized("Preview temporarily applies this outfit's visibility, linked objects, shape values and material swaps in the scene. Press its Preview button to check how the whole combination looks.", "미리보기는 의상 표시·연결 오브젝트·쉐이프키·머티리얼 교체를 씬에 임시로 적용합니다. 이 의상의 미리보기를 눌러 조합이 어떻게 보이는지 확인하세요.", "プレビューは衣装の表示・連動対象・シェイプキー・マテリアル交換をシーンに一時適用します。この衣装のプレビューを押し、組み合わせの見た目を確認してください。");
                return;
            case TutorialStep.RestorePreview:
                title = Localized("Stop preview", "미리보기 종료", "プレビューを終了");
                body = Localized("Ending preview restores the object's active states, BlendShape weights and materials from before previewing. Press the same Preview button again to restore the scene.", "미리보기를 종료하면 시작 전의 오브젝트 활성 상태·쉐이프키 값·머티리얼을 복원합니다. 같은 미리보기 버튼을 다시 눌러 씬을 되돌리세요.", "プレビューを終了すると開始前の有効状態・シェイプキー値・マテリアルを復元します。同じプレビューボタンをもう一度押し、シーンを元に戻してください。");
                return;
            case TutorialStep.ShapeKeys:
                title = Localized("Add a body mesh", "바디 메쉬 추가", "体メッシュを追加");
                body = Localized("Shape Key Targets are meshes whose BlendShapes should change with outfits, such as a body with clothing-fit shapes. Drag the body mesh here to make its shape controls available for each outfit.", "쉐이프키 타겟은 의상에 맞춰 쉐이프키를 바꿀 메시로, 의상 맞춤 쉐이프키가 있는 바디 등을 넣습니다. 바디 메쉬를 드래그하면 의상별 형태 조절을 사용할 수 있습니다.", "シェイプキー対象は衣装に合わせて形状を変えるメッシュで、衣装調整用シェイプキーのある体などを指定します。体メッシュをドラッグすると衣装ごとの形状調整が使えます。");
                return;
            case TutorialStep.ShapeKeySettings:
                title = Localized("Open shape keys", "쉐이프키 설정 열기", "シェイプキー設定を開く");
                body = Localized("Each outfit can store its own BlendShape values for the registered meshes. Add a body mesh, then expand this outfit's Shape Key Settings to choose how it fits.", "등록한 메시의 쉐이프키 값을 의상마다 다르게 저장할 수 있습니다. 바디 메쉬를 추가하고 이 의상의 쉐이프키 설정을 펼쳐 의상에 맞는 형태를 정하세요.", "登録したメッシュのシェイプキー値を衣装ごとに保存できます。体メッシュを追加し、この衣装のシェイプキー設定を開いて衣装に合う形を決めてください。");
                return;
            case TutorialStep.ShapeKeyValues:
                title = Localized("Set shape values", "쉐이프키 값 조절", "シェイプキー値を調整");
                body = Localized("These values are applied when this outfit is selected, useful for hiding body parts or matching clothing fit. Move the needed sliders and use Preview to check the result.", "이 값은 의상을 선택할 때 적용되어 신체 일부를 숨기거나 의상에 맞는 형태를 만드는 데 쓰입니다. 필요한 슬라이더를 조절하고 미리보기로 결과를 확인하세요.", "この値は衣装を選ぶと適用され、体の一部を隠したり衣装に合う形を作る際に使えます。必要なスライダーを調整し、プレビューで結果を確認してください。");
                return;
            case TutorialStep.LinkedObjects:
                title = Localized("Add linked objects", "연결 오브젝트 추가", "連動対象を追加");
                body = Localized("Linked objects switch ON with this outfit, such as matching shoes or a bag. Drag those accessories here so the outfit button controls the whole set together.", "연결 오브젝트는 어울리는 신발·가방처럼 이 의상과 함께 켜질 대상입니다. 액세서리를 여기에 드래그해 의상 버튼 하나로 세트를 함께 전환하세요.", "連動対象はお揃いの靴やバッグなど、この衣装と一緒にONにする対象です。ここにドラッグして衣装ボタン1つでセット全体を切り替えてください。");
                return;
            case TutorialStep.Materials:
                title = Localized("Add a material swap", "마테리얼 교체 추가", "マテリアル交換を追加");
                body = Localized("Material Swaps let an outfit button change a mesh's materials, such as its color or fabric. Press Add to register the renderer whose appearance should change with this outfit.", "머티리얼 교체로 의상 버튼을 누를 때 메시의 색상·원단 같은 외형을 바꿀 수 있습니다. 추가를 눌러 이 의상과 함께 바꿀 Renderer를 등록하세요.", "マテリアル交換で衣装ボタンを選ぶとメッシュの色や生地などを変えられます。追加を押し、この衣装と一緒に見た目を変えるRendererを登録してください。");
                return;
            case TutorialStep.MaterialRenderer:
                title = Localized("Choose a Renderer", "Renderer 지정", "Rendererを指定");
                body = Localized("The Renderer identifies which mesh receives the material swap. Expand Material Swaps and assign that object's Renderer to reveal its numbered material slots.", "Renderer는 머티리얼을 교체할 메시를 지정합니다. 머티리얼 교체를 펼쳐 대상 Renderer를 넣으면 번호별 머티리얼 슬롯이 표시됩니다.", "Rendererはマテリアルを交換するメッシュを指定します。マテリアル交換を開いて対象のRendererを入れると、番号付きスロットが表示されます。");
                return;
            case TutorialStep.MaterialSlots:
                title = Localized("Choose materials", "교체 마테리얼 지정", "交換マテリアルを指定");
                body = Localized("Each numbered slot corresponds to a material slot on the assigned Renderer. Put replacement materials in the slots you want to change, then preview the outfit to check their placement.", "각 번호는 지정한 Renderer의 머티리얼 슬롯에 대응합니다. 바꿀 칸에 교체 머티리얼을 넣고 의상을 미리봐서 원하는 위치에 적용되는지 확인하세요.", "各番号は指定したRendererのマテリアルスロットに対応します。変更する枠に交換用マテリアルを入れ、衣装プレビューで適用先を確認してください。");
                return;
            case TutorialStep.Categories:
                title = Localized("Add a category", "카테고리 추가", "カテゴリーを追加");
                body = Localized("Separate categories let you choose a top and shoes independently. Press + to create another group of outfits that should replace one another.", "카테고리를 나누면 상의와 신발 등을 따로 선택할 수 있습니다. +를 눌러 서로 교체할 의상들의 그룹을 추가하세요.", "カテゴリーを分けるとトップスと靴などを別々に選べます。+を押し、互いに切り替える衣装のグループを追加してください。");
                return;
            case TutorialStep.CategorySwitch:
                title = Localized("Select a category", "카테고리 선택", "カテゴリーを選択");
                body = Localized("A category tab opens that group's default state and outfit list. Select the tab you want to edit; the other categories keep their own outfits and settings.", "카테고리 탭은 해당 그룹의 기본 상태와 의상 목록을 엽니다. 편집할 탭을 선택하세요. 다른 카테고리의 의상과 설정은 따로 유지됩니다.", "カテゴリーのタブでそのグループの初期状態と衣装一覧を開きます。編集するタブを選んでください。ほかのカテゴリーはそれぞれの衣装と設定を保持します。");
                return;
            case TutorialStep.IndependentToggles:
                title = Localized("Independent toggles", "독립 토글", "独立トグル");
                body = Localized("Independent Toggles switch accessories separately from outfit choices and can group several objects into one switch. Select this tab if you want a hat or glasses you can turn on with any outfit.", "독립 토글은 의상 선택과 별도로 액세서리를 켜고 끄며 여러 오브젝트를 하나의 스위치로 묶을 수 있습니다. 어떤 의상에서도 사용할 모자·안경 토글이 필요하면 이 탭을 선택하세요.", "独立トグルは衣装の選択とは別にアクセサリーを切り替え、複数の対象を1つのスイッチにまとめられます。どの衣装でも使う帽子や眼鏡が必要ならこのタブを選んでください。");
                return;
            case TutorialStep.AutoApply:
                title = Localized("Check the avatar", "아바타 확인", "アバターを確認");
                body = Localized("Play Mode and upload automatically generate the configured wardrobe menus and animations on a temporary avatar. Check the outfit buttons, linked accessories, shapes and material swaps on that avatar.", "Play Mode와 업로드에서는 설정한 옷장 메뉴와 애니메이션을 임시 아바타에 자동 생성합니다. 해당 아바타에서 의상 버튼·연결 액세서리·쉐이프키·머티리얼 교체를 확인하세요.", "Play Modeとアップロードで設定した衣装メニューとアニメーションを一時アバターに自動生成します。そのアバターで衣装ボタン・連動アクセサリー・形状・マテリアル交換を確認してください。");
                return;
            default:
                title = Localized("Done", "완료", "完了");
                body = Localized("You can now build outfit categories and coordinate their objects, shapes and materials. Add the rest of your outfits using the same workflow; click to finish and restore any active preview.", "이제 의상 카테고리를 만들고 오브젝트·쉐이프키·머티리얼을 함께 구성할 수 있습니다. 같은 방법으로 나머지 의상을 추가하세요. 말풍선을 누르면 안내를 마치고 진행 중인 미리보기를 복원합니다.", "衣装カテゴリーを作り、対象・形状・マテリアルを連動させる手順を確認しました。同じ方法で残りの衣装を追加してください。クリックすると案内を終了し、実行中のプレビューを復元します。");
                return;
        }
    }
}
#endif
