#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

/// <summary>하이어라키 우클릭 → Di Ne → Lighting Designer.</summary>
public static class DiNeLightingDesignerMenu
{
    [MenuItem("GameObject/Di Ne/Lighting Designer", false, 10)]
    public static void AddLightingDesigner(MenuCommand menuCommand)
    {
        var designerObject = new GameObject("Lighting Designer");
        var designer = designerObject.AddComponent<DiNeLightingDesigner>();

        if (menuCommand.context is GameObject parent)
        {
            GameObjectUtility.SetParentAndAlign(designerObject, parent);
        }

        designer.EnsureDefaults();

        Undo.RegisterCreatedObjectUndo(designerObject, "Create Lighting Designer");
        EditorApplication.delayCall += () => { Selection.activeObject = designerObject; };

        if (designerObject.GetComponentInParent<VRCAvatarDescriptor>() == null)
        {
            Debug.LogWarning(
                "[DiNe 라이팅 디자이너] 아바타(VRCAvatarDescriptor) 밖에 만들어졌습니다. 아바타 하위로 옮겨야 설치됩니다.");
        }
    }
}
#endif
