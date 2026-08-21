// Poiyomi의 메인 색 보정(Color Adjust)을 텍스처에 구워 넣기 위한 블릿 셰이더.
// 연산 순서와 수식은 Poiyomi의 CGI_PoiMainTex.cginc / CGI_FunctionsArtistic.cginc를 그대로 옮긴 것이다.
//   albedo = mainTex * _Color
//   hue shift  → saturation → brightness (가산)
// 마스크(_MainColorAdjustTexture)는 UV0 / 패닝 없음을 가정한다.
Shader "Hidden/DiNe/LightingDesigner/PoiyomiColorAdjustBaker"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _MainColorAdjustTexture ("Mask", 2D) = "white" {}
        _MainHueShift ("Hue Shift", Range(0,1)) = 0
        _MainHueShiftReplace ("Hue Replace", Float) = 1
        _Saturation ("Saturation", Range(-1,10)) = 0
        _MainBrightness ("Brightness", Range(-1,1)) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MainColorAdjustTexture;
            float4 _Color;
            float _MainHueShift;
            float _MainHueShiftReplace;
            float _Saturation;
            float _MainBrightness;

            // Poiyomi CGI_FunctionsArtistic.cginc의 hueShift 그대로.
            float3 poiHueShift(float3 color, float offset)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 P = lerp(float4(color.bg, K.wz), float4(color.gb, K.xy), step(color.b, color.g));
                float4 Q = lerp(float4(P.xyw, color.r), float4(color.r, P.yzx), step(P.x, color.r));
                float D = Q.x - min(Q.w, Q.y);
                float E = 0.0000000001;
                float3 hsv = float3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);

                float hue = hsv.x + offset;
                hsv.x = frac(hue);

                float4 K2 = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 P2 = abs(frac(hsv.xxx + K2.xyz) * 6.0 - K2.www);
                return hsv.z * lerp(K2.xxx, saturate(P2 - K2.xxx), hsv.y);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 albedo = tex2D(_MainTex, i.uv);
                albedo.rgb *= max(_Color.rgb, float3(0.000000001, 0.000000001, 0.000000001));
                albedo.a *= max(_Color.a, 0.0000001);

                float4 mask = tex2D(_MainColorAdjustTexture, i.uv);

                if (_MainHueShiftReplace)
                {
                    albedo.rgb = lerp(albedo.rgb, poiHueShift(albedo.rgb, _MainHueShift), mask.r);
                }
                else
                {
                    albedo.rgb = poiHueShift(albedo.rgb, frac(_MainHueShift - (1 - mask.r)));
                }

                albedo.rgb = lerp(albedo.rgb, dot(albedo.rgb, float3(0.3, 0.59, 0.11)), -_Saturation * mask.b);
                albedo.rgb = saturate(albedo.rgb + _MainBrightness * mask.g);

                return albedo;
            }
            ENDCG
        }
    }
}
