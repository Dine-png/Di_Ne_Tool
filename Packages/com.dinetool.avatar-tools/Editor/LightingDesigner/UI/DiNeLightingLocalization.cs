#if UNITY_EDITOR
using UnityEditor;

internal enum DiNeLightingLanguage
{
    English,
    Korean,
    Japanese,
}

internal static class DiNeLightingLocalization
{
    internal static readonly string[] LanguageButtonLabels = { "English", "한국어", "日本語" };

    internal static DiNeLightingLanguage CurrentLanguage
    {
        get
        {
            int value = EditorPrefs.GetInt("DiNeLang", 0);
            if (value < 0 || value > 2) value = 0;
            return (DiNeLightingLanguage)value;
        }
        set => EditorPrefs.SetInt("DiNeLang", (int)value);
    }

    internal static string T(string korean, string english, string japanese)
    {
        switch (CurrentLanguage)
        {
            case DiNeLightingLanguage.Korean: return korean;
            case DiNeLightingLanguage.Japanese: return japanese;
            default: return english;
        }
    }

    internal static string ControlName(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin: return T("조명 밝기", "Lighting Brightness", "ライティング明るさ");
            case DiNeLightingControl.Saturation: return T("채도", "Saturation", "彩度");
            case DiNeLightingControl.Hue: return T("색조", "Hue", "色相");
            case DiNeLightingControl.Brightness: return T("명도", "Brightness", "明度");
            case DiNeLightingControl.ColorTemperature: return T("색온도", "Color Temperature", "色温度");
            case DiNeLightingControl.Monochrome: return T("흑백화", "Monochrome", "モノクロ");
            case DiNeLightingControl.ShadowStrength: return T("그림자 농도", "Shadow Strength", "影の濃さ");
            case DiNeLightingControl.OutlineTint: return T("아웃라인 색", "Outline Tint", "アウトライン色");
            case DiNeLightingControl.OutlineWidth: return T("아웃라인 두께", "Outline Width", "アウトライン幅");
            case DiNeLightingControl.Reflectance: return T("반사/광택", "Reflectance / Gloss", "反射・光沢");
            default:
                return DiNeLightingControlDef.Get(control)?.DisplayName ?? control.ToString();
        }
    }

    internal static string ControlTooltip(DiNeLightingControl control)
    {
        switch (control)
        {
            case DiNeLightingControl.LightMin:
                return T("아바타의 최소·최대 조명 밝기를 하나의 슬라이더로 함께 조절합니다.", "Controls the avatar's minimum and maximum lighting together with one slider.", "アバターの最小・最大ライティングを1つのスライダーで同時に調整します。");
            case DiNeLightingControl.Saturation:
                return T("0.5가 원본입니다. 낮추면 탁해지고 올리면 선명해집니다.", "0.5 is the original value. Lower is duller; higher is more vivid.", "0.5が元の値です。下げると鈍く、上げると鮮やかになります。");
            case DiNeLightingControl.Hue:
                return T("색상환을 따라 전체 색을 회전합니다. 0이 원본입니다.", "Rotates all colors around the hue wheel. 0 is original.", "色相環に沿って全体の色を回転します。0が元の値です。");
            case DiNeLightingControl.Brightness:
                return T("0.5가 원본입니다. 텍스처의 밝기를 조절합니다.", "0.5 is original. Adjusts texture brightness.", "0.5が元の値です。テクスチャの明るさを調整します。");
            case DiNeLightingControl.ColorTemperature:
                return T("낮추면 따뜻하게, 올리면 차갑게 보입니다.", "Lower looks warmer; higher looks cooler.", "下げると暖かく、上げると冷たく見えます。");
            case DiNeLightingControl.Monochrome:
                return T("조명의 색 성분을 줄여 조명 색에 덜 물들게 합니다.", "Reduces light coloration on the avatar.", "ライトの色成分を減らし、照明色の影響を抑えます。");
            case DiNeLightingControl.ShadowStrength:
                return T("그림자가 얼마나 진하게 보일지 조절합니다.", "Controls how dark shadows appear.", "影の濃さを調整します。");
            case DiNeLightingControl.OutlineTint:
                return T("지정한 두 아웃라인 색 사이를 보간합니다.", "Blends between two outline colors.", "指定した2つのアウトライン色の間を補間します。");
            case DiNeLightingControl.OutlineWidth:
                return T("아웃라인의 굵기를 조절합니다.", "Controls outline thickness.", "アウトラインの太さを調整します。");
            case DiNeLightingControl.Reflectance:
                return T("표면의 반사와 광택 강도를 조절합니다.", "Controls surface reflection and gloss.", "表面の反射と光沢の強さを調整します。");
            default:
                return DiNeLightingControlDef.Get(control)?.Tooltip ?? string.Empty;
        }
    }
}
#endif
