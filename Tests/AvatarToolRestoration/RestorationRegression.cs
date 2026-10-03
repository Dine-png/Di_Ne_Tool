using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3A.Editor;
using Object = UnityEngine.Object;

public static class RestorationRegression
{
    const string Key = "DiNe.MultiDresser.TempSessions";
    const string BuildKey = "DiNe.MultiDresser.BuildInProgress";
    static readonly List<string> Results = new List<string>();
    static int failures;
    static Type Production => typeof(DiNeMultiDresserAutoApply);
    static object Call(string name, params object[] args) => Production.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    static IDictionary Cache => (IDictionary)Production.GetField("ActiveSessions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Flush() { var callbacks = EditorApplication.delayCall; EditorApplication.delayCall = null; callbacks?.Invoke(); }
    const string SmokeKey = "DiNe.RestorationSmoke";
    [InitializeOnLoadMethod]
    static void ResumeSmoke()
    {
        if (SessionState.GetBool(SmokeKey, false))
        {
            EditorApplication.playModeStateChanged -= SmokeState;
            EditorApplication.playModeStateChanged += SmokeState;
        }
    }
    public static void RunPlaySmoke()
    {
        Flush(); SessionState.SetBool(SmokeKey, true); SessionState.SetInt(SmokeKey + ".case", 0);
        StartSmokeCase();
    }
    static void StartSmokeCase()
    {
        int index = SessionState.GetInt(SmokeKey + ".case", 0);
        EditorSettings.enterPlayModeOptionsEnabled = index != 0;
        EditorSettings.enterPlayModeOptions = index == 1 ? EnterPlayModeOptions.DisableDomainReload :
            index == 2 ? EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload :
            index == 3 ? EnterPlayModeOptions.DisableSceneReload : EnterPlayModeOptions.None;
        var f = new Fixture();
        SessionState.SetString(SmokeKey + ".fx", AssetDatabase.GetAssetPath(f.fx));
        SessionState.SetString(SmokeKey + ".menu", AssetDatabase.GetAssetPath(f.menu));
        SessionState.SetString(SmokeKey + ".parameters", AssetDatabase.GetAssetPath(f.parameters));
        SessionState.SetString(SmokeKey + ".folder", Path.GetDirectoryName(AssetDatabase.GetAssetPath(f.fx)).Replace('\\', '/'));
        EditorSceneManager.SaveScene(f.root.scene, "Assets/PlaySmoke.unity");
        ResumeSmoke(); EditorApplication.isPlaying = true;
    }
    static void SmokeState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () => EditorApplication.delayCall += ValidateSmoke;
    }
    static void ValidateSmoke()
    {
        int index = SessionState.GetInt(SmokeKey + ".case", 0);
        try
        {
            var descriptor = Object.FindObjectOfType<VRCAvatarDescriptor>();
            Require(descriptor != null, "Scene avatar lost");
            Require(AssetDatabase.GetAssetPath(descriptor.baseAnimationLayers[0].animatorController) == SessionState.GetString(SmokeKey + ".fx", ""), "FX left pointing to generated asset");
            Require(AssetDatabase.GetAssetPath(descriptor.expressionsMenu) == SessionState.GetString(SmokeKey + ".menu", ""), "Menu left pointing to generated asset");
            Require(AssetDatabase.GetAssetPath(descriptor.expressionParameters) == SessionState.GetString(SmokeKey + ".parameters", ""), "Parameters left pointing to generated asset");
            Require(!descriptor.customizeAnimationLayers && descriptor.baseAnimationLayers[0].isDefault, "Original layer settings lost");
            Require(SessionState.GetString(Key, "") == "", "Restoration session remains");
            Object.DestroyImmediate(descriptor.gameObject);
            AssetDatabase.DeleteAsset(SessionState.GetString(SmokeKey + ".folder", ""));
            File.AppendAllText("PlaySmoke-results.txt", "PASS Actual play enter/exit, option case " + index + "\n");
            if (index < 3) { SessionState.SetInt(SmokeKey + ".case", index + 1); EditorApplication.delayCall += StartSmokeCase; return; }
            SessionState.SetBool(SmokeKey, false); EditorSettings.enterPlayModeOptionsEnabled = false;
            AssetDatabase.DeleteAsset("Assets/PlaySmoke.unity"); EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.AppendAllText("PlaySmoke-results.txt", "FAIL Actual play enter/exit, option case " + index + ": " + e + "\n");
            SessionState.SetBool(SmokeKey, false); EditorApplication.Exit(1);
        }
    }
    public static void Run()
    {
        Flush();
        Test("Failed dresser generation rejects build and restores original references", () => {
            using (var f = new Fixture()) {
                var dresser = f.root.AddComponent<DiNeMultiDresser>();
                dresser.animatorController = f.fx; dresser.expressionsMenu = f.menu;
                dresser.failGeneration = true;
                bool accepted = (bool)Call("ApplyToBuildAvatar", f.root);
                f.TempFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(f.descriptor.expressionParameters)).Replace('\\', '/');
                Require(!accepted, "Build accepted partially generated dresser data");
                Flush(); f.AssertOriginal();
                Require(dresser.animatorController == f.fx && dresser.expressionsMenu == f.menu,
                    "Failed generation left dresser references pointing to temporary assets");
                Require(Cache.Count == 0 && !SessionState.GetBool(BuildKey, false), "Failed build left a session or build guard");
            }
        });
        Test("Empty cache at assembly reload preserves originals", () => {
            using (var f = new Fixture()) { f.Apply(); string record = SessionState.GetString(Key, ""); Cache.Clear(); Call("OnBeforeAssemblyReload"); Require(SessionState.GetString(Key, "") == record, "Reload erased the original record"); Call("RestoreAllSessions", "test reload"); f.AssertOriginal(); }
        });
        Test("Adding a new session after reload preserves an unresolved earlier avatar", () => {
            using (var a = new Fixture()) using (var b = new Fixture()) {
                a.Apply(); string folder = a.TempFolder; Cache.Clear();
                // Model an unsaved/unresolvable descriptor after domain reload.
                string record = SessionState.GetString(Key, "");
                record = System.Text.RegularExpressions.Regex.Replace(record, "\"descriptorId\":\"[^\"]*\"", "\"descriptorId\":\"\"");
                SessionState.SetString(Key, record); b.Apply();
                Require(SessionState.GetString(Key, "").Contains(folder), "New session discarded the earlier originals");
                Call("RestoreAllSessions", "two avatars"); a.AssertOriginal(); b.AssertOriginal();
            }
        });
        Test("Edit mode return restores despite stale build guard", () => {
            using (var f = new Fixture()) { f.Apply(); SessionState.SetBool(BuildKey, true); Call("OnPlayModeStateChanged", PlayModeStateChange.EnteredEditMode); Flush(); f.AssertOriginal(); }
        });
        Test("Restart/scene load recovers disk manifest without SessionState", () => {
            using (var f = new Fixture()) { f.Apply(); Cache.Clear(); SessionState.EraseString(Key); Call("RecoverAfterReload"); f.AssertOriginal(); }
        });
        for (int i = 0; i < 4; i++) {
            int kind = i;
            Test("SDK completion/error event restores references " + kind, () => {
                using (var f = new Fixture()) { f.Apply(); SessionState.SetBool(BuildKey, true); VRCSdkControlPanel.Builder = new TestBuilder(); Call("RecoverAfterReload"); ((TestBuilder)VRCSdkControlPanel.Builder).End(kind); Flush(); f.AssertOriginal(); }
            });
        }
        Test("Destroyed play clone still restores unsaved scene avatar", () => {
            using (var f = new Fixture()) {
                f.Apply(); var clone = Object.Instantiate(f.root);
                object session = Cache.Values.Cast<object>().Single(); session.GetType().GetField("Descriptor").SetValue(session, clone.GetComponent<VRCAvatarDescriptor>());
                Object.DestroyImmediate(clone); Call("RestoreAllSessions", "destroyed clone"); f.AssertOriginal();
            }
        });
        Test("Originally empty FX/menu/parameter slots return to empty", () => {
            using (var f = new Fixture()) { f.descriptor.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>(); f.descriptor.expressionsMenu = null; f.descriptor.expressionParameters = null; f.Apply(); Call("RestoreAllSessions", "empty slots"); Require(f.descriptor.baseAnimationLayers.Length == 0 && f.descriptor.expressionsMenu == null && f.descriptor.expressionParameters == null && !f.descriptor.customizeAnimationLayers, "Empty baseline not restored"); }
        });
        Test("Dresser restores its own FX/menu baseline", () => {
            using (var f = new Fixture()) {
                var dresser = f.root.AddComponent<DiNeMultiDresser>();
                dresser.animatorController = f.fx; dresser.expressionsMenu = null;
                Call("ApplyTemporarySession", f.descriptor, new List<DiNeMultiDresser> { dresser }, new List<DiNeSmartToggle>(), new List<DiNeLightingDesigner>());
                f.TempFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(f.descriptor.expressionParameters)).Replace('\\', '/');
                Require(dresser.animatorController != f.fx && dresser.expressionsMenu != null, "Dresser not using temporary assets");
                Call("RestoreAllSessions", "dresser baseline"); f.AssertOriginal();
                Require(dresser.animatorController == f.fx && dresser.expressionsMenu == null, "Dresser's distinct baseline was lost");
            }
        });
        Test("Missing original asset remains recorded and never becomes an empty slot", () => {
            using (var f = new Fixture()) {
                EditorSceneManager.SaveScene(f.root.scene, "Assets/RestorationSavedScene.unity");
                f.Apply(); string path = AssetDatabase.GetAssetPath(f.parameters); AssetDatabase.DeleteAsset(path); Cache.Clear();
                Call("HydrateActiveSessionsFromPersisted"); Require(Cache.Count == 0, "Missing original hydrated as an empty original");
                Call("RestoreAllSessions", "missing original");
                Require(f.descriptor.expressionParameters != null && AssetDatabase.IsValidFolder(f.TempFolder), "Missing original was replaced by null or temporary asset deleted");
                Require(SessionState.GetString(Key, "").Contains(path), "Missing original path was discarded");
                AssetDatabase.DeleteAsset("Assets/RestorationSavedScene.unity");
            }
        });
        Test("Unresolved orphan cannot become a new original", () => {
            using (var f = new Fixture()) {
                f.Apply(); string folder = f.TempFolder; Cache.Clear(); SessionState.EraseString(Key);
                File.Delete(folder + "/__DiNeOriginals.json");
                f.descriptor.expressionParameters.name = "Unknown"; AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(f.descriptor.expressionParameters), "Unknown_DiNe");
                f.Apply(); Require(Cache.Count == 0, "Captured unresolved temporary data as originals");
                // Restore manually for disposal after deliberately removing recovery evidence.
                f.descriptor.baseAnimationLayers = new[] { f.originalLayer }; f.descriptor.expressionsMenu = f.menu; f.descriptor.expressionParameters = f.parameters;
            }
        });
        Results.Add("Failures: " + failures);
        File.WriteAllLines("RestorationRegression-results.txt", Results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
    static void Test(string name, Action body)
    {
        try { body(); Results.Add("PASS " + name); }
        catch (Exception e) { failures++; Results.Add("FAIL " + name + ": " + e); }
        finally { Cache.Clear(); SessionState.EraseString(Key); SessionState.SetBool(BuildKey, false); EditorApplication.delayCall = null; }
        Debug.Log(Results.Last());
    }
    sealed class Fixture : IDisposable
    {
        public readonly GameObject root;
        public readonly VRCAvatarDescriptor descriptor;
        public readonly AnimatorController fx;
        public readonly VRCExpressionsMenu menu;
        public readonly VRCExpressionParameters parameters;
        public readonly VRCAvatarDescriptor.CustomAnimLayer originalLayer;
        readonly string folder;
        public string TempFolder;
        public Fixture()
        {
            folder = "Assets/Original_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            fx = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Chocolat_FX.controller");
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); AssetDatabase.CreateAsset(menu, folder + "/Chocolat_Menu.asset");
            parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>(); AssetDatabase.CreateAsset(parameters, folder + "/Chocolat_EXParameters.asset");
            root = new GameObject("Chocolat"); descriptor = root.AddComponent<VRCAvatarDescriptor>();
            originalLayer = new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx, isDefault = true };
            descriptor.baseAnimationLayers = new[] { originalLayer }; descriptor.expressionsMenu = menu; descriptor.expressionParameters = parameters;
            root.AddComponent<DiNeSmartToggle>(); root.AddComponent<DiNeLightingDesigner>();
        }
        public void Apply()
        {
            Call("ApplyTemporarySession", descriptor, new List<DiNeMultiDresser>(), new List<DiNeSmartToggle> { root.GetComponent<DiNeSmartToggle>() }, new List<DiNeLightingDesigner> { root.GetComponent<DiNeLightingDesigner>() });
            TempFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(descriptor.expressionParameters)).Replace('\\', '/');
        }
        public void AssertOriginal()
        {
            Require(descriptor.baseAnimationLayers[0].animatorController == fx, "Original FX reference lost");
            Require(descriptor.baseAnimationLayers[0].isDefault && !descriptor.customizeAnimationLayers, "Playable layer flags not restored");
            Require(descriptor.expressionsMenu == menu && descriptor.expressionParameters == parameters, "Original menu/parameters reference lost");
            Require(!AssetDatabase.IsValidFolder(TempFolder), "Temporary assets not removed");
        }
        public void Dispose() { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); if (TempFolder != null) AssetDatabase.DeleteAsset(TempFolder); }
    }
}
