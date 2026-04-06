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

    // ✅ [신규 기능] 기존 DiNe 관련 데이터를 깔끔하게 지우는 함수
    public void DeleteAllGeneratedData()
    {
        if (animatorController != null)
        {
            // 1. FX 레이어 삭제 (뒤에서부터 삭제해야 인덱스 오류 안 남)
            var acLayers = animatorController.layers.ToList();
            bool layerRemoved = false;
            for (int i = acLayers.Count - 1; i >= 0; i--)
            {
                // "DiNe" 로 시작하는 레이어는 전부 우리 것
                if (acLayers[i].name.StartsWith("DiNe"))
                {
                    animatorController.RemoveLayer(i);
                    layerRemoved = true;
                }
            }
            if (layerRemoved) Debug.Log("🧹 [Clean] 기존 FX 레이어 삭제 완료");

            // 2. FX 파라미터 삭제
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

        // 3. VRC 파라미터 삭제
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
            
            // 4. 메인 메뉴에서 연결 끊기 (MenuGenerator가 다시 연결함)
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
        }
    }

    private string GetSafeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private void SaveProfile()
    {
        string folder = "Assets/Di Ne/MultiDresser/Profiles";
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string rawName = rootTransform != null ? rootTransform.name : gameObject.name;
        string safeName = GetSafeName(rawName);

        if (savedProfile == null)
        {
            savedProfile = ScriptableObject.CreateInstance<DiNeProfile>();
            string path = $"{folder}/Profile_{safeName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.asset";
            AssetDatabase.CreateAsset(savedProfile, path);
        }

        savedProfile.savedLayers.Clear();
        foreach(var layer in layers)
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
        AssetDatabase.Refresh(); 
    }

    public void TryRestoreFromProfile()
    {
        if (savedProfile != null) 
        {
            RestoreInternal(savedProfile);
            return;
        }

        string folder = "Assets/Di Ne/MultiDresser/Profiles";
        if (!Directory.Exists(folder)) return;

        string[] guids = AssetDatabase.FindAssets("t:DiNeProfile", new[] { folder });
        List<DiNeProfile> candidates = new List<DiNeProfile>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DiNeProfile p = AssetDatabase.LoadAssetAtPath<DiNeProfile>(path);
            if (p != null) candidates.Add(p);
        }

        DiNeProfile bestMatch = null;
        
        if (rootTransform != null)
        {
            string safeAvatarName = GetSafeName(rootTransform.name);
            var matches = candidates
                .Where(p => p.name.Contains(safeAvatarName))
                .OrderByDescending(p => p.name)
                .ToList();
            if (matches.Count > 0) bestMatch = matches[0];
        }

        if (bestMatch == null)
        {
            string safeObjName = GetSafeName(gameObject.name);
            var matches = candidates
                .Where(p => p.name.Contains(safeObjName))
                .OrderByDescending(p => p.name)
                .ToList();
            if (matches.Count > 0) bestMatch = matches[0];
        }

        if (bestMatch != null)
        {
            savedProfile = bestMatch;
            Debug.Log($"♻ 설정 복구 완료! 불러온 파일: {bestMatch.name}");
            RestoreInternal(bestMatch);
        }
    }

    private void RestoreInternal(DiNeProfile profile)
    {
        if (profile == null) return;

        layers.Clear();
        foreach(var saved in profile.savedLayers)
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
            layers.Add(layer);
        }
        shapeKeyTargets = new List<GameObject>(profile.shapeKeyTargets);
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
            // ✅ 경로 변경: DiNe/MultiDresser/LayerName
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
            // ✅ 경로 변경 반영
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";
            string animLayerName = $"DiNe {safeLayerName}"; // 레이어 이름은 공백 유지 (가독성)
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

        // 레이어 웨이트 강제 고정 (Safety Lock)
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

                foreach (var skName in layerActiveKeys[m])
                {
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