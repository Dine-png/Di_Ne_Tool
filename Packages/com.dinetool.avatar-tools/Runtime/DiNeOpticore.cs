using UnityEngine;

[AddComponentMenu("Di Ne/Opticore")]
[DisallowMultipleComponent]
public sealed class DiNeOpticore : MonoBehaviour
{
    [Header("Optimization Modules")]
    [SerializeField] private bool _optimizeMeshes = true;
    [SerializeField] private bool _optimizeMaterials = true;
    [SerializeField] private bool _optimizeRigAndBones = true;
    [SerializeField] private bool _optimizePhysBones = true;
    [SerializeField] private bool _optimizeAnimator = true;
    [SerializeField] private bool _removeUnusedObjects = true;

    [Header("Behavior")]
    [SerializeField] private bool _preserveAvatarBehavior = true;
    [SerializeField] private bool _experimentalMode;

    public bool OptimizeMeshes => _optimizeMeshes;
    public bool OptimizeMaterials => _optimizeMaterials;
    public bool OptimizeRigAndBones => _optimizeRigAndBones;
    public bool OptimizePhysBones => _optimizePhysBones;
    public bool OptimizeAnimator => _optimizeAnimator;
    public bool RemoveUnusedObjects => _removeUnusedObjects;
    public bool PreserveAvatarBehavior => _preserveAvatarBehavior;
    public bool ExperimentalMode => _experimentalMode;
}
