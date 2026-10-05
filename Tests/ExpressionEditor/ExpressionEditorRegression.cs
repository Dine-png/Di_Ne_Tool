#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiNeTool.ExpressionEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;
using Control = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

public static class ExpressionEditorRegression
{
    private static readonly List<string> Results = new List<string>();
    private static readonly List<Object> TransientObjects = new List<Object>();
    private static string caseFolder;
    private static int tests, failures;

    public static void Run()
    {
        // Let the production InitializeOnLoad/delayCall registration finish naturally.
        // No test-only registry mutation or explicit registration precedes selection.
        EditorApplication.delayCall += () => EditorApplication.delayCall += RunSuite;
    }

    private static void RunSuite()
    {
        Results.Add("Menu SDK assembly: " + typeof(VRCExpressionsMenu).Assembly.GetName().Name);
        Results.Add("Parameters SDK assembly: " + typeof(VRCExpressionParameters).Assembly.GetName().Name);
        Results.Add("Avatar SDK assembly: " + typeof(VRCAvatarDescriptor).Assembly.GetName().Name);
        Test("Real SDK types are used and VRCSDK+ is absent", RealSdkTypes);
        Test("Control cloning isolates nested parameters, labels and arrays", DeepControlClone);
        Test("Menu edits enforce the SDK limit and preserve invalid input", MenuLimitsAndInvalidInputs);
        Test("Menu reorder preserves control values and supports Undo/Redo", MenuOrderUndo);
        Test("Failed cross-menu move is atomic and successful move preserves values", CrossMenuMove);
        Test("Cross-menu move is one Undo/Redo transaction", CrossMenuUndo);
        Test("Submenu scan terminates across cycles and handles null controls", CyclicMenus);
        Test("Parameter cloning preserves every SDK public field", ParameterClone);
        Test("Parameter merge keeps existing conflicts, first source duplicates and source state", ParameterMerge);
        Test("Over-budget merge rejects every planned addition atomically", ParameterMergeBudget);
        Test("Unsynced parameters merge without consuming network budget", UnsyncedParameters);
        Test("Null parameter slots retain their real SDK budget cost", NullParameterBudget);
        Test("SDK total parameter count limit rejects additions atomically", ParameterCountLimit);
        Test("Parameter cleanup preserves same-name conflicting definitions", CleanupConflicts);
        Test("Parameter add enforces name, duplicate and network budget", AddParameterValidation);
        Test("Parameter add, merge and cleanup support Undo/Redo", ParameterUndo);
        Test("SDK isEmpty survives cloning and edits", EmptyParameterState);
        Test("Animator parameters are collected from actual descriptor controllers", AnimatorParameters);
        Test("Read-only assets refuse mutation without changing source state", ReadOnlyAssets);
        Test("SDK cost handling of damaged parameter arrays is observed without mutation", ObserveSdkNullCost);
        Test("Unity selects SDK+ inspectors when enabled and actual native SDK roots when disabled", InspectorSelection);
        Test("Opening native inspectors preserves intentional and pristine empty parameter assets", EmptyInspectorInitialization);
        Test("Inspector priority registration is idempotent and preserves SDK records", RegistryIdempotence);
        Test("SDK+ editor callbacks duplicate, move, append merge, name cleanup and Controller Add with Undo", ConnectedInspectorActions);
        Test("Descriptor Quick Setup preserves FX/menu/parameters references and supports Undo", QuickSetupReferences);
        Test("SDK+ advisory controller lookup preserves Trigger and conflicting names during cleanup", InspectorAnimatorParameters);
        // UI Toolkit binding, list virtualization and layout need real Editor update
        // frames. The final case completes asynchronously before exiting Unity.
        tests++;
        ExpressionEditorUiRegression.Verify(error =>
        {
            string name = "Actual SDK+ and native inspector roots render at three widths and languages without mutation; visible row buttons support Undo";
            if (error == null) Results.Add("PASS " + name);
            else { failures++; Results.Add("FAIL " + name + ": " + error); }
            Debug.Log(Results[Results.Count - 1]);
            CompleteSuite();
        });
    }

    private static void CompleteSuite()
    {
        Results.Add("Tests: " + tests);
        Results.Add("Failures: " + failures);
        File.WriteAllLines("ExpressionEditorRegression-results.txt", Results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Test(string name, Action test)
    {
        tests++;
        Undo.ClearAll();
        TransientObjects.Clear();
        caseFolder = "Assets/Case_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(caseFolder));
        try { test(); Results.Add("PASS " + name); }
        catch (Exception e) { failures++; Results.Add("FAIL " + name + ": " + e); }
        finally
        {
            foreach (Object item in TransientObjects) if (item != null) Object.DestroyImmediate(item);
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(caseFolder);
        }
        Debug.Log(Results[Results.Count - 1]);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T Asset<T>(string name) where T : ScriptableObject
    {
        T item = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(item, caseFolder + "/" + name + ".asset");
        return item;
    }

    private static VRCExpressionsMenu Menu(string name, params Control[] controls)
    {
        var item = Asset<VRCExpressionsMenu>(name);
        item.controls = new List<Control>(controls);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static VRCExpressionParameters Parameters(string name, params Parameter[] parameters)
    {
        var item = Asset<VRCExpressionParameters>(name);
        item.parameters = parameters;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static Parameter P(string name, ValueType type = ValueType.Bool, float value = 0f, bool saved = true, bool synced = true)
    {
        return new Parameter { name = name, valueType = type, defaultValue = value, saved = saved, networkSynced = synced };
    }

    private static Control C(string name)
    {
        return new Control { name = name, type = Control.ControlType.Toggle, parameter = new Control.Parameter { name = name + "Param" }, value = 1f };
    }

    private static string Snapshot(Object item) => EditorJsonUtility.ToJson(item);

    private static void BeginUndo() { Undo.IncrementCurrentGroup(); }
    private static void UndoNow() { Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); }

    private static void RealSdkTypes()
    {
        Require(typeof(VRCExpressionsMenu).Assembly.GetName().Name == "VRCSDK3A", "Expression types were replaced by stubs.");
        Require(typeof(VRCAvatarDescriptor).Assembly.GetName().Name == "VRCSDK3A", "AvatarDescriptor did not come from the installed runtime assembly.");
        Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.IndexOf("vrcsdkplus", StringComparison.OrdinalIgnoreCase) >= 0), "VRCSDK+ implementation was imported.");
    }

    private static void DeepControlClone()
    {
        var submenu = Menu("Submenu");
        var icon = new Texture2D(2, 2); TransientObjects.Add(icon);
        var original = C("Puppet");
        original.type = Control.ControlType.FourAxisPuppet;
        original.icon = icon;
        original.subMenu = submenu;
        original.subParameters = new[] { new Control.Parameter { name = "X" }, null, new Control.Parameter { name = "Y" } };
        original.labels = new[] { new Control.Label { name = "Left", icon = icon }, default(Control.Label), new Control.Label { name = "Right" } };
        var clone = DiNeExpressionUtility.CloneControl(original);
        Require(clone != null && !ReferenceEquals(original, clone), "Control was not copied.");
        Require(clone.subMenu == submenu && clone.icon == icon, "Unity asset references changed.");
        Require(!ReferenceEquals(original.parameter, clone.parameter), "Main parameter aliases source.");
        Require(!ReferenceEquals(original.subParameters, clone.subParameters) && !ReferenceEquals(original.subParameters[0], clone.subParameters[0]), "Subparameters alias source.");
        Require(!ReferenceEquals(original.labels, clone.labels), "Label array aliases source.");
        Require(clone.labels[1].name == null && clone.subParameters[1] == null, "Empty label or null subparameter entries changed.");
        clone.parameter.name = "Changed"; clone.subParameters[0].name = "ChangedX"; clone.labels[0].name = "ChangedLabel";
        Require(original.parameter.name == "PuppetParam" && original.subParameters[0].name == "X" && original.labels[0].name == "Left", "Clone mutation changed source.");
        var noArrays = DiNeExpressionUtility.CloneControl(new Control { parameter = null, labels = null, subParameters = null });
        Require(noArrays.parameter == null && noArrays.labels == null && noArrays.subParameters == null, "Null members were normalized during cloning.");
        Require(DiNeExpressionUtility.CloneControl(null) == null, "Null control clone changed state.");
    }

    private static void MenuLimitsAndInvalidInputs()
    {
        Require(DiNeExpressionUtility.MenuLimit == 8, "Menu control limit differs from SDK.");
        var menu = Menu("Menu");
        for (int i = 0; i < 8; i++) Require(DiNeExpressionUtility.AddControl(menu, C("Item" + i), "Add"), "Valid add rejected at " + i);
        string before = Snapshot(menu);
        Require(!DiNeExpressionUtility.AddControl(menu, C("Overflow"), "Overflow") && Snapshot(menu) == before, "Overflow changed menu.");
        Require(!DiNeExpressionUtility.AddControl(menu, null, "Null") && Snapshot(menu) == before, "Null add changed menu.");
        Require(!DiNeExpressionUtility.RemoveControl(menu, -1, "Invalid") && !DiNeExpressionUtility.RemoveControl(menu, 8, "Invalid"), "Invalid removal succeeded.");
        Require(!DiNeExpressionUtility.MoveControl(menu, -1, 0, "Invalid") && !DiNeExpressionUtility.MoveControl(menu, 0, 8, "Invalid"), "Invalid reorder succeeded.");
        Require(Snapshot(menu) == before, "Invalid menu input mutated state.");
        Require(DiNeExpressionUtility.RemoveControl(menu, 7, "Remove") && menu.controls.Count == 7, "Valid remove rejected.");
    }

    private static void MenuOrderUndo()
    {
        var menu = Menu("Menu", C("A"), C("B"), C("C"));
        string before = Snapshot(menu);
        BeginUndo();
        Require(DiNeExpressionUtility.MoveControl(menu, 0, 2, "Move"), "Reorder rejected.");
        string after = Snapshot(menu);
        Require(string.Join(",", menu.controls.Select(c => c.name)) == "B,C,A", "Reorder lost expected values.");
        UndoNow(); Require(Snapshot(menu) == before, "Undo did not restore menu order.");
        Undo.PerformRedo(); Require(Snapshot(menu) == after, "Redo did not restore menu order.");
        BeginUndo(); Require(DiNeExpressionUtility.AddControl(menu, C("D"), "Add"), "Undoable add rejected.");
        UndoNow(); Require(Snapshot(menu) == after, "Undo did not remove added control.");
        BeginUndo(); Require(DiNeExpressionUtility.RemoveControl(menu, 1, "Remove"), "Undoable remove rejected.");
        UndoNow(); Require(Snapshot(menu) == after, "Undo did not restore removed control.");
    }

    private static void CrossMenuMove()
    {
        var item = C("Transfer"); item.labels = new[] { new Control.Label { name = "Label" } };
        var source = Menu("Source", item, C("Keep"));
        var full = Menu("Full", Enumerable.Range(0, 8).Select(i => C("Full" + i)).ToArray());
        string sourceBefore = Snapshot(source), destBefore = Snapshot(full);
        Require(!DiNeExpressionUtility.MoveControlToMenu(source, 0, full, "Fail"), "Move into full menu succeeded.");
        Require(Snapshot(source) == sourceBefore && Snapshot(full) == destBefore, "Failed move partially changed source or destination.");
        var dest = Menu("Destination");
        Require(DiNeExpressionUtility.MoveControlToMenu(source, 0, dest, "Transfer"), "Valid transfer rejected.");
        Require(source.controls.Count == 1 && source.controls[0].name == "Keep" && dest.controls.Count == 1, "Transfer changed unexpected controls.");
        Require(dest.controls[0].parameter.name == "TransferParam" && dest.controls[0].labels[0].name == "Label", "Transfer lost control settings.");
        Require(!DiNeExpressionUtility.MoveControlToMenu(source, 0, source, "Self"), "Same menu move unexpectedly changed assets.");
    }

    private static void CrossMenuUndo()
    {
        var source = Menu("Source", C("A"), C("B")); var dest = Menu("Destination", C("C"));
        string sourceBefore = Snapshot(source), destBefore = Snapshot(dest);
        BeginUndo(); Require(DiNeExpressionUtility.MoveControlToMenu(source, 1, dest, "Transfer"), "Move rejected.");
        string sourceAfter = Snapshot(source), destAfter = Snapshot(dest);
        UndoNow(); Require(Snapshot(source) == sourceBefore && Snapshot(dest) == destBefore, "One Undo did not restore both assets.");
        Undo.PerformRedo(); Require(Snapshot(source) == sourceAfter && Snapshot(dest) == destAfter, "Redo did not restore both assets.");
    }

    private static void CyclicMenus()
    {
        VRCExpressionsMenu a = Menu("A"), b = Menu("B"), c = Menu("C"), outside = Menu("Outside");
        a.controls.Add(new Control { type = Control.ControlType.SubMenu, subMenu = b });
        b.controls.Add(null); b.controls.Add(new Control { type = Control.ControlType.SubMenu, subMenu = c });
        c.controls.Add(new Control { type = Control.ControlType.SubMenu, subMenu = a });
        string before = Snapshot(a) + Snapshot(b) + Snapshot(c);
        Require(DiNeExpressionUtility.ContainsMenu(a, a) && DiNeExpressionUtility.ContainsMenu(a, c), "Reachable menu not found.");
        Require(!DiNeExpressionUtility.ContainsMenu(a, outside), "Outside menu found through cycle.");
        Require(!DiNeExpressionUtility.ContainsMenu(null, a) && !DiNeExpressionUtility.ContainsMenu(a, null), "Null menu input accepted.");
        Require(Snapshot(a) + Snapshot(b) + Snapshot(c) == before, "Cycle analysis mutated menus.");
    }

    private static void ParameterClone()
    {
        var original = P("State", ValueType.Int, 73f, false, false);
        var clone = DiNeExpressionUtility.CloneParameter(original);
        Require(clone != null && !ReferenceEquals(original, clone), "Parameter was not cloned.");
        foreach (FieldInfo field in typeof(Parameter).GetFields(BindingFlags.Public | BindingFlags.Instance))
            Require(Equals(field.GetValue(original), field.GetValue(clone)), "Cloning lost SDK parameter field " + field.Name);
        clone.name = "Changed"; Require(original.name == "State", "Parameter clone aliases source.");
        Require(DiNeExpressionUtility.CloneParameter(null) == null, "Null parameter clone changed state.");
    }

    private static void ParameterMerge()
    {
        var target = Parameters("Target", P("A", ValueType.Bool));
        var source = Parameters("Source", P("A", ValueType.Float, .8f, false), P("B", ValueType.Int, 7f, false), P("B", ValueType.Float), P(""), null, P("C"));
        string before = Snapshot(source);
        Require(DiNeExpressionUtility.MergeParameters(target, source, "Merge") == 2, "Merge did not add exactly two unique valid names.");
        Require(target.parameters[0].name == "A" && target.parameters[0].valueType == ValueType.Bool && target.parameters[0].saved, "Existing same-name configuration changed.");
        Require(target.parameters[1].name == "B" && target.parameters[1].valueType == ValueType.Int && target.parameters[1].defaultValue == 7f, "First source definition did not win.");
        Require(Snapshot(source) == before && !ReferenceEquals(target.parameters[1], source.parameters[1]), "Merge changed source or retained managed aliases.");
        target.parameters[1].name = "ChangedCopy"; Require(source.parameters[1].name == "B", "Merged parameter aliases source.");
    }

    private static void ParameterMergeBudget()
    {
        var target = Parameters("Target", Enumerable.Range(0, 31).Select(i => P("P" + i, ValueType.Int)).ToArray());
        var source = Parameters("Source", P("ExtraA", ValueType.Int), P("ExtraB", ValueType.Int));
        Require(target.CalcTotalCost() == 248, "Unexpected real SDK budget cost.");
        string targetBefore = Snapshot(target), sourceBefore = Snapshot(source);
        Require(DiNeExpressionUtility.MergeParameters(target, source, "Overflow") == -1, "Over-budget merge succeeded.");
        Require(Snapshot(target) == targetBefore && Snapshot(source) == sourceBefore, "Budget failure applied a partial merge.");
        Require(DiNeExpressionUtility.MergeParameters(target, Parameters("One", P("ExtraA", ValueType.Int)), "ExactLimit") == 1 && target.CalcTotalCost() == 256, "Exact budget limit rejected.");
    }

    private static void UnsyncedParameters()
    {
        var target = Parameters("Target", Enumerable.Range(0, 32).Select(i => P("P" + i, ValueType.Int)).ToArray());
        var source = Parameters("Source", P("Local", ValueType.Float, .5f, true, false));
        Require(DiNeExpressionUtility.MergeParameters(target, source, "MergeLocal") == 1 && target.CalcTotalCost() == 256, "Local-only parameter consumed network cost.");
        Require(!target.parameters.Last().networkSynced, "Local parameter sync flag changed.");
    }

    private static void CleanupConflicts()
    {
        var target = Parameters("Target", P("A"), P("A"), P("A", ValueType.Int), P("A", ValueType.Bool, 1f), P("A", ValueType.Bool, 0f, false), P("A", ValueType.Bool, 0f, true, false), P(""), null, P("B"));
        Require(DiNeExpressionUtility.CleanupParameters(target, "Cleanup") == 3, "Cleanup did not remove only duplicate and empty entries.");
        Require(target.parameters.Length == 6 && target.parameters.Count(p => p.name == "A") == 5, "Cleanup removed conflicting definitions.");
        Require(target.parameters.Last().name == "B", "Cleanup changed valid parameter order.");
        Require(DiNeExpressionUtility.CleanupParameters(target, "Again") == 0, "Cleanup is not idempotent.");
    }

    private static void AddParameterValidation()
    {
        var target = Parameters("Target");
        Require(DiNeExpressionUtility.AddParameter(target, "Valid", ValueType.Bool, "Add"), "Valid parameter add rejected.");
        string before = Snapshot(target);
        Require(!DiNeExpressionUtility.AddParameter(target, "Valid", ValueType.Float, "Duplicate") && !DiNeExpressionUtility.AddParameter(target, "", ValueType.Bool, "Empty"), "Invalid name/duplicate accepted.");
        Require(Snapshot(target) == before, "Rejected parameter add mutated target.");
        target.parameters = Enumerable.Range(0, 32).Select(i => P("P" + i, ValueType.Int)).ToArray();
        before = Snapshot(target);
        Require(!DiNeExpressionUtility.AddParameter(target, "Overflow", ValueType.Bool, "Overflow") && Snapshot(target) == before, "Parameter overflow mutated target.");
    }

    private static void ParameterUndo()
    {
        var target = Parameters("Target", P("A"));
        string before = Snapshot(target);
        BeginUndo(); Require(DiNeExpressionUtility.AddParameter(target, "B", ValueType.Int, "Add"), "Add rejected.");
        string after = Snapshot(target); UndoNow(); Require(Snapshot(target) == before, "Add Undo failed.");
        Undo.PerformRedo(); Require(Snapshot(target) == after, "Add Redo failed.");
        BeginUndo(); Require(DiNeExpressionUtility.MergeParameters(target, Parameters("Source", P("C")), "Merge") == 1, "Merge rejected.");
        UndoNow(); Require(Snapshot(target) == after, "Merge Undo failed.");
        target.parameters = new[] { P("A"), P("A"), P("B") }; EditorUtility.SetDirty(target);
        before = Snapshot(target); BeginUndo(); Require(DiNeExpressionUtility.CleanupParameters(target, "Cleanup") == 1, "Cleanup rejected.");
        after = Snapshot(target); UndoNow(); Require(Snapshot(target) == before, "Cleanup Undo failed.");
        Undo.PerformRedo(); Require(Snapshot(target) == after, "Cleanup Redo failed.");
    }

    private static void EmptyParameterState()
    {
        var empty = Parameters("Empty", P(""), null);
        empty.isEmpty = false;
        BeginUndo();
        Require(DiNeExpressionUtility.CleanupParameters(empty, "Clear") == 2 && empty.parameters.Length == 0 && empty.isEmpty, "Cleanup did not mark intentional empty parameters.");
        UndoNow(); Require(empty.parameters.Length == 2 && !empty.isEmpty, "Undo did not restore isEmpty and data together.");
        Undo.PerformRedo(); Require(empty.parameters.Length == 0 && empty.isEmpty, "Redo did not restore intentional empty state.");
        BeginUndo(); Require(DiNeExpressionUtility.AddParameter(empty, "Named", ValueType.Bool, "Add") && !empty.isEmpty, "Add did not clear isEmpty.");
        UndoNow(); Require(empty.parameters.Length == 0 && empty.isEmpty, "Add Undo did not restore intentional empty state.");
        var pristine = Parameters("Pristine"); pristine.isEmpty = false;
        DiNeExpressionUtility.CleanupParameters(pristine, "ClearPristine");
        Require(pristine.isEmpty, "Empty cleanup did not protect against SDK default repopulation.");
    }

    private static void AnimatorParameters()
    {
        var avatar = new GameObject("Isolated avatar"); TransientObjects.Add(avatar);
        var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
        var controller = AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/FX.controller");
        controller.AddParameter("Toggle", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Amount", AnimatorControllerParameterType.Float);
        controller.AddParameter("Choice", AnimatorControllerParameterType.Int);
        controller.AddParameter("Pulse", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Conflict", AnimatorControllerParameterType.Bool);
        descriptor.customizeAnimationLayers = true;
        descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = controller } };
        var rootController = AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/Root.controller");
        rootController.AddParameter("Toggle", AnimatorControllerParameterType.Bool);
        rootController.AddParameter("RootOnly", AnimatorControllerParameterType.Int);
        rootController.AddParameter("Conflict", AnimatorControllerParameterType.Float);
        var overrides = new AnimatorOverrideController(rootController); TransientObjects.Add(overrides);
        avatar.AddComponent<Animator>().runtimeAnimatorController = overrides;
        string descriptorBefore = Snapshot(descriptor);
        var result = DiNeExpressionUtility.GetAnimatorParameters(descriptor);
        Require(result.Count(p => p.name == "Toggle") == 1 && result.Any(p => p.name == "Amount") && result.Any(p => p.name == "Choice") && result.Any(p => p.name == "RootOnly"), "Actual custom FX/root override controller parameters were not collected.");
        Require(!result.Any(p => p.name == "Pulse" || p.name == "Conflict"), "Trigger or same-name type conflict was exposed as an expression suggestion.");
        Require(result.Select(p => p.name).SequenceEqual(result.Select(p => p.name).OrderBy(n => n, StringComparer.Ordinal)), "Suggestions were not sorted deterministically.");
        Require(Snapshot(descriptor) == descriptorBefore && descriptor.expressionsMenu == null && descriptor.expressionParameters == null, "Parameter lookup changed avatar FX/menu/parameter references.");
        Require(DiNeExpressionUtility.GetAnimatorParameters(null).Count == 0, "Null avatar produced parameters.");
    }

    private static void ReadOnlyAssets()
    {
        const string modeKey = "DiNeExpressionInspectorEnabled";
        bool hadMode = EditorPrefs.HasKey(modeKey), oldMode = EditorPrefs.GetBool(modeKey, true);
        var menu = Menu("ReadOnly", C("A")); var parameters = Parameters("ReadOnlyParameters", P("A"));
        AssetDatabase.SaveAssets();
        string menuPath = AssetDatabase.GetAssetPath(menu), parametersPath = AssetDatabase.GetAssetPath(parameters);
        string menuBefore = Snapshot(menu), parametersBefore = Snapshot(parameters);
        File.SetAttributes(menuPath, File.GetAttributes(menuPath) | FileAttributes.ReadOnly);
        File.SetAttributes(parametersPath, File.GetAttributes(parametersPath) | FileAttributes.ReadOnly);
        try
        {
            Require(!DiNeExpressionUtility.CanEditAsset(menu) && !DiNeExpressionUtility.CanEditAsset(parameters), "Read-only assets reported writable.");
            Require(!DiNeExpressionUtility.AddControl(menu, C("B"), "Add") && !DiNeExpressionUtility.RemoveControl(menu, 0, "Remove"), "Read-only menu mutation allowed.");
            Require(!DiNeExpressionUtility.AddParameter(parameters, "B", ValueType.Bool, "Add") && DiNeExpressionUtility.MergeParameters(parameters, Parameters("Source", P("B")), "Merge") == -1, "Read-only parameter mutation allowed.");
            foreach (bool enhanced in new[] { false, true })
            foreach (Object item in new Object[] { menu, parameters })
            {
                EditorPrefs.SetBool(modeKey, enhanced);
                var editor = UnityEditor.Editor.CreateEditor(item);
                try
                {
                    Require(editor.CreateInspectorGUI() != null, "Locked inspector created no root.");
                    var field = typeof(DiNeExpressionInspectorBase).GetField("sdkEditor", BindingFlags.NonPublic | BindingFlags.Instance);
                    Require(field.GetValue(editor) == null, "Locked asset was passed into SDK initialization.");
                }
                finally { Object.DestroyImmediate(editor); }
            }
            Require(Snapshot(menu) == menuBefore && Snapshot(parameters) == parametersBefore, "Read-only rejection changed assets.");
        }
        finally
        {
            File.SetAttributes(menuPath, File.GetAttributes(menuPath) & ~FileAttributes.ReadOnly);
            File.SetAttributes(parametersPath, File.GetAttributes(parametersPath) & ~FileAttributes.ReadOnly);
            if (hadMode) EditorPrefs.SetBool(modeKey, oldMode); else EditorPrefs.DeleteKey(modeKey);
        }
    }

    private static void InspectorSelection()
    {
        const string modeKey = "DiNeExpressionInspectorEnabled";
        bool hadMode = EditorPrefs.HasKey(modeKey), oldMode = EditorPrefs.GetBool(modeKey, true);
        try
        {
            EditorPrefs.SetBool(modeKey, true);
            var menu = Menu("InspectorMenu", C("A")); var parameters = Parameters("InspectorParameters", P("A"));
            foreach (var item in new Object[] { menu, parameters })
            {
                string before = Snapshot(item);
                var editor = UnityEditor.Editor.CreateEditor(item);
                try
                {
                    DescribeEditorRegistry(item.GetType());
                    string expected = item is VRCExpressionsMenu ? "DiNeTool.ExpressionEditor.DiNeExpressionMenuEditor" : "DiNeTool.ExpressionEditor.DiNeExpressionParametersEditor";
                    Require(editor.GetType().FullName == expected, "Unity selected " + editor.GetType().FullName + " instead of " + expected);
                    var root = editor.CreateInspectorGUI();
                    Require(root != null, "Enhanced SDK inspector did not create its root.");
                    ExpressionEditorUiRegression.AssertInspectorMode(root, item, EditorPrefs.GetBool(modeKey, true));
                    EditorPrefs.SetBool(modeKey, false);
                    root = editor.CreateInspectorGUI();
                    Require(root != null, "SDK-only mode did not create its root.");
                    ExpressionEditorUiRegression.AssertInspectorMode(root, item, EditorPrefs.GetBool(modeKey, true));
                    var field = typeof(DiNeExpressionInspectorBase).GetField("sdkEditor", BindingFlags.NonPublic | BindingFlags.Instance);
                    var sdkEditor = (UnityEditor.Editor)field.GetValue(editor);
                    Require(sdkEditor != null, "SDK-only mode used generic serialized fields despite real SDK editors being installed.");
                    string sdkName = item is VRCExpressionsMenu ? "VRCExpressionsMenuEditor" : "VRCExpressionParametersEditor";
                    Require(sdkEditor.GetType().Name == sdkName, "Unexpected SDK fallback editor type: " + sdkEditor.GetType().FullName);
                    Require(Snapshot(item) == before, "Creating inspector/fallback mutated its asset.");
                    EditorPrefs.SetBool(modeKey, true);
                }
                finally { Object.DestroyImmediate(editor); }
            }
        }
        finally
        {
            if (hadMode) EditorPrefs.SetBool(modeKey, oldMode); else EditorPrefs.DeleteKey(modeKey);
        }
    }

    private static void EmptyInspectorInitialization()
    {
        const string modeKey = "DiNeExpressionInspectorEnabled";
        bool hadMode = EditorPrefs.HasKey(modeKey), oldMode = EditorPrefs.GetBool(modeKey, true);
        try
        {
            foreach (bool intentionalEmpty in new[] { false, true })
            foreach (bool enhanced in new[] { false, true })
            {
                var parameters = Parameters("Empty_" + intentionalEmpty + "_" + enhanced);
                parameters.isEmpty = intentionalEmpty;
                EditorPrefs.SetBool(modeKey, enhanced);
                string before = Snapshot(parameters);
                var editor = UnityEditor.Editor.CreateEditor(parameters);
                try
                {
                    Require(editor.CreateInspectorGUI() != null, "Empty parameter inspector created no root.");
                    Require(parameters.parameters.Length == 0 && parameters.isEmpty == intentionalEmpty,
                        "SDK initialization populated defaults or changed the empty flag.");
                    Require(Snapshot(parameters) == before, "Opening the inspector changed the empty asset.");
                }
                finally { Object.DestroyImmediate(editor); }
                Require(Snapshot(parameters) == before, "Closing the inspector changed the empty asset.");
            }
        }
        finally
        {
            if (hadMode) EditorPrefs.SetBool(modeKey, oldMode); else EditorPrefs.DeleteKey(modeKey);
        }
    }

    private static void ConnectedInspectorActions()
    {
        const string modeKey = "DiNeExpressionInspectorEnabled";
        bool hadMode = EditorPrefs.HasKey(modeKey), oldMode = EditorPrefs.GetBool(modeKey, true);
        bool hadLanguage = EditorPrefs.HasKey("DiNeLang"); int oldLanguage = EditorPrefs.GetInt("DiNeLang", 0);
        string oldClipboard = EditorGUIUtility.systemCopyBuffer;
        UnityEditor.Editor menuEditor = null, parameterEditor = null, destinationEditor = null;
        GameObject avatarObject = null;
        try
        {
            EditorPrefs.SetBool(modeKey, true); EditorPrefs.SetInt("DiNeLang", 0);
            var menu = Menu("ConnectedMenu", C("Original01"));
            var destination = Menu("ConnectedDestination");
            menuEditor = UnityEditor.Editor.CreateEditor(menu);
            ExpressionEditorUiRegression.AssertInspectorMode(menuEditor.CreateInspectorGUI(), menu, true);
            string menuBefore = Snapshot(menu);
            BeginUndo();
            menuEditor.serializedObject.FindProperty("controls").GetArrayElementAtIndex(0)
                .FindPropertyRelative("name").stringValue = "Edited01";
            InvokeEditorAction(menuEditor, "DuplicateControl", menu.controls[0], false);
            Require(menu.controls.Count == 2 && menu.controls[0].name == "Edited01" && menu.controls[1].name == "Edited02",
                "Connected Duplicate omitted a pending name edit or smart suffix increment.");
            Require(!ReferenceEquals(menu.controls[0], menu.controls[1]), "Connected Duplicate aliases its source.");
            UndoNow();
            Require(menu.controls.Count == 1, "Connected Duplicate Undo did not remove its inserted row.");
            // Pending field edits and the explicit duplicate are separate Unity Undo groups.
            if (Snapshot(menu) != menuBefore) UndoNow();
            Require(Snapshot(menu) == menuBefore, "Undo did not restore the pending edit and duplicate.");
            BeginUndo();
            InvokeEditorAction(menuEditor, "CopyControl", menu.controls[0]);
            InvokeEditorAction(menuEditor, "PasteControl", menu.controls[0], true);
            Require(menu.controls.Count == 2 && menu.controls[1].name == "Original01", "Paste as new failed to insert after the selected row.");
            UndoNow(); Require(Snapshot(menu) == menuBefore, "Paste as new Undo failed.");
            destinationEditor = UnityEditor.Editor.CreateEditor(destination);
            destinationEditor.CreateInspectorGUI();
            string destinationBefore = Snapshot(destination);
            InvokeEditorAction(menuEditor, "BeginMove", menu.controls[0]);
            InvokeEditorAction(destinationEditor, "PlaceControl", null, false);
            Require(menu.controls.Count == 0 && destination.controls.Count == 1, "Move/Place into an empty menu failed to remove the source row.");
            UndoNow();
            Require(Snapshot(menu) == menuBefore && Snapshot(destination) == destinationBefore, "One Move/Place Undo did not restore both assets.");

            var budgeted = Parameters("ConnectedOverBudget", Enumerable.Range(0, 32).Select(index => P("Budget" + index, ValueType.Int)).ToArray());
            Require(budgeted.CalcTotalCost() == 256, "Inline Add fixture did not start at the SDK memory limit.");
            DiNeExpressionUtility.SetMenuParameters(menu, budgeted); menuEditor.serializedObject.Update();
            string budgetBefore = Snapshot(budgeted), lookupMenuBefore = Snapshot(menu);
            BeginUndo(); InvokeEditorAction(menuEditor, "AddMissingParameter", budgeted, "Original01Param", ValueType.Float, menu.controls[0]);
            Require(budgeted.parameters.Length == 33 && budgeted.CalcTotalCost() == 264 && budgeted.parameters.Last().name == "Original01Param" &&
                budgeted.parameters.Last().valueType == ValueType.Float && budgeted.parameters.Last().saved && budgeted.parameters.Last().networkSynced,
                "SDK+ menu inline Add retained a budget gate or lost Saved/Synced defaults.");
            Require(Snapshot(menu) == lookupMenuBefore && DiNeExpressionUtility.GetMenuParameters(menu) == budgeted,
                "Inline Add reassigned menu parameter lookup or changed its control.");
            UndoNow(); Require(Snapshot(budgeted) == budgetBefore && Snapshot(menu) == lookupMenuBefore, "Inline Add Undo failed.");
            DiNeExpressionUtility.SetMenuParameters(menu, null); menuEditor.serializedObject.Update();
            Require(Snapshot(menu) == menuBefore, "Inline Add fixture lookup reference did not restore.");

            var parameters = Parameters("ConnectedParameters", P("A"), P("A"), P("A", ValueType.Int), P(""));
            parameterEditor = UnityEditor.Editor.CreateEditor(parameters);
            ExpressionEditorUiRegression.AssertInspectorMode(parameterEditor.CreateInspectorGUI(), parameters, true);
            string parametersBefore = Snapshot(parameters);
            BeginUndo(); InvokeEditorAction(parameterEditor, "CleanupParameters");
            Require(parameters.parameters.Length == 1 && parameters.parameters[0].name == "A" && parameters.parameters[0].valueType == ValueType.Bool,
                "SDK+ cleanup did not retain the first name while removing blank and duplicate names.");
            UndoNow(); Require(Snapshot(parameters) == parametersBefore, "SDK+ cleanup Undo failed.");
            var source = Parameters("ConnectedMergeSource", P("A", ValueType.Float, 0.25f, false, false), P("B", ValueType.Float));
            string sourceBefore = Snapshot(source);
            BeginUndo(); InvokeEditorAction(parameterEditor, "MergeParameters", source);
            Require(parameters.parameters.Length == 6 && parameters.parameters.Count(parameter => parameter.name == "A") == 4 &&
                parameters.parameters[4].saved == source.parameters[0].saved && parameters.parameters[4].networkSynced == source.parameters[0].networkSynced &&
                parameters.parameters[4].defaultValue == source.parameters[0].defaultValue,
                "SDK+ merge did not append duplicate names and preserve source settings.");
            Require(!ReferenceEquals(parameters.parameters[4], source.parameters[0]) && Snapshot(source) == sourceBefore,
                "SDK+ merge aliases or modifies its source.");
            UndoNow(); Require(Snapshot(parameters) == parametersBefore, "SDK+ append merge Undo failed.");
            BeginUndo(); InvokeEditorAction(parameterEditor, "DuplicateParameter", 0);
            Require(parameters.parameters.Length == 5 && parameters.parameters[1].name != "A", "Parameter Duplicate did not produce a unique name.");
            UndoNow(); Require(Snapshot(parameters) == parametersBefore, "Parameter Duplicate Undo failed.");
            BeginUndo(); InvokeEditorAction(parameterEditor, "AddParameter");
            Require(parameters.parameters.Length == 5, "Parameter footer Add callback failed.");
            UndoNow(); Require(Snapshot(parameters) == parametersBefore, "Parameter Add Undo failed.");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/ConnectedFX.controller");
            avatarObject = new GameObject("Connected Avatar");
            var descriptor = avatarObject.AddComponent<VRCAvatarDescriptor>();
            descriptor.expressionsMenu = menu; descriptor.expressionParameters = parameters;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = controller } };
            descriptor.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            string descriptorBefore = Snapshot(descriptor), controllerBefore = Snapshot(controller);
            BeginUndo();
            Require(((DiNeExpressionInspectorBase)parameterEditor).AddPlayableParameter(controller, "ControllerAdded", ValueType.Int, 7),
                "Explicit Add-to-Controller rejected valid input.");
            var added = controller.parameters.Single(parameter => parameter.name == "ControllerAdded");
            Require(added.type == AnimatorControllerParameterType.Int && added.defaultInt == 7, "Controller Add lost type or default.");
            Require(Snapshot(descriptor) == descriptorBefore && descriptor.expressionsMenu == menu && descriptor.expressionParameters == parameters &&
                descriptor.baseAnimationLayers[0].animatorController == controller && !descriptor.baseAnimationLayers[0].isDefault,
                "Controller Add changed AvatarDescriptor references.");
            Require(!((DiNeExpressionInspectorBase)parameterEditor).AddPlayableParameter(controller, "ControllerAdded", ValueType.Bool, 0),
                "Controller Add permitted a same-name duplicate.");
            UndoNow(); Require(Snapshot(controller) == controllerBefore && Snapshot(descriptor) == descriptorBefore, "Controller Add Undo or reference preservation failed.");
        }
        finally
        {
            if (menuEditor != null) Object.DestroyImmediate(menuEditor);
            if (parameterEditor != null) Object.DestroyImmediate(parameterEditor);
            if (destinationEditor != null) Object.DestroyImmediate(destinationEditor);
            if (avatarObject != null) Object.DestroyImmediate(avatarObject);
            EditorGUIUtility.systemCopyBuffer = oldClipboard;
            if (hadMode) EditorPrefs.SetBool(modeKey, oldMode); else EditorPrefs.DeleteKey(modeKey);
            if (hadLanguage) EditorPrefs.SetInt("DiNeLang", oldLanguage); else EditorPrefs.DeleteKey("DiNeLang");
        }
    }

    private static void InvokeEditorAction(UnityEditor.Editor editor, string methodName, params object[] arguments)
    {
        var method = editor.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Connected editor action is unavailable: " + methodName);
        var apply = typeof(DiNeExpressionInspectorBase).GetMethod("ApplyAction", BindingFlags.Instance | BindingFlags.NonPublic);
        apply.Invoke(editor, new object[] { (Action)(() => method.Invoke(editor, arguments)) });
    }

    private static void QuickSetupReferences()
    {
        var avatar = new GameObject("Quick Setup Avatar"); TransientObjects.Add(avatar);
        avatar.AddComponent<Animator>();
        var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
        var left = new GameObject("LeftEye").transform; left.SetParent(avatar.transform); left.localPosition = new Vector3(-0.03f, 1.5f, 0.08f);
        var right = new GameObject("RightEye").transform; right.SetParent(avatar.transform); right.localPosition = new Vector3(0.03f, 1.5f, 0.08f);
        var body = new GameObject("Body"); body.transform.SetParent(avatar.transform);
        var renderer = body.AddComponent<SkinnedMeshRenderer>();
        var mesh = new Mesh(); TransientObjects.Add(mesh);
        mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
        mesh.AddBlendShapeFrame("Blink", 100, new Vector3[3], new Vector3[3], new Vector3[3]); renderer.sharedMesh = mesh;
        var controller = AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/QuickSetupFX.controller");
        var menu = Menu("QuickSetupMenu", C("A")); var parameters = Parameters("QuickSetupParameters", P("A"));
        descriptor.expressionsMenu = menu; descriptor.expressionParameters = parameters;
        descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = controller } };
        descriptor.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
        string before = Snapshot(descriptor), menuBefore = Snapshot(menu), parametersBefore = Snapshot(parameters), controllerBefore = Snapshot(controller);
        BeginUndo(); DiNeExpressionQuickSetup.Apply(descriptor);
        using (var serialized = new SerializedObject(descriptor))
        {
            serialized.Update(); var eyes = serialized.FindProperty("customEyeLookSettings");
            Require(serialized.FindProperty("enableEyeLook").boolValue && eyes.FindPropertyRelative("leftEye").objectReferenceValue == left &&
                eyes.FindPropertyRelative("rightEye").objectReferenceValue == right, "Quick Setup did not bind the synthetic eye transforms.");
            Require(eyes.FindPropertyRelative("eyelidsSkinnedMesh").objectReferenceValue == renderer &&
                eyes.FindPropertyRelative("eyelidType").enumValueIndex == 2 &&
                eyes.FindPropertyRelative("eyelidsBlendshapes").GetArrayElementAtIndex(0).intValue == 0,
                "Quick Setup did not select the synthetic Blink blend shape.");
            Require(serialized.FindProperty("ViewPosition").vector3Value.y > 1, "Quick Setup did not position the synthetic avatar view.");
        }
        Require(descriptor.expressionsMenu == menu && descriptor.expressionParameters == parameters &&
            descriptor.baseAnimationLayers[0].animatorController == controller && !descriptor.baseAnimationLayers[0].isDefault &&
            Snapshot(menu) == menuBefore && Snapshot(parameters) == parametersBefore && Snapshot(controller) == controllerBefore,
            "Quick Setup changed FX/menu/parameters references or source assets.");
        UndoNow(); Require(Snapshot(descriptor) == before, "Quick Setup Undo did not restore the complete descriptor.");
    }

    private static void InspectorAnimatorParameters()
    {
        const string key = "DiNeExpressionInspectorEnabled";
        bool present = EditorPrefs.HasKey(key), previous = EditorPrefs.GetBool(key, true);
        var avatar = new GameObject("Advisory Lookup Avatar"); TransientObjects.Add(avatar);
        var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
        var fx = AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/AdvisoryFX.controller");
        fx.AddParameter("Pulse", AnimatorControllerParameterType.Trigger);
        fx.AddParameter("Conflict", AnimatorControllerParameterType.Bool);
        var rootController = AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/AdvisoryRoot.controller");
        rootController.AddParameter("Conflict", AnimatorControllerParameterType.Float);
        rootController.AddParameter("Pulse", AnimatorControllerParameterType.Trigger);
        var inner = new AnimatorOverrideController(rootController); TransientObjects.Add(inner);
        var outer = new AnimatorOverrideController(inner); TransientObjects.Add(outer);
        avatar.AddComponent<Animator>().runtimeAnimatorController = outer;
        var menu = Menu("AdvisoryMenu", C("A"));
        var parameters = Parameters("AdvisoryParameters", P("Pulse"), P("Conflict"), P("Unknown"), P(""));
        descriptor.expressionsMenu = menu; descriptor.expressionParameters = parameters;
        descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer {
            type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = fx } };
        descriptor.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
        string descriptorBefore = Snapshot(descriptor), fxBefore = Snapshot(fx), rootBefore = Snapshot(rootController), parametersBefore = Snapshot(parameters);
        var lookup = DiNeExpressionInspectorBase.GetInspectorAnimatorParameters(descriptor);
        Require(lookup.Count(parameter => parameter.name == "Pulse") == 1 && lookup.Single(parameter => parameter.name == "Pulse").type == AnimatorControllerParameterType.Trigger &&
            lookup.Count(parameter => parameter.name == "Conflict") == 2, "SDK+ lookup discarded Trigger or conflicting types.");
        UnityEditor.Editor editor = null;
        try
        {
            EditorPrefs.SetBool(key, true); editor = UnityEditor.Editor.CreateEditor(parameters); editor.CreateInspectorGUI();
            BeginUndo(); InvokeEditorAction(editor, "CleanupParameters");
            Require(string.Join(",", parameters.parameters.Select(parameter => parameter.name)) == "Pulse,Conflict",
                "Cleanup treated existing Trigger/conflict controller names as absent.");
            UndoNow(); Require(Snapshot(parameters) == parametersBefore, "Advisory cleanup Undo failed.");
            Require(Snapshot(descriptor) == descriptorBefore && Snapshot(fx) == fxBefore && Snapshot(rootController) == rootBefore && descriptor.expressionsMenu == menu &&
                descriptor.expressionParameters == parameters && descriptor.baseAnimationLayers[0].animatorController == fx && !descriptor.baseAnimationLayers[0].isDefault,
                "Advisory lookup/cleanup changed source controller or avatar references.");
        }
        finally
        {
            if (editor != null) Object.DestroyImmediate(editor);
            if (present) EditorPrefs.SetBool(key, previous); else EditorPrefs.DeleteKey(key);
        }
    }
    private static void ObserveSdkNullCost()
    {
        var asset = Parameters("Damaged", P("A"), null);
        foreach (var entries in new[] { asset.parameters, null })
        {
            asset.parameters = entries;
            string before = Snapshot(asset);
            try { Results.Add("SDK CalcTotalCost " + (entries == null ? "null array" : "null entry") + ": " + asset.CalcTotalCost()); }
            catch (Exception e) { Results.Add("SDK CalcTotalCost " + (entries == null ? "null array" : "null entry") + ": " + e.GetType().Name); }
            Require(Snapshot(asset) == before, "SDK cost observation changed data.");
        }
    }

    private static void NullParameterBudget()
    {
        var items = Enumerable.Range(0, 31).Select(i => P("P" + i, ValueType.Float)).ToList();
        items.Add(null);
        var target = Parameters("Target", items.ToArray());
        string before = Snapshot(target);
        Require(target.CalcTotalCost() == 256, "Installed SDK no longer charges serialized empty slots eight bits; update compatibility regression.");
        Require(DiNeExpressionUtility.MergeParameters(target, Parameters("Source", P("Extra")), "Merge") == -1, "Null slot cost was dropped during budget validation.");
        Require(!DiNeExpressionUtility.AddParameter(target, "Extra", ValueType.Bool, "Add"), "Parameter add omitted null-slot cost.");
        Require(Snapshot(target) == before, "Null-slot budget rejection changed target.");
    }

    private static void ParameterCountLimit()
    {
        var field = typeof(VRCExpressionParameters).GetField("MAX_PARAMETER_COUNT", BindingFlags.Public | BindingFlags.Static);
        if (field == null) { Results.Add("SDK has no total parameter count limit"); return; }
        int limit = (int)field.GetValue(null);
        var target = Parameters("Target", Enumerable.Range(0, limit).Select(i => P("P" + i, ValueType.Bool, 0f, false, false)).ToArray());
        string before = Snapshot(target);
        Require(!DiNeExpressionUtility.AddParameter(target, "Extra", ValueType.Bool, "Add"), "Total SDK count limit was not enforced by add.");
        Require(DiNeExpressionUtility.MergeParameters(target, Parameters("Source", P("Extra", ValueType.Bool, 0f, false, false)), "Merge") == -1, "Total SDK count limit was not enforced by merge.");
        Require(Snapshot(target) == before, "Count-limit rejection changed target.");
    }

    private static void DescribeEditorRegistry(Type inspectedType)
    {
        var registry = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
        if (registry == null) { Results.Add("Registry type absent"); return; }
        Results.Add("Registry for " + inspectedType.FullName);
        foreach (var field in registry.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            Results.Add("  registry field " + field.FieldType.FullName + " " + field.Name);
            if (!field.IsStatic) continue;
            var value = field.GetValue(null);
            if (value is IDictionary dictionary && dictionary.Contains(inspectedType))
            {
                if (dictionary[inspectedType] is IEnumerable entries)
                    foreach (var entry in entries)
                        foreach (var entryField in entry.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                            Results.Add("    " + entryField.Name + "=" + entryField.GetValue(entry));
            }
        }
        foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<UnityEditor.Editor>())
        {
            foreach (var attribute in type.GetCustomAttributes(typeof(CustomEditor), false))
            {
                var targetField = typeof(CustomEditor).GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetField?.GetValue(attribute) as Type != inspectedType) continue;
                Results.Add("  CustomEditor " + type.FullName + " assembly=" + type.Assembly.GetName().Name);
                foreach (var field in typeof(CustomEditor).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                    Results.Add("    attribute " + field.Name + "=" + field.GetValue(attribute));
            }
        }
    }

    private static void RegistryIdempotence()
    {
        var registryType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
        var cacheField = registryType.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static);
        var multiField = registryType.GetField("kSCustomMultiEditors", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = (IDictionary)cacheField.GetValue(null);
        var multi = (IDictionary)multiField.GetValue(null);
        var snapshots = new Dictionary<Type, object[]>();
        var fields = new Dictionary<object, string>();
        foreach (var target in new[] { typeof(VRCExpressionsMenu), typeof(VRCExpressionParameters) })
        {
            var records = ((IList)cache[target]).Cast<object>().ToArray();
            snapshots[target] = records;
            foreach (var record in records)
                fields[record] = string.Join(";", record.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.Name + "=" + f.GetValue(record)));
        }
        int multiCount = multi.Count;
        Require(typeof(ActiveEditorTracker).GetMethod("ForceRebuild", BindingFlags.Public | BindingFlags.Instance) != null, "Unity public tracker rebuild API is absent.");
        Require(DiNeExpressionInspectorRegistration.EnsureRegistered() && DiNeExpressionInspectorRegistration.EnsureRegistered(), "Supported Unity registry was rejected.");
        foreach (var target in snapshots.Keys)
        {
            var records = ((IList)cache[target]).Cast<object>().ToArray();
            Require(records.SequenceEqual(snapshots[target]), "Idempotent registration changed order, record identity or count.");
            foreach (var record in records)
                Require(fields[record] == string.Join(";", record.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.Name + "=" + f.GetValue(record))), "Registration modified SDK/Di Ne record fields.");
        }
        Require(multi.Count == multiCount, "Registration changed multi-object editor registry.");
    }
}
#endif
