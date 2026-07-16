using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

[assembly: ExportsPlugin(typeof(DiNeTool.ExtraModifier.Editor.DiNeFocusNdmfPlugin))]

namespace DiNeTool.ExtraModifier.Editor
{
    public sealed class DiNeFocusNdmfPlugin : Plugin<DiNeFocusNdmfPlugin>
    {
        public override string QualifiedName => "com.dine.tool.extra-modifier.focus";
        public override string DisplayName => "DiNe Extra Modifier - Focus";
        public override Color? ThemeColor => new Color(0.35f, 0.78f, 0.72f);

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .Run("Apply Focus material correction", context =>
                {
                    DiNeFocusProcessor.Process(context.AvatarRootObject, context.AssetContainer)
                        .Log($"NDMF '{context.AvatarRootObject.name}'");
                });
        }
    }

    public sealed class DiNeFocusVrcBuildProcessor : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -900;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            DiNeFocusProcessor.Process(avatarGameObject).Log($"upload '{avatarGameObject.name}'");
            return true;
        }
    }

    [InitializeOnLoad]
    internal static class DiNeFocusPlayModeProcessor
    {
        private const int DelayUpdates = 10;
        private static int remainingUpdates;

        static DiNeFocusPlayModeProcessor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            remainingUpdates = DelayUpdates;
            EditorApplication.update -= WaitThenProcess;
            EditorApplication.update += WaitThenProcess;
        }

        private static void WaitThenProcess()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= WaitThenProcess;
                return;
            }

            if (--remainingUpdates > 0)
                return;

            EditorApplication.update -= WaitThenProcess;
            foreach (var avatar in UnityEngine.Object.FindObjectsOfType<VRCAvatarDescriptor>(true))
                DiNeFocusProcessor.Process(avatar.gameObject).Log($"play mode '{avatar.name}'");
        }
    }
}
