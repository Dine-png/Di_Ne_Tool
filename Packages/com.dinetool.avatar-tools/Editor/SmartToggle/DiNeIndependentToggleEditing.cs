#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DiNeIndependentToggleEditing
{
    public static DiNeMultiDresser.IndependentToggle Create(DiNeMultiDresser dresser)
    {
        if (dresser == null) return null;
        string name = DiNeSmartToggleEditor.MakeUniqueParameterName(null, null, null, dresser);
        Undo.RecordObject(dresser, "Add Independent Toggle");
        var group = new DiNeMultiDresser.IndependentToggle { parameterName = name };
        dresser.independentToggles.Add(group);
        Changed(dresser);
        return group;
    }

    public static bool CanAdd(DiNeMultiDresser dresser, DiNeMultiDresser.IndependentToggle owner, GameObject go)
    {
        var avatar = dresser != null ? dresser.GetAvatarDescriptor() : null;
        if (avatar == null || go == null || EditorUtility.IsPersistent(go) || go.transform == avatar.transform ||
            !go.transform.IsChildOf(avatar.transform) || go.GetComponent<DiNeSmartToggle>() != null) return false;
        foreach (var other in avatar.GetComponentsInChildren<DiNeMultiDresser>(true))
        {
            if (!other.enabled) continue;
            foreach (var group in other.independentToggles)
                if (group != null && group != owner && group.targets.Contains(go)) return false;
            foreach (var layer in other.layers)
            {
                if (layer == null) continue;
                if (layer.targets.Contains(go)) return false;
                foreach (var linked in layer.linkedObjects)
                    if (linked != null && linked.objects.Contains(go)) return false;
            }
        }
        return true;
    }

    public static int AddTargets(DiNeMultiDresser dresser, DiNeMultiDresser.IndependentToggle owner, IEnumerable<GameObject> objects)
    {
        if (dresser == null || owner == null || objects == null) return 0;
        var valid = objects.Where(go => CanAdd(dresser, owner, go) && !owner.targets.Contains(go)).Distinct().ToList();
        if (valid.Count == 0) return 0;
        Undo.RecordObject(dresser, "Add Toggle Targets");
        owner.targets.AddRange(valid);
        Changed(dresser);
        return valid.Count;
    }

    private static void Changed(DiNeMultiDresser dresser)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(dresser);
        EditorUtility.SetDirty(dresser);
    }
}
#endif
