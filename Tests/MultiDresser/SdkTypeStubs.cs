// Only copied into the isolated regression project; never distributed in the package.
// The tests exercise the real Unity animation APIs and production dresser source.
// SDK/menu types are compile-time stand-ins; no VRChat or MA integration is claimed.
#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

namespace VRC.SDKBase { public interface IEditorOnly { } }
namespace VRC.SDK3.Avatars.Components
{
    public class VRCAvatarDescriptor : MonoBehaviour
    {
        public enum AnimLayerType { FX }
        public struct CustomAnimLayer
        {
            public AnimLayerType type;
            public RuntimeAnimatorController animatorController;
        }
        public CustomAnimLayer[] baseAnimationLayers, specialAnimationLayers;
        public VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu expressionsMenu;
        public VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters expressionParameters;
    }
}
namespace VRC.SDK3.Avatars.ScriptableObjects
{
    public class VRCExpressionsMenu : ScriptableObject
    {
        [System.Serializable]
        public class Control
        {
            public enum ControlType { Toggle, SubMenu }
            [System.Serializable]
            public class Parameter { public string name; }
            public string name;
            public ControlType type;
            public Texture2D icon;
            public float value;
            public Parameter parameter;
            public VRCExpressionsMenu subMenu;
        }
        public List<Control> controls = new List<Control>();
    }
    public class VRCExpressionParameters : ScriptableObject
    {
        public enum ValueType { Int, Bool }
        [System.Serializable]
        public class Parameter { public string name; public ValueType valueType; public bool saved; public float defaultValue; }
        public Parameter[] parameters = new Parameter[0];
    }
}
public static class DiNeMultiMenuGenerator
{
    public static void TryCreateExpressionMenu(DiNeMultiDresser dresser, string folder, bool merge = false) { }
}
#endif
