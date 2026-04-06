using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ArmatureScalerEditor : EditorWindow
{
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    private GameObject targetAvatarRoot;
    
    private HumanoidBodyPart selectedPart = HumanoidBodyPart.None;
    
    private Dictionary<HumanoidBodyPart, Vector3> scaleValues = new Dictionary<HumanoidBodyPart, Vector3>();
    private Dictionary<HumanoidBodyPart, Quaternion> rotationValues = new Dictionary<HumanoidBodyPart, Quaternion>();
    // 추가: 포지션 값 딕셔너리
    private Dictionary<HumanoidBodyPart, Vector3> positionValues = new Dictionary<HumanoidBodyPart, Vector3>();
    
    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Vector2 scrollPosition; 
    
    private Dictionary<HumanBodyBones, Transform> boneMapping;
    
    private Vector3 lastKnownScale = Vector3.one;
    private Quaternion lastKnownRotation = Quaternion.identity;
    private Vector3 lastKnownPosition = Vector3.zero; // 추가
    
    private string[] presetFiles;
    private int selectedPresetIndex = -1;
    private string selectedPresetName = "";
    private Vector2 presetScrollPosition;

    private enum HumanoidBodyPart
    {
        None,
        Head, Spine, Torso, Hips,
        LeftArm, LeftLowerArm, LeftHand,
        RightArm, RightLowerArm, RightHand,
        LeftLeg, LeftLowerLeg, LeftFoot,
        RightLeg, RightLowerLeg, RightFoot,
        LeftBreast, RightBreast, LeftButt, RightButt
    }

    [MenuItem("DiNe/Armature Scaler")]
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow<ArmatureScalerEditor>("Armature Scaler");
        window.minSize = new Vector2(300, 400); 
        window.position = new Rect(window.position.y, window.position.y, 420, 850); 
    }

    void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Di Ne/Assets/DiNe.png");
        SetLanguage(language);
        InitializeValues(); // 이름 변경됨 (InitializeScaleValues -> InitializeValues)
        if (targetAvatarRoot != null)
        {
            boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
            LoadCurrentValues();
        }
        
        RefreshPresetList();
        
        EditorApplication.update += OnEditorUpdate;
    }
    
    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (selectedPart == HumanoidBodyPart.None || targetAvatarRoot == null || boneMapping == null)
        {
            return;
        }

        HumanBodyBones boneType = GetBoneType(selectedPart);
        if (boneMapping.TryGetValue(boneType, out Transform boneTransform))
        {
            // 스케일 감지
            if (boneTransform.localScale != lastKnownScale)
            {
                scaleValues[selectedPart] = boneTransform.localScale;
                lastKnownScale = boneTransform.localScale;
                Repaint();
            }

            // 로테이션 감지 (가능한 부위만)
            if (CanRotate(selectedPart) && boneTransform.localRotation != lastKnownRotation)
            {
                rotationValues[selectedPart] = boneTransform.localRotation;
                lastKnownRotation = boneTransform.localRotation;
                Repaint();
            }

            // 포지션 감지 (전체 가능) - 추가
            if (boneTransform.localPosition != lastKnownPosition)
            {
                positionValues[selectedPart] = boneTransform.localPosition;
                lastKnownPosition = boneTransform.localPosition;
                Repaint();
            }
        }
    }
    
    void OnFocus()
    {
        RefreshPresetList();
    }

    private void RefreshPresetList()
    {
        string[] guids = AssetDatabase.FindAssets("t:ArmatureScalerPresetData", new[] { "Assets" });
        presetFiles = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();
        selectedPresetIndex = -1;
        selectedPresetName = "";
    }

    private void InitializeValues()
    {
        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part != HumanoidBodyPart.None)
            {
                // Scale Init
                if (!scaleValues.ContainsKey(part)) scaleValues.Add(part, Vector3.one);
                else scaleValues[part] = Vector3.one;
                
                // Rotation Init
                if (!rotationValues.ContainsKey(part)) rotationValues.Add(part, Quaternion.identity);
                else rotationValues[part] = Quaternion.identity;

                // Position Init - 추가 (기본값 0,0,0)
                if (!positionValues.ContainsKey(part)) positionValues.Add(part, Vector3.zero);
                else positionValues[part] = Vector3.zero;
            }
        }
    }

    private void LoadCurrentValues()
    {
        if (boneMapping == null) return;

        List<HumanoidBodyPart> partsToUpdate = new List<HumanoidBodyPart>(scaleValues.Keys);

        foreach (var part in partsToUpdate)
        {
            HumanBodyBones boneType = GetBoneType(part);
            if (boneMapping.ContainsKey(boneType))
            {
                Transform t = boneMapping[boneType];
                scaleValues[part] = t.localScale;
                rotationValues[part] = t.localRotation;
                positionValues[part] = t.localPosition; // 포지션 로드 추가
            }
        }
    }
    
    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 28,
            normal = new GUIStyleState() { textColor = Color.white }
        };
        GUIContent titleContent = new GUIContent("Armature Scaler", windowIcon);
        GUILayout.Label(titleContent, titleStyle);
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);
        int currentLanguageIndex = (int)language;
        string[] languageButtons = { "English", "한국어", "日本語" };
        int newLanguageIndex = GUILayout.Toolbar(currentLanguageIndex, languageButtons, GUILayout.Height(30));
        if (newLanguageIndex != currentLanguageIndex)
        {
            language = (LanguagePreset)newLanguageIndex;
            SetLanguage(language);
        }
        GUILayout.Space(10);
        
        EditorGUI.BeginChangeCheck();
        targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(UI_TEXT[0], targetAvatarRoot, typeof(GameObject), true);
        
        if (EditorGUI.EndChangeCheck())
        {
            if (targetAvatarRoot != null)
            {
                boneMapping = ArmatureScalerCore.AssignBoneMappings(targetAvatarRoot);
                LoadCurrentValues();
                selectedPart = HumanoidBodyPart.None;
            }
            else
            {
                boneMapping = null;
                selectedPart = HumanoidBodyPart.None;
                InitializeValues();
            }
        }
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(UI_TEXT[27], EditorStyles.boldLabel);
        
        presetScrollPosition = EditorGUILayout.BeginScrollView(presetScrollPosition, GUILayout.Height(60));
        
        if (presetFiles != null && presetFiles.Length > 0)
        {
            const int buttonsPerRow = 2;
            int buttonCount = presetFiles.Length;

            for (int i = 0; i < buttonCount; i += buttonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                
                int buttonsOnThisRow = Mathf.Min(buttonsPerRow, buttonCount - i);

                for (int j = 0; j < buttonsOnThisRow; j++)
                {
                    int index = i + j;
                    if (index < buttonCount)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(presetFiles[index]);
                        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                        if (selectedPresetIndex == index)
                        {
                            buttonStyle.normal.background = MakeTex(1, 1, new Color(0.2f, 0.4f, 1f, 1f));
                        }
                        
                        if (buttonsOnThisRow == 1)
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(fileName, buttonStyle, GUILayout.Width(position.width * 0.45f), GUILayout.Height(25)))
                            {
                                selectedPresetIndex = index;
                                selectedPresetName = fileName;
                            }
                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            if (GUILayout.Button(fileName, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(25)))
                            {
                                selectedPresetIndex = index;
                                selectedPresetName = fileName;
                            }
                        }
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("No presets found.", EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndScrollView();
        
        // --- 프리셋 불러오기 버튼 영역 (수정됨) ---
        EditorGUI.BeginDisabledGroup(selectedPresetIndex == -1);
        
        // 1. 전체 불러오기
        if (GUILayout.Button(UI_TEXT[28]))
        {
            LoadSelectedPreset(true, true, true); // 스케일, 로테이션, 포지션 모두
        }

        // 2. 분리 적용 버튼 (3개로 확장)
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(UI_TEXT[39])) // 스케일만
        {
            LoadSelectedPreset(true, false, false);
        }
        if (GUILayout.Button(UI_TEXT[40])) // 로테이션만
        {
            LoadSelectedPreset(false, true, false);
        }
        if (GUILayout.Button(UI_TEXT[43])) // 포지션만 (새로 추가됨)
        {
            LoadSelectedPreset(false, false, true);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
        // ----------------------------------------

        if (GUILayout.Button(UI_TEXT[29]))
        {
            SaveNewPreset();
        }
        
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(selectedPresetIndex == -1);
        if (GUILayout.Button(UI_TEXT[31]))
        {
            if (EditorUtility.DisplayDialog(UI_TEXT[31], UI_TEXT[32] + selectedPresetName + UI_TEXT[33], UI_TEXT[34], UI_TEXT[35]))
            {
                DeletePreset(presetFiles[selectedPresetIndex]);
                RefreshPresetList();
                selectedPresetIndex = -1;
            }
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button(UI_TEXT[36]))
        {
            ResetScalesToDefault();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.BeginVertical("box");
        
        GuiLine(1, 10);
        
        // 본 선택 버튼들 UI (기존과 동일)
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginVertical(GUILayout.Width(130));
        DrawBoneButton(HumanoidBodyPart.LeftArm, UI_TEXT[3], 100, 25);
        DrawBoneButton(HumanoidBodyPart.LeftLowerArm, UI_TEXT[4], 100, 25);
        DrawBoneButton(HumanoidBodyPart.LeftHand, UI_TEXT[5], 100, 25);
        GUILayout.Space(50);
        DrawBoneButton(HumanoidBodyPart.LeftLeg, UI_TEXT[13], 100, 25);
        DrawBoneButton(HumanoidBodyPart.LeftLowerLeg, UI_TEXT[14], 100, 25);
        DrawBoneButton(HumanoidBodyPart.LeftFoot, UI_TEXT[15], 100, 25);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginVertical(GUILayout.Width(120));
        DrawBoneButton(HumanoidBodyPart.Head, UI_TEXT[6], 100, 25);
        GUILayout.Space(5);
        DrawBoneButton(HumanoidBodyPart.Torso, UI_TEXT[7], 100, 25);
        DrawBoneButton(HumanoidBodyPart.Spine, UI_TEXT[8], 100, 25);
        DrawBoneButton(HumanoidBodyPart.Hips, UI_TEXT[9], 100, 25);
        EditorGUILayout.EndVertical();
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginVertical(GUILayout.Width(130));
        DrawBoneButton(HumanoidBodyPart.RightArm, UI_TEXT[10], 100, 25);
        DrawBoneButton(HumanoidBodyPart.RightLowerArm, UI_TEXT[11], 100, 25);
        DrawBoneButton(HumanoidBodyPart.RightHand, UI_TEXT[12], 100, 25);
        GUILayout.Space(50);
        DrawBoneButton(HumanoidBodyPart.RightLeg, UI_TEXT[16], 100, 25);
        DrawBoneButton(HumanoidBodyPart.RightLowerLeg, UI_TEXT[17], 100, 25);
        DrawBoneButton(HumanoidBodyPart.RightFoot, UI_TEXT[18], 100, 25);
        EditorGUILayout.EndVertical();
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GuiLine(1, 10);
        EditorGUILayout.LabelField(UI_TEXT[19], EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        DrawBoneButton(HumanoidBodyPart.LeftBreast, UI_TEXT[20], 120, 25);
        GUILayout.FlexibleSpace();
        DrawBoneButton(HumanoidBodyPart.RightBreast, UI_TEXT[21], 120, 25);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        DrawBoneButton(HumanoidBodyPart.LeftButt, UI_TEXT[22], 120, 25);
        GUILayout.FlexibleSpace();
        DrawBoneButton(HumanoidBodyPart.RightButt, UI_TEXT[23], 120, 25);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
        
        GuiLine(1, 10);
        
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(UI_TEXT[24], EditorStyles.boldLabel);

        if (selectedPart != HumanoidBodyPart.None)
        {
            // 1. Scale
            Vector3 scale = GetPartScale(selectedPart);
            EditorGUI.BeginChangeCheck();
            float uniformScale = EditorGUILayout.FloatField(UI_TEXT[26], scale.x);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartScale(new Vector3(uniformScale, uniformScale, uniformScale));
                ArmatureScalerLogic.ApplyScale(targetAvatarRoot, MapToHumanBodyBones(scaleValues));
            }
            
            EditorGUI.BeginChangeCheck();
            Vector3 newScale = EditorGUILayout.Vector3Field(GetPartName(selectedPart) + $" Scale", scale);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartScale(newScale);
                ArmatureScalerLogic.ApplyScale(targetAvatarRoot, MapToHumanBodyBones(scaleValues));
            }

            // 2. Position (추가됨) - 스케일처럼 전체 적용 가능
            GUILayout.Space(10);
            GUILayout.Label(UI_TEXT[41], EditorStyles.boldLabel); // "포지션 조절"
            Vector3 position = GetPartPosition(selectedPart);
            
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = EditorGUILayout.Vector3Field(UI_TEXT[42], position); // "포지션"
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePartPosition(newPosition);
                ArmatureScalerLogic.ApplyPosition(targetAvatarRoot, MapToHumanBodyBonesForPosition(positionValues));
            }
            
            // 3. Rotation (특정 부위만)
            if (CanRotate(selectedPart))
            {
                GUILayout.Space(10);
                GUILayout.Label(UI_TEXT[37], EditorStyles.boldLabel);
                Quaternion rotation = GetPartRotation(selectedPart);
                
                EditorGUI.BeginChangeCheck();
                Quaternion newRotation = Quaternion.Euler(EditorGUILayout.Vector3Field(UI_TEXT[38], rotation.eulerAngles));
                if (EditorGUI.EndChangeCheck())
                {
                    UpdatePartRotation(newRotation);
                    ArmatureScalerLogic.ApplyRotation(targetAvatarRoot, MapToHumanBodyBonesForRotation(rotationValues));
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField(UI_TEXT[25]);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawBoneButton(HumanoidBodyPart part, string buttonText, float width, float height)
    {
        HumanoidBodyPart previousSelectedPart = selectedPart;
        HumanBodyBones boneType = GetBoneType(part);
        string boneName = (boneMapping != null && boneMapping.ContainsKey(boneType)) ? boneMapping[boneType].name : UI_TEXT[30];
        string display = boneName == UI_TEXT[30] ? buttonText : boneName;
        
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        
        if (selectedPart == part)
        {
            buttonStyle.normal.background = MakeTex(1, 1, new Color(0.2f, 0.4f, 1f, 1f));
        }
        else
        {
            buttonStyle.normal.background = GUI.skin.button.normal.background;
        }

        buttonStyle.normal.textColor = Color.white;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        buttonStyle.fontSize = 12;

        bool isEnabled = boneName != UI_TEXT[30];
        EditorGUI.BeginDisabledGroup(!isEnabled);
        
        if (GUILayout.Button(display, buttonStyle, GUILayout.Width(width), GUILayout.Height(height)))
        {
            GUI.FocusControl(null);
            
            if (previousSelectedPart != part && previousSelectedPart != HumanoidBodyPart.None)
            {
                ApplyCurrentChanges(previousSelectedPart);
            }
            
            selectedPart = part;
            HumanBodyBones selectedBoneType = GetBoneType(part);
            if (boneMapping != null && boneMapping.TryGetValue(selectedBoneType, out Transform boneTransform))
            {
                lastKnownScale = boneTransform.localScale;
                lastKnownRotation = boneTransform.localRotation;
                lastKnownPosition = boneTransform.localPosition; // 포지션 업데이트
                Selection.activeGameObject = boneTransform.gameObject;
            }
            else
            {
                lastKnownScale = Vector3.one;
                lastKnownRotation = Quaternion.identity;
                lastKnownPosition = Vector3.zero;
            }
        }
        
        EditorGUI.EndDisabledGroup();
    }

    private void ApplyCurrentChanges(HumanoidBodyPart part)
    {
        if (targetAvatarRoot != null)
        {
            // Scale
            if (scaleValues.TryGetValue(part, out Vector3 scale))
            {
                ArmatureScalerLogic.ApplyScale(targetAvatarRoot, new Dictionary<HumanBodyBones, Vector3> { { GetBoneType(part), scale } });
            }
            // Position (전체 가능)
            if (positionValues.TryGetValue(part, out Vector3 position))
            {
                ArmatureScalerLogic.ApplyPosition(targetAvatarRoot, new Dictionary<HumanBodyBones, Vector3> { { GetBoneType(part), position } });
            }
            // Rotation (제한됨)
            if (CanRotate(part) && rotationValues.TryGetValue(part, out Quaternion rotation))
            {
                ArmatureScalerLogic.ApplyRotation(targetAvatarRoot, new Dictionary<HumanBodyBones, Quaternion> { { GetBoneType(part), rotation } });
            }
        }
    }

    private bool CanRotate(HumanoidBodyPart part)
    {
        return part == HumanoidBodyPart.LeftBreast || part == HumanoidBodyPart.RightBreast ||
               part == HumanoidBodyPart.LeftButt || part == HumanoidBodyPart.RightButt;
    }
    
    private Vector3 GetPartScale(HumanoidBodyPart part)
    {
        if (scaleValues.TryGetValue(part, out Vector3 s)) return s;
        return Vector3.one;
    }
    
    private Quaternion GetPartRotation(HumanoidBodyPart part)
    {
        if (rotationValues.TryGetValue(part, out Quaternion r)) return r;
        return Quaternion.identity;
    }

    // 추가: 포지션 Getter
    private Vector3 GetPartPosition(HumanoidBodyPart part)
    {
        if (positionValues.TryGetValue(part, out Vector3 p)) return p;
        return Vector3.zero;
    }

    private void UpdatePartScale(Vector3 newScale)
    {
        if (selectedPart != HumanoidBodyPart.None && scaleValues.ContainsKey(selectedPart))
        {
            scaleValues[selectedPart] = newScale;
            lastKnownScale = newScale;
        }
    }
    
    private void UpdatePartRotation(Quaternion newRotation)
    {
        if (selectedPart != HumanoidBodyPart.None && rotationValues.ContainsKey(selectedPart))
        {
            rotationValues[selectedPart] = newRotation;
            lastKnownRotation = newRotation;
        }
    }

    // 추가: 포지션 Updater
    private void UpdatePartPosition(Vector3 newPosition)
    {
        if (selectedPart != HumanoidBodyPart.None && positionValues.ContainsKey(selectedPart))
        {
            positionValues[selectedPart] = newPosition;
            lastKnownPosition = newPosition;
        }
    }
    
    private string GetBoneName(HumanoidBodyPart part)
    {
        HumanBodyBones boneType = GetBoneType(part);
        if (boneMapping != null && boneMapping.ContainsKey(boneType))
        {
            return boneMapping[boneType].name;
        }
        return UI_TEXT[30];
    }

    private string GetPartName(HumanoidBodyPart part)
    {
        // ... (기존 이름 반환 로직 동일) ...
        switch (part)
        {
            case HumanoidBodyPart.Head: return UI_TEXT[6];
            case HumanoidBodyPart.Torso: return UI_TEXT[7];
            case HumanoidBodyPart.Spine: return UI_TEXT[8];
            case HumanoidBodyPart.Hips: return UI_TEXT[9];
            case HumanoidBodyPart.LeftArm: return UI_TEXT[3];
            case HumanoidBodyPart.LeftLowerArm: return UI_TEXT[4];
            case HumanoidBodyPart.LeftHand: return UI_TEXT[5];
            case HumanoidBodyPart.RightArm: return UI_TEXT[10];
            case HumanoidBodyPart.RightLowerArm: return UI_TEXT[11];
            case HumanoidBodyPart.RightHand: return UI_TEXT[12];
            case HumanoidBodyPart.LeftLeg: return UI_TEXT[13];
            case HumanoidBodyPart.LeftLowerLeg: return UI_TEXT[14];
            case HumanoidBodyPart.LeftFoot: return UI_TEXT[15];
            case HumanoidBodyPart.RightLeg: return UI_TEXT[16];
            case HumanoidBodyPart.RightLowerLeg: return UI_TEXT[17];
            case HumanoidBodyPart.RightFoot: return UI_TEXT[18];
            case HumanoidBodyPart.LeftBreast: return UI_TEXT[20];
            case HumanoidBodyPart.RightBreast: return UI_TEXT[21];
            case HumanoidBodyPart.LeftButt: return UI_TEXT[22];
            case HumanoidBodyPart.RightButt: return UI_TEXT[23];
            default: return "선택 없음";
        }
    }
    
    private HumanBodyBones GetBoneType(HumanoidBodyPart part)
    {
        // ... (기존 로직 동일) ...
        switch (part)
        {
            case HumanoidBodyPart.Head: return HumanBodyBones.Head;
            case HumanoidBodyPart.Torso: return HumanBodyBones.Chest;
            case HumanoidBodyPart.Spine: return HumanBodyBones.Spine;
            case HumanoidBodyPart.Hips: return HumanBodyBones.Hips;
            case HumanoidBodyPart.LeftBreast: return (HumanBodyBones)100;
            case HumanoidBodyPart.RightBreast: return (HumanBodyBones)101;
            case HumanoidBodyPart.LeftButt: return (HumanBodyBones)102;
            case HumanoidBodyPart.RightButt: return (HumanBodyBones)103;
            case HumanoidBodyPart.LeftArm: return HumanBodyBones.LeftUpperArm;
            case HumanoidBodyPart.LeftLowerArm: return HumanBodyBones.LeftLowerArm;
            case HumanoidBodyPart.LeftHand: return HumanBodyBones.LeftHand;
            case HumanoidBodyPart.RightArm: return HumanBodyBones.RightUpperArm;
            case HumanoidBodyPart.RightLowerArm: return HumanBodyBones.RightLowerArm;
            case HumanoidBodyPart.RightHand: return HumanBodyBones.RightHand;
            case HumanoidBodyPart.LeftLeg: return HumanBodyBones.LeftUpperLeg;
            case HumanoidBodyPart.LeftLowerLeg: return HumanBodyBones.LeftLowerLeg;
            case HumanoidBodyPart.LeftFoot: return HumanBodyBones.LeftFoot;
            case HumanoidBodyPart.RightLeg: return HumanBodyBones.RightUpperLeg;
            case HumanoidBodyPart.RightLowerLeg: return HumanBodyBones.RightLowerLeg;
            case HumanoidBodyPart.RightFoot: return HumanBodyBones.RightFoot;
            default: return HumanBodyBones.LastBone;
        }
    }

    private Dictionary<HumanBodyBones, Vector3> MapToHumanBodyBones(Dictionary<HumanoidBodyPart, Vector3> scales)
    {
        Dictionary<HumanBodyBones, Vector3> result = new Dictionary<HumanBodyBones, Vector3>();
        foreach (var kvp in scales)
        {
            HumanBodyBones boneType = GetBoneType(kvp.Key);
            if (boneType != HumanBodyBones.LastBone)
            {
                result[boneType] = kvp.Value;
            }
        }
        return result;
    }

    // 추가: 포지션 매핑 (스케일과 동일하게 전체 허용)
    private Dictionary<HumanBodyBones, Vector3> MapToHumanBodyBonesForPosition(Dictionary<HumanoidBodyPart, Vector3> positions)
    {
        Dictionary<HumanBodyBones, Vector3> result = new Dictionary<HumanBodyBones, Vector3>();
        foreach (var kvp in positions)
        {
            HumanBodyBones boneType = GetBoneType(kvp.Key);
            if (boneType != HumanBodyBones.LastBone)
            {
                result[boneType] = kvp.Value;
            }
        }
        return result;
    }
    
    private Dictionary<HumanBodyBones, Quaternion> MapToHumanBodyBonesForRotation(Dictionary<HumanoidBodyPart, Quaternion> rotations)
    {
        Dictionary<HumanBodyBones, Quaternion> result = new Dictionary<HumanBodyBones, Quaternion>();
        foreach (var kvp in rotations)
        {
            if (CanRotate(kvp.Key))
            {
                HumanBodyBones boneType = GetBoneType(kvp.Key);
                if (boneType != HumanBodyBones.LastBone)
                {
                    result[boneType] = kvp.Value;
                }
            }
        }
        return result;
    }
    
    void GuiLine(int i_height = 1, int padding = 5)
    {
        GUILayout.Space(padding);
        Rect rect = EditorGUILayout.GetControlRect(false, i_height);
        rect.height = i_height;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        GUILayout.Space(padding);
    }
    
    private void SetLanguage(LanguagePreset lang)
    {
        // 텍스트 배열 확장 (41, 42, 43 인덱스 추가)
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new string[] {
                    "타겟 아바타 루트", "스케일 저장", "저장된 스케일 불러오기",
                    "왼쪽 팔", "왼쪽 아랫팔", "왼쪽 손", "머리", "몸통", "척추", "골반",
                    "오른쪽 팔", "오른쪽 아랫팔", "오른쪽 손",
                    "왼쪽 다리", "왼쪽 아랫다리", "왼쪽 발",
                    "오른쪽 다리", "오른쪽 아랫다리", "오른쪽 발",
                    "세부 부위", "왼쪽 가슴", "오른쪽 가슴", "왼쪽 엉덩이", "오른쪽 엉덩이",
                    "선택된 부위", "부위를 선택하세요.", "일괄 조절",
                    "스케일 프리셋", "프리셋 불러오기", "새 프리셋 저장",
                    "미발견", "선택한 프리셋 삭제", "프리셋 ‘", "’를 정말로 삭제하시겠습니까?",
                    "삭제", "취소", "사이즈 초기화", "로테이션 조절", "로테이션",
                    "스케일만 불러오기", "로테이션만 불러오기",
                    "포지션 조절", "포지션", "포지션만 불러오기" // 41, 42, 43 추가
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new string[] {
                    "対象アバターのルート", "スケールを保存", "保存されたスケールをロード",
                    "左腕", "左前腕", "左手", "頭", "胴体", "脊椎", "骨盤",
                    "右腕", "右前腕", "右手",
                    "左脚", "左下脚", "左足",
                    "右脚", "右下脚", "右足",
                    "詳細部位", "左胸", "右胸", "左お尻", "右お尻",
                    "選択された部位", "部位を選択してください。", "一括調整",
                    "スケールプリセット", "プリセットをロード", "新しいプリセットを保存",
                    "未発見", "選択したプリセットを削除", "プリセット「", "」を本当に削除しますか？",
                    "削除", "キャンセル", "サイズリセット", "回転調整", "回転",
                    "スケールのみロード", "回転のみロード",
                    "位置調整", "位置", "位置のみロード" // 41, 42, 43 추가
                };
                break;
            case LanguagePreset.English:
            default:
                UI_TEXT = new string[] {
                    "Target Avatar Root", "Save Scale", "Load Saved Scale",
                    "Left Arm", "Left Lower Arm", "Left Hand", "Head", "Torso", "Spine", "Hips",
                    "Right Arm", "Right Lower Arm", "Right Hand",
                    "Left Leg", "Left Lower Leg", "Left Foot",
                    "Right Leg", "Right Lower Leg", "Right Foot",
                    "Detailed Parts", "Left Breast", "Right Breast", "Left Butt", "Right Butt",
                    "Selected Part", "Select a part.", "Uniform Scale",
                    "Scale Presets", "Load Preset", "Save New Preset",
                    "Not Found", "Delete Selected Preset", "Are you sure you want to delete the preset '", "'?",
                    "Delete", "Cancel", "Reset Scales", "Rotation Adjustment", "Rotation",
                    "Load Scale Only", "Load Rotation Only",
                    "Position Adjustment", "Position", "Load Position Only" // 41, 42, 43 추가
                };
                break;
        }
    }
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    
    // 로드 함수 수정 (Position 플래그 추가)
    private void LoadSelectedPreset(bool applyScale = true, bool applyRotation = true, bool applyPosition = true)
    {
        if (targetAvatarRoot == null)
        {
            Debug.LogError("아바타를 먼저 지정해주세요.");
            return;
        }
        
        if (selectedPresetIndex >= 0 && selectedPresetIndex < presetFiles.Length)
        {
            string filePath = presetFiles[selectedPresetIndex];
            ArmatureScalerPresetData loadedData = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(filePath);
            if (loadedData != null)
            {
                // 1. Scale
                if (applyScale)
                {
                    scaleValues.Clear();
                    foreach (var kvp in loadedData.scales.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                        {
                            scaleValues[part] = kvp.Value.ToVector3();
                        }
                    }
                    ArmatureScalerLogic.ApplyScale(targetAvatarRoot, MapToHumanBodyBones(scaleValues));
                }

                // 2. Rotation
                if (applyRotation)
                {
                    rotationValues.Clear();
                    foreach (var kvp in loadedData.rotations.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                        {
                            rotationValues[part] = kvp.Value.ToQuaternion();
                        }
                    }
                    ArmatureScalerLogic.ApplyRotation(targetAvatarRoot, MapToHumanBodyBonesForRotation(rotationValues));
                }

                // 3. Position (추가됨)
                if (applyPosition)
                {
                    positionValues.Clear();
                    foreach (var kvp in loadedData.positions.dictionary)
                    {
                        if (System.Enum.TryParse(kvp.Key, out HumanoidBodyPart part))
                        {
                            positionValues[part] = kvp.Value.ToVector3();
                        }
                    }
                    ArmatureScalerLogic.ApplyPosition(targetAvatarRoot, MapToHumanBodyBonesForPosition(positionValues));
                }
                
                string appliedType = "데이터";
                if (applyScale && applyRotation && applyPosition) appliedType = "전체 데이터";
                else
                {
                    List<string> types = new List<string>();
                    if (applyScale) types.Add("스케일");
                    if (applyRotation) types.Add("로테이션");
                    if (applyPosition) types.Add("포지션");
                    appliedType = string.Join(", ", types) + " 데이터";
                }

                Debug.Log($"프리셋 '{Path.GetFileNameWithoutExtension(filePath)}'의 {appliedType}가 성공적으로 불러와졌습니다.");
            }
            else
            {
                Debug.LogError("프리셋 파일을 찾을 수 없거나 형식이 올바르지 않습니다.");
            }
        }
    }

    private void SaveNewPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject("새 프리셋 저장", "NewPreset", "asset", "새로운 프리셋을 저장할 위치와 이름을 지정해주세요.");
        if (!string.IsNullOrEmpty(path))
        {
            ArmatureScalerPresetData existingPreset = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(path);
            if (existingPreset != null)
            {
                if (!EditorUtility.DisplayDialog("경고", "기존 프리셋을 덮어쓰시겠습니까?", "덮어쓰기", "취소"))
                {
                    return;
                }
                AssetDatabase.DeleteAsset(path);
            }
            
            ArmatureScalerPresetData newPreset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            
            // Scale 저장
            foreach (var kvp in scaleValues)
            {
                newPreset.scales.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));
            }

            // Rotation 저장
            foreach (var kvp in rotationValues)
            {
                if(CanRotate(kvp.Key))
                {
                    newPreset.rotations.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableQuaternion(kvp.Value));
                }
            }

            // Position 저장 (추가됨)
            foreach (var kvp in positionValues)
            {
                newPreset.positions.dictionary.Add(kvp.Key.ToString(), new ArmatureScalerPresetData.SerializableVector3(kvp.Value));
            }

            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            RefreshPresetList();
            Debug.Log($"프리셋이 '{path}'에 성공적으로 저장되었습니다.");
        }
    }

    private void DeletePreset(string filePath)
    {
        AssetDatabase.DeleteAsset(filePath);
        AssetDatabase.Refresh();
        Debug.Log("프리셋이 성공적으로 삭제되었습니다.");
    }

    private void ResetScalesToDefault()
    {
        if (targetAvatarRoot == null)
        {
            Debug.LogError("아바타를 먼저 지정해주세요.");
            return;
        }
        
        foreach (HumanoidBodyPart part in System.Enum.GetValues(typeof(HumanoidBodyPart)))
        {
            if (part != HumanoidBodyPart.None)
            {
                if (scaleValues.ContainsKey(part))
                {
                    scaleValues[part] = Vector3.one;
                }
                // *주의: 포지션은 0,0,0으로 초기화하면 뼈가 뭉개지므로 초기화 로직에 포함하지 않거나,
                // 최초 로드 시의 값을 기억해두었다가 복구하는 방식이어야 합니다.
                // 여기서는 스케일 초기화만 유지합니다. (요청에 스케일 리셋만 포함되어 있으므로)
            }
        }
        
        ArmatureScalerLogic.ApplyScale(targetAvatarRoot, MapToHumanBodyBones(scaleValues));
        
        selectedPart = HumanoidBodyPart.None;
        Debug.Log("모든 스케일이 초기화되었습니다.");
    }
}