#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>Inline tutorial guidance anchored to an existing IMGUI control.</summary>
public static class DiNeTutorialBubble
{
    private static readonly Color Mint = new Color(0.30f, 0.82f, 0.76f, 1f);
    private static readonly Color CardTint = new Color(0.50f, 0.50f, 0.50f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.56f);
    private const float TailHeight = 8f;
    private const float BorderWidth = 1.5f;
    private const float SpotlightPadding = 3f;
    private static readonly Rect[] NoRects = new Rect[0];
    private static GUIStyle cardStyle, titleStyle, bodyStyle, hintStyle;
    private static bool cachedProSkin;
    private static TutorialFrame currentFrame;

    private sealed class TutorialFrame
    {
        public readonly Matrix4x4 matrix = GUI.matrix;
        public readonly List<BubbleCapture> bubbles = new List<BubbleCapture>();
        public readonly List<ClipScope> scopes = new List<ClipScope>();
    }

    internal sealed class ClipScope
    {
        internal Rect screenRect;
        internal bool resolved;
    }

    /// <summary>An anchor keeps its screen position and the scroll viewports that contain it.</summary>
    public struct Anchor
    {
        internal Rect screenRect;
        internal ClipScope[] scopes;
    }

    private struct BubbleCapture
    {
        public Anchor anchor;
        public Rect tail, card, bubble, titleRect, bodyRect, hintRect;
        public ClipScope[] scopes;
        public string title, body, hint;
        public bool enabled;
    }

    private struct GuiState
    {
        public Color color, background, content;
        public bool enabled, changed;
        public Matrix4x4 matrix;
        public int indent;
        public float labelWidth;

        public static GuiState Capture() => new GuiState
        {
            color = GUI.color,
            background = GUI.backgroundColor,
            content = GUI.contentColor,
            enabled = GUI.enabled,
            changed = GUI.changed,
            matrix = GUI.matrix,
            indent = EditorGUI.indentLevel,
            labelWidth = EditorGUIUtility.labelWidth
        };

        public void Restore()
        {
            GUI.color = color;
            GUI.backgroundColor = background;
            GUI.contentColor = content;
            GUI.enabled = enabled;
            GUI.changed = changed;
            GUI.matrix = matrix;
            EditorGUI.indentLevel = indent;
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }

    // The full bubble bounds, in the caller's GUI coordinates, after its last repaint.
    internal static Rect LastRect { get; private set; }
    internal static Rect LastOverlayRect { get; private set; }
    internal static Rect LastAnchorRect { get; private set; }
    internal static Rect[] LastDimmedRects { get; private set; } = NoRects;
    internal static bool HasOverlay { get; private set; }

    /// <summary>Begin one balanced frame around a tool's complete inspector body.</summary>
    public static void BeginFrame()
    {
        // A fresh frame never uses a previous inspector, event, or tutorial step.
        currentFrame = new TutorialFrame();
        ClearFrame();
        EditorGUILayout.BeginVertical(GUIStyle.none);
    }

    /// <summary>Discard captured guidance after a tutorial step changes or ends.</summary>
    public static void ClearFrame()
    {
        currentFrame?.bubbles.Clear();
        LastRect = LastOverlayRect = LastAnchorRect = Rect.zero;
        LastDimmedRects = NoRects;
        HasOverlay = false;
    }

    public static Anchor CaptureAnchor(Rect rect) => new Anchor
    {
        screenRect = ToScreenRect(rect), scopes = currentFrame?.scopes.ToArray()
    };

    public static Rect ToLocalRect(Anchor anchor) => FromScreenRect(anchor.screenRect);

    /// <summary>Call immediately before BeginScrollView, and balance after EndScrollView.</summary>
    public static void BeginScrollScope()
    {
        currentFrame?.scopes.Add(new ClipScope());
    }

    public static void EndScrollScope(Rect viewport)
    {
        if (currentFrame == null || currentFrame.scopes.Count == 0) return;
        int last = currentFrame.scopes.Count - 1;
        ClipScope scope = currentFrame.scopes[last];
        scope.screenRect = ToScreenRect(viewport); scope.resolved = true;
        currentFrame.scopes.RemoveAt(last);
    }

    /// <summary>
    /// End the frame after all normal controls. Repaint-only dimming preserves
    /// both the bubble and its target, and never captures or consumes input.
    /// </summary>
    public static void EndFrame()
    {
        var frame = currentFrame;
        currentFrame = null;
        if (frame == null) return;

        EditorGUILayout.EndVertical();
        Rect bounds = GUILayoutUtility.GetLastRect();
        if (Event.current.type != EventType.Repaint || frame.bubbles.Count == 0 ||
            bounds.width <= 0f || bounds.height <= 0f) return;

        var state = GuiState.Capture();
        try
        {
            GUI.matrix = frame.matrix;
            var holes = new List<Rect>(frame.bubbles.Count * 2);
            foreach (var bubble in frame.bubbles)
            {
                AddPositiveRect(holes, Clip(Expand(ToLocalRect(bubble.anchor), SpotlightPadding), bounds, bubble.anchor.scopes));
                AddPositiveRect(holes, Clip(Expand(FromScreenRect(bubble.bubble), SpotlightPadding), bounds, bubble.scopes));
            }
            LastOverlayRect = bounds;
            LastDimmedRects = SubtractHoles(bounds, holes);
            HasOverlay = true;
            GUI.color = GUI.contentColor = Color.white;
            GUI.backgroundColor = CardTint;
            foreach (var dimmed in LastDimmedRects) EditorGUI.DrawRect(dimmed, DimColor);
            foreach (var bubble in frame.bubbles)
            {
                GUI.enabled = bubble.enabled;
                Rect anchor = ToLocalRect(bubble.anchor), card = FromScreenRect(bubble.card);
                Rect visibleAnchor = Clip(anchor, bounds, bubble.anchor.scopes);
                Rect visibleBubble = Clip(FromScreenRect(bubble.bubble), bounds, bubble.scopes);
                LastRect = visibleBubble; LastAnchorRect = visibleAnchor;
                if (visibleAnchor.width > 0f && visibleAnchor.height > 0f)
                {
                    GUI.BeginGroup(visibleAnchor);
                    try { DrawBorder(Offset(anchor, visibleAnchor)); }
                    finally { GUI.EndGroup(); }
                }
                if (visibleBubble.width <= 0f || visibleBubble.height <= 0f) continue;
                GUI.BeginGroup(visibleBubble);
                try
                {
                    Rect localCard = Offset(card, visibleBubble);
                    GUI.Box(localCard, GUIContent.none, cardStyle);
                    if (!string.IsNullOrEmpty(bubble.title)) GUI.Label(FromScreenRect(bubble.titleRect), bubble.title, titleStyle);
                    if (!string.IsNullOrEmpty(bubble.body)) GUI.Label(FromScreenRect(bubble.bodyRect), bubble.body, bodyStyle);
                    if (!string.IsNullOrEmpty(bubble.hint)) GUI.Label(FromScreenRect(bubble.hintRect), bubble.hint, hintStyle);
                    DrawTail(FromScreenRect(bubble.tail), localCard, Offset(anchor, visibleBubble));
                    DrawBorder(localCard);
                }
                finally { GUI.EndGroup(); }
            }
        }
        finally { state.Restore(); }
    }

    /// <summary>
    /// Reserves guidance below the caller's control and returns true only when an
    /// optional bubble is clicked. Required guidance leaves input to that control.
    /// Strings are localized by the calling tool using its shared DiNeLang value.
    /// </summary>
    public static bool Draw(Rect anchor, string title, string body, string hint, bool advanceOnClick)
        => Draw(CaptureAnchor(anchor), title, body, hint, advanceOnClick);

    public static bool Draw(Anchor anchor, string title, string body, string hint, bool advanceOnClick)
    {
        EnsureStyles();
        var state = GuiState.Capture();

        try
        {
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = CardTint;
            EditorGUI.indentLevel = 0;

            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical(GUIStyle.none);
            Rect tailRect = GUILayoutUtility.GetRect(0f, TailHeight, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical(cardStyle);
            Rect titleRect = Rect.zero, bodyRect = Rect.zero, hintRect = Rect.zero;
            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(title, titleStyle, GUILayout.ExpandWidth(true));
                titleRect = GUILayoutUtility.GetLastRect();
            }
            if (!string.IsNullOrEmpty(body))
            {
                if (!string.IsNullOrEmpty(title)) GUILayout.Space(4f);
                GUILayout.Label(body, bodyStyle, GUILayout.ExpandWidth(true));
                bodyRect = GUILayoutUtility.GetLastRect();
            }
            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Space(4f);
                GUILayout.Label(hint, hintStyle, GUILayout.ExpandWidth(true));
                hintRect = GUILayoutUtility.GetLastRect();
            }
            EditorGUILayout.EndVertical();
            Rect cardRect = GUILayoutUtility.GetLastRect();
            EditorGUILayout.EndVertical();
            Rect bubbleRect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.Repaint)
            {
                LastRect = bubbleRect;
                LastAnchorRect = ToLocalRect(anchor);
                if (currentFrame != null)
                    currentFrame.bubbles.Add(new BubbleCapture
                    {
                        anchor = anchor, tail = ToScreenRect(tailRect), card = ToScreenRect(cardRect), bubble = ToScreenRect(bubbleRect),
                        titleRect = ToScreenRect(titleRect), bodyRect = ToScreenRect(bodyRect), hintRect = ToScreenRect(hintRect),
                        scopes = currentFrame.scopes.ToArray(),
                        title = title, body = body, hint = hint, enabled = GUI.enabled
                    });
                if (currentFrame == null) DrawBorder(ToLocalRect(anchor));
                DrawTail(tailRect, cardRect, ToLocalRect(anchor));
                DrawBorder(cardRect);
            }

            bool advanced = false;
            if (advanceOnClick)
            {
                EditorGUIUtility.AddCursorRect(bubbleRect, MouseCursor.Link);
                advanced = GUI.Button(bubbleRect, GUIContent.none, GUIStyle.none);
            }
            GUILayout.Space(4f);
            return advanced;
        }
        finally { state.Restore(); }
    }

    private static Rect Expand(Rect rect, float padding)
        => new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);

    private static Rect ToScreenRect(Rect rect)
    {
        Vector2 min = GUIUtility.GUIToScreenPoint(rect.min), max = GUIUtility.GUIToScreenPoint(rect.max);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    private static Rect FromScreenRect(Rect rect)
    {
        Vector2 min = GUIUtility.ScreenToGUIPoint(rect.min), max = GUIUtility.ScreenToGUIPoint(rect.max);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    // Inside BeginGroup screen conversion already includes the group's offset.
    private static Rect Offset(Rect rect, Rect origin) => new Rect(rect.x - origin.x, rect.y - origin.y, rect.width, rect.height);
    private static Rect Intersect(Rect a, Rect b)
    {
        float minX = Mathf.Max(a.xMin, b.xMin), minY = Mathf.Max(a.yMin, b.yMin);
        float maxX = Mathf.Min(a.xMax, b.xMax), maxY = Mathf.Min(a.yMax, b.yMax);
        return maxX <= minX || maxY <= minY ? Rect.zero : Rect.MinMaxRect(minX, minY, maxX, maxY);
    }
    private static Rect Clip(Rect rect, Rect bounds, ClipScope[] scopes)
    {
        rect = Intersect(rect, bounds);
        if (scopes != null) foreach (var scope in scopes)
        {
            if (!scope.resolved) return Rect.zero;
            rect = Intersect(rect, FromScreenRect(scope.screenRect));
        }
        return rect;
    }

    private static Rect[] SubtractHoles(Rect bounds, List<Rect> holes)
    {
        var remaining = new List<Rect> { bounds };
        foreach (var hole in holes)
        {
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                Rect rect = remaining[i];
                float left = Mathf.Max(rect.xMin, hole.xMin), right = Mathf.Min(rect.xMax, hole.xMax);
                float top = Mathf.Max(rect.yMin, hole.yMin), bottom = Mathf.Min(rect.yMax, hole.yMax);
                if (right <= left || bottom <= top) continue;
                remaining.RemoveAt(i);
                // Four disjoint tiles around the intersection avoid double dimming
                // when spotlight holes overlap or extend beyond the inspector.
                AddPositiveRect(remaining, Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMax, top));
                AddPositiveRect(remaining, Rect.MinMaxRect(rect.xMin, bottom, rect.xMax, rect.yMax));
                AddPositiveRect(remaining, Rect.MinMaxRect(rect.xMin, top, left, bottom));
                AddPositiveRect(remaining, Rect.MinMaxRect(right, top, rect.xMax, bottom));
            }
        }
        return remaining.ToArray();
    }

    private static void AddPositiveRect(List<Rect> rects, Rect rect)
    {
        if (rect.width > 0f && rect.height > 0f) rects.Add(rect);
    }

    private static void EnsureStyles()
    {
        if (cardStyle != null && cachedProSkin == EditorGUIUtility.isProSkin) return;
        cachedProSkin = EditorGUIUtility.isProSkin;
        cardStyle = new GUIStyle(EditorStyles.helpBox)
        {
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(10, 10, 8, 8)
        };
        titleStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, richText = false };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false };
        bodyStyle.normal.textColor = Color.white;
        hintStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = false };
        hintStyle.normal.textColor = new Color(0.80f, 0.80f, 0.80f, 1f);
    }

    private static void DrawBorder(Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, BorderWidth), Mint);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BorderWidth, rect.width, BorderWidth), Mint);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, BorderWidth, rect.height), Mint);
        EditorGUI.DrawRect(new Rect(rect.xMax - BorderWidth, rect.y, BorderWidth, rect.height), Mint);
    }

    private static void DrawTail(Rect tail, Rect card, Rect anchor)
    {
        if (tail.width <= 0f || card.width <= 0f) return;
        float halfBase = Mathf.Min(TailHeight, card.width * 0.25f);
        float center = anchor.width > 0f ? anchor.center.x : card.center.x;
        center = Mathf.Clamp(center, card.x + halfBase, card.xMax - halfBase);
        // Scan lines keep the triangle in the same GUI clipping/matrix scope as
        // the caller without changing Handles state or allocating vertex arrays.
        float height = Mathf.Max(1f, card.y - tail.y);
        for (float y = 0f; y < height; y += 1f)
        {
            float width = Mathf.Max(1f, halfBase * 2f * ((y + 1f) / height));
            EditorGUI.DrawRect(new Rect(center - width * 0.5f, tail.y + y, width, 1f), Mint);
        }
    }
}
#endif
