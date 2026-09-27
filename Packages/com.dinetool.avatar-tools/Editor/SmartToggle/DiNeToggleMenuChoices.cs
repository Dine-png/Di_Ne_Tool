#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

// Shared destination picker. Paths use escaped control names plus their occurrence,
// so inserting unrelated root controls does not invalidate a saved destination.
public sealed class DiNeToggleMenuChoices : IDisposable
{
    public sealed class Choice { public string path, label; public bool generated; }
    private readonly List<Choice> choices = new List<Choice>();
    private VRCAvatarDescriptor cachedAvatar;
    private VRCExpressionsMenu cachedRoot;
    private bool dirty = true;
    public DiNeToggleMenuChoices()
    {
        EditorApplication.hierarchyChanged += Invalidate;
        EditorApplication.projectChanged += Invalidate;
        Undo.undoRedoPerformed += Invalidate;
    }
    public void Invalidate() => dirty = true;
    public void Dispose()
    {
        EditorApplication.hierarchyChanged -= Invalidate;
        EditorApplication.projectChanged -= Invalidate;
        Undo.undoRedoPerformed -= Invalidate;
    }
    public static string Segment(string name, int occurrence = 0) => Uri.EscapeDataString(name ?? "") + "~" + occurrence;
    public static string LegacyPath(DiNeSmartToggle.MenuPlacement placement, string group, string path)
    {
        switch (placement)
        {
            case DiNeSmartToggle.MenuPlacement.Group: return Segment(string.IsNullOrWhiteSpace(group) ? "Smart Toggles" : group.Trim());
            case DiNeSmartToggle.MenuPlacement.MultiDresser: return Segment("Multi Dresser");
            case DiNeSmartToggle.MenuPlacement.DresserCategory: return Segment("Multi Dresser") + "/" + Segment(group);
            case DiNeSmartToggle.MenuPlacement.ExistingMenu: return path ?? "";
            default: return "";
        }
    }
    public IReadOnlyList<Choice> GetChoices(VRCAvatarDescriptor avatar)
    {
        if (!dirty && cachedAvatar == avatar && cachedRoot == (avatar != null ? avatar.expressionsMenu : null)) return choices;
        dirty = false; cachedAvatar = avatar; cachedRoot = avatar != null ? avatar.expressionsMenu : null;
        choices.Clear();
        // The containing avatar is fixed. Display only destinations within its menu.
        choices.Add(new Choice { path = "", label = "" });
        Walk(cachedRoot, "", "", new HashSet<VRCExpressionsMenu>());
        if (avatar != null)
            foreach (var dresser in avatar.GetComponentsInChildren<DiNeMultiDresser>(true))
            {
                if (dresser.GetAvatarDescriptor() != avatar) continue;
                AddGenerated(Segment("Multi Dresser"), "Multi Dresser");
                foreach (var layer in dresser.layers)
                    if (layer != null && !string.IsNullOrWhiteSpace(layer.layerName))
                        AddGenerated(Segment("Multi Dresser") + "/" + Segment(layer.layerName), "Multi Dresser › " + DisplayName(layer.layerName));
            }
        return choices;
    }
    private void AddGenerated(string path, string label)
    {
        var existing = choices.Find(choice => choice.path == path);
        if (existing != null) { existing.generated = true; return; }
        choices.Add(new Choice { path = path, label = label, generated = true });
    }
    private void Walk(VRCExpressionsMenu menu, string path, string label, HashSet<VRCExpressionsMenu> stack)
    {
        if (menu == null || menu.controls == null || !stack.Add(menu)) return;
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var control in menu.controls)
        {
            if (control == null || control.type != VRCExpressionsMenu.Control.ControlType.SubMenu || control.subMenu == null) continue;
            string name = control.name ?? "";
            occurrences.TryGetValue(name, out int occurrence); occurrences[name] = occurrence + 1;
            string nextPath = (path.Length == 0 ? "" : path + "/") + Segment(name, occurrence);
            string nextLabel = (label.Length == 0 ? "" : label + " › ") + DisplayName(name) + (occurrence > 0 ? " (" + (occurrence + 1) + ")" : "");
            choices.Add(new Choice { path = nextPath, label = nextLabel });
            Walk(control.subMenu, nextPath, nextLabel, stack);
        }
        stack.Remove(menu);
    }
    public bool Draw(VRCAvatarDescriptor avatar, SerializedProperty path, SerializedProperty generated,
        DiNeSmartToggle.MenuPlacement legacyPlacement = DiNeSmartToggle.MenuPlacement.ExistingMenu, string legacyGroup = null)
    {
        var available = GetChoices(avatar);
        string currentPath = LegacyPath(legacyPlacement, legacyGroup, path.stringValue);
        int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
        string rootLabel = language == 1 ? "최상단 메뉴" : language == 2 ? "最上位メニュー" : "Top-level Menu";
        var labels = available.Select(choice => choice.path.Length == 0 ? rootLabel : choice.label).ToList();
        int current = choices.FindIndex(choice => choice.path == currentPath);
        if (current < 0)
        {
            current = labels.Count;
            labels.Add((language == 1 ? "메뉴를 다시 선택하세요: " : language == 2 ? "メニューを再選択: " : "Select menu again: ") + string.Join(" › ", currentPath.Split('/').Select(segment => DisplayName(Uri.UnescapeDataString(segment.Contains("~") ? segment.Substring(0, segment.LastIndexOf('~')) : segment)))));
        }
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup(language == 1 ? "메뉴 위치" : language == 2 ? "メニュー配置" : "Menu Placement", current, labels.ToArray());
        if (!EditorGUI.EndChangeCheck() || next >= available.Count) return false;
        path.stringValue = available[next].path;
        generated.boolValue = available[next].generated;
        return true;
    }
    // IMGUI treats '/' in popup labels as a submenu, including slashes in actual names.
    // Keep display text flat without altering serialized destination paths.
    private static string DisplayName(string name) => (name ?? "").Replace('/', '／');

    public static VRCExpressionsMenu Resolve(VRCExpressionsMenu root, string path, string folder, bool allowCreate)
    {
        VRCExpressionsMenu current = root;
        if (string.IsNullOrEmpty(path)) return current;
        foreach (string segment in path.Split('/'))
        {
            int split = segment.LastIndexOf('~');
            if (split < 0 || !int.TryParse(segment.Substring(split + 1), out int occurrence)) return null;
            string name = Uri.UnescapeDataString(segment.Substring(0, split));
            if (current.controls == null) current.controls = new List<VRCExpressionsMenu.Control>();
            if (occurrence < 0) return null;
            var matches = current.controls.Where(control => control != null && control.name == name &&
                control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null).ToList();
            VRCExpressionsMenu.Control selected;
            if (occurrence < matches.Count) selected = matches[occurrence];
            else
            {
                if (!allowCreate || occurrence != 0 || current.controls.Count >= 8) return null;
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); menu.name = name;
                AssetDatabase.CreateAsset(menu, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(name) + ".asset"));
                selected = new VRCExpressionsMenu.Control { name = name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu };
                current.controls.Add(selected); EditorUtility.SetDirty(current);
            }
            string assetPath = AssetDatabase.GetAssetPath(selected.subMenu);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(folder + "/", StringComparison.Ordinal))
            {
                var copy = UnityEngine.Object.Instantiate(selected.subMenu); copy.name = selected.subMenu.name;
                AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(copy.name) + ".asset"));
                selected.subMenu = copy; EditorUtility.SetDirty(current);
            }
            current = selected.subMenu;
        }
        return current;
    }
    private static string SafeName(string value)
    {
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return string.IsNullOrEmpty(value) ? "Menu" : value;
    }
}
#endif
