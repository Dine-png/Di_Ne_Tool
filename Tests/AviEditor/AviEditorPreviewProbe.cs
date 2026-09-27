using UnityEngine;

[ExecuteAlways]
public sealed class AviEditorPreviewProbe : MonoBehaviour
{
    public static int LifecycleCalls;
    private void OnEnable() { LifecycleCalls++; }
    private void OnDisable() { LifecycleCalls++; }
}
