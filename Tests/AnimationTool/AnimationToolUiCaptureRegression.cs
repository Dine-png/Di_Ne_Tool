#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AnimationToolRegression
{
    private static AnimationFixture uiFixture;
    private static ReferenceSnapshot uiReferences;
    private static DiNeAnimationTool uiAnimation;
    private static ArmatureScalerEditor uiAvi;
    private static AnimationToolRegressionHost uiAviHost;
    private static AnimationClip uiAnimationClip, uiRepairClip;
    private static LanguagePreferences uiPreferences;
    private static Action<Exception> uiComplete;
    private static Application.LogCallback uiLogCallback;
    private static readonly List<string> uiErrors = new List<string>();
    private static int uiCase, uiFrame, uiCaptureRetries, uiCaptureCount;
    private static double uiStarted;
    private static object uiView;
    private static MethodInfo uiGrab, uiRepaint;
    private static string uiOutput, uiPreferencesManifest;
    private const int UiHeight = 850;
    private static int UiWidth => uiCase == 18 || uiCase == 19 ? 420 : 440;
    private static void RecoverUiPreferences()
        => LanguagePreferences.Recover(Path.GetFullPath("AnimationToolUiCapture/preferences-restore.json"));

    private static void BeginUiCapture(Action<Exception> complete)
    {
        uiComplete = complete;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            uiOutput = Path.GetFullPath("AnimationToolUiCapture"); Directory.CreateDirectory(uiOutput);
            uiPreferencesManifest = Path.Combine(uiOutput, "preferences-restore.json");
            uiPreferences = new LanguagePreferences();
            uiPreferences.Persist(uiPreferencesManifest);
            uiFixture = new AnimationFixture(); uiFixture.SaveScene();
            uiReferences = new ReferenceSnapshot(uiFixture);
            uiAnimationClip = PreviewClip(uiFixture);
            AssetDatabase.CreateAsset(uiAnimationClip, uiFixture.folder + "/UiPreview.anim");
            var layers = uiFixture.controller.layers; layers[0].name = "Left Hand"; uiFixture.controller.layers = layers;
            uiFixture.controller.layers[0].stateMachine.AddState("Smile gesture").motion = uiAnimationClip;
            uiFixture.controller.layers[0].stateMachine.AddState("Other gesture").motion = uiAnimationClip;
            EditorUtility.SetDirty(uiFixture.controller); AssetDatabase.SaveAssets();
            uiRepairClip = new AnimationClip { name = "Renamed expression source" };
            var old = Float("OldFace", typeof(SkinnedMeshRenderer), "blendShape.OldSmile");
            AnimationUtility.SetEditorCurve(uiRepairClip, old, WeightedCurve(10));
            SetupUiAnimation(old);
            Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            uiGrab = viewType.GetMethod("GrabPixels", BindingFlags.Instance | BindingFlags.NonPublic);
            uiRepaint = viewType.GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public);
            Require(uiGrab != null && uiRepaint != null, "Unity 2022.3 GUIView capture API unavailable");
            uiErrors.Clear(); uiCase = uiFrame = uiCaptureCount = 0; uiStarted = EditorApplication.timeSinceStartup;
            uiLogCallback = (message, stack, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) uiErrors.Add(message + "\n" + stack); };
            Application.logMessageReceived += uiLogCallback;
            PrepareUiCase(); EditorApplication.update += TickUiCapture;
        }
        catch (Exception error) { FinishUiCapture(error); }
    }

    private static EditorWindow UiWindow => uiCase < 9 || uiCase >= 18 ? (EditorWindow)uiAnimation : uiAviHost;
    private static string UiCaseName
    {
        get
        {
            if (uiCase == 18) return "animation-en-preview-420";
            if (uiCase == 19) return "animation-ja-expression-420";
            if (uiCase == 20) return "animation-ko-repair-lower-440";
            if (uiCase == 21) return "animation-ko-expression-lower-440";
            int localCase = uiCase % 9, language = localCase / 3, tab = localCase % 3;
            string prefix = uiCase < 9 ? "animation" : "avi";
            string[] languages = { "en", "ko", "ja" };
            string[] tabs = uiCase < 9 ? new[] { "preview", "expression", "repair" } : new[] { "armature", "shapekey", "extra" };
            return prefix + "-" + languages[language] + "-" + tabs[tab] + "-440";
        }
    }

    private static void PrepareUiCase()
    {
        if (uiCase == 9)
        {
            CloseUiWindow(uiAnimation); uiAnimation = null;
            uiAvi = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            typeof(ArmatureScalerEditor).GetField("targetAvatarRoot", InstanceFields).SetValue(uiAvi, uiFixture.root);
            typeof(ArmatureScalerEditor).GetField("_skeSmr", InstanceFields).SetValue(uiAvi, uiFixture.renderer);
            uiAviHost = ScriptableObject.CreateInstance<AnimationToolRegressionHost>(); uiAviHost.Tool = uiAvi;
        }
        if (uiCase == 18)
        {
            CloseUiWindow(uiAviHost); uiAviHost = null; CloseUiWindow(uiAvi); uiAvi = null;
            SetupUiAnimation(Float("OldFace", typeof(SkinnedMeshRenderer), "blendShape.OldSmile"));
        }
        int local = uiCase % 9;
        EditorPrefs.SetInt("DiNeLang", uiCase == 18 ? 0 : uiCase == 19 ? 2 : uiCase >= 20 ? 1 : local / 3);
        if (uiCase < 9 || uiCase >= 18)
        {
            Set(uiAnimation, "selectedTab", uiCase == 20 ? 2 : uiCase == 21 ? 1 : uiCase >= 18 ? uiCase - 18 : local % 3);
            Set(uiAnimation, "scrollPosition", uiCase >= 20 ? new Vector2(0, 10000) : Vector2.zero);
            Set(uiAnimation, "previewDirty", true); Set(uiAnimation, "status", ""); Set(uiAnimation, "repairStatus", "");
        }
        else typeof(ArmatureScalerEditor).GetMethod("SetMainModeIndex", InstanceFields).Invoke(uiAvi, new object[] { local % 3 });
        if (uiAvi != null) uiAvi.position = new Rect(50, 50, UiWidth, UiHeight);
        EditorWindow window = UiWindow;
        window.position = new Rect(50, 50, UiWidth, UiHeight);
        window.ShowUtility(); window.Focus(); window.Repaint();
        window.position = new Rect(50, 50, UiWidth, UiHeight);
        uiView = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        Require(uiView != null, "Actual editor window has no GUIView");
        uiFrame = uiCaptureRetries = 0;
    }

    private static void TickUiCapture()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - uiStarted < 120, "22-window capture exceeded its 120-second bound");
            if (++uiFrame < 8) { UiWindow.Repaint(); return; }
            UiWindow.SendEvent(new Event { type = EventType.Layout });
            UiWindow.SendEvent(new Event { type = EventType.Repaint });
            uiRepaint.Invoke(uiView, null);
            object parentPosition = uiView.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(uiView);
            object cache = typeof(GUILayoutUtility).GetField("current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
            object topLevel = cache?.GetType().GetField("topLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(cache);
            object layoutRect = topLevel?.GetType().GetField("rect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(topLevel);
            Results.Add("METRIC UI geometry " + UiCaseName + ": window=" + UiWindow.position + ", GUIView=" + parentPosition + ", layout=" + layoutRect);
            Require(Mathf.Abs(UiWindow.position.width - UiWidth) < .1f, "Native window width differs from requested capture width");
            var target = new RenderTexture(UiWidth, UiHeight, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(UiWidth, UiHeight, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create(); RenderTexture.active = target; GL.Clear(true, true, Color.magenta);
                uiGrab.Invoke(uiView, new object[] { target, new Rect(0, 0, UiWidth, UiHeight) });
                texture.ReadPixels(new Rect(0, 0, UiWidth, UiHeight), 0, 0); texture.Apply();
                string path = Path.Combine(uiOutput, UiCaseName + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                int colors = new HashSet<Color32>(texture.GetPixels32()).Count;
                if (colors < 32 && ++uiCaptureRetries < 3)
                {
                    Debug.Log("UI capture buffer not ready: " + UiCaseName + ", retry=" + uiCaptureRetries);
                    uiFrame = 0; UiWindow.Focus(); UiWindow.Repaint(); return;
                }
                Require(colors >= 32, "GUIView yielded no varied UI pixels: " + path);
                Results.Add("CAPTURE " + UiCaseName + " colors=" + colors + " file=" + path);
                uiReferences.Verify(uiFixture.root);
                if (uiAviHost != null) Require(uiAviHost.Failure == null, "Actual Avi OnGUI render failed: " + uiAviHost.Failure);
                Require(!uiFixture.root.scene.isDirty, "Passive UI draw dirtied synthetic avatar scene");
                Require(uiErrors.Count == 0, "Actual window capture emitted errors: " + string.Join("\n", uiErrors));
                uiCaptureCount++;
            }
            finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); }
            if (++uiCase >= 22) FinishUiCapture(null);
            else PrepareUiCase();
        }
        catch (Exception error) { FinishUiCapture(error is TargetInvocationException target ? target.InnerException ?? target : error); }
    }

    private static void FinishUiCapture(Exception error)
    {
        EditorApplication.update -= TickUiCapture;
        if (uiLogCallback != null) Application.logMessageReceived -= uiLogCallback;
        try
        {
            CloseUiWindow(uiAnimation); CloseUiWindow(uiAviHost); CloseUiWindow(uiAvi);
            if (uiReferences != null && uiFixture != null && uiFixture.root != null) uiReferences.Verify(uiFixture.root);
            if (uiAnimationClip != null && !AssetDatabase.Contains(uiAnimationClip)) Object.DestroyImmediate(uiAnimationClip);
            if (uiRepairClip != null) Object.DestroyImmediate(uiRepairClip);
            uiFixture?.Dispose();
            Results.Add("METRIC SDK descriptor assembly: " + typeof(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor).Assembly.GetName().Name);
            Results.Add("METRIC UI captures: " + uiCaptureCount);
        }
        catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
        finally
        {
            try
            {
                uiPreferences?.Dispose();
                if (!string.IsNullOrEmpty(uiPreferencesManifest) && File.Exists(uiPreferencesManifest)) File.Delete(uiPreferencesManifest);
                Results.Add("METRIC language/preset preferences restored and verified");
            }
            catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
        }
        Action<Exception> complete = uiComplete; uiComplete = null;
        complete?.Invoke(error);
    }

    private static void CloseUiWindow(EditorWindow window)
    {
        if (window == null) return;
        try
        {
            object parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            if (parent != null) window.Close();
        }
        finally { if (window != null) Object.DestroyImmediate(window); }
    }

    private static void SetupUiAnimation(EditorCurveBinding old)
    {
        uiAnimation = ScriptableObject.CreateInstance<DiNeAnimationTool>();
        Set(uiAnimation, "targetAvatarRoot", uiFixture.root); Set(uiAnimation, "animationClip", uiAnimationClip); Call(uiAnimation, "RefreshAvatar");
        Set(uiAnimation, "repairClips", new List<AnimationClip> { uiRepairClip }); Call(uiAnimation, "AnalyzeRepairClips");
        Set(uiAnimation, "repairMappings", new List<DiNeAnimationRepairCore.BindingMapping> { Map(old, false, "Body", "blendShape.Smile") });
        Call(uiAnimation, "RefreshRepairPlan");
    }
}
#endif
