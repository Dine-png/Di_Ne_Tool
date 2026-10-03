#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DiNeTool.ExpressionEditor
{
    /// <summary>
    /// Gives the existing Di Ne CustomEditor records priority in Unity 2022.3's
    /// cache. SDK and other packages' records and record fields are never changed.
    /// This implementation was written independently using Unity metadata.
    /// </summary>
    internal static class DiNeExpressionInspectorRegistration
    {
        private const string WarningKey = "DiNe.ExpressionInspector.RegistryWarningShown";
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool registering;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Allow initializers and Unity's normal editor discovery to finish.
            EditorApplication.delayCall += ScheduleLateRegistration;
        }

        private static void ScheduleLateRegistration()
        {
            EditorApplication.delayCall += RegisterAfterStartup;
        }

        private static void RegisterAfterStartup()
        {
            EnsureRegistered();
        }

        internal static bool EnsureRegistered()
        {
            if (registering) return false;
            registering = true;
            try
            {
                Type registryType = typeof(Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
                FieldInfo cacheField = registryType?.GetField("kSCustomEditors", StaticFields);
                FieldInfo initializedField = registryType?.GetField("s_Initialized", StaticFields);
                if (cacheField == null || initializedField == null || initializedField.FieldType != typeof(bool))
                    return WarnUnsupportedOnce("registry schema");

                // Reading the flag is sufficient; only Unity initializes its cache.
                if (!(bool)initializedField.GetValue(null)) PrimeCacheWithTemporaryObjects();

                var cache = cacheField.GetValue(null) as IDictionary;
                if (cache == null) return WarnUnsupportedOnce("registry cache");

                if (!TryFindOwnRecord(cache, typeof(VRCExpressionsMenu), typeof(DiNeExpressionMenuEditor),
                    out var menuRecords, out int menuIndex, out object menuRecord) ||
                    !TryFindOwnRecord(cache, typeof(VRCExpressionParameters), typeof(DiNeExpressionParametersEditor),
                    out var parameterRecords, out int parameterIndex, out object parameterRecord))
                    return WarnUnsupportedOnce("Di Ne registration records");

                // Both records are validated before any list is changed.
                bool changed = MoveOwnRecordFirst(menuRecords, menuIndex, menuRecord);
                changed |= MoveOwnRecordFirst(parameterRecords, parameterIndex, parameterRecord);
                if (changed)
                {
                    EditorApplication.delayCall -= RebuildOpenInspectors;
                    EditorApplication.delayCall += RebuildOpenInspectors;
                }
                return true;
            }
            catch (Exception exception)
            {
                return WarnUnsupportedOnce(exception.GetType().Name);
            }
            finally
            {
                registering = false;
            }
        }

        private static bool TryFindOwnRecord(IDictionary cache, Type inspectedType, Type ownEditorType,
            out IList records, out int index, out object ownRecord)
        {
            records = cache.Contains(inspectedType) ? cache[inspectedType] as IList : null;
            index = -1;
            ownRecord = null;
            if (records == null || records.IsReadOnly || records.IsFixedSize) return false;

            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i];
                if (record == null) continue;
                FieldInfo inspectorType = record.GetType().GetField("m_InspectorType", InstanceFields);
                if (inspectorType == null || inspectorType.FieldType != typeof(Type)) return false;
                if (inspectorType.GetValue(record) as Type != ownEditorType) continue;
                index = i;
                ownRecord = record;
                return true;
            }
            return false;
        }

        private static bool MoveOwnRecordFirst(IList records, int index, object ownRecord)
        {
            if (index == 0) return false;
            records.RemoveAt(index);
            records.Insert(0, ownRecord);
            return true;
        }

        private static void PrimeCacheWithTemporaryObjects()
        {
            PrimeCache<VRCExpressionsMenu>();
            PrimeCache<VRCExpressionParameters>();
        }

        private static void PrimeCache<T>() where T : ScriptableObject
        {
            T temporary = null;
            Editor editor = null;
            try
            {
                temporary = ScriptableObject.CreateInstance<T>();
                temporary.hideFlags = HideFlags.HideAndDontSave;
                editor = Editor.CreateEditor(temporary);
            }
            finally
            {
                if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
                if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static void RebuildOpenInspectors()
        {
            // Public tracker API refreshes the ordinary Inspector without changing Selection.
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window != null) window.Repaint();
        }

        private static bool WarnUnsupportedOnce(string detail)
        {
            if (SessionState.GetBool(WarningKey, false)) return false;
            SessionState.SetBool(WarningKey, true);
            int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
            string warning = language == 1
                ? "이 Unity 버전의 Inspector 등록 구조를 확인하지 못해 Di Ne Inspector의 자동 선택을 보장할 수 없습니다. Unity 2022.3용 등록 방식을 사용합니다. 기존 Inspector와 에셋은 보존됩니다."
                : language == 2
                    ? "このUnityバージョンのInspector登録構造を確認できないため、Di Ne Inspectorの自動選択を保証できません。Unity 2022.3向けの登録方式を使用しています。既存のInspectorとアセットは維持されます。"
                    : "The inspector registration structure could not be verified for this Unity version, so automatic selection of the Di Ne inspectors is not guaranteed. Registration targets Unity 2022.3. Existing inspectors and assets are preserved.";
            Debug.LogWarning("[Di Ne Expression Editor] " + warning + " (Unity " + Application.unityVersion + "; " + detail + ")");
            return false;
        }
    }
}
#endif
