#if UNITY_EDITOR
namespace DiNeTool.ExtraModifier.Editor
{
    // The unrelated, sealed Focus panel is compiled without its NDMF processor.
    // SDK types are supplied by actual installed DLLs in this mode.
    public static class DiNeFocusProcessor
    {
        public static DiNeFocus FindSettings(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor)
        {
            return descriptor != null ? descriptor.GetComponent<DiNeFocus>() : null;
        }
    }
}
#endif
