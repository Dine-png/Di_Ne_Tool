#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public static class ExpressionEditorSdkDescription
{
    public static void Run()
    {
        var lines = new List<string>();
        lines.Add("ActiveEditorTracker public ForceRebuild: " + (typeof(ActiveEditorTracker).GetMethod("ForceRebuild", BindingFlags.Public | BindingFlags.Instance) != null));
        foreach (Type type in new[] { typeof(VRCExpressionsMenu), typeof(VRCExpressionsMenu.Control), typeof(VRCExpressionsMenu.Control.Parameter), typeof(VRCExpressionsMenu.Control.Label), typeof(VRCExpressionParameters), typeof(VRCExpressionParameters.Parameter), typeof(VRCAvatarDescriptor) })
        {
            lines.Add(type.FullName + " | " + type.Assembly.GetName().Name + " | " + type.Assembly.Location);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                lines.Add("  field " + field.FieldType.FullName + " " + field.Name);
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                lines.Add("  property " + property.PropertyType.FullName + " " + property.Name);
        }
        File.WriteAllLines("ExpressionEditorRegression-sdk-description.txt", lines);
        EditorApplication.Exit(0);
    }
}
#endif
