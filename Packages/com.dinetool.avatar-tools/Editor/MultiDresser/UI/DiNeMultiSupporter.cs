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
    private int draggedItemIndex = -1;
    private int dragTargetIndex  = -1;
    private readonly List<Rect> itemRects = new List<Rect>();

    private int previewLayerIndex  = -1;
    private int previewButtonIndex = -1;
    private readonly List<System.Action> previewRestoreActions = new List<System.Action>();
    private string pendingProfileName = "";
    
    private enum Language { English, Korean, Japanese }
    private static readonly string[] LangButtonLabels = { "English", "한국어", "日本語" };
    
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
                { "shapeKeyTargets", "쉐이프키 타겟 (바디 메쉬) 🦋" },
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
                { "catIcon", "아이콘" },
                { "fxController", "FX 컨트롤러" },
                { "expressionMenu", "익스프레션 메뉴" },
                { "newLayer", "새 레이어" },
                { "autoApplyHint", "플레이 모드 진입 및 아바타 업로드 시 전체 설정이 자동으로 생성/적용됩니다." },
                { "cleanupDialogTitle", "데이터 초기화" },
                { "cleanupDialogMsg", "드레서에 설정된 모든 데이터(레이어, 오브젝트, 쉐이프키)를 초기화합니다.\n이 작업은 되돌릴 수 없습니다." },
                { "cleanupDialogOk", "초기화 (Yes)" },
                { "cleanupDialogCancel", "취소 (No)" },
                { "particle",   "파티클 오브젝트" },
                { "matSwap",    "마테리얼 교체" },
                { "addMatSwap", "+ 추가" },
                { "presetSection",  "💾 프리셋" },
                { "presetCurrent",  "현재 프리셋" },
                { "presetName",     "이름" },
                { "presetSave",     "저장" },
                { "presetSaveAs",   "다른 이름으로 저장" },
                { "presetLoad",     "불러오기" },
                { "presetLoaded",   "프리셋을 불러왔습니다." },
                { "presetSaved",    "프리셋이 저장됐습니다." },
                { "presetNone",     "없음 (저장하면 자동 생성)" },
                { "presetNameEmpty","이름을 입력해주세요." }
            }
        },
        {
            Language.English, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "Global Settings (Avatar & FX & Menu)" },
                { "avatarRoot", "Avatar Root" },
                { "refreshTooltip", "Reload Avatar & Restore Settings (Refresh)" },
                { "shapeKeyTargets", "Shape Key Targets (Body Meshes) 🦋" },
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
                { "catIcon", "Icon" },
                { "fxController", "FX Controller" },
                { "expressionMenu", "Expression Menu" },
                { "newLayer", "New Layer" },
                { "autoApplyHint", "All settings will be automatically generated/applied when entering Play Mode or uploading the avatar." },
                { "cleanupDialogTitle", "Reset Data" },
                { "cleanupDialogMsg", "This will clear all dresser settings (layers, objects, shape keys).\nThis action cannot be undone." },
                { "cleanupDialogOk", "Reset (Yes)" },
                { "cleanupDialogCancel", "Cancel (No)" },
                { "particle",   "Particle Object" },
                { "matSwap",    "Material Swap" },
                { "addMatSwap", "+ Add" },
                { "presetSection",  "💾 Preset" },
                { "presetCurrent",  "Current Preset" },
                { "presetName",     "Name" },
                { "presetSave",     "Save" },
                { "presetSaveAs",   "Save As..." },
                { "presetLoad",     "Load" },
                { "presetLoaded",   "Preset loaded." },
                { "presetSaved",    "Preset saved." },
                { "presetNone",     "None (auto-created on save)" },
                { "presetNameEmpty","Please enter a name." }
            }
        },
        {
            Language.Japanese, new Dictionary<string, string>
            {
                { "title", "🌸 DiNe Multi Dresser 🌸" },
                { "globalSettings", "基本設定 (アバター & FX & メニュー)" },
                { "avatarRoot", "アバター Root" },
                { "refreshTooltip", "アバター再検索 & 設定復元 (更新)" },
                { "shapeKeyTargets", "シェイプキー対象 (体メッシュ) 🦋" },
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
                { "catIcon", "アイコン" },
                { "fxController", "FX コントローラー" },
                { "expressionMenu", "表情メニュー" },
                { "newLayer", "新しいレイヤー" },
                { "autoApplyHint", "プレイモード開始またはアバターアップロード時に全設定が自動生成/適用されます。" },
                { "cleanupDialogTitle", "データ初期化" },
                { "cleanupDialogMsg", "ドレッサーに設定された全データ（レイヤー、オブジェクト、シェイプキー）を初期化します。\nこの操作は元に戻せません。" },
                { "cleanupDialogOk", "初期化 (Yes)" },
                { "cleanupDialogCancel", "キャンセル (No)" },
                { "particle",   "パーティクル" },
                { "matSwap",    "マテリアル交換" },
                { "addMatSwap", "+ 追加" },
                { "presetSection",  "💾 プリセット" },
                { "presetCurrent",  "現在のプリセット" },
                { "presetName",     "名前" },
                { "presetSave",     "保存" },
                { "presetSaveAs",   "別名で保存" },
                { "presetLoad",     "読込" },
                { "presetLoaded",   "プリセットを読み込みました。" },
                { "presetSaved",    "プリセットを保存しました。" },
                { "presetNone",     "なし (保存時に自動作成)" },
                { "presetNameEmpty","名前を入力してください。" }
            }
        }
    };

    private void OnDisable()
    {
        ClearPreview();
        // 인스펙터 비활성화 시 프로필 자동 저장 (도메인 리로드·플레이모드 진입 전 최신값 보존)
        var gen = target as DiNeMultiDresser;
        if (gen != null && gen.layers.Count > 0)
            gen.SaveProfile();
    }

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

        DrawHeader("Multi Dresser");
        
        GUILayout.Space(5);
        int langIndex = DrawCustomToolbar((int)currentLanguage, LangButtonLabels, 35); 
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
        GUILayout.Label(lang["fxController"], GUILayout.Width(110));
        controller.objectReferenceValue = EditorGUILayout.ObjectField(controller.objectReferenceValue, typeof(RuntimeAnimatorController), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(lang["expressionMenu"], GUILayout.Width(110));
        exMenu.objectReferenceValue = EditorGUILayout.ObjectField(exMenu.objectReferenceValue, typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu), false);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField(lang["shapeKeyTargets"], EditorStyles.boldLabel);
        DrawGlobalShapeKeyTargets(shapeKeyTargets, lang);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── 프리셋 섹션 ──────────────────────────────────────────────
        DrawPresetUI(gen, lang);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(lang["layerCategory"], EditorStyles.boldLabel);

        if (layers.arraySize == 0)
        {
            // 빈 레이어면 프로필 복구 먼저 시도 (플레이 모드 후 유실 케이스 포함)
            gen.TryRestoreFromProfile();
            serializedObject.Update();

            if (layers.arraySize == 0)
            {
                // 프로필도 없으면 기본 레이어 생성
                layers.InsertArrayElementAtIndex(0);
                layers.GetArrayElementAtIndex(0).FindPropertyRelative("layerName").stringValue = "Main";
                serializedObject.ApplyModifiedProperties();
                gen.layers[0].EnsureSize(0);
            }
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
        selectedLayerIndex = DrawCustomToolbar(selectedLayerIndex, tabNames.ToArray(), 35);

        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button("+", GUILayout.Width(40), GUILayout.Height(35)))
        {
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var newLayerProp = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            newLayerProp.FindPropertyRelative("layerName").stringValue = lang["newLayer"];
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
        
        // [자동 적용 힌트]
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        EditorGUILayout.HelpBox(lang["autoApplyHint"], MessageType.Info);
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        // [삭제 버튼] - 붉은색 경고 느낌
        GUI.backgroundColor = new Color(0.60f, 0.25f, 0.25f);
        var cleanBtnStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        if (GUILayout.Button(lang["cleanup"], cleanBtnStyle))
        {
            if (EditorUtility.DisplayDialog(lang["cleanupDialogTitle"], lang["cleanupDialogMsg"], lang["cleanupDialogOk"], lang["cleanupDialogCancel"]))
            {
                Undo.RecordObject(gen, "Clear Multi Dresser Data");
                gen.layers.Clear();
                gen.shapeKeyTargets.Clear();
                selectedLayerIndex = 0;
                EditorUtility.SetDirty(gen);
                serializedObject.Update();
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

        // 레이어 전환 시 미리보기 해제
        if (previewLayerIndex >= 0 && previewLayerIndex != index) ClearPreview();

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

        EditorGUILayout.Space(8);

        // 파티클 오브젝트 (레이어 공통)
        SerializedProperty particleProp = layerProp.FindPropertyRelative("particleObject");
        EditorGUILayout.BeginHorizontal("helpBox");
        GUILayout.Label(lang["particle"], GUILayout.Width(120));
        particleProp.objectReferenceValue = EditorGUILayout.ObjectField(particleProp.objectReferenceValue, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        Event evt = Event.current;
        if (evt.type == EventType.Repaint) itemRects.Clear();

        for (int i = 0; i < targets.arraySize; i++)
        {
            SerializedProperty t    = targets.GetArrayElementAtIndex(i);
            SerializedProperty l    = labels.GetArrayElementAtIndex(i);
            SerializedProperty icon = icons.GetArrayElementAtIndex(i);

            // ── 삽입 표시줄 ──
            Rect insertRect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
            if (evt.type == EventType.Repaint && draggedItemIndex >= 0 && dragTargetIndex == i)
                EditorGUI.DrawRect(new Rect(insertRect.x, insertRect.y + 1, insertRect.width, 2),
                                   new Color(0.3f, 0.82f, 0.9f));

            // ── 아이템 helpBox ──
            EditorGUILayout.BeginVertical("helpBox");

            // 헤더 행: [grip] [레이블] [X]
            EditorGUILayout.BeginHorizontal();

            Rect handleRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18), GUILayout.Height(18));
            if (evt.type == EventType.Repaint)
            {
                Color lc = (draggedItemIndex == i)
                    ? new Color(0.3f, 0.82f, 0.9f)
                    : new Color(0.6f, 0.6f, 0.6f);
                float lx = handleRect.x + 2f;
                float ly = handleRect.center.y - 3f;
                EditorGUI.DrawRect(new Rect(lx, ly,      13, 1.5f), lc);
                EditorGUI.DrawRect(new Rect(lx, ly + 3f, 13, 1.5f), lc);
                EditorGUI.DrawRect(new Rect(lx, ly + 6f, 13, 1.5f), lc);
            }
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);

            string headerLabel = (i == 0) ? lang["defaultState"] : $"{lang["menuButton"]} {i}";
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);

            // ── 미리보기 토글 ──
            bool isPreviewing = (previewLayerIndex == index && previewButtonIndex == i);
            GUI.backgroundColor = isPreviewing ? new Color(0.3f, 0.82f, 0.9f) : new Color(0.55f, 0.55f, 0.55f);
            GUIStyle previewStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, normal = { textColor = Color.white } };
            if (GUILayout.Button("미리보기", previewStyle, GUILayout.Width(60), GUILayout.Height(20)))
            {
                if (isPreviewing) ClearPreview();
                else { ClearPreview(); ApplyPreview(gen, currentLayerData, i, index); }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("X", GUILayout.Width(30), GUILayout.Height(20)))
            {
                ClearPreview();
                currentLayerData.RemoveAt(i);
                EditorUtility.SetDirty(target);
                serializedObject.Update();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();

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
            DrawPerButtonMaterialSwapUI(currentLayerData, i, index, lang);

            if (currentLayerData.linkedObjects.Count > i)
            {
                int capturedI = i;
                EditorGUILayout.LabelField(lang["linkedObj"]);
                for (int j = 0; j < currentLayerData.linkedObjects[i].objects.Count; j++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    currentLayerData.linkedObjects[i].objects[j] = (GameObject)EditorGUILayout.ObjectField(currentLayerData.linkedObjects[i].objects[j], typeof(GameObject), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        if (previewLayerIndex == index && previewButtonIndex == i)
                            RefreshPreview(gen, currentLayerData);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        currentLayerData.linkedObjects[i].objects.RemoveAt(j);
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        if (previewLayerIndex == index && previewButtonIndex == i)
                            RefreshPreview(gen, currentLayerData);
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
                    foreach(var o in objs) currentLayerData.linkedObjects[capturedI].objects.Add(o);
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();
                    if (previewLayerIndex == index && previewButtonIndex == capturedI)
                        RefreshPreview(gen, currentLayerData);
                });
            }

            // 변경 감지 → 미리보기 즉시 갱신
            if (EditorGUI.EndChangeCheck() && previewLayerIndex == index && previewButtonIndex == i)
            {
                serializedObject.ApplyModifiedProperties();
                RefreshPreview(gen, currentLayerData);
            }

            EditorGUILayout.EndVertical(); // helpBox 끝

            // 아이템 rect 저장
            if (evt.type == EventType.Repaint)
                itemRects.Add(GUILayoutUtility.GetLastRect());

            // 핸들 클릭 → 드래그 시작
            if (evt.type == EventType.MouseDown && handleRect.Contains(evt.mousePosition))
            {
                draggedItemIndex = i;
                dragTargetIndex  = i;
                evt.Use();
            }
        }

        // ── 마지막 아이템 뒤 삽입 표시줄 ──
        Rect lastInsertRect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
        if (evt.type == EventType.Repaint && draggedItemIndex >= 0 && dragTargetIndex == targets.arraySize)
            EditorGUI.DrawRect(new Rect(lastInsertRect.x + 28, lastInsertRect.y + 1, lastInsertRect.width - 28, 2),
                               new Color(0.3f, 0.82f, 0.9f));

        // ── 의상 추가 드래그 영역 (하단) ──
        EditorGUILayout.Space(4);
        GUIStyle dragHintStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize  = 13,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        Color dropOrigColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
        GUI.Box(dropArea, lang["mainDragHint"], dragHintStyle);
        GUI.backgroundColor = dropOrigColor;

        HandleDragDrop(dropArea, (objs) => {
            foreach (var go in objs) {
                currentLayerData.targets.Add(go);
                currentLayerData.labels.Add(go.name);
                currentLayerData.icons.Add(null);
                currentLayerData.linkedObjects.Add(new DiNeMultiDresser.LinkedGroup());
                currentLayerData.perButtonShapeKeyStates.Add(new DiNeMultiDresser.ShapeKeyMeshList());
                currentLayerData.perButtonMaterialSwaps.Add(new DiNeMultiDresser.MaterialSwapList());
            }
            SyncShapeKeyData(gen, currentLayerData);
            EditorUtility.SetDirty(target);
            serializedObject.Update();
        });

        EditorGUILayout.Space(4);

        // ── 드래그 이벤트 처리 ──
        if (draggedItemIndex >= 0)
        {
            if (evt.type == EventType.MouseDrag)
            {
                float mouseY = evt.mousePosition.y;
                dragTargetIndex = itemRects.Count;
                for (int i = 0; i < itemRects.Count; i++)
                {
                    if (mouseY < itemRects[i].center.y)
                    {
                        dragTargetIndex = i;
                        break;
                    }
                }
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                int from     = draggedItemIndex;
                int to       = dragTargetIndex;
                int actualTo = (to > from) ? to - 1 : to;

                if (actualTo != from && actualTo >= 0 && actualTo < targets.arraySize)
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(gen, "Reorder Dresser Items");
                    MoveItem(currentLayerData, from, actualTo);
                    EditorUtility.SetDirty(gen);
                    serializedObject.Update();
                }

                draggedItemIndex = -1;
                dragTargetIndex  = -1;
                Repaint();
                evt.Use();
            }

            EditorGUIUtility.AddCursorRect(
                new Rect(0, 0, EditorGUIUtility.currentViewWidth, Screen.height),
                MouseCursor.Pan);
        }
    }
    
    private void SwapItems(DiNeMultiDresser.DresserLayer layerData, int indexA, int indexB)
    {
        if (indexA < 0 || indexB < 0 || indexA >= layerData.targets.Count || indexB >= layerData.targets.Count) return;

        // Swap targets
        var tempTarget = layerData.targets[indexA];
        layerData.targets[indexA] = layerData.targets[indexB];
        layerData.targets[indexB] = tempTarget;

        // Swap labels
        var tempLabel = layerData.labels[indexA];
        layerData.labels[indexA] = layerData.labels[indexB];
        layerData.labels[indexB] = tempLabel;

        // Swap icons
        var tempIcon = layerData.icons[indexA];
        layerData.icons[indexA] = layerData.icons[indexB];
        layerData.icons[indexB] = tempIcon;

        // Swap linkedObjects
        if (indexA < layerData.linkedObjects.Count && indexB < layerData.linkedObjects.Count)
        {
            var tempLinked = layerData.linkedObjects[indexA];
            layerData.linkedObjects[indexA] = layerData.linkedObjects[indexB];
            layerData.linkedObjects[indexB] = tempLinked;
        }

        // Swap perButtonShapeKeyStates
        if (indexA < layerData.perButtonShapeKeyStates.Count && indexB < layerData.perButtonShapeKeyStates.Count)
        {
            var tempShape = layerData.perButtonShapeKeyStates[indexA];
            layerData.perButtonShapeKeyStates[indexA] = layerData.perButtonShapeKeyStates[indexB];
            layerData.perButtonShapeKeyStates[indexB] = tempShape;
        }
    }

    private void MoveItem(DiNeMultiDresser.DresserLayer layerData, int from, int to)
    {
        MoveInList(layerData.targets,                from, to);
        MoveInList(layerData.labels,                 from, to);
        MoveInList(layerData.icons,                  from, to);
        MoveInList(layerData.linkedObjects,           from, to);
        MoveInList(layerData.perButtonShapeKeyStates, from, to);
        MoveInList(layerData.perButtonMaterialSwaps,  from, to);
    }

    private void MoveInList<T>(List<T> list, int from, int to)
    {
        if (from < 0 || to < 0 || from >= list.Count || to >= list.Count) return;
        T item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
    }

    private void ApplyPreview(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData, int buttonIdx, int layerIdx)
    {
        previewLayerIndex  = layerIdx;
        previewButtonIndex = buttonIdx;
        previewRestoreActions.Clear();

        // ── 메인 타겟 오브젝트 ──
        for (int j = 0; j < layerData.targets.Count; j++)
        {
            var go = layerData.targets[j];
            if (go == null) continue;
            bool was  = go.activeSelf;
            bool next = (j == buttonIdx);
            previewRestoreActions.Add(() => { if (go != null) SafeSetActive(go, was); });
            SafeSetActive(go, next);
        }

        // ── 링크 오브젝트 (애니메이션 생성 로직과 동일) ──
        var linkedMap = new Dictionary<GameObject, bool>();
        for (int j = 0; j < layerData.linkedObjects.Count; j++)
        {
            if (layerData.linkedObjects[j] == null) continue;
            foreach (var linkObj in layerData.linkedObjects[j].objects)
            {
                if (linkObj == null) continue;
                if (j == buttonIdx)               linkedMap[linkObj] = true;
                else if (!linkedMap.ContainsKey(linkObj)) linkedMap[linkObj] = false;
            }
        }
        foreach (var kvp in linkedMap)
        {
            var go    = kvp.Key;
            bool was  = go.activeSelf;
            bool next = kvp.Value;
            previewRestoreActions.Add(() => { if (go != null) SafeSetActive(go, was); });
            SafeSetActive(go, next);
        }

        // ── 쉐이프키 ──
        // 레이어 내 어느 버튼에서든 한 번이라도 everRecorded된 키만 "관리 대상"으로 수집
        var managedKeys = new Dictionary<int, HashSet<string>>(); // meshIndex → keyName set
        for (int j = 0; j < layerData.perButtonShapeKeyStates.Count; j++)
        {
            var bs = layerData.perButtonShapeKeyStates[j];
            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                if (m >= bs.meshShapeKeys.Count) continue;
                if (!managedKeys.ContainsKey(m)) managedKeys[m] = new HashSet<string>();
                foreach (var sk in bs.meshShapeKeys[m].shapeKeys)
                    if (sk.everRecorded) managedKeys[m].Add(sk.name);
            }
        }

        var currentBtnState = buttonIdx < layerData.perButtonShapeKeyStates.Count
            ? layerData.perButtonShapeKeyStates[buttonIdx] : null;

        foreach (var kvp in managedKeys)
        {
            int m = kvp.Key;
            var meshObj = gen.shapeKeyTargets[m];
            if (meshObj == null) continue;
            var smr = meshObj.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            foreach (var skName in kvp.Value)
            {
                int skIdx = smr.sharedMesh.GetBlendShapeIndex(skName);
                if (skIdx < 0) continue;

                var   capturedSmr = smr;
                int   capturedIdx = skIdx;
                float was         = smr.GetBlendShapeWeight(skIdx);
                previewRestoreActions.Add(() => { if (capturedSmr != null) capturedSmr.SetBlendShapeWeight(capturedIdx, was); });

                // 현재 버튼에서 이 키가 everRecorded면 그 값, 아니면 0
                float targetValue = 0f;
                if (currentBtnState != null && m < currentBtnState.meshShapeKeys.Count)
                {
                    var found = currentBtnState.meshShapeKeys[m].shapeKeys.Find(k => k.name == skName);
                    if (found.name != null && found.everRecorded) targetValue = found.value;
                }
                smr.SetBlendShapeWeight(skIdx, targetValue);
            }
        }

        // ── 머티리얼 교체 ──
        if (buttonIdx < layerData.perButtonMaterialSwaps.Count)
        {
            var swapList = layerData.perButtonMaterialSwaps[buttonIdx];
            foreach (var entry in swapList.entries)
            {
                var rend = entry.renderer;
                if (rend == null) continue;
                var capturedRend = rend;
                var wasMats = rend.sharedMaterials;  // 원본 배열 캡처
                previewRestoreActions.Add(() => { if (capturedRend != null) capturedRend.sharedMaterials = wasMats; });

                // entry.materials 슬롯 수만큼 교체 (나머지 슬롯은 원본 유지)
                var newMats = (Material[])wasMats.Clone();
                for (int si = 0; si < entry.materials.Count && si < newMats.Length; si++)
                {
                    if (entry.materials[si] != null)
                        newMats[si] = entry.materials[si];
                }
                rend.sharedMaterials = newMats;
            }
        }
    }

    // VRC SDK가 에디터에서 SetActive 시 뱉는 MissingReferenceException 억제
    private static void SafeSetActive(GameObject go, bool active)
    {
        try { go.SetActive(active); }
        catch (System.Exception) { /* VRC 내부 stale 참조 — 무시 */ }
    }

    private void ClearPreview()
    {
        foreach (var action in previewRestoreActions) action?.Invoke();
        previewRestoreActions.Clear();
        previewLayerIndex  = -1;
        previewButtonIndex = -1;
    }

    // 원상태 저장 없이 현재 데이터를 다시 아바타에 적용 (미리보기 중 실시간 갱신용)
    private void RefreshPreview(DiNeMultiDresser gen, DiNeMultiDresser.DresserLayer layerData)
    {
        int buttonIdx = previewButtonIndex;

        // 메인 타겟
        for (int j = 0; j < layerData.targets.Count; j++)
        {
            var go = layerData.targets[j];
            if (go != null) SafeSetActive(go, j == buttonIdx);
        }

        // 링크 오브젝트
        var linkedMap = new Dictionary<GameObject, bool>();
        for (int j = 0; j < layerData.linkedObjects.Count; j++)
        {
            if (layerData.linkedObjects[j] == null) continue;
            foreach (var linkObj in layerData.linkedObjects[j].objects)
            {
                if (linkObj == null) continue;
                if (j == buttonIdx)                      linkedMap[linkObj] = true;
                else if (!linkedMap.ContainsKey(linkObj)) linkedMap[linkObj] = false;
            }
        }
        foreach (var kvp in linkedMap)
            if (kvp.Key != null) SafeSetActive(kvp.Key, kvp.Value);

        // 쉐이프키 — 관리 대상 키(어느 버튼이든 everRecorded된 것)만 갱신
        var refreshManaged = new Dictionary<int, HashSet<string>>();
        for (int j = 0; j < layerData.perButtonShapeKeyStates.Count; j++)
        {
            var bs = layerData.perButtonShapeKeyStates[j];
            for (int m = 0; m < gen.shapeKeyTargets.Count; m++)
            {
                if (m >= bs.meshShapeKeys.Count) continue;
                if (!refreshManaged.ContainsKey(m)) refreshManaged[m] = new HashSet<string>();
                foreach (var sk in bs.meshShapeKeys[m].shapeKeys)
                    if (sk.everRecorded) refreshManaged[m].Add(sk.name);
            }
        }

        var refreshBtnState = buttonIdx < layerData.perButtonShapeKeyStates.Count
            ? layerData.perButtonShapeKeyStates[buttonIdx] : null;

        foreach (var kvp in refreshManaged)
        {
            int m = kvp.Key;
            var meshObj = gen.shapeKeyTargets[m];
            if (meshObj == null) continue;
            var smr = meshObj.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            foreach (var skName in kvp.Value)
            {
                int skIdx = smr.sharedMesh.GetBlendShapeIndex(skName);
                if (skIdx < 0) continue;

                float targetValue = 0f;
                if (refreshBtnState != null && m < refreshBtnState.meshShapeKeys.Count)
                {
                    var found = refreshBtnState.meshShapeKeys[m].shapeKeys.Find(k => k.name == skName);
                    if (found.name != null && found.everRecorded) targetValue = found.value;
                }
                smr.SetBlendShapeWeight(skIdx, targetValue);
            }
        }

        // 머티리얼 교체 갱신
        if (buttonIdx < layerData.perButtonMaterialSwaps.Count)
        {
            var swapList = layerData.perButtonMaterialSwaps[buttonIdx];
            foreach (var entry in swapList.entries)
            {
                var rend = entry.renderer;
                if (rend == null) continue;
                // previewRestoreActions에 저장된 원본으로 일단 되돌린 뒤 재적용
                var baseMats = (Material[])rend.sharedMaterials.Clone();
                var newMats  = baseMats;
                for (int si = 0; si < entry.materials.Count && si < newMats.Length; si++)
                {
                    if (entry.materials[si] != null)
                        newMats[si] = entry.materials[si];
                }
                rend.sharedMaterials = newMats;
            }
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

    private void DrawPerButtonMaterialSwapUI(DiNeMultiDresser.DresserLayer layerData, int buttonIdx, int layerIdx, Dictionary<string, string> lang)
    {
        while (layerData.perButtonMaterialSwaps.Count <= buttonIdx)
            layerData.perButtonMaterialSwaps.Add(new DiNeMultiDresser.MaterialSwapList());

        var swapList = layerData.perButtonMaterialSwaps[buttonIdx];

        string foldoutKey = $"DiNe_MS_{layerIdx}_{buttonIdx}";
        bool foldout = EditorPrefs.GetBool(foldoutKey, false);

        EditorGUILayout.BeginHorizontal();
        foldout = EditorGUILayout.Foldout(foldout, lang["matSwap"], true);
        GUI.backgroundColor = new Color(0.7f, 0.9f, 0.7f);
        if (GUILayout.Button(lang["addMatSwap"], GUILayout.Width(60), GUILayout.Height(16)))
        {
            swapList.entries.Add(new DiNeMultiDresser.MaterialSwapEntry());
            foldout = true;
            EditorUtility.SetDirty(target);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorPrefs.SetBool(foldoutKey, foldout);

        if (!foldout || swapList.entries.Count == 0) return;

        EditorGUI.indentLevel++;
        for (int e = 0; e < swapList.entries.Count; e++)
        {
            var entry = swapList.entries[e];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Renderer 행
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            entry.renderer = (Renderer)EditorGUILayout.ObjectField(entry.renderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck() && entry.renderer != null)
            {
                entry.materials.Clear();
                foreach (var m in entry.renderer.sharedMaterials) entry.materials.Add(m);
                EditorUtility.SetDirty(target);
            }
            if (GUILayout.Button("−", GUILayout.Width(22)))
            {
                swapList.entries.RemoveAt(e);
                EditorUtility.SetDirty(target);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // 마테리얼 슬롯
            for (int mi = 0; mi < entry.materials.Count; mi++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"[{mi}]", GUILayout.Width(24));
                EditorGUI.BeginChangeCheck();
                entry.materials[mi] = (Material)EditorGUILayout.ObjectField(entry.materials[mi], typeof(Material), false);
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(target);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
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
                        Undo.RecordObject(target, "Change Shape Key");
                        key.value = newVal;
                        key.everRecorded = true;
                        keys[k] = key;
                        EditorUtility.SetDirty(target);
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
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label) { font = titleFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 36 };
        float iconSize = 72f;
        GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label(titleText, titleStyle, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = "";
        switch (currentLanguage)
        {
            case Language.Korean: desc = "여러 개의 의상과 액세서리를 손쉽게 켜고 끌 수 있는 FX 토글을 생성합니다."; break;
            case Language.Japanese: desc = "複数の衣装やアクセサリーを簡単に切り替えるFXトグルを生成します。"; break;
            default: desc = "Generates FX toggles to easily turn multiple clothing and accessories on/off."; break;
        }
        GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel) 
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
    }

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button) { 
                fontStyle = (i == selected) ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal = { textColor = (i == selected) ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
            {
                newSelected = i;
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }

    private void DrawPresetUI(DiNeMultiDresser gen, Dictionary<string, string> lang)
    {
        SerializedProperty savedProfileProp = serializedObject.FindProperty("savedProfile");

        EditorGUILayout.BeginVertical("GroupBox");
        EditorGUILayout.LabelField(lang["presetSection"], EditorStyles.boldLabel);

        // ── 현재 프리셋 ObjectField ──
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(lang["presetCurrent"], GUILayout.Width(90));
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(savedProfileProp, GUIContent.none);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            // 다른 프리셋이 드래그됐을 때 즉시 불러오기
            if (savedProfileProp.objectReferenceValue != null)
            {
                gen.TryRestoreFromProfile();
                serializedObject.Update();
                EditorUtility.DisplayDialog("DiNe", lang["presetLoaded"], "OK");
            }
        }

        // 불러오기 버튼 (현재 프리셋이 있을 때)
        if (savedProfileProp.objectReferenceValue != null)
        {
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button(lang["presetLoad"], GUILayout.Width(60)))
            {
                gen.TryRestoreFromProfile();
                serializedObject.Update();
                EditorUtility.DisplayDialog("DiNe", lang["presetLoaded"], "OK");
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        // ── 저장 행 ──
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(lang["presetName"], GUILayout.Width(90));
        pendingProfileName = EditorGUILayout.TextField(pendingProfileName);

        // 현재 프리셋이 있으면 "저장 (덮어쓰기)" 버튼
        if (savedProfileProp.objectReferenceValue != null)
        {
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button(lang["presetSave"], GUILayout.Width(50)))
            {
                gen.SaveProfile();
                serializedObject.Update();
                EditorUtility.DisplayDialog("DiNe", lang["presetSaved"], "OK");
            }
            GUI.backgroundColor = Color.white;
        }

        // "다른 이름으로 저장" 버튼
        GUI.backgroundColor = new Color(0.9f, 0.75f, 0.4f);
        if (GUILayout.Button(lang["presetSaveAs"], GUILayout.Width(savedProfileProp.objectReferenceValue != null ? 120 : 80)))
        {
            string trimmed = pendingProfileName.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                EditorUtility.DisplayDialog("DiNe", lang["presetNameEmpty"], "OK");
            }
            else
            {
                gen.SaveProfileAs(trimmed);
                serializedObject.Update();
                pendingProfileName = "";
                EditorUtility.DisplayDialog("DiNe", lang["presetSaved"], "OK");
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
#endif
