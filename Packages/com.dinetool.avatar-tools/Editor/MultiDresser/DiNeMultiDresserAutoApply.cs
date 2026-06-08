#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

public class DiNeMultiDresserAutoApply : IVRCSDKBuildRequestedCallback, IVRCSDKPreprocessAvatarCallback
{
    private const string TempRootFolder = "Assets/Di Ne/MultiDresser/__Temp";
    private const string SessionStateKey = "DiNe.MultiDresser.TempSessions";

    private static readonly Dictionary<int, TemporarySession> ActiveSessions = new Dictionary<int, TemporarySession>();
    private static readonly Dictionary<string, AnimatorControllerParameterType> KnownAnimatorParameters = new Dictionary<string, AnimatorControllerParameterType>
    {
        { "GestureLeft", AnimatorControllerParameterType.Int },
        { "GestureRight", AnimatorControllerParameterType.Int },
        { "GestureLeftWeight", AnimatorControllerParameterType.Float },
        { "GestureRightWeight", AnimatorControllerParameterType.Float },
        { "VRMode", AnimatorControllerParameterType.Int },
        { "Viseme", AnimatorControllerParameterType.Int },
        { "Voice", AnimatorControllerParameterType.Float },
        { "Upright", AnimatorControllerParameterType.Float },
        { "AngularY", AnimatorControllerParameterType.Float },
        { "VelocityX", AnimatorControllerParameterType.Float },
        { "VelocityY", AnimatorControllerParameterType.Float },
        { "VelocityZ", AnimatorControllerParameterType.Float },
        { "VelocityMagnitude", AnimatorControllerParameterType.Float },
        { "Grounded", AnimatorControllerParameterType.Bool },
        { "Seated", AnimatorControllerParameterType.Bool },
        { "AFK", AnimatorControllerParameterType.Bool },
        { "IsLocal", AnimatorControllerParameterType.Bool },
        { "IsOnFriendsList", AnimatorControllerParameterType.Bool },
        { "InStation", AnimatorControllerParameterType.Bool },
        { "MuteSelf", AnimatorControllerParameterType.Bool },
        { "TrackingType", AnimatorControllerParameterType.Int },
        { "AvatarVersion", AnimatorControllerParameterType.Int },
        { "IsAnimatorEnabled", AnimatorControllerParameterType.Bool },
        { "ScaleFactor", AnimatorControllerParameterType.Float },
        { "ScaleFactorInverse", AnimatorControllerParameterType.Float },
        { "EyeHeightAsMeters", AnimatorControllerParameterType.Float },
        { "EyeHeightAsPercent", AnimatorControllerParameterType.Float },
    };
    private static bool builderEventsHooked;

    public int callbackOrder => 0;

    [Serializable]
    private class PersistedState
    {
        public List<PersistedSession> sessions = new List<PersistedSession>();
    }

    [Serializable]
    private class PersistedSession
    {
        public string descriptorId;
        public string tempFolderPath;
        public string originalFxControllerPath;
        public string originalMenuPath;
        public string originalParametersPath;
        public List<PersistedDresserBinding> dressers = new List<PersistedDresserBinding>();
    }

    [Serializable]
    private class PersistedDresserBinding
    {
        public string dresserId;
        public string originalAnimatorControllerPath;
        public string originalExpressionsMenuPath;
    }

    private sealed class DresserBinding
    {
        public DiNeMultiDresser Dresser;
        public AnimatorController OriginalAnimatorController;
        public VRCExpressionsMenu OriginalExpressionsMenu;
    }

    private sealed class TemporarySession
    {
        public VRCAvatarDescriptor Descriptor;
        public string TempFolderPath;
        public RuntimeAnimatorController OriginalFxController;
        public VRCExpressionsMenu OriginalMenu;
        public VRCExpressionParameters OriginalParameters;
        public AnimatorController TempAnimatorController;
        public VRCExpressionsMenu TempExpressionsMenu;
        public VRCExpressionParameters TempExpressionParameters;
        public List<DresserBinding> Dressers = new List<DresserBinding>();
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    private static void OnBeforeAssemblyReload()
    {
        PersistSessions();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Apply temporary FX/Menu assets before Unity clones the scene for play mode.
            // EnteredPlayMode is too late for tools that cache avatar controllers during startup.
            ApplyAllDressersInScene();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => RestoreAllSessions("play mode ended");
        }
    }

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType != VRCSDKRequestedBuildType.Avatar)
            return true;

        try
        {
            EnsureBuilderHooks();
            ApplyAllDressersInScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to prepare temporary Multi Dresser data for build: {e.Message}\n{e.StackTrace}");
        }

        return true;
    }

    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        try
        {
            ApplyDressersForAvatarRoot(avatarGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to apply temporary Multi Dresser data to build avatar: {e.Message}\n{e.StackTrace}");
        }

        return true;
    }

    private static void EnsureBuilderHooks()
    {
        if (builderEventsHooked)
            return;

        if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder) || builder == null)
            return;

        builder.OnSdkBuildFinish += OnBuildEnded;
        builder.OnSdkBuildError += OnBuildEnded;
        builder.OnSdkUploadFinish += OnBuildEnded;
        builder.OnSdkUploadError += OnBuildEnded;
        builderEventsHooked = true;
    }

    private static void OnBuildEnded(object sender, string _)
    {
        EditorApplication.delayCall += () => RestoreAllSessions("build/upload finished");
    }

    private static void ApplyAllDressersInScene()
    {
        RestoreAllSessions("refresh temporary session");

        var dressers = UnityEngine.Object.FindObjectsOfType<DiNeMultiDresser>()
            .Where(dresser => dresser != null && dresser.gameObject.activeInHierarchy)
            .ToArray();

        if (dressers.Length == 0)
            return;

        var groups = new Dictionary<VRCAvatarDescriptor, List<DiNeMultiDresser>>();
        foreach (var dresser in dressers)
        {
            dresser.TryAutoAssignFXController();

            var descriptor = FindDescriptor(dresser);
            if (descriptor == null)
            {
                Debug.LogWarning($"[DiNe] Skipping '{dresser.name}' because no VRCAvatarDescriptor was found.");
                continue;
            }

            if (!groups.TryGetValue(descriptor, out var groupedDressers))
            {
                groupedDressers = new List<DiNeMultiDresser>();
                groups.Add(descriptor, groupedDressers);
            }

            groupedDressers.Add(dresser);
        }

        foreach (var pair in groups)
        {
            ApplyTemporarySession(pair.Key, pair.Value);
        }
    }

    private static void ApplyDressersForAvatarRoot(GameObject avatarGameObject)
    {
        if (avatarGameObject == null)
            return;

        var descriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>()
            ?? avatarGameObject.GetComponentInChildren<VRCAvatarDescriptor>(true);
        if (descriptor == null)
            return;

        var dressers = avatarGameObject.GetComponentsInChildren<DiNeMultiDresser>(true)
            .Where(dresser => dresser != null && dresser.enabled)
            .ToList();
        if (dressers.Count == 0)
            return;

        Debug.Log($"[DiNe] Applying Multi Dresser to build avatar '{avatarGameObject.name}' with {dressers.Count} dresser(s).");

        foreach (var dresser in dressers)
        {
            dresser.TryAutoAssignFXController();
        }

        ApplyTemporarySession(descriptor, dressers);
    }

    private static void ApplyTemporarySession(VRCAvatarDescriptor descriptor, List<DiNeMultiDresser> dressers)
    {
        if (descriptor == null || dressers == null || dressers.Count == 0)
            return;

        var session = CreateTemporarySession(descriptor, dressers);
        if (session == null)
            return;

        ActiveSessions[descriptor.GetInstanceID()] = session;
        PersistSessions();

        for (int i = 0; i < session.Dressers.Count; i++)
        {
            var binding = session.Dressers[i];
            bool clearExistingGeneratedData = i == 0;
            GenerateDresser(binding.Dresser, session.TempFolderPath, clearExistingGeneratedData);
        }
    }

    private static TemporarySession CreateTemporarySession(VRCAvatarDescriptor descriptor, List<DiNeMultiDresser> dressers)
    {
        EnsureFolderExists(TempRootFolder);

        string sessionFolder = AssetDatabase.GenerateUniqueAssetPath(
            $"{TempRootFolder}/{GetSafeName(descriptor.gameObject.name)}_{Guid.NewGuid():N}");
        EnsureFolderExists(sessionFolder);

        var session = new TemporarySession
        {
            Descriptor = descriptor,
            TempFolderPath = sessionFolder,
            OriginalFxController = GetDescriptorFxController(descriptor),
            OriginalMenu = descriptor.expressionsMenu,
            OriginalParameters = descriptor.expressionParameters
        };

        var sourceFxController = session.OriginalFxController as AnimatorController;
        var sourceMenu = descriptor.expressionsMenu != null
            ? descriptor.expressionsMenu
            : dressers.Select(d => d.expressionsMenu).FirstOrDefault(menu => menu != null);

        session.TempAnimatorController = CloneOrCreateAnimatorController(sourceFxController, sessionFolder);
        session.TempExpressionsMenu = CloneOrCreateExpressionsMenu(sourceMenu, sessionFolder);
        session.TempExpressionParameters = CloneOrCreateExpressionParameters(descriptor.expressionParameters, sessionFolder);
        SanitizeAnimatorController(session.TempAnimatorController);

        SetDescriptorFxController(descriptor, session.TempAnimatorController);
        descriptor.expressionsMenu = session.TempExpressionsMenu;
        descriptor.expressionParameters = session.TempExpressionParameters;
        EditorUtility.SetDirty(descriptor);

        foreach (var dresser in dressers)
        {
            var binding = new DresserBinding
            {
                Dresser = dresser,
                OriginalAnimatorController = dresser.animatorController,
                OriginalExpressionsMenu = dresser.expressionsMenu
            };

            dresser.animatorController = session.TempAnimatorController;
            dresser.expressionsMenu = session.TempExpressionsMenu;
            EditorUtility.SetDirty(dresser);

            session.Dressers.Add(binding);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return session;
    }

    private static void RestoreAllSessions(string reason)
    {
        var persisted = LoadPersistedState();
        bool restored = false;

        foreach (var session in ActiveSessions.Values.ToList())
        {
            RestoreSession(session);
            restored = true;
        }

        ActiveSessions.Clear();

        if (!restored && persisted.sessions.Count > 0)
        {
            foreach (var session in persisted.sessions)
            {
                RestorePersistedSession(session);
            }
        }

        SessionState.EraseString(SessionStateKey);

        if (restored || persisted.sessions.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DiNe] Restored temporary Multi Dresser data after {reason}.");
        }
    }

    private static void RestoreSession(TemporarySession session)
    {
        if (session == null)
            return;

        if (session.Descriptor != null)
        {
            SetDescriptorFxController(session.Descriptor, session.OriginalFxController);
            session.Descriptor.expressionsMenu = session.OriginalMenu;
            session.Descriptor.expressionParameters = session.OriginalParameters;
            EditorUtility.SetDirty(session.Descriptor);
        }

        foreach (var binding in session.Dressers)
        {
            RestoreDresserBinding(binding);
        }

        DeleteTemporaryFolder(session.TempFolderPath);
    }

    private static void RestorePersistedSession(PersistedSession session)
    {
        if (session == null)
            return;

        var descriptor = LoadSceneObject<VRCAvatarDescriptor>(session.descriptorId);
        if (descriptor != null)
        {
            SetDescriptorFxController(descriptor, LoadAssetByPath<RuntimeAnimatorController>(session.originalFxControllerPath));
            descriptor.expressionsMenu = LoadAssetByPath<VRCExpressionsMenu>(session.originalMenuPath);
            descriptor.expressionParameters = LoadAssetByPath<VRCExpressionParameters>(session.originalParametersPath);
            EditorUtility.SetDirty(descriptor);
        }

        if (session.dressers != null)
        {
            foreach (var binding in session.dressers)
            {
                var dresser = LoadSceneObject<DiNeMultiDresser>(binding.dresserId);
                if (dresser == null)
                    continue;

                dresser.animatorController = LoadAssetByPath<AnimatorController>(binding.originalAnimatorControllerPath);
                dresser.expressionsMenu = LoadAssetByPath<VRCExpressionsMenu>(binding.originalExpressionsMenuPath);
                EditorUtility.SetDirty(dresser);
            }
        }

        DeleteTemporaryFolder(session.tempFolderPath);
    }

    private static void RestoreDresserBinding(DresserBinding binding)
    {
        if (binding == null || binding.Dresser == null)
            return;

        binding.Dresser.animatorController = binding.OriginalAnimatorController;
        binding.Dresser.expressionsMenu = binding.OriginalExpressionsMenu;
        EditorUtility.SetDirty(binding.Dresser);
    }

    private static void PersistSessions()
    {
        var state = new PersistedState();

        foreach (var session in ActiveSessions.Values)
        {
            if (session == null || session.Descriptor == null)
                continue;

            var persistedSession = new PersistedSession
            {
                descriptorId = ToGlobalObjectId(session.Descriptor),
                tempFolderPath = session.TempFolderPath,
                originalFxControllerPath = AssetDatabase.GetAssetPath(session.OriginalFxController),
                originalMenuPath = AssetDatabase.GetAssetPath(session.OriginalMenu),
                originalParametersPath = AssetDatabase.GetAssetPath(session.OriginalParameters)
            };

            foreach (var binding in session.Dressers)
            {
                if (binding?.Dresser == null)
                    continue;

                persistedSession.dressers.Add(new PersistedDresserBinding
                {
                    dresserId = ToGlobalObjectId(binding.Dresser),
                    originalAnimatorControllerPath = AssetDatabase.GetAssetPath(binding.OriginalAnimatorController),
                    originalExpressionsMenuPath = AssetDatabase.GetAssetPath(binding.OriginalExpressionsMenu)
                });
            }

            state.sessions.Add(persistedSession);
        }

        if (state.sessions.Count == 0)
        {
            SessionState.EraseString(SessionStateKey);
            return;
        }

        SessionState.SetString(SessionStateKey, JsonUtility.ToJson(state));
    }

    private static PersistedState LoadPersistedState()
    {
        string json = SessionState.GetString(SessionStateKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new PersistedState();

        return JsonUtility.FromJson<PersistedState>(json) ?? new PersistedState();
    }

    private static VRCAvatarDescriptor FindDescriptor(DiNeMultiDresser dresser)
    {
        if (dresser == null)
            return null;

        return dresser.GetComponentInParent<VRCAvatarDescriptor>()
            ?? dresser.GetComponentInChildren<VRCAvatarDescriptor>();
    }

    private static RuntimeAnimatorController GetDescriptorFxController(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null)
            return null;

        if (descriptor.baseAnimationLayers != null)
        {
            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer.animatorController;
            }
        }

        if (descriptor.specialAnimationLayers != null)
        {
            foreach (var layer in descriptor.specialAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    return layer.animatorController;
            }
        }

        return null;
    }

    private static void SetDescriptorFxController(VRCAvatarDescriptor descriptor, RuntimeAnimatorController controller)
    {
        if (descriptor == null)
            return;

        bool updated = false;

        if (descriptor.baseAnimationLayers != null)
        {
            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
            {
                if (descriptor.baseAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;

                var layer = descriptor.baseAnimationLayers[i];
                layer.animatorController = controller;
                descriptor.baseAnimationLayers[i] = layer;
                updated = true;
            }
        }

        if (descriptor.specialAnimationLayers != null)
        {
            for (int i = 0; i < descriptor.specialAnimationLayers.Length; i++)
            {
                if (descriptor.specialAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    continue;

                var layer = descriptor.specialAnimationLayers[i];
                layer.animatorController = controller;
                descriptor.specialAnimationLayers[i] = layer;
                updated = true;
            }
        }

        if (updated)
        {
            EditorUtility.SetDirty(descriptor);
        }
    }

    private static AnimatorController CloneOrCreateAnimatorController(AnimatorController source, string folder)
    {
        if (source != null)
        {
            var clone = CloneAsset<AnimatorController>(source, folder, "FX_Temp");
            if (clone != null)
                return clone;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/FX_Temp.controller");
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    private static void SanitizeAnimatorController(AnimatorController controller)
    {
        if (controller == null)
            return;

        var parameterMap = new Dictionary<string, AnimatorControllerParameterType>();
        foreach (var parameter in controller.parameters)
        {
            if (!string.IsNullOrEmpty(parameter.name))
                parameterMap[parameter.name] = parameter.type;
        }

        int removedTransitions = 0;
        int addedParameters = 0;

        foreach (var layer in controller.layers)
        {
            if (layer.stateMachine == null)
                continue;

            removedTransitions += SanitizeStateMachine(layer.stateMachine, controller, parameterMap, ref addedParameters);
        }

        if (removedTransitions > 0 || addedParameters > 0)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DiNe] Sanitized temporary FX controller '{controller.name}': removed {removedTransitions} invalid transition(s), added {addedParameters} missing parameter(s).");
        }
    }

    private static int SanitizeStateMachine(
        AnimatorStateMachine stateMachine,
        AnimatorController controller,
        Dictionary<string, AnimatorControllerParameterType> parameterMap,
        ref int addedParameters)
    {
        if (stateMachine == null)
            return 0;

        int removedTransitions = 0;

        foreach (var transition in stateMachine.entryTransitions.ToArray())
        {
            if (IsInvalidTransition(transition))
            {
                stateMachine.RemoveEntryTransition(transition);
                removedTransitions++;
                continue;
            }

            EnsureTransitionParameters(transition, controller, parameterMap, ref addedParameters);
        }

        foreach (var transition in stateMachine.anyStateTransitions.ToArray())
        {
            if (IsInvalidTransition(transition))
            {
                stateMachine.RemoveAnyStateTransition(transition);
                removedTransitions++;
                continue;
            }

            EnsureTransitionParameters(transition, controller, parameterMap, ref addedParameters);
        }

        foreach (var childState in stateMachine.states)
        {
            var state = childState.state;
            if (state == null)
                continue;

            foreach (var transition in state.transitions.ToArray())
            {
                if (IsInvalidTransition(transition))
                {
                    state.RemoveTransition(transition);
                    removedTransitions++;
                    continue;
                }

                EnsureTransitionParameters(transition, controller, parameterMap, ref addedParameters);
            }
        }

        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine == null)
                continue;

            removedTransitions += SanitizeStateMachine(childStateMachine.stateMachine, controller, parameterMap, ref addedParameters);
        }

        return removedTransitions;
    }

    private static bool IsInvalidTransition(AnimatorTransitionBase transition)
    {
        if (transition == null)
            return true;

        if (transition is AnimatorStateTransition stateTransition)
            return !stateTransition.isExit
                && stateTransition.destinationState == null
                && stateTransition.destinationStateMachine == null;

        if (transition is AnimatorTransition entryTransition)
            return entryTransition.destinationState == null
                && entryTransition.destinationStateMachine == null;

        return false;
    }

    private static void EnsureTransitionParameters(
        AnimatorTransitionBase transition,
        AnimatorController controller,
        Dictionary<string, AnimatorControllerParameterType> parameterMap,
        ref int addedParameters)
    {
        if (transition == null)
            return;

        foreach (var condition in transition.conditions)
        {
            if (string.IsNullOrEmpty(condition.parameter) || parameterMap.ContainsKey(condition.parameter))
                continue;

            var parameterType = InferParameterType(condition.parameter, condition.mode);
            controller.AddParameter(condition.parameter, parameterType);
            parameterMap[condition.parameter] = parameterType;
            addedParameters++;
        }
    }

    private static AnimatorControllerParameterType InferParameterType(string parameterName, AnimatorConditionMode mode)
    {
        if (KnownAnimatorParameters.TryGetValue(parameterName, out var knownType))
            return knownType;

        switch (mode)
        {
            case AnimatorConditionMode.If:
            case AnimatorConditionMode.IfNot:
                return AnimatorControllerParameterType.Bool;
            case AnimatorConditionMode.Equals:
            case AnimatorConditionMode.NotEqual:
                return AnimatorControllerParameterType.Int;
            default:
                return AnimatorControllerParameterType.Float;
        }
    }

    private static VRCExpressionsMenu CloneOrCreateExpressionsMenu(VRCExpressionsMenu source, string folder)
    {
        if (source != null)
        {
            var clone = CloneAsset<VRCExpressionsMenu>(source, folder, "ExpressionsMenu_Temp");
            if (clone != null)
                return clone;
        }

        var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/ExpressionsMenu_Temp.asset");
        AssetDatabase.CreateAsset(menu, path);
        return menu;
    }

    private static VRCExpressionParameters CloneOrCreateExpressionParameters(VRCExpressionParameters source, string folder)
    {
        if (source != null)
        {
            var clone = CloneAsset<VRCExpressionParameters>(source, folder, "ExpressionParameters_Temp");
            if (clone != null)
                return clone;
        }

        var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
        parameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/ExpressionParameters_Temp.asset");
        AssetDatabase.CreateAsset(parameters, path);
        return parameters;
    }

    private static T CloneAsset<T>(T source, string folder, string fileNameWithoutExtension) where T : UnityEngine.Object
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
            return null;

        string extension = Path.GetExtension(sourcePath);
        string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileNameWithoutExtension}{extension}");

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<T>(targetPath);
    }

    private static T LoadAssetByPath<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
            return null;

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static T LoadSceneObject<T>(string globalObjectId) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(globalObjectId))
            return null;

        if (!GlobalObjectId.TryParse(globalObjectId, out var parsedId))
            return null;

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsedId) as T;
    }

    private static string ToGlobalObjectId(UnityEngine.Object target)
    {
        if (target == null)
            return string.Empty;

        return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
    }

    private static void DeleteTemporaryFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.DeleteAsset(folderPath);
        }
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        Directory.CreateDirectory(folderPath);
        AssetDatabase.Refresh();
    }

    private static void GenerateDresser(
        DiNeMultiDresser dresser,
        string generatedRootFolder,
        bool clearExistingGeneratedData)
    {
        try
        {
            Debug.Log($"[DiNe] Generating temporary Multi Dresser data: {dresser.name}");
            DiNeMultiIconGenerator.GenerateIcons(dresser);
            dresser.Generate(
                generatedRootFolder,
                clearExistingGeneratedData,
                !clearExistingGeneratedData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] Failed to generate '{dresser.name}': {e.Message}\n{e.StackTrace}");
        }
    }

    private static string GetSafeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Avatar";

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace("\\", "_").Replace("/", "_");
    }
}
#endif
