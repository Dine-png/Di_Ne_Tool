#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class DiNeMultiCleaner
{
    static DiNeMultiCleaner()
    {
        EditorApplication.hierarchyChanged += QueueCheck;
        QueueCheck();
    }

    private static bool checkQueued;

    private static void QueueCheck()
    {
        if (checkQueued) return;
        checkQueued = true;
        EditorApplication.delayCall += CheckEditorOnlyTag;
    }

    private static void CheckEditorOnlyTag()
    {
        checkQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var allDresserObjects = GameObject.FindObjectsOfType<DiNeMultiDresser>(true);
        foreach (var dresser in allDresserObjects)
        {
            var go = dresser.gameObject;
            // 아바타 루트나 옷/아마추어처럼 다른 컴포넌트·자식이 있는 오브젝트에 붙었으면 태그를 달지 않는다(통째로 업로드에서 빠짐).
            if (go.transform.childCount > 0 || go.GetComponents<Component>().Length > 2) continue;
            if (go.tag != "EditorOnly")
            {
                Undo.RecordObject(go, "Set EditorOnly Tag");
                go.tag = "EditorOnly"; // 업로드 안 되게 태그 설정
                Debug.Log($"🏷 DiNeMultiCleaner: '{go.name}' 오브젝트에 'EditorOnly' 태그를 자동으로 적용했습니다.");
            }
        }
    }
}
#endif
