using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public partial class DiNeAnimationTool
{
    [SerializeField] private SkinnedMeshRenderer _bodySmr;
    [SerializeField] private AnimationClip _exprClip;
    [SerializeField] private string _exprNewClipName = "New Expression";
    [SerializeField] private bool _includeGestureKeys = true;
    [SerializeField] private AnimatorController _exprFxController;
    [SerializeField] private int _exprFxLayerSel;
    [SerializeField] private string _exprShapeSearch = "";
    private float[] _exprShapeValues, _exprWorkingValues;
    private int _exprFxStateSel = -1;
    private bool _exprFxPreviewMode;

    private void DrawExpressionGUI()
    {
        if (targetAvatarRoot == null) return;
        BeginCard(Tr("Expression Preview", "표정 미리보기", "表情プレビュー"));
        SkinnedMeshRenderer[] meshes = targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0).ToArray();
        if (meshes.Length == 0)
        {
            EditorGUILayout.HelpBox(Tr("No mesh with shape keys was found in this avatar.", "이 아바타에서 쉐이프키가 있는 메시를 찾지 못했습니다.", "このアバターにシェイプキーを持つメッシュが見つかりません。"), MessageType.Info);
            EndCard(); return;
        }
        int current = Array.IndexOf(meshes, _bodySmr);
        if (current < 0) { _bodySmr = meshes[0]; ReadShapeValues(); current = 0; }
        string[] names = meshes.Select(r => AnimationUtility.CalculateTransformPath(r.transform, targetAvatarRoot.transform) + " (" + r.sharedMesh.blendShapeCount + ")").ToArray();
        int next = EditorGUILayout.Popup(Tr("Face Mesh", "얼굴 메시", "顔メッシュ"), current, names);
        TutorialAnchor("expression-mesh");
        if (next != current) { _bodySmr = meshes[next]; ReadShapeValues(); }
        TutorialDraw("expression-mesh");
        DrawPreviewImage(true);
        EndCard();
        DrawExpressionClipSection();
        DrawExpressionFxSection();
        DrawExpressionShapeKeys();
    }

    private void RefreshBodySmr()
    {
        _bodySmr = null; _exprShapeValues = _exprWorkingValues = null;
        _exprFxPreviewMode = false; _exprFxStateSel = -1;
        if (targetAvatarRoot == null) { _exprFxController = null; return; }
        SkinnedMeshRenderer[] renderers = targetAvatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _bodySmr = renderers.FirstOrDefault(r => r.name == "Body" && r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0)
            ?? renderers.Where(r => r.sharedMesh != null).OrderByDescending(r => r.sharedMesh.blendShapeCount).FirstOrDefault();
        ReadShapeValues();
        _exprFxController = null;
        AutoFindFxController();
    }

    private void ReadShapeValues()
    {
        _exprFxStateSel = -1; _exprFxPreviewMode = false; _exprWorkingValues = null;
        _exprShapeValues = null;
        if (_bodySmr != null && _bodySmr.sharedMesh != null)
        {
            _exprShapeValues = new float[_bodySmr.sharedMesh.blendShapeCount];
            for (int i = 0; i < _exprShapeValues.Length; i++) _exprShapeValues[i] = _bodySmr.GetBlendShapeWeight(i);
        }
        previewDirty = true;
    }

    private void DrawExpressionClipSection()
    {
        BeginCard(Tr("Expression Clip", "표정 클립", "表情クリップ"));
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        _exprClip = (AnimationClip)EditorGUILayout.ObjectField(_exprClip, typeof(AnimationClip), false);
        TutorialAnchor("expression-clip");
        if (EditorGUI.EndChangeCheck() && _exprClip != null) _exprNewClipName = _exprClip.name;
        using (new EditorGUI.DisabledScope(_exprClip == null))
            if (GUILayout.Button(Tr("Load", "불러오기", "読み込む"), GUILayout.Width(78), GUILayout.Height(24))) LoadExpressionFromClip();
        if (GUILayout.Button(Tr("New", "새로 만들기", "新規"), GUILayout.Width(88), GUILayout.Height(24)))
        {
            _exprClip = null; _exprNewClipName = "New Expression";
            _exprFxStateSel = -1; _exprFxPreviewMode = false; _exprWorkingValues = null;
            if (_exprShapeValues != null) Array.Clear(_exprShapeValues, 0, _exprShapeValues.Length);
            previewDirty = true;
        }
        EditorGUILayout.EndHorizontal();
        TutorialDraw("expression-clip");
        _exprNewClipName = EditorGUILayout.TextField(Tr("New Clip Name", "새 클립 이름", "新規クリップ名"), _exprNewClipName);
        _includeGestureKeys = EditorGUILayout.Toggle(new GUIContent(Tr("Include Gesture Keys at Zero", "제스처 쉐이프키 0값 포함", "ジェスチャーキーの0値を含める"),
            Tr("Include shape keys used by FX clips for this mesh at zero to prevent overlapping expressions.",
                "이 메시의 FX 클립에서 쓰는 쉐이프키의 0값을 포함해 표정이 겹치는 현상을 방지합니다.",
                "このメッシュのFXクリップで使うシェイプキーの0値を含め、表情の重なりを防ぎます。")), _includeGestureKeys);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!CanOverwriteExpression()))
            if (GUILayout.Button(Tr("Overwrite Clip", "클립 덮어쓰기", "クリップを上書き"), GUILayout.Height(30))) SaveExpressionClip(true);
        if (PrimaryButton(Tr("Save as New Clip", "새 클립으로 저장", "新規クリップとして保存"))) SaveExpressionClip(false);
        EditorGUILayout.EndHorizontal();
        TutorialAnchor("expression-save");
        TutorialDraw("expression-save");
        if (_exprClip != null && !CanOverwriteExpression())
            EditorGUILayout.HelpBox(Tr("Imported or read-only clips can be saved as a new .anim file.", "가져온 클립이나 읽기 전용 클립은 새 .anim 파일로 저장할 수 있습니다.", "インポート済み・読み取り専用クリップは新しい.animファイルとして保存できます。"), MessageType.Info);
        EndCard();
    }

    private void DrawExpressionFxSection()
    {
        BeginCard(Tr("FX Gestures", "FX 제스처", "FXジェスチャー"));
        EditorGUI.BeginChangeCheck();
        _exprFxController = (AnimatorController)EditorGUILayout.ObjectField(Tr("FX Controller", "FX 컨트롤러", "FXコントローラー"), _exprFxController, typeof(AnimatorController), false);
        TutorialAnchor("expression-fx");
        if (EditorGUI.EndChangeCheck()) EndFxPreview();
        TutorialDraw("expression-fx");
        if (_exprFxController == null)
        {
            EditorGUILayout.HelpBox(Tr("Assign an FX Controller to preview gestures and include their zero keys.", "FX Controller를 지정하면 제스처 미리보기와 0값 키 저장을 사용할 수 있습니다.", "FX Controllerを指定するとジェスチャーのプレビューと0値キーの保存が使えます。"), MessageType.Info);
            EndCard(); return;
        }
        string[] layers = GetHandLayerNames(_exprFxController);
        if (layers.Length == 0) layers = _exprFxController.layers.Select(l => l.name).ToArray();
        if (layers.Length == 0)
        {
            EditorGUILayout.HelpBox(Tr("The controller has no animation layers.", "컨트롤러에 애니메이션 레이어가 없습니다.", "コントローラーにアニメーションレイヤーがありません。"), MessageType.Info);
            EndCard(); return;
        }
        _exprFxLayerSel = Mathf.Clamp(_exprFxLayerSel, 0, layers.Length - 1);
        int layer = EditorGUILayout.Popup(Tr("Layer", "레이어", "レイヤー"), _exprFxLayerSel, layers);
        if (layer != _exprFxLayerSel) { EndFxPreview(); _exprFxLayerSel = layer; }
        FxClipEntry[] entries = GetHandLayerClips(_exprFxController, layers[_exprFxLayerSel]);
        if (entries.Length == 0) EditorGUILayout.LabelField(Tr("No animation states in this layer.", "이 레이어에 애니메이션 상태가 없습니다.", "このレイヤーにアニメーションステートがありません。"), EditorStyles.wordWrappedMiniLabel);
        for (int i = 0; i < entries.Length; i++)
        {
            FxClipEntry entry = entries[i];
            EditorGUILayout.BeginHorizontal();
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = _exprFxStateSel == i ? Mint : old;
            using (new EditorGUI.DisabledScope(entry.clip == null))
                if (GUILayout.Button(new GUIContent(entry.label, entry.clip != null ? entry.clip.name : ""), GUILayout.Height(24)))
                {
                    if (_exprFxStateSel == i) EndFxPreview();
                    else
                    {
                        if (!_exprFxPreviewMode) SaveWorkingValues();
                        _exprFxStateSel = i; _exprFxPreviewMode = true; PreviewFxClip(entry.clip);
                    }
                }
            GUI.backgroundColor = old;
            using (new EditorGUI.DisabledScope(!CanWriteFxController()))
                if (GUILayout.Button(Tr("Replace", "교체", "差し替え"), GUILayout.Width(72), GUILayout.Height(24)))
                {
                    // Replacing is an explicit asset edit. Preview itself never assigns avatar references.
                    if (EditorUtility.DisplayDialog(Tr("Replace FX Clip", "FX 클립 교체", "FXクリップを差し替え"),
                        Tr($"Replace '{entry.label}' with the current expression clip? Undo is available.",
                            $"'{entry.label}'을 현재 표정 클립으로 교체할까요? 실행 취소로 되돌릴 수 있습니다.",
                            $"「{entry.label}」を現在の表情クリップに差し替えますか？元に戻す操作が利用できます。"),
                        Tr("Replace", "교체", "差し替え"), Tr("Cancel", "취소", "キャンセル")))
                        ReplaceClipInFxLayer(_exprFxController, layers[_exprFxLayerSel], i);
                }
            EditorGUILayout.EndHorizontal();
            if (entry.clip != null) EditorGUILayout.LabelField(entry.clip.name, EditorStyles.centeredGreyMiniLabel);
        }
        if (_exprFxPreviewMode && GUILayout.Button(Tr("Return to Working Expression", "작업 중인 표정으로 돌아가기", "編集中の表情に戻る"), GUILayout.Height(24))) EndFxPreview();
        EndCard();
    }

    private void DrawExpressionShapeKeys()
    {
        if (_bodySmr == null || _bodySmr.sharedMesh == null) return;
        if (_exprShapeValues == null || _exprShapeValues.Length != _bodySmr.sharedMesh.blendShapeCount) ReadShapeValues();
        BeginCard(Tr($"Shape Keys ({_exprShapeValues.Length})", $"쉐이프키 ({_exprShapeValues.Length}개)", $"シェイプキー ({_exprShapeValues.Length}個)"));
        _exprShapeSearch = EditorGUILayout.TextField(new GUIContent(Tr("Search", "검색", "検索")), _exprShapeSearch, EditorStyles.toolbarSearchField);
        TutorialAnchor("expression-shapes");
        TutorialDraw("expression-shapes");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("Reset All", "전체 초기화", "すべてリセット"), GUILayout.Height(24)))
        {
            EndFxPreview(); Array.Clear(_exprShapeValues, 0, _exprShapeValues.Length); previewDirty = true;
        }
        using (new EditorGUI.DisabledScope(!CanApply()))
            if (GUILayout.Button(Tr("Apply Expression to Scene", "표정을 씬에 적용", "表情をシーンに適用"), GUILayout.Height(24))) ApplyExpression();
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < _exprShapeValues.Length; i++)
        {
            string name = _bodySmr.sharedMesh.GetBlendShapeName(i);
            if (!string.IsNullOrEmpty(_exprShapeSearch) && name.IndexOf(_exprShapeSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(name, _exprShapeValues[i], 0f, 100f);
            if (EditorGUI.EndChangeCheck())
            {
                // Editing a previewed FX expression starts a new working expression.
                _exprFxStateSel = -1; _exprFxPreviewMode = false; _exprWorkingValues = null;
                _exprShapeValues[i] = value; previewDirty = true; Repaint();
            }
        }
        EndCard();
    }

    private void ApplyExpression()
    {
        if (!CanApply() || _bodySmr == null || _bodySmr.sharedMesh == null || _exprShapeValues == null) return;
        Undo.RecordObject(_bodySmr, "Animation Tool Apply Expression");
        for (int i = 0; i < Mathf.Min(_exprShapeValues.Length, _bodySmr.sharedMesh.blendShapeCount); i++) _bodySmr.SetBlendShapeWeight(i, _exprShapeValues[i]);
        MarkChanged(_bodySmr); SceneView.RepaintAll(); previewDirty = true;
        SetStatus(Tr("Applied expression to the scene. Undo is available.", "표정을 씬에 적용했습니다. 실행 취소로 되돌릴 수 있습니다.", "表情をシーンに適用しました。元に戻す操作が利用できます。"));
    }

    private void SaveWorkingValues()
    {
        if (_exprShapeValues == null) ReadShapeValues();
        _exprWorkingValues = _exprShapeValues != null ? (float[])_exprShapeValues.Clone() : null;
    }
    private void RestoreWorkingValues()
    {
        if (_exprWorkingValues != null && _exprShapeValues != null)
            Array.Copy(_exprWorkingValues, _exprShapeValues, Mathf.Min(_exprWorkingValues.Length, _exprShapeValues.Length));
        previewDirty = true; Repaint();
    }
    private void EndFxPreview()
    {
        if (_exprFxPreviewMode) RestoreWorkingValues();
        _exprFxStateSel = -1; _exprFxPreviewMode = false; _exprWorkingValues = null;
    }
    private void PreviewFxClip(AnimationClip clip)
    {
        if (clip == null) return;
        if (_exprWorkingValues != null && _exprShapeValues != null)
            Array.Copy(_exprWorkingValues, _exprShapeValues, Mathf.Min(_exprWorkingValues.Length, _exprShapeValues.Length));
        ReadClipValues(clip);
    }
    private void LoadExpressionFromClip()
    {
        if (_exprClip == null) return;
        EndFxPreview();
        ReadClipValues(_exprClip);
        SetStatus(Tr("Loaded the clip's shape weights. Other shape keys retain their working values.",
            "클립의 쉐이프키 값을 불러왔습니다. 다른 쉐이프키는 작업 중인 값을 유지합니다.",
            "クリップのシェイプキー値を読み込みました。他のキーは編集中の値を保持します。"));
    }
    private void ReadClipValues(AnimationClip clip)
    {
        if (_bodySmr == null || _bodySmr.sharedMesh == null || targetAvatarRoot == null) return;
        if (_exprShapeValues == null || _exprShapeValues.Length != _bodySmr.sharedMesh.blendShapeCount) ReadShapeValues();
        string path = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || binding.path != path || !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;
            int index = _bodySmr.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11));
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (index >= 0 && curve != null) _exprShapeValues[index] = curve.Evaluate(0f);
        }
        previewDirty = true; Repaint();
    }

    private bool CanOverwriteExpression()
    {
        if (_exprClip == null || AssetDatabase.IsSubAsset(_exprClip)) return false;
        string path = AssetDatabase.GetAssetPath(_exprClip);
        return path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) && AssetDatabase.IsOpenForEdit(_exprClip);
    }
    private bool CanWriteFxController()
    {
        if (_exprFxController == null) return false;
        return AssetDatabase.GetAssetPath(_exprFxController).StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsOpenForEdit(_exprFxController);
    }

    private void SaveExpressionClip(bool overwriteExisting = false)
    {
        if (targetAvatarRoot == null || _bodySmr == null || _bodySmr.sharedMesh == null) return;
        if (overwriteExisting && !CanOverwriteExpression()) return;
        if (_exprShapeValues == null) ReadShapeValues();
        string meshPath = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        AnimationClip clip = overwriteExisting ? _exprClip : new AnimationClip();
        string path = overwriteExisting ? AssetDatabase.GetAssetPath(clip) : "";
        if (overwriteExisting) Undo.RecordObject(clip, "Animation Tool Save Expression Clip");
        // Keep transforms, material curves, and other meshes when editing an existing clip.
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            if (binding.type == typeof(SkinnedMeshRenderer) && binding.path == meshPath && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                AnimationUtility.SetEditorCurve(clip, binding, null);
        HashSet<string> zeroKeys = _includeGestureKeys && _exprFxController != null ? CollectGestureShapeKeys(_exprFxController) : new HashSet<string>();
        for (int i = 0; i < _exprShapeValues.Length; i++)
        {
            string name = _bodySmr.sharedMesh.GetBlendShapeName(i);
            float weight = _exprShapeValues[i];
            if (Mathf.Approximately(weight, 0f) && !zeroKeys.Contains(name)) continue;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(meshPath, typeof(SkinnedMeshRenderer), "blendShape." + name), AnimationCurve.Constant(0f, 0f, weight));
        }
        if (!overwriteExisting)
        {
            const string directory = "Assets/Di Ne/Expressions";
            EnsureAssetFolder(directory);
            string name = string.IsNullOrWhiteSpace(_exprNewClipName) ? "New Expression" : _exprNewClipName.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            name = name.Replace('/', '_').Replace('\\', '_');
            path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + name + ".anim");
            AssetDatabase.CreateAsset(clip, path);
            Undo.RegisterCreatedObjectUndo(clip, "Animation Tool Create Expression Clip");
            _exprClip = clip;
        }
        EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
        SetStatus(Tr("Saved expression: ", "표정 저장: ", "表情を保存: ") + path);
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private HashSet<string> CollectGestureShapeKeys(AnimatorController controller)
    {
        var result = new HashSet<string>();
        if (controller == null || _bodySmr == null || targetAvatarRoot == null) return result;
        string path = AnimationUtility.CalculateTransformPath(_bodySmr.transform, targetAvatarRoot.transform);
        var visited = new HashSet<Motion>();
        foreach (AnimatorControllerLayer layer in controller.layers)
            CollectFromStateMachine(layer.stateMachine, result, visited, path);
        return result;
    }
    private static void CollectFromStateMachine(AnimatorStateMachine machine, HashSet<string> keys, HashSet<Motion> visited, string path)
    {
        if (machine == null) return;
        foreach (ChildAnimatorState child in machine.states) CollectShapeKeysFromMotion(child.state.motion, keys, visited, path);
        foreach (ChildAnimatorStateMachine child in machine.stateMachines) CollectFromStateMachine(child.stateMachine, keys, visited, path);
    }
    private static void CollectShapeKeysFromMotion(Motion motion, HashSet<string> keys, HashSet<Motion> visited, string path)
    {
        if (motion == null || !visited.Add(motion)) return;
        if (motion is AnimationClip clip)
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.type == typeof(SkinnedMeshRenderer) && binding.path == path && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) keys.Add(binding.propertyName.Substring(11));
        if (motion is BlendTree tree)
            foreach (ChildMotion child in tree.children) CollectShapeKeysFromMotion(child.motion, keys, visited, path);
    }

    private void AutoFindFxController()
    {
        if (targetAvatarRoot == null) return;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        Type descriptorType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor")).FirstOrDefault(t => t != null);
        Component descriptor = descriptorType != null ? targetAvatarRoot.GetComponent(descriptorType) : null;
        bool customized = descriptor != null && descriptorType.GetField("customizeAnimationLayers", flags)?.GetValue(descriptor) is bool enabled && enabled;
        Array layers = customized ? descriptorType.GetField("baseAnimationLayers", flags)?.GetValue(descriptor) as Array : null;
        if (layers != null)
            foreach (object layer in layers)
            {
                Type type = layer.GetType();
                if (type.GetField("type", flags)?.GetValue(layer)?.ToString() != "FX") continue;
                if (type.GetField("isDefault", flags)?.GetValue(layer) is bool isDefault && isDefault) continue;
                _exprFxController = type.GetField("animatorController", flags)?.GetValue(layer) as AnimatorController;
                if (_exprFxController != null) return;
            }
        foreach (Animator animator in targetAvatarRoot.GetComponentsInChildren<Animator>(true))
        {
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller != null && (animator.gameObject == targetAvatarRoot || GetHandLayerNames(controller).Length > 0))
            { _exprFxController = controller; return; }
        }
    }

    private string[] GetHandLayerNames(AnimatorController controller)
    {
        return controller.layers.Select(l => l.name).Where(n =>
        {
            string normalized = n.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            return normalized.Contains("lefthand") || normalized.Contains("righthand");
        }).ToArray();
    }
    private struct FxClipEntry
    {
        public AnimationClip clip;
        public AnimatorState state;
        public BlendTree tree;
        public int childIndex;
        public string label;
    }
    private FxClipEntry[] GetHandLayerClips(AnimatorController controller, string layerName)
    {
        var entries = new List<FxClipEntry>();
        foreach (AnimatorControllerLayer layer in controller.layers)
            if (layer.name == layerName) CollectFxStates(layer.stateMachine, "", entries);
        return entries.ToArray();
    }
    private static void CollectFxStates(AnimatorStateMachine machine, string prefix, List<FxClipEntry> entries)
    {
        if (machine == null) return;
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.motion is BlendTree tree) CollectFxTree(child.state, tree, prefix + child.state.name, entries, new HashSet<BlendTree>());
            else entries.Add(new FxClipEntry { state = child.state, clip = child.state.motion as AnimationClip, childIndex = -1, label = prefix + child.state.name });
        }
        foreach (ChildAnimatorStateMachine child in machine.stateMachines) CollectFxStates(child.stateMachine, prefix + child.stateMachine.name + "/", entries);
    }
    private static void CollectFxTree(AnimatorState state, BlendTree tree, string prefix, List<FxClipEntry> entries, HashSet<BlendTree> visited)
    {
        if (tree == null || !visited.Add(tree)) return;
        ChildMotion[] children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is BlendTree sub) CollectFxTree(state, sub, prefix + "/" + sub.name, entries, visited);
            else entries.Add(new FxClipEntry { state = state, clip = children[i].motion as AnimationClip, tree = tree, childIndex = i, label = prefix + "/" + (children[i].motion != null ? children[i].motion.name : i.ToString()) });
        }
        visited.Remove(tree);
    }
    private void ReplaceClipInFxLayer(AnimatorController controller, string layerName, int stateIndex)
    {
        if (controller == null || !AssetDatabase.GetAssetPath(controller).StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(controller)) return;
        FxClipEntry[] entries = GetHandLayerClips(controller, layerName);
        if (stateIndex < 0 || stateIndex >= entries.Length) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Animation Tool Replace FX Clip");
        // Store the working values in a new clip so replacements always represent the current editor expression.
        SaveExpressionClip(false);
        if (_exprClip == null) return;
        FxClipEntry entry = entries[stateIndex];
        if (entry.tree != null)
        {
            Undo.RecordObject(entry.tree, "Animation Tool Replace FX Clip");
            ChildMotion[] children = entry.tree.children;
            children[entry.childIndex].motion = _exprClip;
            entry.tree.children = children;
            EditorUtility.SetDirty(entry.tree);
        }
        else
        {
            Undo.RecordObject(entry.state, "Animation Tool Replace FX Clip");
            entry.state.motion = _exprClip;
            EditorUtility.SetDirty(entry.state);
        }
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssetIfDirty(entry.tree != null ? (UnityEngine.Object)entry.tree : entry.state);
        SetStatus(Tr("Replaced FX clip: ", "FX 클립 교체: ", "FXクリップを差し替え: ") + entry.label);
    }
}
