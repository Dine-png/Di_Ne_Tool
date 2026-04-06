using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public static class ArmatureScalerLogic
{
    public static void ApplyScale(
        Dictionary<HumanBodyBones, Transform> boneMapping,
        Dictionary<HumanBodyBones, Vector3> scaleValues)
    {
        if (boneMapping == null) return;
        foreach (var kvp in scaleValues)
        {
            ScaleBone(boneMapping, kvp.Key, kvp.Value);
        }
    }

    public static void ApplyRotation(
        Dictionary<HumanBodyBones, Transform> boneMapping,
        Dictionary<HumanBodyBones, Quaternion> rotationValues)
    {
        if (boneMapping == null) return;
        foreach (var kvp in rotationValues)
        {
            RotateBone(boneMapping, kvp.Key, kvp.Value);
        }
    }

    public static void ApplyPosition(
        Dictionary<HumanBodyBones, Transform> boneMapping,
        Dictionary<HumanBodyBones, Vector3> positionValues)
    {
        if (boneMapping == null) return;
        foreach (var kvp in positionValues)
        {
            MoveBone(boneMapping, kvp.Key, kvp.Value);
        }
    }

    private static void ScaleBone(Dictionary<HumanBodyBones, Transform> boneMapping, HumanBodyBones bone, Vector3 scale)
    {
        if (boneMapping.TryGetValue(bone, out Transform boneTransform))
        {
            Undo.RecordObject(boneTransform, "Scale Bone");
            boneTransform.localScale = scale;
        }
    }

    private static void RotateBone(Dictionary<HumanBodyBones, Transform> boneMapping, HumanBodyBones bone, Quaternion rotation)
    {
        if (boneMapping.TryGetValue(bone, out Transform boneTransform))
        {
            Undo.RecordObject(boneTransform, "Rotate Bone");
            boneTransform.localRotation = rotation;
        }
    }

    // 추가: 포지션 적용 private 메서드
    private static void MoveBone(Dictionary<HumanBodyBones, Transform> boneMapping, HumanBodyBones bone, Vector3 position)
    {
        if (boneMapping.TryGetValue(bone, out Transform boneTransform))
        {
            Undo.RecordObject(boneTransform, "Move Bone");
            boneTransform.localPosition = position;
        }
    }
}