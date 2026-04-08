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

            // mesh.triangles 는 호출마다 배열을 힙에 복사하므로 고유 메쉬당 한 번만 호출해서 캐시
            var meshTriLengthCache = new Dictionary<Mesh, int>();

            foreach (var smr in skinnedRenderers)
            {
                if (smr.sharedMesh == null) continue;
                var mesh = smr.sharedMesh;
                if (!meshTriLengthCache.ContainsKey(mesh))
                    meshTriLengthCache[mesh] = mesh.triangles.Length;
                data.TriangleCount += meshTriLengthCache[mesh] / 3;
                data.VertexCount   += mesh.vertexCount;
            }
            foreach (var mr in meshRenderers)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf?.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (!meshTriLengthCache.ContainsKey(mesh))
                    meshTriLengthCache[mesh] = mesh.triangles.Length;
                data.TriangleCount += meshTriLengthCache[mesh] / 3;
                data.VertexCount   += mesh.vertexCount;
            }

            // Bones — root를 제외한 고유 Transform 수
            data.BoneCount = avatarRoot.GetComponentsInChildren<Transform>(true).Length - 1;

            // Materials & Textures
            var allRenderers    = avatarRoot.GetComponentsInChildren<Renderer>(true);
            var uniqueMaterials = new HashSet<Material>();
            var uniqueTextures  = new HashSet<Texture>();

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

            // 업로드 사이즈 추정: 캐시된 삼각형 수 재사용 (중복 mesh.triangles 호출 방지)
            foreach (var kvp in meshTriLengthCache)
            {
                long indices  = kvp.Value * 4L;          // index buffer (int per index)
                long vertices = kvp.Key.vertexCount * 52L; // pos+normal+uv+tangent+boneWeight
                data.UploadSizeBytes += indices + vertices;
            }

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

        // EstimateMeshSize 제거 — Calculate() 내부에서 캐시된 값으로 인라인 처리

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
            // VRChat PC 아바타 퍼포먼스 랭크 — 공식 기준 (복합 지표)
            // https://docs.vrchat.com/docs/avatar-performance-ranking-system
            int  tri    = data.TriangleCount;
            int  bone   = data.BoneCount;
            int  mat    = data.MaterialCount;
            long vramMB = data.VRAMBytes / (1024 * 1024);

            // 지표별 랭크 점수: 0=Excellent 1=Good 2=Medium 3=Poor 4=VeryPoor
            int triRank  = tri    <=  7_500 ? 0 : tri    <= 10_000 ? 1 : tri    <= 15_000 ? 2 : tri    <= 70_000 ? 3 : 4;
            int boneRank = bone   <=     75 ? 0 : bone   <=    150 ? 1 : bone   <=    256 ? 2 : bone   <=    400 ? 3 : 4;
            int matRank  = mat    <=      1 ? 0 : mat    <=      4 ? 1 : mat    <=      8 ? 2 : mat    <=     16 ? 3 : 4;
            int vramRank = vramMB <=     75 ? 0 : vramMB <=    150 ? 1 : vramMB <=    300 ? 2 : vramMB <=    500 ? 3 : 4;

            // 가장 나쁜 지표 기준으로 종합 랭크 결정
            int rank = Mathf.Max(triRank, Mathf.Max(boneRank, Mathf.Max(matRank, vramRank)));

            string[] names  = { "Excellent", "Good", "Medium", "Poor", "Very Poor" };
            Color[]  colors =
            {
                new Color(0.20f, 0.85f, 0.35f),
                new Color(0.55f, 0.90f, 0.30f),
                new Color(1.00f, 0.85f, 0.20f),
                new Color(1.00f, 0.50f, 0.10f),
                new Color(1.00f, 0.20f, 0.20f),
            };

            data.PerformanceRank = names[rank];
            data.RankColor       = colors[rank];
            data.TriColor        = colors[triRank]; // 트라이앵글 지표는 별도 색상

            data.VRAMColor = vramMB <= 150
                ? new Color(0.20f, 0.85f, 0.35f)
                : vramMB <= 300
                    ? new Color(1.00f, 0.85f, 0.20f)
                    : new Color(1.00f, 0.30f, 0.20f);
        }
    }
}
