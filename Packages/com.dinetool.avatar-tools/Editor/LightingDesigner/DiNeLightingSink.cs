#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 셰이더 프로파일이 애니메이션을 기록하는 대상. 0~1 구간의 여러 시점(t)마다 클립을 만들고,
/// 나중에 제너레이터가 그 클립들을 1D 블렌드 트리로 조립한다.
/// 하나의 싱크는 하나의 제어 항목(또는 Default 클립 전체)에 대응하며,
/// 렌더러마다 CurrentPath/CurrentType을 바꿔가며 재사용한다.
/// </summary>
internal sealed class DiNeLightingSink
{
    private readonly SortedDictionary<float, AnimationClip> _clips = new SortedDictionary<float, AnimationClip>();
    private readonly Object _assetContainer;
    private readonly string _name;

    public string CurrentPath { get; set; }
    public Type CurrentType { get; set; }

    public DiNeLightingSink(string name, Object assetContainer)
    {
        _name = name;
        _assetContainer = assetContainer;
    }

    public bool IsEmpty => _clips.Count == 0;

    public IEnumerable<KeyValuePair<float, AnimationClip>> Clips => _clips;

    /// <summary>t 시점의 클립. 없으면 만든다.</summary>
    private AnimationClip GetClip(float t)
    {
        if (_clips.TryGetValue(t, out var clip))
            return clip;

        clip = new AnimationClip { name = $"{_name}_{t:0.###}", frameRate = 60f };
        if (_assetContainer != null)
            AssetDatabase.AddObjectToAsset(clip, _assetContainer);
        _clips.Add(t, clip);
        return clip;
    }

    public void SetFloat(float t, string property, float value)
    {
        // 아바타 루트에 붙은 렌더러는 경로가 빈 문자열이므로 null만 걸러낸다.
        if (CurrentPath == null || CurrentType == null) return;

        var binding = EditorCurveBinding.FloatCurve(CurrentPath, CurrentType, "material." + property);
        AnimationUtility.SetEditorCurve(GetClip(t), binding, AnimationCurve.Constant(0f, 1f / 60f, value));
    }

    public void SetColor(float t, string property, Color value)
    {
        SetFloat(t, property + ".r", value.r);
        SetFloat(t, property + ".g", value.g);
        SetFloat(t, property + ".b", value.b);
        SetFloat(t, property + ".a", value.a);
    }

    public void SetVector(float t, string property, Vector4 value)
    {
        SetFloat(t, property + ".x", value.x);
        SetFloat(t, property + ".y", value.y);
        SetFloat(t, property + ".z", value.z);
        SetFloat(t, property + ".w", value.w);
    }

    /// <summary>제어 클립용: 0에서 min, 1에서 max.</summary>
    public void SetFloatRange(string property, float min, float max)
    {
        SetFloat(0f, property, min);
        SetFloat(1f, property, max);
    }

    public void SetColorRange(string property, Color min, Color max)
    {
        SetColor(0f, property, min);
        SetColor(1f, property, max);
    }

    /// <summary>Default 클립용: t=0 한 점만 기록.</summary>
    public void SetFloatConstant(string property, float value) => SetFloat(0f, property, value);

    public void SetColorConstant(string property, Color value) => SetColor(0f, property, value);

    public void SetVectorConstant(string property, Vector4 value) => SetVector(0f, property, value);
}
#endif
