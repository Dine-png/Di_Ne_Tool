using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DiNeTool.InGameChecker
{
    public static class DiNeAvatarStats
    {
        public struct StatsData
        {
            public int TriangleCount;
            public int VertexCount;
            public int MeshCount;
            public int BoneCount;
            public int MaterialCount;
            public int TextureCount;
            public long VRAMBytes;
            public long UploadSizeBytes;
            public string PerformanceRank;
            public Color RankColor;
            public Color VRAMColor;
            public Color TriColor;
        }

        public static StatsData Calculate(GameObject avatarRoot)
        {
            var data = new StatsData();

            var skinnedRenderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshRenderers    = avatarRoot.GetComponentsInChildren<MeshRenderer>(true);

            data.MeshCount = skinnedRenderers.Length + meshRenderers.Length;

            var collectedMeshes = new HashSet<Mesh>();

            foreach (var smr in skinnedRenderers)
            {
                if (smr.sharedMesh == null) continue;
                data.TriangleCount += smr.sharedMesh.triangles.Length / 3;
                data.VertexCount   += smr.sharedMesh.vertexCount;
                collectedMeshes.Add(smr.sharedMesh);
            }
            foreach (var mr in meshRenderers)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf?.sharedMesh == null) continue;
                data.TriangleCount += mf.sharedMesh.triangles.Length / 3;
                data.VertexCount   += mf.sharedMesh.vertexCount;
                collectedMeshes.Add(mf.sharedMesh);
            }

            // Bones — count unique non-root transforms
            data.BoneCount = avatarRoot.GetComponentsInChildren<Transform>(true).Length - 1;

            // Materials & Textures
            var allRenderers     = avatarRoot.GetComponentsInChildren<Renderer>(true);
            var uniqueMaterials  = new HashSet<Material>();
            var uniqueTextures   = new HashSet<Texture>();

            foreach (var r in allRenderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    uniqueMaterials.Add(mat);
                    CollectTextures(mat, uniqueTextures);
                }
            }

            data.MaterialCount = uniqueMaterials.Count;
            data.TextureCount  = uniqueTextures.Count;

            foreach (var tex in uniqueTextures)
                data.VRAMBytes += EstimateTextureVRAM(tex);

            // Upload size estimate: compressed mesh data + compressed texture data
            foreach (var mesh in collectedMeshes)
                data.UploadSizeBytes += EstimateMeshSize(mesh);

            foreach (var tex in uniqueTextures)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path)) continue;
                var fi = new System.IO.FileInfo(path);
                if (fi.Exists) data.UploadSizeBytes += fi.Length;
            }

            AssignRanks(ref data);
            return data;
        }

        private static void CollectTextures(Material mat, HashSet<Texture> set)
        {
            var shader = mat.shader;
            if (shader == null) return;
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                var tex = mat.GetTexture(ShaderUtil.GetPropertyName(shader, i));
                if (tex != null) set.Add(tex);
            }
        }

        private static long EstimateTextureVRAM(Texture tex)
        {
            if (tex is Texture2D t2d)
            {
                float bpp = GetBytesPerPixel(t2d.format);
                long baseSize = (long)(t2d.width * t2d.height * bpp);
                return (long)(baseSize * 1.33f); // +33% for mipmaps
            }
            if (tex is RenderTexture rt)
                return (long)(rt.width * rt.height * 4 * 1.33f);
            return 0;
        }

        private static long EstimateMeshSize(Mesh mesh)
        {
            // index buffer + vertex buffer (position+normal+uv+tangent+boneweights)
            long indices  = mesh.triangles.Length * 4L;
            long vertices = mesh.vertexCount * 52L; // 3+3+2+4+4 floats * 4 bytes
            return indices + vertices;
        }

        private static float GetBytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.DXT1:   return 0.5f;
                case TextureFormat.DXT5:   return 1f;
                case TextureFormat.BC7:    return 1f;
                case TextureFormat.RGB24:  return 3f;
                case TextureFormat.RGBA32: return 4f;
                case TextureFormat.RGBA4444: return 2f;
                case TextureFormat.R8:     return 1f;
                case TextureFormat.RG16:   return 2f;
                case TextureFormat.RGBAHalf: return 8f;
                default:                   return 4f;
            }
        }

        private static void AssignRanks(ref StatsData data)
        {
            // VRChat PC avatar performance thresholds (simplified)
            int tri = data.TriangleCount;

            if (tri <= 32_000)
            {
                data.PerformanceRank = "Excellent";
                data.RankColor = new Color(0.2f, 0.85f, 0.35f);
                data.TriColor  = new Color(0.2f, 0.85f, 0.35f);
            }
            else if (tri <= 70_000)
            {
                data.PerformanceRank = "Good";
                data.RankColor = new Color(0.55f, 0.9f, 0.3f);
                data.TriColor  = new Color(0.55f, 0.9f, 0.3f);
            }
            else if (tri <= 100_000)
            {
                data.PerformanceRank = "Medium";
                data.RankColor = new Color(1f, 0.85f, 0.2f);
                data.TriColor  = new Color(1f, 0.85f, 0.2f);
            }
            else if (tri <= 200_000)
            {
                data.PerformanceRank = "Poor";
                data.RankColor = new Color(1f, 0.5f, 0.1f);
                data.TriColor  = new Color(1f, 0.5f, 0.1f);
            }
            else
            {
                data.PerformanceRank = "Very Poor";
                data.RankColor = new Color(1f, 0.2f, 0.2f);
                data.TriColor  = new Color(1f, 0.2f, 0.2f);
            }

            long vramMB = data.VRAMBytes / (1024 * 1024);
            if      (vramMB <= 200)  data.VRAMColor = new Color(0.2f, 0.85f, 0.35f);
            else if (vramMB <= 500)  data.VRAMColor = new Color(1f, 0.85f, 0.2f);
            else                     data.VRAMColor = new Color(1f, 0.3f, 0.2f);
        }
    }
}
