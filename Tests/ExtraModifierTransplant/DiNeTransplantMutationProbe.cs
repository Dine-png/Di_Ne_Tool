using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class DiNeTransplantMutationProbe : MonoBehaviour
{
    public int marker;
    public static bool Armed;
    public static GameObject TargetRoot;
    public static GameObject[] Objects;
    public static int Observations;
    public static bool Mutated;
    public static bool ActiveOnly;
    private static bool applying;

    private void OnEnable() { MutateExistingTarget(); }
    private void OnValidate() { MutateExistingTarget(); }

    private void MutateExistingTarget()
    {
        if (!Armed || applying || Mutated || TargetRoot == null || Objects == null || !transform.IsChildOf(TargetRoot.transform)) return;
        applying = true;
        Mutated = true; // One real callback side effect; later restoration can settle.
        try
        {
            Observations++;
            foreach (var host in Objects)
            {
                if (host == null) continue;
                if (ActiveOnly)
                {
                    host.SetActive(!host.activeSelf);
#if UNITY_EDITOR
                    if (PrefabUtility.IsPartOfPrefabInstance(host)) PrefabUtility.RecordPrefabInstancePropertyModifications(host);
#endif
                    continue;
                }
                host.transform.localPosition = new Vector3(101, 202, 303);
                host.transform.localRotation = Quaternion.Euler(73, 51, 29);
                host.transform.localScale = new Vector3(8, 9, 10);
                host.name = "CallbackChanged_" + Observations;
                host.layer = 2; host.tag = "EditorOnly";
#if UNITY_EDITOR
                GameObjectUtility.SetStaticEditorFlags(host, StaticEditorFlags.OccluderStatic);
#endif
                var rect = host.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = Vector2.zero;
                    rect.sizeDelta = new Vector2(777, 888); rect.anchoredPosition3D = new Vector3(999, 111, 222);
                }
                host.SetActive(!host.activeSelf);
            }
        }
        finally { applying = false; }
    }
}
