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
        new[] {"Hips", "Hip", "pelvis", "Pelvis_Root", "Waist", "Koshi", "Hips_Root", "Root_Hips"},
        new[] {"LeftUpperLeg", "UpperLeg_Left", "UpperLeg_L", "Leg_Left", "Leg_L", "ULeg_L", "Left leg", "LeftUpLeg", "UpLeg.L", "Thigh_L",
               "UpperLeg.L", "Thigh.L", "Thigh_Left", "L_Thigh", "L_UpperLeg", "L_Leg", "LegUpper_L", "Femur_L", "Femur.L", "UpLeg_L"},
        new[] {"RightUpperLeg", "UpperLeg_Right", "UpperLeg_R", "Leg_Right", "Leg_R", "ULeg_R", "Right leg", "RightUpLeg", "UpLeg.R", "Thigh_R",
               "UpperLeg.R", "Thigh.R", "Thigh_Right", "R_Thigh", "R_UpperLeg", "R_Leg", "LegUpper_R", "Femur_R", "Femur.R", "UpLeg_R"},
        new[] {"LeftLowerLeg", "LowerLeg_Left", "LowerLeg_L", "Knee_Left", "Knee_L", "LLeg_L", "Left knee", "LeftLeg", "leg_L", "shin.L",
               "LowerLeg.L", "Shin_L", "Shin_Left", "Calf_L", "Calf.L", "Calf_Left", "L_Calf", "L_LowerLeg", "L_Knee", "LegLower_L", "Knee.L"},
        new[] {"RightLowerLeg", "LowerLeg_Right", "LowerLeg_R", "Knee_Right", "Knee_R", "LLeg_R", "Right knee", "RightLeg", "leg_R", "shin.R",
               "LowerLeg.R", "Shin_R", "Shin_Right", "Calf_R", "Calf.R", "Calf_Right", "R_Calf", "R_LowerLeg", "R_Knee", "LegLower_R", "Knee.R"},
        new[] {"LeftFoot", "Foot_Left", "Foot_L", "Ankle_L", "Foot.L.001", "Left ankle", "heel.L", "heel",
               "Foot.L", "Ankle.L", "Ankle_Left", "L_Foot", "L_Ankle", "LeftAnkle", "Left_Foot", "Left foot"},
        new[] {"RightFoot", "Foot_Right", "Foot_R", "Ankle_R", "Foot.R.001", "Right ankle", "heel.R", "heel",
               "Foot.R", "Ankle.R", "Ankle_Right", "R_Foot", "R_Ankle", "RightAnkle", "Right_Foot", "Right foot"},
        new[] {"Spine", "spine01", "Spine_01", "LowerBack", "Lower_Back", "Torso", "Abdomen", "Stomach", "Belly", "Spine_Lower"},
        new[] {"Chest", "Bust", "spine02", "upper_chest", "Chest_01", "Spine_02", "UpperBody", "Upper_Body", "Ribcage", "Rib", "Thorax", "Spine_Upper", "Mune"},
        new[] {"Neck", "neck", "Neck_01", "neck_01", "Neck01", "neck01", "Neck1", "neck1", "NeckBone", "neckbone", "neck_bone", "Neck_Bone"},
        new[] {"Head", "Head_01", "Atama", "Skull", "Head_Root", "HeadRoot"},
        new[] {"LeftShoulder", "Shoulder_Left", "Shoulder_L", "Shoulder.L", "shoulder_L", "shoulder.l", "Left_Shoulder", "Left Shoulder",
               "Clavicle_L", "clavicle_L", "Clavicle.L", "clavicle.l", "clavicle_left", "Clavicle_Left",
               "clav_L", "Clav_L", "clav.l", "Clav.L", "Clav_Left",
               "CollarBone_L", "Collarbone_L", "collar_L", "collar_l", "collar.l", "Collar_L", "Collar_Left",
               "ShoulderBone_L", "shoulder_bone_l", "L_Shoulder", "L_Clavicle", "L_Collar",
               "LeftCollar", "Left_Collar", "LeftClavicle", "Left_Clavicle"},
        new[] {"RightShoulder", "Shoulder_Right", "Shoulder_R", "Shoulder.R", "shoulder_R", "shoulder.r", "Right_Shoulder", "Right Shoulder",
               "Clavicle_R", "clavicle_R", "Clavicle.R", "clavicle.r", "clavicle_right", "Clavicle_Right",
               "clav_R", "Clav_R", "clav.r", "Clav.R", "Clav_Right",
               "CollarBone_R", "Collarbone_R", "collar_R", "collar_r", "collar.r", "Collar_R", "Collar_Right",
               "ShoulderBone_R", "shoulder_bone_r", "R_Shoulder", "R_Clavicle", "R_Collar",
               "RightCollar", "Right_Collar", "RightClavicle", "Right_Clavicle"},
        new[] {"LeftUpperArm", "UpperArm_Left", "UpperArm_L", "Arm_Left", "Arm_L", "UArm_L", "Left arm", "UpperLeftArm",
               "UpperArm.L", "Arm.L", "UpArm_L", "Bicep_L", "Bicep.L", "L_UpperArm", "L_Arm", "ArmUpper_L", "Humerus_L", "Humerus.L"},
        new[] {"RightUpperArm", "UpperArm_Right", "UpperArm_R", "Arm_Right", "Arm_R", "UArm_R", "Right arm", "UpperRightArm",
               "UpperArm.R", "Arm.R", "UpArm_R", "Bicep_R", "Bicep.R", "R_UpperArm", "R_Arm", "ArmUpper_R", "Humerus_R", "Humerus.R"},
        new[] {"LeftLowerArm", "LowerArm_Left", "LowerArm_L", "LArm_L", "Left elbow", "LeftForeArm", "Elbow_L", "forearm_L", "ForArm_L",
               "LowerArm.L", "forearm.L", "ForeArm_L", "ForeArm.L", "Elbow.L", "L_LowerArm", "L_ForeArm", "L_Elbow", "ArmLower_L", "Ulna_L"},
        new[] {"RightLowerArm", "LowerArm_Right", "LowerArm_R", "LArm_R", "Right elbow", "RightForeArm", "Elbow_R", "forearm_R", "ForArm_R",
               "LowerArm.R", "forearm.R", "ForeArm_R", "ForeArm.R", "Elbow.R", "R_LowerArm", "R_ForeArm", "R_Elbow", "ArmLower_R", "Ulna_R"},
        new[] {"LeftHand", "Hand_Left", "Hand_L", "Left wrist", "Wrist_L",
               "Hand.L", "Wrist.L", "Wrist_Left", "L_Hand", "L_Wrist", "LeftWrist", "Left_Hand", "Left hand", "Palm_L"},
        new[] {"RightHand", "Hand_Right", "Hand_R", "Right wrist", "Wrist_R",
               "Hand.R", "Wrist.R", "Wrist_Right", "R_Hand", "R_Wrist", "RightWrist", "Right_Hand", "Right hand", "Palm_R"},
        new[] {"LeftToes", "Toes_Left", "Toe_Left", "ToeIK_L", "Toes_L", "Toe_L", "Foot.L.002", "Left Toe", "LeftToeBase",
               "Toe.L", "Toes.L", "ToeBase_L", "ToeBase.L", "L_Toe", "L_Toes", "Left_Toe", "Ball_L", "Ball.L"},
        new[] {"RightToes", "Toes_Right", "Toe_Right", "ToeIK_R", "Toes_R", "Toe_R", "Foot.R.002", "Right Toe", "RightToeBase",
               "Toe.R", "Toes.R", "ToeBase_R", "ToeBase.R", "R_Toe", "R_Toes", "Right_Toe", "Ball_R", "Ball.R"},
        new[] {"LeftEye", "Eye_Left", "Eye_L", "Eye.L", "eye.l", "L_Eye", "Left_Eye", "Left eye", "EyeBall_L", "Eyeball.L", "Eye_Bone_L"},
        new[] {"RightEye", "Eye_Right", "Eye_R", "Eye.R", "eye.r", "R_Eye", "Right_Eye", "Right eye", "EyeBall_R", "Eyeball.R", "Eye_Bone_R"},
        new[] {"Jaw", "Chin", "Mouth", "LowerJaw", "Jaw_Root"},
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
        new[] {"LeftRingIntermediate", "IntermediateRing_Left", "IntermediateRing_L", "Ring2_L", "RingFinger2_L", "LeftHandRing2", "Ring Intermediate.L", "finger04_02_L", "f_ring.02.l"},
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
        new[] {"UpperChest", "UChest", "Upper_Chest", "Chest_Upper", "Spine03", "Spine_03", "Chest2", "UpperTorso"}
    };
    
    // 접두사 바리에이션 제거용
    // mixamorig:Hips / Bip01 Spine / J_Bip_C_Head(VRM) / cf_j_hips(코이카츠) / DEF-spine(Rigify) / Bone_Chest ...
    private static readonly Regex PAT_STRIP_PREFIX = new Regex(
        @"^(?:mixamorig[0-9]*[:_\.\- ]?" +
        @"|bip(?:ed)?[0-9]*[:_\.\- ]" +
        @"|j[_\.\-](?:bip|sec|adj|opt|col)[_\.\-]" +
        @"|(?:bone|def|org|mch|mf|mfb|rig|skin|bind|jnt|joint|jt|cf|cm|b|j)[_\.\-])+",
        RegexOptions.IgnoreCase);

    // 접미사 바리에이션 제거용
    // Chest_MFBase / Shoulder.R_MFBase / NeckBone / Hand_L_jnt / Spine_bind / Head-dummy ...
    private static readonly Regex PAT_STRIP_SUFFIX = new Regex(
        @"(?:[_\.\- ]?(?:mfbase|mfbone|mfb|mf|base|bone|jnt|joint|bind|skin|def|org|mch|dummy|helper|proxy|null|grp|group|ref|rig)[0-9]*)+$",
        RegexOptions.IgnoreCase);

    // 이름 중간에 끼어드는 토큰 제거용 (Chest_MFBase_L 처럼 좌우 표기 앞에 붙는 경우)
    private static readonly Regex PAT_STRIP_INNER = new Regex(
        @"[_\.\- ](?:mfbase|mfbone|dummy|proxy)(?=[_\.\- ])",
        RegexOptions.IgnoreCase);

    private static string NormalizeName(string name)
    {
        if (name == null) return null;

        name = name.Trim().ToLowerInvariant();

        // 접두사/접미사 바리에이션을 먼저 걷어낸 뒤(구분자가 살아있을 때) 나머지를 정리한다.
        string stripped = PAT_STRIP_INNER.Replace(name, "");
        stripped = PAT_STRIP_PREFIX.Replace(stripped, "");
        stripped = PAT_STRIP_SUFFIX.Replace(stripped, "");

        string result = Regex.Replace(stripped, @"[0-9 _.:'|/\\-]", "");

        // 전부 깎여나간 경우(예: 이름이 그냥 "Bone")에는 원본 기준으로 되돌린다.
        if (result.Length == 0)
            result = Regex.Replace(name, @"[0-9 _.:'|/\\-]", "");

        return result;
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
        if (string.IsNullOrEmpty(name)) return;

        if (!map.TryGetValue(name, out var list))
        {
            list = new List<HumanBodyBones>();
            map[name] = list;
        }
        // 바리에이션이 늘면서 같은 본이 중복 등록될 수 있으므로 한 번만 담는다.
        if (!list.Contains(bone))
            list.Add(bone);
    }

    // 헬퍼 함수: 패턴 목록을 순회하며 자식 본 찾기
    // 이름을 정규화해서 비교하므로 _MFBase 같은 접미사나 대소문자/구분자 차이도 잡아낸다.
    private static Transform FindChildByPatterns(Transform parent, string[] patterns)
    {
        if (parent == null) return null;

        var normalizedPatterns = new HashSet<string>();
        foreach (string pattern in patterns)
        {
            var n = NormalizeName(pattern);
            if (!string.IsNullOrEmpty(n)) normalizedPatterns.Add(n);
        }

        // 직계 자식 우선, 없으면 손자 세대까지 확인
        Transform found = FindInChildren(parent, normalizedPatterns);
        if (found != null) return found;

        for (int i = 0; i < parent.childCount; i++)
        {
            found = FindInChildren(parent.GetChild(i), normalizedPatterns);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindInChildren(Transform parent, HashSet<string> normalizedPatterns)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (normalizedPatterns.Contains(NormalizeName(child.name)))
                return child;
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

        // --- 여기서부터 요청하신 확장된 패턴 적용 ---

        // 1. 패턴 정의 (요청하신 Hips 패턴 포함)
        string[] leftBreastPatterns = new[] {
            "LeftBreast", "Breast_L", "Breast L", "Breast.L", "Breast_l", "breast_L", "breast.l", "breast_l",
            "leftbreast", "left_breast", "Left_Breast", "Left breast", "Breasts_L", "Breasts_l",
            "Breast_root_L", "Breast_Root_L", "BreastRoot_L", // 루트 본 바리에이션
            "L_Breast", "L.Breast", "Bust_L", "Bust.L", "Boob_L", "Boobs_L", "Chest_L", "Chest.L",
            "Mune_L", "Mune.L", "Oppai_L", "Bre_L", "Breast_01_L", "Breast1_L", "BreastUpper_L"
        };
        string[] rightBreastPatterns = new[] {
            "RightBreast", "Breast_R", "Breast R", "Breast.R", "Breast_r", "breast.r", "breast_R", "breast_r",
            "rightbreast", "right_breast", "Right_Breast", "Right breast", "Breasts_R", "Breasts_r",
            "Breast_root_R", "Breast_Root_R", "BreastRoot_R", // 루트 본 바리에이션
            "R_Breast", "R.Breast", "Bust_R", "Bust.R", "Boob_R", "Boobs_R", "Chest_R", "Chest.R",
            "Mune_R", "Mune.R", "Oppai_R", "Bre_R", "Breast_01_R", "Breast1_R", "BreastUpper_R"
        };

        string[] leftButtPatterns = new[]
        {
            "Butt_L", "Butt L", "butt.l", "butt_l", "leftbutt", "left_butt", "Left_Butt", "Left butt", "Butts_L", "Butts_l",
            "Hips_L", "Hips-L", "Hips L", "HipsL",
            "Butt_Root_L", "Butt_root_L", "ButtRoot_L", // 루트 본 바리에이션
            "L_Butt", "L.Butt", "Butt.L", "Hips.L", "Hip_L", "Hip.L", "Ass_L", "Ass.L",
            "Buttock_L", "Buttock.L", "Buttocks_L", "Glute_L", "Glutes_L", "Cheek_L", "Oshiri_L", "Siri_L"
        };

        string[] rightButtPatterns = new[]
        {
            "Butt_R", "Butt R", "butt.r", "butt_r", "rightbutt", "right_butt", "Right_Butt", "Right butt", "Butts_R", "Butts_r",
            "Hips_R", "Hips-R", "Hips R", "HipsR",
            "Butt_Root_R", "Butt_root_R", "ButtRoot_R", // 루트 본 바리에이션
            "R_Butt", "R.Butt", "Butt.R", "Hips.R", "Hip_R", "Hip.R", "Ass_R", "Ass.R",
            "Buttock_R", "Buttock.R", "Buttocks_R", "Glute_R", "Glutes_R", "Cheek_R", "Oshiri_R", "Siri_R"
        };

        // 2. 탐색 기준 본 결정
        // 가슴 본은 Chest 아래가 일반적이지만 UpperChest/Spine 아래에 달린 아바타도 있어 순서대로 확인한다.
        Transform chestTransform = null;
        foreach (var key in new[] { HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine })
        {
            if (!boneMapping.TryGetValue(key, out var candidate) || candidate == null) continue;
            if (FindChildByPatterns(candidate, leftBreastPatterns) != null ||
                FindChildByPatterns(candidate, rightBreastPatterns) != null)
            {
                chestTransform = candidate;
                break;
            }
        }
        if (chestTransform == null)
            chestTransform = boneMapping.ContainsKey(HumanBodyBones.Chest) ? boneMapping[HumanBodyBones.Chest] : null;

        Transform hipsTransform = boneMapping.ContainsKey(HumanBodyBones.Hips) ? boneMapping[HumanBodyBones.Hips] : null;

        // 3. 가슴 (Chest 자식에서 찾기)
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

        // 4. 엉덩이 (Hips 자식에서 찾기)
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
