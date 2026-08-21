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

/// <summary>
/// 라이팅 디자이너의 FX 레이어 / 메뉴 / Expression 파라미터를 생성한다.
/// DiNeMultiDresserAutoApply가 만든 임시(__Temp) 세션 위에서만 호출된다.
/// </summary>
public static class DiNeLightingGenerator
{
    public const string LayerName = "DiNe Lighting Designer";
    public const string PresetLayerName = "DiNe Lighting Presets";

    public static void ApplyToTemporaryAvatar(
        VRCAvatarDescriptor descriptor,
        AnimatorController controller,
        VRCExpressionsMenu rootMenu,
        VRCExpressionParameters expressionParameters,
        IReadOnlyList<DiNeLightingDesigner> designers,
        string generatedFolder)
    {
        if (descriptor == null || controller == null || rootMenu == null || expressionParameters == null ||
            designers == null || designers.Count == 0)
            return;

        RemoveGeneratedLayers(controller);

        var active = designers.Where(item => item != null && item.enabled).ToList();
        if (active.Count == 0)
            return;

        if (active.Count > 1)
        {
            Debug.LogWarning(
                $"[DiNe 라이팅 디자이너] '{descriptor.name}'에 라이팅 디자이너가 {active.Count}개 있습니다. " +
                $"첫 번째('{active[0].name}')만 적용합니다.");
        }

        var designer = active[0];
        designer.EnsureDefaults();

        WarnOnLightLimitChanger(descriptor);

        var renderers = CollectRenderers(descriptor, designer);
        if (renderers.Count == 0)
        {
            Debug.LogWarning(
                $"[DiNe 라이팅 디자이너] '{descriptor.name}'에서 대상 셰이더를 쓰는 렌더러를 찾지 못했습니다. " +
                "대상 셰이더 설정과 제외 목록을 확인하세요.");
            return;
        }

        var controls = designer.Controls
            .Where(setting => setting != null && setting.enabled && DiNeLightingControlDef.Get(setting.control) != null)
            .ToList();

        if (controls.Count == 0)
        {
            Debug.LogWarning($"[DiNe 라이팅 디자이너] '{designer.name}'에 켜진 제어 항목이 없어 건너뜁니다.");
            return;
        }

        EnsureFolder(generatedFolder);

        // ── 클립 생성 ────────────────────────────────────
        var defaultSink = new DiNeLightingSink("DiNeLighting_Default", controller);

        // 키: (제어 항목, 그룹 인덱스). 그룹 인덱스 -1은 공용 슬라이더.
        var controlSinks = new Dictionary<ControlKey, DiNeLightingSink>();
        var unsupported = new HashSet<DiNeLightingControl>(controls.Select(setting => setting.control));

        // 렌더러 → 그룹 인덱스. 한 렌더러가 여러 그룹에 있으면 첫 번째 그룹만 인정한다.
        var groupOfRenderer = BuildRendererGroupMap(designer, renderers);

        bool sawLockedPoiyomi = false;

        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            var profiles = DiNeShaderProfile.ResolveProfiles(materials, designer.TargetShaders);
            if (profiles.Count == 0) continue;

            if (!sawLockedPoiyomi && materials.Any(DiNeShaderProfilePoiyomi.IsLocked))
                sawLockedPoiyomi = true;

            var context = BuildContext(designer, profiles, materials);
            string path = AnimationUtility.CalculateTransformPath(renderer.transform, descriptor.transform);
            var rendererType = renderer.GetType();

            defaultSink.CurrentPath = path;
            defaultSink.CurrentType = rendererType;

            groupOfRenderer.TryGetValue(renderer, out int rendererGroupIndex);

            foreach (var setting in controls)
            {
                // 이 렌더러가 속한 그룹이 해당 항목을 따로 떼어갔으면 그룹 전용 싱크로 보낸다.
                int groupIndex = -1;
                if (rendererGroupIndex >= 0 &&
                    rendererGroupIndex < designer.Groups.Count &&
                    designer.Groups[rendererGroupIndex].Separates(setting.control))
                {
                    groupIndex = rendererGroupIndex;
                }

                var key = new ControlKey(setting.control, groupIndex);
                if (!controlSinks.TryGetValue(key, out var sink))
                {
                    var def = DiNeLightingControlDef.Get(setting.control);
                    string sinkName = groupIndex < 0
                        ? "DiNeLighting_" + def.ParameterSuffix
                        : $"DiNeLighting_G{groupIndex}_{def.ParameterSuffix}";
                    sink = new DiNeLightingSink(sinkName, controller);
                    controlSinks.Add(key, sink);
                }

                sink.CurrentPath = path;
                sink.CurrentType = rendererType;

                foreach (var profile in profiles)
                {
                    if (!profile.Supports(setting.control)) continue;

                    unsupported.Remove(setting.control);
                    profile.WriteDefault(defaultSink, setting.control, context);
                    profile.WriteControl(sink, setting.control, context);
                }
            }
        }

        if (sawLockedPoiyomi)
        {
            Debug.LogWarning(
                "[DiNe 라이팅 디자이너] 이미 잠긴(locked) Poiyomi 머티리얼이 있습니다. " +
                "잠긴 머티리얼은 대상 프로퍼티가 이미 상수로 인라인돼 있어 조절이 먹지 않습니다. " +
                "해당 머티리얼의 잠금을 해제한 뒤 업로드하세요. " +
                "(업로드 시점에 Poiyomi가 잠그는 경우는 라이팅 디자이너가 Animated 태그를 붙여두므로 문제 없습니다.)");
        }

        foreach (var control in unsupported)
        {
            Debug.LogWarning(
                $"[DiNe 라이팅 디자이너] '{designer.GetDisplayName(control)}' 항목을 지원하는 셰이더가 이 아바타에 없습니다. " +
                $"(지원: {DiNeShaderProfile.DescribeSupport(control)})");
        }

        // 빈 싱크(= 아무 렌더러에도 기록되지 않음)는 파라미터도 메뉴도 만들지 않는다.
        foreach (var key in controlSinks.Keys.ToList())
        {
            if (controlSinks[key].IsEmpty)
                controlSinks.Remove(key);
        }

        var effective = controls.Where(setting =>
            controlSinks.Keys.Any(key => key.Control == setting.control)).ToList();

        if (effective.Count == 0)
        {
            Debug.LogWarning($"[DiNe 라이팅 디자이너] '{designer.name}': 생성할 애니메이션이 없어 설치를 건너뜁니다.");
            return;
        }

        // ── 애니메이터 조립 ──────────────────────────────
        BuildAnimatorLayer(controller, designer, defaultSink, controlSinks);
        BuildPresetLayer(controller, designer, controlSinks);

        // ── 파라미터 / 메뉴 ──────────────────────────────
        InstallParameters(expressionParameters, designer, controlSinks);
        InstallMenu(rootMenu, designer, controlSinks, generatedFolder);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(expressionParameters);
        EditorUtility.SetDirty(rootMenu);
        AssetDatabase.SaveAssets();

        int cost = expressionParameters.CalcTotalCost();
        if (cost > VRCExpressionParameters.MAX_PARAMETER_COST)
        {
            Debug.LogError(
                $"[DiNe 라이팅 디자이너] Expression 파라미터 예산을 초과했습니다: {cost} / {VRCExpressionParameters.MAX_PARAMETER_COST} bits. " +
                "제어 항목을 줄이세요.");
        }
    }

    // ──────────────────────────────────────────────────
    //  수집 / 컨텍스트
    // ──────────────────────────────────────────────────

    private static List<Renderer> CollectRenderers(VRCAvatarDescriptor descriptor, DiNeLightingDesigner designer)
    {
        var excluded = new HashSet<Renderer>(designer.Excludes.Where(item => item != null));

        return descriptor.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
            .Where(renderer => !excluded.Contains(renderer))
            .Where(renderer => renderer.sharedMaterials != null && renderer.sharedMaterials.Any(material => material != null))
            .ToList();
    }

    private static DiNeLightingContext BuildContext(
        DiNeLightingDesigner designer,
        List<DiNeShaderProfile> profiles,
        Material[] materials)
    {
        float defaultMin = Mathf.Lerp(designer.MinLightValue, designer.MaxLightValue, designer.DefaultMinLightValue);
        float defaultMax = Mathf.Lerp(designer.MinLightValue, designer.MaxLightValue, designer.DefaultMaxLightValue);

        if (!designer.OverwriteDefaultLightMinMax)
        {
            foreach (var profile in profiles)
            {
                if (profile.TryGetLightRange(materials, out float materialMin, out float materialMax))
                {
                    defaultMin = materialMin;
                    defaultMax = materialMax;
                    break;
                }
            }
        }

        return new DiNeLightingContext
        {
            MinLight = designer.MinLightValue,
            MaxLight = designer.MaxLightValue,
            DefaultMinLight = defaultMin,
            DefaultMaxLight = defaultMax,
            DefaultMonochrome = 0f,
            LightDirection = designer.LightDirection,
            OutlineTintFrom = designer.OutlineTintFrom,
            OutlineTintTo = designer.OutlineTintTo,
            OutlineWidthMax = designer.OutlineWidthMax,
            ReflectanceMax = designer.ReflectanceMax,
            Materials = materials,
        };
    }

    // ──────────────────────────────────────────────────
    //  애니메이터
    // ──────────────────────────────────────────────────

    private static void BuildAnimatorLayer(
        AnimatorController controller,
        DiNeLightingDesigner designer,
        DiNeLightingSink defaultSink,
        Dictionary<ControlKey, DiNeLightingSink> controlSinks)
    {
        // Direct Blend Tree의 가중치로 쓸 상수 1 파라미터.
        AddOrReplaceFloatParameter(controller, DiNeLightingDesigner.DirectParameterName, 1f);

        var controlTree = CreateBlendTree(controller, "Controls", BlendTreeType.Direct);
        SetNormalizedBlendValues(controlTree, false);

        foreach (var pair in controlSinks.OrderBy(item => item.Key.GroupIndex).ThenBy(item => (int)item.Key.Control))
        {
            var key = pair.Key;
            var def = DiNeLightingControlDef.Get(key.Control);
            string parameterName = BuildParameterName(designer, key);
            float initialValue = designer.GetSetting(key.Control)?.initialValue ?? def.DefaultInitialValue;

            // 1D 블렌드 파라미터는 Float여야 하므로 토글도 애니메이터 쪽은 Float로 만든다.
            AddOrReplaceFloatParameter(controller, parameterName, initialValue);

            var puppet = CreateBlendTree(controller, def.ParameterSuffix, BlendTreeType.Simple1D);
            puppet.blendParameter = parameterName;
            puppet.useAutomaticThresholds = false;
            foreach (var clip in pair.Value.Clips)
                puppet.AddChild(clip.Value, clip.Key);

            controlTree.AddChild(puppet);
        }

        ApplyDirectBlendParameter(controlTree, DiNeLightingDesigner.DirectParameterName);

        Motion rootMotion;
        if (designer.UseEnableToggle && !defaultSink.IsEmpty)
        {
            AddOrReplaceFloatParameter(controller, DiNeLightingDesigner.EnableParameterName, designer.EnableDefaultOn ? 1f : 0f);

            var enableTree = CreateBlendTree(controller, "Enable", BlendTreeType.Simple1D);
            enableTree.blendParameter = DiNeLightingDesigner.EnableParameterName;
            enableTree.useAutomaticThresholds = false;
            enableTree.AddChild(defaultSink.Clips.First().Value, 0f);
            enableTree.AddChild(controlTree, 1f);

            var root = CreateBlendTree(controller, "DiNe Lighting Root", BlendTreeType.Direct);
            SetNormalizedBlendValues(root, false);
            root.AddChild(enableTree);
            ApplyDirectBlendParameter(root, DiNeLightingDesigner.DirectParameterName);
            rootMotion = root;
        }
        else
        {
            rootMotion = controlTree;
        }

        var stateMachine = new AnimatorStateMachine { name = LayerName };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        var state = stateMachine.AddState("Lighting (WD ON)", new Vector3(280f, 120f));
        state.writeDefaultValues = true;
        state.motion = rootMotion;
        stateMachine.defaultState = state;

        controller.AddLayer(new AnimatorControllerLayer
        {
            name = LayerName,
            defaultWeight = 1f,
            stateMachine = stateMachine
        });
    }

    private static BlendTree CreateBlendTree(AnimatorController controller, string name, BlendTreeType type)
    {
        var tree = new BlendTree { name = name, blendType = type };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    /// <summary>Direct Blend Tree의 모든 자식이 같은 상수 파라미터를 가중치로 쓰게 한다.</summary>
    private static void ApplyDirectBlendParameter(BlendTree tree, string parameterName)
    {
        var children = tree.children;
        for (int i = 0; i < children.Length; i++)
            children[i].directBlendParameter = parameterName;
        tree.children = children;
    }

    /// <summary>Direct Blend Tree는 정규화를 끄지 않으면 자식 수만큼 값이 나뉘어 버린다.</summary>
    private static void SetNormalizedBlendValues(BlendTree tree, bool value)
    {
        using (var serialized = new SerializedObject(tree))
        {
            var property = serialized.FindProperty("m_NormalizedBlendValues");
            if (property != null)
            {
                property.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void AddOrReplaceFloatParameter(AnimatorController controller, string name, float defaultValue)
    {
        var existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing != null)
            controller.RemoveParameter(existing);

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = defaultValue
        });
    }

    public static void RemoveGeneratedLayers(AnimatorController controller)
    {
        if (controller == null) return;
        for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            string name = controller.layers[i].name;
            if (name == LayerName || name == PresetLayerName)
                controller.RemoveLayer(i);
        }
    }

    // ──────────────────────────────────────────────────
    //  프리셋
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 프리셋은 블렌드 트리가 아니라 VRC Parameter Driver로 구현한다.
    /// 버튼을 누르면 드라이버가 해당 슬라이더 파라미터들을 지정 값으로 옮기고 프리셋 값을 0으로 되돌린다.
    /// 슬라이더 파라미터 자체가 이미 동기화되므로 프리셋 파라미터는 동기화할 필요가 없다(0 bit).
    /// 프리셋을 누른 뒤에도 사용자가 슬라이더를 이어서 조절할 수 있다는 게 블렌드 트리 방식과의 차이.
    /// </summary>
    private static void BuildPresetLayer(
        AnimatorController controller,
        DiNeLightingDesigner designer,
        Dictionary<ControlKey, DiNeLightingSink> controlSinks)
    {
        var presets = designer.Presets.Where(preset => preset != null).ToList();
        if (presets.Count == 0)
            return;

        AddOrReplaceIntParameter(controller, DiNeLightingDesigner.PresetParameterName, 0);

        var stateMachine = new AnimatorStateMachine { name = PresetLayerName };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        var idle = stateMachine.AddState("Idle", new Vector3(280f, 60f));
        idle.writeDefaultValues = true;
        stateMachine.defaultState = idle;

        int created = 0;
        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            int presetValue = i + 1;   // 0은 "아무것도 누르지 않음"

            var entries = new List<VRC.SDKBase.VRC_AvatarParameterDriver.Parameter>();

            foreach (var pair in controlSinks)
            {
                if (!preset.TryGetValue(pair.Key.Control, out float value))
                    continue;

                entries.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                    name = BuildParameterName(designer, pair.Key),
                    value = Mathf.Clamp01(value)
                });
            }

            if (entries.Count == 0)
            {
                Debug.LogWarning(
                    $"[DiNe 라이팅 디자이너] 프리셋 '{preset.name}'에 적용할 항목이 없어 건너뜁니다.");
                continue;
            }

            // 프리셋 파라미터를 즉시 0으로 되돌려 같은 버튼을 다시 누를 수 있게 한다.
            entries.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
            {
                type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                name = DiNeLightingDesigner.PresetParameterName,
                value = 0f
            });

            var state = stateMachine.AddState(SafeStateName(preset.name, i), new Vector3(280f, 160f + i * 60f));
            state.writeDefaultValues = true;

            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = true;   // 결과 파라미터가 동기화되므로 로컬에서만 실행하면 충분하다.
            driver.parameters = entries;

            var enter = stateMachine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.canTransitionToSelf = false;
            enter.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.Equals,
                    parameter = DiNeLightingDesigner.PresetParameterName,
                    threshold = presetValue
                }
            };

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0f;
            exit.duration = 0f;

            created++;
        }

        if (created == 0)
        {
            // 만들어진 프리셋이 하나도 없으면 빈 레이어를 남기지 않는다.
            UnityEngine.Object.DestroyImmediate(stateMachine, true);
            return;
        }

        controller.AddLayer(new AnimatorControllerLayer
        {
            name = PresetLayerName,
            defaultWeight = 1f,
            stateMachine = stateMachine
        });
    }

    private static void AddOrReplaceIntParameter(AnimatorController controller, string name, int defaultValue)
    {
        var existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing != null)
            controller.RemoveParameter(existing);

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Int,
            defaultInt = defaultValue
        });
    }

    // ──────────────────────────────────────────────────
    //  그룹
    // ──────────────────────────────────────────────────

    /// <summary>렌더러 → 그룹 인덱스. 어느 그룹에도 없으면 -1.</summary>
    private static Dictionary<Renderer, int> BuildRendererGroupMap(
        DiNeLightingDesigner designer,
        List<Renderer> renderers)
    {
        var map = new Dictionary<Renderer, int>();
        foreach (var renderer in renderers)
            map[renderer] = -1;

        var groups = designer.Groups;
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group?.renderers == null) continue;

            foreach (var renderer in group.renderers)
            {
                if (renderer == null || !map.ContainsKey(renderer)) continue;

                if (map[renderer] >= 0)
                {
                    Debug.LogWarning(
                        $"[DiNe 라이팅 디자이너] '{renderer.name}'이(가) 여러 그룹에 들어 있습니다. " +
                        $"'{DiNeLightingDesigner.GetGroupDisplayName(groups[map[renderer]], map[renderer])}'만 적용합니다.");
                    continue;
                }

                map[renderer] = i;
            }
        }

        return map;
    }

    private static string BuildParameterName(DiNeLightingDesigner designer, ControlKey key)
    {
        if (key.GroupIndex < 0 || key.GroupIndex >= designer.Groups.Count)
            return DiNeLightingDesigner.BuildParameterName(key.Control);

        return DiNeLightingDesigner.BuildParameterName(key.Control, designer.Groups[key.GroupIndex], key.GroupIndex);
    }

    private static string SafeStateName(string name, int index)
    {
        string trimmed = string.IsNullOrWhiteSpace(name) ? "Preset" : name.Trim();
        return $"{GetSafeAnimatorName(trimmed)}_{index}";
    }

    private static string GetSafeAnimatorName(string name)
    {
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-')
                continue;
            chars[i] = '_';
        }
        var result = new string(chars).Trim();
        return string.IsNullOrEmpty(result) ? "Preset" : result;
    }

    /// <summary>제어 항목 + 그룹 인덱스(-1 = 공용)를 묶은 키.</summary>
    private readonly struct ControlKey : IEquatable<ControlKey>
    {
        public readonly DiNeLightingControl Control;
        public readonly int GroupIndex;

        public ControlKey(DiNeLightingControl control, int groupIndex)
        {
            Control = control;
            GroupIndex = groupIndex;
        }

        public bool Equals(ControlKey other) => Control == other.Control && GroupIndex == other.GroupIndex;
        public override bool Equals(object obj) => obj is ControlKey other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Control * 397) ^ GroupIndex);
    }

    // ──────────────────────────────────────────────────
    //  파라미터 / 메뉴
    // ──────────────────────────────────────────────────

    private static void InstallParameters(
        VRCExpressionParameters expressionParameters,
        DiNeLightingDesigner designer,
        Dictionary<ControlKey, DiNeLightingSink> controlSinks)
    {
        var list = expressionParameters.parameters != null
            ? expressionParameters.parameters.ToList()
            : new List<VRCExpressionParameters.Parameter>();

        // 이전 실행 잔여물 제거.
        list.RemoveAll(parameter => parameter != null && parameter.name != null &&
            parameter.name.StartsWith(DiNeLightingDesigner.ParameterPrefix, StringComparison.Ordinal));

        if (designer.UseEnableToggle)
        {
            list.Add(new VRCExpressionParameters.Parameter
            {
                name = DiNeLightingDesigner.EnableParameterName,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = designer.EnableDefaultOn ? 1f : 0f,
                saved = designer.EnableSaved,
                networkSynced = true
            });
        }

        foreach (var key in controlSinks.Keys.OrderBy(item => item.GroupIndex).ThenBy(item => (int)item.Control))
        {
            var def = DiNeLightingControlDef.Get(key.Control);
            var setting = designer.GetSetting(key.Control);

            list.Add(new VRCExpressionParameters.Parameter
            {
                name = BuildParameterName(designer, key),
                valueType = def.Kind == DiNeLightingControlKind.Toggle
                    ? VRCExpressionParameters.ValueType.Bool
                    : VRCExpressionParameters.ValueType.Float,
                defaultValue = setting?.initialValue ?? def.DefaultInitialValue,
                saved = setting?.saved ?? true,
                networkSynced = true
            });
        }

        // 프리셋 파라미터는 Parameter Driver가 로컬에서만 읽으면 되므로 동기화하지 않는다.
        // 드라이버가 바꾸는 슬라이더 파라미터들이 동기화되기 때문에 원격에도 결과가 그대로 전달된다.
        if (designer.Presets.Any(preset => preset != null))
        {
            list.Add(new VRCExpressionParameters.Parameter
            {
                name = DiNeLightingDesigner.PresetParameterName,
                valueType = VRCExpressionParameters.ValueType.Int,
                defaultValue = 0f,
                saved = false,
                networkSynced = false
            });
        }

        expressionParameters.parameters = list.ToArray();
    }

    private static void InstallMenu(
        VRCExpressionsMenu rootMenu,
        DiNeLightingDesigner designer,
        Dictionary<ControlKey, DiNeLightingSink> controlSinks,
        string generatedFolder)
    {
        if (rootMenu.controls == null)
            rootMenu.controls = new List<VRCExpressionsMenu.Control>();

        string menuName = string.IsNullOrWhiteSpace(designer.MenuName) ? "Lighting Designer" : designer.MenuName.Trim();

        var menu = CreateMenuAsset(generatedFolder, menuName);

        if (designer.UseEnableToggle)
        {
            AddControlWithPages(menu, new VRCExpressionsMenu.Control
            {
                name = "켜기 / 끄기",
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = DiNeLightingDesigner.EnableParameterName },
                value = 1f
            }, menuName, generatedFolder, 1);
        }

        AddPresetMenu(menu, designer, menuName, generatedFolder);

        // 공용 슬라이더는 메인 메뉴에, 그룹 전용 슬라이더는 그룹 서브메뉴에 넣는다.
        foreach (var key in controlSinks.Keys.Where(item => item.GroupIndex < 0).OrderBy(item => (int)item.Control))
        {
            AddControlWithPages(menu, BuildControl(designer, key), menuName, generatedFolder, 1);
        }

        var groupKeys = controlSinks.Keys.Where(item => item.GroupIndex >= 0)
            .GroupBy(item => item.GroupIndex)
            .OrderBy(group => group.Key);

        foreach (var group in groupKeys)
        {
            if (group.Key >= designer.Groups.Count) continue;

            var groupData = designer.Groups[group.Key];
            string groupName = DiNeLightingDesigner.GetGroupDisplayName(groupData, group.Key);
            var groupMenu = CreateMenuAsset(generatedFolder, groupName);

            foreach (var key in group.OrderBy(item => (int)item.Control))
                AddControlWithPages(groupMenu, BuildControl(designer, key), groupName, generatedFolder, 1);

            AddControlWithPages(menu, new VRCExpressionsMenu.Control
            {
                name = groupName,
                icon = groupData.icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = groupMenu
            }, menuName, generatedFolder, 1);
        }

        // 루트에 이미 붙어 있던 우리 항목은 제거하고 다시 건다.
        rootMenu.controls.RemoveAll(control =>
            control != null && control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.name == menuName);

        if (rootMenu.controls.Count >= 8)
        {
            Debug.LogError(
                $"[DiNe 라이팅 디자이너] 아바타 루트 메뉴가 8칸을 다 써서 '{menuName}' 서브메뉴를 추가하지 못했습니다.");
            return;
        }

        rootMenu.controls.Add(new VRCExpressionsMenu.Control
        {
            name = menuName,
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = menu,
            icon = designer.MenuIcon
        });
    }

    /// <summary>슬라이더/토글 메뉴 컨트롤 하나를 만든다.</summary>
    private static VRCExpressionsMenu.Control BuildControl(DiNeLightingDesigner designer, ControlKey key)
    {
        var def = DiNeLightingControlDef.Get(key.Control);
        var setting = designer.GetSetting(key.Control);
        string parameterName = BuildParameterName(designer, key);
        string label = designer.GetDisplayName(key.Control);

        if (def.Kind == DiNeLightingControlKind.Toggle)
        {
            return new VRCExpressionsMenu.Control
            {
                name = label,
                icon = setting?.icon,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName },
                value = 1f
            };
        }

        return new VRCExpressionsMenu.Control
        {
            name = label,
            icon = setting?.icon,
            type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
            subParameters = new[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = parameterName }
            }
        };
    }

    /// <summary>프리셋 버튼 서브메뉴. Toggle이 아니라 Button이어야 누를 때마다 다시 적용된다.</summary>
    private static void AddPresetMenu(
        VRCExpressionsMenu menu,
        DiNeLightingDesigner designer,
        string menuName,
        string generatedFolder)
    {
        var presets = designer.Presets.Where(preset => preset != null).ToList();
        if (presets.Count == 0)
            return;

        var presetMenu = CreateMenuAsset(generatedFolder, "프리셋");

        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            AddControlWithPages(presetMenu, new VRCExpressionsMenu.Control
            {
                name = string.IsNullOrWhiteSpace(preset.name) ? "프리셋 " + (i + 1) : preset.name.Trim(),
                icon = preset.icon,
                type = VRCExpressionsMenu.Control.ControlType.Button,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = DiNeLightingDesigner.PresetParameterName },
                value = i + 1
            }, "프리셋", generatedFolder, 1);
        }

        AddControlWithPages(menu, new VRCExpressionsMenu.Control
        {
            name = "프리셋",
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = presetMenu
        }, menuName, generatedFolder, 1);
    }

    private static VRCExpressionsMenu CreateMenuAsset(string folder, string name)
    {
        var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        menu.name = name;
        menu.controls = new List<VRCExpressionsMenu.Control>();
        AssetDatabase.CreateAsset(menu, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(name) + ".asset"));
        return menu;
    }

    /// <summary>메뉴 한 페이지는 8칸이므로, 넘치면 "다음 ▶" 서브메뉴로 이어붙인다.</summary>
    private static void AddControlWithPages(
        VRCExpressionsMenu menu,
        VRCExpressionsMenu.Control control,
        string baseName,
        string folder,
        int pageNumber)
    {
        if (menu.controls == null)
            menu.controls = new List<VRCExpressionsMenu.Control>();

        if (menu.controls.Count < 8)
        {
            menu.controls.Add(control);
            EditorUtility.SetDirty(menu);
            return;
        }

        var next = menu.controls.FirstOrDefault(item =>
            item.type == VRCExpressionsMenu.Control.ControlType.SubMenu && item.name == "다음 ▶" && item.subMenu != null);

        if (next == null)
        {
            var moved = menu.controls[7];
            menu.controls.RemoveAt(7);
            var nextMenu = CreateMenuAsset(folder, baseName + " " + (pageNumber + 1));
            next = new VRCExpressionsMenu.Control
            {
                name = "다음 ▶",
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = nextMenu
            };
            menu.controls.Add(next);
            nextMenu.controls.Add(moved);
        }

        AddControlWithPages(next.subMenu, control, baseName, folder, pageNumber + 1);
        EditorUtility.SetDirty(menu);
    }

    // ──────────────────────────────────────────────────
    //  잡동사니
    // ──────────────────────────────────────────────────

    private static void WarnOnLightLimitChanger(VRCAvatarDescriptor descriptor)
    {
        foreach (var component in descriptor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            if (component.GetType().Name != "LightLimitChangerSettings") continue;

            Debug.LogWarning(
                "[DiNe 라이팅 디자이너] 같은 아바타에 Light Limit Changer가 함께 설치돼 있습니다. " +
                "두 도구가 같은 셰이더 프로퍼티를 건드려 조명이 이상하게 보일 수 있으니 하나만 쓰세요.");
            return;
        }
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "LightingDesigner";
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
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
