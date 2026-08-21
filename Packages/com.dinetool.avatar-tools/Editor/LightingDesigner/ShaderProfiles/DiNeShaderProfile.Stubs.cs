#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>
/// Sunao 스텁. 아직 매핑을 채우지 않았다. 채우려면 Supports/Write*만 구현하면 된다.
/// Sunao는 색 보정(HSVG)·그림자 농도·아웃라인·반사·라이트 방향 프로퍼티가 없어서
/// 대응 가능한 항목이 밝기/Unlit/흑백화/색온도 정도다.
/// </summary>
internal sealed class DiNeShaderProfileSunao : DiNeShaderProfile
{
    public static readonly DiNeShaderProfileSunao Instance = new DiNeShaderProfileSunao();

    public override string Name => "Sunao";
    public override DiNeLightingTargetShaders Flag => DiNeLightingTargetShaders.Sunao;

    public override bool IsTarget(Shader shader)
    {
        return shader != null && shader.name.IndexOf("Sunao", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public override bool Supports(DiNeLightingControl control) => false;

    public override void WriteDefault(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context) { }

    public override void WriteControl(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context) { }
}

/// <summary>
/// MToon 스텁. MToon 0.x(UniVRM)와 MToon 1.0(VRM10)이 프로퍼티 이름이 달라
/// (_ShadeColor vs _ShadeColorFactor) 사실상 프로파일 두 개 분량이다.
/// </summary>
internal sealed class DiNeShaderProfileMToon : DiNeShaderProfile
{
    public static readonly DiNeShaderProfileMToon Instance = new DiNeShaderProfileMToon();

    public override string Name => "MToon";
    public override DiNeLightingTargetShaders Flag => DiNeLightingTargetShaders.MToon;

    public override bool IsTarget(Shader shader)
    {
        return shader != null && shader.name.IndexOf("MToon", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public override bool Supports(DiNeLightingControl control) => false;

    public override void WriteDefault(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context) { }

    public override void WriteControl(DiNeLightingSink sink, DiNeLightingControl control, in DiNeLightingContext context) { }
}
#endif
