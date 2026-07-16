#if UNITY_EDITOR
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("DiNe/Smart Toggle")]
public sealed class DiNeSmartToggle : MonoBehaviour
{
    public enum MenuPlacement
    {
        Root,
        Group
    }

    [SerializeField] private string displayName;
    [SerializeField] private string parameterName;
    [SerializeField] private bool defaultOn = true;
    [SerializeField] private bool saved = true;
    [SerializeField] private MenuPlacement menuPlacement = MenuPlacement.Group;
    [SerializeField] private string groupName = "Smart Toggles";
    [SerializeField] private Texture2D icon;

    [SerializeField, HideInInspector] private Vector2 iconEuler = new Vector2(0f, 180f);
    [SerializeField, HideInInspector] private Vector2 iconPan = Vector2.zero;
    [SerializeField, HideInInspector] private float iconZoom = 1f;
    [SerializeField, HideInInspector] private bool iconOutline;
    [SerializeField, HideInInspector] private Color iconOutlineColor = new Color(0.03f, 0.03f, 0.03f, 1f);
    [SerializeField, HideInInspector] private int iconOutlineSize = 4;
    [SerializeField, HideInInspector] private bool iconForbiddenOverlay;
    [SerializeField, HideInInspector] private float iconForbiddenOpacity = 1f;
    [SerializeField, HideInInspector] private float iconForbiddenScale = 0.85f;
    [SerializeField, HideInInspector] private bool iconForbiddenBehindObject = true;

    public string DisplayName { get => displayName; set => displayName = value; }
    public string ParameterName { get => parameterName; set => parameterName = value; }
    public bool DefaultOn { get => defaultOn; set => defaultOn = value; }
    public bool Saved { get => saved; set => saved = value; }
    public MenuPlacement Placement { get => menuPlacement; set => menuPlacement = value; }
    public string GroupName { get => groupName; set => groupName = value; }
    public Texture2D Icon { get => icon; set => icon = value; }
    public Vector2 IconEuler { get => iconEuler; set => iconEuler = value; }
    public Vector2 IconPan { get => iconPan; set => iconPan = value; }
    public float IconZoom { get => iconZoom; set => iconZoom = value; }
    public bool IconOutline { get => iconOutline; set => iconOutline = value; }
    public Color IconOutlineColor { get => iconOutlineColor; set => iconOutlineColor = value; }
    public int IconOutlineSize { get => iconOutlineSize; set => iconOutlineSize = value; }
    public bool IconForbiddenOverlay { get => iconForbiddenOverlay; set => iconForbiddenOverlay = value; }
    public float IconForbiddenOpacity { get => iconForbiddenOpacity; set => iconForbiddenOpacity = value; }
    public float IconForbiddenScale { get => iconForbiddenScale; set => iconForbiddenScale = value; }
    public bool IconForbiddenBehindObject { get => iconForbiddenBehindObject; set => iconForbiddenBehindObject = value; }

    private void Reset()
    {
        displayName = gameObject.name;
        parameterName = BuildDefaultParameterName(gameObject.name);
        defaultOn = gameObject.activeSelf;
    }

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameObject.name;
        if (string.IsNullOrWhiteSpace(parameterName))
            parameterName = BuildDefaultParameterName(gameObject.name);
        if (string.IsNullOrWhiteSpace(groupName))
            groupName = "Smart Toggles";
        iconZoom = Mathf.Clamp(iconZoom, 0.2f, 5f);
        iconOutlineSize = Mathf.Clamp(iconOutlineSize, 1, 12);
        iconForbiddenOpacity = Mathf.Clamp01(iconForbiddenOpacity);
        iconForbiddenScale = Mathf.Clamp(iconForbiddenScale, 0.2f, 1.2f);
    }

    public static string BuildDefaultParameterName(string objectName)
    {
        string value = string.IsNullOrWhiteSpace(objectName) ? "Object" : objectName.Trim();
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        value = value.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        return "DiNe/ST_" + value;
    }
}
#endif
