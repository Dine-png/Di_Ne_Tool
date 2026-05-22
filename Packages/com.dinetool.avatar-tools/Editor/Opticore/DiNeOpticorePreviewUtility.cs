using System;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Text;
using DiNeTool.InGameChecker;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityObject = UnityEngine.Object;

public static class DiNeOpticorePreviewUtility
{
    [Serializable]
    private sealed class MaterialStringContainer : ScriptableSingleton<MaterialStringContainer>
    {
        public string TheString;
    }

    private sealed class ShaderPropertyInfo
    {
        private readonly HashSet<uint> _properties;

        public ShaderPropertyInfo(Shader shader)
        {
            _properties = new HashSet<uint>();
            if (shader == null)
                return;

            SerializedProperty serializedProperty = new SerializedObject(MaterialStringContainer.instance)
                .FindProperty(nameof(MaterialStringContainer.TheString));

            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                serializedProperty.stringValue = shader.GetPropertyName(i);
                _properties.Add(serializedProperty.contentHash);
            }

            foreach (string fallbackProperty in FallbackShaderProperties)
            {
                serializedProperty.stringValue = fallbackProperty;
                _properties.Add(serializedProperty.contentHash);
            }
        }

        public bool HasProperty(SerializedProperty property)
        {
            return property != null && _properties.Contains(property.contentHash);
        }
    }

    private sealed class BlendShapeAnimationInfo
    {
        public bool HasAnimation { get; set; }
        public bool IsConstant { get; set; }
        public float ConstantValue { get; set; }
    }

    public sealed class PreviewReport
    {
        public PreviewReport(
            GameObject source,
            GameObject dummy,
            DiNeAvatarStats.StatsData before,
            DiNeAvatarStats.StatsData after,
            int removedBrokenRenderers,
            int trimmedMaterialSlots,
            int removedUnusedBones,
            int cleanedPhysBoneReferences,
            int removedEmptyObjects,
            IReadOnlyList<string> appliedChanges,
            IReadOnlyList<string> pendingModules)
        {
            Source = source;
            Dummy = dummy;
            Before = before;
            After = after;
            RemovedBrokenRenderers = removedBrokenRenderers;
            TrimmedMaterialSlots = trimmedMaterialSlots;
            RemovedUnusedBones = removedUnusedBones;
            CleanedPhysBoneReferences = cleanedPhysBoneReferences;
            RemovedEmptyObjects = removedEmptyObjects;
            AppliedChanges = appliedChanges;
            PendingModules = pendingModules;
        }

        public GameObject Source { get; }
        public GameObject Dummy { get; }
        public DiNeAvatarStats.StatsData Before { get; }
        public DiNeAvatarStats.StatsData After { get; }
        public int RemovedBrokenRenderers { get; }
        public int TrimmedMaterialSlots { get; }
        public int RemovedUnusedBones { get; }
        public int CleanedPhysBoneReferences { get; }
        public int RemovedEmptyObjects { get; }
        public IReadOnlyList<string> AppliedChanges { get; }
        public IReadOnlyList<string> PendingModules { get; }
    }

    private const string PreviewNamePrefix = "__DiNeOpticorePreview__";
    private const string PreviewRootName = "__DiNeOpticorePreviewRoot__";
    private const HideFlags PreviewHideFlags = HideFlags.DontSaveInEditor;
    private static readonly HashSet<string> FallbackShaderProperties = new HashSet<string>
    {
        "_MainTex",
        "_MetallicGlossMap",
        "_SpecGlossMap",
        "_BumpMap",
        "_ParallaxMap",
        "_OcclusionMap",
        "_EmissionMap",
        "_DetailMask",
        "_DetailAlbedoMap",
        "_DetailNormalMap",
        "_Color",
        "_EmissionColor",
        "_SpecColor",
        "_Cutoff",
        "_Glossiness",
        "_GlossMapScale",
        "_SpecularHighlights",
        "_GlossyReflections",
        "_SmoothnessTextureChannel",
    };

    private static readonly Dictionary<int, GameObject> PreviewObjects = new Dictionary<int, GameObject>();
    private static readonly Dictionary<int, PreviewReport> PreviewReports = new Dictionary<int, PreviewReport>();
    private static readonly Dictionary<Shader, ShaderPropertyInfo> ShaderPropertyCache = new Dictionary<Shader, ShaderPropertyInfo>();

    public static void ApplyOptimizationsInPlace(GameObject targetRoot, DiNeOpticore opticore, bool stripOpticoreComponents = false)
    {
        if (targetRoot == null || opticore == null)
            return;

        if (stripOpticoreComponents)
        {
            foreach (var nestedOpticore in targetRoot.GetComponentsInChildren<DiNeOpticore>(true))
            {
                if (nestedOpticore != null)
                    UnityObject.DestroyImmediate(nestedOpticore);
            }
        }

        ApplyOptimizations(
            targetRoot,
            opticore,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);
    }

    public static PreviewReport GeneratePreview(DiNeOpticore opticore)
    {
        if (opticore == null)
            return null;

        ClearPreview(opticore);

        GameObject source = opticore.gameObject;
        DiNeAvatarStats.StatsData before = DiNeAvatarStats.Calculate(source);

        GameObject previewRoot = GetOrCreatePreviewRoot();
        GameObject dummy = UnityObject.Instantiate(source, previewRoot.transform);
        dummy.name = GetPreviewObjectName(opticore);
        dummy.tag = "EditorOnly";
        dummy.hideFlags = PreviewHideFlags;
        dummy.SetActive(true);
        dummy.transform.SetPositionAndRotation(
            source.transform.position + CalculatePreviewOffset(source),
            source.transform.rotation);
        dummy.transform.localScale = source.transform.localScale;

        foreach (var nestedOpticore in dummy.GetComponentsInChildren<DiNeOpticore>(true))
            UnityObject.DestroyImmediate(nestedOpticore);

        ApplyOptimizations(
            dummy,
            opticore,
            out int removedBrokenRenderers,
            out int trimmedMaterialSlots,
            out int removedUnusedBones,
            out int cleanedPhysBoneReferences,
            out int removedEmptyObjects,
            out List<string> appliedChanges,
            out List<string> pendingModules);

        DiNeAvatarStats.StatsData after = DiNeAvatarStats.Calculate(dummy);
        var report = new PreviewReport(
            source,
            dummy,
            before,
            after,
            removedBrokenRenderers,
            trimmedMaterialSlots,
            removedUnusedBones,
            cleanedPhysBoneReferences,
            removedEmptyObjects,
            appliedChanges,
            pendingModules);

        PreviewObjects[opticore.GetInstanceID()] = dummy;
        PreviewReports[opticore.GetInstanceID()] = report;
        Selection.activeGameObject = dummy;
        EditorGUIUtility.PingObject(dummy);

        return report;
    }

    public static bool TryGetPreviewReport(DiNeOpticore opticore, out PreviewReport report)
    {
        report = null;
        if (opticore == null)
            return false;

        if (!PreviewReports.TryGetValue(opticore.GetInstanceID(), out report))
            return false;

        if (report == null || report.Dummy == null)
        {
            ClearPreviewCache(opticore.GetInstanceID());
            report = null;
            return false;
        }

        return true;
    }

    public static GameObject GetPreviewDummy(DiNeOpticore opticore)
    {
        if (opticore == null)
            return null;

        if (PreviewObjects.TryGetValue(opticore.GetInstanceID(), out GameObject dummy) && dummy != null)
            return dummy;

        string name = GetPreviewObjectName(opticore);
        GameObject found = GameObject.Find(name);
        if (found != null)
            PreviewObjects[opticore.GetInstanceID()] = found;
        return found;
    }

    public static void SelectPreviewDummy(DiNeOpticore opticore)
    {
        GameObject dummy = GetPreviewDummy(opticore);
        if (dummy == null)
            return;

        Selection.activeGameObject = dummy;
        EditorGUIUtility.PingObject(dummy);
    }

    public static void ClearPreview(DiNeOpticore opticore)
    {
        if (opticore == null)
            return;

        int id = opticore.GetInstanceID();
        GameObject dummy = GetPreviewDummy(opticore);
        if (dummy != null)
            UnityObject.DestroyImmediate(dummy);

        ClearPreviewCache(id);
        RemovePreviewRootIfEmpty();
    }

    private static void AppendPendingModule(bool enabled, string message, List<string> pendingModules)
    {
        if (enabled)
            pendingModules.Add(message);
    }

    private static void ApplyOptimizations(
        GameObject targetRoot,
        DiNeOpticore opticore,
        out int removedBrokenRenderers,
        out int trimmedMaterialSlots,
        out int removedUnusedBones,
        out int cleanedPhysBoneReferences,
        out int removedEmptyObjects,
        out List<string> appliedChanges,
        out List<string> pendingModules)
    {
        removedBrokenRenderers = 0;
        trimmedMaterialSlots = 0;
        removedUnusedBones = 0;
        cleanedPhysBoneReferences = 0;
        removedEmptyObjects = 0;
        appliedChanges = new List<string>();
        pendingModules = new List<string>();

        if (targetRoot == null || opticore == null)
            return;

        var state = DiNeOpticoreTraceAndOptimizeState.Create(opticore);
        var protectedTransforms = BuildProtectedTransformSet(targetRoot, state.PreserveAvatarBehavior);
        var animatedTransforms = BuildAnimatedTransformSet(targetRoot);
        var meshMutations = new MeshMutationContext();
        var materialMutations = new MaterialMutationContext();
        var blendShapeAnimationMap = BuildBlendShapeAnimationMap(targetRoot);
        int removedTriangles = 0;
        int removedSubMeshesWithoutMaterial = 0;
        int removedEmptySubMeshes = 0;
        int mergedMaterialSlots = 0;
        int frozenBlendShapes = 0;
        int removedUnusedMaterialProperties = 0;
        int removedUnusedMaterialTextures = 0;
        int collapsedIntermediateBones = 0;
        int mergedSkinnedMeshGroups = 0;
        int mergedSkinnedMeshRenderers = 0;
        int removedMissingScripts = 0;

        if (state.MergeSkinnedMesh)
        {
            if (state.FreezeBlendShapes)
                frozenBlendShapes = FreezeBlendShapes(targetRoot, meshMutations, blendShapeAnimationMap);

            removedTriangles = RemoveZeroSizedPolygons(targetRoot, meshMutations);
            removedBrokenRenderers = RemoveBrokenOrEmptyRenderers(targetRoot);
            appliedChanges.Add(
                removedBrokenRenderers > 0 || removedTriangles > 0 || frozenBlendShapes > 0
                    ? $"Froze {frozenBlendShapes} blendShape(s), removed {removedTriangles} zero-sized triangle(s), and removed {removedBrokenRenderers} broken or zero-triangle renderer component(s)."
                    : "No freezable blendShapes, zero-sized polygons, or broken renderers were found.");
        }
        else
        {
            appliedChanges.Add("Mesh cleanup was disabled, so renderer structure stayed unchanged.");
        }

        if (state.MergeMaterials)
        {
            materialMutations.EnsureClonedMaterials(targetRoot);
            trimmedMaterialSlots = TrimExtraMaterialSlots(targetRoot);
            NormalizeSubMeshesAndMaterials(
                targetRoot,
                meshMutations,
                out removedSubMeshesWithoutMaterial,
                out removedEmptySubMeshes,
                out mergedMaterialSlots);
            removedUnusedMaterialProperties = RemoveUnusedMaterialProperties(targetRoot, materialMutations);
            removedUnusedMaterialTextures = RemoveUnusedMaterialTextures(targetRoot, materialMutations);
            appliedChanges.Add(
                trimmedMaterialSlots > 0 || removedSubMeshesWithoutMaterial > 0 || removedEmptySubMeshes > 0 || mergedMaterialSlots > 0 || removedUnusedMaterialProperties > 0 || removedUnusedMaterialTextures > 0
                    ? $"Trimmed {trimmedMaterialSlots} extra material slot(s), removed {removedSubMeshesWithoutMaterial} submesh(es) without material, removed {removedEmptySubMeshes} empty submesh(es), merged {mergedMaterialSlots} duplicate material slot(s), removed {removedUnusedMaterialProperties} unused material property entry / entries, and cleared {removedUnusedMaterialTextures} unused texture slot(s)."
                    : "No material slot, empty submesh, or duplicate submesh cleanup was needed.");
        }
        else
        {
            appliedChanges.Add("Material slot cleanup was disabled, so slot layout stayed unchanged.");
        }

        if (state.MergeSkinnedMesh)
        {
            AutoMergeCompatibleSkinnedMeshes(
                targetRoot,
                meshMutations,
                protectedTransforms,
                animatedTransforms,
                out mergedSkinnedMeshGroups,
                out mergedSkinnedMeshRenderers);

            appliedChanges.Add(
                mergedSkinnedMeshGroups > 0
                    ? $"Merged {mergedSkinnedMeshRenderers} compatible skinned mesh renderer component(s) into {mergedSkinnedMeshGroups} consolidated renderer group(s)."
                    : "No conservatively mergeable skinned mesh renderer groups were found.");
        }

        if (state.ConfigureLeafMergeBone)
        {
            removedUnusedBones = RemoveUnusedLeafBones(targetRoot, protectedTransforms);
            collapsedIntermediateBones = CollapseIdentityIntermediateBones(targetRoot, protectedTransforms, animatedTransforms);
            appliedChanges.Add(
                removedUnusedBones > 0 || collapsedIntermediateBones > 0
                    ? $"Removed {removedUnusedBones} unreferenced leaf bone object(s) and collapsed {collapsedIntermediateBones} identity intermediate bone object(s)."
                    : "No removable unreferenced or collapsible intermediate bones were found.");
        }
        else
        {
            appliedChanges.Add("Rig / bone cleanup was disabled, so bone hierarchy stayed unchanged.");
        }

        if (state.OptimizePhysBone)
        {
            int mirroredIgnoreTransforms = state.MirrorIgnoreOtherPhysBonesToIgnoreTransform
                ? MirrorIgnoreOtherPhysBonesToIgnoreTransform(targetRoot)
                : 0;
            int mergedPhysBoneColliders = MergeDuplicatePhysBoneColliders(targetRoot, animatedTransforms);
            int optimizedPhysBoneIsAnimated = state.OptimizePhysBoneIsAnimated
                ? OptimizePhysBoneIsAnimated(targetRoot, animatedTransforms)
                : 0;
            int replacedPhysBoneLeafEndpoints = ReplacePhysBoneLeafBonesWithEndpointPosition(targetRoot, protectedTransforms, animatedTransforms);
            cleanedPhysBoneReferences = CleanupPhysBoneReferences(targetRoot);
            appliedChanges.Add(
                cleanedPhysBoneReferences > 0 || mirroredIgnoreTransforms > 0 || mergedPhysBoneColliders > 0 || optimizedPhysBoneIsAnimated > 0 || replacedPhysBoneLeafEndpoints > 0
                    ? $"Updated {cleanedPhysBoneReferences + mirroredIgnoreTransforms} PhysBone reference entry / entries, merged {mergedPhysBoneColliders} duplicate PhysBone collider(s), disabled isAnimated on {optimizedPhysBoneIsAnimated} PhysBone component(s), and replaced {replacedPhysBoneLeafEndpoints} PhysBone leaf endpoint chain(s)."
                    : "No invalid, duplicate, mirrorable, mergeable, or optimizable PhysBone references were found.");
        }
        else
        {
            appliedChanges.Add("PhysBone cleanup was disabled, so PhysBone references stayed unchanged.");
        }

        if (state.SweepComponents)
        {
            removedMissingScripts = RemoveMissingScripts(targetRoot);
            removedEmptyObjects = RemoveEmptyLeafObjects(targetRoot.transform, protectedTransforms, state.ExperimentalMode);
            appliedChanges.Add(
                removedEmptyObjects > 0 || removedMissingScripts > 0
                    ? $"Removed {removedMissingScripts} missing-script component(s) and {removedEmptyObjects} empty hierarchy object(s)."
                    : "No removable missing-script components or empty hierarchy objects were found.");
        }
        else
        {
            appliedChanges.Add("Unused object cleanup was disabled, so the hierarchy stayed unchanged.");
        }

        if (state.PreserveAvatarBehavior)
        {
            appliedChanges.Add(
                protectedTransforms.Count > 0
                    ? $"Protected {protectedTransforms.Count} referenced transform(s) from conservative cleanup."
                    : "Behavior-preservation mode was enabled, but no extra protected references were discovered.");
        }

        if (state.ExperimentalMode)
            appliedChanges.Add("Experimental mode allowed more aggressive empty-object cleanup on the temporary optimized avatar.");

        AppendPendingModule(state.OptimizeAnimator, "Animator optimization is excluded from the current port scope.", pendingModules);
    }

    private sealed class MeshMutationContext
    {
        private readonly Dictionary<int, Mesh> _mutableMeshesByRenderer = new Dictionary<int, Mesh>();

        public Mesh GetMutableMesh(Renderer renderer)
        {
            if (renderer == null)
                return null;

            int rendererId = renderer.GetInstanceID();
            if (_mutableMeshesByRenderer.TryGetValue(rendererId, out Mesh existing) && existing != null)
                return existing;

            Mesh sourceMesh = GetRendererMesh(renderer);
            if (sourceMesh == null)
                return null;

            Mesh mutableMesh = UnityObject.Instantiate(sourceMesh);
            mutableMesh.name = sourceMesh.name + " [Opticore]";
            AssignRendererMesh(renderer, mutableMesh);
            _mutableMeshesByRenderer[rendererId] = mutableMesh;
            return mutableMesh;
        }

        public void ReplaceMutableMesh(Renderer renderer, Mesh mesh)
        {
            if (renderer == null || mesh == null)
                return;

            SetRendererMesh(renderer, mesh);
            _mutableMeshesByRenderer[renderer.GetInstanceID()] = mesh;
        }

        private static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                return skinnedMeshRenderer.sharedMesh;

            if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                return meshFilter != null ? meshFilter.sharedMesh : null;
            }

            return null;
        }

        private static void AssignRendererMesh(Renderer renderer, Mesh mesh)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                skinnedMeshRenderer.sharedMesh = mesh;
                return;
            }

            if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                    meshFilter.sharedMesh = mesh;
            }
        }
    }

    private sealed class MaterialMutationContext
    {
        private readonly Dictionary<int, Material> _clonedMaterialsBySource = new Dictionary<int, Material>();
        private readonly HashSet<int> _processedRenderers = new HashSet<int>();

        public void EnsureClonedMaterials(GameObject root)
        {
            if (root == null)
                return;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                EnsureClonedMaterials(renderer);
        }

        public void EnsureClonedMaterials(Renderer renderer)
        {
            if (renderer == null || !_processedRenderers.Add(renderer.GetInstanceID()))
                return;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return;

            bool changed = false;
            Material[] clonedArray = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null)
                    continue;

                clonedArray[i] = GetOrClone(source);
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = clonedArray;
        }

        public IEnumerable<Material> GetAllClonedMaterials()
        {
            return _clonedMaterialsBySource.Values;
        }

        private Material GetOrClone(Material source)
        {
            if (source == null)
                return null;

            int sourceId = source.GetInstanceID();
            if (_clonedMaterialsBySource.TryGetValue(sourceId, out Material existing) && existing != null)
                return existing;

            Material clone = new Material(source)
            {
                name = source.name + " [Opticore]"
            };
            _clonedMaterialsBySource[sourceId] = clone;
            return clone;
        }
    }

    private sealed class MutableSubMesh
    {
        public MutableSubMesh(MeshTopology topology, Material material, int[] indices)
        {
            Topology = topology;
            Material = material;
            Indices = indices != null ? new List<int>(indices) : new List<int>();
        }

        public MeshTopology Topology { get; }
        public Material Material { get; }
        public List<int> Indices { get; }
    }

    private static void ClearPreviewCache(int opticoreId)
    {
        PreviewObjects.Remove(opticoreId);
        PreviewReports.Remove(opticoreId);
    }

    private static Vector3 CalculatePreviewOffset(GameObject source)
    {
        var renderers = source.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return source.transform.right * 0.6f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float spacing = Mathf.Max(bounds.size.x, 0.6f) + 0.2f;
        return source.transform.right * spacing;
    }

    private static string GetPreviewObjectName(DiNeOpticore opticore)
    {
        return $"{PreviewNamePrefix}{opticore.GetInstanceID()}__{opticore.gameObject.name}";
    }

    private static GameObject GetOrCreatePreviewRoot()
    {
        GameObject existing = GameObject.Find(PreviewRootName);
        if (existing != null)
        {
            existing.hideFlags = PreviewHideFlags;
            return existing;
        }

        var root = new GameObject(PreviewRootName);
        root.tag = "EditorOnly";
        root.hideFlags = PreviewHideFlags;
        root.transform.position = Vector3.zero;
        return root;
    }

    private static void RemovePreviewRootIfEmpty()
    {
        GameObject previewRoot = GameObject.Find(PreviewRootName);
        if (previewRoot != null && previewRoot.transform.childCount == 0)
            UnityObject.DestroyImmediate(previewRoot);
    }

    private static int RemoveBrokenOrEmptyRenderers(GameObject root)
    {
        int removed = 0;

        var skinnedMeshRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinnedMeshRenderers)
        {
            if (smr == null)
                continue;

            Mesh mesh = smr.sharedMesh;
            if (mesh != null && mesh.vertexCount > 0 && mesh.triangles.Length > 0)
                continue;

            UnityObject.DestroyImmediate(smr);
            removed++;
        }

        var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in meshRenderers)
        {
            if (mr == null)
                continue;

            MeshFilter meshFilter = mr.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh != null && mesh.vertexCount > 0 && mesh.triangles.Length > 0)
                continue;

            UnityObject.DestroyImmediate(mr);
            removed++;

            if (meshFilter != null && mesh == null)
                UnityObject.DestroyImmediate(meshFilter);
        }

        return removed;
    }

    private static int RemoveZeroSizedPolygons(GameObject root, MeshMutationContext meshMutations)
    {
        int removedTriangles = 0;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Mesh mesh = meshMutations.GetMutableMesh(renderer);
            if (mesh == null)
                continue;

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                continue;

            bool changed = false;
            int subMeshCount = mesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                if (mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
                    continue;

                int[] triangles = mesh.GetTriangles(subMeshIndex);
                if (triangles == null || triangles.Length < 3)
                    continue;

                List<int> filtered = new List<int>(triangles.Length);
                for (int i = 0; i <= triangles.Length - 3; i += 3)
                {
                    int a = triangles[i];
                    int b = triangles[i + 1];
                    int c = triangles[i + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                        continue;

                    if (IsZeroSizedTriangle(vertices[a], vertices[b], vertices[c]))
                    {
                        removedTriangles++;
                        changed = true;
                        continue;
                    }

                    filtered.Add(a);
                    filtered.Add(b);
                    filtered.Add(c);
                }

                if (changed)
                    mesh.SetTriangles(filtered, subMeshIndex, false);
            }

            if (changed)
                mesh.RecalculateBounds();
        }

        return removedTriangles;
    }

    private static int FreezeBlendShapes(
        GameObject root,
        MeshMutationContext meshMutations,
        Dictionary<int, Dictionary<string, BlendShapeAnimationInfo>> blendShapeAnimationMap)
    {
        if (root == null)
            return 0;

        int frozenCount = 0;
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null || renderer.GetComponent<Cloth>() != null)
                continue;

            Mesh mesh = meshMutations.GetMutableMesh(renderer);
            if (mesh == null || mesh.blendShapeCount == 0)
                continue;

            Dictionary<string, BlendShapeAnimationInfo> rendererAnimationMap = null;
            blendShapeAnimationMap?.TryGetValue(renderer.GetInstanceID(), out rendererAnimationMap);

            var candidates = new List<(int index, float weight)>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                float currentWeight = renderer.GetBlendShapeWeight(i);

                if (rendererAnimationMap == null || !rendererAnimationMap.TryGetValue(name, out BlendShapeAnimationInfo info) || !info.HasAnimation)
                {
                    candidates.Add((i, currentWeight));
                    continue;
                }

                if (info.IsConstant && Mathf.Approximately(info.ConstantValue, currentWeight))
                    candidates.Add((i, currentWeight));
            }

            if (candidates.Count == 0)
                continue;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasTangents = tangents != null && tangents.Length == vertices.Length;
            var removedBlendShapes = new HashSet<int>();
            var retainedWeights = new List<float>();

            foreach ((int index, float weight) in candidates)
            {
                ApplyBlendShapeToGeometry(mesh, index, weight, vertices, hasNormals ? normals : null, hasTangents ? tangents : null);
                removedBlendShapes.Add(index);
                frozenCount++;
            }

            for (int oldIndex = 0; oldIndex < mesh.blendShapeCount; oldIndex++)
            {
                if (!removedBlendShapes.Contains(oldIndex))
                    retainedWeights.Add(renderer.GetBlendShapeWeight(oldIndex));
            }

            Mesh rebuilt = RebuildMeshWithoutBlendShapes(mesh, vertices, hasNormals ? normals : null, hasTangents ? tangents : null, removedBlendShapes);
            meshMutations.ReplaceMutableMesh(renderer, rebuilt);

            for (int newBlendShapeIndex = 0; newBlendShapeIndex < retainedWeights.Count; newBlendShapeIndex++)
                renderer.SetBlendShapeWeight(newBlendShapeIndex, retainedWeights[newBlendShapeIndex]);
        }

        return frozenCount;
    }

    private static void ApplyBlendShapeToGeometry(
        Mesh mesh,
        int blendShapeIndex,
        float targetWeight,
        Vector3[] vertices,
        Vector3[] normals,
        Vector4[] tangents)
    {
        if (mesh == null || blendShapeIndex < 0 || blendShapeIndex >= mesh.blendShapeCount || Mathf.Approximately(targetWeight, 0f))
            return;

        int frameCount = mesh.GetBlendShapeFrameCount(blendShapeIndex);
        if (frameCount <= 0 || vertices == null || vertices.Length == 0)
            return;

        int vertexCount = mesh.vertexCount;
        var frameWeights = new float[frameCount];
        var deltaVertices = new Vector3[frameCount][];
        var deltaNormals = new Vector3[frameCount][];
        var deltaTangents = new Vector3[frameCount][];

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            frameWeights[frameIndex] = mesh.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex);
            deltaVertices[frameIndex] = new Vector3[vertexCount];
            deltaNormals[frameIndex] = new Vector3[vertexCount];
            deltaTangents[frameIndex] = new Vector3[vertexCount];
            mesh.GetBlendShapeFrameVertices(
                blendShapeIndex,
                frameIndex,
                deltaVertices[frameIndex],
                deltaNormals[frameIndex],
                deltaTangents[frameIndex]);
        }

        int lowerFrame = 0;
        int upperFrame = 0;
        float interpolation = 0f;

        if (frameCount == 1)
        {
            lowerFrame = upperFrame = 0;
            interpolation = Mathf.Approximately(frameWeights[0], 0f) ? 0f : targetWeight / frameWeights[0];
        }
        else if (targetWeight <= frameWeights[0])
        {
            lowerFrame = upperFrame = 0;
            interpolation = Mathf.Approximately(frameWeights[0], 0f) ? 0f : targetWeight / frameWeights[0];
        }
        else if (targetWeight >= frameWeights[frameCount - 1])
        {
            lowerFrame = upperFrame = frameCount - 1;
            interpolation = Mathf.Approximately(frameWeights[frameCount - 1], 0f) ? 0f : targetWeight / frameWeights[frameCount - 1];
        }
        else
        {
            for (int frameIndex = 1; frameIndex < frameCount; frameIndex++)
            {
                if (targetWeight > frameWeights[frameIndex])
                    continue;

                lowerFrame = frameIndex - 1;
                upperFrame = frameIndex;
                float denominator = frameWeights[upperFrame] - frameWeights[lowerFrame];
                interpolation = Mathf.Approximately(denominator, 0f)
                    ? 0f
                    : (targetWeight - frameWeights[lowerFrame]) / denominator;
                break;
            }
        }

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            Vector3 vertexDelta;
            Vector3 normalDelta = Vector3.zero;
            Vector3 tangentDelta = Vector3.zero;

            if (lowerFrame == upperFrame)
            {
                vertexDelta = deltaVertices[lowerFrame][vertexIndex] * interpolation;
                if (normals != null)
                    normalDelta = deltaNormals[lowerFrame][vertexIndex] * interpolation;
                if (tangents != null)
                    tangentDelta = deltaTangents[lowerFrame][vertexIndex] * interpolation;
            }
            else
            {
                vertexDelta = Vector3.Lerp(deltaVertices[lowerFrame][vertexIndex], deltaVertices[upperFrame][vertexIndex], interpolation);
                if (normals != null)
                    normalDelta = Vector3.Lerp(deltaNormals[lowerFrame][vertexIndex], deltaNormals[upperFrame][vertexIndex], interpolation);
                if (tangents != null)
                    tangentDelta = Vector3.Lerp(deltaTangents[lowerFrame][vertexIndex], deltaTangents[upperFrame][vertexIndex], interpolation);
            }

            vertices[vertexIndex] += vertexDelta;
            if (normals != null)
                normals[vertexIndex] += normalDelta;
            if (tangents != null)
                tangents[vertexIndex] += new Vector4(tangentDelta.x, tangentDelta.y, tangentDelta.z, 0f);
        }
    }

    private static Mesh RebuildMeshWithoutBlendShapes(
        Mesh source,
        Vector3[] vertices,
        Vector3[] normals,
        Vector4[] tangents,
        HashSet<int> removedBlendShapes)
    {
        var rebuilt = new Mesh
        {
            name = source.name + " [Opticore Frozen]",
            indexFormat = source.indexFormat,
            vertices = vertices,
            bounds = source.bounds,
            bindposes = source.bindposes,
            boneWeights = source.boneWeights
        };

        if (normals != null && normals.Length == vertices.Length)
            rebuilt.normals = normals;
        if (tangents != null && tangents.Length == vertices.Length)
            rebuilt.tangents = tangents;

        rebuilt.colors = source.colors;
        rebuilt.colors32 = source.colors32;

        for (int channel = 0; channel < 8; channel++)
            CopyUVs(source, rebuilt, channel);

        rebuilt.subMeshCount = source.subMeshCount;
        for (int subMeshIndex = 0; subMeshIndex < source.subMeshCount; subMeshIndex++)
            rebuilt.SetIndices(source.GetIndices(subMeshIndex), source.GetTopology(subMeshIndex), subMeshIndex, false);

        for (int blendShapeIndex = 0; blendShapeIndex < source.blendShapeCount; blendShapeIndex++)
        {
            if (removedBlendShapes.Contains(blendShapeIndex))
                continue;

            string name = source.GetBlendShapeName(blendShapeIndex);
            int frameCount = source.GetBlendShapeFrameCount(blendShapeIndex);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameWeight = source.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex);
                Vector3[] dv = new Vector3[source.vertexCount];
                Vector3[] dn = new Vector3[source.vertexCount];
                Vector3[] dt = new Vector3[source.vertexCount];
                source.GetBlendShapeFrameVertices(blendShapeIndex, frameIndex, dv, dn, dt);
                rebuilt.AddBlendShapeFrame(name, frameWeight, dv, dn, dt);
            }
        }

        rebuilt.RecalculateBounds();
        return rebuilt;
    }

    private static void CopyUVs(Mesh source, Mesh target, int channel)
    {
        var uvs = new List<Vector4>();
        source.GetUVs(channel, uvs);
        if (uvs.Count > 0)
            target.SetUVs(channel, uvs);
    }

    private static int TrimExtraMaterialSlots(GameObject root)
    {
        int trimmed = 0;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            int subMeshCount = GetSubMeshCount(renderer);
            if (subMeshCount < 0)
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length <= subMeshCount)
                continue;

            Material[] trimmedMaterials = new Material[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
                trimmedMaterials[i] = materials[i];

            renderer.sharedMaterials = trimmedMaterials;
            trimmed += materials.Length - subMeshCount;
        }

        return trimmed;
    }

    private static void NormalizeSubMeshesAndMaterials(
        GameObject root,
        MeshMutationContext meshMutations,
        out int removedSubMeshesWithoutMaterial,
        out int removedEmptySubMeshes,
        out int mergedMaterialSlots)
    {
        removedSubMeshesWithoutMaterial = 0;
        removedEmptySubMeshes = 0;
        mergedMaterialSlots = 0;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Mesh mesh = meshMutations.GetMutableMesh(renderer);
            if (mesh == null)
                continue;

            Material[] materials = renderer.sharedMaterials ?? System.Array.Empty<Material>();
            int subMeshCount = mesh.subMeshCount;
            if (subMeshCount <= 0)
                continue;

            List<MutableSubMesh> normalized = new List<MutableSubMesh>(subMeshCount);
            bool changed = false;

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                MeshTopology topology = mesh.GetTopology(subMeshIndex);
                int[] indices = mesh.GetIndices(subMeshIndex);
                Material material = subMeshIndex < materials.Length ? materials[subMeshIndex] : null;

                if (material == null)
                {
                    removedSubMeshesWithoutMaterial++;
                    changed = true;
                    continue;
                }

                if (indices == null || indices.Length == 0)
                {
                    removedEmptySubMeshes++;
                    changed = true;
                    continue;
                }

                MutableSubMesh existing = normalized.Find(candidate => candidate.Topology == topology && candidate.Material == material);
                if (existing != null)
                {
                    existing.Indices.AddRange(indices);
                    mergedMaterialSlots++;
                    changed = true;
                    continue;
                }

                normalized.Add(new MutableSubMesh(topology, material, indices));
            }

            if (!changed)
                continue;

            mesh.subMeshCount = normalized.Count;
            for (int i = 0; i < normalized.Count; i++)
                mesh.SetIndices(normalized[i].Indices, normalized[i].Topology, i, false);

            renderer.sharedMaterials = normalized.ConvertAll(subMesh => subMesh.Material).ToArray();
            mesh.RecalculateBounds();
        }
    }

    private static int RemoveUnusedMaterialProperties(GameObject root, MaterialMutationContext materialMutations)
    {
        if (root == null || materialMutations == null)
            return 0;

        int removedEntries = 0;
        foreach (Material material in materialMutations.GetAllClonedMaterials())
        {
            if (material == null || material.shader == null)
                continue;

            if (!ShaderPropertyCache.TryGetValue(material.shader, out ShaderPropertyInfo shaderInfo))
            {
                shaderInfo = new ShaderPropertyInfo(material.shader);
                ShaderPropertyCache[material.shader] = shaderInfo;
            }

            using (SerializedObject serializedObject = new SerializedObject(material))
            {
                removedEntries += DeleteUnusedMaterialProperties(serializedObject.FindProperty("m_SavedProperties.m_TexEnvs"), shaderInfo);
                removedEntries += DeleteUnusedMaterialProperties(serializedObject.FindProperty("m_SavedProperties.m_Floats"), shaderInfo);
                removedEntries += DeleteUnusedMaterialProperties(serializedObject.FindProperty("m_SavedProperties.m_Colors"), shaderInfo);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        return removedEntries;
    }

    private static int DeleteUnusedMaterialProperties(SerializedProperty properties, ShaderPropertyInfo shaderInfo)
    {
        if (properties == null || shaderInfo == null || properties.arraySize == 0)
            return 0;

        int removedCount = 0;
        for (int i = properties.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty item = properties.GetArrayElementAtIndex(i);
            SerializedProperty propertyName = item.FindPropertyRelative("first");
            if (shaderInfo.HasProperty(propertyName))
                continue;

            properties.DeleteArrayElementAtIndex(i);
            removedCount++;
        }
        return removedCount;
    }

    private static int RemoveUnusedMaterialTextures(GameObject root, MaterialMutationContext materialMutations)
    {
        if (root == null || materialMutations == null)
            return 0;

        int clearedTextures = 0;
        foreach (Material material in materialMutations.GetAllClonedMaterials())
        {
            if (material == null || material.shader == null)
                continue;

            HashSet<string> usedProperties = new HashSet<string>();
            int propertyCount = material.shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (material.shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                    usedProperties.Add(material.shader.GetPropertyName(i));
            }

            foreach (string property in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(property);
                if (texture == null)
                    continue;

                bool supportedTexture = texture is Texture2D || texture is RenderTexture renderTexture && renderTexture.dimension == TextureDimension.Tex2D;
                if (!supportedTexture)
                    continue;

                if (usedProperties.Contains(property))
                    continue;

                material.SetTexture(property, null);
                clearedTextures++;
            }
        }

        return clearedTextures;
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            return skinnedMeshRenderer.sharedMesh != null ? skinnedMeshRenderer.sharedMesh.subMeshCount : -1;

        if (renderer is MeshRenderer meshRenderer)
        {
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            return meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.subMeshCount : -1;
        }

        return -1;
    }

    private static bool IsZeroSizedTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 cross = Vector3.Cross(b - a, c - a);
        return cross.sqrMagnitude <= 1e-10f;
    }

    private static int RemoveUnusedLeafBones(GameObject root, HashSet<Transform> protectedTransforms)
    {
        int removed = 0;
        bool changed;

        do
        {
            changed = false;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                Transform current = transforms[i];
                if (current == null || current == root.transform)
                    continue;

                if (current.childCount > 0)
                    continue;

                if (protectedTransforms.Contains(current))
                    continue;

                Component[] components = current.GetComponents<Component>();
                if (components.Length != 1)
                    continue;

                UnityObject.DestroyImmediate(current.gameObject);
                removed++;
                changed = true;
            }
        }
        while (changed);

        return removed;
    }

    private static int CollapseIdentityIntermediateBones(
        GameObject root,
        HashSet<Transform> protectedTransforms,
        HashSet<Transform> animatedTransforms)
    {
        int collapsed = 0;
        bool changed;

        do
        {
            changed = false;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                Transform current = transforms[i];
                if (current == null || current == root.transform || current.parent == null)
                    continue;

                if (current.childCount != 1)
                    continue;

                if (protectedTransforms.Contains(current) || animatedTransforms.Contains(current))
                    continue;

                if (current.GetComponents<Component>().Length != 1)
                    continue;

                if (current.localPosition != Vector3.zero || current.localRotation != Quaternion.identity || current.localScale != Vector3.one)
                    continue;

                Transform child = current.GetChild(0);
                child.SetParent(current.parent, false);
                UnityObject.DestroyImmediate(current.gameObject);
                collapsed++;
                changed = true;
            }
        }
        while (changed);

        return collapsed;
    }

    private static void AutoMergeCompatibleSkinnedMeshes(
        GameObject root,
        MeshMutationContext meshMutations,
        HashSet<Transform> protectedTransforms,
        HashSet<Transform> animatedTransforms,
        out int mergedGroups,
        out int mergedRenderers)
    {
        mergedGroups = 0;
        mergedRenderers = 0;

        if (root == null || meshMutations == null)
            return;

        var groupedRenderers = new Dictionary<string, List<SkinnedMeshRenderer>>(StringComparer.Ordinal);
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null)
                continue;

            Mesh mesh = meshMutations.GetMutableMesh(renderer);
            if (!TryBuildSkinnedMeshMergeSignature(renderer, mesh, root.transform, animatedTransforms, out string signature))
                continue;

            if (!groupedRenderers.TryGetValue(signature, out List<SkinnedMeshRenderer> group))
            {
                group = new List<SkinnedMeshRenderer>();
                groupedRenderers.Add(signature, group);
            }

            group.Add(renderer);
        }

        foreach (List<SkinnedMeshRenderer> group in groupedRenderers.Values)
        {
            if (group.Count < 2)
                continue;

            if (!TryMergeSkinnedMeshGroup(group, meshMutations, protectedTransforms))
                continue;

            mergedGroups++;
            mergedRenderers += group.Count - 1;
        }
    }

    private static bool TryBuildSkinnedMeshMergeSignature(
        SkinnedMeshRenderer renderer,
        Mesh mesh,
        Transform avatarRoot,
        HashSet<Transform> animatedTransforms,
        out string signature)
    {
        signature = null;

        if (renderer == null || mesh == null || avatarRoot == null)
            return false;

        if (renderer.transform == avatarRoot || renderer.transform.parent == null)
            return false;

        if (renderer.GetComponent<Cloth>() != null)
            return false;

        if (mesh.vertexCount == 0 || mesh.subMeshCount == 0 || mesh.blendShapeCount != 0)
            return false;

        if (renderer.rootBone == null || renderer.bones == null || renderer.bones.Length == 0)
            return false;

        if (mesh.bindposes == null || mesh.bindposes.Length != renderer.bones.Length)
            return false;

        if (IsAnimatedTransformOrParent(renderer.transform, avatarRoot, animatedTransforms))
            return false;

        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length < mesh.subMeshCount)
            return false;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            if (materials[i] == null)
                return false;
        }

        for (int i = 0; i < renderer.bones.Length; i++)
        {
            if (renderer.bones[i] == null)
                return false;
        }

        Vector3 localPosition = RoundVector3(renderer.transform.localPosition);
        Quaternion localRotation = RoundQuaternion(renderer.transform.localRotation);
        Vector3 localScale = RoundVector3(renderer.transform.localScale);

        var builder = new StringBuilder(512);
        builder.Append(renderer.transform.parent.GetInstanceID()).Append('|');
        builder.Append(renderer.gameObject.activeSelf ? '1' : '0').Append('|');
        builder.Append(renderer.rootBone.GetInstanceID()).Append('|');
        builder.Append(localPosition.x.ToString("F5")).Append('|');
        builder.Append(localPosition.y.ToString("F5")).Append('|');
        builder.Append(localPosition.z.ToString("F5")).Append('|');
        builder.Append(localRotation.x.ToString("F5")).Append('|');
        builder.Append(localRotation.y.ToString("F5")).Append('|');
        builder.Append(localRotation.z.ToString("F5")).Append('|');
        builder.Append(localRotation.w.ToString("F5")).Append('|');
        builder.Append(localScale.x.ToString("F5")).Append('|');
        builder.Append(localScale.y.ToString("F5")).Append('|');
        builder.Append(localScale.z.ToString("F5")).Append('|');
        builder.Append((int)renderer.shadowCastingMode).Append('|');
        builder.Append(renderer.receiveShadows ? '1' : '0').Append('|');
        builder.Append((int)renderer.lightProbeUsage).Append('|');
        builder.Append((int)renderer.reflectionProbeUsage).Append('|');
        builder.Append(renderer.skinnedMotionVectors ? '1' : '0').Append('|');
        builder.Append((int)renderer.quality).Append('|');
        builder.Append(renderer.updateWhenOffscreen ? '1' : '0').Append('|');
        builder.Append(renderer.allowOcclusionWhenDynamic ? '1' : '0').Append('|');
        builder.Append(renderer.probeAnchor != null ? renderer.probeAnchor.GetInstanceID() : 0).Append('|');
        builder.Append(renderer.lightProbeProxyVolumeOverride != null ? renderer.lightProbeProxyVolumeOverride.GetInstanceID() : 0).Append('|');

        for (int i = 0; i < renderer.bones.Length; i++)
            builder.Append(renderer.bones[i].GetInstanceID()).Append(',');

        builder.Append('|');
        AppendBindposeSignature(builder, mesh.bindposes);
        signature = builder.ToString();
        return true;
    }

    private static bool TryMergeSkinnedMeshGroup(
        List<SkinnedMeshRenderer> group,
        MeshMutationContext meshMutations,
        HashSet<Transform> protectedTransforms)
    {
        if (group == null || group.Count < 2 || meshMutations == null)
            return false;

        SkinnedMeshRenderer targetRenderer = group[0];
        Mesh targetMesh = meshMutations.GetMutableMesh(targetRenderer);
        if (targetRenderer == null || targetMesh == null)
            return false;

        Transform[] targetBones = targetRenderer.bones;
        Matrix4x4[] targetBindposes = targetMesh.bindposes;
        if (targetBones == null || targetBindposes == null || targetBones.Length == 0)
            return false;

        int totalVertexCount = 0;
        int totalSubMeshCount = 0;
        bool hasNormals = targetMesh.normals != null && targetMesh.normals.Length == targetMesh.vertexCount;
        bool hasTangents = targetMesh.tangents != null && targetMesh.tangents.Length == targetMesh.vertexCount;
        bool hasColors = targetMesh.colors != null && targetMesh.colors.Length == targetMesh.vertexCount;
        bool hasColors32 = targetMesh.colors32 != null && targetMesh.colors32.Length == targetMesh.vertexCount;
        bool[] useUvChannel = new bool[8];

        for (int rendererIndex = 0; rendererIndex < group.Count; rendererIndex++)
        {
            SkinnedMeshRenderer renderer = group[rendererIndex];
            Mesh mesh = meshMutations.GetMutableMesh(renderer);
            if (renderer == null || mesh == null)
                return false;

            if (!AreTransformArraysEqual(targetBones, renderer.bones))
                return false;

            if (!AreBindposesEqual(targetBindposes, mesh.bindposes))
                return false;

            BoneWeight[] boneWeights = mesh.boneWeights;
            if (boneWeights == null || boneWeights.Length != mesh.vertexCount)
                return false;

            if ((mesh.normals != null && mesh.normals.Length == mesh.vertexCount) != hasNormals)
                return false;

            if ((mesh.tangents != null && mesh.tangents.Length == mesh.vertexCount) != hasTangents)
                return false;

            if ((mesh.colors != null && mesh.colors.Length == mesh.vertexCount) != hasColors)
                return false;

            if ((mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount) != hasColors32)
                return false;

            totalVertexCount += mesh.vertexCount;
            totalSubMeshCount += mesh.subMeshCount;

            for (int channel = 0; channel < useUvChannel.Length; channel++)
                useUvChannel[channel] |= MeshHasUvChannel(mesh, channel);
        }

        if (totalVertexCount == 0 || totalSubMeshCount == 0)
            return false;

        var vertices = new Vector3[totalVertexCount];
        Vector3[] normals = hasNormals ? new Vector3[totalVertexCount] : null;
        Vector4[] tangents = hasTangents ? new Vector4[totalVertexCount] : null;
        Color[] colors = hasColors ? new Color[totalVertexCount] : null;
        Color32[] colors32 = hasColors32 ? new Color32[totalVertexCount] : null;
        var boneWeightsCombined = new BoneWeight[totalVertexCount];
        var uvChannels = new List<Vector4>[useUvChannel.Length];
        for (int channel = 0; channel < uvChannels.Length; channel++)
        {
            if (useUvChannel[channel])
                uvChannels[channel] = new List<Vector4>(totalVertexCount);
        }

        var subMeshEntries = new List<MutableSubMesh>(totalSubMeshCount);
        int vertexOffset = 0;

        foreach (SkinnedMeshRenderer renderer in group)
        {
            Mesh mesh = meshMutations.GetMutableMesh(renderer);
            Vector3[] meshVertices = mesh.vertices;
            Material[] materials = renderer.sharedMaterials;
            if (meshVertices == null || meshVertices.Length != mesh.vertexCount)
                return false;

            Array.Copy(meshVertices, 0, vertices, vertexOffset, mesh.vertexCount);
            Array.Copy(mesh.boneWeights, 0, boneWeightsCombined, vertexOffset, mesh.vertexCount);

            if (hasNormals)
                Array.Copy(mesh.normals, 0, normals, vertexOffset, mesh.vertexCount);

            if (hasTangents)
                Array.Copy(mesh.tangents, 0, tangents, vertexOffset, mesh.vertexCount);

            if (hasColors)
                Array.Copy(mesh.colors, 0, colors, vertexOffset, mesh.vertexCount);

            if (hasColors32)
                Array.Copy(mesh.colors32, 0, colors32, vertexOffset, mesh.vertexCount);

            for (int channel = 0; channel < uvChannels.Length; channel++)
            {
                if (uvChannels[channel] != null)
                    AppendUvChannel(mesh, channel, uvChannels[channel]);
            }

            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                int[] indices = mesh.GetIndices(subMeshIndex);
                if (indices == null || indices.Length == 0)
                    continue;

                int[] shifted = new int[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                    shifted[i] = indices[i] + vertexOffset;

                subMeshEntries.Add(new MutableSubMesh(mesh.GetTopology(subMeshIndex), materials[subMeshIndex], shifted));
            }

            vertexOffset += mesh.vertexCount;
        }

        if (subMeshEntries.Count == 0)
            return false;

        var mergedMesh = new Mesh
        {
            name = targetMesh.name + " [Opticore Merged]",
            indexFormat = totalVertexCount > 65535 ? IndexFormat.UInt32 : targetMesh.indexFormat,
            vertices = vertices,
            bindposes = targetBindposes,
            boneWeights = boneWeightsCombined,
        };

        if (hasNormals)
            mergedMesh.normals = normals;

        if (hasTangents)
            mergedMesh.tangents = tangents;

        if (hasColors)
            mergedMesh.colors = colors;

        if (hasColors32)
            mergedMesh.colors32 = colors32;

        for (int channel = 0; channel < uvChannels.Length; channel++)
        {
            if (uvChannels[channel] != null)
                mergedMesh.SetUVs(channel, uvChannels[channel]);
        }

        mergedMesh.subMeshCount = subMeshEntries.Count;
        for (int subMeshIndex = 0; subMeshIndex < subMeshEntries.Count; subMeshIndex++)
        {
            MutableSubMesh entry = subMeshEntries[subMeshIndex];
            mergedMesh.SetIndices(entry.Indices.ToArray(), entry.Topology, subMeshIndex, false);
        }

        mergedMesh.RecalculateBounds();

        Material[] mergedMaterials = new Material[subMeshEntries.Count];
        for (int i = 0; i < subMeshEntries.Count; i++)
            mergedMaterials[i] = subMeshEntries[i].Material;

        targetRenderer.sharedMaterials = mergedMaterials;
        targetRenderer.bones = targetBones;
        targetRenderer.rootBone = group[0].rootBone;
        targetRenderer.localBounds = mergedMesh.bounds;
        meshMutations.ReplaceMutableMesh(targetRenderer, mergedMesh);

        for (int i = 1; i < group.Count; i++)
            RemoveMergedRendererSource(group[i], protectedTransforms);

        return true;
    }

    private static void RemoveMergedRendererSource(SkinnedMeshRenderer renderer, HashSet<Transform> protectedTransforms)
    {
        if (renderer == null)
            return;

        Transform transform = renderer.transform;
        UnityObject.DestroyImmediate(renderer);

        if (transform == null || transform.parent == null)
            return;

        if (protectedTransforms != null && protectedTransforms.Contains(transform))
            return;

        if (transform.childCount != 0)
            return;

        Component[] remainingComponents = transform.GetComponents<Component>();
        if (remainingComponents.Length == 1)
            UnityObject.DestroyImmediate(transform.gameObject);
    }

    private static bool AreTransformArraysEqual(Transform[] left, Transform[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Length != right.Length)
            return false;

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private static bool AreBindposesEqual(Matrix4x4[] left, Matrix4x4[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Length != right.Length)
            return false;

        for (int i = 0; i < left.Length; i++)
        {
            if (!AreMatricesApproximatelyEqual(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool AreMatricesApproximatelyEqual(Matrix4x4 left, Matrix4x4 right)
    {
        const float tolerance = 0.00001f;
        for (int i = 0; i < 16; i++)
        {
            if (Mathf.Abs(left[i] - right[i]) > tolerance)
                return false;
        }

        return true;
    }

    private static void AppendBindposeSignature(StringBuilder builder, Matrix4x4[] bindposes)
    {
        if (builder == null || bindposes == null)
            return;

        for (int i = 0; i < bindposes.Length; i++)
        {
            Matrix4x4 bindpose = bindposes[i];
            for (int element = 0; element < 16; element++)
                builder.Append(RoundFloat(bindpose[element]).ToString("F5")).Append(',');
            builder.Append(';');
        }
    }

    private static bool MeshHasUvChannel(Mesh mesh, int channel)
    {
        if (mesh == null)
            return false;

        var buffer = new List<Vector4>();
        mesh.GetUVs(channel, buffer);
        return buffer.Count > 0;
    }

    private static void AppendUvChannel(Mesh mesh, int channel, List<Vector4> destination)
    {
        if (mesh == null || destination == null)
            return;

        int vertexCount = mesh.vertexCount;
        var buffer = new List<Vector4>();
        mesh.GetUVs(channel, buffer);

        if (buffer.Count == vertexCount)
        {
            destination.AddRange(buffer);
            return;
        }

        for (int i = 0; i < vertexCount; i++)
            destination.Add(Vector4.zero);
    }

    private static HashSet<Transform> BuildProtectedTransformSet(GameObject root, bool preserveAvatarBehavior)
    {
        var protectedTransforms = new HashSet<Transform>();

        foreach (var skinnedMeshRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinnedMeshRenderer == null)
                continue;

            if (skinnedMeshRenderer.rootBone != null)
                AddTransformHierarchy(skinnedMeshRenderer.rootBone, protectedTransforms);

            Transform[] bones = skinnedMeshRenderer.bones;
            if (bones == null)
                continue;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                    AddTransformHierarchy(bones[i], protectedTransforms);
            }
        }

        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                continue;

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null)
                    AddTransformHierarchy(bone, protectedTransforms);
            }
        }

        if (preserveAvatarBehavior)
        {
            AddReferencedTransforms(root, protectedTransforms);
            AddAnimatedTransforms(root, protectedTransforms);
        }

        return protectedTransforms;
    }

    private static HashSet<Transform> BuildAnimatedTransformSet(GameObject root)
    {
        var animatedTransforms = new HashSet<Transform>();
        AddAnimatedTransforms(root, animatedTransforms);
        return animatedTransforms;
    }

    private static Dictionary<int, Dictionary<string, BlendShapeAnimationInfo>> BuildBlendShapeAnimationMap(GameObject root)
    {
        var map = new Dictionary<int, Dictionary<string, BlendShapeAnimationInfo>>();
        if (root == null)
            return map;

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                RecordBlendShapeBindings(animator.transform, clip, map);
        }

        foreach (Animation animation in root.GetComponentsInChildren<Animation>(true))
        {
            if (animation == null)
                continue;

            foreach (AnimationState state in animation)
            {
                AnimationClip clip = state?.clip;
                if (clip != null)
                    RecordBlendShapeBindings(animation.transform, clip, map);
            }
        }

        return map;
    }

    private static void RecordBlendShapeBindings(
        Transform animationRoot,
        AnimationClip clip,
        Dictionary<int, Dictionary<string, BlendShapeAnimationInfo>> map)
    {
        if (animationRoot == null || clip == null || map == null)
            return;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (string.IsNullOrEmpty(binding.propertyName) || !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                continue;

            Transform target = string.IsNullOrEmpty(binding.path) ? animationRoot : animationRoot.Find(binding.path);
            if (target == null)
                continue;

            SkinnedMeshRenderer renderer = target.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null)
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                continue;

            string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
            if (!map.TryGetValue(renderer.GetInstanceID(), out Dictionary<string, BlendShapeAnimationInfo> rendererMap))
            {
                rendererMap = new Dictionary<string, BlendShapeAnimationInfo>(StringComparer.Ordinal);
                map.Add(renderer.GetInstanceID(), rendererMap);
            }

            if (!rendererMap.TryGetValue(blendShapeName, out BlendShapeAnimationInfo info))
            {
                info = new BlendShapeAnimationInfo();
                rendererMap.Add(blendShapeName, info);
            }

            UpdateBlendShapeAnimationInfo(info, curve);
        }
    }

    private static void UpdateBlendShapeAnimationInfo(BlendShapeAnimationInfo info, AnimationCurve curve)
    {
        if (info == null || curve == null)
            return;

        bool curveConstant = true;
        float constantValue = curve.length > 0 ? curve.keys[0].value : 0f;
        for (int i = 1; i < curve.length; i++)
        {
            if (!Mathf.Approximately(curve.keys[i].value, constantValue))
            {
                curveConstant = false;
                break;
            }
        }

        if (!info.HasAnimation)
        {
            info.HasAnimation = true;
            info.IsConstant = curveConstant;
            info.ConstantValue = constantValue;
            return;
        }

        if (!info.IsConstant || !curveConstant || !Mathf.Approximately(info.ConstantValue, constantValue))
        {
            info.IsConstant = false;
            return;
        }

        info.IsConstant = true;
        info.ConstantValue = constantValue;
    }

    private static void AddTransformHierarchy(Transform leaf, HashSet<Transform> protectedTransforms)
    {
        Transform current = leaf;
        while (current != null && protectedTransforms.Add(current))
            current = current.parent;
    }

    private static void AddReferencedTransforms(GameObject root, HashSet<Transform> protectedTransforms)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            foreach (UnityEngine.Object reference in EnumerateUnityReferences(component))
            {
                Transform referencedTransform = GetReferencedTransform(reference);
                if (referencedTransform == null || !referencedTransform.IsChildOf(root.transform))
                    continue;

                AddTransformHierarchy(referencedTransform, protectedTransforms);
            }
        }
    }

    private static void AddAnimatedTransforms(GameObject root, HashSet<Transform> protectedTransforms)
    {
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null)
                continue;

            RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
            if (runtimeController == null)
                continue;

            foreach (AnimationClip clip in runtimeController.animationClips)
            {
                if (clip == null)
                    continue;

                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    AddBoundTransform(animator.transform, binding.path, protectedTransforms);

                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    AddBoundTransform(animator.transform, binding.path, protectedTransforms);
            }
        }

        foreach (var animation in root.GetComponentsInChildren<Animation>(true))
        {
            if (animation == null)
                continue;

            foreach (AnimationState state in animation)
            {
                AnimationClip clip = state?.clip;
                if (clip == null)
                    continue;

                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    AddBoundTransform(animation.transform, binding.path, protectedTransforms);

                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    AddBoundTransform(animation.transform, binding.path, protectedTransforms);
            }
        }
    }

    private static void AddBoundTransform(Transform root, string relativePath, HashSet<Transform> protectedTransforms)
    {
        if (root == null)
            return;

        Transform target = string.IsNullOrEmpty(relativePath) ? root : root.Find(relativePath);
        if (target != null)
            AddTransformHierarchy(target, protectedTransforms);
    }

    private static void SetRendererMesh(Renderer renderer, Mesh mesh)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            skinnedMeshRenderer.sharedMesh = mesh;
            return;
        }

        if (renderer is MeshRenderer meshRenderer)
        {
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter != null)
                meshFilter.sharedMesh = mesh;
        }
    }

    private static IEnumerable<UnityEngine.Object> EnumerateUnityReferences(Component component)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo field in component.GetType().GetFields(flags))
        {
            if (field.IsStatic)
                continue;

            object value;
            try
            {
                value = field.GetValue(component);
            }
            catch
            {
                continue;
            }

            foreach (UnityEngine.Object reference in EnumerateUnityReferences(value))
                yield return reference;
        }
    }

    private static IEnumerable<UnityEngine.Object> EnumerateUnityReferences(object value)
    {
        if (value == null)
            yield break;

        if (value is string)
            yield break;

        if (value is UnityEngine.Object unityObject)
        {
            yield return unityObject;
            yield break;
        }

        if (!(value is IEnumerable enumerable))
            yield break;

        foreach (object entry in enumerable)
        {
            foreach (UnityEngine.Object nestedReference in EnumerateUnityReferences(entry))
                yield return nestedReference;
        }
    }

    private static Transform GetReferencedTransform(UnityEngine.Object reference)
    {
        if (reference is Transform transform)
            return transform;

        if (reference is GameObject gameObject)
            return gameObject.transform;

        if (reference is Component component)
            return component.transform;

        return null;
    }

    private static int CleanupPhysBoneReferences(GameObject root)
    {
        int cleanedEntries = 0;

        Type physBoneType = FindType("VRC.Dynamics.VRCPhysBoneBase");
        if (physBoneType == null)
            physBoneType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneBase");

        if (physBoneType == null)
            return 0;

        Component[] physBones = root.GetComponentsInChildren(physBoneType, true);
        foreach (Component physBone in physBones)
        {
            cleanedEntries += CleanupReferenceList(physBone, "ignoreTransforms");
            cleanedEntries += CleanupReferenceList(physBone, "colliders");
        }

        return cleanedEntries;
    }

    private static int OptimizePhysBoneIsAnimated(GameObject root, HashSet<Transform> animatedTransforms)
    {
        Type physBoneType = FindType("VRC.Dynamics.VRCPhysBoneBase");
        if (physBoneType == null)
            physBoneType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneBase");

        if (physBoneType == null)
            return 0;

        int optimized = 0;
        Component[] physBones = root.GetComponentsInChildren(physBoneType, true);
        foreach (Component physBone in physBones)
        {
            if (!GetBoolMemberValue(physBone, "isAnimated"))
                continue;

            Transform target = GetPhysBoneTarget(physBone);
            if (target == null || !target.IsChildOf(root.transform))
                continue;

            HashSet<Transform> ignoredTransforms = GetIgnoredTransformSet(physBone);
            bool hasAnimatedAffectedTransform = false;
            foreach (Transform affectedTransform in EnumerateAffectedTransforms(target, ignoredTransforms))
            {
                if (affectedTransform != null && animatedTransforms.Contains(affectedTransform))
                {
                    hasAnimatedAffectedTransform = true;
                    break;
                }
            }

            bool parentsAnimated = IsAnimatedTransformOrParent(physBone.transform, root.transform, animatedTransforms);
            if (hasAnimatedAffectedTransform || parentsAnimated)
                continue;

            if (SetMemberValue(physBone, "isAnimated", false))
                optimized++;
        }

        return optimized;
    }

    private static int ReplacePhysBoneLeafBonesWithEndpointPosition(
        GameObject root,
        HashSet<Transform> protectedTransforms,
        HashSet<Transform> animatedTransforms)
    {
        Type physBoneType = FindType("VRC.Dynamics.VRCPhysBoneBase");
        if (physBoneType == null)
            physBoneType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneBase");

        if (physBoneType == null)
            return 0;

        int replaced = 0;
        Component[] physBones = root.GetComponentsInChildren(physBoneType, true);
        foreach (Component physBone in physBones)
        {
            Transform target = GetPhysBoneTarget(physBone);
            if (target == null || !target.IsChildOf(root.transform))
                continue;

            if (GetVector3MemberValue(physBone, "endpointPosition") != Vector3.zero)
                continue;

            HashSet<Transform> ignoredTransforms = GetIgnoredTransformSet(physBone);
            HashSet<Transform> leafBones = EnumerateAffectedLeafTransforms(target, ignoredTransforms);
            if (leafBones.Count == 0)
                continue;

            if (!IsSafePhysBoneEndpointReplacement(physBone, leafBones, protectedTransforms, animatedTransforms))
                continue;

            Vector3 averageLocalPosition = AverageLocalPosition(leafBones);
            if (!AreApproximatelyEqualLocalPosition(leafBones, averageLocalPosition))
                continue;

            if (!(GetMemberValue(physBone, "ignoreTransforms") is System.Collections.IList ignoreList))
                continue;

            foreach (Transform leafBone in leafBones)
            {
                if (ignoredTransforms.Add(leafBone))
                    ignoreList.Add(leafBone);
            }

            if (!SetMemberValue(physBone, "endpointPosition", averageLocalPosition))
                continue;

            foreach (Transform leafBone in leafBones)
            {
                if (leafBone == null || protectedTransforms.Contains(leafBone))
                    continue;

                if (leafBone.childCount == 0 && leafBone.GetComponents<Component>().Length == 1)
                    UnityObject.DestroyImmediate(leafBone.gameObject);
            }

            replaced++;
        }

        return replaced;
    }

    private static int MergeDuplicatePhysBoneColliders(GameObject root, HashSet<Transform> animatedTransforms)
    {
        Type colliderType = FindType("VRC.Dynamics.VRCPhysBoneColliderBase");
        if (colliderType == null)
            colliderType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneColliderBase");

        if (colliderType == null)
            return 0;

        Component[] colliders = root.GetComponentsInChildren(colliderType, true);
        if (colliders == null || colliders.Length <= 1)
            return 0;

        var mergeableGroups = new Dictionary<string, List<Component>>();
        foreach (Component collider in colliders)
        {
            if (collider == null)
                continue;

            Transform transform = collider.transform;
            if (transform == null || !transform.IsChildOf(root.transform))
                continue;

            if (IsAnimatedTransformOrParent(transform, root.transform, animatedTransforms))
                continue;

            string signature = BuildPhysBoneColliderSignature(collider);
            if (string.IsNullOrEmpty(signature))
                continue;

            if (!mergeableGroups.TryGetValue(signature, out List<Component> bucket))
            {
                bucket = new List<Component>();
                mergeableGroups.Add(signature, bucket);
            }

            bucket.Add(collider);
        }

        var mergedColliders = new Dictionary<Component, Component>();
        foreach (List<Component> group in mergeableGroups.Values)
        {
            if (group.Count <= 1)
                continue;

            Component keep = group[0];
            for (int i = 1; i < group.Count; i++)
                mergedColliders[group[i]] = keep;
        }

        if (mergedColliders.Count == 0)
            return 0;

        Type physBoneType = FindType("VRC.Dynamics.VRCPhysBoneBase");
        if (physBoneType == null)
            physBoneType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneBase");

        if (physBoneType != null)
        {
            Component[] physBones = root.GetComponentsInChildren(physBoneType, true);
            foreach (Component physBone in physBones)
            {
                if (!(GetMemberValue(physBone, "colliders") is System.Collections.IList colliderList))
                    continue;

                for (int i = 0; i < colliderList.Count; i++)
                {
                    if (colliderList[i] is Component currentCollider && mergedColliders.TryGetValue(currentCollider, out Component mergedTo))
                        colliderList[i] = mergedTo;
                }
            }
        }

        foreach (Component duplicate in mergedColliders.Keys)
        {
            if (duplicate != null)
                UnityObject.DestroyImmediate(duplicate);
        }

        return mergedColliders.Count;
    }

    private static bool IsAnimatedTransformOrParent(Transform current, Transform root, HashSet<Transform> animatedTransforms)
    {
        while (current != null && current != root)
        {
            if (animatedTransforms != null && animatedTransforms.Contains(current))
                return true;

            current = current.parent;
        }

        return current != null && animatedTransforms != null && animatedTransforms.Contains(current);
    }

    private static string BuildPhysBoneColliderSignature(Component collider)
    {
        object shapeType = GetMemberValue(collider, "shapeType");
        object insideBounds = GetMemberValue(collider, "insideBounds");
        object radius = GetMemberValue(collider, "radius");
        object height = GetMemberValue(collider, "height");
        object position = GetMemberValue(collider, "position");
        object rotation = GetMemberValue(collider, "rotation");
        object bonesAsSpheres = GetMemberValue(collider, "bonesAsSpheres");

        if (!(position is Vector3 localPosition))
            localPosition = Vector3.zero;

        if (!(rotation is Quaternion localRotation))
            localRotation = Quaternion.identity;

        float worldScale = GetMaxLossyScale(collider.transform);
        float radiusValue = radius is float radiusFloat ? radiusFloat * worldScale : 0f;
        float heightValue = height is float heightFloat ? heightFloat * worldScale : 0f;
        Vector3 worldPosition = collider.transform.TransformPoint(localPosition);
        Quaternion worldRotation = collider.transform.rotation * localRotation;

        Vector3 roundedPosition = RoundVector3(worldPosition);
        Quaternion roundedRotation = RoundQuaternion(worldRotation);
        float roundedRadius = RoundFloat(radiusValue);
        float roundedHeight = RoundFloat(heightValue);

        return string.Join("|",
            shapeType?.ToString() ?? "Unknown",
            insideBounds?.ToString() ?? "False",
            bonesAsSpheres?.ToString() ?? "False",
            roundedRadius.ToString("F5"),
            roundedHeight.ToString("F5"),
            roundedPosition.x.ToString("F5"),
            roundedPosition.y.ToString("F5"),
            roundedPosition.z.ToString("F5"),
            roundedRotation.x.ToString("F5"),
            roundedRotation.y.ToString("F5"),
            roundedRotation.z.ToString("F5"),
            roundedRotation.w.ToString("F5"));
    }

    private static float GetMaxLossyScale(Transform transform)
    {
        if (transform == null)
            return 1f;

        Vector3 lossyScale = transform.lossyScale;
        return Mathf.Max(Mathf.Max(lossyScale.x, lossyScale.y), lossyScale.z);
    }

    private static float RoundFloat(float value)
    {
        return (float)Math.Round(value, 5);
    }

    private static Vector3 RoundVector3(Vector3 value)
    {
        return new Vector3(RoundFloat(value.x), RoundFloat(value.y), RoundFloat(value.z));
    }

    private static Quaternion RoundQuaternion(Quaternion value)
    {
        return new Quaternion(RoundFloat(value.x), RoundFloat(value.y), RoundFloat(value.z), RoundFloat(value.w));
    }

    private static int MirrorIgnoreOtherPhysBonesToIgnoreTransform(GameObject root)
    {
        Type physBoneType = FindType("VRC.Dynamics.VRCPhysBoneBase");
        if (physBoneType == null)
            physBoneType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneBase");

        if (physBoneType == null)
            return 0;

        Component[] physBones = root.GetComponentsInChildren(physBoneType, true);
        if (physBones == null || physBones.Length == 0)
            return 0;

        var physBoneByTarget = new Dictionary<Transform, HashSet<Component>>();
        foreach (Component physBone in physBones)
        {
            Transform target = GetPhysBoneTarget(physBone);
            if (target == null)
                continue;

            if (!physBoneByTarget.TryGetValue(target, out HashSet<Component> bucket))
            {
                bucket = new HashSet<Component>();
                physBoneByTarget.Add(target, bucket);
            }

            bucket.Add(physBone);
        }

        int mirrored = 0;
        foreach (Component physBone in physBones)
        {
            if (!GetBoolMemberValue(physBone, "ignoreOtherPhysBones"))
                continue;

            if (!(GetMemberValue(physBone, "ignoreTransforms") is System.Collections.IList ignoreList))
                continue;

            HashSet<Transform> ignoredTransforms = GetIgnoredTransformSet(ignoreList);

            Transform target = GetPhysBoneTarget(physBone);
            if (target == null || !target.IsChildOf(root.transform))
                continue;

            foreach (Transform affectedTransform in EnumerateAffectedTransforms(target, ignoredTransforms))
            {
                if (affectedTransform == null)
                    continue;

                if (!physBoneByTarget.TryGetValue(affectedTransform, out HashSet<Component> siblings))
                    continue;

                bool hasOtherPhysBone = false;
                foreach (Component sibling in siblings)
                {
                    if (sibling != null && sibling != physBone)
                    {
                        hasOtherPhysBone = true;
                        break;
                    }
                }

                if (!hasOtherPhysBone || !ignoredTransforms.Add(affectedTransform))
                    continue;

                ignoreList.Add(affectedTransform);
                mirrored++;
            }
        }

        return mirrored;
    }

    private static HashSet<Transform> EnumerateAffectedLeafTransforms(Transform target, HashSet<Transform> ignoredTransforms)
    {
        var leafBones = new HashSet<Transform>();
        if (target == null)
            return leafBones;

        var queue = new Queue<Transform>();
        queue.Enqueue(target);
        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (current == null)
                continue;

            List<Transform> children = new List<Transform>();
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child == null || ignoredTransforms.Contains(child))
                    continue;
                children.Add(child);
            }

            if (children.Count == 0)
            {
                if (current != target)
                    leafBones.Add(current);
                continue;
            }

            foreach (Transform child in children)
                queue.Enqueue(child);
        }

        return leafBones;
    }

    private static bool IsSafePhysBoneEndpointReplacement(
        Component physBone,
        HashSet<Transform> leafBones,
        HashSet<Transform> protectedTransforms,
        HashSet<Transform> animatedTransforms)
    {
        if (physBone == null || leafBones == null || leafBones.Count == 0)
            return false;

        if (IsPhysBoneBoneLengthChangeEnabled(physBone))
            return false;

        if (!IsSafeMultiChildReplacement(physBone, leafBones))
            return false;

        foreach (Transform leafBone in leafBones)
        {
            if (leafBone == null)
                return false;

            if (protectedTransforms.Contains(leafBone))
                return false;

            if (animatedTransforms.Contains(leafBone))
                return false;

            if (leafBone.childCount != 0)
                return false;

            if (leafBone.GetComponents<Component>().Length != 1)
                return false;
        }

        return true;
    }

    private static bool IsSafeMultiChildReplacement(Component physBone, HashSet<Transform> leafBones)
    {
        Transform rootBone = GetPhysBoneTarget(physBone);
        if (rootBone == null)
            return false;

        string multiChildType = GetMemberValue(physBone, "multiChildType")?.ToString();
        HashSet<Transform> ignores = GetIgnoredTransformSet(physBone);

        var queue = new Queue<Transform>();
        queue.Enqueue(rootBone);
        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child == null || ignores.Contains(child))
                    continue;
                children.Add(child);
            }

            if (children.Count > 1)
            {
                switch (multiChildType)
                {
                    case "Ignore":
                        int remaining = 0;
                        foreach (Transform child in children)
                        {
                            if (!leafBones.Contains(child))
                                remaining++;
                        }

                        if (remaining < 2)
                            return false;
                        break;
                    case "First":
                        if (leafBones.Contains(children[0]))
                            return false;
                        break;
                    case "Average":
                        foreach (Transform child in children)
                        {
                            if (leafBones.Contains(child))
                                return false;
                        }
                        break;
                }
            }

            foreach (Transform child in children)
                queue.Enqueue(child);
        }

        return true;
    }

    private static bool IsPhysBoneBoneLengthChangeEnabled(Component physBone)
    {
        float maxStretch = GetFloatMemberValue(physBone, "maxStretch");
        float maxSquish = GetFloatMemberValue(physBone, "maxSquish");
        float stretchMotion = GetFloatMemberValue(physBone, "stretchMotion");
        bool anyLengthChange = !Mathf.Approximately(maxStretch, 0f) || !Mathf.Approximately(maxSquish, 0f);
        if (!anyLengthChange)
            return false;

        bool anyGrabbingAllowed = PermissionAllowed(GetMemberValue(physBone, "allowGrabbing"), GetMemberValue(physBone, "grabFilter"));
        bool anyCollisionAllowed = PermissionAllowed(GetMemberValue(physBone, "allowCollision"), GetMemberValue(physBone, "collisionFilter"));
        if (GetMemberValue(physBone, "colliders") is System.Collections.IList colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] is UnityEngine.Object collider && collider != null)
                {
                    anyCollisionAllowed = true;
                    break;
                }
            }
        }

        string version = GetMemberValue(physBone, "version")?.ToString();
        if (string.Equals(version, "Version_1_1", StringComparison.Ordinal))
            return (anyGrabbingAllowed || !Mathf.Approximately(stretchMotion, 0f) || anyCollisionAllowed) && anyLengthChange;

        return (anyGrabbingAllowed || anyCollisionAllowed) && anyLengthChange;
    }

    private static bool PermissionAllowed(object allowValue, object filterValue)
    {
        string allow = allowValue?.ToString();
        switch (allow)
        {
            case "True":
                return true;
            case "False":
                return false;
            case "Other":
                return GetBoolMemberValue(filterValue, "allowSelf") || GetBoolMemberValue(filterValue, "allowOthers");
            default:
                return false;
        }
    }

    private static Vector3 AverageLocalPosition(IEnumerable<Transform> transforms)
    {
        Vector3 total = Vector3.zero;
        int count = 0;
        foreach (Transform transform in transforms)
        {
            if (transform == null)
                continue;

            total += transform.localPosition;
            count++;
        }

        return count == 0 ? Vector3.zero : total / count;
    }

    private static bool AreApproximatelyEqualLocalPosition(IEnumerable<Transform> transforms, Vector3 target)
    {
        const float tolerance = 0.00001f;
        foreach (Transform transform in transforms)
        {
            if (transform == null)
                return false;

            if (Vector3.Distance(transform.localPosition, target) > tolerance)
                return false;
        }

        return true;
    }

    private static HashSet<Transform> GetIgnoredTransformSet(Component physBone)
    {
        if (physBone == null)
            return new HashSet<Transform>();

        return GetMemberValue(physBone, "ignoreTransforms") is System.Collections.IList ignoreList
            ? GetIgnoredTransformSet(ignoreList)
            : new HashSet<Transform>();
    }

    private static HashSet<Transform> GetIgnoredTransformSet(System.Collections.IList ignoreList)
    {
        var ignoredTransforms = new HashSet<Transform>();
        if (ignoreList == null)
            return ignoredTransforms;

        for (int i = 0; i < ignoreList.Count; i++)
        {
            Transform ignoredTransform = AsTransform(ignoreList[i]);
            if (ignoredTransform != null)
                ignoredTransforms.Add(ignoredTransform);
        }

        return ignoredTransforms;
    }

    private static IEnumerable<Transform> EnumerateAffectedTransforms(Transform target, HashSet<Transform> ignoredTransforms)
    {
        if (target == null)
            yield break;

        if (ignoredTransforms != null && ignoredTransforms.Contains(target))
            yield break;

        yield return target;

        for (int i = 0; i < target.childCount; i++)
        {
            Transform child = target.GetChild(i);
            foreach (Transform nested in EnumerateAffectedTransforms(child, ignoredTransforms))
                yield return nested;
        }
    }

    private static Transform GetPhysBoneTarget(Component physBone)
    {
        Transform rootTransform = AsTransform(GetMemberValue(physBone, "rootTransform"));
        return rootTransform != null ? rootTransform : physBone.transform;
    }

    private static bool GetBoolMemberValue(object target, string memberName)
    {
        object value = GetMemberValue(target, memberName);
        return value is bool flag && flag;
    }

    private static float GetFloatMemberValue(object target, string memberName)
    {
        object value = GetMemberValue(target, memberName);
        return value is float number ? number : 0f;
    }

    private static Vector3 GetVector3MemberValue(object target, string memberName)
    {
        object value = GetMemberValue(target, memberName);
        return value is Vector3 vector ? vector : Vector3.zero;
    }

    private static Transform AsTransform(object value)
    {
        if (value is Transform transform)
            return transform;

        if (value is GameObject gameObject)
            return gameObject.transform;

        if (value is Component component)
            return component.transform;

        return null;
    }

    private static int CleanupReferenceList(Component component, string memberName)
    {
        if (component == null)
            return 0;

        object listObject = GetMemberValue(component, memberName);
        if (listObject == null)
            return 0;

        if (!(listObject is System.Collections.IList list))
            return 0;

        var seen = new HashSet<UnityEngine.Object>();
        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            object entry = list[i];
            UnityEngine.Object unityObject = entry as UnityEngine.Object;
            if (unityObject == null || !seen.Add(unityObject))
            {
                list.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null)
            return null;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo property = target.GetType().GetProperty(memberName, flags);
        if (property != null && property.CanRead)
            return property.GetValue(target, null);

        FieldInfo field = target.GetType().GetField(memberName, flags);
        if (field != null)
            return field.GetValue(target);

        return null;
    }

    private static bool SetMemberValue(object target, string memberName, object value)
    {
        if (target == null)
            return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo property = target.GetType().GetProperty(memberName, flags);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return true;
        }

        FieldInfo field = target.GetType().GetField(memberName, flags);
        if (field != null)
        {
            field.SetValue(target, value);
            return true;
        }

        return false;
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(fullName, false);
            if (found != null)
                return found;
        }

        return null;
    }

    private static int RemoveMissingScripts(GameObject root)
    {
        if (root == null)
            return 0;

        int removed = 0;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == null)
                continue;

            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
        }

        return removed;
    }

    private static int RemoveEmptyLeafObjects(Transform root, HashSet<Transform> protectedTransforms, bool aggressive)
    {
        int removed = 0;
        bool changed;

        do
        {
            changed = false;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                Transform current = transforms[i];
                if (current == null || current == root)
                    continue;

                if (current.childCount > 0)
                    continue;

                if (!CanRemoveEmptyLeafTransform(current, root, protectedTransforms, aggressive))
                    continue;

                UnityObject.DestroyImmediate(current.gameObject);
                removed++;
                changed = true;
            }
        }
        while (changed);

        return removed;
    }

    private static bool CanRemoveEmptyLeafTransform(Transform current, Transform root, HashSet<Transform> protectedTransforms, bool aggressive)
    {
        if (current == null || current == root)
            return false;

        if (protectedTransforms != null && protectedTransforms.Contains(current))
            return false;

        Component[] components = current.GetComponents<Component>();
        if (components.Length != 1)
            return false;

        if (aggressive)
            return true;

        return current.localPosition == Vector3.zero
            && current.localRotation == Quaternion.identity
            && current.localScale == Vector3.one;
    }
}
