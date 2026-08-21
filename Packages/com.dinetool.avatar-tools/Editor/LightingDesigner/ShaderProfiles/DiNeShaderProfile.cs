#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>제어 항목을 실제 셰이더 프로퍼티로 옮길 때 필요한 컨텍스트.</summary>
internal struct DiNeLightingContext
{
    /// <summary>라디얼 0 지점에 대응하는 라이트 값.</summary>
    public float MinLight;
    /// <summary>라디얼 1 지점에 대응하는 라이트 값.</summary>
    public float MaxLight;
    /// <summary>Enable OFF일 때 복원할 최소 밝기.</summary>
    public float DefaultMinLight;
    /// <summary>Enable OFF일 때 복원할 최대 밝기.</summary>
    public float DefaultMaxLight;
    /// <summary>Enable OFF일 때 복원할 흑백화 값.</summary>
    public float DefaultMonochrome;

    public Vector3 LightDirection;
    public Color OutlineTintFrom;
    public Color OutlineTintTo;
    public float OutlineWidthMax;
    public float ReflectanceMax;

    /// <summary>현재 처리 중인 렌더러의 머티리얼들.</summary>
    public Material[] Materials;
}

/// <summary>
/// 셰이더별 프로퍼티 매핑. 새 셰이더를 지원하려면 이 클래스를 상속한 파일 하나만 추가하고
/// <see cref="All"/>에 등록하면 된다.
/// </summary>
internal abstract class DiNeShaderProfile
{
    public abstract string Name { get; }
    public abstract DiNeLightingTargetShaders Flag { get; }

    /// <summary>이 프로파일이 담당하는 셰이더인지.</summary>
    public abstract bool IsTarget(Shader shader);

    /// <summary>해당 제어 항목을 이 셰이더에서 지원하는지.</summary>
    public abstract bool Supports(DiNeLightingControl control);

    /// <summary>Enable OFF 상태에서 되돌릴 값을 기록한다.</summary>
    public abstract void WriteDefault(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context);

    /// <summary>라디얼 0~1(또는 토글 OFF/ON)에 대응하는 값을 기록한다.</summary>
    public abstract void WriteControl(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context);

    /// <summary>
    /// 켜진 제어 항목이 덮어쓸 색 보정 프로퍼티를 텍스처에 구워 넣고 프로퍼티를 중립값으로 되돌린다.
    /// 이렇게 해야 채도/색온도 슬라이더의 중앙이 "원본 색"이 된다.
    /// 빌드 클론의 복제 머티리얼에 대해서만 호출되므로 원본 에셋은 안전하다.
    /// </summary>
    /// <returns>머티리얼을 실제로 수정했으면 true.</returns>
    public virtual bool NormalizeMaterial(
        Material material,
        ISet<DiNeLightingControl> controls,
        DiNeLightingBakeSession session)
    {
        return false;
    }

    /// <summary>
    /// 베이킹과 별개로, 애니메이션이 실제로 먹게 하기 위해 머티리얼에 해줘야 하는 준비 작업.
    /// (셰이더 키워드 켜기, Poiyomi의 Animated 태그 붙이기 등)
    /// 복제된 머티리얼에 대해서만 호출된다.
    /// </summary>
    public virtual void PrepareMaterial(Material material, ISet<DiNeLightingControl> controls)
    {
    }

    /// <summary>
    /// 이 항목이 실제로 화면에 나타나려면 머티리얼에서 켜져 있어야 하는 기능 토글 프로퍼티.
    /// (예: lilToon의 아웃라인은 _UseOutline이 꺼져 있으면 두께를 애니메이션해도 아무 일도 없다.)
    /// 없으면 null. 사용자가 의도적으로 끈 기능을 마음대로 켜지는 않고, 인스펙터에서 경고만 띄운다.
    /// </summary>
    public virtual string GetRequiredFeatureToggle(DiNeLightingControl control)
    {
        return null;
    }

    /// <summary>이 머티리얼을 복제해서 손봐야 하는지. false면 원본을 그대로 둔다.</summary>
    public virtual bool RequiresMaterialPreparation(ISet<DiNeLightingControl> controls)
    {
        return NeedsNormalization(controls);
    }

    /// <summary>정규화가 필요한 제어 항목인지. 하나라도 켜져 있어야 베이킹을 돌린다.</summary>
    public static bool NeedsNormalization(ISet<DiNeLightingControl> controls)
    {
        return controls.Contains(DiNeLightingControl.Saturation)
            || controls.Contains(DiNeLightingControl.Hue)
            || controls.Contains(DiNeLightingControl.Brightness)
            || controls.Contains(DiNeLightingControl.Gamma)
            || controls.Contains(DiNeLightingControl.ColorTemperature);
    }

    /// <summary>머티리얼이 원래 쓰던 라이트 상/하한. 덮어쓰기를 끈 경우 Enable OFF 복원값으로 쓴다.</summary>
    public virtual bool TryGetLightRange(Material[] materials, out float min, out float max)
    {
        min = 0f;
        max = 1f;
        return false;
    }

    private static DiNeShaderProfile[] _all;

    public static DiNeShaderProfile[] All => _all ?? (_all = new DiNeShaderProfile[]
    {
        DiNeShaderProfileLilToon.Instance,
        DiNeShaderProfilePoiyomi.Instance,
        DiNeShaderProfileSunao.Instance,
        DiNeShaderProfileMToon.Instance,
    });

    /// <summary>주어진 머티리얼들에 해당하는 프로파일을 중복 없이 모은다.</summary>
    public static List<DiNeShaderProfile> ResolveProfiles(Material[] materials, DiNeLightingTargetShaders mask)
    {
        var result = new List<DiNeShaderProfile>();
        if (materials == null) return result;

        foreach (var material in materials)
        {
            if (material == null || material.shader == null) continue;

            foreach (var profile in All)
            {
                if ((mask & profile.Flag) == 0) continue;
                if (result.Contains(profile)) continue;
                if (profile.IsTarget(material.shader))
                    result.Add(profile);
            }
        }
        return result;
    }

    /// <summary>인스펙터에서 "이 항목을 지원하는 셰이더" 안내를 만들 때 사용.</summary>
    public static string DescribeSupport(DiNeLightingControl control)
    {
        var supported = new List<string>();
        foreach (var profile in All)
            if (profile.Supports(control))
                supported.Add(profile.Name);

        return supported.Count == 0 ? "지원 셰이더 없음" : string.Join(", ", supported);
    }
}
#endif
