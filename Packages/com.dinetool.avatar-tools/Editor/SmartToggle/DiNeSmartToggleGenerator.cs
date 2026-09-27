#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public static class DiNeSmartToggleGenerator
{
    private const string IconFolder = "Assets/Di Ne/SmartToggle/Icons";
    private const string LayerPrefix = "DiNe Smart Toggle/";

    public static Texture2D EnsureIcon(DiNeSmartToggle smartToggle)
    {
        if (smartToggle == null) return null;
        smartToggle.EnsureDefaults();
        if (smartToggle.Icon != null) return smartToggle.Icon;
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GetIconPath(smartToggle));
        if (existing != null)
        {
            Undo.RecordObject(smartToggle, "Assign Smart Toggle Icon");
            smartToggle.Icon = existing;
            PrefabUtility.RecordPrefabInstancePropertyModifications(smartToggle);
            EditorUtility.SetDirty(smartToggle);
            return existing;
        }
        return RegenerateIcon(smartToggle);
    }

    public static Texture2D RegenerateIcon(DiNeSmartToggle smartToggle)
    {
        if (smartToggle == null) return null;
        EnsureFolder(IconFolder);
        smartToggle.EnsureDefaults();
        string path = GetIconPath(smartToggle);
        var settings = new DiNeIconMaker.Settings
        {
            outlineEnabled = smartToggle.IconOutline,
            outlineColor = smartToggle.IconOutlineColor,
            outlineSize = smartToggle.IconOutlineSize,
            forbiddenOverlay = smartToggle.IconForbiddenOverlay,
            forbiddenOpacity = smartToggle.IconForbiddenOpacity,
            forbiddenScale = smartToggle.IconForbiddenScale,
            forbiddenBehindObject = smartToggle.IconForbiddenBehindObject
        };
        Texture2D result = DiNeScreenSaver.DiNeScreenSaver.GenerateConfiguredIcon(
            smartToggle.gameObject, smartToggle.IconEuler, smartToggle.IconPan,
            smartToggle.IconZoom, settings, path, smartToggle.IconIdlePose);
        if (result == null)
            result = DiNeIconMaker.GenerateIcon(smartToggle.gameObject, null, path, settings);

        Undo.RecordObject(smartToggle, "Generate Smart Toggle Icon");
        if (result != null) smartToggle.Icon = result;
        PrefabUtility.RecordPrefabInstancePropertyModifications(smartToggle);
        EditorUtility.SetDirty(smartToggle);
        AssetDatabase.SaveAssets();
        return result;
    }

    private sealed class ToggleRequest
    {
        public string name, parameter, path;
        public bool defaultOn, saved, allowCreate;
        public Texture2D icon;
        public GameObject[] targets;
    }

    public static void ApplyToTemporaryAvatar(VRCAvatarDescriptor descriptor, AnimatorController controller,
        VRCExpressionsMenu rootMenu, VRCExpressionParameters expressionParameters,
        IReadOnlyList<DiNeSmartToggle> toggles, string generatedFolder)
    {
        if (toggles == null) return;
        var requests = new List<ToggleRequest>();
        foreach (var toggle in toggles)
        {
            if (toggle == null || !toggle.enabled || descriptor == null || toggle.transform == descriptor.transform ||
                !toggle.transform.IsChildOf(descriptor.transform)) continue;
            toggle.EnsureDefaults();
            requests.Add(new ToggleRequest {
                name = toggle.DisplayName, parameter = toggle.ParameterName, defaultOn = toggle.DefaultOn,
                saved = toggle.Saved, icon = EnsureIcon(toggle), targets = new[] { toggle.gameObject },
                path = DiNeToggleMenuChoices.LegacyPath(toggle.Placement, toggle.GroupName, toggle.MenuPath),
                allowCreate = toggle.Placement != DiNeSmartToggle.MenuPlacement.ExistingMenu || toggle.GeneratedMenuDestination
            });
        }
        ApplyRequests(descriptor, controller, rootMenu, expressionParameters, requests, generatedFolder, LayerPrefix);
    }

    public static void ApplyDresserTogglesToTemporaryAvatar(VRCAvatarDescriptor descriptor, AnimatorController controller,
        VRCExpressionsMenu rootMenu, VRCExpressionParameters expressionParameters,
        IReadOnlyList<DiNeMultiDresser> dressers, string generatedFolder)
    {
        if (dressers == null) return;
        var requests = new List<ToggleRequest>();
        foreach (var dresser in dressers)
        {
            if (dresser == null || !dresser.enabled || dresser.GetAvatarDescriptor() != descriptor) continue;
            foreach (var group in dresser.independentToggles)
            {
                if (group == null) continue;
                var targets = group.targets.Where(go => DiNeIndependentToggleEditing.CanAdd(dresser, group, go)).Distinct().ToArray();
                if (targets.Length != group.targets.Count(go => go != null))
                    Debug.LogWarning($"[DiNe Multi Dresser] '{group.displayName}': invalid, duplicate or conflicting targets were skipped.");
                if (targets.Length == 0) continue;
                requests.Add(new ToggleRequest { name = group.displayName, parameter = group.parameterName,
                    defaultOn = group.defaultOn, saved = group.saved, icon = group.icon,
                    targets = targets, path = group.menuPath, allowCreate = group.generatedMenuDestination });
            }
        }
        ApplyRequests(descriptor, controller, rootMenu, expressionParameters, requests, generatedFolder, "DiNe Independent Toggle/");
    }

    private static void ApplyRequests(VRCAvatarDescriptor descriptor, AnimatorController controller,
        VRCExpressionsMenu rootMenu, VRCExpressionParameters expressionParameters,
        List<ToggleRequest> requests, string generatedFolder, string prefix)
    {
        if (descriptor == null || controller == null || rootMenu == null || expressionParameters == null || requests.Count == 0) return;
        EnsureFolder(generatedFolder);
        if (rootMenu.controls == null) rootMenu.controls = new List<VRCExpressionsMenu.Control>();
        // A repeated apply may reuse only parameters owned by this generator's existing layers.
        var owned = controller.layers.Where(layer => layer.name.StartsWith(prefix, StringComparison.Ordinal))
            .SelectMany(layer => layer.stateMachine.states)
            .SelectMany(state => state.state.transitions).SelectMany(transition => transition.conditions)
            .Select(condition => condition.parameter).ToHashSet();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in controller.parameters)
            if (parameter.type != AnimatorControllerParameterType.Bool || !owned.Contains(parameter.name)) used.Add(parameter.name);
        var parameters = expressionParameters.parameters != null ? expressionParameters.parameters.ToList()
            : new List<VRCExpressionParameters.Parameter>();
        foreach (var parameter in parameters)
            if (parameter != null && (parameter.valueType != VRCExpressionParameters.ValueType.Bool || !owned.Contains(parameter.name))) used.Add(parameter.name);
        for (int i = controller.layers.Length - 1; i >= 0; i--)
            if (controller.layers[i].name.StartsWith(prefix, StringComparison.Ordinal)) controller.RemoveLayer(i);
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var destination = DiNeToggleMenuChoices.Resolve(rootMenu, request.path, generatedFolder, request.allowCreate);
            if (destination == null)
            {
                Debug.LogError($"[DiNe Toggle] '{request.name}': menu destination is missing or its parent menu is full. Select another menu.");
                continue;
            }
            string baseName = string.IsNullOrWhiteSpace(request.parameter) ? DiNeSmartToggle.BuildDefaultParameterName(request.name) : request.parameter.Trim();
            string parameterName = baseName;
            for (int suffix = 2; !used.Add(parameterName); suffix++) parameterName = baseName + "_" + suffix;
            if (parameterName != baseName) Debug.LogWarning($"[DiNe Toggle] Parameter '{baseName}' is already used; using '{parameterName}' for '{request.name}'.");
            var existing = parameters.FirstOrDefault(parameter => parameter != null && parameter.name == parameterName);
            if (existing == null)
            {
                existing = new VRCExpressionParameters.Parameter { name = parameterName };
                parameters.Add(existing);
            }
            existing.valueType = VRCExpressionParameters.ValueType.Bool;
            existing.defaultValue = request.defaultOn ? 1f : 0f; existing.saved = request.saved;
            AddOrUpdateAnimatorParameter(controller, parameterName, request.defaultOn);
            CreateToggleLayer(descriptor, controller, request, parameterName, generatedFolder, i, prefix);
            AddControlWithPages(destination, new VRCExpressionsMenu.Control {
                name = request.name, icon = request.icon != null ? request.icon : DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png"),
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName }, value = 1f
            }, request.name, generatedFolder, 1);
        }
        expressionParameters.parameters = parameters.ToArray();
        EditorUtility.SetDirty(expressionParameters); EditorUtility.SetDirty(rootMenu); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void AddOrUpdateAnimatorParameter(AnimatorController controller, string name, bool defaultOn)
    {
        AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing == null)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = defaultOn
            });
            return;
        }

        controller.RemoveParameter(existing);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Bool,
            defaultBool = defaultOn
        });
    }

    private static void CreateToggleLayer(
        VRCAvatarDescriptor descriptor,
        AnimatorController controller,
        ToggleRequest request,
        string parameterName,
        string generatedFolder,
        int index, string prefix)
    {
        string safeName = SafeName(request.name);
        string[] paths = request.targets.Select(go => AnimationUtility.CalculateTransformPath(go.transform, descriptor.transform)).Distinct().ToArray();
        AnimationClip offClip = CreateActiveClip(paths, false, generatedFolder, safeName + "_Off_" + index);
        AnimationClip onClip = CreateActiveClip(paths, true, generatedFolder, safeName + "_On_" + index);

        var stateMachine = new AnimatorStateMachine { name = prefix + safeName };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);
        // These Bool toggles need the same protection as wardrobe Int layers.
        // MA can relocate layers during a build, so padding alone is insufficient.
        DiNeMultiDresser.PreserveLayerInMmd(stateMachine);
        AnimatorState offState = stateMachine.AddState("Off", new Vector3(250f, 80f));
        AnimatorState onState = stateMachine.AddState("On", new Vector3(250f, 180f));
        offState.motion = offClip;
        onState.motion = onClip;
        stateMachine.defaultState = request.defaultOn ? onState : offState;

        AnimatorStateTransition toOn = offState.AddTransition(onState);
        ConfigureTransition(toOn, parameterName, AnimatorConditionMode.If);
        AnimatorStateTransition toOff = onState.AddTransition(offState);
        ConfigureTransition(toOff, parameterName, AnimatorConditionMode.IfNot);

        // Standalone/group-only avatars may have no wardrobe layers to reserve
        // the FX slots that MMD worlds disable (zero-based indices 1 and 2).
        while (controller.layers.Length < 3)
            controller.AddLayer("DiNe MMD Reserved " + controller.layers.Length);

        controller.AddLayer(new AnimatorControllerLayer
        {
            name = prefix + safeName,
            defaultWeight = 1f,
            stateMachine = stateMachine
        });
    }

    private static AnimationClip CreateActiveClip(string[] paths, bool active, string folder, string fileName)
    {
        var clip = new AnimationClip { name = fileName, frameRate = 60f };
        foreach (string path in paths)
        {
            var binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f / 60f, active ? 1f : 0f));
        }
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(fileName) + ".anim");
        AssetDatabase.CreateAsset(clip, assetPath);
        return clip;
    }

    private static void ConfigureTransition(AnimatorStateTransition transition, string parameterName, AnimatorConditionMode mode)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(mode, 0f, parameterName);
    }

    // 임시 세션은 루트 메뉴만 복제한다. 사용자의 원본 서브메뉴 에셋을 직접 고치지 않도록 임시 폴더에 복제해 쓴다.
    private static VRCExpressionsMenu CloneIfExternal(VRCExpressionsMenu menu, string folder)
    {
        string path = AssetDatabase.GetAssetPath(menu);
        if (!string.IsNullOrEmpty(path) && path.StartsWith(folder + "/", StringComparison.Ordinal)) return menu;

        var copy = UnityEngine.Object.Instantiate(menu);
        copy.name = menu.name;
        AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(menu.name) + ".asset"));
        return copy;
    }

    private static void AddControlWithPages(
        VRCExpressionsMenu menu,
        VRCExpressionsMenu.Control control,
        string groupName,
        string folder,
        int pageNumber)
    {
        RemoveMatchingControl(menu, control.parameter.name);
        if (menu.controls == null)
            menu.controls = new List<VRCExpressionsMenu.Control>();
        if (menu.controls.Count < 8)
        {
            menu.controls.Add(control);
            EditorUtility.SetDirty(menu);
            return;
        }

        VRCExpressionsMenu.Control next = menu.controls.FirstOrDefault(item =>
            item.type == VRCExpressionsMenu.Control.ControlType.SubMenu && item.name == "Next ▶" && item.subMenu != null);
        if (next == null)
        {
            VRCExpressionsMenu.Control moved = menu.controls[7];
            menu.controls.RemoveAt(7);
            var nextMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            nextMenu.name = groupName + " " + (pageNumber + 1);
            AssetDatabase.CreateAsset(nextMenu,
                AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(nextMenu.name) + ".asset"));
            next = new VRCExpressionsMenu.Control
            {
                name = "Next ▶",
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = nextMenu
            };
            menu.controls.Add(next);
            nextMenu.controls.Add(moved);
        }
        else
        {
            next.subMenu = CloneIfExternal(next.subMenu, folder);
        }
        AddControlWithPages(next.subMenu, control, groupName, folder, pageNumber + 1);
        EditorUtility.SetDirty(menu);
    }

    private static void RemoveMatchingControl(VRCExpressionsMenu menu, string parameterName)
    {
        if (menu?.controls == null || string.IsNullOrEmpty(parameterName)) return;
        menu.controls.RemoveAll(control => control.parameter != null && control.parameter.name == parameterName);
    }

    private static string GetIconPath(DiNeSmartToggle smartToggle)
    {
        string key = string.IsNullOrWhiteSpace(smartToggle.ParameterName)
            ? smartToggle.gameObject.name
            : smartToggle.ParameterName;
        return IconFolder + "/" + SafeName(key) + ".png";
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "SmartToggle";
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value.Replace('/', '_').Replace('\\', '_').Trim();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }
}
#endif
