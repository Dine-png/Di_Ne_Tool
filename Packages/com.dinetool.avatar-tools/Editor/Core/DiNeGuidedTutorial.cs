#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>A localized instruction and, for required actions, a read-only completion check.</summary>
public sealed class DiNeTutorialStep
{
    public readonly string Id, English, Korean, Japanese;
    public readonly bool IsRequired, IsPrerequisite;
    public readonly Func<bool> IsComplete;
    public readonly Action OnEnter;

    private DiNeTutorialStep(string id, string en, string ko, string ja, Func<bool> complete,
        bool required, bool prerequisite, Action enter)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("A tutorial step needs a stable ID.", nameof(id));
        Id = id; English = en; Korean = ko; Japanese = ja;
        IsComplete = complete; IsRequired = required; IsPrerequisite = prerequisite; OnEnter = enter;
    }

    public static DiNeTutorialStep Required(string id, string enBody, string koBody, string jaBody,
        Func<bool> complete, bool prerequisite = true, Action onEnter = null)
    {
        if (complete == null) throw new ArgumentNullException(nameof(complete));
        return new DiNeTutorialStep(id, enBody, koBody, jaBody, complete, true, prerequisite, onEnter);
    }

    public static DiNeTutorialStep Optional(string id, string enBody, string koBody, string jaBody, Action onEnter = null)
        => new DiNeTutorialStep(id, enBody, koBody, jaBody, null, false, false, onEnter);

    public string Text(int language) => language == 1 ? Korean : language == 2 ? Japanese : English;
}

/// <summary>Session-only guidance shared by the existing Di Ne windows and inspectors.</summary>
public sealed class DiNeGuidedTutorial
{
    private const int SchemaVersion = 1;
    private readonly UnityEngine.Object owner;
    private readonly string toolId;
    private readonly Dictionary<string, DiNeTutorialBubble.Anchor> anchors = new Dictionary<string, DiNeTutorialBubble.Anchor>();
    private DiNeTutorialStep[] steps = new DiNeTutorialStep[0];
    private string courseId, sessionKey, enName, koName, jaName;
    private string enOverview, koOverview, jaOverview;
    private Action onStop;
    private int index = -1, schema, pending = int.MinValue;
    private int controlGeneration;
    private bool active, wasComplete, focusPending, drawn, frameOpen, suspended, aborted;
    private bool lastExpanded;
    private DiNeTutorialBubble.Anchor controlsAnchor;

    public bool IsActive => active && OwnsSession;
    public bool IsExpanded
    {
        get => EditorPrefs.GetBool("DiNe.Tutorial." + toolId + ".Expanded", true);
        set
        {
            if (IsExpanded == value) return;
            EditorPrefs.SetBool("DiNe.Tutorial." + toolId + ".Expanded", value);
            ApplyVisibility(value);
            Repaint();
        }
    }
    public string CurrentStepId => index >= 0 && index < steps.Length ? steps[index].Id : index < 0 ? "welcome" : "complete";
    public int CurrentStepIndex => index;
    public int StepCount => steps.Length;
    public bool CanClickCurrent => IsActive && IsExpanded && (index < 0 || index >= steps.Length ||
        !steps[index].IsRequired || (wasComplete && Complete(steps[index])));
    private bool OwnsSession => owner != null && sessionKey != null &&
        SessionState.GetInt(sessionKey + ".Owner", 0) == owner.GetInstanceID();
    private int Language => Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
    private string CourseName => L(enName, koName, jaName);
    private string CourseOverview => L(enOverview, koOverview, jaOverview);

    public DiNeGuidedTutorial(UnityEngine.Object owner, string toolId)
    {
        this.owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        this.toolId = !string.IsNullOrEmpty(toolId) ? toolId : throw new ArgumentException("A tutorial needs a tool ID.", nameof(toolId));
        lastExpanded = IsExpanded;
    }

    private void ApplyVisibility(bool expanded)
    {
        lastExpanded = expanded;
        CancelPending(); DiNeTutorialBubble.ClearFrame();
        if (expanded && IsActive)
        {
            if (index >= 0 && index < steps.Length) steps[index].OnEnter?.Invoke();
            CaptureEntry(); focusPending = true;
        }
    }

    /// <summary>Select the visible feature's course and localized purpose. Reconfiguring the same course preserves its progress.</summary>
    public void Configure(string courseId, string enName, string koName, string jaName, DiNeTutorialStep[] steps,
        int version = 1, Action onStop = null, string overviewEn = null, string overviewKo = null, string overviewJa = null)
    {
        if (steps == null) throw new ArgumentNullException(nameof(steps));
        enOverview = overviewEn; koOverview = overviewKo; jaOverview = overviewJa;
        int nextSchema = SchemaVersion;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        unchecked
        {
            nextSchema = nextSchema * 31 + version;
            foreach (var step in steps)
            {
                if (step == null || !ids.Add(step.Id)) throw new ArgumentException("Tutorial step IDs must be unique.", nameof(steps));
                foreach (char c in step.Id) nextSchema = nextSchema * 31 + c;
                nextSchema = nextSchema * 31 + (step.IsRequired ? 1 : 0);
            }
        }
        if (this.courseId == courseId && schema == nextSchema && !suspended)
        {
            this.steps = steps; this.enName = enName; this.koName = koName; this.jaName = jaName; this.onStop = onStop;
            return;
        }
        Suspend();
        this.courseId = courseId; schema = nextSchema;
        suspended = false;
        this.steps = steps; this.enName = enName; this.koName = koName; this.jaName = jaName; this.onStop = onStop;
        var editor = owner as Editor;
        int targetId = editor != null && editor.target != null ? editor.target.GetInstanceID() : owner.GetInstanceID();
        sessionKey = $"DiNe.Guide.{toolId}.{courseId}.{targetId}";
        index = SessionState.GetInt(sessionKey + ".Index", -2);
        active = SessionState.GetInt(sessionKey + ".Schema", 0) == schema && index >= -1 && index <= steps.Length;
        var priorOwner = EditorUtility.InstanceIDToObject(SessionState.GetInt(sessionKey + ".Owner", 0));
        if (priorOwner != null && priorOwner != owner) active = false;
        if (active)
        {
            SessionState.SetInt(sessionKey + ".Owner", owner.GetInstanceID());
            if (IsExpanded && index >= 0 && index < steps.Length) steps[index].OnEnter?.Invoke();
            CaptureEntry(); focusPending = true;
        }
        else index = -1;
    }

    public void BeginFrame()
    {
        if (lastExpanded != IsExpanded) ApplyVisibility(IsExpanded);
        anchors.Clear(); drawn = false; controlsAnchor = default(DiNeTutorialBubble.Anchor);
        frameOpen = true;
        aborted = false;
        DiNeTutorialBubble.BeginFrame();
    }

    public void EndFrame()
    {
        if (!frameOpen) return;
        try
        {
            if (aborted) return;
            Validate();
            // Hidden or unavailable optional controls remain escapable without changing user data.
            if (IsActive && IsExpanded && !drawn && pending == int.MinValue && index >= 0 && index < steps.Length)
                Draw(CurrentStepId, controlsAnchor);
        }
        finally
        {
            frameOpen = false;
            DiNeTutorialBubble.EndFrame();
        }
    }

    /// <summary>Discard guidance while Unity exits the current GUI event.</summary>
    public void AbortFrame()
    {
        aborted = true;
        DiNeTutorialBubble.ClearFrame();
    }

    public void BeginScrollScope() => DiNeTutorialBubble.BeginScrollScope();
    public void EndScrollScope(Rect viewport) => DiNeTutorialBubble.EndScrollScope(viewport);

    public void DrawControls()
    {
        if (courseId == null) return;
        if (active && !OwnsSession) { active = false; CancelPending(); }
        Color bg = GUI.backgroundColor;
        bool changed = GUI.changed, enabled = GUI.enabled;
        try
        {
            GUI.enabled = true;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            IsExpanded = EditorGUILayout.Foldout(IsExpanded, new GUIContent(L("Tutorial", "튜토리얼", "チュートリアル"),
                L("Collapse or expand the instructions. Collapsing pauses guidance and keeps your current step.",
                    "튜토리얼 안내를 접거나 펼칩니다. 접어 두면 현재 단계를 유지한 채 안내를 잠시 숨깁니다.",
                    "案内を折りたたむ・開く操作です。折りたたむと現在のステップを保ったまま案内を一時的に隠します。")), true, EditorStyles.foldoutHeader);
            if (!IsExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }
            if (IsActive) EditorGUILayout.LabelField($"{index + 2} / {steps.Length + 2} · {CourseName}", EditorStyles.miniLabel);
            else if (!string.IsNullOrWhiteSpace(CourseOverview))
            {
                EditorGUILayout.LabelField(CourseOverview, EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
            }
            EditorGUILayout.BeginHorizontal();
            if (!IsActive)
            {
                GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
                if (GUILayout.Button(new GUIContent(L("Start guided tutorial", "말풍선 튜토리얼 시작", "吹き出しチュートリアルを開始"),
                    CourseName), GUILayout.Height(30))) ScheduleControl(Start);
            }
            else
            {
                if (GUILayout.Button(new GUIContent(L("Restart", "처음부터", "最初から"), L("Restart this tutorial.", "첫 단계부터 다시 시작합니다.", "最初のステップから再開します。")), GUILayout.Height(24))) ScheduleControl(Start);
                if (GUILayout.Button(new GUIContent(L("End tutorial", "튜토리얼 종료", "終了"), L("Close the speech-bubble guidance.", "말풍선 안내를 끝냅니다.", "吹き出しの案内を終了します。")), GUILayout.Height(24))) ScheduleControl(Stop);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            controlsAnchor = DiNeTutorialBubble.CaptureAnchor(GUILayoutUtility.GetLastRect());
            if (IsActive && (index < 0 || index >= steps.Length)) DrawBuiltin();
        }
        finally { GUI.backgroundColor = bg; GUI.enabled = enabled; GUI.changed = changed; }
    }

    public void Anchor(string id, Rect rect) => anchors[id] = DiNeTutorialBubble.CaptureAnchor(rect);
    public void Draw(string id, Rect rect) { Anchor(id, rect); Draw(id); }
    public void Draw(string id)
    {
        if (anchors.TryGetValue(id, out var anchor)) Draw(id, anchor);
    }

    private void Draw(string id, DiNeTutorialBubble.Anchor anchor)
    {
        if (!IsActive || !IsExpanded || drawn || CurrentStepId != id || index < 0 || index >= steps.Length || pending != int.MinValue) return;
        drawn = true;
        var step = steps[index];
        string hint = step.IsRequired
            ? CanClickCurrent ? L("Already set. Click to continue.", "설정 완료. 말풍선을 눌러 계속하세요.", "設定済みです。クリックで次へ。")
                : L("Complete this action to continue.", "이 동작을 완료하면 넘어갑니다.", "この操作を完了すると進みます。")
            : L("Click to continue.", "말풍선을 눌러 넘어가세요.", "クリックして次へ。");
        bool enabled = GUI.enabled;
        try
        {
            GUI.enabled = true;
            if (DiNeTutorialBubble.Draw(anchor, $"{index + 2} / {steps.Length + 2} · {CourseName}", step.Text(Language), hint, CanClickCurrent)) Advance();
            Focus(anchor);
        }
        finally { GUI.enabled = enabled; }
    }

    private void DrawBuiltin()
    {
        if (drawn || !IsExpanded) return;
        drawn = true;
        bool welcome = index < 0;
        string startHint = L("Follow the highlighted controls. Click to begin.", "강조된 조작을 따라 하세요. 말풍선을 누르면 시작합니다.", "強調された操作を進めます。クリックして開始します。");
        bool hasOverview = !string.IsNullOrWhiteSpace(CourseOverview);
        string body = welcome
            ? (hasOverview ? CourseOverview : startHint)
            : L("Click to finish the tutorial.", "말풍선을 눌러 튜토리얼을 마치세요.", "クリックしてチュートリアルを終了してください。");
        if (DiNeTutorialBubble.Draw(controlsAnchor, $"{index + 2} / {steps.Length + 2} · {CourseName}", body, welcome && hasOverview ? startHint : "", true)) Advance();
        Focus(controlsAnchor);
    }

    private void Focus(DiNeTutorialBubble.Anchor anchor)
    {
        if (!focusPending || pending != int.MinValue || Event.current.type != EventType.Repaint) return;
        Rect local = DiNeTutorialBubble.ToLocalRect(anchor), bubble = DiNeTutorialBubble.LastRect;
        bool changed = GUI.changed;
        GUI.ScrollTo(Rect.MinMaxRect(Mathf.Min(local.xMin, bubble.xMin), Mathf.Min(local.yMin, bubble.yMin),
            Mathf.Max(local.xMax, bubble.xMax), Mathf.Max(local.yMax, bubble.yMax)));
        GUI.changed = changed; focusPending = false;
    }

    public void NotifyAction(string id)
    {
        if (!IsActive || !IsExpanded || index < 0 || index >= steps.Length || CurrentStepId != id || !steps[index].IsRequired) return;
        // Serialized fields may not be applied until the end of this GUI event.
        // Flush checks the final state before accepting the transition.
        Queue(index + 1);
    }

    public void Validate()
    {
        if (!IsActive || !IsExpanded || pending != int.MinValue) return;
        for (int i = 0; i < Mathf.Min(index, steps.Length); i++)
            if (steps[i].IsRequired && steps[i].IsPrerequisite && !Complete(steps[i])) { Queue(i); return; }
        // Assignments in another inspector and Play mode also complete real actions.
        // A step that was already complete on entry still waits for a bubble click.
        if (index >= 0 && index < steps.Length && steps[index].IsRequired && !wasComplete && Complete(steps[index]))
            Queue(index + 1);
    }

    public void Start()
    {
        if (owner == null || sessionKey == null) return;
        CancelPending(); DiNeTutorialBubble.ClearFrame();
        SessionState.SetInt(sessionKey + ".Owner", owner.GetInstanceID());
        active = true; index = -1; focusPending = true; CaptureEntry(); Save(); Repaint();
    }

    public void Stop()
    {
        bool owned = OwnsSession;
        CancelPending(); DiNeTutorialBubble.ClearFrame();
        active = false; wasComplete = false;
        if (owned) { SessionState.SetInt(sessionKey + ".Index", -2); SessionState.SetInt(sessionKey + ".Owner", 0); onStop?.Invoke(); }
        Repaint();
    }

    public void Suspend()
    {
        CancelPending();
        if (OwnsSession)
        {
            Save(); SessionState.SetInt(sessionKey + ".Owner", 0);
            if (active) onStop?.Invoke();
        }
        active = false;
        suspended = true;
    }

    private void Advance()
    {
        if (!CanClickCurrent) return;
        if (index >= steps.Length) ScheduleControl(Stop);
        else Queue(index + 1);
    }

    private void Queue(int next)
    {
        if (!IsActive || !IsExpanded || pending != int.MinValue) return;
        pending = next; DiNeTutorialBubble.ClearFrame();
        EditorApplication.delayCall -= Flush;
        EditorApplication.delayCall += Flush;
        Repaint();
    }

    private void Flush()
    {
        int next = pending; pending = int.MinValue;
        EditorApplication.delayCall -= Flush;
        if (!IsActive || !IsExpanded || next < -1 || next > steps.Length) return;
        if (next == index + 1 && index >= 0 && index < steps.Length && steps[index].IsRequired && !Complete(steps[index])) return;
        DiNeTutorialBubble.ClearFrame(); index = next;
        if (index >= 0 && index < steps.Length) steps[index].OnEnter?.Invoke();
        CaptureEntry();
        focusPending = true; Save(); Repaint();
    }

    private void CaptureEntry() => wasComplete = index >= 0 && index < steps.Length && steps[index].IsRequired && Complete(steps[index]);
    private static bool Complete(DiNeTutorialStep step) => step.IsComplete != null && step.IsComplete();
    private void Save()
    {
        if (!OwnsSession) return;
        SessionState.SetInt(sessionKey + ".Schema", schema);
        SessionState.SetInt(sessionKey + ".Index", active ? index : -2);
    }
    private void CancelPending() { EditorApplication.delayCall -= Flush; pending = int.MinValue; controlGeneration++; }
    private void ScheduleControl(Action action)
    {
        string requestedCourse = courseId;
        int generation = controlGeneration;
        EditorApplication.delayCall += () => { if (owner != null && generation == controlGeneration && courseId == requestedCourse) action(); };
    }
    private void Repaint()
    {
        if (owner is EditorWindow window) window.Repaint();
        else if (owner is Editor editor) editor.Repaint();
    }
    private string L(string en, string ko, string ja) => Language == 1 ? ko : Language == 2 ? ja : en;
}
#endif
