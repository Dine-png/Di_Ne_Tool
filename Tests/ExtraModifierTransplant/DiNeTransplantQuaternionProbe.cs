using UnityEngine;

[ExecuteAlways]
public sealed class DiNeTransplantQuaternionProbe : MonoBehaviour
{
    public int marker;
    public static bool Armed;
    public static GameObject SourceRoot;
    public static int Observations;
    private bool changed;

    private void OnEnable() { ChangeSign(); }
    private void OnValidate() { ChangeSign(); }

    private void ChangeSign()
    {
        if (!Armed || changed || SourceRoot == null || transform.IsChildOf(SourceRoot.transform)) return;
        changed = true;
        Observations++;
        var rotation = transform.localRotation;
        transform.localRotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
    }
}
