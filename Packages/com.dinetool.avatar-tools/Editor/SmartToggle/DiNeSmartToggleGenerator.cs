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
            smartToggle.IconZoom, settings, path);
        if (result == null)
            result = DiNeIconMaker.GenerateIcon(smartToggle.gameObject, null, path, settings);

        Undo.RecordObject(smartToggle, "Generate Smart Toggle Icon");
        smartToggle.Icon = result;
        EditorUtility.SetDirty(smartToggle);
        AssetDatabase.SaveAssets();
        return result;
    }

    public static void ApplyToTemporaryAvatar(
        VRCAvatarDescriptor descriptor,
        AnimatorController controller,
        VRCExpressionsMenu rootMenu,
        VRCExpressionParameters expressionParameters,
        IReadOnlyList<DiNeSmartToggle> toggles,
        string generatedFolder)
    {
        if (descriptor == null || controller == null || rootMenu == null || expressionParameters == null ||
            toggles == null || toggles.Count == 0)
            return;

        EnsureFolder(generatedFolder);
        if (rootMenu.controls == null)
            rootMenu.controls = new List<VRCExpressionsMenu.Control>();
        RemoveGeneratedLayers(controller);

        var validToggles = toggles.Where(toggle => toggle != null && toggle.enabled).ToList();
        var parameterNames = BuildEffectiveParameterNames(validToggles);
        var parameters = expressionParameters.parameters != null
            ? expressionParameters.parameters.ToList()
            : new List<VRCExpressionParameters.Parameter>();
        var groupMenus = new Dictionary<string, VRCExpressionsMenu>(StringComparer.Ordinal);

        for (int i = 0; i < validToggles.Count; i++)
        {
            DiNeSmartToggle smartToggle = validToggles[i];
            smartToggle.EnsureDefaults();
            Texture2D icon = EnsureIcon(smartToggle);
            string parameterName = parameterNames[i];

            AddOrUpdateExpressionParameter(parameters, parameterName, smartToggle);
            AddOrUpdateAnimatorParameter(controller, parameterName, smartToggle.DefaultOn);
            CreateToggleLayer(descriptor, controller, smartToggle, parameterName, generatedFolder, i);

            var control = new VRCExpressionsMenu.Control
            {
                name = smartToggle.DisplayName,
                icon = icon,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName },
                value = 1f
            };

            if (smartToggle.Placement == DiNeSmartToggle.MenuPlacement.Root)
            {
                RemoveMatchingControl(rootMenu, parameterName);
                if (rootMenu.controls.Count < 8)
                    rootMenu.controls.Insert(0, control);
                else
                    Debug.LogError($"[DiNe Smart Toggle] Root menu is full. '{smartToggle.DisplayName}' could not be added.");
            }
            else
            {
                string groupName = string.IsNullOrWhiteSpace(smartToggle.GroupName) ? "Smart Toggles" : smartToggle.GroupName.Trim();
                VRCExpressionsMenu groupMenu = GetOrCreateGroupMenu(rootMenu, groupMenus, groupName, generatedFolder);
                if (groupMenu != null)
                    AddControlWithPages(groupMenu, control, groupName, generatedFolder, 1);
            }
        }

        expressionParameters.parameters = parameters.ToArray();
        EditorUtility.SetDirty(expressionParameters);
        EditorUtility.SetDirty(rootMenu);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static List<string> BuildEffectiveParameterNames(IReadOnlyList<DiNeSmartToggle> toggles)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(toggles.Count);
        foreach (DiNeSmartToggle toggle in toggles)
        {
            string baseName = string.IsNullOrWhiteSpace(toggle.ParameterName)
                ? DiNeSmartToggle.BuildDefaultParameterName(toggle.gameObject.name)
                : toggle.ParameterName.Trim();
            string candidate = baseName;
            for (int suffix = 2; !used.Add(candidate); suffix++)
                candidate = baseName + "_" + suffix;
            result.Add(candidate);
        }
        return result;
    }

    private static void AddOrUpdateExpressionParameter(
        List<VRCExpressionParameters.Parameter> parameters,
        string parameterName,
        DiNeSmartToggle smartToggle)
    {
        var existing = parameters.FirstOrDefault(parameter => parameter.name == parameterName);
        if (existing == null)
        {
            parameters.Add(new VRCExpressionParameters.Parameter
            {
                name = parameterName,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = smartToggle.DefaultOn ? 1f : 0f,
                saved = smartToggle.Saved
            });
            return;
        }

        existing.valueType = VRCExpressionParameters.ValueType.Bool;
        existing.defaultValue = smartToggle.DefaultOn ? 1f : 0f;
        existing.saved = smartToggle.Saved;
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
        DiNeSmartToggle smartToggle,
        string parameterName,
        string generatedFolder,
        int index)
    {
        string safeName = SafeName(smartToggle.DisplayName);
        string relativePath = AnimationUtility.CalculateTransformPath(smartToggle.transform, descriptor.transform);
        AnimationClip offClip = CreateActiveClip(relativePath, false, generatedFolder, safeName + "_Off_" + index);
        AnimationClip onClip = CreateActiveClip(relativePath, true, generatedFolder, safeName + "_On_" + index);

        var stateMachine = new AnimatorStateMachine { name = LayerPrefix + safeName };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);
        AnimatorState offState = stateMachine.AddState("Off", new Vector3(250f, 80f));
        AnimatorState onState = stateMachine.AddState("On", new Vector3(250f, 180f));
        offState.motion = offClip;
        onState.motion = onClip;
        stateMachine.defaultState = smartToggle.DefaultOn ? onState : offState;

        AnimatorStateTransition toOn = offState.AddTransition(onState);
        ConfigureTransition(toOn, parameterName, AnimatorConditionMode.If);
        AnimatorStateTransition toOff = onState.AddTransition(offState);
        ConfigureTransition(toOff, parameterName, AnimatorConditionMode.IfNot);

        controller.AddLayer(new AnimatorControllerLayer
        {
            name = LayerPrefix + safeName,
            defaultWeight = 1f,
            stateMachine = stateMachine
        });
    }

    private static AnimationClip CreateActiveClip(string path, bool active, string folder, string fileName)
    {
        var clip = new AnimationClip { name = fileName, frameRate = 60f };
        var binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
        AnimationUtility.SetEditorCurve(clip, binding,
            AnimationCurve.Constant(0f, 1f / 60f, active ? 1f : 0f));
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

    private static void RemoveGeneratedLayers(AnimatorController controller)
    {
        for (int i = controller.layers.Length - 1; i >= 0; i--)
            if (controller.layers[i].name.StartsWith(LayerPrefix, StringComparison.Ordinal))
                controller.RemoveLayer(i);
    }

    private static VRCExpressionsMenu GetOrCreateGroupMenu(
        VRCExpressionsMenu rootMenu,
        Dictionary<string, VRCExpressionsMenu> cache,
        string groupName,
        string folder)
    {
        if (cache.TryGetValue(groupName, out VRCExpressionsMenu cached)) return cached;

        if (rootMenu.controls == null)
            rootMenu.controls = new List<VRCExpressionsMenu.Control>();

        var existing = rootMenu.controls.FirstOrDefault(control =>
            control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.name == groupName && control.subMenu != null);
        if (existing != null)
        {
            cache[groupName] = existing.subMenu;
            return existing.subMenu;
        }

        if (rootMenu.controls.Count >= 8)
        {
            Debug.LogError($"[DiNe Smart Toggle] Root menu is full. The '{groupName}' submenu could not be added.");
            return null;
        }

        var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        menu.name = groupName;
        AssetDatabase.CreateAsset(menu, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(groupName) + ".asset"));
        rootMenu.controls.Add(new VRCExpressionsMenu.Control
        {
            name = groupName,
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = menu
        });
        cache[groupName] = menu;
        return menu;
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
