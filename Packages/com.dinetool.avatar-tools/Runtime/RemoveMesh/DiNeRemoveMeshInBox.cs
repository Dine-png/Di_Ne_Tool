using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Removes mesh polygons that fall inside (or, optionally, outside) one or more boxes from the
/// renderer on the same GameObject. Non-destructive: the removal is applied to a cloned avatar at
/// build time (and on NDMF apply-on-play), never to the original mesh asset.
///
/// Boxes are defined in the renderer's local space. For a SkinnedMeshRenderer the stored mesh
/// vertices are already expressed in that space (bind pose), so the box test uses them directly.
///
/// Implements <see cref="IEditorOnly"/> so the component is stripped from the uploaded avatar.
/// </summary>
[AddComponentMenu("DiNe/Remove Mesh In Box")]
[HelpURL("https://github.com/Dine-png/DI_NE_TOOL")]
public sealed class DiNeRemoveMeshInBox : MonoBehaviour, IEditorOnly
{
    [Serializable]
    public struct Box
    {
        public Vector3 center;
        public Vector3 size;
        public Quaternion rotation;

        public static Box Default => new Box
        {
            center = Vector3.zero,
            size = Vector3.one,
            rotation = Quaternion.identity,
        };

        /// <summary>True if <paramref name="point"/> (in the renderer's local space) is inside this box.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - center);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.y) <= half.y
                && Mathf.Abs(local.z) <= half.z;
        }
    }

    [Tooltip("Boxes (in the renderer's local space) used to select polygons for removal.")]
    [SerializeField]
    internal Box[] boxes = { Box.Default };

    [Tooltip("ON: remove polygons fully inside a box. OFF: remove polygons that are NOT fully inside any box.")]
    [SerializeField]
    internal bool removeInBox = true;

    public IReadOnlyList<Box> Boxes => boxes;
    public bool RemoveInBox => removeInBox;
}
