#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

/// <summary>Hierarchy context menu: selected object → standalone Bool toggle.</summary>
public static class DiNeSmartToggleMenu
{
    // Match the other Di Ne creation commands so Unity includes this in the hierarchy menu.
    [MenuItem("GameObject/Di Ne/Smart Toggle", false, 10)]
    public static void AddSmartToggle(MenuCommand command)
    {
        GameObject target = command.context as GameObject ?? Selection.activeGameObject;
        if (!CanCreate(target)) return;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Smart Toggle");
        try
        {
            if (target.GetComponent<DiNeSmartToggle>() == null)
                DiNeSmartToggleEditor.CreateToggle(target, DiNeSmartToggle.MenuPlacement.Root, null, null);
            Selection.activeGameObject = target;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    [MenuItem("GameObject/Di Ne/Smart Toggle", true)]
    public static bool ValidateAddSmartToggle(MenuCommand command)
        => CanCreate(command.context as GameObject ?? Selection.activeGameObject);

    private static bool CanCreate(GameObject target)
    {
        if (target == null || EditorUtility.IsPersistent(target) || !target.scene.IsValid() ||
            EditorApplication.isPlayingOrWillChangePlaymode) return false;
        var avatar = target.GetComponentInParent<VRCAvatarDescriptor>(true);
        return avatar != null && target != avatar.gameObject;
    }

}
#endif
