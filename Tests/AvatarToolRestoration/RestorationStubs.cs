// Isolated harness only. SDK events/generators are stand-ins; Unity scene and asset APIs are real.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
namespace VRC.SDKBase.Editor { }
namespace VRC.SDKBase.Editor.BuildPipeline
{
    public enum VRCSDKRequestedBuildType { Avatar }
    public interface IVRCSDKBuildRequestedCallback { int callbackOrder { get; } bool OnBuildRequested(VRCSDKRequestedBuildType type); }
    public interface IVRCSDKPreprocessAvatarCallback { int callbackOrder { get; } bool OnPreprocessAvatar(GameObject root); }
    public interface IVRCSDKPostprocessAvatarCallback { int callbackOrder { get; } void OnPostprocessAvatar(); }
}
namespace VRC.SDK3A.Editor
{
    public interface IVRCSdkAvatarBuilderApi
    {
        event EventHandler<string> OnSdkBuildFinish, OnSdkBuildError, OnSdkUploadFinish, OnSdkUploadError;
    }
    public class TestBuilder : IVRCSdkAvatarBuilderApi
    {
        public event EventHandler<string> OnSdkBuildFinish, OnSdkBuildError, OnSdkUploadFinish, OnSdkUploadError;
        public void End(int kind)
        {
            switch(kind) {
                case 0: OnSdkBuildFinish?.Invoke(this, ""); break;
                case 1: OnSdkBuildError?.Invoke(this, ""); break;
                case 2: OnSdkUploadFinish?.Invoke(this, ""); break;
                case 3: OnSdkUploadError?.Invoke(this, ""); break;
            }
        }
    }
    public static class VRCSdkControlPanel
    {
        public static IVRCSdkAvatarBuilderApi Builder = new TestBuilder();
        public static bool TryGetBuilder<T>(out T builder) where T : class { builder = Builder as T; return builder != null; }
    }
}
public static class DiNeMultiSupporter { public static void ClearAllActivePreviews() { } }
public static class DiNeMultiIconGenerator { public static void GenerateIcons(DiNeMultiDresser d) { } }
public static class DiNeLightingBaker
{
    public static void NormalizeForPlayMode(GameObject root) { }
    public static void NormalizeForBuild(GameObject root) { }
    public static void CleanupTempAssets() { }
}
public static class DiNeSmartToggleGenerator
{
    public static void ApplyDresserTogglesToTemporaryAvatar(VRCAvatarDescriptor d, AnimatorController fx, VRCExpressionsMenu m, VRCExpressionParameters p, List<DiNeMultiDresser> tools, string folder) { }
    public static void ApplyToTemporaryAvatar(VRCAvatarDescriptor d, AnimatorController fx, VRCExpressionsMenu m, VRCExpressionParameters p, List<DiNeSmartToggle> tools, string folder) { }
}
public static class DiNeLightingGenerator
{
    public static void ApplyToTemporaryAvatar(VRCAvatarDescriptor d, AnimatorController fx, VRCExpressionsMenu m, VRCExpressionParameters p, List<DiNeLightingDesigner> tools, string folder) { }
}
