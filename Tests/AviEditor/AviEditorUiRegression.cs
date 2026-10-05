#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Measures the actual Avi Editor OnGUI, including scroll clipping, in an isolated project.</summary>
public static class AviEditorUiRegression
{
    internal const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<string> Results = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private static readonly Dictionary<string, float> DiagramHeights = new Dictionary<string, float>();
    private static readonly int[] Widths = { 300, 420, 620 };
    private static readonly int[] Heights = { 850, 520 };
    private static readonly string[] PresetActions = {
        "preset-load", "preset-scale", "preset-rotation", "preset-position", "preset-ma", "preset-save", "preset-delete" };
    private static readonly string[] PreferenceKeys = {
        "DiNe.AviEditor.ArmaturePreset.Guid", "DiNe.AviEditor.ArmaturePreset.Path", "DiNe.AviEditor.ArmaturePreset.Name",
        "DiNe.AviEditor.MAScalePreset.Guid", "DiNe.AviEditor.MAScalePreset.Path", "DiNe.AviEditor.MAScalePreset.Name" };

    [Serializable] private sealed class Preferences
    {
        public bool languagePresent;
        public int language;
        public bool[] present;
        public string[] values;
    }

    private static Preferences preferences;
    private static ArmatureScalerEditor tool;
    private static AviEditorUiRegressionHost host;
    private static GameObject avatar;
    private static Dictionary<Transform, Vector3> originalScales, originalPositions;
    private static Dictionary<Transform, Quaternion> originalRotations;
    private static string fixtureFolder, output, manifest;
    private static MethodInfo grab, repaint;
    private static object view;
    private static int caseIndex, phase, frame, captureCount;
    private static double started;

    private static int Width => Widths[caseIndex / 12];
    private static int Height => Heights[caseIndex / 6 % 2];
    private static int Language => caseIndex / 2 % 3;
    private static bool IsMA => caseIndex % 2 != 0;
    private static string CaseName => new[] { "en", "ko", "ja" }[Language] + "-" +
        (IsMA ? "ma" : "direct") + "-" + Width + "x" + Height;

    public static void Run()
    {
        try
        {
            output = Path.GetFullPath("AviEditorUiRegression");
            Directory.CreateDirectory(output);
            manifest = Path.Combine(output, "preferences-restore.json");
            if (File.Exists(manifest))
            {
                RestorePreferences(JsonUtility.FromJson<Preferences>(File.ReadAllText(manifest)));
                File.Delete(manifest);
            }
            preferences = new Preferences {
                languagePresent = EditorPrefs.HasKey("DiNeLang"), language = EditorPrefs.GetInt("DiNeLang", 0),
                present = new bool[PreferenceKeys.Length], values = new string[PreferenceKeys.Length] };
            for (int i = 0; i < PreferenceKeys.Length; i++)
            {
                preferences.present[i] = EditorPrefs.HasKey(PreferenceKeys[i]);
                preferences.values[i] = EditorPrefs.GetString(PreferenceKeys[i], "");
            }
            File.WriteAllText(manifest, JsonUtility.ToJson(preferences, true));
            CreateFixture();
            Type guiView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            grab = guiView?.GetMethod("GrabPixels", Instance);
            repaint = guiView?.GetMethod("RepaintImmediately", Instance);
            Require(grab != null && repaint != null, "Unity GUIView capture APIs are unavailable");
            Application.logMessageReceived += CaptureErrors;
            started = EditorApplication.timeSinceStartup;
            PrepareCase();
            EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void CreateFixture()
    {
        avatar = new GameObject("Synthetic Avi Editor layout avatar") { hideFlags = HideFlags.DontSave };
        var mapping = new Dictionary<HumanBodyBones, Transform>();
        foreach (HumanBodyBones bone in new[] {
            HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Hips,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot })
        {
            var transform = new GameObject(bone.ToString()).transform;
            transform.SetParent(avatar.transform, false);
            mapping.Add(bone, transform);
        }
        mapping[HumanBodyBones.Neck].gameObject.AddComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>().Scale =
            new Vector3(1.1f, .9f, 1.05f);
        originalScales = mapping.Values.ToDictionary(t => t, t => t.localScale);
        originalPositions = mapping.Values.ToDictionary(t => t, t => t.localPosition);
        originalRotations = mapping.Values.ToDictionary(t => t, t => t.localRotation);
        tool = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
        Set("targetAvatarRoot", avatar); Set("_boneMappingRoot", avatar); Set("boneMapping", mapping);
        SetEnum("selectedPart", "HumanoidBodyPart", "Neck");
        Set("maAdjustChildPositionParts", new List<string> { "Neck" });
        Call("LoadCurrentValues");
        fixtureFolder = "Assets/AviEditorUiFixture_" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(fixtureFolder); AssetDatabase.Refresh();
        var preset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
        Call("CaptureArmaturePreset", preset);
        string path = fixtureFolder + "/Combined bone and MA preset.asset";
        AssetDatabase.CreateAsset(preset, path); AssetDatabase.SaveAssets(); Call("RefreshPresetList", path);
        host = ScriptableObject.CreateInstance<AviEditorUiRegressionHost>();
        host.Tool = tool; host.minSize = new Vector2(300, 300);
        host.ShowUtility(); host.Focus();
    }

    private static void PrepareCase()
    {
        EditorPrefs.SetInt("DiNeLang", Language);
        SetEnum("armatureEditMode", "ArmatureEditMode", IsMA ? "ModularAvatarScale" : "DirectTransform");
        Set("armaturePresetStatus", "");
        Set("scrollPosition", Vector2.zero);
        tool.position = host.position = new Rect(50, 50, Width, Height);
        host.Focus(); host.Repaint(); host.Failure = null;
        view = typeof(EditorWindow).GetField("m_Parent", Instance).GetValue(host);
        Require(view != null, "Actual EditorWindow has no GUIView");
        phase = frame = 0;
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 150, "UI layout regression exceeded 150 seconds");
            if (++frame < 8) { host.Repaint(); return; }
            host.SendEvent(new Event { type = EventType.Layout });
            host.SendEvent(new Event { type = EventType.Repaint });
            repaint.Invoke(view, null);
            Require(host.Failure == null, "Avi Editor OnGUI failed: " + host.Failure);
            if ((caseIndex == 0 && phase == 0) || !host.ButtonBackgroundsValid)
                Results.Add("METRIC button skin " + CaseName + " phase=" + phase + " " + host.ButtonStyleDescription);
            if (!host.ButtonBackgroundsValid) Capture();
            Require(host.ButtonBackgroundsValid, "Themed buttons failed to recover their active Unity skin backgrounds: " +
                host.ButtonStyleDescription);
            Require(Errors.Count == 0, "UI draw emitted errors: " + string.Join("\n", Errors));
            Require(Mathf.Abs(host.position.width - Width) < .1f && Mathf.Abs(host.position.height - Height) < .1f,
                "Native window rejected requested size: " + host.position);
            VerifyFixture();
            if (phase == 0) VerifyLayout();
            else if (phase == 1) VerifyHandSelection();
            else if (phase == 2) VerifyDiagramBottom();
            else VerifySelectedControls();
            if (Height == 850) Capture();
            if (phase == 0)
            {
                var diagram = host.Anchors["bone"];
                float currentY = Get<Vector2>("scrollPosition").y;
                float handY = diagram.Rect.y + 320f * diagram.Rect.height / 660f;
                Set("scrollPosition", new Vector2(0, Mathf.Max(0, currentY + handY - diagram.Viewport.center.y)));
                foreach (string field in new[] { "themedButtonStyle", "themedBoldButtonStyle" })
                {
                    Get<GUIStyle>(field).normal.background = null;
                    Get<GUIStyle>(field).normal.scaledBackgrounds = new Texture2D[0];
                }
                phase = 1; frame = 0; host.Repaint();
            }
            else if (phase == 1)
            {
                var diagram = host.Anchors["bone"];
                float currentY = Get<Vector2>("scrollPosition").y;
                Set("scrollPosition", new Vector2(0, Mathf.Max(0, currentY + diagram.Rect.yMax - diagram.Viewport.yMax + 8)));
                phase = 2; frame = 0; host.Repaint();
            }
            else if (phase == 2)
            {
                Set("scrollPosition", new Vector2(0, 10000)); phase = 3; frame = 0; host.Repaint();
            }
            else
            {
                Results.Add("PASS " + CaseName + " (scroll scope, button geometry/text, stale skin recovery, hand hit areas, fixed diagram, reachable lower controls)");
                if (++caseIndex == 36) Finish(null); else PrepareCase();
            }
        }
        catch (Exception error) { Finish(error is TargetInvocationException target ? target.InnerException ?? target : error); }
    }

    private static void VerifyLayout()
    {
        Require(host.Anchors.ContainsKey("bone"), "Body map anchor is absent");
        var diagram = host.Anchors["bone"];
        Require(diagram.Scope != null, "Body map is outside the armature scroll view");
        Require(diagram.Rect.height >= 400, "Body map was squeezed: " + diagram.Rect);
        Require(diagram.Viewport.height >= 80 && diagram.Viewport.yMin >= -.1f && diagram.Viewport.yMax <= Height + 1,
            "Armature scroll viewport is missing or extends past the window: " + diagram.Viewport);
        string key = Width + ":" + Language + ":" + IsMA;
        if (DiagramHeights.TryGetValue(key, out float previous))
            Require(Mathf.Abs(previous - diagram.Rect.height) < .1f, "Body map height depends on window height");
        else DiagramHeights.Add(key, diagram.Rect.height);
        string[] ids = PresetActions.Concat(new[] { "avatar", "refresh", IsMA ? "ma-scale-uniform" : "scale-uniform",
            IsMA ? "ma-scale-vector" : "scale-vector" }).Concat(IsMA ? new[] { "ma-children", "ma-remove" } :
            new[] { "reset-scales", "position", "rotation" }).ToArray();
        foreach (string id in ids)
        {
            Require(host.Anchors.TryGetValue(id, out var anchor), "Expected armature control is absent: " + id);
            Require(ReferenceEquals(anchor.Scope, diagram.Scope), id + " is not inside the same complete armature scroll area");
            Require(anchor.Rect.width > 0 && anchor.Rect.height > 0, id + " has an empty rectangle");
            Require(anchor.Rect.xMin >= diagram.Viewport.xMin - 1 && anchor.Rect.xMax <= diagram.Viewport.xMax + 1,
                id + " extends horizontally past the scroll viewport: " + anchor.Rect + ", viewport=" + diagram.Viewport);
        }
        foreach (string id in PresetActions.Concat(IsMA ? new[] { "ma-remove", "ma-children" } : new[] { "reset-scales" }))
        {
            var anchor = host.Anchors[id];
            Require(anchor.Rect.height >= 23, id + " button lost its minimum hit area");
            Require(anchor.Rect.height + 1 >= anchor.TextHeight,
                id + " text is clipped: button=" + anchor.Rect + ", required height=" + anchor.TextHeight);
            if (id == "ma-children")
                Require(anchor.LabelWidth + 1 >= anchor.TextWidth,
                    "MA child-position label is clipped by its checkbox: available=" + anchor.LabelWidth + ", required=" + anchor.TextWidth);
        }
        foreach (string a in PresetActions)
        foreach (string b in PresetActions)
            if (string.CompareOrdinal(a, b) < 0)
                Require(!host.Anchors[a].Rect.Overlaps(host.Anchors[b].Rect), "Preset actions overlap: " + a + ", " + b);
        Results.Add("METRIC " + CaseName + " viewport=" + diagram.Viewport + " diagram=" + diagram.Rect +
            " delete=" + host.Anchors["preset-delete"].Rect + " save=" + host.Anchors["preset-save"].Rect);
    }

    private static void VerifyDiagramBottom()
    {
        var diagram = host.Anchors["bone"];
        Require(diagram.Rect.yMax <= diagram.Viewport.yMax + 1 && diagram.Rect.yMax >= diagram.Viewport.yMin + 30,
            "Scrolling cannot expose the complete lower body map: " + diagram.Rect + ", viewport=" + diagram.Viewport);
    }

    private static void VerifyHandSelection()
    {
        var diagram = host.Anchors["bone"];
        float horizontalScale = Mathf.Min(1f, Mathf.Max(0f, diagram.Rect.width - 64f) / 266f);
        float y = diagram.Rect.y + 320f * diagram.Rect.height / 660f;
        foreach (string part in new[] { "LeftHand", "RightHand" })
        {
            float x = diagram.Rect.center.x + (part == "LeftHand" ? -133 : 133) * horizontalScale;
            var point = new Vector2(x, y);
            Require(diagram.Viewport.Contains(point), "Hand click target lies outside the visible body map: " + part);
            host.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 });
            host.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
            Require(Get<object>("selectedPart").ToString() == part, "Body map hit area cannot select " + part);
        }
        SetEnum("selectedPart", "HumanoidBodyPart", "Neck");
        Call("LoadCurrentValues");
        host.SendEvent(new Event { type = EventType.Layout });
        host.SendEvent(new Event { type = EventType.Repaint });
        VerifyFixture();
    }

    private static void VerifySelectedControls()
    {
        float maxScroll = Get<Vector2>("scrollPosition").y;
        foreach (string id in IsMA ? new[] { "ma-children", "ma-remove", "ma-scale-uniform", "ma-scale-vector" } :
            new[] { "scale-uniform", "scale-vector", "position", "rotation" })
        {
            var anchor = host.Anchors[id];
            // A narrow Vector3 field can occupy two lines, so a short window need
            // not show the entire selected-part card simultaneously. Each control
            // must have some reachable scroll offset at which it is fully visible.
            float leastScroll = anchor.Rect.yMax + maxScroll - anchor.Viewport.yMax;
            float mostScroll = anchor.Rect.yMin + maxScroll - anchor.Viewport.yMin;
            Require(Mathf.Max(0, leastScroll) <= Mathf.Min(maxScroll, mostScroll) + 1,
                "Scrolling cannot expose lower selected-bone control " + id + ": " + anchor.Rect);
        }
        Require(maxScroll > 0, "Armature content did not become scrollable");
    }

    private static void Capture()
    {
        var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create(); RenderTexture.active = target; GL.Clear(true, true, Color.magenta);
            grab.Invoke(view, new object[] { target, new Rect(0, 0, Width, Height) });
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply();
            string path = Path.Combine(output, CaseName + "-" + new[] { "top", "hands", "lower-map", "controls" }[phase] + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Require(new HashSet<Color32>(texture.GetPixels32()).Count >= 32, "GUIView returned no varied UI pixels: " + path);
            captureCount++;
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); }
    }

    private static void VerifyFixture()
    {
        foreach (var entry in originalScales) Require(entry.Key.localScale == entry.Value, "Passive UI changed a bone scale");
        foreach (var entry in originalPositions) Require(entry.Key.localPosition == entry.Value, "Passive UI changed a bone position");
        foreach (var entry in originalRotations) Require(entry.Key.localRotation == entry.Value, "Passive UI changed a bone rotation");
        Require(avatar.GetComponentsInChildren<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>(true).Length == 1,
            "Passive UI changed MA component membership");
        Require(avatar.transform.Find("Neck").GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>().Scale ==
            new Vector3(1.1f, .9f, 1.05f), "Passive UI changed MA values");
    }

    private static void CaptureErrors(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message + "\n" + stack); }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= CaptureErrors;
        try
        {
            if (host != null) { host.Tool = null; host.Close(); if (host != null) Object.DestroyImmediate(host); }
            if (tool != null) Object.DestroyImmediate(tool);
            if (avatar != null) { VerifyFixture(); Object.DestroyImmediate(avatar); }
            if (!string.IsNullOrEmpty(fixtureFolder)) AssetDatabase.DeleteAsset(fixtureFolder);
        }
        catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
        try
        {
            if (preferences != null)
            {
                RestorePreferences(preferences);
                File.Delete(manifest);
                Results.Add("PASS Original DiNeLang and preset picker preferences restored and verified");
            }
        }
        catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
        Results.Add("METRIC actual GUIView captures: " + captureCount);
        if (error != null) { Results.Add("FAIL " + (caseIndex < 36 ? CaseName : "cleanup") + ": " + error); Debug.LogError(error); }
        Results.Add("Failures: " + (error == null ? 0 : 1));
        File.WriteAllLines("AviEditorUiRegression-results.txt", Results);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void RestorePreferences(Preferences snapshot)
    {
        Require(snapshot != null && snapshot.present?.Length == PreferenceKeys.Length && snapshot.values?.Length == PreferenceKeys.Length,
            "Preference recovery manifest is invalid; preserve it for manual recovery");
        if (snapshot.languagePresent) EditorPrefs.SetInt("DiNeLang", snapshot.language); else EditorPrefs.DeleteKey("DiNeLang");
        for (int i = 0; i < PreferenceKeys.Length; i++)
            if (snapshot.present[i]) EditorPrefs.SetString(PreferenceKeys[i], snapshot.values[i]); else EditorPrefs.DeleteKey(PreferenceKeys[i]);
        Require(EditorPrefs.HasKey("DiNeLang") == snapshot.languagePresent &&
            (!snapshot.languagePresent || EditorPrefs.GetInt("DiNeLang") == snapshot.language), "Language preference restoration failed");
        for (int i = 0; i < PreferenceKeys.Length; i++)
            Require(EditorPrefs.HasKey(PreferenceKeys[i]) == snapshot.present[i] &&
                (!snapshot.present[i] || EditorPrefs.GetString(PreferenceKeys[i]) == snapshot.values[i]), "Picker preference restoration failed");
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Set(string field, object value) => typeof(ArmatureScalerEditor).GetField(field, Instance).SetValue(tool, value);
    private static T Get<T>(string field) => (T)typeof(ArmatureScalerEditor).GetField(field, Instance).GetValue(tool);
    private static void SetEnum(string field, string type, string value) => Set(field,
        Enum.Parse(typeof(ArmatureScalerEditor).GetNestedType(type, BindingFlags.NonPublic), value));
    private static void Call(string method, params object[] args) => typeof(ArmatureScalerEditor).GetMethod(method, Instance).Invoke(tool, args);
}

public sealed class AviEditorUiRegressionHost : EditorWindow
{
    public sealed class Measurement
    {
        public Rect Rect, Viewport;
        public object Scope;
        public float TextHeight;
        public float TextWidth, LabelWidth;
    }
    public ArmatureScalerEditor Tool;
    public Exception Failure;
    public bool ButtonBackgroundsValid;
    public string ButtonStyleDescription;
    public readonly Dictionary<string, Measurement> Anchors = new Dictionary<string, Measurement>();

    private void OnGUI()
    {
        if (Tool == null) return;
        try
        {
            typeof(ArmatureScalerEditor).GetMethod("OnGUI", AviEditorUiRegression.Instance).Invoke(Tool, null);
            if (Event.current.type != EventType.Repaint) return;
            var normalStyle = (GUIStyle)typeof(ArmatureScalerEditor).GetField("themedButtonStyle", AviEditorUiRegression.Instance).GetValue(Tool);
            var boldStyle = (GUIStyle)typeof(ArmatureScalerEditor).GetField("themedBoldButtonStyle", AviEditorUiRegression.Instance).GetValue(Tool);
            var cachedSkin = typeof(ArmatureScalerEditor).GetField("themedButtonSkin", AviEditorUiRegression.Instance).GetValue(Tool) as GUISkin;
            ButtonStyleDescription = "GUI.skin=" + TextureIdentity(GUI.skin) + ", cachedSkin=" + TextureIdentity(cachedSkin) +
                ", proSkin=" + EditorGUIUtility.isProSkin + ", enabled=" + GUI.enabled + ", event=" + Event.current.type +
                ", color=" + GUI.color + ", backgroundColor=" + GUI.backgroundColor + ", source=" + DescribeStyle(GUI.skin.button) +
                ", cachedNormal=" + DescribeStyle(normalStyle) + ", cachedBold=" + DescribeStyle(boldStyle);
            GUIStyle source = GUI.skin.button;
            bool sourceHasNormalTexture = source.normal.background != null ||
                (source.normal.scaledBackgrounds != null && source.normal.scaledBackgrounds.Any(t => t != null));
            ButtonBackgroundsValid = sourceHasNormalTexture && MatchesButtonTextures(normalStyle, source) && MatchesButtonTextures(boldStyle, source);
            var tutorial = typeof(ArmatureScalerEditor).GetField("_tutorial", AviEditorUiRegression.Instance).GetValue(Tool);
            var anchors = (Dictionary<string, DiNeTutorialBubble.Anchor>)typeof(DiNeGuidedTutorial)
                .GetField("anchors", AviEditorUiRegression.Instance).GetValue(tutorial);
            Anchors.Clear();
            foreach (var entry in anchors)
            {
                var anchor = entry.Value;
                var scope = anchor.scopes?.FirstOrDefault();
                Rect rect = DiNeTutorialBubble.ToLocalRect(anchor);
                var measurement = new Measurement { Rect = rect, Scope = scope, Viewport = scope == null ? Rect.zero :
                    DiNeTutorialBubble.ToLocalRect(new DiNeTutorialBubble.Anchor { screenRect = scope.screenRect }) };
                GUIContent content = ButtonContent(entry.Key);
                if (content != null)
                {
                    if (entry.Key == "ma-children")
                    {
                        // Match DrawThemedCheckboxToggle's actual unwrapped
                        // label and reserve its 14px box, gaps and right margin.
                        var labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, fontStyle = FontStyle.Bold };
                        measurement.LabelWidth = Mathf.Max(0, rect.width - 32);
                        measurement.TextWidth = labelStyle.CalcSize(content).x;
                        measurement.TextHeight = labelStyle.CalcHeight(content, measurement.LabelWidth);
                        Anchors.Add(entry.Key, measurement);
                        continue;
                    }
                    bool bold = entry.Key == "preset-load";
                    var style = typeof(ArmatureScalerEditor).GetField(bold ? "themedBoldButtonStyle" : "themedButtonStyle",
                        AviEditorUiRegression.Instance)?.GetValue(Tool) as GUIStyle;
                    style = style ?? new GUIStyle(GUI.skin.button) { wordWrap = true, fontSize = 12 };
                    measurement.TextHeight = style.CalcHeight(content, rect.width);
                }
                Anchors.Add(entry.Key, measurement);
            }
        }
        catch (TargetInvocationException error) { if (!(error.InnerException is ExitGUIException)) Failure = error.InnerException ?? error; }
        catch (ExitGUIException) { }
        catch (Exception error) { Failure = error; }
    }

    private static string TextureIdentity(Object value) => value == null ? "null" : value.name + "#" + value.GetInstanceID();
    private static bool SameTextures(GUIStyleState actual, GUIStyleState expected)
    {
        if (actual.background != expected.background) return false;
        Texture2D[] actualScaled = actual.scaledBackgrounds ?? new Texture2D[0];
        Texture2D[] expectedScaled = expected.scaledBackgrounds ?? new Texture2D[0];
        return actualScaled.SequenceEqual(expectedScaled);
    }
    private static bool MatchesButtonTextures(GUIStyle actual, GUIStyle expected) => actual != null &&
        SameTextures(actual.normal, expected.normal) && SameTextures(actual.hover, expected.hover) &&
        SameTextures(actual.active, expected.active) && SameTextures(actual.onNormal, expected.onNormal);
    private static string DescribeState(GUIStyleState state) => "background=" + TextureIdentity(state.background) +
        ", scaled=" + (state.scaledBackgrounds == null ? "null" : state.scaledBackgrounds.Length + ":" +
            string.Join(",", state.scaledBackgrounds.Select(t => TextureIdentity(t))));
    private static string DescribeStyle(GUIStyle style) => style == null ? "null" : style.name +
        "[normal " + DescribeState(style.normal) + "; hover " + DescribeState(style.hover) + "; active " +
        DescribeState(style.active) + "; onNormal " + DescribeState(style.onNormal) + "]";

    private GUIContent ButtonContent(string id)
    {
        string[] text;
        switch (id)
        {
            case "preset-load": text = new[] { "Load Entire Preset", "프리셋 전체 불러오기", "プリセット全体を読み込む" }; break;
            case "preset-scale": text = new[] { "Bone Scale Only", "기본 크기만", "ボーンサイズのみ" }; break;
            case "preset-rotation": text = new[] { "Rotation Only", "회전만", "回転のみ" }; break;
            case "preset-position": text = new[] { "Position Only", "위치만", "位置のみ" }; break;
            case "preset-ma": text = new[] { "MA Scale Adjuster Only", "MA Scale Adjuster만 불러오기", "MA Scale Adjusterのみ読み込む" }; break;
            case "preset-save": text = new[] { "＋ Save Both as New Preset", "＋ 두 조정값을 새 프리셋으로 저장", "＋ 両方の調整値を新規プリセットとして保存" }; break;
            case "ma-remove": text = new[] { "Remove Adjuster", "Adjuster 제거", "Adjusterを削除" }; break;
            case "ma-children": text = new[] { "Adjust Child Positions", "자식 위치 조정", "子位置調整" }; break;
            case "preset-delete": return new GUIContent(((string[])typeof(ArmatureScalerEditor).GetField("UI_TEXT", AviEditorUiRegression.Instance).GetValue(Tool))[31]);
            case "reset-scales": return new GUIContent(((string[])typeof(ArmatureScalerEditor).GetField("UI_TEXT", AviEditorUiRegression.Instance).GetValue(Tool))[36]);
            default: return null;
        }
        return new GUIContent(text[Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2)]);
    }
}
#endif
