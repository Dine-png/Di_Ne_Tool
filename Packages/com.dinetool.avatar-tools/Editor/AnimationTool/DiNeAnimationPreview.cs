using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Animation is sampled only on detached transforms and graphics, never on avatar behaviours.</summary>
internal sealed class DiNeAnimationPreview : IDisposable
{
    private Scene scene;
    private Camera camera;
    private GameObject hierarchyRoot;
    private AnimationClip sampledClip;
    private readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
    private readonly Dictionary<Renderer, Renderer> renderers = new Dictionary<Renderer, Renderer>();
    private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
    public GameObject Root { get; private set; }

    public void Sample(GameObject source, AnimationClip clip, float time)
    {
        ClearHierarchy();
        if (source == null) return;
        try
        {
            EnsureScene();
            CopyAncestors(source.transform.parent);
            CopyHierarchy(source.transform);
            Root = transforms[source.transform].gameObject;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true)) CopyRenderer(renderer);
            foreach (Animator animator in source.GetComponentsInChildren<Animator>(true))
            {
                Animator copy = transforms[animator.transform].gameObject.AddComponent<Animator>();
                copy.avatar = animator.avatar;
                copy.applyRootMotion = animator.applyRootMotion;
                copy.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                copy.runtimeAnimatorController = null;
            }
            if (clip == null) return;
            sampledClip = UnityEngine.Object.Instantiate(clip);
            sampledClip.hideFlags = HideFlags.HideAndDontSave;
            // Object curves may swap materials before shader-property curves are evaluated.
            // Remap those references as well so every material reachable by sampling is private.
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(sampledClip))
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(sampledClip, binding);
                for (int i = 0; i < keys.Length; i++)
                    if (keys[i].value is Material material) keys[i].value = CopyMaterial(material);
                AnimationUtility.SetObjectReferenceCurve(sampledClip, binding, keys);
            }
            sampledClip.SampleAnimation(Root, Mathf.Clamp(time, 0f, clip.length));
        }
        catch
        {
            ClearHierarchy();
            throw;
        }
    }

    public Transform GetTransform(Transform source)
    {
        return source != null && transforms.TryGetValue(source, out Transform copy) ? copy : null;
    }

    public Renderer GetRenderer(Renderer source)
    {
        return source != null && renderers.TryGetValue(source, out Renderer copy) ? copy : null;
    }

    public void SetShapeWeights(SkinnedMeshRenderer source, IReadOnlyList<float> values)
    {
        if (source == null || values == null || !renderers.TryGetValue(source, out Renderer copy) ||
            !(copy is SkinnedMeshRenderer skin) || skin.sharedMesh == null) return;
        for (int i = 0; i < Mathf.Min(values.Count, skin.sharedMesh.blendShapeCount); i++)
            skin.SetBlendShapeWeight(i, values[i]);
    }

    public void Render(RenderTexture target, Vector3 cameraPosition, Quaternion cameraRotation,
        float fieldOfView, float nearClipPlane, float farClipPlane)
    {
        if (Root == null || target == null) return;
        EnsureScene();
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = nearClipPlane;
            camera.farClipPlane = farClipPlane;
            camera.aspect = (float)target.width / Mathf.Max(1, target.height);
            camera.targetTexture = target;
            camera.Render();
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
        }
    }

    private void EnsureScene()
    {
        if (scene.IsValid() && camera != null) return;
        Dispose();
        scene = EditorSceneManager.NewPreviewScene();
        GameObject rig = CreateObject("__DiNeAnimationPreviewCameraRig__");
        GameObject cameraObject = CreateObject("__DiNeAnimationPreviewCamera__");
        cameraObject.transform.SetParent(rig.transform, false);
        camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        AddLight("Key", new Vector3(25f, -20f, 0f), 1.05f, Color.white);
        AddLight("Fill", new Vector3(-10f, 150f, 0f), .45f, new Color(.86f, .89f, 1f));
    }

    private GameObject CreateObject(string name)
    {
        GameObject copy = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
        SceneManager.MoveGameObjectToScene(copy, scene);
        copy.layer = 0;
        return copy;
    }

    private void AddLight(string name, Vector3 rotation, float intensity, Color color)
    {
        GameObject go = CreateObject("__DiNeAnimationPreview" + name + "Light__");
        go.transform.SetParent(camera.transform, false);
        go.transform.localRotation = Quaternion.Euler(rotation);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1;
    }

    private void CopyAncestors(Transform source)
    {
        if (source == null) return;
        CopyAncestors(source.parent);
        CopyTransform(source);
    }

    private void CopyHierarchy(Transform source)
    {
        CopyTransform(source);
        for (int i = 0; i < source.childCount; i++) CopyHierarchy(source.GetChild(i));
    }

    private Transform CopyTransform(Transform source)
    {
        if (transforms.TryGetValue(source, out Transform existing)) return existing;
        GameObject copy = CreateObject(source.name);
        if (source.parent != null && transforms.TryGetValue(source.parent, out Transform parent))
            copy.transform.SetParent(parent, false);
        else hierarchyRoot = copy;
        copy.transform.localPosition = source.localPosition;
        copy.transform.localRotation = source.localRotation;
        copy.transform.localScale = source.localScale;
        copy.SetActive(source.gameObject.activeSelf);
        transforms.Add(source, copy.transform);
        return copy.transform;
    }

    private Material CopyMaterial(Material source)
    {
        if (source == null) return null;
        if (materials.TryGetValue(source, out Material copy)) return copy;
        copy = new Material(source) { name = source.name, hideFlags = HideFlags.HideAndDontSave };
        materials.Add(source, copy);
        return copy;
    }

    private void CopyRenderer(Renderer source)
    {
        Renderer copy;
        GameObject go = transforms[source.transform].gameObject;
        if (source is SkinnedMeshRenderer skin)
        {
            SkinnedMeshRenderer destination = go.AddComponent<SkinnedMeshRenderer>();
            destination.sharedMesh = skin.sharedMesh;
            destination.quality = skin.quality;
            destination.updateWhenOffscreen = true;
            destination.rootBone = skin.rootBone != null ? GetTransform(skin.rootBone) : null;
            Transform[] bones = skin.bones;
            Transform[] copiedBones = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++) copiedBones[i] = GetTransform(bones[i]);
            destination.bones = copiedBones;
            destination.localBounds = skin.localBounds;
            if (skin.sharedMesh != null)
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    destination.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            copy = destination;
        }
        else if (source is MeshRenderer)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null) return;
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            copy = go.AddComponent<MeshRenderer>();
        }
        else return;
        Material[] sourceMaterials = source.sharedMaterials;
        Material[] copiedMaterials = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++) copiedMaterials[i] = CopyMaterial(sourceMaterials[i]);
        copy.sharedMaterials = copiedMaterials;
        copy.enabled = source.enabled;
        copy.forceRenderingOff = source.forceRenderingOff || source.shadowCastingMode == ShadowCastingMode.ShadowsOnly;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder;
        source.GetPropertyBlock(propertyBlock);
        copy.SetPropertyBlock(propertyBlock);
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            propertyBlock.Clear();
            source.GetPropertyBlock(propertyBlock, i);
            if (!propertyBlock.isEmpty) copy.SetPropertyBlock(propertyBlock, i);
        }
        propertyBlock.Clear();
        renderers.Add(source, copy);
    }

    private void ClearHierarchy()
    {
        if (hierarchyRoot != null) UnityEngine.Object.DestroyImmediate(hierarchyRoot);
        if (sampledClip != null) UnityEngine.Object.DestroyImmediate(sampledClip);
        foreach (Material material in materials.Values)
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        hierarchyRoot = null;
        sampledClip = null;
        Root = null;
        transforms.Clear();
        renderers.Clear();
        materials.Clear();
    }

    public void Dispose()
    {
        ClearHierarchy();
        if (camera != null) camera.targetTexture = null;
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        camera = null;
        scene = default;
    }
}
