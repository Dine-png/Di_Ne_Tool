#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Shared identity and controls for every Di Ne editor surface.</summary>
public static class DiNeEditorUI
{
    public static readonly Color Mint = new Color(0.30f, 0.82f, 0.76f, 1f);
    public static readonly Color Inactive = new Color(0.50f, 0.50f, 0.50f, 1f);
    public static readonly Color MutedText = new Color(0.80f, 0.80f, 0.80f, 1f);
    public static readonly Color HeaderBackground = new Color(0.90f, 0.90f, 0.90f, 1f);

    public const float IconSize = 72f;
    public const float TitleSize = 36f;
    public const float DescriptionSize = 12f;
    public const float CompactButtonHeight = 24f;
    public const float ButtonHeight = 30f;
    public const float ToolbarHeight = 35f;
    public const float CardSpacing = 8f;

    private static readonly string[] LanguageLabels = { "English", "한국어", "日本語" };
    private static GUISkin skin;
    private static bool proSkin;
    private static Texture2D icon;
    private static Font titleFont;
    private static GUIStyle titleStyle, descriptionStyle, selectedStyle, unselectedStyle;

    public static GUIStyle CardStyle => EditorStyles.helpBox;

    private static void EnsureStyles()
    {
        if (titleStyle != null && skin == GUI.skin && proSkin == EditorGUIUtility.isProSkin
            && ButtonBackgroundsMatch(selectedStyle, GUI.skin.button))
            return;

        skin = GUI.skin;
        proSkin = EditorGUIUtility.isProSkin;
        icon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
        titleStyle = new GUIStyle(EditorStyles.label)
        {
            font = titleFont,
            fontSize = (int)TitleSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            fixedHeight = 0f,
            fixedWidth = 0f,
            contentOffset = Vector2.zero,
        };
        descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = (int)DescriptionSize,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 0f,
            fixedWidth = 0f,
            contentOffset = Vector2.zero,
        };
        descriptionStyle.normal.textColor = MutedText;
        selectedStyle = CreateButtonStyle(true);
        unselectedStyle = CreateButtonStyle(false);
    }

    private static GUIStyle CreateButtonStyle(bool selected)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 0f,
            fixedWidth = 0f,
            contentOffset = Vector2.zero,
        };
        Color text = selected ? Color.white : MutedText;
        style.normal.textColor = text;
        style.hover.textColor = text;
        style.active.textColor = text;
        style.focused.textColor = text;
        style.onNormal.textColor = text;
        style.onHover.textColor = text;
        style.onActive.textColor = text;
        style.onFocused.textColor = text;
        return style;
    }

    private static bool BackgroundsMatch(GUIStyleState cached, GUIStyleState source)
    {
        if (cached.background != source.background) return false;
        var left = cached.scaledBackgrounds;
        var right = source.scaledBackgrounds;
        int count = left == null ? 0 : left.Length;
        if (count != (right == null ? 0 : right.Length)) return false;
        for (int i = 0; i < count; i++) if (left[i] != right[i]) return false;
        return true;
    }

    private static bool ButtonBackgroundsMatch(GUIStyle cached, GUIStyle source)
    {
        return cached != null && BackgroundsMatch(cached.normal, source.normal)
            && BackgroundsMatch(cached.hover, source.hover) && BackgroundsMatch(cached.active, source.active)
            && BackgroundsMatch(cached.focused, source.focused) && BackgroundsMatch(cached.onNormal, source.onNormal)
            && BackgroundsMatch(cached.onHover, source.onHover) && BackgroundsMatch(cached.onActive, source.onActive)
            && BackgroundsMatch(cached.onFocused, source.onFocused);
    }

    /// <summary>Draw the boxed identity block; callers own the 5/15 px language spacing.</summary>
    public static void DrawHeader(string title, string description)
    {
        EnsureStyles();
        Color previousBackground = GUI.backgroundColor;
        try
        {
            GUI.backgroundColor = HeaderBackground;
            // Reserve one deterministic block. Nested layout groups can otherwise
            // retain old row heights when a docked view changes width or language.
            const float padding = 6f;
            const float gap = 6f;
            float available = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 24f);
            float titleWidth = Mathf.Min(titleStyle.CalcSize(new GUIContent(title)).x,
                Mathf.Max(1f, available - IconSize - gap));
            float rowHeight = Mathf.Max(IconSize, titleStyle.CalcHeight(new GUIContent(title), titleWidth));
            float descriptionHeight = descriptionStyle.CalcHeight(new GUIContent(description), available);
            Rect header = GUILayoutUtility.GetRect(0f, rowHeight + descriptionHeight + 9f + padding * 2f,
                GUILayout.ExpandWidth(true));
            GUI.Box(header, GUIContent.none, "box");
            float contentWidth = Mathf.Max(1f, header.width - padding * 2f);
            titleWidth = Mathf.Min(titleWidth, Mathf.Max(1f, contentWidth - IconSize - gap));
            float start = header.x + (header.width - IconSize - gap - titleWidth) * 0.5f;
            if (icon != null)
                GUI.DrawTexture(new Rect(start, header.y + padding + (rowHeight - IconSize) * 0.5f,
                    IconSize, IconSize), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(start + IconSize + gap, header.y + padding, titleWidth, rowHeight), title, titleStyle);
            GUI.Label(new Rect(header.x + padding, header.y + padding + rowHeight + 4f,
                contentWidth, descriptionHeight), description, descriptionStyle);
        }
        finally { GUI.backgroundColor = previousBackground; }
    }

    public static int DrawLanguageToolbar(int selected)
    {
        int current = Mathf.Clamp(selected, 0, LanguageLabels.Length - 1);
        int next = DrawToolbar(current, LanguageLabels);
        if (next != current) EditorPrefs.SetInt("DiNeLang", next);
        return next;
    }

    public static int DrawToolbar(int selected, string[] options, float height = ToolbarHeight)
    {
        EnsureStyles();
        if (options.Length == 0) return selected;
        int next = selected;
        Color previousBackground = GUI.backgroundColor;
        try
        {
            Rect row = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            float width = Mathf.Max(0f, (row.width - 4f * (options.Length - 1)) / options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                GUI.backgroundColor = i == selected ? Mint : Inactive;
                if (GUI.Button(new Rect(row.x + i * (width + 4f), row.y, width, height),
                    options[i], i == selected ? selectedStyle : unselectedStyle)) next = i;
            }
        }
        finally { GUI.backgroundColor = previousBackground; }
        return next;
    }

    public static int DrawToolbar(int selected, GUIContent[] options, float height = ToolbarHeight)
    {
        EnsureStyles();
        if (options.Length == 0) return selected;
        int next = selected;
        Color previousBackground = GUI.backgroundColor;
        try
        {
            Rect row = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            float width = Mathf.Max(0f, (row.width - 4f * (options.Length - 1)) / options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                GUI.backgroundColor = i == selected ? Mint : Inactive;
                if (GUI.Button(new Rect(row.x + i * (width + 4f), row.y, width, height),
                    options[i], i == selected ? selectedStyle : unselectedStyle)) next = i;
            }
        }
        finally { GUI.backgroundColor = previousBackground; }
        return next;
    }

    public static bool Button(string label, float height = ButtonHeight, params GUILayoutOption[] options)
    {
        return Button(new GUIContent(label), height, options);
    }

    public static bool Button(GUIContent label, float height = ButtonHeight, params GUILayoutOption[] options)
    {
        return SegmentButton(label, true, height, options);
    }

    // Unlike an index-returning toolbar, this keeps actions on a repeated selected click.
    public static bool SegmentButton(string label, bool selected, float height = ToolbarHeight, params GUILayoutOption[] options)
    {
        return SegmentButton(new GUIContent(label), selected, height, options);
    }

    public static bool SegmentButton(GUIContent label, bool selected, float height = ToolbarHeight, params GUILayoutOption[] options)
    {
        EnsureStyles();
        var layout = new GUILayoutOption[options.Length + 1];
        layout[0] = GUILayout.Height(height);
        options.CopyTo(layout, 1);
        Color previousBackground = GUI.backgroundColor;
        try
        {
            GUI.backgroundColor = selected ? Mint : Inactive;
            return GUILayout.Button(label, selected ? selectedStyle : unselectedStyle, layout);
        }
        finally { GUI.backgroundColor = previousBackground; }
    }
}
#endif
