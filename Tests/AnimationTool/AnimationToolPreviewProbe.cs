using UnityEngine;

[ExecuteAlways]
public sealed class AnimationToolPreviewProbe : MonoBehaviour
{
    public static int LifecycleCalls;
    public static int AnimationEventCalls;
    private void OnEnable() { LifecycleCalls++; }
    private void OnDisable() { LifecycleCalls++; }
    public void RegressionSentinel() { AnimationEventCalls++; }
}
