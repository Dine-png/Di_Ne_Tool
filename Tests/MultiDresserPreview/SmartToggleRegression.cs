#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor.Animations;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

// Uses real Unity animation/assets and production generators. The capture and VRChat SDK are stubs.
public static class SmartToggleRegression
{
    private static void Require(bool condition, string message) => MultiDresserPreviewRegression.Require(condition, message);
    private sealed class Fixture : IDisposable
    {
        public readonly GameObject root = new GameObject("Toggle regression avatar");
        public readonly VRCAvatarDescriptor avatar;
        public readonly AnimatorController controller;
        public readonly VRCExpressionsMenu rootMenu, dresserMenu, outfitMenu;
        public readonly VRCExpressionParameters parameters;
        public readonly DiNeSmartToggle toggle;
        public readonly string folder;
        public Fixture()
        {
            folder = "Assets/SmartToggleRegression_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            avatar = root.AddComponent<VRCAvatarDescriptor>();
            controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/FX.controller");
            controller.AddLayer("Outfit layer to preserve");
            rootMenu = Menu("Root"); dresserMenu = Menu("Multi Dresser"); outfitMenu = Menu("Clothes");
            rootMenu.controls.Add(Submenu("Multi Dresser", dresserMenu));
            dresserMenu.controls.Add(Submenu("Clothes", outfitMenu));
            parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            AssetDatabase.CreateAsset(parameters, folder + "/Parameters.asset");
            avatar.expressionParameters = parameters;
            avatar.expressionsMenu = rootMenu;
            var target = new GameObject("Accessory"); target.transform.SetParent(root.transform);
            toggle = target.AddComponent<DiNeSmartToggle>();
            toggle.DisplayName = "Accessory";
            toggle.ParameterName = "AccessoryEnabled";
            toggle.DefaultOn = false;
            toggle.Icon = new Texture2D(4, 4);
        }
        public VRCExpressionsMenu Menu(string name)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = name;
            AssetDatabase.CreateAsset(menu, folder + "/" + name + ".asset");
            return menu;
        }
        public void Apply(params DiNeSmartToggle[] toggles) => DiNeSmartToggleGenerator.ApplyToTemporaryAvatar(
            avatar, controller, rootMenu, parameters, toggles, folder + "/Generated");
        public void Dispose()
        {
            if (toggle.Icon != null && !EditorUtility.IsPersistent(toggle.Icon)) Object.DestroyImmediate(toggle.Icon);
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(folder);
        }
    }
    private static VRCExpressionsMenu.Control Submenu(string name, VRCExpressionsMenu menu) =>
        new VRCExpressionsMenu.Control { name = name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu };

    public static void MmdSwitching()
    {
        foreach (int originalCount in new[] { 0, 1, 2, 3, 5 })
        using (var fixture = new Fixture())
        {
            while (fixture.controller.layers.Length > originalCount)
                fixture.controller.RemoveLayer(fixture.controller.layers.Length - 1);
            while (fixture.controller.layers.Length < originalCount)
                fixture.controller.AddLayer("Original " + fixture.controller.layers.Length);
            var originals = fixture.controller.layers.Select(layer => layer.stateMachine).ToArray();
            var dresser = fixture.root.AddComponent<DiNeMultiDresser>();
            var first = new GameObject("Group first"); first.transform.SetParent(fixture.root.transform);
            var second = new GameObject("Group second"); second.transform.SetParent(fixture.root.transform); second.SetActive(false);
            var group = new DiNeMultiDresser.IndependentToggle {
                displayName = "Grouped accessory", parameterName = "GroupedEnabled", defaultOn = false
            };
            group.targets.Add(first); group.targets.Add(second); dresser.independentToggles.Add(group);
            Action apply = () => {
                DiNeSmartToggleGenerator.ApplyDresserTogglesToTemporaryAvatar(fixture.avatar, fixture.controller,
                    fixture.rootMenu, fixture.parameters, new[] { dresser }, fixture.folder + "/Groups");
                fixture.Apply(fixture.toggle);
            };
            apply();
            int count = fixture.controller.layers.Length;
            apply();
            Require(fixture.controller.layers.Length == count, "Repeated generation accumulated padding/toggles");
            for (int i = 0; i < originals.Length; i++)
                Require(fixture.controller.layers[i].stateMachine == originals[i], "Original FX layer order changed");

            var animator = fixture.root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("Independent toggle MMD regression");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, fixture.controller);
                var output = AnimationPlayableOutput.Create(graph, "FX", animator);
                output.SetSourcePlayable(playable); graph.Play();
                for (int phase = 0; phase < 3; phase++)
                {
                    for (int i = 1; i <= 2; i++) playable.SetLayerWeight(i, phase == 1 ? 0f : 1f);
                    foreach (bool grouped in new[] { false, true, false })
                    foreach (bool standalone in new[] { true, false, true })
                    {
                        playable.SetBool(group.parameterName, grouped);
                        playable.SetBool(fixture.toggle.ParameterName, standalone);
                        for (int frame = 0; frame < 5; frame++) graph.Evaluate(1f / 60f);
                        Require(first.activeSelf == grouped && second.activeSelf == grouped,
                            "Group lost On/Off with original FX count " + originalCount + ", phase " + phase);
                        Require(fixture.toggle.gameObject.activeSelf == standalone,
                            "Standalone toggle lost On/Off with original FX count " + originalCount + ", phase " + phase);
                        Require(playable.GetBool(group.parameterName) == grouped, "Group parameter changed");
                    }
                }
            }
            finally { graph.Destroy(); }
        }
    }

    public static void MmdBuildIntent()
    {
        var controlType = TypeCache.GetTypesDerivedFrom<StateMachineBehaviour>().FirstOrDefault(type =>
            type.FullName == "nadena.dev.modular_avatar.core.ModularAvatarMMDLayerControl");
        // The optional runner input uses MA's actual behaviour source, without a fake build pipeline.
        if (controlType == null) { Debug.Log("MA absent: MMD behaviour check not applicable"); return; }
        using (var fixture = new Fixture())
        {
            var dresser = fixture.root.AddComponent<DiNeMultiDresser>();
            var target = new GameObject("Group target"); target.transform.SetParent(fixture.root.transform);
            var group = new DiNeMultiDresser.IndependentToggle { displayName = "Group", parameterName = "GroupEnabled" };
            group.targets.Add(target); dresser.independentToggles.Add(group);
            DiNeSmartToggleGenerator.ApplyDresserTogglesToTemporaryAvatar(fixture.avatar, fixture.controller,
                fixture.rootMenu, fixture.parameters, new[] { dresser }, fixture.folder + "/Groups");
            fixture.Apply(fixture.toggle);
            for (int i = 0; i < 2; i++) fixture.controller.RemoveLayer(0);
            foreach (var layer in fixture.controller.layers.Where(layer =>
                layer.name.StartsWith("DiNe Independent Toggle/") || layer.name.StartsWith("DiNe Smart Toggle/")))
            {
                // Mimic earlier build passes removing original layers: protection must be explicit.
                var behaviour = layer.stateMachine.behaviours.SingleOrDefault(item => item.GetType() == controlType);
                Require(behaviour != null && !(bool)controlType.GetProperty("DisableInMMDMode").GetValue(behaviour),
                    layer.name + " is not explicitly preserved by MA in MMD mode");
                Require(layer.stateMachine.states.All(state => state.state.behaviours.All(item => item.GetType() != controlType)),
                    "MMD control must be attached to the layer, not individual states");
            }
        }
    }

    public static void IconReuse()
    {
        var go = new GameObject("Reuse_" + Guid.NewGuid().ToString("N"));
        var layer = new DiNeMultiDresser.DresserLayer();
        layer.EnsureSize(2); layer.targets[1] = go;
        string path = DiNeMultiIconGenerator.GetIconAssetPath(go.name);
        try
        {
            DiNeIconMaker.GenerateIcon(go, null, path, new DiNeIconMaker.Settings());
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            int captures = DiNeIconMaker.Captures;
            DiNeMultiIconGenerator.EnsureIcon(layer, 1);
            Require(layer.icons[1] == existing && captures == DiNeIconMaker.Captures, "Automatic assignment recaptured existing asset");
            Require(bytes.SequenceEqual(System.IO.File.ReadAllBytes(path)), "Existing PNG changed");
            DiNeMultiIconGenerator.RegenerateIcon(layer, 1);
            Require(DiNeIconMaker.Captures == captures + 1, "Explicit regeneration did not capture");
            DiNeIconMaker.FailCapture = true;
            var before = layer.icons[1];
            DiNeMultiIconGenerator.RegenerateIcon(layer, 1);
            Require(layer.icons[1] == before, "Failed capture cleared the previous reference");
            DiNeIconMaker.FailCapture = false;
            string renamed = AssetDatabase.GenerateUniqueAssetPath(path.Replace(".png", "_New.png"));
            try
            {
                DiNeMultiIconGenerator.RegenerateIcon(layer, 1, renamed);
                Require(AssetDatabase.GetAssetPath(layer.icons[1]) == renamed && System.IO.File.Exists(path), "New-name generation overwrote original");
            }
            finally { AssetDatabase.DeleteAsset(renamed); }
        }
        finally { DiNeIconMaker.FailCapture = false; AssetDatabase.DeleteAsset(path); Object.DestroyImmediate(go); }
        using (var fixture = new Fixture())
        {
            string smartPath = "Assets/Di Ne/SmartToggle/Icons/" + fixture.toggle.ParameterName + ".png";
            var previousIcon = fixture.toggle.Icon;
            Object.DestroyImmediate(previousIcon);
            fixture.toggle.Icon = null;
            try
            {
                DiNeIconMaker.GenerateIcon(fixture.toggle.gameObject, null, smartPath, new DiNeIconMaker.Settings());
                int captures = DiNeIconMaker.Captures;
                Texture2D existing = DiNeSmartToggleGenerator.EnsureIcon(fixture.toggle);
                Require(existing != null && fixture.toggle.Icon == existing && captures == DiNeIconMaker.Captures, "Smart Toggle recaptured existing icon");
            }
            finally { fixture.toggle.Icon = null; AssetDatabase.DeleteAsset(smartPath); }
        }
    }

    public static void Placement()
    {
        using (var fixture = new Fixture())
        {
            Require(fixture.toggle.Placement == DiNeSmartToggle.MenuPlacement.Root, "New toggles should be a direct root button");
            fixture.toggle.Placement = DiNeSmartToggle.MenuPlacement.DresserCategory;
            fixture.toggle.GroupName = "Clothes";
            fixture.Apply(fixture.toggle);
            var dresserCopy = fixture.rootMenu.controls.Single().subMenu;
            var outfitCopy = dresserCopy.controls.Single().subMenu;
            Require(dresserCopy != fixture.dresserMenu && outfitCopy != fixture.outfitMenu, "Original submenu asset was edited");
            Require(fixture.outfitMenu.controls.Count == 0, "Original outfit menu changed");
            var control = outfitCopy.controls.Single();
            Require(control.type == VRCExpressionsMenu.Control.ControlType.Toggle && control.subMenu == null &&
                control.parameter.name == "AccessoryEnabled" && control.value == 1, "Expected a direct Bool toggle");
            Require(fixture.parameters.parameters.Single().valueType == VRCExpressionParameters.ValueType.Bool, "Expression parameter is not Bool");
            var parameter = fixture.controller.parameters.Single();
            Require(parameter.type == AnimatorControllerParameterType.Bool && !parameter.defaultBool, "Animator Bool/default mismatch");
            var toggleLayer = fixture.controller.layers.Single(layer => layer.name.StartsWith("DiNe Smart Toggle/"));
            var states = toggleLayer.stateMachine.states.Select(state => state.state).ToArray();
            Require(states.Length == 2 && toggleLayer.stateMachine.defaultState.name == "Off", "Expected On/Off states and default Off");
            var off = states.Single(state => state.name == "Off"); var on = states.Single(state => state.name == "On");
            Require(off.transitions.Single().conditions.Single().mode == AnimatorConditionMode.If &&
                on.transitions.Single().conditions.Single().mode == AnimatorConditionMode.IfNot, "Bool transition conditions incorrect");
            ((AnimationClip)off.motion).SampleAnimation(fixture.root, 0);
            Require(!fixture.toggle.gameObject.activeSelf, "Off clip did not disable target");
            ((AnimationClip)on.motion).SampleAnimation(fixture.root, 0);
            Require(fixture.toggle.gameObject.activeSelf, "On clip did not enable target");
            Require(fixture.controller.layers.Any(layer => layer.name == "Outfit layer to preserve"), "Existing outfit layer was removed");
            fixture.Apply(fixture.toggle);
            Require(fixture.controller.layers.Count(layer => layer.name.StartsWith("DiNe Smart Toggle/")) == 1, "Repeated generation accumulated toggle layers");
            outfitCopy = fixture.rootMenu.controls.Single().subMenu.controls.Single().subMenu;
            Require(outfitCopy.controls.Count(control => control.parameter?.name == "AccessoryEnabled") == 1, "Repeated generation duplicated toggle controls");
        }
        using (var fixture = new Fixture())
        {
            fixture.toggle.Placement = DiNeSmartToggle.MenuPlacement.MultiDresser;
            fixture.Apply(fixture.toggle);
            var menu = fixture.rootMenu.controls.Single().subMenu;
            Require(menu.controls.Any(control => control.type == VRCExpressionsMenu.Control.ControlType.Toggle && control.subMenu == null), "Dresser root got a submenu instead of a toggle");
        }
    }

    public static void Pagination()
    {
        using (var fixture = new Fixture())
        {
            for (int i = 0; i < 8; i++) fixture.outfitMenu.controls.Add(new VRCExpressionsMenu.Control { name = "Outfit " + i });
            fixture.toggle.Placement = DiNeSmartToggle.MenuPlacement.DresserCategory;
            fixture.toggle.GroupName = "Clothes";
            fixture.Apply(fixture.toggle);
            var menu = fixture.rootMenu.controls.Single().subMenu.controls.Single().subMenu;
            Require(menu.controls.Count == 8 && fixture.outfitMenu.controls.Count == 8, "Page size/original menu changed");
            var next = menu.controls.Single(control => control.subMenu != null).subMenu;
            Require(next.controls.Count == 2 && next.controls.Any(control => control.parameter?.name == "AccessoryEnabled"), "Overflow toggle lost");
        }
    }

    public static void ParameterCollision()
    {
        using (var fixture = new Fixture())
        {
            fixture.controller.AddParameter("AccessoryEnabled", AnimatorControllerParameterType.Int);
            fixture.parameters.parameters = new[] { new VRCExpressionParameters.Parameter
                { name = "AccessoryEnabled", valueType = VRCExpressionParameters.ValueType.Int } };
            fixture.Apply(fixture.toggle);
            Require(fixture.controller.parameters.Single(parameter => parameter.name == "AccessoryEnabled").type == AnimatorControllerParameterType.Int,
                "Existing Animator Int was overwritten");
            Require(fixture.parameters.parameters.Single(parameter => parameter.name == "AccessoryEnabled").valueType == VRCExpressionParameters.ValueType.Int,
                "Existing expression Int was overwritten");
            Require(fixture.rootMenu.controls.Any(control => control.parameter?.name == "AccessoryEnabled_2"), "Expected unique Bool parameter");
        }
        using (var fixture = new Fixture())
        {
            fixture.controller.AddParameter("AccessoryEnabled", AnimatorControllerParameterType.Bool);
            fixture.parameters.parameters = new[] { new VRCExpressionParameters.Parameter {
                name = "AccessoryEnabled", valueType = VRCExpressionParameters.ValueType.Bool, defaultValue = 1f, saved = false } };
            fixture.Apply(fixture.toggle);
            Require(fixture.parameters.parameters.Single(item => item.name == "AccessoryEnabled").defaultValue == 1f &&
                !fixture.parameters.parameters.Single(item => item.name == "AccessoryEnabled").saved, "Existing Bool settings overwritten");
            Require(fixture.rootMenu.controls.Any(control => control.parameter?.name == "AccessoryEnabled_2"), "Existing Bool did not receive unique suffix");
            fixture.Apply(fixture.toggle);
            Require(fixture.parameters.parameters.Length == 2 && fixture.rootMenu.controls.Count(control => control.parameter?.name == "AccessoryEnabled_2") == 1,
                "Repeated collision apply changed suffix or duplicated controls");
        }
    }

    public static void ContextMenu()
    {
        using (var fixture = new Fixture())
        {
            var target = new GameObject("Right click target"); target.transform.SetParent(fixture.root.transform, false);
            Selection.activeGameObject = target;
            Require(DiNeSmartToggleMenu.ValidateAddSmartToggle(new MenuCommand(target)), "Context menu disabled");
            Require(EditorApplication.ExecuteMenuItem("GameObject/Di Ne/Smart Toggle"), "Command not registered");
            int undoGroup = Undo.GetCurrentGroup();
            var toggle = target.GetComponent<DiNeSmartToggle>();
            Require(toggle != null && toggle.Dresser == null && toggle.Placement == DiNeSmartToggle.MenuPlacement.Root, "Smart Toggle is not standalone");
            Require(Selection.activeGameObject == target && fixture.root.GetComponentsInChildren<DiNeMultiDresser>(true).Length == 0, "Command created a dresser or selected wrong object");
            DiNeSmartToggleMenu.AddSmartToggle(new MenuCommand(target));
            Require(target.GetComponents<DiNeSmartToggle>().Length == 1, "Repeated command duplicated component");
            Undo.RevertAllDownToGroup(undoGroup);
            Require(target != null && target.GetComponent<DiNeSmartToggle>() == null, "Undo removed target or kept component");
        }
    }

    public static void BatchCreation()
    {
        using (var fixture = new Fixture())
        {
            var go = new GameObject("Grouped dresser"); go.transform.SetParent(fixture.root.transform, false);
            var dresser = go.AddComponent<DiNeMultiDresser>();
            var first = new GameObject("First"); first.transform.SetParent(fixture.root.transform, false);
            var second = new GameObject("Second"); second.transform.SetParent(fixture.root.transform, false); second.SetActive(false);
            var outside = new GameObject("Outside");
            try
            {
                Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
                var group = DiNeIndependentToggleEditing.Create(dresser);
                group.menuPath = DiNeToggleMenuChoices.Segment("Multi Dresser") + "/" + DiNeToggleMenuChoices.Segment("Clothes");
                int count = DiNeIndependentToggleEditing.AddTargets(dresser, group, new[] { first, second, first, outside, fixture.root, fixture.toggle.gameObject, null });
                Require(count == 2 && group.targets.Count == 2, "Expected two objects in one toggle");
                Require(first.GetComponent<DiNeSmartToggle>() == null && second.GetComponent<DiNeSmartToggle>() == null, "Dresser added Smart Toggle components");
                Require(DiNeIndependentToggleEditing.AddTargets(dresser, group, new[] { first, second }) == 0, "Duplicate targets accepted");
                var other = DiNeIndependentToggleEditing.Create(dresser);
                Require(other.parameterName != group.parameterName && DiNeIndependentToggleEditing.AddTargets(dresser, other, new[] { first }) == 0, "Duplicate names or competing ownership");
                DiNeSmartToggleGenerator.ApplyDresserTogglesToTemporaryAvatar(fixture.avatar, fixture.controller, fixture.rootMenu,
                    fixture.parameters, new[] { dresser }, fixture.folder + "/Grouped");
                Require(fixture.parameters.parameters.Length == 1, "Grouped targets used more than one Bool");
                var layer = fixture.controller.layers.Single(item => item.name.StartsWith("DiNe Independent Toggle/"));
                var off = (AnimationClip)layer.stateMachine.states.Single(item => item.state.name == "Off").state.motion;
                var on = (AnimationClip)layer.stateMachine.states.Single(item => item.state.name == "On").state.motion;
                Require(AnimationUtility.GetCurveBindings(off).Length == 2 && AnimationUtility.GetCurveBindings(on).Length == 2, "Missing target curves");
                off.SampleAnimation(fixture.root, 0); Require(!first.activeSelf && !second.activeSelf, "Group Off did not disable both");
                on.SampleAnimation(fixture.root, 0); Require(first.activeSelf && second.activeSelf, "Group On did not enable both");
                var menu = fixture.rootMenu.controls.Single().subMenu.controls.Single().subMenu;
                Require(menu.controls.Count == 1 && fixture.outfitMenu.controls.Count == 0, "Wrong menu control count or original asset modified");
                fixture.Apply(fixture.toggle);
                Require(fixture.controller.layers.Count(item => item.name.StartsWith("DiNe Independent Toggle/")) == 1 &&
                    fixture.controller.layers.Count(item => item.name.StartsWith("DiNe Smart Toggle/")) == 1, "Generators removed each other's layers");
                var inspector = (DiNeMultiSupporter)Editor.CreateEditor(dresser);
                var window = ScriptableObject.CreateInstance<MultiDresserPreviewTestWindow>();
                int languageBefore = EditorPrefs.GetInt("DiNeLang", 0);
                try
                {
                    window.Inspector = inspector; window.position = new Rect(50, 50, 900, 2000); window.Show(); window.RenderFrame();
                    foreach (int language in new[] { 0, 1, 2 })
                    {
                        EditorPrefs.SetInt("DiNeLang", language);
                        string before = EditorJsonUtility.ToJson(dresser); string smartBefore = EditorJsonUtility.ToJson(fixture.toggle);
                        window.RenderFrame(); window.RenderFrame();
                        Require(window.Error == null, "Grouped inspector failed: " + window.Error);
                        Require(before == EditorJsonUtility.ToJson(dresser) && smartBefore == EditorJsonUtility.ToJson(fixture.toggle), "Idle inspector changed group or Smart Toggle");
                    }
                }
                finally { EditorPrefs.SetInt("DiNeLang", languageBefore); window.Inspector = null; window.Close(); Object.DestroyImmediate(inspector); }
                Undo.RevertAllDownToGroup(undoGroup);
                Require(dresser.independentToggles.Count == 0 && first != null && second != null, "Group Undo did not preserve targets/remove groups");
            }
            finally { Object.DestroyImmediate(outside); }
        }
    }

    public static void MenuDestinations()
    {
        using (var fixture = new Fixture())
        using (var choices = new DiNeToggleMenuChoices())
        {
            var existing = fixture.Menu("Existing"); var nested = fixture.Menu("Nested");
            fixture.rootMenu.controls.Add(Submenu("Accessories / Props", existing));
            existing.controls.Add(Submenu("Glasses", nested));
            nested.controls.Add(Submenu("Cycle", existing));
            fixture.rootMenu.controls.Add(Submenu("Accessories / Props", fixture.Menu("Duplicate")));
            var go = new GameObject("Dresser"); go.transform.SetParent(fixture.root.transform);
            var dresser = go.AddComponent<DiNeMultiDresser>();
            dresser.layers.Add(new DiNeMultiDresser.DresserLayer { layerName = "Pending Wardrobe" });
            string path = DiNeToggleMenuChoices.Segment("Accessories / Props") + "/" + DiNeToggleMenuChoices.Segment("Glasses");
            var all = choices.GetChoices(fixture.avatar);
            Require(all.Any(choice => choice.path == path && choice.label == "Accessories ／ Props › Glasses"), "Existing names are not relative to the containing avatar");
            Require(all.All(choice => !choice.label.Contains('/')), "Popup labels would create extra submenu levels");
            Require(all.Any(choice => choice.path == DiNeToggleMenuChoices.Segment("Accessories / Props", 1)), "Duplicate submenu not distinguishable");
            Require(all.Any(choice => choice.label.EndsWith("Pending Wardrobe") && choice.generated), "Configured dresser names absent");
            fixture.toggle.Placement = DiNeSmartToggle.MenuPlacement.ExistingMenu; fixture.toggle.MenuPath = path;
            fixture.Apply(fixture.toggle);
            var destination = fixture.rootMenu.controls.Single(control => control.name == "Accessories / Props" && control.subMenu.name == "Existing").subMenu.controls.Single().subMenu;
            Require(destination.controls.Any(control => control.parameter?.name == fixture.toggle.ParameterName), "Saved nested destination not used");
            Require(nested.controls.Count == 1 && existing.controls.Single().subMenu == nested, "Original nested menus mutated");
        }
        using (var fixture = new Fixture())
        {
            var go = new GameObject("Dresser"); go.transform.SetParent(fixture.root.transform);
            var dresser = go.AddComponent<DiNeMultiDresser>();
            dresser.layers.Add(new DiNeMultiDresser.DresserLayer { layerName = "Unbuilt Wardrobe" });
            var group = DiNeIndependentToggleEditing.Create(dresser);
            var target = new GameObject("Grouped prop"); target.transform.SetParent(fixture.root.transform);
            Require(DiNeIndependentToggleEditing.AddTargets(dresser, group, new[] { target }) == 1, "Cannot add group target");
            group.menuPath = DiNeToggleMenuChoices.Segment("Multi Dresser") + "/" + DiNeToggleMenuChoices.Segment("Unbuilt Wardrobe");
            DiNeSmartToggleGenerator.ApplyDresserTogglesToTemporaryAvatar(fixture.avatar, fixture.controller, fixture.rootMenu,
                fixture.parameters, new[] { dresser }, fixture.folder + "/Generated");
            var wardrobe = fixture.rootMenu.controls.Single().subMenu.controls.Single(control => control.name == "Unbuilt Wardrobe").subMenu;
            Require(wardrobe.controls.Single().parameter.name == group.parameterName && dresser.HasConfiguredContent(), "Empty wardrobe did not host independent toggle");
        }
    }

    public static void PrefilledNames()
    {
        using (var fixture = new Fixture())
        {
            fixture.avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer
                { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fixture.controller } };
            fixture.parameters.parameters = new[] {
                new VRCExpressionParameters.Parameter { name = "Reserved", valueType = VRCExpressionParameters.ValueType.Bool },
                new VRCExpressionParameters.Parameter { name = "Reserved_2", valueType = VRCExpressionParameters.ValueType.Int }
            };
            fixture.controller.AddParameter("FxOnly", AnimatorControllerParameterType.Bool);
            var dresserObject = new GameObject("Names dresser"); dresserObject.transform.SetParent(fixture.root.transform, false);
            var dresser = dresserObject.AddComponent<DiNeMultiDresser>();
            dresser.layers.Add(new DiNeMultiDresser.DresserLayer { layerName = "Pending" });
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(null, null, null, dresser) == "DiNe/ST_Toggle", "Empty creation field default missing");
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(fixture.toggle.gameObject, "Reserved", fixture.toggle, dresser) == "Reserved_3",
                "Existing Bool/Int names were not reserved");
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(fixture.toggle.gameObject, "FxOnly", fixture.toggle, dresser) == "FxOnly_2",
                "FX-only Bool name was ignored");
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(fixture.toggle.gameObject, "DiNe/MultiDresser/Pending", fixture.toggle, dresser) == "DiNe/MultiDresser/Pending_2",
                "Ungenerated dresser parameter was ignored");
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(fixture.toggle.gameObject, fixture.toggle.ParameterName, fixture.toggle, dresser) == fixture.toggle.ParameterName,
                "Own ungenerated name should not conflict with itself");
            var target = new GameObject("Named toggle"); target.transform.SetParent(fixture.root.transform, false);
            var created = DiNeSmartToggleEditor.CreateToggle(target, DiNeSmartToggle.MenuPlacement.MultiDresser, null, "Reserved", dresser);
            Require(created.ParameterName == "Reserved_3", "Corrected custom name was not saved on creation");
            var group = DiNeIndependentToggleEditing.Create(dresser);
            Require(group.parameterName == "DiNe/ST_Toggle", "Group name was not prefilled");
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(null, group.parameterName, null, dresser, group) == group.parameterName, "Own group name collided");
            Require(DiNeSmartToggleEditor.MakeUniqueParameterName(target, group.parameterName, created, dresser) == group.parameterName + "_2", "Group name not reserved for Smart Toggle");
        }
    }

    public static void Inspector()
    {
        using (var fixture = new Fixture())
        {
            var inspector = (DiNeSmartToggleEditor)Editor.CreateEditor(fixture.toggle);
            var window = ScriptableObject.CreateInstance<SmartToggleProbeWindow>();
            int previousLanguage = EditorPrefs.GetInt("DiNeLang", 0);
            try
            {
                window.Inspector = inspector;
                window.Show(); window.position = new Rect(50, 50, 650, 850);
                foreach (int language in new[] { 0, 1, 2 })
                {
                    EditorPrefs.SetInt("DiNeLang", language);
                    window.Repaint();
                    var before = EditorJsonUtility.ToJson(fixture.toggle);
                    for (int i = 0; i < 5; i++)
                    {
                        window.SendEvent(new Event { type = EventType.Layout });
                        window.SendEvent(new Event { type = EventType.Repaint });
                    }
                    Require(window.Error == null, "Smart Toggle inspector failed: " + window.Error);
                    Require(before == EditorJsonUtility.ToJson(fixture.toggle), "Idle inspector changed serialized data");
                }
            }
            finally { EditorPrefs.SetInt("DiNeLang", previousLanguage); window.Inspector = null; window.Close(); Object.DestroyImmediate(inspector); }
        }
    }
}
public sealed class SmartToggleProbeWindow : EditorWindow
{
    public Editor Inspector;
    public Exception Error;
    private void OnGUI()
    {
        if (Inspector == null) return;
        GUI.changed = false;
        try { Inspector.OnInspectorGUI(); }
        catch (Exception exception) { Error = exception; }
    }
}
#endif
