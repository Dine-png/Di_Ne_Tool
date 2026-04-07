#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(DiNeMultiDresser))]
public class DiNeMultiSupporter : Editor
{
    private Texture2D windowIcon;
    private Font      titleFont;
    private int selectedLayerIndex = 0; 
    
    private enum Language { Korean, English, Japanese }
    private static readonly string[] LangButtonLabels = { "🇰🇷 한국어", "🇺🇸 English", "🇯🇵 日本語" };
    
    private Language currentLanguage
    {
        get 
        {
            int val = EditorPrefs.GetInt("DiNeLang", 0);
            if (val < 0 || val >= 3) val = 0;
            return (Language)val;
        }
        set => EditorPrefs.SetInt("DiNeLang", (int)value);
    }

    private readonly Dictionary<Language, Dictionary<string, string>> text = new Dictionary<Language, Dictionary<string, string>>
    {
        {
            Language.Korean, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "기본 설정 (아바타 & FX & 메뉴)" },
                { "avatarRoot", "아바타 Root" },
                { "refreshTooltip", "아바타 다시 찾기 & 저장된 설정 불러오기 (새로고침)" },
                { "shapeKeyTargets", "쉐이프키 타겟 (바디/얼굴 메쉬) 🦋" },
                { "skDragHint", "여기로 드래그하여 쉐이프키 타겟 추가" },
                { "layerCategory", "옷장 카테고리 (레이어) 📂" },
                { "catName", "카테고리 이름" },
                { "delCat", "삭제" },
                { "mainDragHint", "🧲 여기로 옷 오브젝트들을 드래그하세요!" },
                { "defaultState", "기본 상태 (Default)" },
                { "menuButton", "메뉴 버튼" },
                { "defaultWarn", "기본 상태는 메뉴 버튼이 생성되지 않습니다. (꺼진 상태 or 기본 의상)" },
                { "menuName", "메뉴 이름" },
                { "linkedObj", "함께 켜질 오브젝트 (Linked):" },
                { "linkDragHint", "추가 오브젝트 드래그" },
                { "skSettings", "쉐이프키 설정 (ShapeKeys)" },
                { "generate", "🎀 적용 및 생성 (Generate)" },
                { "cleanup", "🗑 모든 데이터 삭제 (Clean Up)" }, // 추가됨
                { "catIcon", "아이콘" }
            }
        },
        {
            Language.English, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "Global Settings (Avatar & FX & Menu)" },
                { "avatarRoot", "Avatar Root" },
                { "refreshTooltip", "Reload Avatar & Restore Settings (Refresh)" },
                { "shapeKeyTargets", "Shape Key Targets (Body/Face Meshes) 🦋" },
                { "skDragHint", "Drag here to add Shape Key Targets" },
                { "layerCategory", "Outfit Categories (Layers) 📂" },
                { "catName", "Category Name" },
                { "delCat", "Delete" },
                { "mainDragHint", "🧲 Drag outfit objects here!" },
                { "defaultState", "Default State" },
                { "menuButton", "Menu Button" },
                { "defaultWarn", "Default state does not create a menu button. (Off state or Base outfit)" },
                { "menuName", "Menu Name" },
                { "linkedObj", "Linked Objects (Toggle Together):" },
                { "linkDragHint", "Drag extra objects here" },
                { "skSettings", "Shape Key Settings" },
                { "generate", "🎀 Generate All" },
                { "cleanup", "🗑 Clean Up All Data" }, // 추가됨
                { "catIcon", "Icon" }
            }
        },
        {
            Language.Japanese, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "基本設定 (アバター & FX & メニュー)" },
                { "avatarRoot", "アバター Root" },
                { "refreshTooltip", "アバター再検索 & 設定復元 (更新)" },
                { "shapeKeyTargets", "シェイプキー対象 (体/顔メッシュ) 🦋" },
                { "skDragHint", "ここにドラッグしてシェイプキー対象を追加" },
                { "layerCategory", "衣装カテゴリー (レイヤー) 📂" },
                { "catName", "カテゴリー名" },
                { "delCat", "削除" },
                { "mainDragHint", "🧲 ここに衣装オブジェクトをドラッグ！" },
                { "defaultState", "基本状態 (Default)" },
                { "menuButton", "メニューボタン" },
                { "defaultWarn", "基本状態はメニューボタンが生成されません。(オフ状態 or 基本衣装)" },
                { "menuName", "メニュー名" },
                { "linkedObj", "連動オブジェクト (Linked):" },
                { "linkDragHint", "追加オブジェクトをドラッグ" },
                { "skSettings", "シェイプキー設定 (ShapeKeys)" },
                { "generate", "🎀 適用して生成 (Generate)" },
                { "cleanup", "🗑 全データ削除 (Clean Up)" }, // 추가됨
                { "catIcon", "アイコン" }
            }
        }
    };

    private void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        DiNeMultiDresser gen = (DiNeMultiDresser)target;
        if(gen.rootTransform == null) gen.TryAutoAssignFXController();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DiNeMultiDresser gen = (DiNeMultiDresser)target;
        SerializedProperty root = serializedObject.FindProperty("rootTransform");
        SerializedProperty controller = serializedObject.FindProperty("animatorController");
        SerializedProperty exMenu = serializedObject.FindProperty("expressionsMenu");
        SerializedProperty shapeKeyTargets = serializedObject.FindProperty("shapeKeyTargets");
        SerializedProperty layers = serializedObject.FindProperty("layers");

        DrawHeader("DiNe Multi Dresser");
        
        GUILayout.Space(5);
        int langIndex = (int)currentLanguage;
        langIndex = GUILayout.Toolbar(langIndex, LangButtonLabels, GUILayout.Height(35)); 
        currentLanguage = (Language)langIndex;
        var lang = text[currentLanguage]; 

        GUILayout.Space(15);

        EditorGUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField(lang["globalSettings"], EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        Transform before = root.objectReferenceValue as Transform;
        Transform after = EditorGUILayout.ObjectField(lang["avatarRoot"], before, typeof(Transform), true) as Transform;
        
        if (GUILayout.Button(new GUIContent("↺", lang["refreshTooltip"]), GUILayout.Width(30), GUILayout.Height(20)))
        {
            gen.TryAutoAssignFXController(); 
            gen.TryRestoreFromProfile(); 
            serializedObject.Update();   
        }
        EditorGUILayout.EndHorizontal();

        if (after != before) {
            root.objectReferenceValue = after;
            serializedObject.ApplyModifiedProperties();
            gen.TryAutoAssignFXController();
            gen.TryRestoreFromProfile(); 
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("FX Controller", GUILayout.Width(100));
        controller.objectReferenceValue = EditorGUILayout.ObjectField(controller.objectReferenceValue, typeof(RuntimeAnimatorController), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Expression Menu", GUILayout.Width(100));
        exMenu.objectReferenceValue = EditorGUILayout.ObjectField(exMenu.objectReferenceValue, typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu), false);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField(lang["shapeKeyTargets"], EditorStyles.boldLabel);
        DrawGlobalShapeKeyTargets(shapeKeyTargets, lang);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(lang["layerCategory"], EditorStyles.boldLabel);

        if (layers.arraySize == 0)
        {
            layers.InsertArrayElementAtIndex(0);
            layers.GetArrayElementAtIndex(0).FindPropertyRelative("layerName").stringValue = "Main";
            serializedObject.ApplyModifiedProperties();
            gen.layers[0].EnsureSize(0);
        }

        List<string> tabNames = new List<string>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layerNameProp = layers.GetArrayElementAtIndex(i).FindPropertyRelative("layerName");
            string name = layerNameProp != null ? layerNameProp.stringValue : $"Layer {i}";
            tabNames.Add(string.IsNullOrEmpty(name) ? $"Layer {i}" : name);
        }

        EditorGUILayout.BeginHorizontal();
        if (selectedLayerIndex >= layers.arraySize) selectedLayerIndex = layers.arraySize - 1;
        selectedLayerIndex = GUILayout.Toolbar(selectedLayerIndex, tabNames.ToArray(), GUILayout.Height(35));

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("+", GUILayout.Width(40), GUILayout.Height(35)))
        {
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var newLayerProp = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            newLayerProp.FindPropertyRelative("layerName").stringValue = "New Layer";
            newLayerProp.FindPropertyRelative("targets").ClearArray();
            newLayerProp.FindPropertyRelative("labels").ClearArray();
            newLayerProp.FindPropertyRelative("icons").ClearArray();
            
            serializedObject.ApplyModifiedProperties();
            gen.layers[gen.layers.Count - 1].EnsureSize(0);
            gen.layers[gen.layers.Count - 1].linkedObjects.Clear();
            gen.layers[gen.layers.Count - 1].perButtonShapeKeyStates.Clear();
            
            selectedLayerIndex = layers.arraySize - 1;
            GUI.FocusControl(null);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        DrawSelectedLayerUI(gen, layers, selectedLayerIndex, lang);

        EditorGUILayout.Space(20);
        
        // [생성 버튼]
        if (GUILayout.Button(lang["generate"], new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, fixedHeight = 50 }))
        {
            DiNeMultiIconGenerator.GenerateIcons(gen);
            gen.Generate();
        }

        GUILayout.Space(5);

        // [삭제 버튼] - 붉은색 경고 느낌
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button(lang["cleanup"], new GUIStyle(GUI.skin.button) { fixedHeight = 30 }))
        {
            if (EditorUtility.DisplayDialog("Clean Up", "정말로 생성된 모든 데이터(파라미터, 레이어, 메뉴)를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", "삭제 (Yes)", "취소 (No)"))
            {
                gen.DeleteAllGeneratedData();
            }
        }
        GUI.backgroundColor = Color.white;

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSelectedLayerUI(DiNeMultiDresser gen, SerializedProperty layers, int index, Dictionary<string, string> lang)
    {
        if (index < 0 || index >= layers.arraySize) return;

        SerializedProperty layerProp = layers.GetArrayElementAtIndex(index);
        SerializedProperty layerName = layerProp.FindPropertyRelative("layerName");
        SerializedProperty layerIcon = layerProp.FindPropertyRelative("layerIcon");
        SerializedProperty targets = layerProp.FindPropertyRelative("targets");
        SerializedProperty labels = layerProp.FindPropertyRelative("labels");
        SerializedProperty icons = layerProp.FindPropertyRelative("icons");

        var currentLayerData = gen.layers[index];
        currentLayerData.EnsureSize(targets.arraySize);
        SyncShapeKeyData(gen, currentLayerData);

        EditorGUILayout.BeginVertical("helpBox"); 
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.BeginVertical(GUILayout.Width(70));
        GUILayout.Label(lang["catIcon"], EditorStyles.centeredGreyMiniLabel);
        layerIcon.objectReferenceValue = EditorGUILayout.ObjectField(layerIcon.objectReferenceValue, typeof(Texture2D), false, GUILayout.Width(64), GUILayout.Height(64));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        GUILayout.Space(5);
        EditorGUILayout.LabelField(lang["catName"], EditorStyles.boldLabel);
        layerName.stringValue = EditorGUILayout.TextField(layerName.stringValue, GUILayout.Height(25)); 
        
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (layers.arraySize > 1)
        {
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button(lang["delCat"], GUILayout.Width(80), GUILayout.Height(24)))
            {
                layers.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                selectedLayerIndex = Mathf.Max(0, index - 1);
                GUI.backgroundColor = Color.white;
                return;
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.EndVertical(); 

        EditorGUILayout.Space(10);
        
        Rect dropArea = GUILayoutUtility.GetRect(0, 45, GUILayout.ExpandWidth(true)); 
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f); 
        GUI.Box(dropArea, lang["mainDragHint"], EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;

        HandleDragDrop(dropArea, (objs) => {
            foreach(var go in objs) {
                int idx = targets.arraySize;
                targets.InsertArrayElementAtIndex(idx);
                targets.GetArrayElementAtIndex(idx).objectReferenceValue = go;
                labels.InsertArrayElementAtIndex(idx);
                labels.GetArrayElementAtIndex(idx).stringValue = go.name;
                icons.InsertArrayElementAtIndex(idx);
                icons.GetArrayElementAtIndex(idx).objectReferenceValue = null;
                currentLayerData.linkedObjects.Add(new DiNeMultiDresser.LinkedGroup());
                currentLayerData.perButtonShapeKeyStates.Add(new DiNeMultiDresser.ShapeKeyMeshList());
            }
            serializedObject.ApplyModifiedProperties();
            SyncShapeKeyData(gen, currentLayerData);
        });

        EditorGUILayout.Space(5);

        for (int i = 0; i < targets.arraySize; i++)
        {
            SerializedProperty t = targets.GetArrayElementAtIndex(i);
            SerializedProperty l = labels.GetArrayElementAtIndex(i);
            SerializedProperty icon = icons.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("helpBox");
            
            EditorGUILayout.BeginHorizontal();
            string headerLabel = (i == 0) ? lang["defaultState"] : $"{lang["menuButton"]} {i}";
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(30), GUILayout.Height(20)))
            {
                targets.DeleteArrayElementAtIndex(i);
                labels.DeleteArrayElementAtIndex(i);
                icons.DeleteArrayElementAtIndex(i);
                currentLayerData.RemoveAt(i);
                serializedObject.ApplyModifiedProperties();
                break;
            }
            EditorGUILayout.EndHorizontal();

            t.objectReferenceValue = EditorGUILayout.ObjectField(GUIContent.none, t.objectReferenceValue, typeof(GameObject), true);
            
            if (i != 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                l.stringValue = EditorGUILayout.TextField(lang["menuName"], l.stringValue);
                EditorGUILayout.EndVertical();

                icon.objectReferenceValue = EditorGUILayout.ObjectField(
                    icon.objectReferenceValue, typeof(Texture2D), false,
                    GUILayout.Width(60), GUILayout.Height(60)
                );
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(lang["defaultWarn"], MessageType.None);
            }

            DrawPerButtonShapeKeyUI(gen, currentLayerData, i, index, lang);

            if (currentLayerData.linkedObjects.Count > i)
            {
                EditorGUILayout.LabelField(lang["linkedObj"]);
                for (int j = 0; j < currentLayerData.linkedObjects[i].objects.Count; j++)
                {
                    EditorGUILayout.BeginHorizontal();
                    currentLayerData.linkedObjects[i].objects[j] = (GameObject)EditorGUILayout.ObjectField(currentLayerData.linkedObjects[i].objects[j], typeof(GameObject), true);
                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        currentLayerData.linkedObjects[i].objects.RemoveAt(j);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                Rect subDrop = GUILayoutUtility.GetRect(0, 25, GUILayout.ExpandWidth(true));
                Color subOriginalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.9f, 1f); 
                GUI.Box(subDrop, lang["linkDragHint"], EditorStyles.helpBox);
                GUI.backgroundColor = subOriginalColor;

                HandleDragDrop(subDrop, (objs) => {
                     foreach(var o in objs) currentLayerData.linkedObjects[i].objects.Add(o);
                });
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
    
    private void DrawGlobalShapeKeyTargets(SerializedProperty shapeKeyTargets, Dictionary<string, string> lang)
    {
        for (int i = 0; i < shapeKeyTargets.arraySize; i++)
        {
            EditorGUILayout.BeginHorizontal();
            SerializedProperty prop = shapeKeyTargets.GetArrayElementAtIndex(i);
            prop.objectReferenceValue = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(GameObject), true);
            if (GUILayout.Button("-", GUILayout.Width(25)))
            {
                shapeKeyTargets.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        Rect dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f); 
        GUI.Box(dropArea, lang["skDragHint"], EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;

        HandleDragDrop(dropArea, (objs) => {
            foreach(var o in objs) {
                 bool exists = false;
                 for(int k=0; k<shapeKeyTargets.arraySize; k++) 
                     if(shapeKeyTargets.GetArrayElementAtIndex(k).objectReferenceValue == o) exists = true;
                 if(!exists) {
                    int idx = shapeKeyTargets.arraySize;
                    shapeKeyTargets.InsertArrayElementAtIndex(idx);
                    shapeKeyTargets.GetArrayElementAtIndex(idx).objectReferenceValue = o;
                 }
            }
        });
    }

    private void SyncShapeKeyData(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData)
    {
        while (layerData.perButtonShapeKeyStates.Count < layerData.targets.Count)
            layerData.perButtonShapeKeyStates.Add(new DiNeMultiDresser.ShapeKeyMeshList());

        foreach (var buttonState in layerData.perButtonShapeKeyStates)
        {
            while (buttonState.meshShapeKeys.Count < gen.shapeKeyTargets.Count)
                buttonState.meshShapeKeys.Add(new DiNeMultiDresser.ShapeKeyList());

            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                var meshObj = gen.shapeKeyTargets[m];
                if (meshObj == null) continue;
                
                var skinned = meshObj.GetComponent<SkinnedMeshRenderer>();
                if (skinned == null || skinned.sharedMesh == null) continue;

                var savedList = buttonState.meshShapeKeys[m].shapeKeys;
                for(int s=0; s<skinned.sharedMesh.blendShapeCount; s++) 
                {
                    string realName = skinned.sharedMesh.GetBlendShapeName(s);
                    if (!savedList.Exists(x => x.name == realName))
                        savedList.Add(new DiNeMultiDresser.ShapeKeyState { name = realName, value = 0 });
                }
            }
        }
    }

    private void DrawPerButtonShapeKeyUI(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData, int buttonIdx, int layerIdx, Dictionary<string, string> lang)
    {
        if (gen.shapeKeyTargets.Count == 0) return;
        
        string foldoutKey = $"DiNe_SK_{layerIdx}_{buttonIdx}";
        bool foldout = EditorPrefs.GetBool(foldoutKey, false);
        foldout = EditorGUILayout.Foldout(foldout, lang["skSettings"]);
        EditorPrefs.SetBool(foldoutKey, foldout);

        if (foldout)
        {
            if (buttonIdx >= layerData.perButtonShapeKeyStates.Count) return;
            var buttonState = layerData.perButtonShapeKeyStates[buttonIdx];

            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                var mesh = gen.shapeKeyTargets[m];
                if (mesh == null) continue;
                if (m >= buttonState.meshShapeKeys.Count) continue;

                EditorGUILayout.LabelField($"[{mesh.name}]", EditorStyles.miniLabel);
                var keys = buttonState.meshShapeKeys[m].shapeKeys;
                
                for (int k = 0; k < keys.Count; k++)
                {
                    var key = keys[k];
                    float newVal = EditorGUILayout.Slider(key.name, key.value, 0, 100);
                    if (newVal != key.value)
                    {
                        key.value = newVal;
                        key.everRecorded = true;
                        keys[k] = key;
                    }
                }
            }
        }
    }

    private void HandleDragDrop(Rect dropArea, System.Action<List<GameObject>> onDrop)
    {
        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                List<GameObject> dropped = new List<GameObject>();
                foreach (var obj in DragAndDrop.objectReferences)
                    if (obj is GameObject go) dropped.Add(go);
                onDrop?.Invoke(dropped);
                evt.Use();
            }
        }
    }

    private void DrawHeader(string titleText)
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label) { font = titleFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 28 };
        GUIContent content = new GUIContent(titleText, windowIcon);
        GUILayout.Label(content, titleStyle);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }
}
#endif