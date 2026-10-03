using System;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
namespace VRC.SDK3.Avatars.Components
{
    // Schema adapter used only by the isolated stub run. Real SDK runs exclude this file.
    public sealed class VRCAvatarDescriptor : MonoBehaviour
    {
        public enum AnimLayerType { Base, Additive, Gesture, Action, FX, Sitting, TPose, IKPose }
        [Serializable] public struct CustomAnimLayer
        {
            public AnimLayerType type;
            public RuntimeAnimatorController animatorController;
            public bool isDefault;
            public AvatarMask mask;
        }
        public bool customizeAnimationLayers;
        public CustomAnimLayer[] baseAnimationLayers, specialAnimationLayers;
        public VRCExpressionsMenu expressionsMenu;
        public VRCExpressionParameters expressionParameters;
    }
}
