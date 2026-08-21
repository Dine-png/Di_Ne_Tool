#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

/// <summary>
/// 업로드 전에 잡을 수 있는 문제들을 모아 인스펙터에 보여준다.
/// 특히 "빌드는 되는데 인게임에서 아무 일도 안 일어나는" 종류(셰이더 기능 토글 꺼짐, Poiyomi 잠금 등)를
/// 미리 잡는 게 목적이다.
/// </summary>
internal static class DiNeLightingDiagnostics
{
    private static string T(string korean, string english, string japanese)
    {
        return DiNeLightingLocalization.T(korean, english, japanese);
    }

    private static string DisplayName(DiNeLightingDesigner designer, DiNeLightingControl control)
    {
        var setting = designer.GetSetting(control);
        return setting != null && !string.IsNullOrWhiteSpace(setting.displayNameOverride)
            ? setting.displayNameOverride.Trim()
            : DiNeLightingLocalization.ControlName(control);
    }

    internal readonly struct Issue
    {
        public readonly MessageType Severity;
        public readonly string Message;

        public Issue(MessageType severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static List<Issue> Collect(DiNeLightingDesigner designer)
    {
        var issues = new List<Issue>();
        if (designer == null) return issues;

        var descriptor = designer.GetComponentInParent<VRCAvatarDescriptor>();
        if (descriptor == null)
        {
            issues.Add(new Issue(MessageType.Error,
                T(
                    "아바타(VRCAvatarDescriptor) 하위에 있어야 합니다. 아바타 안으로 옮겨주세요.",
                    "Lighting Designer must be under a VRCAvatarDescriptor. Move it inside the avatar.",
                    "Lighting DesignerはVRCAvatarDescriptorの子に配置してください。アバター内へ移動してください。")));
            return issues;
        }

        var enabledControls = designer.Controls
            .Where(setting => setting != null && setting.enabled)
            .Select(setting => setting.control)
            .ToList();

        if (enabledControls.Count == 0)
        {
            issues.Add(new Issue(MessageType.Warning,
                T(
                    "켜진 제어 항목이 없습니다. 최소 하나는 켜야 메뉴가 만들어집니다.",
                    "No controls are enabled. Enable at least one control to create a menu.",
                    "有効な制御項目がありません。メニューを作成するには1つ以上有効にしてください。")));
        }

        var targets = CollectTargetRenderers(descriptor, designer);
        CheckTargets(issues, targets);
        CheckControlSupport(issues, designer, enabledControls, targets);
        CheckFeatureToggles(issues, designer, enabledControls, targets);
        CheckLockedPoiyomi(issues, targets);
        CheckBudget(issues, designer, descriptor);
        CheckRootMenu(issues, designer, descriptor);
        CheckGroups(issues, designer, targets);
        CheckPresets(issues, designer);
        CheckLightLimitChanger(issues, descriptor);

        return issues;
    }

    // ──────────────────────────────────────────────────

    /// <summary>대상 렌더러와, 각 렌더러에 해당하는 프로파일.</summary>
    internal readonly struct Target
    {
        public readonly Renderer Renderer;
        public readonly Material[] Materials;
        public readonly List<DiNeShaderProfile> Profiles;

        public Target(Renderer renderer, Material[] materials, List<DiNeShaderProfile> profiles)
        {
            Renderer = renderer;
            Materials = materials;
            Profiles = profiles;
        }
    }

    public static List<Target> CollectTargetRenderers(VRCAvatarDescriptor descriptor, DiNeLightingDesigner designer)
    {
        var excluded = new HashSet<Renderer>(designer.Excludes.Where(item => item != null));
        var result = new List<Target>();

        foreach (var renderer in descriptor.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer)) continue;
            if (excluded.Contains(renderer)) continue;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.All(material => material == null)) continue;

            var profiles = DiNeShaderProfile.ResolveProfiles(materials, designer.TargetShaders);
            if (profiles.Count == 0) continue;

            result.Add(new Target(renderer, materials, profiles));
        }

        return result;
    }

    private static void CheckTargets(List<Issue> issues, List<Target> targets)
    {
        if (targets.Count == 0)
        {
            issues.Add(new Issue(MessageType.Error,
                T(
                    "대상 셰이더를 쓰는 렌더러를 찾지 못했습니다. 아바타의 셰이더가 lilToon / Poiyomi인지, 심화 설정의 '대상 셰이더'와 제외 목록이 맞는지 확인하세요.",
                    "No renderers using a target shader were found. Check that the avatar uses lilToon or Poiyomi and review Target Shaders and exclusions in Advanced settings.",
                    "対象シェーダーを使うレンダラーが見つかりません。アバターがlilToonまたはPoiyomiを使用しているか、詳細設定の対象シェーダーと除外リストを確認してください。")));
        }
    }

    /// <summary>켜진 항목을 이 아바타의 셰이더가 실제로 지원하는지.</summary>
    private static void CheckControlSupport(
        List<Issue> issues,
        DiNeLightingDesigner designer,
        List<DiNeLightingControl> enabledControls,
        List<Target> targets)
    {
        if (targets.Count == 0) return;

        var available = new HashSet<DiNeShaderProfile>(targets.SelectMany(target => target.Profiles));

        foreach (var control in enabledControls)
        {
            if (available.Any(profile => profile.Supports(control))) continue;

            issues.Add(new Issue(MessageType.Warning,
                T(
                    $"'{DisplayName(designer, control)}' 항목을 이 아바타의 셰이더가 지원하지 않습니다. 파라미터만 소모하고 아무 효과도 없습니다. (지원: {DiNeShaderProfile.DescribeSupport(control)})",
                    $"This avatar's shaders do not support '{DisplayName(designer, control)}'. It consumes parameters without any effect. (Supported by: {DiNeShaderProfile.DescribeSupport(control)})",
                    $"このアバターのシェーダーは「{DisplayName(designer, control)}」をサポートしていません。効果なくパラメーターだけを消費します。（対応: {DiNeShaderProfile.DescribeSupport(control)}）")));
        }
    }

    /// <summary>
    /// 셰이더 기능 토글이 꺼져 있어 슬라이더가 무의미해지는 경우.
    /// 예: lilToon에서 _UseOutline이 꺼져 있으면 아웃라인 두께를 아무리 움직여도 보이지 않는다.
    /// </summary>
    private static void CheckFeatureToggles(
        List<Issue> issues,
        DiNeLightingDesigner designer,
        List<DiNeLightingControl> enabledControls,
        List<Target> targets)
    {
        foreach (var control in enabledControls)
        {
            string toggle = null;
            bool anyProfileWants = false;
            bool anyMaterialOn = false;

            foreach (var target in targets)
            {
                foreach (var profile in target.Profiles)
                {
                    string required = profile.GetRequiredFeatureToggle(control);
                    if (string.IsNullOrEmpty(required)) continue;

                    anyProfileWants = true;
                    toggle = required;

                    foreach (var material in target.Materials)
                    {
                        if (material == null || !material.HasProperty(required)) continue;
                        if (material.GetFloat(required) > 0.5f)
                        {
                            anyMaterialOn = true;
                            break;
                        }
                    }

                    if (anyMaterialOn) break;
                }
                if (anyMaterialOn) break;
            }

            if (anyProfileWants && !anyMaterialOn)
            {
                issues.Add(new Issue(MessageType.Warning,
                    T(
                        $"'{DisplayName(designer, control)}' 항목이 켜져 있지만, 머티리얼에서 해당 기능({toggle})이 전부 꺼져 있어 인게임에서 아무 변화도 보이지 않습니다. 머티리얼에서 먼저 그 기능을 켜세요.",
                        $"'{DisplayName(designer, control)}' is enabled, but its material feature ({toggle}) is disabled on every target. Enable that feature in the materials first.",
                        $"「{DisplayName(designer, control)}」は有効ですが、すべての対象マテリアルで機能（{toggle}）が無効です。先にマテリアル側で有効にしてください。")));
            }
        }
    }

    private static void CheckLockedPoiyomi(List<Issue> issues, List<Target> targets)
    {
        var locked = targets
            .SelectMany(target => target.Materials)
            .Where(material => material != null && DiNeShaderProfilePoiyomi.IsLocked(material))
            .Select(material => material.name)
            .Distinct()
            .ToList();

        if (locked.Count == 0) return;

        string names = string.Join(", ", locked.Take(5));
        if (locked.Count > 5)
            names += T($" 외 {locked.Count - 5}개", $" and {locked.Count - 5} more", $" ほか{locked.Count - 5}個");

        issues.Add(new Issue(MessageType.Error,
            T(
                $"이미 잠긴(locked) Poiyomi 머티리얼이 있어 조절이 먹지 않습니다: {names}\n해당 머티리얼의 잠금을 해제한 뒤 업로드하세요. 업로드 시점에 Poiyomi가 자동으로 잠그는 것은 문제없습니다.",
                $"Some Poiyomi materials are already locked, so controls cannot affect them: {names}\nUnlock those materials before uploading. Poiyomi locking them automatically during upload is supported.",
                $"すでにロックされたPoiyomiマテリアルがあるため調整できません: {names}\nアップロード前にロックを解除してください。アップロード時にPoiyomiが自動ロックする場合は問題ありません。")));
    }

    private static void CheckBudget(List<Issue> issues, DiNeLightingDesigner designer, VRCAvatarDescriptor descriptor)
    {
        int cost = designer.CalculateParameterCost();
        int used = CalculateOtherCost(descriptor);
        int total = used + cost;
        int max = VRCExpressionParameters.MAX_PARAMETER_COST;

        if (total > max)
        {
            issues.Add(new Issue(MessageType.Error,
                T(
                    $"동기화 파라미터 예산을 초과했습니다: {total} / {max} bits. 이대로는 업로드되지 않습니다. 제어 항목이나 그룹 분리를 줄이세요.",
                    $"Synced parameter budget exceeded: {total} / {max} bits. Upload will fail. Reduce controls or separated group controls.",
                    $"同期パラメーターの上限を超えています: {total} / {max} bits。このままではアップロードできません。制御項目またはグループ分離を減らしてください。")));
        }
        else if (total > max - 8)
        {
            issues.Add(new Issue(MessageType.Warning,
                T(
                    $"동기화 파라미터가 거의 찼습니다: {total} / {max} bits. 다른 기능을 추가할 여유가 거의 없습니다.",
                    $"Synced parameters are nearly full: {total} / {max} bits. Very little room remains for other features.",
                    $"同期パラメーターがほぼ上限です: {total} / {max} bits。他の機能を追加する余裕がほとんどありません。")));
        }
    }

    public static int CalculateOtherCost(VRCAvatarDescriptor descriptor)
    {
        if (descriptor == null || descriptor.expressionParameters == null) return 0;

        // 임시 세션 잔여물 등으로 이미 우리 파라미터가 들어있으면 중복 계산하지 않는다.
        return descriptor.expressionParameters.parameters
            .Where(parameter => parameter != null && parameter.name != null &&
                !parameter.name.StartsWith(DiNeLightingDesigner.ParameterPrefix, System.StringComparison.Ordinal))
            .Where(parameter => parameter.networkSynced)
            .Sum(parameter => VRCExpressionParameters.TypeCost(parameter.valueType));
    }

    private static void CheckRootMenu(List<Issue> issues, DiNeLightingDesigner designer, VRCAvatarDescriptor descriptor)
    {
        var menu = descriptor.expressionsMenu;
        if (menu == null || menu.controls == null) return;

        string menuName = string.IsNullOrWhiteSpace(designer.MenuName) ? "Lighting Designer" : designer.MenuName.Trim();
        bool alreadyThere = menu.controls.Any(control =>
            control != null && control.name == menuName &&
            control.type == VRCExpressionsMenu.Control.ControlType.SubMenu);

        if (!alreadyThere && menu.controls.Count >= 8)
        {
            issues.Add(new Issue(MessageType.Error,
                T(
                    $"아바타 루트 메뉴가 8칸을 다 써서 '{menuName}' 서브메뉴를 넣을 자리가 없습니다. 루트 메뉴에서 항목을 하나 정리하거나 서브메뉴로 묶으세요.",
                    $"The avatar root menu already uses all 8 slots, so there is no room for the '{menuName}' submenu. Remove an item or group items into another submenu.",
                    $"アバターのルートメニューが8枠すべて使用されているため、「{menuName}」サブメニューを追加できません。項目を整理するか別のサブメニューへまとめてください。")));
        }
    }

    private static void CheckGroups(List<Issue> issues, DiNeLightingDesigner designer, List<Target> targets)
    {
        var targetSet = new HashSet<Renderer>(targets.Select(target => target.Renderer));
        var seen = new Dictionary<Renderer, int>();

        for (int i = 0; i < designer.Groups.Count; i++)
        {
            var group = designer.Groups[i];
            if (group == null) continue;

            string groupName = DiNeLightingDesigner.GetGroupDisplayName(group, i);
            var renderers = group.renderers.Where(renderer => renderer != null).ToList();

            if (group.separateControls.Count == 0)
            {
                issues.Add(new Issue(MessageType.Warning,
                    T(
                        $"그룹 '{groupName}'에 따로 조절할 항목이 없습니다. 아무 효과도 없으니 항목을 고르거나 그룹을 지우세요.",
                        $"Group '{groupName}' has no separately controlled items. Select controls or remove the group.",
                        $"グループ「{groupName}」に個別調整する項目がありません。項目を選ぶかグループを削除してください。")));
                continue;
            }

            if (renderers.Count == 0)
            {
                issues.Add(new Issue(MessageType.Warning,
                    T(
                        $"그룹 '{groupName}'에 렌더러가 없습니다. 파라미터만 소모합니다.",
                        $"Group '{groupName}' has no renderers and only consumes parameters.",
                        $"グループ「{groupName}」にレンダラーがなく、パラメーターだけを消費します。")));
                continue;
            }

            foreach (var renderer in renderers)
            {
                if (!targetSet.Contains(renderer))
                {
                    issues.Add(new Issue(MessageType.Warning,
                        T(
                            $"그룹 '{groupName}'의 '{renderer.name}'은(는) 대상 렌더러가 아닙니다. 제외 목록에 있거나 대상 셰이더를 쓰지 않습니다.",
                            $"'{renderer.name}' in group '{groupName}' is not a target renderer. It is excluded or does not use a target shader.",
                            $"グループ「{groupName}」の「{renderer.name}」は対象レンダラーではありません。除外されているか、対象シェーダーを使用していません。")));
                    continue;
                }

                if (seen.TryGetValue(renderer, out int firstGroup))
                {
                    issues.Add(new Issue(MessageType.Warning,
                        T(
                            $"'{renderer.name}'이(가) 여러 그룹에 들어 있습니다. '{DiNeLightingDesigner.GetGroupDisplayName(designer.Groups[firstGroup], firstGroup)}'만 적용됩니다.",
                            $"'{renderer.name}' is in multiple groups. Only '{DiNeLightingDesigner.GetGroupDisplayName(designer.Groups[firstGroup], firstGroup)}' will be applied.",
                            $"「{renderer.name}」が複数のグループに含まれています。「{DiNeLightingDesigner.GetGroupDisplayName(designer.Groups[firstGroup], firstGroup)}」のみ適用されます。")));
                }
                else
                {
                    seen.Add(renderer, i);
                }
            }
        }
    }

    private static void CheckPresets(List<Issue> issues, DiNeLightingDesigner designer)
    {
        for (int i = 0; i < designer.Presets.Count; i++)
        {
            var preset = designer.Presets[i];
            if (preset == null) continue;

            if (preset.controls.Count == 0)
            {
                string name = string.IsNullOrWhiteSpace(preset.name)
                    ? T("프리셋 ", "Preset ", "プリセット ") + (i + 1)
                    : preset.name;
                issues.Add(new Issue(MessageType.Warning,
                    T(
                        $"프리셋 '{name}'에 지정된 항목이 없습니다. 버튼을 눌러도 아무 일도 일어나지 않습니다.",
                        $"Preset '{name}' has no assigned controls, so pressing its button does nothing.",
                        $"プリセット「{name}」に指定された項目がないため、ボタンを押しても何も起こりません。")));
            }
        }
    }

    private static void CheckLightLimitChanger(List<Issue> issues, VRCAvatarDescriptor descriptor)
    {
        foreach (var component in descriptor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            if (component.GetType().Name != "LightLimitChangerSettings") continue;

            issues.Add(new Issue(MessageType.Warning,
                T(
                    "같은 아바타에 Light Limit Changer가 함께 설치돼 있습니다. 두 도구가 같은 셰이더 프로퍼티를 건드려 조명이 이상하게 보일 수 있으니 하나만 쓰세요.",
                    "Light Limit Changer is also installed on this avatar. Both tools modify the same shader properties, so use only one to avoid incorrect lighting.",
                    "同じアバターにLight Limit Changerも設定されています。両方が同じシェーダープロパティを変更するため、照明の不具合を避けるにはどちらか一方だけを使用してください。")));
            return;
        }
    }
}
#endif
