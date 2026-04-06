using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Immutable;

public static class ArmatureScalerCore
{
    private static readonly Regex PAT_END_NUMBER = new Regex(@"[_\.][0-9]+");

    // 상단의 정적 패턴 배열 (참고용 및 표준 본 매핑용)
    private static string[][] boneNamePatterns = new[]
    {
        new[] {"Hips", "Hip", "pelvis"},
        new[] {"LeftUpperLeg", "UpperLeg_Left", "UpperLeg_L", "Leg_Left", "Leg_L", "ULeg_L", "Left leg", "LeftUpLeg", "UpLeg.L", "Thigh_L"},
        new[] {"RightUpperLeg", "UpperLeg_Right", "UpperLeg_R", "Leg_Right", "Leg_R", "ULeg_R", "Right leg", "RightUpLeg", "UpLeg.R", "Thigh_R"},
        new[] {"LeftLowerLeg", "LowerLeg_Left", "LowerLeg_L", "Knee_Left", "Knee_L", "LLeg_L", "Left knee", "LeftLeg", "leg_L", "shin.L"},
        new[] {"RightLowerLeg", "LowerLeg_Right", "LowerLeg_R", "Knee_Right", "Knee_R", "LLeg_R", "Right knee", "RightLeg", "leg_R", "shin.R"},
        new[] {"LeftFoot", "Foot_Left", "Foot_L", "Ankle_L", "Foot.L.001", "Left ankle", "heel.L", "heel"},
        new[] {"RightFoot", "Foot_Right", "Foot_R", "Ankle_R", "Foot.R.001", "Right ankle", "heel.R", "heel"},
        new[] {"Spine", "spine01"},
        new[] {"Chest", "Bust", "spine02", "upper_chest"},
        new[] {"Neck"},
        new[] {"Head"},
        new[] {"LeftShoulder", "Shoulder_Left", "Shoulder_L"},
        new[] {"RightShoulder", "Shoulder_Right", "Shoulder_R"},
        new[] {"LeftUpperArm", "UpperArm_Left", "UpperArm_L", "Arm_Left", "Arm_L", "UArm_L", "Left arm", "UpperLeftArm"},
        new[] {"RightUpperArm", "UpperArm_Right", "UpperArm_R", "Arm_Right", "Arm_R", "UArm_R", "Right arm", "UpperRightArm"},
        new[] {"LeftLowerArm", "LowerArm_Left", "LowerArm_L", "LArm_L", "Left elbow", "LeftForeArm", "Elbow_L", "forearm_L", "ForArm_L"},
        new[] {"RightLowerArm", "LowerArm_Right", "LowerArm_R", "LArm_R", "Right elbow", "RightForeArm", "Elbow_R", "forearm_R", "ForArm_R"},
        new[] {"LeftHand", "Hand_Left", "Hand_L", "Left wrist", "Wrist_L"},
        new[] {"RightHand", "Hand_Right", "Hand_R", "Right wrist", "Wrist_R"},
        new[] {"LeftToes", "Toes_Left", "Toe_Left", "ToeIK_L", "Toes_L", "Toe_L", "Foot.L.002", "Left Toe", "LeftToeBase"},
        new[] {"RightToes", "Toes_Right", "Toe_Right", "ToeIK_R", "Toes_R", "Toe_R", "Foot.R.002", "Right Toe", "RightToeBase"},
        new[] {"LeftEye", "Eye_Left", "Eye_L"},
        new[] {"RightEye", "Eye_Right", "Eye_R"},
        new[] {"Jaw"},
        new[] {"LeftThumbProximal", "ProximalThumb_Left", "ProximalThumb_L", "Thumb1_L", "ThumbFinger1_L", "LeftHandThumb1", "Thumb Proximal.L", "Thunb1_L", "finger01_01_L"},
        new[] {"LeftThumbIntermediate", "IntermediateThumb_Left", "IntermediateThumb_L", "Thumb2_L", "ThumbFinger2_L", "LeftHandThumb2", "Thumb Intermediate.L", "Thunb2_L", "finger01_02_L"},
        new[] {"LeftThumbDistal", "DistalThumb_Left", "DistalThumb_L", "Thumb3_L", "ThumbFinger3_L", "LeftHandThumb3", "Thumb Distal.L", "Thunb3_L", "finger01_03_L"},
        new[] {"LeftIndexProximal", "ProximalIndex_Left", "ProximalIndex_L", "Index1_L", "IndexFinger1_L", "LeftHandIndex1", "Index Proximal.L", "finger02_01_L", "f_index.01.L"},
        new[] {"LeftIndexIntermediate", "IntermediateIndex_Left", "IntermediateIndex_L", "Index2_L", "IndexFinger2_L", "LeftHandIndex2", "Index Intermediate.L", "finger02_02_L", "f_index.02.L"},
        new[] {"LeftIndexDistal", "DistalIndex_Left", "DistalIndex_L", "Index3_L", "IndexFinger3_L", "LeftHandIndex3", "Index Distal.L", "finger02_03_L", "f_index.03.L"},
        new[] {"LeftMiddleProximal", "ProximalMiddle_Left", "ProximalMiddle_L", "Middle1_L", "MiddleFinger1_L", "LeftHandMiddle1", "Middle Proximal.L", "finger03_01_L", "f_middle.01.L"},
        new[] {"LeftMiddleIntermediate", "IntermediateMiddle_Left", "IntermediateMiddle_L", "Middle2_L", "MiddleFinger2_L", "LeftHandMiddle2", "Middle Intermediate.L", "finger03_02_L", "f_middle.02.L"},
        new[] {"LeftMiddleDistal", "DistalMiddle_Left", "DistalMiddle_L", "Middle3_L", "MiddleFinger3_L", "LeftHandMiddle3", "Middle Distal.L", "finger03_03_L", "f_middle.03.L"},
        new[] {"LeftRingProximal", "ProximalRing_Left", "ProximalRing_L", "Ring1_L", "RingFinger1_L", "LeftHandRing1", "Ring Proximal.L", "finger04_01_L", "f_ring.01.L"},
        new[] {"LeftRingIntermediate", "IntermediateRing_Left", "IntermediateRing_L", "Ring2_L", "RingFinger2_L", "LeftHandRing2", "Ring Intermediate.L", "finger04_02_R", "f_ring.02.l"},
        new[] {"LeftRingDistal", "DistalRing_Left", "DistalRing_L", "Ring3_L", "RingFinger3_L", "LeftHandRing3", "Ring Distal.L", "finger04_03_L", "f_ring.03.l"},
        new[] {"LeftLittleProximal", "ProximalLittle_Left", "ProximalLittle_L", "Little1_L", "LittleFinger1_L", "LeftHandPinky1", "Little Proximal.L", "finger05_01_L", "f_pinky.01.L"},
        new[] {"LeftLittleIntermediate", "IntermediateLittle_Left", "IntermediateLittle_L", "Little2_L", "LittleFinger2_L", "LeftHandPinky2", "Little Intermediate.L", "finger05_02_L", "f_pinky.02.L"},
        new[] {"LeftLittleDistal", "DistalLittle_Left", "DistalLittle_L", "Little3_L", "LittleFinger3_L", "LeftHandPinky3", "Little Distal.L", "finger05_03_L", "f_pinky.03.L"},
        new[] {"RightThumbProximal", "ProximalThumb_Right", "ProximalThumb_R", "Thumb1_R", "ThumbFinger1_R", "RightHandThumb1", "Thumb Proximal.R", "Thunb1_R", "finger01_01_R"},
        new[] {"RightThumbIntermediate", "IntermediateThumb_Right", "IntermediateThumb_R", "Thumb2_R", "ThumbFinger2_R", "RightHandThumb2", "Thumb Intermediate.R", "Thunb2_R", "finger01_02_R"},
        new[] {"RightThumbDistal", "DistalThumb_Right", "DistalThumb_R", "Thumb3_R", "ThumbFinger3_R", "RightHandThumb3", "Thumb Distal.R", "Thunb3_R", "finger01_03_R"},
        new[] {"RightIndexProximal", "ProximalIndex_Right", "ProximalIndex_R", "Index1_R", "IndexFinger1_R", "RightHandIndex1", "Index Proximal.R", "finger02_01_R", "f_index.01.R"},
        new[] {"RightIndexIntermediate", "IntermediateIndex_Right", "IntermediateIndex_R", "Index2_R", "IndexFinger2_R", "RightHandIndex2", "Index Intermediate.R", "finger02_02_R", "f_index.02.R"},
        new[] {"RightIndexDistal", "DistalIndex_Right", "DistalIndex_R", "Index3_R", "IndexFinger3_R", "RightHandIndex3", "Index Distal.R", "finger02_03_R", "f_index.03.R"},
        new[] {"RightMiddleProximal", "ProximalMiddle_Right", "ProximalMiddle_R", "Middle1_R", "MiddleFinger1_R", "RightHandMiddle1", "Middle Proximal.R", "finger03_01_R", "f_middle.01.R"},
        new[] {"RightMiddleIntermediate", "IntermediateMiddle_Right", "IntermediateMiddle_R", "Middle2_R", "MiddleFinger2_R", "RightHandMiddle2", "Middle Intermediate.R", "finger03_02_R", "f_middle.02.R"},
        new[] {"RightMiddleDistal", "DistalMiddle_Right", "DistalMiddle_R", "Middle3_R", "MiddleFinger3_R", "RightHandMiddle3", "Middle Distal.R", "finger03_03_R", "f_middle.03.R"},
        new[] {"RightRingProximal", "ProximalRing_Right", "ProximalRing_R", "Ring1_R", "RingFinger1_R", "RightHandRing1", "Ring Proximal.R", "finger04_01_R", "f_ring.01.R"},
        new[] {"RightRingIntermediate", "IntermediateRing_Right", "IntermediateRing_R", "Ring2_R", "RingFinger2_R", "RightHandRing2", "Ring Intermediate.R", "finger04_02_R", "f_ring.02.R"},
        new[] {"RightRingDistal", "DistalRing_Right", "DistalRing_R", "Ring3_R", "RingFinger3_R", "RightHandRing3", "Ring Distal.R", "finger04_03_R", "f_ring.03.R"},
        new[] {"RightLittleProximal", "ProximalLittle_Right", "ProximalLittle_R", "Little1_R", "LittleFinger1_R", "RightHandPinky1", "Little Proximal.R", "finger05_01_R", "f_pinky.01.R"},
        new[] {"RightLittleIntermediate", "IntermediateLittle_Right", "IntermediateLittle_R", "Little2_R", "LittleFinger2_R", "RightHandPinky2", "Little Intermediate.R", "finger05_02_R", "f_pinky.02.R"},
        new[] {"RightLittleDistal", "DistalLittle_Right", "DistalLittle_R", "Little3_R", "LittleFinger3_R", "RightHandPinky3", "Little Distal.R", "finger05_03_R", "f_pinky.03.R"},
        new[] {"UpperChest", "UChest"}
    };
    
    private static string NormalizeName(string name)
    {
        if (name == null) return null;
        name = name.ToLowerInvariant();
        name = Regex.Replace(name, @"^bone_|[0-9 _.-]", "");
        return name;
    }

    private static readonly ImmutableDictionary<string, List<HumanBodyBones>> NameToBoneMap;

    static ArmatureScalerCore()
    {
        var pat_end_side = new Regex(@"[_\.]([LR])$");
        var nameToBoneMap = new Dictionary<string, List<HumanBodyBones>>();

        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var bone = (HumanBodyBones)i;
            if (i < boneNamePatterns.Length && boneNamePatterns[i] != null)
            {
                foreach (var name in boneNamePatterns[i])
                {
                    RegisterNameForBone(NormalizeName(name), bone, nameToBoneMap);
                    var match = pat_end_side.Match(name);
                    if (match.Success)
                    {
                        var altName = name.Substring(0, name.Length - 2);
                        altName = match.Groups[1] + "." + altName;
                        RegisterNameForBone(NormalizeName(altName), bone, nameToBoneMap);
                    }
                    else
                    {
                        var altName = "C." + name;
                        RegisterNameForBone(NormalizeName(altName), bone, nameToBoneMap);
                    }
                }
            }
        }
        
        NameToBoneMap = nameToBoneMap.ToImmutableDictionary();
    }
    
    private static void RegisterNameForBone(string name, HumanBodyBones bone, Dictionary<string, List<HumanBodyBones>> map)
    {
        if (!map.TryGetValue(name, out var list))
        {
            list = new List<HumanBodyBones>();
            map[name] = list;
        }
        list.Add(bone);
    }

    // 헬퍼 함수: 패턴 목록을 순회하며 자식 본 찾기
    private static Transform FindChildByPatterns(Transform parent, string[] patterns)
    {
        if (parent == null) return null;

        foreach (string pattern in patterns)
        {
            Transform t = parent.Find(pattern);
            if (t != null)
            {
                return t;
            }
        }
        return null;
    }

    public static Dictionary<HumanBodyBones, Transform> AssignBoneMappings(GameObject avatar)
    {
        Dictionary<HumanBodyBones, Transform> boneMapping = new Dictionary<HumanBodyBones, Transform>();
        
        Transform[] allTransforms = avatar.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform t in allTransforms)
        {
            var normalizedName = NormalizeName(t.name);
            if (NameToBoneMap.TryGetValue(normalizedName, out List<HumanBodyBones> bones))
            {
                foreach (var bone in bones)
                {
                    if (!boneMapping.ContainsKey(bone))
                    {
                        boneMapping[bone] = t;
                        break;
                    }
                }
            }
        }

        Transform chestTransform = boneMapping.ContainsKey(HumanBodyBones.Chest) ? boneMapping[HumanBodyBones.Chest] : null;
        Transform hipsTransform = boneMapping.ContainsKey(HumanBodyBones.Hips) ? boneMapping[HumanBodyBones.Hips] : null;

        // --- 여기서부터 요청하신 확장된 패턴 적용 ---
        
        // 1. 패턴 정의 (요청하신 Hips 패턴 포함)
        string[] leftBreastPatterns = new[] {
            "LeftBreast", "Breast_L", "Breast L", "Breast.L", "Breast_l", "breast_L", "breast.l", "breast_l",
            "leftbreast", "left_breast", "Left_Breast", "Left breast", "Breasts_L", "Breasts_l",
            "Breast_root_L", "Breast_Root_L", "BreastRoot_L" // 루트 본 바리에이션
        };
        string[] rightBreastPatterns = new[] {
            "RightBreast", "Breast_R", "Breast R", "Breast.R", "Breast_r", "breast.r", "breast_R", "breast_r",
            "rightbreast", "right_breast", "Right_Breast", "Right breast", "Breasts_R", "Breasts_r",
            "Breast_root_R", "Breast_Root_R", "BreastRoot_R" // 루트 본 바리에이션
        };

        string[] leftButtPatterns = new[]
        {
            "Butt_L", "Butt L", "butt.l", "butt_l", "leftbutt", "left_butt", "Left_Butt", "Left butt", "Butts_L", "Butts_l",
            "Hips_L", "Hips-L", "Hips L", "HipsL",
            "Butt_Root_L", "Butt_root_L", "ButtRoot_L" // 루트 본 바리에이션
        };

        string[] rightButtPatterns = new[]
        {
            "Butt_R", "Butt R", "butt.r", "butt_r", "rightbutt", "right_butt", "Right_Butt", "Right butt", "Butts_R", "Butts_r",
            "Hips_R", "Hips-R", "Hips R", "HipsR",
            "Butt_Root_R", "Butt_root_R", "ButtRoot_R" // 루트 본 바리에이션
        };

        // 2. 가슴 (Chest 자식에서 찾기)
        if (chestTransform != null)
        {
            Transform leftBreast = FindChildByPatterns(chestTransform, leftBreastPatterns);
            if (leftBreast != null)
            {
                boneMapping[(HumanBodyBones)100] = leftBreast;
            }

            Transform rightBreast = FindChildByPatterns(chestTransform, rightBreastPatterns);
            if (rightBreast != null)
            {
                boneMapping[(HumanBodyBones)101] = rightBreast;
            }
        }

        // 3. 엉덩이 (Hips 자식에서 찾기)
        if (hipsTransform != null)
        {
            Transform leftButt = FindChildByPatterns(hipsTransform, leftButtPatterns);
            if (leftButt != null)
            {
                boneMapping[(HumanBodyBones)102] = leftButt;
            }

            Transform rightButt = FindChildByPatterns(hipsTransform, rightButtPatterns);
            if (rightButt != null)
            {
                boneMapping[(HumanBodyBones)103] = rightButt;
            }
        }

        return boneMapping;
    }
}