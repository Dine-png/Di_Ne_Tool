using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal enum DiNePhysBoneBatchSetting
{
    AllowGrabbing,
    AllowPosing,
    AllowCollision
}

internal enum DiNePhysBoneSettingState
{
    Disabled,
    Enabled,
    Custom,
    Unsupported
}

internal readonly struct DiNePhysBoneBatchSummary
{
    public int Total { get; }
    public int Enabled { get; }
    public int Disabled { get; }
    public int Custom { get; }
    public int Unsupported { get; }

    public DiNePhysBoneBatchSummary(int total, int enabled, int disabled, int custom, int unsupported)
    {
        Total = total;
        Enabled = enabled;
        Disabled = disabled;
        Custom = custom;
        Unsupported = unsupported;
    }
}

internal readonly struct DiNePhysBoneBatchResult
{
    public int Total { get; }
    public int Changed { get; }
    public int Unsupported { get; }

    public DiNePhysBoneBatchResult(int total, int changed, int unsupported)
    {
        Total = total;
        Changed = changed;
        Unsupported = unsupported;
    }
}

internal static class DiNePhysBoneBatchUtility
{
    private const string PhysBoneTypeName = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";

    public static DiNePhysBoneBatchSummary GetSummary(GameObject avatarRoot, DiNePhysBoneBatchSetting setting)
    {
        List<Component> physBones = FindPhysBones(avatarRoot);
        int enabled = 0;
        int disabled = 0;
        int custom = 0;
        int unsupported = 0;

        foreach (Component physBone in physBones)
        {
            switch (ReadState(physBone, setting))
            {
                case DiNePhysBoneSettingState.Enabled:
                    enabled++;
                    break;
                case DiNePhysBoneSettingState.Disabled:
                    disabled++;
                    break;
                case DiNePhysBoneSettingState.Custom:
                    custom++;
                    break;
                default:
                    unsupported++;
                    break;
            }
        }

        return new DiNePhysBoneBatchSummary(physBones.Count, enabled, disabled, custom, unsupported);
    }

    public static DiNePhysBoneBatchResult Apply(
        GameObject avatarRoot, DiNePhysBoneBatchSetting setting, bool enabled)
    {
        List<Component> physBones = FindPhysBones(avatarRoot);
        if (physBones.Count == 0)
            return new DiNePhysBoneBatchResult(0, 0, 0);

        string propertyName = GetPropertyName(setting);
        string undoName = $"Set PhysBone {propertyName}";
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        Undo.RecordObjects(physBones.Cast<UnityEngine.Object>().ToArray(), undoName);

        int changed = 0;
        int unsupported = 0;

        foreach (Component physBone in physBones)
        {
            var serializedObject = new SerializedObject(physBone);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !TrySetEnabled(property, enabled, out bool didChange))
            {
                unsupported++;
                continue;
            }

            if (!didChange)
                continue;

            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(physBone);
            EditorUtility.SetDirty(physBone);
            changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        return new DiNePhysBoneBatchResult(physBones.Count, changed, unsupported);
    }

    private static List<Component> FindPhysBones(GameObject avatarRoot)
    {
        if (avatarRoot == null)
            return new List<Component>();

        return avatarRoot.GetComponentsInChildren<Component>(true)
            .Where(IsPhysBone)
            .ToList();
    }

    private static bool IsPhysBone(Component component)
    {
        if (component == null)
            return false;

        Type type = component.GetType();
        while (type != null)
        {
            if (string.Equals(type.FullName, PhysBoneTypeName, StringComparison.Ordinal))
                return true;
            type = type.BaseType;
        }

        return false;
    }

    private static DiNePhysBoneSettingState ReadState(
        Component physBone, DiNePhysBoneBatchSetting setting)
    {
        if (physBone == null)
            return DiNePhysBoneSettingState.Unsupported;

        var serializedObject = new SerializedObject(physBone);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(GetPropertyName(setting));
        if (property == null)
            return DiNePhysBoneSettingState.Unsupported;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Enum:
                if (property.enumValueIndex < 0 || property.enumValueIndex >= property.enumNames.Length)
                    return DiNePhysBoneSettingState.Unsupported;

                string enumName = property.enumNames[property.enumValueIndex];
                if (string.Equals(enumName, "False", StringComparison.OrdinalIgnoreCase))
                    return DiNePhysBoneSettingState.Disabled;
                if (string.Equals(enumName, "True", StringComparison.OrdinalIgnoreCase))
                    return DiNePhysBoneSettingState.Enabled;
                return DiNePhysBoneSettingState.Custom;

            case SerializedPropertyType.Boolean:
                return property.boolValue
                    ? DiNePhysBoneSettingState.Enabled
                    : DiNePhysBoneSettingState.Disabled;

            case SerializedPropertyType.Integer:
                return property.intValue == 0
                    ? DiNePhysBoneSettingState.Disabled
                    : DiNePhysBoneSettingState.Enabled;

            default:
                return DiNePhysBoneSettingState.Unsupported;
        }
    }

    private static bool TrySetEnabled(SerializedProperty property, bool enabled, out bool changed)
    {
        changed = false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Enum:
                string targetName = enabled ? "True" : "False";
                int targetIndex = Array.FindIndex(property.enumNames,
                    name => string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase));
                if (targetIndex < 0)
                    return false;

                changed = property.enumValueIndex != targetIndex;
                if (changed)
                    property.enumValueIndex = targetIndex;
                return true;

            case SerializedPropertyType.Boolean:
                changed = property.boolValue != enabled;
                if (changed)
                    property.boolValue = enabled;
                return true;

            case SerializedPropertyType.Integer:
                int targetValue = enabled ? 1 : 0;
                changed = property.intValue != targetValue;
                if (changed)
                    property.intValue = targetValue;
                return true;

            default:
                return false;
        }
    }

    private static string GetPropertyName(DiNePhysBoneBatchSetting setting)
    {
        switch (setting)
        {
            case DiNePhysBoneBatchSetting.AllowGrabbing:
                return "allowGrabbing";
            case DiNePhysBoneBatchSetting.AllowPosing:
                return "allowPosing";
            case DiNePhysBoneBatchSetting.AllowCollision:
                return "allowCollision";
            default:
                throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
        }
    }
}
