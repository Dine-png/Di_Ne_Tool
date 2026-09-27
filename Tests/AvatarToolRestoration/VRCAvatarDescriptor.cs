using System;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
namespace VRC.SDK3.Avatars.Components
{
    public class VRCAvatarDescriptor : MonoBehaviour
    {
        public enum AnimLayerType { Base, FX }
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
