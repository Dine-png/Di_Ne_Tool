using UnityEngine;
using VRC.SDKBase;

namespace DiNeTool.ExtraModifier
{
    public enum DiNeFocusZWrite
    {
        Off = 0,
        On = 1,
        Keep = 2
    }

    /// <summary>
    /// Enables the Focus material correction for the containing avatar.
    /// This component is editor-only and is removed from the uploaded avatar.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DiNeFocus : MonoBehaviour, IEditorOnly
    {
        // Sealed for now. Keep the implementation available for a future reactivation.
        public static bool FeatureEnabled => false;
        public const int QueueThreshold = 2400;
        public const int SafeTargetQueue = 2400;
        public const int ForceTargetQueue = 2450;

        [SerializeField, HideInInspector] private bool forceAll;

        public bool ForceAll => forceAll;
        public int TargetQueue => forceAll ? ForceTargetQueue : SafeTargetQueue;
        public DiNeFocusZWrite TargetZWrite => forceAll ? DiNeFocusZWrite.On : DiNeFocusZWrite.Keep;

        public void SetForceAll(bool value)
        {
            forceAll = value;
        }
    }
}
