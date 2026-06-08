#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies <see cref="DiNeRemoveMeshInBox"/> removal to a renderer by dropping the affected triangles
/// from a cloned mesh. The original shared mesh asset is never modified.
/// </summary>
public static class DiNeRemoveMeshUtility
{
    /// <summary>Processes one component in place (on whatever avatar copy it currently lives on).</summary>
    public static bool Apply(DiNeRemoveMeshInBox component)
    {
        if (component == null)
            return false;

        var renderer = component.GetComponent<Renderer>();
        return RemoveInBox(renderer, component.Boxes, component.RemoveInBox);
    }

    /// <summary>
    /// Removes triangles whose three vertices are all inside one of <paramref name="boxes"/>
    /// (or, when <paramref name="removeInBox"/> is false, the triangles that are NOT fully inside any box).
    /// </summary>
    /// <returns>True if the mesh was changed.</returns>
    public static bool RemoveInBox(Renderer renderer, IReadOnlyList<DiNeRemoveMeshInBox.Box> boxes, bool removeInBox)
    {
        if (renderer == null || boxes == null || boxes.Count == 0)
            return false;

        Mesh mesh = GetMesh(renderer);
        if (mesh == null || mesh.vertexCount == 0)
            return false;

        Vector3[] vertices = mesh.vertices;
        int vertexCount = vertices.Length;

        // Precompute, per vertex, whether it sits inside any box.
        var insideAnyBox = new bool[vertexCount];
        for (int v = 0; v < vertexCount; v++)
        {
            Vector3 p = vertices[v];
            for (int b = 0; b < boxes.Count; b++)
            {
                if (boxes[b].Contains(p))
                {
                    insideAnyBox[v] = true;
                    break;
                }
            }
        }

        Mesh result = Object.Instantiate(mesh);
        result.name = mesh.name + " [Opticore RemoveMesh]";

        bool changed = false;
        int subMeshCount = result.subMeshCount;
        for (int sub = 0; sub < subMeshCount; sub++)
        {
            if (result.GetTopology(sub) != MeshTopology.Triangles)
                continue;

            int[] triangles = result.GetTriangles(sub);
            if (triangles == null || triangles.Length < 3)
                continue;

            var kept = new List<int>(triangles.Length);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                bool fullyInside = a < vertexCount && b < vertexCount && c < vertexCount
                    && insideAnyBox[a] && insideAnyBox[b] && insideAnyBox[c];

                bool remove = removeInBox ? fullyInside : !fullyInside;
                if (remove)
                {
                    changed = true;
                    continue;
                }

                kept.Add(a);
                kept.Add(b);
                kept.Add(c);
            }

            if (changed)
                result.SetTriangles(kept, sub, false);
        }

        if (!changed)
        {
            Object.DestroyImmediate(result);
            return false;
        }

        result.RecalculateBounds();
        SetMesh(renderer, result);
        return true;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer smr)
            return smr.sharedMesh;

        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        return null;
    }

    private static void SetMesh(Renderer renderer, Mesh mesh)
    {
        if (renderer is SkinnedMeshRenderer smr)
        {
            smr.sharedMesh = mesh;
            return;
        }

        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
                filter.sharedMesh = mesh;
        }
    }
}
#endif
