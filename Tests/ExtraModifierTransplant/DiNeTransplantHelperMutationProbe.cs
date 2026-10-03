using UnityEngine;

[ExecuteAlways]
public sealed class DiNeTransplantHelperMutationProbe : MonoBehaviour
{
    public int marker;
    public static bool Armed;
    public static GameObject SourceRoot;
    public static int Observations;

    private void Reset() { MoveSelf(); }
    private void OnValidate() { MoveSelf(); }
    private void OnEnable() { MoveSelf(); }

    private void MoveSelf()
    {
        if (!Armed || SourceRoot == null || transform.IsChildOf(SourceRoot.transform)) return;
        Observations++;
        transform.localPosition = new Vector3(91, 82, 73);
        transform.localRotation = Quaternion.Euler(61, 52, 43);
        transform.localScale = new Vector3(7, 8, 9);
    }
}
