#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DiNeTool.ExpressionEditor
{
    /// <summary>
    /// Shared preference for Di Ne's expression inspectors. Inspector registration
    /// uses CustomEditor attributes and the separate Di Ne priority bridge.
    /// </summary>
    [InitializeOnLoad]
    internal static class DiNeExpressionInspectorSettings
    {
        private const string PreferenceKey = "DiNeExpressionInspectorEnabled";
        private const string ConflictWarningKey = "DiNe.ExpressionInspector.CompetingPackageWarningShown";
        private const string MenuPath = "DiNe/Expression Editor/Di Ne Inspector";

        private static readonly string[] CompetingPackageNames =
        {
            "dev.vrlabs.vrcsdkplus",
            "com.dreadscripts.vrcsdkplus",
            "com.awavr.vrcsdkplus"
        };

        static DiNeExpressionInspectorSettings()
        {
            EditorApplication.delayCall += WarnIfCompetingInspectorPackage;
        }

        internal static bool UseDiNeInspector
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set
            {
                if (UseDiNeInspector == value)
                    return;

                EditorPrefs.SetBool(PreferenceKey, value);
                DiNeExpressionInspectorRegistration.EnsureRegistered();
                Menu.SetChecked(MenuPath, value);
                RepaintOpenInspectors();
            }
        }

        internal static bool HasCompetingInspectorPackage
        {
            get
            {
                var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (packages == null)
                    return false;

                foreach (var package in packages)
                {
                    if (package == null)
                        continue;

                    foreach (string packageName in CompetingPackageNames)
                    {
                        if (package.name == packageName)
                            return true;
                    }
                }

                return false;
            }
        }

        internal static string CompetingInspectorWarning
        {
            get
            {
                int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
                switch (language)
                {
                    case 1:
                        return "VRCSDK+가 설치되어 있습니다. 두 도구가 같은 Expression Menu/Parameters Inspector를 사용하므로 함께 설치한 상태는 지원하지 않습니다. Di Ne Inspector를 사용하려면 VCC의 Manage Project에서 VRCSDK+를 제거하세요. 기본 SDK Inspector로 돌아갈 때도 VRCSDK+가 설치되어 있으면 해당 도구가 Inspector를 바꿀 수 있습니다.";
                    case 2:
                        return "VRCSDK+がインストールされています。両ツールは同じExpression Menu/Parameters Inspectorを使用するため、同時インストールはサポートされません。Di Ne Inspectorを使用するには、VCCのManage ProjectでVRCSDK+を削除してください。標準SDK Inspectorへ戻す場合も、VRCSDK+がインストールされているとInspectorが置き換えられることがあります。";
                    default:
                        return "VRCSDK+ is installed. Both tools use the Expression Menu/Parameters inspectors, so installing them together is not supported. Remove VRCSDK+ through Manage Project in VCC to use the Di Ne inspectors. VRCSDK+ can also replace the inspector when you switch back to the standard SDK inspector.";
                }
            }
        }

        /// <summary>Reports the conflict once per Unity Editor session.</summary>
        internal static void WarnIfCompetingInspectorPackage()
        {
            if (SessionState.GetBool(ConflictWarningKey, false) || !HasCompetingInspectorPackage)
                return;

            SessionState.SetBool(ConflictWarningKey, true);
            Debug.LogWarning("[Di Ne Expression Editor] " + CompetingInspectorWarning);
        }

        [MenuItem(MenuPath, false, 40)]
        private static void ToggleInspector()
        {
            UseDiNeInspector = !UseDiNeInspector;
            WarnIfCompetingInspectorPackage();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggleInspector()
        {
            Menu.SetChecked(MenuPath, UseDiNeInspector);
            return true;
        }

        private static void RepaintOpenInspectors()
        {
            foreach (var editor in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
            {
                if (editor != null)
                    editor.Repaint();
            }

            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window != null)
                    window.Repaint();
            }
        }
    }
}
#endif
