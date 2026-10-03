using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.PackageManager;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Control = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;
using ParameterType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;
using Object = UnityEngine.Object;

namespace DiNeTool.ExpressionEditor
{
    // Implemented against the public SDK data model. No avatar/controller references are changed.
    internal static class DiNeExpressionUtility
    {
        internal const int MenuLimit = 8;

        // Keep convenience members optional across SDK versions rather than requiring
        // every supported SDK to expose them in the compile-time data contract.
        internal static VRCExpressionParameters GetMenuParameters(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            var field = typeof(VRCExpressionsMenu).GetField("Parameters", BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(VRCExpressionParameters))
                return (VRCExpressionParameters)field.GetValue(menu);
            var property = typeof(VRCExpressionsMenu).GetProperty("Parameters", BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.PropertyType == typeof(VRCExpressionParameters) &&
                property.GetGetMethod() != null ? (VRCExpressionParameters)property.GetValue(menu, null) : null;
        }

        // The caller records Undo/dirty state when applying this to an existing asset.
        internal static void SetMenuParameters(VRCExpressionsMenu menu, VRCExpressionParameters value)
        {
            if (menu == null) return;
            var field = typeof(VRCExpressionsMenu).GetField("Parameters", BindingFlags.Instance | BindingFlags.Public);
            if (field != null && !field.IsInitOnly && field.FieldType == typeof(VRCExpressionParameters))
            {
                field.SetValue(menu, value);
                return;
            }
            var property = typeof(VRCExpressionsMenu).GetProperty("Parameters", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(VRCExpressionParameters) && property.GetSetMethod() != null)
                property.SetValue(menu, value, null);
        }

        internal static bool GetIsEmpty(VRCExpressionParameters parameters)
        {
            if (parameters == null) return false;
            var field = typeof(VRCExpressionParameters).GetField("isEmpty", BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(parameters);
            var property = typeof(VRCExpressionParameters).GetProperty("isEmpty", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(bool) && property.GetGetMethod() != null)
                return (bool)property.GetValue(parameters, null);
            using (var serialized = new SerializedObject(parameters))
            {
                var flag = serialized.FindProperty("isEmpty");
                if (flag != null && flag.propertyType == SerializedPropertyType.Boolean) return flag.boolValue;
            }
            return parameters.parameters == null || parameters.parameters.Length == 0;
        }

        private static void SetIsEmpty(VRCExpressionParameters parameters, bool value)
        {
            var field = typeof(VRCExpressionParameters).GetField("isEmpty", BindingFlags.Instance | BindingFlags.Public);
            if (field != null && !field.IsInitOnly && field.FieldType == typeof(bool))
            {
                field.SetValue(parameters, value);
                return;
            }
            var property = typeof(VRCExpressionParameters).GetProperty("isEmpty", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(bool) && property.GetSetMethod() != null)
            {
                property.SetValue(parameters, value, null);
                return;
            }
            using (var serialized = new SerializedObject(parameters))
            {
                var flag = serialized.FindProperty("isEmpty");
                if (flag == null || flag.propertyType != SerializedPropertyType.Boolean) return;
                flag.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static Control CloneControl(Control source)
        {
            return (Control)CloneSerializedValue(source, new Dictionary<object, object>(ManagedReferenceComparer.Instance));
        }

        private static object CloneSerializedValue(object source, Dictionary<object, object> copies)
        {
            if (source == null || source is Object) return source;
            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return source;
            if (!type.IsValueType && copies.TryGetValue(source, out var previous)) return previous;

            if (source is Array sourceArray)
            {
                // Unity serializes one-dimensional arrays. Preserve their shape and null entries.
                var arrayCopy = Array.CreateInstance(type.GetElementType(), sourceArray.Length);
                copies.Add(source, arrayCopy);
                for (int index = 0; index < sourceArray.Length; index++)
                    arrayCopy.SetValue(CloneSerializedValue(sourceArray.GetValue(index), copies), index);
                return arrayCopy;
            }
            if (source is IList sourceList)
            {
                var listCopy = (IList)Activator.CreateInstance(type);
                copies.Add(source, listCopy);
                foreach (var item in sourceList) listCopy.Add(CloneSerializedValue(item, copies));
                return listCopy;
            }

            // Recreate normal cache defaults, then replace only Unity's serialized fields.
            // A managed-reference type without a default constructor uses serialization allocation.
            bool hasDefaultConstructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null) != null;
            object copy = type.IsValueType || hasDefaultConstructor ? Activator.CreateInstance(type, true) :
                FormatterServices.GetUninitializedObject(type);
            if (!type.IsValueType) copies.Add(source, copy);
            foreach (var field in GetSerializedFields(type))
            {
                field.SetValue(copy, CloneSerializedValue(field.GetValue(source), copies));
            }
            return copy;
        }

        private static IEnumerable<FieldInfo> GetSerializedFields(Type type)
        {
            for (Type declaringType = type; declaringType != null && declaringType != typeof(object); declaringType = declaringType.BaseType)
                foreach (var field in declaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized &&
                        (field.IsPublic || field.IsDefined(typeof(SerializeField), true) || field.IsDefined(typeof(SerializeReference), true)))
                        yield return field;
        }

        private sealed class ManagedReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ManagedReferenceComparer Instance = new ManagedReferenceComparer();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        internal static bool AddControl(VRCExpressionsMenu menu, Control control, string undoName)
        {
            if (!CanEditAsset(menu) || control == null || (menu.controls?.Count ?? 0) >= MenuLimit)
                return false;

            var controls = menu.controls == null ? new List<Control>() : new List<Control>(menu.controls);
            controls.Add(CloneControl(control));
            Undo.RecordObject(menu, undoName);
            menu.controls = controls;
            EditorUtility.SetDirty(menu);
            return true;
        }

        internal static bool RemoveControl(VRCExpressionsMenu menu, int index, string undoName)
        {
            if (!CanEditAsset(menu) || !HasControlIndex(menu, index)) return false;
            var controls = new List<Control>(menu.controls);
            controls.RemoveAt(index);
            Undo.RecordObject(menu, undoName);
            menu.controls = controls;
            EditorUtility.SetDirty(menu);
            return true;
        }

        internal static bool MoveControl(VRCExpressionsMenu menu, int from, int to, string undoName)
        {
            if (!CanEditAsset(menu) || !HasControlIndex(menu, from) || !HasControlIndex(menu, to) || from == to)
                return false;

            var controls = new List<Control>(menu.controls);
            var control = controls[from];
            controls.RemoveAt(from);
            controls.Insert(to, control);
            Undo.RecordObject(menu, undoName);
            menu.controls = controls;
            EditorUtility.SetDirty(menu);
            return true;
        }

        internal static bool MoveControlToMenu(VRCExpressionsMenu menu, int index,
            VRCExpressionsMenu destination, string undoName)
        {
            if (!CanEditAsset(menu) || !CanEditAsset(destination) || menu == destination ||
                !HasControlIndex(menu, index) || (destination.controls?.Count ?? 0) >= MenuLimit)
                return false;

            // Validate and stage both lists before either object is changed.
            var sourceControls = new List<Control>(menu.controls);
            var destinationControls = destination.controls == null ?
                new List<Control>() : new List<Control>(destination.controls);
            destinationControls.Add(sourceControls[index]);
            sourceControls.RemoveAt(index);
            Undo.RecordObjects(new Object[] { menu, destination }, undoName);
            menu.controls = sourceControls;
            destination.controls = destinationControls;
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(destination);
            return true;
        }

        private static bool HasControlIndex(VRCExpressionsMenu menu, int index)
        {
            return menu != null && menu.controls != null && index >= 0 && index < menu.controls.Count;
        }

        internal static Parameter CloneParameter(Parameter source)
        {
            return (Parameter)CloneSerializedValue(source, new Dictionary<object, object>(ManagedReferenceComparer.Instance));
        }

        // -1 means no changes were made because editing or the resulting SDK budget was rejected.
        internal static int MergeParameters(VRCExpressionParameters target,
            VRCExpressionParameters source, string undoName)
        {
            if (!CanEditAsset(target) || source == null) return -1;
            var parameters = target.parameters == null ? new List<Parameter>() : new List<Parameter>(target.parameters);
            var names = new HashSet<string>(parameters.Where(HasParameterName).Select(parameter => parameter.name),
                StringComparer.Ordinal);
            int added = 0;
            foreach (var parameter in source.parameters ?? Array.Empty<Parameter>())
            {
                if (!HasParameterName(parameter) || !names.Add(parameter.name)) continue;
                parameters.Add(CloneParameter(parameter));
                added++;
            }

            if (added == 0) return 0;
            if (!IsWithinBudget(parameters)) return -1;
            ApplyParameters(target, parameters.ToArray(), undoName);
            return added;
        }

        internal static int CleanupParameters(VRCExpressionParameters target, string undoName)
        {
            if (!CanEditAsset(target)) return 0;
            var result = new List<Parameter>();
            var byName = new Dictionary<string, List<Parameter>>(StringComparer.Ordinal);
            int removed = 0;
            foreach (var parameter in target.parameters ?? Array.Empty<Parameter>())
            {
                if (!HasParameterName(parameter))
                {
                    removed++;
                    continue;
                }

                if (!byName.TryGetValue(parameter.name, out var sameName))
                {
                    sameName = new List<Parameter>();
                    byName.Add(parameter.name, sameName);
                }
                if (sameName.Any(existing => SameParameterSettings(existing, parameter)))
                {
                    removed++;
                    continue;
                }
                sameName.Add(parameter);
                result.Add(parameter);
            }

            // An intentional empty list must not be repopulated by the SDK's default inspector.
            if (removed > 0 || (result.Count == 0 && !GetIsEmpty(target)))
                ApplyParameters(target, result.ToArray(), undoName);
            return removed;
        }

        private static bool SameParameterSettings(Parameter left, Parameter right)
        {
            return left.valueType == right.valueType && left.saved == right.saved &&
                left.defaultValue.Equals(right.defaultValue) && left.networkSynced == right.networkSynced &&
                // Retain duplicates with different settings introduced by newer SDK versions, too.
                string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
        }

        private static bool HasParameterName(Parameter parameter)
        {
            return parameter != null && !string.IsNullOrWhiteSpace(parameter.name);
        }

        internal static bool AddParameter(VRCExpressionParameters target, string name,
            ParameterType type, string undoName)
        {
            if (!CanEditAsset(target) || string.IsNullOrWhiteSpace(name) ||
                !Enum.IsDefined(typeof(ParameterType), type)) return false;
            var parameters = target.parameters == null ? new List<Parameter>() : new List<Parameter>(target.parameters);
            if (parameters.Any(parameter => parameter != null && string.Equals(parameter.name, name, StringComparison.Ordinal)))
                return false;

            parameters.Add(new Parameter { name = name, valueType = type, saved = true, networkSynced = true });
            if (!IsWithinBudget(parameters)) return false;
            ApplyParameters(target, parameters.ToArray(), undoName);
            return true;
        }

        private static void ApplyParameters(VRCExpressionParameters target, Parameter[] parameters, string undoName)
        {
            Undo.RecordObject(target, undoName);
            target.parameters = parameters;
            SetIsEmpty(target, parameters.Length == 0);
            EditorUtility.SetDirty(target);
        }

        private static bool IsWithinBudget(List<Parameter> parameters)
        {
            // Newer SDKs also cap the total count. Reflection keeps older supported SDKs compatible.
            var countLimit = typeof(VRCExpressionParameters).GetField("MAX_PARAMETER_COUNT", BindingFlags.Public | BindingFlags.Static);
            if (countLimit != null && parameters.Count > (int)countLimit.GetValue(null)) return false;

            var preview = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            preview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // Preserve every existing slot: the SDK may charge memory for null entries.
                preview.parameters = parameters.ToArray();
                SetIsEmpty(preview, preview.parameters.Length == 0);
                return preview.CalcTotalCost() <= VRCExpressionParameters.MAX_PARAMETER_COST;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NullReferenceException ||
                                               exception is InvalidOperationException || exception is OverflowException)
            {
                // Do not accept an edit when malformed SDK data prevents a budget check.
                return false;
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }
        }

        internal static List<AnimatorControllerParameter> GetAnimatorParameters(VRCAvatarDescriptor avatar)
        {
            var result = new List<AnimatorControllerParameter>();
            if (avatar == null) return result;
            var controllers = new HashSet<RuntimeAnimatorController>();
            if (avatar.baseAnimationLayers != null)
                foreach (var layer in avatar.baseAnimationLayers)
                    if (layer.animatorController != null) controllers.Add(layer.animatorController);
            if (avatar.specialAnimationLayers != null)
                foreach (var layer in avatar.specialAnimationLayers)
                    if (layer.animatorController != null) controllers.Add(layer.animatorController);
            foreach (var animator in avatar.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null) controllers.Add(animator.runtimeAnimatorController);

            var byName = new Dictionary<string, AnimatorControllerParameter>(StringComparer.Ordinal);
            var conflicts = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<RuntimeAnimatorController>();
            foreach (var runtimeController in controllers)
            {
                var controller = GetAnimatorController(runtimeController, visited);
                if (controller == null) continue;
                foreach (var parameter in controller.parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.name)) continue;
                    if (byName.TryGetValue(parameter.name, out var existing))
                    {
                        if (existing.type != parameter.type) conflicts.Add(parameter.name);
                        continue;
                    }
                    byName.Add(parameter.name, parameter);
                }
            }

            foreach (var entry in byName.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                if (!conflicts.Contains(entry.Key) && entry.Value.type != AnimatorControllerParameterType.Trigger)
                    result.Add(new AnimatorControllerParameter
                    {
                        name = entry.Value.name,
                        type = entry.Value.type,
                        defaultBool = entry.Value.defaultBool,
                        defaultInt = entry.Value.defaultInt,
                        defaultFloat = entry.Value.defaultFloat
                    });
            return result;
        }

        private static AnimatorController GetAnimatorController(RuntimeAnimatorController controller,
            HashSet<RuntimeAnimatorController> visited)
        {
            while (controller != null && visited.Add(controller))
            {
                if (controller is AnimatorController animatorController) return animatorController;
                if (!(controller is AnimatorOverrideController overrides)) return null;
                controller = overrides.runtimeAnimatorController;
            }
            return null;
        }

        internal static bool ContainsMenu(VRCExpressionsMenu root, VRCExpressionsMenu candidate)
        {
            if (root == null || candidate == null) return false;
            var pending = new Stack<VRCExpressionsMenu>();
            var visited = new HashSet<VRCExpressionsMenu>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var menu = pending.Pop();
                if (menu == null || !visited.Add(menu)) continue;
                if (menu == candidate) return true;
                if (menu.controls == null) continue;
                foreach (var control in menu.controls)
                    if (control != null && control.subMenu != null)
                        pending.Push(control.subMenu);
            }
            return false;
        }

        internal static bool CanEditAsset(Object asset)
        {
            if (asset == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                (asset.hideFlags & HideFlags.NotEditable) != 0) return false;
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return !EditorUtility.IsPersistent(asset);
            if (!AssetDatabase.IsOpenForEdit(assetPath)) return false;

            try
            {
                string filePath;
                if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                    if (package == null || (package.source != PackageSource.Embedded && package.source != PackageSource.Local))
                        return false;
                    filePath = Path.Combine(package.resolvedPath,
                        assetPath.Substring(package.assetPath.Length).TrimStart('/', '\\'));
                }
                else
                {
                    if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return false;
                    filePath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, assetPath);
                }
                return File.Exists(filePath) && (File.GetAttributes(filePath) & FileAttributes.ReadOnly) == 0;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is ArgumentException || exception is System.Security.SecurityException)
            {
                return false;
            }
        }
    }
}
