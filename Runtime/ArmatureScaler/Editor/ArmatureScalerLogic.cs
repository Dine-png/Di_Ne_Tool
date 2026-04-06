using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public static class ArmatureScalerLogic
{
    public static void ApplyScale(
        GameObject targetAvatarRoot,
        Dictionary<HumanBodyBones, Vector3> scaleValues)
    {
        if (targetAvatarRoot == null) return;

        Dictionary<HumanBodyBones, Transform> boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
        
        foreach (var kvp in scaleValues)
        {
            ScaleBone(boneMapping, kvp.Key, kvp.Value);
        }
    }

    public static void ApplyRotation(
        GameObject targetAvatarRoot,
        Dictionary<HumanBodyBones, Quaternion> rotationValues)
    {
        if (targetAvatarRoot == null) return;

        Dictionary<HumanBodyBones, Transform> boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
        
        foreach (var kvp in rotationValues)
        {
            RotateBone(boneMapping, kvp.Key, kvp.Value);
        }
    }

    // 추가: 포지션 적용 메서드
    public static void ApplyPosition(
        GameObject targetAvatarRoot,
        Dictionary<HumanBodyBones, Vector3> positionValues)
    {
        if (targetAvatarRoot == null) return;

        Dictionary<HumanBodyBones, Transform> boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);

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