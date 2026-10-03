// Minimal MA component adapter for the tool's capture/apply and Undo regressions.
// The real Modular Avatar build pipeline and runtime behavior are not tested here.
namespace nadena.dev.modular_avatar.core
{
    public sealed class ModularAvatarScaleAdjuster : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Vector3 Scale = UnityEngine.Vector3.one;
    }
}
