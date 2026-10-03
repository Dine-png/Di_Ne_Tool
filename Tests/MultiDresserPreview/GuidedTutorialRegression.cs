#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GuidedTutorialRegression
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Invoke(DiNeGuidedTutorial guide, string method) => typeof(DiNeGuidedTutorial)
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(guide, null);
    private static void Next(DiNeGuidedTutorial guide) { Invoke(guide, "Advance"); Invoke(guide, "Flush"); }
    private static void Flush(DiNeGuidedTutorial guide) => Invoke(guide, "Flush");

    public static void Progression()
    {
        var owner = ScriptableObject.CreateInstance<GuidedTutorialProbe>();
        bool ready = true, preview = false;
        var guide = new DiNeGuidedTutorial(owner, "Regression." + Guid.NewGuid());
        var steps = new[] {
            DiNeTutorialStep.Required("target", "Assign target.", "대상을 넣으세요.", "対象を指定。", () => ready),
            DiNeTutorialStep.Required("preview", "Start preview.", "미리보기를 시작하세요.", "プレビュー開始。", () => preview, false),
            DiNeTutorialStep.Optional("save", "Save if needed.", "필요하면 저장하세요.", "必要なら保存。")
        };
        try
        {
            guide.Configure("main", "Test", "검사", "テスト", steps); guide.Start(); Next(guide);
            guide.Validate(); Flush(guide);
            Require(guide.CurrentStepId == "target" && guide.CanClickCurrent, "Existing target did not wait for a clickable bubble.");
            Next(guide); Require(guide.CurrentStepId == "preview" && !guide.CanClickCurrent, "Incomplete action became clickable.");
            guide.NotifyAction("preview"); Flush(guide);
            Require(guide.CurrentStepId == "preview", "Failed action advanced.");
            // Serialized properties apply later in the same GUI event.
            guide.NotifyAction("preview"); preview = true; Flush(guide);
            Require(guide.CurrentStepId == "save", "Applied required action did not advance.");
            preview = false; guide.Validate(); Flush(guide);
            Require(guide.CurrentStepId == "save", "Restored preview incorrectly rewound a transient prerequisite.");
            ready = false; guide.Validate(); Flush(guide);
            Require(guide.CurrentStepId == "target", "Invalid prerequisite did not rewind.");
            ready = true; guide.Validate(); Flush(guide);
            Require(guide.CurrentStepId == "preview", "Actual assignment from another inspector did not advance.");
            guide.NotifyAction("preview"); guide.Suspend(); preview = true; Flush(guide);
            Require(!guide.IsActive, "A suspended course accepted a delayed action.");
        }
        finally { guide.Stop(); UnityEngine.Object.DestroyImmediate(owner); }
    }

    public static void CoursesAndOwnership()
    {
        var subject = new GameObject("Tutorial session regression");
        var first = Editor.CreateEditor(subject); var second = Editor.CreateEditor(subject);
        string id = "Regression." + Guid.NewGuid();
        var a = new DiNeGuidedTutorial(first, id); var b = new DiNeGuidedTutorial(second, id);
        var steps = new[] { DiNeTutorialStep.Optional("one", "One", "하나", "一"), DiNeTutorialStep.Optional("two", "Two", "둘", "二") };
        try
        {
            a.Configure("A", "A", "A", "A", steps); a.Start(); Next(a); Next(a);
            b.Configure("A", "A", "A", "A", steps);
            Require(a.IsActive && !b.IsActive, "Locked inspectors both owned the same course.");
            b.Suspend(); Require(a.IsActive, "Inactive inspector released another owner's session.");
            a.Configure("B", "B", "B", "B", steps);
            Require(!a.IsActive, "A different course reused active progress.");
            a.Start(); Next(a); a.Configure("A", "A", "A", "A", steps);
            Require(a.IsActive && a.CurrentStepId == "two", "Returning to a course lost its progress.");
            a.Configure("A", "A", "A", "A", steps); Require(a.CurrentStepId == "two", "Repeated Configure reset progress.");
            a.Configure("A", "A", "A", "A", steps, 2); Require(!a.IsActive, "Changed course schema retained incompatible progress.");
            a.Start(); Next(a); a.Suspend(); a.Configure("A", "A", "A", "A", steps, 2);
            Require(a.IsActive && a.CurrentStepId == "one", "Returning to the same suspended course did not resume.");
        }
        finally { a.Stop(); b.Stop(); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(subject); }
    }

    public static void NestedScrollSpotlight()
    {
        var window = ScriptableObject.CreateInstance<GuidedTutorialProbe>();
        Vector2 scroll = new Vector2(0, 90);
        Rect viewport = Rect.zero, expectedAnchor = Rect.zero;
        int clicks = 0;
        try
        {
            window.position = new Rect(50, 50, 540, 800); window.Show();
            window.Draw = () => {
                DiNeTutorialBubble.BeginFrame(); GUILayout.Space(45);
                DiNeTutorialBubble.BeginScrollScope();
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
                GUILayout.Space(120);
                Rect control = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true)); GUI.Box(control, "Target");
                var anchor = DiNeTutorialBubble.CaptureAnchor(control);
                GUILayout.Space(260); EditorGUILayout.EndScrollView();
                viewport = GUILayoutUtility.GetLastRect(); DiNeTutorialBubble.EndScrollScope(viewport);
                expectedAnchor = DiNeTutorialBubble.ToLocalRect(anchor);
                DiNeTutorialBubble.Draw(anchor, "Short action", "Assign the target.", "Click to continue.", true);
                if (GUILayout.Button("Dimmed input", GUILayout.Height(30))) clicks++;
                if (Event.current.type == EventType.Repaint) window.InputRect = GUILayoutUtility.GetLastRect();
                GUILayout.Space(120); DiNeTutorialBubble.EndFrame();
            };
            window.Render(); window.Render();
            Require(window.Error == null, "Nested scroll GUI failed: " + window.Error);
            Require(DiNeTutorialBubble.HasOverlay, "Scroll frame had no spotlight.");
            Require(Vector2.Distance(DiNeTutorialBubble.LastAnchorRect.center, expectedAnchor.center) < 1f, "Scroll anchor replay used content coordinates.");
            Require(viewport.Contains(DiNeTutorialBubble.LastAnchorRect.center), "Anchor highlight escaped its viewport.");
            foreach (Rect dim in DiNeTutorialBubble.LastDimmedRects)
                Require(!dim.Contains(DiNeTutorialBubble.LastAnchorRect.center) && !dim.Contains(DiNeTutorialBubble.LastRect.center), "Spotlight dimmed the target or bubble.");
            window.Click(window.InputRect.center); Require(clicks == 1, "Dimming consumed unrelated control input.");
            window.Draw = () => { DiNeTutorialBubble.BeginFrame(); GUILayout.Space(100); DiNeTutorialBubble.EndFrame(); };
            window.Render(); Require(!DiNeTutorialBubble.HasOverlay, "Inactive tool retained another spotlight.");
        }
        finally { window.Close(); }
    }

    public static void BubbleMouseInput()
    {
        var window = ScriptableObject.CreateInstance<GuidedTutorialProbe>();
        bool ready = true;
        var guide = new DiNeGuidedTutorial(window, "Regression." + Guid.NewGuid());
        try
        {
            guide.Configure("input", "Input", "입력", "入力", new[] {
                DiNeTutorialStep.Required("target", "Assign target.", "대상을 넣으세요.", "対象を指定。", () => ready),
                DiNeTutorialStep.Optional("optional", "Choose an option.", "옵션을 고르세요.", "オプションを選択。") });
            window.position = new Rect(50, 50, 540, 800); window.Show();
            window.Draw = () => {
                guide.BeginFrame(); GUILayout.Space(40); guide.DrawControls();
                Rect rect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true)); GUI.Box(rect, "Target"); guide.Draw("target", rect);
                guide.Draw("optional", rect); GUILayout.Space(80); guide.EndFrame();
            };
            guide.Start(); window.Render(); window.Render(); window.Click(DiNeTutorialBubble.LastRect.center); Flush(guide);
            Require(guide.CurrentStepId == "target", "Welcome mouse input failed.");
            window.Render(); window.Render(); window.Click(DiNeTutorialBubble.LastRect.center); Flush(guide);
            Require(guide.CurrentStepId == "optional", "Completed required bubble mouse input failed.");
            window.Render(); window.Render(); window.Click(DiNeTutorialBubble.LastRect.center); Flush(guide);
            Require(guide.CurrentStepId == "complete", "Optional bubble mouse input failed.");
            guide.Stop(); ready = false; guide.Start(); Next(guide); window.Render(); window.Render();
            window.Click(DiNeTutorialBubble.LastRect.center); Flush(guide);
            Require(guide.CurrentStepId == "target", "Incomplete required bubble accepted mouse input.");
            window.Draw = () => { guide.BeginFrame(); GUILayout.Space(40); guide.DrawControls(); guide.AbortFrame(); guide.EndFrame(); };
            window.Render();
            Require(!DiNeTutorialBubble.HasOverlay && guide.CurrentStepId == "target", "An aborted GUI event drew fallback guidance or advanced.");
            Require(window.Error == null, "Bubble GUI failed: " + window.Error);
        }
        finally { guide.Stop(); window.Close(); }
    }
}

public sealed class GuidedTutorialProbe : EditorWindow
{
    public Action Draw;
    public Exception Error;
    public Rect InputRect;
    private Vector2 origin;
    public void Render() { SendEvent(new Event { type = EventType.Layout }); SendEvent(new Event { type = EventType.Repaint }); }
    public void Click(Vector2 point)
    {
        SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point + origin });
        SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point + origin });
    }
    private void OnGUI()
    {
        origin = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
        try { Draw?.Invoke(); } catch (Exception exception) { Error = exception; }
    }
}
#endif
