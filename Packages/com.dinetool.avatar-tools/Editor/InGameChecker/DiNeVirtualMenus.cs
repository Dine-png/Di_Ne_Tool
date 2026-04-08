using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor;

namespace DiNeTool.InGameChecker
{
    public static class DiNeVirtualMenus
    {
        public static VRCExpressionsMenu CreateOptionsMenu()
        {
            var root = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            root.name = "Options";

            // Extras
            var extras = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            extras.name = "Extras";
            extras.controls.Add(CreateToggle("IsLocal", "BSX_GM_IsLocal"));
            extras.controls.Add(CreateRadial("Gesture\nRight Weight", "GestureRightWeight", "BSX_GM_GestureRightWeight"));
            extras.controls.Add(CreateToggle("MuteSelf", "BSX_GM_MuteSelf"));
            extras.controls.Add(CreateToggle("InStation", "BSX_GM_Seated"));
            extras.controls.Add(CreateToggle("Earmuffs", "BSX_GM_Earmuffs"));
            extras.controls.Add(CreateRadial("Gesture\nLeft Weight", "GestureLeftWeight", "BSX_GM_GestureLeftWeight"));
            extras.controls.Add(CreateToggle("IsOnFriendsList", "BSX_GM_IsOnFriendsList"));

            // Tracking
            var tracking = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            tracking.name = "Tracking";
            tracking.controls.Add(CreateTrackingToggle("Uninitialized", 0, "BSX_GM_Uninitialized"));
            tracking.controls.Add(CreateTrackingToggle("Generic", 1, "BSX_GM_Generic"));
            tracking.controls.Add(CreateTrackingToggle("Hands-only", 2, "BSX_GM_HandsOnly"));
            tracking.controls.Add(CreateToggle("VRMode", "BSX_GM_VRMode"));
            tracking.controls.Add(CreateTrackingToggle("Head And Hands", 3, "BSX_GM_HeadHands"));
            tracking.controls.Add(CreateTrackingToggle("4-Point VR", 4, "BSX_GM_FourPoint"));
            tracking.controls.Add(CreateTrackingToggle("Full Body", 6, "BSX_GM_FullBody"));

            // States
            var states = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            states.name = "States";
            states.controls.Add(CreateToggle("AFK", "BSX_GM_AFK"));
            states.controls.Add(CreateRadial("Visemes", "Viseme", "BSX_GM_Visemes")); // Usually Viseme is an Int, but radial works fine to test? Actually it's an int parameter usually.
            states.controls.Add(CreateToggle("Seated", "BSX_GM_Seated"));
            states.controls.Add(CreateToggle("Avatar Culling\n(Animator Enabled)", "IsAnimatorEnabled", "BSX_GM_IsAnimatorEnabled"));

            // Locomotion
            var locomotion = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            locomotion.name = "Locomotion";
            locomotion.controls.Add(CreateToggle("Grounded", "BSX_GM_Grounded"));
            locomotion.controls.Add(CreateRadial("Falling Speed", "VelocityY", "BSX_GM_FallingSpeed"));
            locomotion.controls.Add(CreateRadial("Upright", "Upright", "BSX_GM_Upright"));
            
            // X and Z velocity as a TwoAxis Puppet?
            var velocityPuppet = new VRCExpressionsMenu.Control
            {
                name = "Velocity",
                type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
                icon = LoadIcon("BSX_GM_Velocity"),
                subParameters = new[]
                {
                    new VRCExpressionsMenu.Control.Parameter { name = "" },
                    new VRCExpressionsMenu.Control.Parameter { name = "VelocityX" },
                    new VRCExpressionsMenu.Control.Parameter { name = "VelocityZ" },
                    new VRCExpressionsMenu.Control.Parameter { name = "" }
                }
            };
            locomotion.controls.Add(velocityPuppet);

            // Add all to root
            root.controls.Add(CreateSubMenu("Extra", extras, "BSX_GM_Extras"));
            root.controls.Add(CreateSubMenu("Tracking", tracking, "BSX_GM_HeadHands"));
            root.controls.Add(CreateSubMenu("States", states, "BSX_GM_AFK"));
            root.controls.Add(CreateSubMenu("Locomotion", locomotion, "BSX_GM_Velocity"));

            return root;
        }

        private static Texture2D LoadIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"Packages/com.dine.tool/Assets/RadialMenuIcons/{name}.png");
        }

        private static VRCExpressionsMenu.Control CreateToggle(string paramName, string iconName)
        {
            return new VRCExpressionsMenu.Control
            {
                name = paramName,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName },
                value = 1f,
                icon = LoadIcon(iconName)
            };
        }

        private static VRCExpressionsMenu.Control CreateToggle(string displayName, string paramName, string iconName)
        {
            return new VRCExpressionsMenu.Control
            {
                name = displayName,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName },
                value = 1f,
                icon = LoadIcon(iconName)
            };
        }

        private static VRCExpressionsMenu.Control CreateTrackingToggle(string displayName, float activeValue, string iconName)
        {
            return new VRCExpressionsMenu.Control
            {
                name = displayName,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "TrackingType" },
                value = activeValue,
                icon = LoadIcon(iconName)
            };
        }

        private static VRCExpressionsMenu.Control CreateRadial(string displayName, string paramName, string iconName)
        {
            return new VRCExpressionsMenu.Control
            {
                name = displayName,
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = paramName } },
                icon = LoadIcon(iconName)
            };
        }

        private static VRCExpressionsMenu.Control CreateSubMenu(string displayName, VRCExpressionsMenu subMenu, string iconName)
        {
            return new VRCExpressionsMenu.Control
            {
                name = displayName,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = subMenu,
                icon = LoadIcon(iconName)
            };
        }
    }
}
