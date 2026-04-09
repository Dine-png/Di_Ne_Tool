#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class DiNeAnimationFreezer : EditorWindow
{
    private enum Lang { English, Korean, Japanese }
    private Lang _lang = Lang.Korean;
    private int L => (int)_lang;

    private static readonly string[][] UI =
    {
        /* 00 */ new[] { "Target Settings",          "대상 설정",           "対象設定"                   },
        /* 01 */ new[] { "Target Object (Root)",     "대상 오브젝트 (Root)", "対象オブジェクト (Root)"    },
        /* 02 */ new[] { "Animation Clip",           "애니메이션 클립",      "アニメーションクリップ"      },
        /* 03 */ new[] { "Time",                     "시점 조절",            "タイミング調整"             },
        /* 04 */ new[] { "Shape Keys Only",          "쉐이프키만 적용",      "シェイプキーのみ"            },
        /* 05 */ new[] { "Pose Only",                "포즈만 적용",          "ポーズのみ"                 },
        /* 06 */ new[] { "Apply All",                "전체 적용",            "すべて適用"                 },
        /* 07 */ new[] { "Shape Keys: applied.",     "쉐이프키 적용 완료!",  "シェイプキーを適用しました。" },
        /* 08 */ new[] { "Pose: applied.",           "포즈 적용 완료!",      "ポーズを適用しました。"      },
        /* 09 */ new[] { "All: applied.",            "전체 적용 완료!",      "すべて適用しました。"        },
        /* 10 */ new[] { "Slide to preview. Buttons apply to scene.",
                         "슬라이더로 미리보기, 버튼으로 씬에 적용합니다.",
                         "スライダーでプレビュー、ボタンでシーンに適用します。" },
        /* 11 */ new[] { "↩  Restore Original",     "↩  원본으로 복원",     "↩  元に戻す"               },
        /* 12 */ new[] { "Restored to original state.", "원본 상태로 복원했습니다.", "元の状態に戻しました。" },
        /* 13 */ new[] { "Snapshot saved.",          "스냅샷 저장됨",        "スナップショット保存済み"    },
    };
    private string T(int i) => UI[i][L];

    private GameObject    _target;
    private GameObject    _prevTarget;
    private AnimationClip _clip;
    private float         _time;

    private Texture2D _windowIcon;
    private Texture2D _tabIcon;
    private Font      _titleFont;

    // ── 스냅샷 ──
    private Dictionary<SkinnedMeshRenderer, float[]>                         _snapShapeKeys  = new Dictionary<SkinnedMeshRenderer, float[]>();
    private Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scl)> _snapTransforms = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
    private bool _hasSnapshot;

    [MenuItem("DiNe/Avatar/Animation Freezer")]
    public static void ShowWindow()
    {
        var w = GetWindow<DiNeAnimationFreezer>("Animation Freezer");
        w.minSize  = new Vector2(300, 360);
        w.position = new Rect(w.position.x, w.position.y, 430, 440);
    }

    void OnEnable()
    {
        _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        _tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("Anim", _tabIcon);
    }

    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // ── 헤더 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        float iconSize = 72f;
        GUILayout.Label(_windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label("Animation Freezer", new GUIStyle(EditorStyles.label)
        {
            font      = _titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 36,
            normal    = { textColor = Color.white }
        }, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = L == 1 ? "애니메이션 클립의 쉐이프키·포즈 값을 씬 오브젝트에 고정합니다."
                    : L == 2 ? "アニメーションクリップのシェイプキー・ポーズ値をシーンに固定します。"
                             : "Freeze BlendShape and/or Pose values from an animation clip onto the scene.";
        GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 언어 선택 ──
        int next = DrawToolbar(L, new[] { "English", "한국어", "日本語" }, 28);
        if (next != L) _lang = (Lang)next;

        GUILayout.Space(10);

        // ── 대상 설정 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(T(0), EditorStyles.boldLabel);
        GUILayout.Space(3);
        _target = (GameObject)EditorGUILayout.ObjectField(T(1), _target, typeof(GameObject), true);
        _clip   = (AnimationClip)EditorGUILayout.ObjectField(T(2), _clip,   typeof(AnimationClip), false);
        EditorGUILayout.EndVertical();

        // 타겟이 바뀌면 자동 스냅샷
        if (_target != _prevTarget)
        {
            _prevTarget = _target;
            if (_target != null) TakeSnapshot();
        }

        GUILayout.Space(5);

        // ── 시점 슬라이더 ──
        EditorGUI.BeginDisabledGroup(_target == null || _clip == null);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(T(3), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (_clip != null)
            GUILayout.Label($"{_time:F3}s / {_clip.length:F3}s", new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(3);

        EditorGUI.BeginChangeCheck();
        _time = EditorGUILayout.Slider(_time, 0f, _clip != null ? _clip.length : 1f);
        if (EditorGUI.EndChangeCheck())
            ApplyShapeKeys(); // 슬라이더 드래그 시 쉐이프키만 실시간 미리보기

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);

        // ── 적용 버튼 3개 ──
        EditorGUI.BeginDisabledGroup(_target == null || _clip == null);

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 36,
            normal      = { textColor = Color.white },
            hover       = { textColor = Color.white },
        };
        var prevBg = GUI.backgroundColor;

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        if (GUILayout.Button(T(4), btnStyle))
        {
            ApplyShapeKeys();
            Debug.Log($"[DiNe Animation Freezer] {_target.name} — {T(7)}");
        }

        GUI.backgroundColor = new Color(0.25f, 0.65f, 0.60f);
        if (GUILayout.Button(T(5), btnStyle))
        {
            ApplyPose();
            Debug.Log($"[DiNe Animation Freezer] {_target.name} — {T(8)}");
        }

        GUI.backgroundColor = new Color(0.21f, 0.21f, 0.24f);
        if (GUILayout.Button(T(6), new GUIStyle(btnStyle)
            { normal = { textColor = new Color(0.30f, 0.82f, 0.76f) }, hover = { textColor = Color.white } }))
        {
            ApplyShapeKeys();
            ApplyPose();
            Debug.Log($"[DiNe Animation Freezer] {_target.name} — {T(9)}");
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = prevBg;

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(5);

        // ── 복원 버튼 ──
        EditorGUI.BeginDisabledGroup(!_hasSnapshot);

        GUI.backgroundColor = new Color(0.21f, 0.21f, 0.24f);
        if (GUILayout.Button(T(11), new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 30,
            normal      = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            hover       = { textColor = Color.white },
        }))
        {
            RestoreSnapshot();
            Debug.Log($"[DiNe Animation Freezer] {T(12)}");
        }

        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(6);
        EditorGUILayout.HelpBox(T(10), MessageType.Info);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Snapshot
    // ══════════════════════════════════════════════════════════════════════
    private void TakeSnapshot()
    {
        _snapShapeKeys.Clear();
        _snapTransforms.Clear();

        if (_target == null) { _hasSnapshot = false; return; }

        // 쉐이프키 스냅샷
        foreach (var smr in _target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            int count = smr.sharedMesh.blendShapeCount;
            var weights = new float[count];
            for (int i = 0; i < count; i++)
                weights[i] = smr.GetBlendShapeWeight(i);
            _snapShapeKeys[smr] = weights;
        }

        // Transform 스냅샷 (전체 자식 포함)
        foreach (Transform t in _target.GetComponentsInChildren<Transform>(true))
            _snapTransforms[t] = (t.localPosition, t.localRotation, t.localScale);

        _hasSnapshot = true;
    }

    private void RestoreSnapshot()
    {
        if (!_hasSnapshot) return;

        foreach (var kvp in _snapShapeKeys)
        {
            if (kvp.Key == null) continue;
            Undo.RecordObject(kvp.Key, "DiNe Restore Snapshot");
            for (int i = 0; i < kvp.Value.Length; i++)
                kvp.Key.SetBlendShapeWeight(i, kvp.Value[i]);
        }

        foreach (var kvp in _snapTransforms)
        {
            if (kvp.Key == null) continue;
            Undo.RecordObject(kvp.Key, "DiNe Restore Snapshot");
            kvp.Key.localPosition = kvp.Value.pos;
            kvp.Key.localRotation = kvp.Value.rot;
            kvp.Key.localScale    = kvp.Value.scl;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Apply — Shape Keys
    // ══════════════════════════════════════════════════════════════════════
    private void ApplyShapeKeys()
    {
        if (_target == null || _clip == null) return;

        foreach (var b in AnimationUtility.GetCurveBindings(_clip))
        {
            if (!b.propertyName.StartsWith("blendShape.")) continue;

            var curve = AnimationUtility.GetEditorCurve(_clip, b);
            if (curve == null) continue;

            var t = ResolveTransform(b.path);
            if (t == null) continue;

            var smr = t.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) continue;

            int idx = smr.sharedMesh.GetBlendShapeIndex(b.propertyName.Substring("blendShape.".Length));
            if (idx < 0) continue;

            Undo.RecordObject(smr, "DiNe Freeze ShapeKey");
            smr.SetBlendShapeWeight(idx, curve.Evaluate(_time));
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Apply — Pose (Transform)
    // ══════════════════════════════════════════════════════════════════════
    private void ApplyPose()
    {
        if (_target == null || _clip == null) return;

        var samples = new Dictionary<string, TransformSample>();

        foreach (var b in AnimationUtility.GetCurveBindings(_clip))
        {
            if (b.propertyName.StartsWith("blendShape.")) continue;
            if (b.type != typeof(Transform)) continue;

            var curve = AnimationUtility.GetEditorCurve(_clip, b);
            if (curve == null) continue;

            if (!samples.ContainsKey(b.path))
                samples[b.path] = new TransformSample();

            samples[b.path].Set(b.propertyName, curve.Evaluate(_time));
        }

        foreach (var kvp in samples)
        {
            var t = ResolveTransform(kvp.Key);
            if (t == null) continue;
            Undo.RecordObject(t, "DiNe Freeze Pose");
            kvp.Value.ApplyTo(t);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════
    private Transform ResolveTransform(string path)
    {
        if (string.IsNullOrEmpty(path)) return _target.transform;
        return _target.transform.Find(path);
    }

    private int DrawToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int result = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = i == selected ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 12,
                fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                normal    = { textColor = i == selected ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
                result = i;
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Transform Sample Accumulator
    // ══════════════════════════════════════════════════════════════════════
    private class TransformSample
    {
        float? px, py, pz;
        float? rx, ry, rz, rw;
        float? ex, ey, ez;
        float? sx, sy, sz;

        public void Set(string prop, float v)
        {
            switch (prop)
            {
                case "localPosition.x":      case "m_LocalPosition.x": px = v; break;
                case "localPosition.y":      case "m_LocalPosition.y": py = v; break;
                case "localPosition.z":      case "m_LocalPosition.z": pz = v; break;
                case "localRotation.x":      case "m_LocalRotation.x": rx = v; break;
                case "localRotation.y":      case "m_LocalRotation.y": ry = v; break;
                case "localRotation.z":      case "m_LocalRotation.z": rz = v; break;
                case "localRotation.w":      case "m_LocalRotation.w": rw = v; break;
                case "localEulerAnglesRaw.x":                           ex = v; break;
                case "localEulerAnglesRaw.y":                           ey = v; break;
                case "localEulerAnglesRaw.z":                           ez = v; break;
                case "localScale.x":         case "m_LocalScale.x":    sx = v; break;
                case "localScale.y":         case "m_LocalScale.y":    sy = v; break;
                case "localScale.z":         case "m_LocalScale.z":    sz = v; break;
            }
        }

        public void ApplyTo(Transform t)
        {
            if (px.HasValue || py.HasValue || pz.HasValue)
            {
                var p = t.localPosition;
                if (px.HasValue) p.x = px.Value;
                if (py.HasValue) p.y = py.Value;
                if (pz.HasValue) p.z = pz.Value;
                t.localPosition = p;
            }

            if (rx.HasValue && ry.HasValue && rz.HasValue && rw.HasValue)
            {
                t.localRotation = new Quaternion(rx.Value, ry.Value, rz.Value, rw.Value);
            }
            else if (ex.HasValue || ey.HasValue || ez.HasValue)
            {
                var e = t.localEulerAngles;
                if (ex.HasValue) e.x = ex.Value;
                if (ey.HasValue) e.y = ey.Value;
                if (ez.HasValue) e.z = ez.Value;
                t.localEulerAngles = e;
            }

            if (sx.HasValue || sy.HasValue || sz.HasValue)
            {
                var s = t.localScale;
                if (sx.HasValue) s.x = sx.Value;
                if (sy.HasValue) s.y = sy.Value;
                if (sz.HasValue) s.z = sz.Value;
                t.localScale = s;
            }
        }
    }
}
#endif
