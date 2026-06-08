#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[AddComponentMenu("DiNe/Multi Dresser")]
public class DiNeMultiDresser : MonoBehaviour
{
    [System.Serializable]
    public struct ShapeKeyState { public string name; public float value; public bool everRecorded; }
    [System.Serializable]
    public class ShapeKeyList { public List<ShapeKeyState> shapeKeys = new List<ShapeKeyState>(); }
    [System.Serializable]
    public class ShapeKeyMeshList { public List<ShapeKeyList> meshShapeKeys = new List<ShapeKeyList>(); }
    [System.Serializable]
    public class MaterialSwapEntry { public Renderer renderer; public List<Material> materials = new List<Material>(); }
    [System.Serializable]
    public class MaterialSwapList   { public List<MaterialSwapEntry> entries = new List<MaterialSwapEntry>(); }
    [System.Serializable]
    public class LinkedGroup { public List<GameObject> objects = new List<GameObject>(); }

    [System.Serializable]
    public class DresserLayer
    {
        public string layerName = "New Layer";
        public Texture2D layerIcon;
        public List<GameObject> targets = new List<GameObject>();
        public List<string> labels = new List<string>();
        public List<Texture2D> icons = new List<Texture2D>();
        public List<LinkedGroup> linkedObjects = new List<LinkedGroup>();
        public List<ShapeKeyMeshList>  perButtonShapeKeyStates  = new List<ShapeKeyMeshList>();
        public List<MaterialSwapList>  perButtonMaterialSwaps   = new List<MaterialSwapList>();
        public GameObject              particleObject;

        public void EnsureSize(int size)
        {
            while (targets.Count < size)                targets.Add(null);
            while (labels.Count < size)                 labels.Add("");
            while (icons.Count < size)                  icons.Add(null);
            while (linkedObjects.Count < size)          linkedObjects.Add(new LinkedGroup());
            while (perButtonShapeKeyStates.Count < size) perButtonShapeKeyStates.Add(new ShapeKeyMeshList());
            while (perButtonMaterialSwaps.Count < size)  perButtonMaterialSwaps.Add(new MaterialSwapList());
        }

        public void RemoveAt(int index)
        {
            if (index >= targets.Count) return;
            targets.RemoveAt(index);
            labels.RemoveAt(index);
            icons.RemoveAt(index);
            if (index < linkedObjects.Count)           linkedObjects.RemoveAt(index);
            if (index < perButtonShapeKeyStates.Count) perButtonShapeKeyStates.RemoveAt(index);
            if (index < perButtonMaterialSwaps.Count)  perButtonMaterialSwaps.RemoveAt(index);
        }
    }

    [SerializeField] public AnimatorController animatorController;
    [SerializeField] public VRCExpressionsMenu expressionsMenu;
    [SerializeField] public Transform rootTransform;
    [SerializeField] public List<GameObject> shapeKeyTargets = new List<GameObject>();
    [SerializeField] public List<DresserLayer> layers = new List<DresserLayer>();

    private static Material[] CloneMaterials(Material[] materials)
    {
        return materials != null ? (Material[])materials.Clone() : new Material[0];
    }

    private static Material[] BuildMaterialSwap(Material[] baseMaterials, MaterialSwapEntry entry)
    {
        var result = CloneMaterials(baseMaterials);
        if (entry == null || entry.materials == null) return result;

        for (int i = 0; i < entry.materials.Count && i < result.Length; i++)
        {
            if (entry.materials[i] != null)
                result[i] = entry.materials[i];
        }

        return result;
    }

    private static MaterialSwapEntry FindMaterialSwapEntry(DresserLayer layerData, int buttonIdx, Renderer renderer)
    {
        if (layerData == null || renderer == null) return null;
        if (buttonIdx < 0 || buttonIdx >= layerData.perButtonMaterialSwaps.Count) return null;

        var swapList = layerData.perButtonMaterialSwaps[buttonIdx];
        if (swapList == null || swapList.entries == null) return null;

        return swapList.entries.Find(entry => entry != null && entry.renderer == renderer);
    }

    private static Material[] BuildDefaultMaterials(DresserLayer layerData, Renderer renderer)
    {
        var baseMaterials = CloneMaterials(renderer != null ? renderer.sharedMaterials : null);
        return BuildMaterialSwap(baseMaterials, FindMaterialSwapEntry(layerData, 0, renderer));
    }

    private void Reset()
    {
        TryAutoAssignFXController();
    }

    public void Generate(
        string generatedRootFolder = "Assets/Di Ne/MultiDresser",
        bool clearExistingGeneratedData = true,
        bool mergeIntoExistingMenu = false)
    {
        Debug.Log("🚀 [DiNe] 생성 프로세스 시작...");
        TryAutoAssignFXController();

        // 1. 기존 데이터 말소 (Clean Up)
        if (clearExistingGeneratedData)
            DeleteAllGeneratedData();

        // 2. 데이터 재생성
        TryAddExpressionParameters();
        TryCreateAnimationLayers(generatedRootFolder);         // 내부에서 AssetDatabase.Refresh() 호출됨
        DiNeMultiMenuGenerator.TryCreateExpressionMenu(this, generatedRootFolder);

        // 3. Refresh() 이후에 다시 실행해야 씬 오브젝트 참조가 재임포트로 null이 되는 것을 방지
        TryAddExpressionParameters();
        DiNeMultiMenuGenerator.TryCreateExpressionMenu(this, generatedRootFolder, mergeIntoExistingMenu);


        Debug.Log("✨ [DiNe] 모든 작업 완료! (기존 데이터 삭제 후 재생성됨)");
    }

    public void DeleteAllGeneratedData()
    {
        if (animatorController != null)
        {
            var acLayers = animatorController.layers.ToList();
            bool layerRemoved = false;
            for (int i = acLayers.Count - 1; i >= 0; i--)
            {
                if (acLayers[i].name.StartsWith("DiNe"))
                {
                    animatorController.RemoveLayer(i);
                    layerRemoved = true;
                }
            }
            if (layerRemoved) Debug.Log("🧹 [Clean] 기존 FX 레이어 삭제 완료");

            var acParams = animatorController.parameters.ToList();
            bool paramRemoved = false;
            for (int i = acParams.Count - 1; i >= 0; i--)
            {
                if (acParams[i].name.StartsWith("DiNe"))
                {
                    animatorController.RemoveParameter(acParams[i]);
                    paramRemoved = true;
                }
            }
            if (paramRemoved) Debug.Log("🧹 [Clean] 기존 FX 파라미터 삭제 완료");
        }

        if (rootTransform != null)
        {
            var descriptor = rootTransform.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null && descriptor.expressionParameters != null)
            {
                var vrcParams = descriptor.expressionParameters.parameters.ToList();
                int removedCount = vrcParams.RemoveAll(p => p.name.StartsWith("DiNe"));

                if (removedCount > 0)
                {
                    descriptor.expressionParameters.parameters = vrcParams.ToArray();
                    EditorUtility.SetDirty(descriptor.expressionParameters);
                    Debug.Log($"🧹 [Clean] VRC 파라미터 {removedCount}개 삭제 완료");
                }
            }

            if (descriptor != null && descriptor.expressionsMenu != null)
            {
                var controls = descriptor.expressionsMenu.controls;
                var target = controls.Find(c => c.name == "Multi Dresser");
                if (target != null)
                {
                    controls.Remove(target);
                    EditorUtility.SetDirty(descriptor.expressionsMenu);
                    Debug.Log("🧹 [Clean] 메인 메뉴 연결 해제 완료");
                }
            }
        }

        AssetDatabase.SaveAssets();
    }

    public void ClearAllData(bool clearGeneratedData = true)
    {
        if (clearGeneratedData)
            DeleteAllGeneratedData();

        layers.Clear();
        shapeKeyTargets.Clear();

        EditorUtility.SetDirty(this);
    }

    public bool HasConfiguredContent()
    {
        if (shapeKeyTargets != null && shapeKeyTargets.Count > 0)
            return true;

        if (layers == null)
            return false;

        foreach (var layer in layers)
        {
            if (layer == null) continue;
            if (layer.targets != null && layer.targets.Count > 0)
                return true;
            if (layer.particleObject != null)
                return true;
        }

        return false;
    }

    // 빌드 시 임시 데이터가 저장되는 폴더. 이 경로의 에셋이 아바타/드레서에 남아있으면
    // 이전 빌드의 복원이 실패한 것이므로 경고 대상이다.
    private const string TempAssetFolderHint = "/MultiDresser/__Temp";

    /// <summary>이 드레서가 속한 아바타의 VRCAvatarDescriptor를 찾는다.</summary>
    public VRCAvatarDescriptor GetAvatarDescriptor()
    {
        var descriptor = GetComponentInParent<VRCAvatarDescriptor>();
        if (descriptor == null) descriptor = GetComponentInChildren<VRCAvatarDescriptor>();
        return descriptor;
    }

    /// <summary>아바타 디스크립터의 FX 레이어에 실제로 할당된 컨트롤러를 반환한다(없으면 null).</summary>
    public static RuntimeAnimatorController GetAvatarFxController(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null) return null;

        if (descriptor.baseAnimationLayers != null)
        {
            foreach (var layer in descriptor.baseAnimationLayers)
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer.animatorController;
        }
        if (descriptor.specialAnimationLayers != null)
        {
            foreach (var layer in descriptor.specialAnimationLayers)
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer.animatorController;
        }
        return null;
    }

    // 비어있는 슬롯만 보충하는 기존 동작(빌드/리셋 내부에서 사용).
    public void TryAutoAssignFXController()
    {
        var descriptor = GetAvatarDescriptor();
        if (descriptor == null) return;

        rootTransform = descriptor.transform;

        if (animatorController == null)
        {
            var fx = GetAvatarFxController(descriptor) as AnimatorController;
            if (fx != null) animatorController = fx;
        }

        if (expressionsMenu == null && descriptor.expressionsMenu != null)
            expressionsMenu = descriptor.expressionsMenu;
    }

    /// <summary>
    /// 새로고침(↺) 버튼용: 루트/FX/메뉴를 아바타의 현재 값으로 강제로 다시 가져오고,
    /// 누락된 Expression 파라미터를 보충한다. stale/다른 아바타의 FX·메뉴를 덮어쓴다.
    /// </summary>
    public void ReassignFromAvatar()
    {
        var descriptor = GetAvatarDescriptor();
        if (descriptor == null)
        {
            Debug.LogWarning("[DiNe] Multi Dresser: 아바타(VRCAvatarDescriptor)를 찾지 못해 재배정을 건너뜁니다.");
            return;
        }

        rootTransform = descriptor.transform;

        var fx = GetAvatarFxController(descriptor) as AnimatorController;
        if (fx != null) animatorController = fx;   // 강제 재배정

        if (descriptor.expressionsMenu != null)
            expressionsMenu = descriptor.expressionsMenu;   // 강제 재배정

        // 파라미터(아바타 Expression Parameters)에 누락분 보충
        TryAddExpressionParameters();

        EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// 현재 할당된 FX/메뉴가 아바타 내부의 것과 일치하는지 검사한다.
    /// 불일치하면 false와 사용자용 메시지를 반환한다. (인스펙터에서 수시로 호출)
    /// </summary>
    public bool ValidateAssignment(out string message)
    {
        message = null;

        var descriptor = GetAvatarDescriptor();
        if (descriptor == null)
        {
            message = "아바타(VRCAvatarDescriptor)를 찾을 수 없습니다. 멀티 드레서가 아바타 하위에 있는지 확인하세요.";
            return false;
        }

        var avatarFx = GetAvatarFxController(descriptor) as AnimatorController;
        if (avatarFx == null)
        {
            message = "아바타의 FX 레이어에 Animator Controller가 비어 있습니다. 아바타에 FX 컨트롤러를 먼저 설정하세요.";
            return false;
        }

        if (animatorController == null)
        {
            message = "할당된 FX Controller가 없습니다. 새로고침(↺) 버튼을 눌러 아바타의 FX를 다시 가져오세요.";
            return false;
        }

        if (animatorController != avatarFx)
        {
            message = "할당된 FX Controller가 아바타 내부의 FX와 다릅니다.\n이대로 업로드하면 잘못된 FX를 덮어쓸 수 있으니, 새로고침(↺) 버튼을 눌러 재배정하세요.";
            return false;
        }

        // 이전 빌드의 임시 FX가 복원되지 않고 남아있는 경우
        string fxPath = AssetDatabase.GetAssetPath(animatorController);
        if (!string.IsNullOrEmpty(fxPath) && fxPath.Replace('\\', '/').Contains(TempAssetFolderHint))
        {
            message = "임시 빌드용 FX가 아바타에 남아 있습니다(복원 실패).\n원본 FX로 교체한 뒤 새로고침(↺) 버튼을 눌러 재배정하세요.";
            return false;
        }

        if (descriptor.expressionsMenu != null && expressionsMenu != null &&
            expressionsMenu != descriptor.expressionsMenu)
        {
            message = "할당된 Expression Menu가 아바타 내부의 메뉴와 다릅니다.\n새로고침(↺) 버튼을 눌러 재배정하세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 파일명으로 안전한 문자열로 변환 (경로에 쓸 수 없는 문자 치환).
    /// </summary>
    private string GetSafeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static string GetSafeAnimatorName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "State";

        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '(' || c == ')' || c == '[' || c == ']')
                continue;

            chars[i] = '_';
        }

        var safeName = new string(chars).Trim();
        return string.IsNullOrEmpty(safeName) ? "State" : safeName;
    }

    private static void ClearAnimatorStateMachine(AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null)
            return;

        stateMachine.defaultState = null;

        foreach (var transition in stateMachine.entryTransitions.ToArray())
        {
            if (transition != null)
                stateMachine.RemoveEntryTransition(transition);
        }

        foreach (var transition in stateMachine.anyStateTransitions.ToArray())
        {
            if (transition != null)
                stateMachine.RemoveAnyStateTransition(transition);
        }

        foreach (var childState in stateMachine.states.ToArray())
        {
            if (childState.state != null)
                stateMachine.RemoveState(childState.state);
        }
    }


    public void TryAddExpressionParameters()
    {
        if (rootTransform == null) return;
        var descriptor = rootTransform.GetComponent<VRCAvatarDescriptor>();
        if (descriptor == null || descriptor.expressionParameters == null) return;

        var list = new List<VRCExpressionParameters.Parameter>(descriptor.expressionParameters.parameters);
        bool modified = false;

        foreach (var layer in layers)
        {
            string safeLayerName = string.IsNullOrEmpty(layer.layerName) ? "Layer" : layer.layerName;
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";

            if (!list.Exists(p => p.name == paramName))
            {
                list.Add(new VRCExpressionParameters.Parameter { name = paramName, valueType = VRCExpressionParameters.ValueType.Int, saved = true });
                modified = true;
            }
        }

        if (modified)
        {
            descriptor.expressionParameters.parameters = list.ToArray();
            EditorUtility.SetDirty(descriptor.expressionParameters);
            AssetDatabase.SaveAssets();
        }
    }

    // ──────────────────────────────────────────────
    //  애니메이션 레이어 생성
    // ──────────────────────────────────────────────

    private void TryCreateAnimationLayers(string generatedRootFolder)
    {
        if (rootTransform == null || animatorController == null)
        {
            Debug.LogError("❌ RootTransform 또는 AnimatorController가 비어있습니다!");
            return;
        }

        string baseFolder = $"{generatedRootFolder}/Animations";
        if (!Directory.Exists(baseFolder)) { Directory.CreateDirectory(baseFolder); AssetDatabase.Refresh(); }

        var controllerLayers = animatorController.layers;
        var materialOwners = new Dictionary<Renderer, List<string>>();

        foreach (var layerData in layers)
        {
            if (layerData?.perButtonMaterialSwaps == null) continue;

            foreach (var swapList in layerData.perButtonMaterialSwaps)
            {
                if (swapList?.entries == null) continue;

                foreach (var entry in swapList.entries)
                {
                    if (entry?.renderer == null) continue;

                    if (!materialOwners.TryGetValue(entry.renderer, out var ownerLayers))
                    {
                        ownerLayers = new List<string>();
                        materialOwners.Add(entry.renderer, ownerLayers);
                    }

                    string ownerName = string.IsNullOrEmpty(layerData.layerName) ? "Layer" : layerData.layerName;
                    if (!ownerLayers.Contains(ownerName))
                        ownerLayers.Add(ownerName);
                }
            }
        }

        foreach (var pair in materialOwners)
        {
            if (pair.Value.Count > 1)
            {
                Debug.LogWarning($"[DiNe] Renderer '{pair.Key.name}' material swap is controlled by multiple layers: {string.Join(", ", pair.Value)}");
            }
        }

        foreach (var layerData in layers)
        {
            if (layerData.targets == null || layerData.targets.Count <= 1) continue;

            string safeLayerName = string.IsNullOrEmpty(layerData.layerName) ? "Layer" : layerData.layerName;
            string safeAnimatorLayerName = GetSafeAnimatorName(safeLayerName);
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";
            string animLayerName = $"DiNe {safeAnimatorLayerName}";
            string layerFolder = $"{baseFolder}/{GetSafeName(safeLayerName)}";

            if (!Directory.Exists(layerFolder)) Directory.CreateDirectory(layerFolder);

            bool paramExists = false;
            foreach(var p in animatorController.parameters) if(p.name == paramName) paramExists = true;
            if(!paramExists) animatorController.AddParameter(paramName, AnimatorControllerParameterType.Int);

            int layerIndex = -1;
            for(int i=0; i<controllerLayers.Length; i++) {
                if(controllerLayers[i].name == animLayerName) {
                    layerIndex = i;
                    break;
                }
            }

            if (layerIndex == -1)
            {
                animatorController.AddLayer(animLayerName);
                controllerLayers = animatorController.layers;
                layerIndex = controllerLayers.Length - 1;
            }

            controllerLayers[layerIndex].defaultWeight = 1f;

            var sm = controllerLayers[layerIndex].stateMachine;
            if (sm == null)
            {
                sm = new AnimatorStateMachine { name = animLayerName + "_SM" };
                AssetDatabase.AddObjectToAsset(sm, animatorController);
                controllerLayers[layerIndex].stateMachine = sm;
            }

            ClearAnimatorStateMachine(sm);

            CreateStatesForLayer(layerData, sm, layerFolder, paramName);
        }

        animatorController.layers = controllerLayers;

        // 레이어 웨이트 강제 고정
        var finalLayers = animatorController.layers;
        bool needDoubleSave = false;

        for (int i = 0; i < finalLayers.Length; i++)
        {
            if (finalLayers[i].name.StartsWith("DiNe"))
            {
                if (finalLayers[i].defaultWeight != 1f)
                {
                    finalLayers[i].defaultWeight = 1f;
                    needDoubleSave = true;
                }
            }
        }

        if (needDoubleSave)
        {
            animatorController.layers = finalLayers;
            EditorUtility.SetDirty(animatorController);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void CreateStatesForLayer(DresserLayer layerData, AnimatorStateMachine sm, string folderPath, string paramName)
    {
        AnimatorState[] states = new AnimatorState[layerData.targets.Count];

        List<HashSet<string>> layerActiveKeys = new List<HashSet<string>>();
        for (int m = 0; m < shapeKeyTargets.Count; m++) layerActiveKeys.Add(new HashSet<string>());

        foreach (var btnState in layerData.perButtonShapeKeyStates)
        {
            for (int m = 0; m < shapeKeyTargets.Count; m++)
            {
                if (m >= btnState.meshShapeKeys.Count) continue;
                foreach (var sk in btnState.meshShapeKeys[m].shapeKeys)
                {
                    if (sk.everRecorded) layerActiveKeys[m].Add(sk.name);
                }
            }
        }

        // 마테리얼 교체: 사용된 모든 Renderer 수집 + 기본 마테리얼 결정
        var allSwapRenderers = new List<Renderer>();
        var rendSet = new HashSet<Renderer>();
        foreach (var swapList in layerData.perButtonMaterialSwaps)
            foreach (var entry in swapList.entries)
                if (entry.renderer != null && rendSet.Add(entry.renderer))
                    allSwapRenderers.Add(entry.renderer);

        var defaultMaterials = new Dictionary<Renderer, Material[]>();
        foreach (var rend in allSwapRenderers)
            defaultMaterials[rend] = BuildDefaultMaterials(layerData, rend);

        for (int i = 0; i < layerData.targets.Count; i++)
        {
            AnimationClip clip = new AnimationClip();
            string rawStateName = $"Changer_{layerData.layerName}_{i}_{layerData.targets[i]?.name ?? "Null"}";
            string clipName = GetSafeAnimatorName(rawStateName);
            string clipFileName = GetSafeName(rawStateName);
            string clipPath = $"{folderPath}/{clipFileName}.anim";

            // (1) Main Targets
            for (int j = 0; j < layerData.targets.Count; j++)
            {
                var t = layerData.targets[j];
                if (t == null) continue;
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t.transform, rootTransform), typeof(GameObject), "m_IsActive"),
                    new AnimationCurve(new Keyframe(0, i == j ? 1 : 0)));
            }

            // (2) Linked Objects — collect all linked objects across all buttons,
            //     then determine on/off: if the object is linked to the CURRENT state (i), it's ON.
            var linkedMap = new Dictionary<GameObject, float>();
            for (int j = 0; j < layerData.linkedObjects.Count; j++)
            {
                if (layerData.linkedObjects[j] == null) continue;
                foreach (var linkObj in layerData.linkedObjects[j].objects)
                {
                    if (linkObj == null) continue;
                    if (i == j)
                        linkedMap[linkObj] = 1f;              // this button is active → ON
                    else if (!linkedMap.ContainsKey(linkObj))
                        linkedMap[linkObj] = 0f;              // not yet set → OFF (may be overridden later)
                }
            }
            foreach (var kvp in linkedMap)
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(kvp.Key.transform, rootTransform), typeof(GameObject), "m_IsActive"),
                    new AnimationCurve(new Keyframe(0, kvp.Value)));
            }

            // (3) ShapeKeys
            for (int m = 0; m < shapeKeyTargets.Count; m++)
            {
                var meshObj = shapeKeyTargets[m];
                if (meshObj == null) continue;

                // 메시에 실제로 존재하는 셰이프키만 처리
                var smr = meshObj.GetComponent<SkinnedMeshRenderer>();
                HashSet<string> existingKeys = new HashSet<string>();
                if (smr != null && smr.sharedMesh != null)
                {
                    for (int sk = 0; sk < smr.sharedMesh.blendShapeCount; sk++)
                        existingKeys.Add(smr.sharedMesh.GetBlendShapeName(sk));
                }

                foreach (var skName in layerActiveKeys[m])
                {
                    // 실제 메시에 해당 셰이프키가 있는지 확인
                    if (!existingKeys.Contains(skName)) continue;

                    float targetValue = 0f;

                    if (i < layerData.perButtonShapeKeyStates.Count &&
                        m < layerData.perButtonShapeKeyStates[i].meshShapeKeys.Count)
                    {
                        var currentKeys = layerData.perButtonShapeKeyStates[i].meshShapeKeys[m].shapeKeys;
                        var foundKey = currentKeys.Find(k => k.name == skName);

                        if (foundKey.name != null && foundKey.everRecorded)
                        {
                            targetValue = foundKey.value;
                        }
                    }

                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(meshObj.transform, rootTransform), typeof(SkinnedMeshRenderer), "blendShape." + skName),
                        new AnimationCurve(new Keyframe(0, targetValue)));
                }
            }

            // (4) Material Swaps
            foreach (var rend in allSwapRenderers)
            {
                var defaultEntry = FindMaterialSwapEntry(layerData, 0, rend);
                var currentEntry = FindMaterialSwapEntry(layerData, i, rend);

                // Only animate this renderer when the current state explicitly owns it,
                // or when state 0 defines the layer-wide default for it.
                if (currentEntry == null && defaultEntry == null)
                    continue;

                var mats = defaultMaterials.ContainsKey(rend)
                    ? CloneMaterials(defaultMaterials[rend])
                    : CloneMaterials(rend.sharedMaterials);

                if (i == 0)
                {
                    mats = BuildMaterialSwap(CloneMaterials(rend.sharedMaterials), defaultEntry);
                }
                else if (currentEntry != null && currentEntry.materials.Count > 0)
                {
                    mats = BuildMaterialSwap(mats, currentEntry);
                }
                string rPath = AnimationUtility.CalculateTransformPath(rend.transform, rootTransform);
                System.Type rType = rend.GetType();
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    if (mats[mi] == null) continue;
                    AnimationUtility.SetObjectReferenceCurve(clip,
                        EditorCurveBinding.PPtrCurve(rPath, rType, $"m_Materials.Array.data[{mi}]"),
                        new ObjectReferenceKeyframe[]
                        {
                            new ObjectReferenceKeyframe { time = 0f, value = mats[mi] },
                            new ObjectReferenceKeyframe { time = 1f, value = mats[mi] }
                        });
                }
            }

            // (5) Particle (0s=OFF → 0.01s=ON)
            if (layerData.particleObject != null)
            {
                string pPath = AnimationUtility.CalculateTransformPath(layerData.particleObject.transform, rootTransform);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(pPath, typeof(GameObject), "m_IsActive"),
                    new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.01f, 1f)));
            }

            AssetDatabase.CreateAsset(clip, clipPath);

            var state = sm.AddState(clipName);
            state.motion = clip;
            states[i] = state;
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i] == null) continue;
            var tr = sm.AddAnyStateTransition(states[i]);
            tr.duration = 0;
            tr.hasExitTime = false;
            tr.canTransitionToSelf = false;
            tr.conditions = new AnimatorCondition[] {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = paramName, threshold = i }
            };
        }
    }
}
#endif
