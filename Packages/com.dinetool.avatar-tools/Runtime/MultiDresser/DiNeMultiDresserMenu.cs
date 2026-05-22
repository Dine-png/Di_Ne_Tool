#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DiNeMultiDresserMenu
{
    [MenuItem("GameObject/Di Ne/Multi Dresser", false, 0)]
    public static void AddMultiDresser(MenuCommand menuCommand)
    {
        var dresser = new GameObject("Multi Dresser");
        dresser.AddComponent<DiNeMultiDresser>();

        if (menuCommand.context is GameObject parent)
        {
            GameObjectUtility.SetParentAndAlign(dresser, parent);
        }

        Undo.RegisterCreatedObjectUndo(dresser, "Create Multi Dresser");
        EditorApplication.delayCall += () => { Selection.activeObject = dresser; };
    }
}
#endif
