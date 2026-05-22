#if UNITY_EDITOR
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;

[assembly: ExportsPlugin(typeof(DiNeTool.Opticore.Ndmf.DiNeOpticoreNDMFPlugin))]

namespace DiNeTool.Opticore.Ndmf
{
    internal sealed class DiNeOpticoreBuildState
    {
        public DiNeOpticore[] TopLevelOpticores { get; set; } = System.Array.Empty<DiNeOpticore>();
    }

    internal sealed class DiNeOpticoreLoadPass : Pass<DiNeOpticoreLoadPass>
    {
        public static readonly DiNeOpticoreLoadPass Instance = new DiNeOpticoreLoadPass();

        public override string DisplayName => "Opticore: Load Configuration";

        protected override void Execute(BuildContext context)
        {
            var opticores = context.AvatarRootObject
                .GetComponentsInChildren<DiNeOpticore>(true)
                .Where(opticore => opticore != null && !HasParentOpticore(opticore))
                .ToArray();

            context.GetState<DiNeOpticoreBuildState>().TopLevelOpticores = opticores;
        }

        private static bool HasParentOpticore(DiNeOpticore opticore)
        {
            Transform current = opticore.transform.parent;
            while (current != null)
            {
                if (current.GetComponent<DiNeOpticore>() != null)
                    return true;

                current = current.parent;
            }

            return false;
        }
    }

    internal sealed class DiNeOpticoreApplyPass : Pass<DiNeOpticoreApplyPass>
    {
        public static readonly DiNeOpticoreApplyPass Instance = new DiNeOpticoreApplyPass();

        public override string DisplayName => "Opticore: Apply Optimizations";

        protected override void Execute(BuildContext context)
        {
            var state = context.GetState<DiNeOpticoreBuildState>();
            foreach (var opticore in state.TopLevelOpticores)
            {
                if (opticore == null)
                    continue;

                DiNeOpticorePreviewUtility.ApplyOptimizationsInPlace(opticore.gameObject, opticore, false);
            }

            foreach (var opticore in context.AvatarRootObject.GetComponentsInChildren<DiNeOpticore>(true))
            {
                if (opticore != null)
                    Object.DestroyImmediate(opticore);
            }

            foreach (var marker in context.AvatarRootObject.GetComponentsInChildren<DiNeOpticorePlaySessionMarker>(true))
            {
                if (marker != null)
                    Object.DestroyImmediate(marker);
            }
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class DiNeOpticoreNDMFPlugin : Plugin<DiNeOpticoreNDMFPlugin>
    {
        public override string DisplayName => "Di Ne Opticore";

        public override string QualifiedName => "com.dine.tool.opticore";

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .Run(DiNeOpticoreLoadPass.Instance)
                .Then.Run(DiNeOpticoreApplyPass.Instance);
        }
    }
}
#endif
