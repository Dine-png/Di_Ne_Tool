#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 쉐이프키 편집기 — 핵심 메시 조작 로직
/// · 기존 쉐이프키를 혼합해 새 쉐이프키 생성
/// · 기존 쉐이프키의 강도(스케일) 수정
/// </summary>
public static class DiNeShapeKeyEditorCore
{
    private const string SAVE_FOLDER = "Assets/Di Ne/ShapeKeys";

    // ─── 유틸 ────────────────────────────────────────────────────────────────

    /// <summary>아바타 루트에서 Body SMR 자동 탐색. 없으면 첫 번째 SMR 반환.</summary>
    public static SkinnedMeshRenderer FindBodySmr(GameObject avatarRoot)
    {
        if (avatarRoot == null) return null;
        var all = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var s in all)
            if (s.name.Equals("Body", System.StringComparison.OrdinalIgnoreCase)) return s;
        return all.Length > 0 ? all[0] : null;
    }

    /// <summary>SMR 메시의 모든 쉐이프키 이름 배열 반환.</summary>
    public static string[] GetShapeKeyNames(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.sharedMesh == null) return new string[0];
        var mesh = smr.sharedMesh;
        int n = mesh.blendShapeCount;
        var names = new string[n];
        for (int i = 0; i < n; i++) names[i] = mesh.GetBlendShapeName(i);
        return names;
    }

    // ─── 새 쉐이프키 혼합 생성 ────────────────────────────────────────────────

    /// <summary>
    /// 기존 쉐이프키들을 지정 비율로 혼합해 새 쉐이프키를 만든다.
    /// weight = 100 이면 해당 키의 최대 변형 100% 적용.
    /// </summary>
    public static bool CreateMixedShapeKey(
        SkinnedMeshRenderer smr,
        IList<(int index, float weight)> entries,
        string newName,
        out string error)
    {
        error = null;
        if (!ValidateSmr(smr, out error)) return false;
        if (string.IsNullOrWhiteSpace(newName)) { error = "이름을 입력해주세요."; return false; }
        if (entries == null || entries.Count == 0) { error = "혼합할 쉐이프키를 추가해주세요."; return false; }

        var mesh = smr.sharedMesh;
        int vCount = mesh.vertexCount;

        var totalDelta  = new Vector3[vCount];
        var totalNormal = new Vector3[vCount];
        var totalTangent= new Vector3[vCount];

        bool anyValid = false;
        foreach (var (idx, wt) in entries)
        {
            if (idx < 0 || idx >= mesh.blendShapeCount) continue;
            if (Mathf.Approximately(wt, 0f)) continue;

            int frames = mesh.GetBlendShapeFrameCount(idx);
            if (frames == 0) continue;

            // 마지막 프레임(최대값) 기준으로 정규화
            int lastFrame = frames - 1;
            float frameW = mesh.GetBlendShapeFrameWeight(idx, lastFrame);
            if (Mathf.Approximately(frameW, 0f)) continue;

            var d  = new Vector3[vCount];
            var dn = new Vector3[vCount];
            var dt = new Vector3[vCount];
            mesh.GetBlendShapeFrameVertices(idx, lastFrame, d, dn, dt);

            float scale = (wt / 100f) / (frameW / 100f); // weight 기준 정규화
            for (int i = 0; i < vCount; i++)
            {
                totalDelta[i]   += d[i]  * scale;
                totalNormal[i]  += dn[i] * scale;
                totalTangent[i] += dt[i] * scale;
            }
            anyValid = true;
        }

        if (!anyValid) { error = "유효한 쉐이프키 항목이 없습니다."; return false; }

        // 기존 블렌드셰이프를 모두 유지한 복사본 생성
        var newMesh = Object.Instantiate(mesh);
        newMesh.name = mesh.name;
        newMesh.AddBlendShapeFrame(newName, 100f, totalDelta, totalNormal, totalTangent);

        return SaveMesh(smr, newMesh, out error);
    }

    // ─── 기존 쉐이프키 배율 수정 ─────────────────────────────────────────────

    /// <summary>
    /// 기존 쉐이프키의 버텍스 델타를 scaleFactor 배 조정한다.
    /// scaleFactor = 0.5 → 원래 100% 강도가 새 100% 강도의 절반.
    /// scaleFactor = 1.5 → 원래 100% 강도를 1.5배 확장.
    /// </summary>
    public static bool ModifyShapeKeyScale(
        SkinnedMeshRenderer smr,
        int shapeKeyIndex,
        float scaleFactor,
        out string error)
    {
        error = null;
        if (!ValidateSmr(smr, out error)) return false;
        var mesh = smr.sharedMesh;
        if (shapeKeyIndex < 0 || shapeKeyIndex >= mesh.blendShapeCount)
        { error = "잘못된 쉐이프키 인덱스입니다."; return false; }
        if (Mathf.Approximately(scaleFactor, 0f))
        { error = "배율이 0이면 쉐이프키가 사라집니다."; return false; }

        int vCount = mesh.vertexCount;
        int shapeCount = mesh.blendShapeCount;

        // 모든 블렌드셰이프를 재구성 (수정 대상만 스케일 변경)
        var newMesh = BuildMeshBase(mesh);

        for (int si = 0; si < shapeCount; si++)
        {
            string name = mesh.GetBlendShapeName(si);
            int frames = mesh.GetBlendShapeFrameCount(si);
            for (int fi = 0; fi < frames; fi++)
            {
                float fw = mesh.GetBlendShapeFrameWeight(si, fi);
                var d  = new Vector3[vCount];
                var dn = new Vector3[vCount];
                var dt = new Vector3[vCount];
                mesh.GetBlendShapeFrameVertices(si, fi, d, dn, dt);

                if (si == shapeKeyIndex)
                {
                    for (int i = 0; i < vCount; i++)
                    {
                        d[i]  *= scaleFactor;
                        dn[i] *= scaleFactor;
                        dt[i] *= scaleFactor;
                    }
                }

                newMesh.AddBlendShapeFrame(name, fw, d, dn, dt);
            }
        }

        return SaveMesh(smr, newMesh, out error);
    }

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────────────

    private static bool ValidateSmr(SkinnedMeshRenderer smr, out string error)
    {
        error = null;
        if (smr == null) { error = "대상 SkinnedMeshRenderer가 없습니다."; return false; }
        if (smr.sharedMesh == null) { error = "메시가 비어있습니다."; return false; }
        if (smr.sharedMesh.blendShapeCount == 0) { error = "쉐이프키가 없는 메시입니다."; return false; }
        return true;
    }

    /// <summary>블렌드셰이프 없이 기본 메시 데이터만 복사한 빈 Mesh 생성.</summary>
    private static Mesh BuildMeshBase(Mesh src)
    {
        var m = new Mesh();
        m.name           = src.name;
        m.indexFormat    = src.indexFormat;
        m.vertices       = src.vertices;
        m.normals        = src.normals;
        m.tangents       = src.tangents;
        m.colors         = src.colors;
        m.uv             = src.uv;
        m.uv2            = src.uv2;
        m.uv3            = src.uv3;
        m.uv4            = src.uv4;
        m.boneWeights    = src.boneWeights;
        m.bindposes      = src.bindposes;
        m.subMeshCount   = src.subMeshCount;
        for (int i = 0; i < src.subMeshCount; i++)
            m.SetTriangles(src.GetTriangles(i), i);
        m.RecalculateBounds();
        return m;
    }

    private static bool SaveMesh(SkinnedMeshRenderer smr, Mesh newMesh, out string error)
    {
        error = null;
        try
        {
            if (!Directory.Exists(SAVE_FOLDER)) Directory.CreateDirectory(SAVE_FOLDER);

            // 이미 DiNe 저장 메시가 있으면 덮어쓰기, 없으면 새로 생성
            string safeName = newMesh.name.Replace(" ", "_").Replace("/", "_");
            string targetPath = $"{SAVE_FOLDER}/{safeName}_dine.asset";

            if (File.Exists(targetPath))
            {
                // 기존 에셋 업데이트
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(newMesh, existing);
                    Object.DestroyImmediate(newMesh);
                    newMesh = existing;
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(newMesh, targetPath);
                }
            }
            else
            {
                AssetDatabase.CreateAsset(newMesh, targetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RecordObject(smr, "DiNe 쉐이프키 수정");
            smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
            EditorUtility.SetDirty(smr);
            return true;
        }
        catch (System.Exception e)
        {
            error = $"저장 실패: {e.Message}";
            if (newMesh != null && !AssetDatabase.Contains(newMesh))
                Object.DestroyImmediate(newMesh);
            return false;
        }
    }
}
#endif
