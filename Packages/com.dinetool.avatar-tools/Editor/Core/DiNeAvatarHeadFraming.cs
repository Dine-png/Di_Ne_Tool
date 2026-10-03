using UnityEngine;

/// <summary>Read-only face framing shared by avatar and animation editing.</summary>
public static class DiNeAvatarHeadFraming
{
    public static bool Compute(GameObject targetAvatarRoot, Renderer fallbackRenderer, out Vector3 focus, out Vector3 faceDir, out float headSize)
    {
        focus    = Vector3.zero;
        faceDir  = Vector3.forward;
        headSize = 0.25f;

        Transform head = null, leftEye = null, rightEye = null, leftArm = null, rightArm = null;
        Transform root = targetAvatarRoot != null && fallbackRenderer != null &&
                         fallbackRenderer.transform.IsChildOf(targetAvatarRoot.transform)
            ? targetAvatarRoot.transform : fallbackRenderer != null ? fallbackRenderer.transform.root : null;

        // 1) 휴머노이드 아바타가 있으면 본 이름 추측보다 우선한다.
        var animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        if (animator != null && animator.avatar != null && animator.isHuman)
        {
            head     = animator.GetBoneTransform(HumanBodyBones.Head);
            leftEye  = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            leftArm  = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        // 2) 이름 기반 매핑으로 보완 (표정 탭에서는 boneMapping 이 아직 비어 있을 수 있다)
        if (root != null && (head == null || leftEye == null || rightEye == null || leftArm == null || rightArm == null))
        {
            var previewBones = ArmatureScalerCore.AssignBoneMappings(root.gameObject);
            if (previewBones != null)
            {
                Transform t;
                if (head     == null && previewBones.TryGetValue(HumanBodyBones.Head,          out t)) head     = t;
                if (leftEye  == null && previewBones.TryGetValue(HumanBodyBones.LeftEye,       out t)) leftEye  = t;
                if (rightEye == null && previewBones.TryGetValue(HumanBodyBones.RightEye,      out t)) rightEye = t;
                if (leftArm  == null && previewBones.TryGetValue(HumanBodyBones.LeftUpperArm,  out t)) leftArm  = t;
                if (rightArm == null && previewBones.TryGetValue(HumanBodyBones.RightUpperArm, out t)) rightArm = t;
            }
        }

        // 3) 정면 방향: 좌우 대칭 본으로 오른쪽 축을 구해 계산한다.
        //    루트 오브젝트가 회전돼 있거나 루트가 아바타 상위 부모여도 정확하다.
        //    눈 → 팔 순서. 눈이 있으면 머리를 돌려 놓은 아바타도 얼굴 정면을 잡는다.
        Vector3 fwd = ForwardFromPair(leftEye, rightEye);
        if (fwd.sqrMagnitude < 1e-6f) fwd = ForwardFromPair(leftArm, rightArm);
        if (fwd.sqrMagnitude < 1e-6f)
        {
            Transform fb = root != null ? root : (fallbackRenderer != null ? fallbackRenderer.transform : null);
            if (fb != null)
            {
                Vector3 rf = fb.forward; rf.y = 0f;
                if (rf.sqrMagnitude > 1e-6f) fwd = rf.normalized;
            }
        }
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        faceDir = fwd.normalized;

        // 4) 머리 위치와 크기
        if (head != null)
        {
            Vector3 headPos = head.position;

            // 체형 비율 추정 (머리 본 높이의 약 16% ≒ 머리 높이)
            float est = 0.22f;
            if (root != null)
            {
                float h = headPos.y - root.position.y;
                if (h > 0.05f) est = h * 0.16f;
            }
            // 메쉬 바운드 상단으로 실측 후, 추정치 범위 안으로 제한한다.
            headSize = est;
            if (fallbackRenderer != null)
            {
                float measured = GetBounds(fallbackRenderer).max.y - headPos.y;
                if (measured > 0.01f)
                    headSize = Mathf.Clamp(measured, est * 0.6f, est * 2.5f);
            }
            headSize = Mathf.Max(0.05f, headSize);

            focus = headPos + Vector3.up * headSize * 0.5f;
            if (leftEye != null && rightEye != null)
                focus = (leftEye.position + rightEye.position) * 0.5f;
        }
        else if (fallbackRenderer != null)
        {
            var b = GetBounds(fallbackRenderer);
            headSize = Mathf.Max(0.05f, b.size.y * 0.16f);
            focus    = new Vector3(b.center.x, b.max.y - headSize * 0.5f, b.center.z);
        }
        else
        {
            return false;
        }

        return true;
    }

    public static Bounds GetBounds(Renderer renderer)
    {
        if (!(renderer is SkinnedMeshRenderer skin) || skin.sharedMesh == null)
            return renderer.bounds;

        var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            skin.BakeMesh(baked, false);
            Vector3[] vertices = baked.vertices;
            if (vertices.Length == 0) return renderer.bounds;
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            var bounds = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(toWorld.MultiplyPoint3x4(vertices[i]));
            return bounds;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(baked);
        }
    }

    private static Vector3 ForwardFromPair(Transform left, Transform right)
    {
        if (left == null || right == null) return Vector3.zero;
        Vector3 r = right.position - left.position;
        r.y = 0f;
        if (r.sqrMagnitude < 1e-6f) return Vector3.zero;
        return Vector3.Cross(r.normalized, Vector3.up).normalized;
    }
}
