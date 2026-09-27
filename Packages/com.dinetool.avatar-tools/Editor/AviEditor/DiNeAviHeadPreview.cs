using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders only detached graphics in a hidden preview scene. Source renderers, transforms,
/// materials, and blend shape weights are read-only throughout the preview lifetime.
/// </summary>
internal sealed class DiNeAviHeadPreview : IDisposable
{
    private Scene _scene;
    private Camera _camera;
    private readonly List<GameObject> _snapshotRoots = new List<GameObject>();
    private readonly List<Mesh> _snapshotMeshes = new List<Mesh>();
    private readonly Dictionary<Transform, Transform> _transforms = new Dictionary<Transform, Transform>();
    private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    public void Render(
        GameObject avatarRoot,
        SkinnedMeshRenderer editedRenderer,
        IReadOnlyDictionary<int, float> weightOverrides,
        Mesh previewMesh,
        RenderTexture target,
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        float fieldOfView,
        float nearClipPlane,
        float farClipPlane)
    {
        if (target == null) return;
        if (editedRenderer != null && (avatarRoot == null ||
            !editedRenderer.transform.IsChildOf(avatarRoot.transform)))
            avatarRoot = editedRenderer.transform.root.gameObject;
        if (avatarRoot == null) return;

        RenderTexture previousActive = RenderTexture.active;
        try
        {
            EnsureScene();
            foreach (Renderer source in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy ||
                    source.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                    continue;

                if (source is SkinnedMeshRenderer skinned)
                    AddSkinnedSnapshot(skinned, skinned == editedRenderer ? weightOverrides : null,
                        skinned == editedRenderer ? previewMesh : null);
                else if (source is MeshRenderer meshRenderer)
                    AddMeshSnapshot(meshRenderer);
            }

            _camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = nearClipPlane;
            _camera.farClipPlane = farClipPlane;
            _camera.aspect = (float)target.width / Mathf.Max(1, target.height);
            _camera.targetTexture = target;
            _camera.Render();
        }
        finally
        {
            if (_camera != null) _camera.targetTexture = null;
            RenderTexture.active = previousActive;
            ClearSnapshot();
        }
    }

    private void EnsureScene()
    {
        if (_scene.IsValid() && _camera != null) return;

        Dispose();
        try
        {
            _scene = EditorSceneManager.NewPreviewScene();
            GameObject cameraRig = CreatePreviewObject("__DiNeHeadPreviewRig__");
            GameObject cameraObject = CreatePreviewObject("__DiNeHeadPreviewCamera__");
            // NDMF hooks parentless cameras and can substitute cached avatar proxies.
            // A parented camera rendering to a texture is excluded from that hook.
            cameraObject.transform.SetParent(cameraRig.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            // Moving the camera alone does not isolate Camera.Render() from other open scenes.
            _camera.scene = _scene;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.orthographic = false;
            _camera.cullingMask = 1;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;

            AddLight("__DiNeHeadPreviewKeyLight__", new Vector3(25f, -20f, 0f), 1.05f, Color.white);
            AddLight("__DiNeHeadPreviewFillLight__", new Vector3(-10f, 150f, 0f), 0.45f,
                new Color(0.86f, 0.89f, 1f));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private GameObject CreatePreviewObject(string name)
    {
        // Hide the empty object immediately and move it before adding any rendering components.
        GameObject copy = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
        try
        {
            SceneManager.MoveGameObjectToScene(copy, _scene);
            return copy;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(copy);
            throw;
        }
    }

    private void AddLight(string name, Vector3 localEuler, float intensity, Color color)
    {
        GameObject lightObject = CreatePreviewObject(name);
        lightObject.transform.SetParent(_camera.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(localEuler);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1;
    }

    private Transform CopyTransform(Transform source)
    {
        if (source == null) return null;
        if (_transforms.TryGetValue(source, out Transform existing)) return existing;

        Transform parent = CopyTransform(source.parent);
        GameObject copy = CreatePreviewObject(source.name);
        if (parent == null) _snapshotRoots.Add(copy);
        else copy.transform.SetParent(parent, false);
        copy.transform.localPosition = source.localPosition;
        copy.transform.localRotation = source.localRotation;
        copy.transform.localScale = source.localScale;
        _transforms.Add(source, copy.transform);
        return copy.transform;
    }

    private void AddSkinnedSnapshot(
        SkinnedMeshRenderer source, IReadOnlyDictionary<int, float> weightOverrides, Mesh previewMesh)
    {
        Mesh mesh = previewMesh != null ? previewMesh : source.sharedMesh;
        if (mesh == null) return;

        Transform copy = CopyTransform(source.transform);
        SkinnedMeshRenderer skin = copy.gameObject.AddComponent<SkinnedMeshRenderer>();
        skin.enabled = false;
        skin.sharedMesh = mesh;
        skin.quality = source.quality;
        skin.localBounds = source.localBounds;
        skin.updateWhenOffscreen = true;
        skin.rootBone = CopyTransform(source.rootBone);
        Transform[] sourceBones = source.bones;
        var bones = new Transform[sourceBones.Length];
        for (int i = 0; i < bones.Length; i++) bones[i] = CopyTransform(sourceBones[i]);
        skin.bones = bones;

        Mesh sourceMesh = source.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            int sourceIndex = sourceMesh == mesh ? i
                : sourceMesh != null ? sourceMesh.GetBlendShapeIndex(mesh.GetBlendShapeName(i)) : -1;
            float weight = sourceIndex >= 0 ? source.GetBlendShapeWeight(sourceIndex) : 0f;
            if (weightOverrides != null && sourceIndex >= 0 && weightOverrides.TryGetValue(sourceIndex, out float overridden))
                weight = overridden;
            skin.SetBlendShapeWeight(i, weight);
        }

        var baked = new Mesh { name = "__DiNeHeadPreviewMesh__", hideFlags = HideFlags.HideAndDontSave };
        _snapshotMeshes.Add(baked);
        skin.BakeMesh(baked, true);
        // Only a static renderer is exposed to the preview camera, never a source behaviour.
        UnityEngine.Object.DestroyImmediate(skin);
        copy.gameObject.AddComponent<MeshFilter>().sharedMesh = baked;
        CopyRenderer(source, copy.gameObject.AddComponent<MeshRenderer>());
    }

    private void AddMeshSnapshot(MeshRenderer source)
    {
        MeshFilter filter = source.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Transform copy = CopyTransform(source.transform);
        copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        CopyRenderer(source, copy.gameObject.AddComponent<MeshRenderer>());
    }

    private void CopyRenderer(Renderer source, MeshRenderer destination)
    {
        Material[] materials = source.sharedMaterials;
        destination.sharedMaterials = materials;
        destination.shadowCastingMode = ShadowCastingMode.Off;
        destination.receiveShadows = false;
        destination.lightProbeUsage = LightProbeUsage.Off;
        destination.reflectionProbeUsage = ReflectionProbeUsage.Off;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        source.GetPropertyBlock(_propertyBlock);
        destination.SetPropertyBlock(_propertyBlock);
        for (int i = 0; i < materials.Length; i++)
        {
            _propertyBlock.Clear();
            source.GetPropertyBlock(_propertyBlock, i);
            if (!_propertyBlock.isEmpty) destination.SetPropertyBlock(_propertyBlock, i);
        }
        _propertyBlock.Clear();
    }

    private void ClearSnapshot()
    {
        foreach (GameObject root in _snapshotRoots)
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        foreach (Mesh mesh in _snapshotMeshes)
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
        _snapshotRoots.Clear();
        _snapshotMeshes.Clear();
        _transforms.Clear();
    }

    public void Dispose()
    {
        ClearSnapshot();
        if (_camera != null) _camera.targetTexture = null;
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        _camera = null;
        _scene = default;
    }
}
