using UnityEngine;

// Compile-only names used by the existing Focus panel. None of these adapters
// replaces the transplant implementation or claims SDK behavior/integration.
namespace VRC.SDKBase
{
    public interface IEditorOnly { }
}

namespace VRC.SDK3.Avatars.Components
{
    public sealed class VRCAvatarDescriptor : MonoBehaviour { }
}

#if UNITY_EDITOR
namespace DiNeTool.ExtraModifier.Editor
{
    public static class DiNeFocusProcessor
    {
        public static DiNeFocus FindSettings(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            return descriptor != null ? descriptor.GetComponent<DiNeFocus>() : null;
        }
    }
}
#endif
