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
    private Rect tutorialShapeTargetAnchor;
    private TutorialStep tutorialStep;
    private int pendingTutorialStep = -1;
    private bool tutorialPreviewWasStarted;
    private int tutorialPreviewLayer = -1, tutorialPreviewButton = -1;
    private string TutorialSessionKey => $"DiNe.MultiDresser.Tutorial.{target.GetInstanceID()}";
    private const int TutorialStepCount = (int)TutorialStep.Complete + 1;
    private bool OwnsTutorial => target != null &&
        SessionState.GetInt(TutorialSessionKey + ".Owner", 0) == GetInstanceID();

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
        if (!tutorialActive || pendingTutorialStep >= 0) return;
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
        if (!tutorialActive || !OwnsTutorial || next < 0 || next >= TutorialStepCount) return;
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
        if (!tutorialActive) return false;
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
        if (!tutorialActive || tutorialStep != step || !IsRequiredTutorialStep(step)) return;
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
        if (!tutorialActive || pendingTutorialStep >= 0 || tutorialStep <= TutorialStep.Avatar) return;
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
        tutorialBubbleDrawn = false;
        if (tutorialActive && !OwnsTutorial)
        {
            DiNeTutorialBubble.ClearFrame();
            tutorialActive = false;
            pendingTutorialStep = -1;
            EditorApplication.delayCall -= FlushTutorialTransition;
        }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(Localized("Tutorial", "튜토리얼", "チュートリアル"), EditorStyles.boldLabel);
        if (tutorialActive)
        {
            EditorGUILayout.LabelField($"{(int)tutorialStep + 1} / {TutorialStepCount}", EditorStyles.miniLabel);
        }
        Color previous = GUI.backgroundColor;
        EditorGUILayout.BeginHorizontal();
        if (!tutorialActive)
        {
            GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
            if (GUILayout.Button(new GUIContent(Localized("Start guided tutorial", "말풍선 튜토리얼 시작", "吹き出しチュートリアルを開始"),
                    Localized("Learn with your current avatar and settings.", "현재 아바타와 설정으로 사용법을 따라 해봅니다.", "現在のアバターと設定で操作を学びます。")), GUILayout.Height(30)))
            {
                EditorApplication.delayCall += () => { if (this != null && target != null) StartTutorial(); };
            }
        }
        else
        {
            if (GUILayout.Button(new GUIContent(Localized("Restart", "처음부터", "最初から"),
                    Localized("Return to the first explanation.", "첫 설명부터 다시 시작합니다.", "最初の説明に戻ります。")), GUILayout.Height(24)))
                QueueTutorialStep(TutorialStep.Welcome);
            if (GUILayout.Button(new GUIContent(Localized("End tutorial", "튜토리얼 종료", "終了"),
                    Localized("End guidance and restore the current preview.", "안내를 종료하고 현재 미리보기를 복원합니다.", "案内を終了して現在のプレビューを元に戻します。")), GUILayout.Height(24)))
                EditorApplication.delayCall += () => { if (this != null && target != null) StopTutorial(); };
        }
        GUI.backgroundColor = previous;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        Rect anchor = GUILayoutUtility.GetLastRect();
        DrawTutorialBubble(TutorialStep.Welcome, anchor);
        DrawTutorialBubble(TutorialStep.Complete, anchor);
        GUILayout.Space(8);
    }

    private void DrawTutorialNavigation(Rect anchor)
    {
        if (!tutorialActive) return;
        if (tutorialStep == TutorialStep.IndependentToggles)
            DrawTutorialBubble(TutorialStep.IndependentToggles, anchor);
        else if (selectedContentTab != 0 && tutorialStep > TutorialStep.Avatar && tutorialStep < TutorialStep.AutoApply)
            DiNeTutorialBubble.Draw(anchor, Localized("Return to Wardrobe", "옷장 탭으로 돌아가기", "衣装タブに戻る"),
                Localized("Select Wardrobe to continue.", "옷장 탭을 선택하세요.", "衣装タブを選んでください。"), "", false);
    }

    private void DrawTutorialBubble(TutorialStep step, Rect anchor)
    {
        if (!tutorialActive || tutorialStep != step || tutorialBubbleDrawn) return;
        tutorialBubbleDrawn = true;
        GetTutorialCopy(step, out string title, out string body);
        var gen = target as DiNeMultiDresser;
        if (!IsRequiredTutorialStep(step) && step >= TutorialStep.Menu && step <= TutorialStep.MaterialSlots &&
            step != TutorialStep.ShapeKeys && FindTutorialOutfit(gen) < 1)
            body = Localized("Add an outfit first, or click to continue.", "의상을 먼저 추가하거나 말풍선을 눌러 넘어가세요.", "衣装を追加するか、クリックして次へ進んでください。");
        else if ((step == TutorialStep.ShapeKeySettings || step == TutorialStep.ShapeKeyValues) &&
                 gen != null && gen.shapeKeyTargets.Count == 0)
            body = Localized("Add a body mesh here, or click to continue.", "여기에 바디 메쉬를 추가하거나 말풍선을 눌러 넘어가세요.", "ここに体メッシュを追加するか、クリックして次へ進んでください。");
        else if ((step == TutorialStep.MaterialRenderer || step == TutorialStep.MaterialSlots) &&
                 gen != null && selectedLayerIndex >= 0 && selectedLayerIndex < gen.layers.Count)
        {
            int outfit = FindTutorialOutfit(gen);
            var swaps = gen.layers[selectedLayerIndex].perButtonMaterialSwaps;
            if (outfit >= 0 && (outfit >= swaps.Count || swaps[outfit] == null || swaps[outfit].entries.Count == 0))
                body = Localized("Press Add, then assign the object's Renderer.",
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
        if (!tutorialActive || tutorialStep != TutorialStep.Category || tutorialStepWasComplete) return default(Rect);
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
        if (!tutorialActive || (tutorialStep != TutorialStep.DefaultEmpty && tutorialStep != TutorialStep.Outfit)) return;
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
        if (FindTutorialOutfit(gen) >= 1) return;
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
        if (!tutorialActive) return;
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
                body = Localized("Follow the highlighted controls. Click to begin.", "강조된 조작을 따라 하세요. 말풍선을 누르면 시작합니다.", "強調された操作を進めます。クリックして開始します。");
                return;
            case TutorialStep.Avatar:
                title = Localized("Avatar", "아바타", "アバター");
                body = Localized("Assign Avatar Root or press ↺.", "아바타 Root를 지정하거나 ↺를 누르세요.", "Avatar Rootを指定するか、↺を押してください。");
                return;
            case TutorialStep.Category:
                title = Localized("Category name", "카테고리 이름", "カテゴリー名");
                body = Localized("Enter a category name.", "카테고리 이름을 입력하세요.", "カテゴリー名を入力してください。");
                return;
            case TutorialStep.DefaultState:
                title = Localized("First slot", "첫 칸", "最初の枠");
                body = Localized("Use the first slot for the default outfit.", "첫 칸에 기본 의상을 등록하세요.", "最初の枠に初期衣装を登録してください。");
                return;
            case TutorialStep.DefaultOutfit:
                title = Localized("Default outfit", "기본 의상", "初期衣装");
                body = Localized("Drag the default outfit into Target Object. For an empty default, continue.", "대상 오브젝트 칸에 기본 의상을 드래그하세요. 빈 기본 상태를 쓰려면 넘어가세요.", "対象欄に初期衣装をドラッグします。空にする場合は次へ進んでください。");
                return;
            case TutorialStep.DefaultEmpty:
                title = Localized("Empty default", "빈 기본 상태", "空の初期状態");
                body = Localized("For an empty default, leave the first Target Object empty. If no slot exists, press Use an all-OFF default.", "빈 기본 상태를 쓰려면 첫 대상을 비워 두세요. 칸이 없다면 모두 OFF인 기본 상태 만들기를 누르세요.", "空の初期状態にする場合は最初の対象欄を空にします。枠がなければすべてOFFの初期状態を作成を押してください。");
                return;
            case TutorialStep.Outfit:
                title = Localized("Add an outfit", "의상 추가", "衣装を追加");
                body = Localized("Drag an outfit from Hierarchy here. Add it after the default slot.", "Hierarchy의 의상을 여기에 드래그하세요. 기본 칸 다음에 등록하세요.", "Hierarchyの衣装をここにドラッグし、初期枠の後に登録してください。");
                return;
            case TutorialStep.Menu:
                title = Localized("Menu name", "메뉴 이름", "メニュー名");
                body = Localized("Type the outfit button name here.", "여기에 의상 버튼 이름을 입력하세요.", "ここに衣装ボタンの名前を入力してください。");
                return;
            case TutorialStep.Icon:
                title = Localized("Edit icon", "아이콘 편집", "アイコン編集");
                body = Localized("Press Edit Icon to adjust the image.", "아이콘 편집을 눌러 이미지를 조절하세요.", "アイコン編集を押して画像を調整してください。");
                return;
            case TutorialStep.RegenerateIcon:
                title = Localized("Update icon", "아이콘 갱신", "アイコンを更新");
                body = Localized("Press Regenerate Icon to capture the outfit again.", "아이콘 재생성을 눌러 의상을 다시 촬영하세요.", "アイコン再生成を押して衣装を撮り直してください。");
                return;
            case TutorialStep.Preview:
                title = Localized("Preview", "미리보기", "プレビュー");
                body = Localized("Press this outfit's Preview button.", "이 의상의 미리보기 버튼을 누르세요.", "この衣装のプレビューボタンを押してください。");
                return;
            case TutorialStep.RestorePreview:
                title = Localized("Stop preview", "미리보기 종료", "プレビューを終了");
                body = Localized("Press the same Preview button again.", "같은 미리보기 버튼을 다시 누르세요.", "同じプレビューボタンをもう一度押してください。");
                return;
            case TutorialStep.ShapeKeys:
                title = Localized("Add a body mesh", "바디 메쉬 추가", "体メッシュを追加");
                body = Localized("Drag a body mesh into Shape Key Targets.", "쉐이프키 타겟에 바디 메쉬를 드래그하세요.", "シェイプキー対象に体メッシュをドラッグしてください。");
                return;
            case TutorialStep.ShapeKeySettings:
                title = Localized("Open shape keys", "쉐이프키 설정 열기", "シェイプキー設定を開く");
                body = Localized("Add a body mesh, then expand this outfit's Shape Key Settings.", "바디 메쉬를 추가한 뒤 이 의상의 쉐이프키 설정을 펼치세요.", "体メッシュを追加し、この衣装のシェイプキー設定を開いてください。");
                return;
            case TutorialStep.ShapeKeyValues:
                title = Localized("Set shape values", "쉐이프키 값 조절", "シェイプキー値を調整");
                body = Localized("Expand Shape Key Settings and move the sliders.", "쉐이프키 설정을 펼쳐 슬라이더 값을 조절하세요.", "シェイプキー設定を開き、スライダーを調整してください。");
                return;
            case TutorialStep.LinkedObjects:
                title = Localized("Add linked objects", "연결 오브젝트 추가", "連動対象を追加");
                body = Localized("Drag this outfit's accessories into this area.", "이 의상에 붙일 액세서리를 여기에 드래그하세요.", "この衣装のアクセサリーをここにドラッグしてください。");
                return;
            case TutorialStep.Materials:
                title = Localized("Add a material swap", "마테리얼 교체 추가", "マテリアル交換を追加");
                body = Localized("Press Add beside Material Swaps.", "마테리얼 교체 옆의 추가를 누르세요.", "マテリアル交換の横の追加を押してください。");
                return;
            case TutorialStep.MaterialRenderer:
                title = Localized("Choose a Renderer", "Renderer 지정", "Rendererを指定");
                body = Localized("Expand Material Swaps and assign the object's Renderer.", "마테리얼 교체를 펼쳐 Renderer를 지정하세요.", "マテリアル交換を開き、Rendererを指定してください。");
                return;
            case TutorialStep.MaterialSlots:
                title = Localized("Choose materials", "교체 마테리얼 지정", "交換マテリアルを指定");
                body = Localized("Set the Renderer, then put new materials in its numbered slots.", "Renderer를 지정하고 번호가 있는 칸에 새 마테리얼을 넣으세요.", "Rendererを指定し、番号の付いた枠に新しいマテリアルを入れてください。");
                return;
            case TutorialStep.Categories:
                title = Localized("Add a category", "카테고리 추가", "カテゴリーを追加");
                body = Localized("Press + to add another category.", "+를 눌러 다른 카테고리를 추가하세요.", "+を押して別のカテゴリーを追加してください。");
                return;
            case TutorialStep.CategorySwitch:
                title = Localized("Select a category", "카테고리 선택", "カテゴリーを選択");
                body = Localized("Select a category tab to edit its outfits.", "카테고리 탭을 눌러 해당 의상을 편집하세요.", "カテゴリーのタブを選び、衣装を編集してください。");
                return;
            case TutorialStep.IndependentToggles:
                title = Localized("Independent toggles", "독립 토글", "独立トグル");
                body = Localized("Select Independent Toggles to add separate switches.", "독립 토글 탭을 눌러 별도 토글을 추가하세요.", "独立トグルタブを選んで個別のスイッチを追加してください。");
                return;
            case TutorialStep.AutoApply:
                title = Localized("Check the avatar", "아바타 확인", "アバターを確認");
                body = Localized("Use Play Mode or upload to check the avatar.", "Play Mode나 업로드에서 아바타를 확인하세요.", "Play Modeまたはアップロードでアバターを確認してください。");
                return;
            default:
                title = Localized("Done", "완료", "完了");
                body = Localized("Click to finish the tutorial.", "말풍선을 눌러 튜토리얼을 마치세요.", "クリックしてチュートリアルを終了してください。");
                return;
        }
    }
}
#endif
