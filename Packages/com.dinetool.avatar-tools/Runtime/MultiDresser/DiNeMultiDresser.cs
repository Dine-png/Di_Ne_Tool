#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class DiNeMultiDresser : MonoBehaviour
{
    [System.Serializable]
    public struct ShapeKeyState { public string name; public float value; public bool everRecorded; }
    [System.Serializable]
    public class ShapeKeyList { public List<ShapeKeyState> shapeKeys = new List<ShapeKeyState>(); }
    [System.Serializable]
    public class ShapeKeyMeshList { public List<ShapeKeyList> meshShapeKeys = new List<ShapeKeyList>(); }
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
        public List<ShapeKeyMeshList> perButtonShapeKeyStates = new List<ShapeKeyMeshList>();

        public void EnsureSize(int size)
        {
            while (targets.Count < size) targets.Add(null);
            while (labels.Count < size) labels.Add("");
            while (icons.Count < size) icons.Add(null);
            while (linkedObjects.Count < size) linkedObjects.Add(new LinkedGroup());
            while (perButtonShapeKeyStates.Count < size) perButtonShapeKeyStates.Add(new ShapeKeyMeshList());
        }

        public void RemoveAt(int index)
        {
            if (index >= targets.Count) return;
            targets.RemoveAt(index);
            labels.RemoveAt(index);
            icons.RemoveAt(index);
            if (index < linkedObjects.Count) linkedObjects.RemoveAt(index);
            if (index < perButtonShapeKeyStates.Count) perButtonShapeKeyStates.RemoveAt(index);
        }
    }

    [SerializeField] public AnimatorController animatorController;
    [SerializeField] public VRCExpressionsMenu expressionsMenu;
    [SerializeField] public Transform rootTransform;
    [SerializeField] public List<GameObject> shapeKeyTargets = new List<GameObject>();
    [SerializeField] public List<DresserLayer> layers = new List<DresserLayer>();
    [SerializeField] private DiNeProfile savedProfile;

    private void Reset()
    {
        TryAutoAssignFXController();
        EditorApplication.delayCall += () => {
            if (this != null) TryRestoreFromProfile();
        };
    }

    public void Generate()
    {
        Debug.Log("🚀 [DiNe] 생성 프로세스 시작...");
        TryAutoAssignFXController();
        SaveProfile();

        // 1. 기존 데이터 말소 (Clean Up)
        DeleteAllGeneratedData();

        // 2. 데이터 재생성
        TryAddExpressionParameters();
        TryCreateAnimationLayers();
        DiNeMultiMenuGenerator.TryCreateExpressionMenu(this);

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

    public void TryAutoAssignFXController()
    {
        var descriptor = GetComponentInParent<VRCAvatarDescriptor>();
        if (descriptor == null) descriptor = GetComponentInChildren<VRCAvatarDescriptor>();

        if (descriptor != null)
        {
            rootTransform = descriptor.transform;

            // FX Controller 자동 할당
            if (descriptor.baseAnimationLayers != null)
            {
                foreach (var layer in descriptor.baseAnimationLayers)
                {
                    if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    {
                        if (layer.animatorController != null)
                            animatorController = (AnimatorController)layer.animatorController;
                        break;
                    }
                }
            }
            if (animatorController == null && descriptor.specialAnimationLayers != null)
            {
                foreach (var layer in descriptor.specialAnimationLayers)
                {
                    if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    {
                        if (layer.animatorController != null)
                            animatorController = (AnimatorController)layer.animatorController;
                        break;
                    }
                }
            }

            // Expression Menu 자동 할당
            if (expressionsMenu == null && descriptor.expressionsMenu != null)
                expressionsMenu = descriptor.expressionsMenu;
        }
    }

    // ──────────────────────────────────────────────
    //  아바타 고유 식별
    // ──────────────────────────────────────────────

    /// <summary>
    /// FX Controller 에셋의 GUID를 반환. 아바타별로 고유한 값.
    /// 이름을 바꿔도 GUID는 변하지 않으므로 안정적인 식별자.
    /// </summary>
    private string GetAvatarGUID()
    {
        if (animatorController == null) return null;
        string path = AssetDatabase.GetAssetPath(animatorController);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.AssetPathToGUID(path);
    }

    private string GetSafeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    // ──────────────────────────────────────────────
    //  프로필 저장
    // ──────────────────────────────────────────────

    private void SaveProfile()
    {
        string folder = "Assets/Di Ne/MultiDresser/Profiles";
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string avatarGUID = GetAvatarGUID();
        string rawName = rootTransform != null ? rootTransform.name : gameObject.name;
        string safeName = GetSafeName(rawName);

        // 기존 프로필이 없으면, GUID로 기존 프로필 먼저 검색
        if (savedProfile == null && !string.IsNullOrEmpty(avatarGUID))
        {
            savedProfile = FindProfileByGUID(avatarGUID);
        }

        if (savedProfile == null)
        {
            savedProfile = ScriptableObject.CreateInstance<DiNeProfile>();
            string path = $"{folder}/Profile_{safeName}.asset";
            // 동일 이름 파일 있으면 덮어쓰지 않고 고유 경로 생성
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(savedProfile, path);
        }

        // 아바타 식별 정보 저장
        savedProfile.avatarControllerGUID = avatarGUID ?? "";
        savedProfile.avatarName = safeName;

        // 레이어 데이터 저장
        savedProfile.savedLayers.Clear();
        foreach (var layer in layers)
        {
            var saved = new DiNeProfile.SavedLayer
            {
                layerName = layer.layerName,
                layerIcon = layer.layerIcon,
                targets = new List<GameObject>(layer.targets),
                labels = new List<string>(layer.labels),
                icons = new List<Texture2D>(layer.icons),
                linkedObjects = new List<LinkedGroup>(layer.linkedObjects),
                perButtonShapeKeyStates = new List<ShapeKeyMeshList>(layer.perButtonShapeKeyStates)
            };
            savedProfile.savedLayers.Add(saved);
        }
        savedProfile.shapeKeyTargets = new List<GameObject>(shapeKeyTargets);

        EditorUtility.SetDirty(savedProfile);
        AssetDatabase.SaveAssets();
    }

    // ──────────────────────────────────────────────
    //  프로필 복원
    // ──────────────────────────────────────────────

    public void TryRestoreFromProfile()
    {
        // 1순위: 이미 연결된 프로필이 있으면 그대로 사용
        if (savedProfile != null)
        {
            RestoreInternal(savedProfile);
            return;
        }

        // 2순위: FX Controller GUID로 검색
        string avatarGUID = GetAvatarGUID();
        if (!string.IsNullOrEmpty(avatarGUID))
        {
            var match = FindProfileByGUID(avatarGUID);
            if (match != null)
            {
                savedProfile = match;
                Debug.Log($"♻ [DiNe] GUID 기반 설정 복구 완료! ({match.name})");
                RestoreInternal(match);
                return;
            }
        }

        // 3순위: 아바타 이름으로 fallback 검색
        DiNeProfile nameMatch = FindProfileByName();
        if (nameMatch != null)
        {
            savedProfile = nameMatch;
            // GUID가 있으면 프로필에 업데이트 (다음부터는 GUID로 매칭됨)
            if (!string.IsNullOrEmpty(avatarGUID))
            {
                nameMatch.avatarControllerGUID = avatarGUID;
                EditorUtility.SetDirty(nameMatch);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"♻ [DiNe] 이름 기반 설정 복구 완료! ({nameMatch.name})");
            RestoreInternal(nameMatch);
        }
    }

    /// <summary>
    /// GUID로 프로필 검색 (가장 정확한 매칭)
    /// </summary>
    private DiNeProfile FindProfileByGUID(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;

        string folder = "Assets/Di Ne/MultiDresser/Profiles";
        if (!Directory.Exists(folder)) return null;

        string[] assetGuids = AssetDatabase.FindAssets("t:DiNeProfile", new[] { folder });
        foreach (string ag in assetGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(ag);
            DiNeProfile p = AssetDatabase.LoadAssetAtPath<DiNeProfile>(path);
            if (p != null && p.avatarControllerGUID == guid)
                return p;
        }
        return null;
    }

    /// <summary>
    /// 아바타 이름으로 프로필 검색 (fallback)
    /// </summary>
    private DiNeProfile FindProfileByName()
    {
        string folder = "Assets/Di Ne/MultiDresser/Profiles";
        if (!Directory.Exists(folder)) return null;

        string[] guids = AssetDatabase.FindAssets("t:DiNeProfile", new[] { folder });
        List<DiNeProfile> candidates = new List<DiNeProfile>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DiNeProfile p = AssetDatabase.LoadAssetAtPath<DiNeProfile>(path);
            if (p != null) candidates.Add(p);
        }

        // rootTransform 이름으로 매칭
        if (rootTransform != null)
        {
            string safeName = GetSafeName(rootTransform.name);
            var match = candidates.FirstOrDefault(p => p.avatarName == safeName);
            if (match != null) return match;

            // avatarName 필드가 비어있는 기존 프로필은 파일명으로 매칭 (하위호환)
            match = candidates
                .Where(p => string.IsNullOrEmpty(p.avatarName) && p.name.Contains(safeName))
                .OrderByDescending(p => p.name)
                .FirstOrDefault();
            if (match != null) return match;
        }

        // gameObject 이름으로 최후 시도
        string safeObjName = GetSafeName(gameObject.name);
        var objMatch = candidates.FirstOrDefault(p => p.avatarName == safeObjName);
        if (objMatch != null) return objMatch;

        objMatch = candidates
            .Where(p => string.IsNullOrEmpty(p.avatarName) && p.name.Contains(safeObjName))
            .OrderByDescending(p => p.name)
            .FirstOrDefault();
        return objMatch;
    }

    /// <summary>
    /// 프로필 데이터를 실제 레이어에 복원. Null 참조 정리 포함.
    /// </summary>
    private void RestoreInternal(DiNeProfile profile)
    {
        if (profile == null) return;

        layers.Clear();
        foreach (var saved in profile.savedLayers)
        {
            var layer = new DresserLayer
            {
                layerName = saved.layerName,
                layerIcon = saved.layerIcon,
                targets = new List<GameObject>(saved.targets),
                labels = new List<string>(saved.labels),
                icons = new List<Texture2D>(saved.icons),
                linkedObjects = new List<LinkedGroup>(saved.linkedObjects),
                perButtonShapeKeyStates = new List<ShapeKeyMeshList>(saved.perButtonShapeKeyStates)
            };

            // Null 참조 검증: 삭제된 오브젝트가 있으면 경고
            int nullCount = layer.targets.Count(t => t == null);
            // 인덱스 0 (기본 상태)는 null일 수 있으므로 제외
            int realNulls = 0;
            for (int i = 1; i < layer.targets.Count; i++)
            {
                if (layer.targets[i] == null) realNulls++;
            }
            if (realNulls > 0)
            {
                Debug.LogWarning($"⚠ [DiNe] 레이어 '{layer.layerName}'에서 {realNulls}개의 오브젝트 참조가 유실되었습니다. Inspector에서 다시 지정해주세요.");
            }

            // LinkedObjects 내부 null 정리
            foreach (var linked in layer.linkedObjects)
            {
                if (linked?.objects != null)
                    linked.objects.RemoveAll(o => o == null);
            }

            // icons 리스트 크기 맞추기
            while (layer.icons.Count < layer.targets.Count) layer.icons.Add(null);

            layers.Add(layer);
        }

        // shapeKeyTargets에서 null 제거 (인덱스 유지가 중요하므로 null 유지하되 경고)
        shapeKeyTargets = new List<GameObject>(profile.shapeKeyTargets);
        int nullMeshes = shapeKeyTargets.Count(t => t == null);
        if (nullMeshes > 0 && shapeKeyTargets.Count > 0)
        {
            Debug.LogWarning($"⚠ [DiNe] 셰이프키 타겟 중 {nullMeshes}개의 메시 참조가 유실되었습니다.");
        }
    }

    // ──────────────────────────────────────────────
    //  VRC 파라미터 생성
    // ──────────────────────────────────────────────

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

    private void TryCreateAnimationLayers()
    {
        if (rootTransform == null || animatorController == null)
        {
            Debug.LogError("❌ RootTransform 또는 AnimatorController가 비어있습니다!");
            return;
        }

        string baseFolder = "Assets/Di Ne/MultiDresser/Animations";
        if (!Directory.Exists(baseFolder)) { Directory.CreateDirectory(baseFolder); AssetDatabase.Refresh(); }

        var controllerLayers = animatorController.layers;

        foreach (var layerData in layers)
        {
            if (layerData.targets == null || layerData.targets.Count <= 1) continue;

            string safeLayerName = string.IsNullOrEmpty(layerData.layerName) ? "Layer" : layerData.layerName;
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";
            string animLayerName = $"DiNe {safeLayerName}";
            string layerFolder = $"{baseFolder}/{safeLayerName}";

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

            sm.states = new ChildAnimatorState[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];
            sm.entryTransitions = new AnimatorTransition[0];

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

        for (int i = 0; i < layerData.targets.Count; i++)
        {
            AnimationClip clip = new AnimationClip();
            string clipName = $"Changer_{layerData.layerName}_{i}_{layerData.targets[i]?.name ?? "Null"}";
            string clipPath = $"{folderPath}/{clipName}.anim";

            // (1) Main Targets
            for (int j = 0; j < layerData.targets.Count; j++)
            {
                var t = layerData.targets[j];
                if (t == null) continue;
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t.transform, rootTransform), typeof(GameObject), "m_IsActive"),
                    new AnimationCurve(new Keyframe(0, i == j ? 1 : 0)));
            }

            // (2) Linked Objects
            for (int j = 0; j < layerData.linkedObjects.Count; j++)
            {
                if (layerData.linkedObjects[j] == null) continue;
                foreach (var linkObj in layerData.linkedObjects[j].objects)
                {
                    if (linkObj == null) continue;
                    float targetVal = (i == j) ? 1f : 0f;
                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(linkObj.transform, rootTransform), typeof(GameObject), "m_IsActive"),
                        new AnimationCurve(new Keyframe(0, targetVal)));
                }
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
