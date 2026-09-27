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

        if (mesh.GetBlendShapeIndex(newName) >= 0) { error = "이미 존재하는 쉐이프키 이름입니다."; return false; }

        // 기존 블렌드셰이프를 모두 유지한 복사본 생성
        var newMesh = Object.Instantiate(mesh);
        newMesh.name = mesh.name;
        newMesh.AddBlendShapeFrame(newName, 100f, totalDelta, totalNormal, totalTangent);

        return SaveMesh(smr, newMesh, out error);
    }

    // ─── 기존 쉐이프키를 믹스로 교체 ────────────────────────────────────────────

    /// <summary>
    /// 기존 쉐이프키(targetIndex)의 버텍스 델타를
    /// 다른 쉐이프키들의 혼합으로 완전히 교체한다.
    /// </summary>
    public static bool ReplaceShapeKeyWithMix(
        SkinnedMeshRenderer smr,
        int targetIndex,
        IList<(int index, float weight)> entries,
        out string error)
    {
        error = null;
        if (!ValidateSmr(smr, out error)) return false;
        var newMesh = BuildReplacementMesh(smr.sharedMesh, targetIndex, entries, out error);
        return newMesh != null && SaveMesh(smr, newMesh, out error);
    }

    /// <summary>
    /// Creates a caller-owned replacement mesh for both preview and Apply.
    /// Leaves source data and assets untouched; returns null for invalid mix inputs.
    /// </summary>
    public static Mesh BuildReplacementMesh(
        Mesh mesh,
        int targetIndex,
        IList<(int index, float weight)> entries,
        out string error)
    {
        error = null;
        if (mesh == null) { error = "메시가 비어있습니다."; return null; }
        if (mesh.blendShapeCount == 0) { error = "쉐이프키가 없는 메시입니다."; return null; }
        if (targetIndex < 0 || targetIndex >= mesh.blendShapeCount)
        { error = "잘못된 대상 쉐이프키 인덱스입니다."; return null; }
        if (entries == null || entries.Count == 0)
        { error = "혼합할 쉐이프키를 추가해주세요."; return null; }

        int vCount = mesh.vertexCount;

        // 믹스 계산
        var totalDelta   = new Vector3[vCount];
        var totalNormal  = new Vector3[vCount];
        var totalTangent = new Vector3[vCount];
        bool anyValid = false;
        foreach (var (idx, wt) in entries)
        {
            if (idx < 0 || idx >= mesh.blendShapeCount) continue;
            if (Mathf.Approximately(wt, 0f)) continue;
            int frames = mesh.GetBlendShapeFrameCount(idx);
            if (frames == 0) continue;
            int lastFrame = frames - 1;
            float frameW = mesh.GetBlendShapeFrameWeight(idx, lastFrame);
            if (Mathf.Approximately(frameW, 0f)) continue;
            var d  = new Vector3[vCount];
            var dn = new Vector3[vCount];
            var dt = new Vector3[vCount];
            mesh.GetBlendShapeFrameVertices(idx, lastFrame, d, dn, dt);
            float scale = (wt / 100f) / (frameW / 100f);
            for (int i = 0; i < vCount; i++)
            {
                totalDelta[i]   += d[i]  * scale;
                totalNormal[i]  += dn[i] * scale;
                totalTangent[i] += dt[i] * scale;
            }
            anyValid = true;
        }
        if (!anyValid) { error = "유효한 쉐이프키 항목이 없습니다."; return null; }

        // 메시 재구성 — 대상 키만 델타 교체
        int shapeCount = mesh.blendShapeCount;
        var newMesh = BuildMeshBase(mesh);
        try
        {
            for (int si = 0; si < shapeCount; si++)
            {
                string name = mesh.GetBlendShapeName(si);
                int frames = mesh.GetBlendShapeFrameCount(si);
                float lastFW = mesh.GetBlendShapeFrameWeight(si, frames - 1);
                for (int fi = 0; fi < frames; fi++)
                {
                    float fw = mesh.GetBlendShapeFrameWeight(si, fi);
                    var d  = new Vector3[vCount];
                    var dn = new Vector3[vCount];
                    var dt = new Vector3[vCount];
                    mesh.GetBlendShapeFrameVertices(si, fi, d, dn, dt);
                    if (si == targetIndex)
                    {
                        float ratio = Mathf.Approximately(lastFW, 0f) ? 1f : fw / lastFW;
                        for (int i = 0; i < vCount; i++)
                        {
                            d[i]  = totalDelta[i]   * ratio;
                            dn[i] = totalNormal[i]  * ratio;
                            dt[i] = totalTangent[i] * ratio;
                        }
                    }
                    newMesh.AddBlendShapeFrame(name, fw, d, dn, dt);
                }
            }
            return newMesh;
        }
        catch
        {
            Object.DestroyImmediate(newMesh);
            throw;
        }
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

        return SaveMesh(smr, BuildScaledMesh(mesh, shapeKeyIndex, scaleFactor), out error);
    }

    /// <summary>
    /// Creates a caller-owned mesh with every frame of one shape key scaled.
    /// Used by both the isolated preview and Apply so multi-frame keys render identically.
    /// Does not mutate the source mesh, renderer, scene, or any saved asset.
    /// </summary>
    public static Mesh BuildScaledMesh(Mesh source, int shapeKeyIndex, float scaleFactor)
    {
        if (source == null) throw new System.ArgumentNullException(nameof(source));
        if (shapeKeyIndex < 0 || shapeKeyIndex >= source.blendShapeCount)
            throw new System.ArgumentOutOfRangeException(nameof(shapeKeyIndex));
        if (float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor))
            throw new System.ArgumentOutOfRangeException(nameof(scaleFactor));

        int vCount = source.vertexCount;
        int shapeCount = source.blendShapeCount;

        // 모든 블렌드셰이프를 재구성 (수정 대상만 스케일 변경)
        var newMesh = BuildMeshBase(source);
        try
        {
            for (int si = 0; si < shapeCount; si++)
            {
                string name = source.GetBlendShapeName(si);
                int frames = source.GetBlendShapeFrameCount(si);
                for (int fi = 0; fi < frames; fi++)
                {
                    float fw = source.GetBlendShapeFrameWeight(si, fi);
                    var d  = new Vector3[vCount];
                    var dn = new Vector3[vCount];
                    var dt = new Vector3[vCount];
                    source.GetBlendShapeFrameVertices(si, fi, d, dn, dt);

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
            return newMesh;
        }
        catch
        {
            Object.DestroyImmediate(newMesh);
            throw;
        }
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
        // Preserve all vertex channels, variable bone influences, submesh topology,
        // and bounds. Rebuilding a short list of properties silently loses UV5–UV8
        // and newer skinning data on otherwise unrelated parts of the avatar.
        var m = Object.Instantiate(src);
        m.name = src.name;
        m.hideFlags = HideFlags.None;
        m.ClearBlendShapes();
        return m;
    }

    private static bool SaveMesh(SkinnedMeshRenderer smr, Mesh newMesh, out string error)
    {
        error = null;
        try
        {
            string[] folders = SAVE_FOLDER.Split('/');
            string parent = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string folder = parent + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(parent, folders[i]);
                parent = folder;
            }

            // Each edit gets a distinct mesh identity. In-place CopySerialized can
            // leave a bound renderer using old skinning data; name-based overwrite
            // also changes other avatars sharing that asset and cannot undo safely.
            string safeName = newMesh.name.Replace(" ", "_");
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');
            if (string.IsNullOrEmpty(safeName)) safeName = "Mesh";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{SAVE_FOLDER}/{safeName}_dine.asset");
            newMesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(newMesh, targetPath);
            if (!AssetDatabase.Contains(newMesh))
                throw new IOException(targetPath);
            AssetDatabase.SaveAssetIfDirty(newMesh);

            var originalMesh = smr.sharedMesh;
            var originalWeights = new float[originalMesh.blendShapeCount];
            for (int i = 0; i < originalWeights.Length; i++)
                originalWeights[i] = smr.GetBlendShapeWeight(i);
            Undo.RecordObject(smr, "DiNe 쉐이프키 수정");
            smr.sharedMesh = newMesh;
            for (int i = 0; i < originalWeights.Length; i++)
                smr.SetBlendShapeWeight(i, originalWeights[i]);
            if (PrefabUtility.IsPartOfPrefabInstance(smr))
                PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
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
