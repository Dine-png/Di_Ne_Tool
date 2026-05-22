internal sealed class DiNeOpticoreTraceAndOptimizeState
{
    public bool PreserveAvatarBehavior { get; private set; }
    public bool ExperimentalMode { get; private set; }
    public bool OptimizeAnimator { get; private set; }
    public bool MergeSkinnedMesh { get; private set; }
    public bool FreezeBlendShapes { get; private set; }
    public bool MergeMaterials { get; private set; }
    public bool OptimizePhysBone { get; private set; }
    public bool OptimizePhysBoneIsAnimated { get; private set; }
    public bool SweepComponents { get; private set; }
    public bool ConfigureLeafMergeBone { get; private set; }
    public bool MirrorIgnoreOtherPhysBonesToIgnoreTransform { get; private set; }

    public static DiNeOpticoreTraceAndOptimizeState Create(DiNeOpticore config)
    {
        var state = new DiNeOpticoreTraceAndOptimizeState();
        state.Initialize(config);
        return state;
    }

    private void Initialize(DiNeOpticore config)
    {
        if (config == null)
            return;

        PreserveAvatarBehavior = config.PreserveAvatarBehavior;
        ExperimentalMode = config.ExperimentalMode;

        if (config.OptimizeMeshes)
        {
            MergeSkinnedMesh = true;
            FreezeBlendShapes = true;
        }

        if (config.OptimizeMaterials)
            MergeMaterials = true;

        if (config.OptimizeRigAndBones)
            ConfigureLeafMergeBone = true;

        if (config.OptimizePhysBones)
        {
            OptimizePhysBone = true;
            OptimizePhysBoneIsAnimated = true;
            MirrorIgnoreOtherPhysBonesToIgnoreTransform = true;
        }

        if (config.RemoveUnusedObjects)
            SweepComponents = true;

        if (config.OptimizeAnimator)
            OptimizeAnimator = true;
    }
}
