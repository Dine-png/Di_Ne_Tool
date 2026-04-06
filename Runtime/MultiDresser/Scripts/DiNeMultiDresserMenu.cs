#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class DiNeMultiDresserMenu
{
    [MenuItem("GameObject/Di Ne/Multi Dresser", false, 0)]
    public static void AddMultiDresser(MenuCommand menuCommand)
    {
        GameObject dresser = new GameObject("Multi Dresser");

        if (menuCommand.context is GameObject parent)
        {
            GameObjectUtility.SetParentAndAlign(dresser, parent);
        }

        dresser.AddComponent<DiNeMultiDresser>();
        Undo.RegisterCreatedObjectUndo(dresser, "Create Multi Dresser");
        
        EditorApplication.delayCall += () => { Selection.activeObject = dresser; };
    }
}
#endif