#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(DiNeRemoveMeshInBox))]
public class DiNeRemoveMeshInBoxEditor : Editor
{
    private enum LanguagePreset { English, Korean, Japanese }

    private static readonly Color ColAccent = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColLine = new Color(0.30f, 0.30f, 0.35f, 0.8f);
    private static readonly Color BoxWire = new Color(0.95f, 0.40f, 0.40f, 0.9f);

    private LanguagePreset _language = LanguagePreset.Korean;
    private Texture2D _windowIcon;
    private Font _titleFont;

    private SerializedProperty _removeInBox;
    private SerializedProperty _boxes;
    private readonly BoxBoundsHandle _boxHandle = new BoxBoundsHandle();

    private string L(string en, string ko, string ja)
    {
        switch (_language)
        {
            case LanguagePreset.Korean: return ko;
            case LanguagePreset.Japanese: return ja;
            default: return en;
        }
    }

    private void OnEnable()
    {
        _windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        _titleFont = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
        _removeInBox = serializedObject.FindProperty("removeInBox");
        _boxes = serializedObject.FindProperty("boxes");
        if (EditorPrefs.HasKey("DiNeOpticore_ComponentLang"))
            _language = (LanguagePreset)EditorPrefs.GetInt("DiNeOpticore_ComponentLang");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeaderBar();
        DrawLanguageBar();
        DrawHorizontalLine();

        if (((DiNeRemoveMeshInBox)target).GetComponent<Renderer>() == null)
        {
            EditorGUILayout.HelpBox(
                L("Add this to a GameObject with a SkinnedMeshRenderer or MeshRenderer.",
                  "SkinnedMeshRenderer 또는 MeshRenderer가 있는 오브젝트에 추가하세요.",
                  "SkinnedMeshRenderer または MeshRenderer があるオブジェクトに追加してください。"),
                MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        _removeInBox.boolValue = EditorGUILayout.ToggleLeft(
            _removeInBox.boolValue
                ? L("Remove polygons inside the boxes", "박스 안의 폴리곤 제거", "ボックス内のポリゴンを削除")
                : L("Remove polygons NOT inside any box", "박스 밖의 폴리곤 제거", "ボックス外のポリゴンを削除"),
            _removeInBox.boolValue);

        GUILayout.Space(4f);
        GUILayout.Label(L("Boxes (renderer local space)", "박스 (렌더러 로컬 공간)", "ボックス (レンダラーローカル空間)"),
            EditorStyles.boldLabel);

        int removeIndex = -1;
        for (int i = 0; i < _boxes.arraySize; i++)
        {
            SerializedProperty box = _boxes.GetArrayElementAtIndex(i);
            SerializedProperty center = box.FindPropertyRelative("center");
            SerializedProperty size = box.FindPropertyRelative("size");
            SerializedProperty rotation = box.FindPropertyRelative("rotation");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"#{i + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(28f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(24f)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();

            center.vector3Value = EditorGUILayout.Vector3Field(L("Center", "중심", "中心"), center.vector3Value);
            size.vector3Value = EditorGUILayout.Vector3Field(L("Size", "크기", "サイズ"), size.vector3Value);
            Vector3 euler = rotation.quaternionValue.eulerAngles;
            EditorGUI.BeginChangeCheck();
            euler = EditorGUILayout.Vector3Field(L("Rotation", "회전", "回転"), euler);
            if (EditorGUI.EndChangeCheck())
                rotation.quaternionValue = Quaternion.Euler(euler);
            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
            _boxes.DeleteArrayElementAtIndex(removeIndex);

        GUILayout.Space(4f);
        if (GUILayout.Button(L("Add Box", "박스 추가", "ボックス追加")))
        {
            int idx = _boxes.arraySize;
            _boxes.arraySize++;
            SerializedProperty box = _boxes.GetArrayElementAtIndex(idx);
            box.FindPropertyRelative("center").vector3Value = Vector3.zero;
            box.FindPropertyRelative("size").vector3Value = Vector3.one * 0.3f;
            box.FindPropertyRelative("rotation").quaternionValue = Quaternion.identity;
        }

        EditorGUILayout.HelpBox(
            L("Applied non-destructively at build (and on play) by the Opticore pipeline. Drag the box handles in the Scene view.",
              "Opticore 파이프라인이 빌드 시(그리고 플레이 시) 비파괴적으로 적용합니다. 씬 뷰에서 박스 핸들을 드래그하세요.",
              "Opticore パイプラインがビルド時（および再生時）に非破壊で適用します。シーンビューでボックスハンドルをドラッグしてください。"),
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        var component = (DiNeRemoveMeshInBox)target;
        var renderer = component.GetComponent<Renderer>();
        if (renderer == null)
            return;

        serializedObject.Update();
        Matrix4x4 rendererToWorld = renderer.transform.localToWorldMatrix;

        for (int i = 0; i < _boxes.arraySize; i++)
        {
            SerializedProperty box = _boxes.GetArrayElementAtIndex(i);
            SerializedProperty centerProp = box.FindPropertyRelative("center");
            SerializedProperty sizeProp = box.FindPropertyRelative("size");
            SerializedProperty rotationProp = box.FindPropertyRelative("rotation");

            Vector3 center = centerProp.vector3Value;
            Vector3 size = sizeProp.vector3Value;
            Quaternion rotation = rotationProp.quaternionValue;
            if (rotation.x == 0 && rotation.y == 0 && rotation.z == 0 && rotation.w == 0)
                rotation = Quaternion.identity;

            // Draw + size handle in the box's own rotated frame (within renderer local space).
            Matrix4x4 boxFrame = rendererToWorld * Matrix4x4.TRS(center, rotation, Vector3.one);
            using (new Handles.DrawingScope(boxFrame))
            {
                Handles.color = BoxWire;
                Handles.DrawWireCube(Vector3.zero, size);

                _boxHandle.center = Vector3.zero;
                _boxHandle.size = size;
                EditorGUI.BeginChangeCheck();
                _boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    sizeProp.vector3Value = _boxHandle.size;
                    // BoxBoundsHandle may shift its center when resizing from a face; fold that back into the box center.
                    centerProp.vector3Value = center + rotation * _boxHandle.center;
                }
            }

            // Position + rotation handles in renderer local space.
            using (new Handles.DrawingScope(rendererToWorld))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newCenter = Handles.PositionHandle(center, rotation);
                Quaternion newRotation = Handles.RotationHandle(rotation, center);
                if (EditorGUI.EndChangeCheck())
                {
                    centerProp.vector3Value = newCenter;
                    rotationProp.quaternionValue = newRotation;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeaderBar()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (_windowIcon != null)
            GUILayout.Label(_windowIcon, GUILayout.Width(48f), GUILayout.Height(48f));
        var titleStyle = new GUIStyle(EditorStyles.label)
        {
            font = _titleFont,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUILayout.Space(6f);
        GUILayout.Label("Remove Mesh In Box", titleStyle, GUILayout.Height(48f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawLanguageBar()
    {
        EditorGUILayout.BeginHorizontal();
        string[] options = { "English", "한국어", "日本語" };
        for (int i = 0; i < options.Length; i++)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = (int)_language == i ? ColAccent : new Color(0.5f, 0.5f, 0.5f);
            if (GUILayout.Button(options[i], GUILayout.Height(22f)))
            {
                _language = (LanguagePreset)i;
                EditorPrefs.SetInt("DiNeOpticore_ComponentLang", i);
            }
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawHorizontalLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, ColLine);
        GUILayout.Space(4f);
    }
}
#endif
