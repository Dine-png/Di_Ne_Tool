#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;

public static class DiNeMultiMenuGenerator
{
    public static void TryCreateExpressionMenu(DiNeMultiDresser context)
    {
        var descriptor = context.rootTransform?.GetComponent<VRCAvatarDescriptor>();
        if (descriptor == null) return;

        var mainMenu = descriptor.expressionsMenu;
        if (mainMenu == null)
        {
            Debug.LogWarning("⚠ 아바타에 Expression Menu가 없습니다.");
            return;
        }

        string folder = "Assets/Di Ne/MultiDresser/Menus";
        if (!System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        Texture2D defaultIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Di Ne/MultiDresser/Assets/DNDresser.png");

        VRCExpressionsMenu multiMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        bool isAnyLayerAdded = false;

        foreach (var layer in context.layers)
        {
            if (layer.targets == null || layer.targets.Count <= 1) continue;

            string safeLayerName = string.IsNullOrEmpty(layer.layerName) ? "Layer" : layer.layerName;
            // ✅ 경로 변경: DiNe/MultiDresser/LayerName
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";

            VRCExpressionsMenu layerMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

            int addedButtonCount = 0;
            for (int i = 1; i < layer.targets.Count; i++)
            {
                if (layer.targets[i] == null) continue;

                string label = (i < layer.labels.Count) ? layer.labels[i] : layer.targets[i].name;
                Texture2D icon = (i < layer.icons.Count) ? layer.icons[i] : null;

                layerMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = label,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle, 
                    icon = icon,
                    value = i,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName }
                });
                addedButtonCount++;
            }

            if (addedButtonCount == 0) continue;

            string layerMenuPath = $"{folder}/{safeLayerName}_Menu.asset";
            AssetDatabase.CreateAsset(layerMenu, layerMenuPath);
            EditorUtility.SetDirty(layerMenu);

            multiMenu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = safeLayerName,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = layerMenu,
                icon = layer.layerIcon != null ? layer.layerIcon : defaultIcon
            });
            isAnyLayerAdded = true;
        }

        string multiMenuPath = $"{folder}/MultiDresser_Main.asset";
        AssetDatabase.CreateAsset(multiMenu, multiMenuPath);
        EditorUtility.SetDirty(multiMenu);

        string rootBtnName = "Multi Dresser";
        var exist = mainMenu.controls.Find(c => c.name == rootBtnName);
        
        if (!isAnyLayerAdded)
        {
             Debug.LogWarning("⚠ 생성할 버튼이 없어 메뉴가 연결되지 않았습니다.");
             if (exist != null) mainMenu.controls.Remove(exist);
        }
        else
        {
            if (exist != null)
            {
                exist.subMenu = multiMenu; 
            }
            else
            {
                if (mainMenu.controls.Count < 8)
                {
                    mainMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = rootBtnName,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = multiMenu,
                        icon = defaultIcon
                    });
                }
                else
                {
                    Debug.LogError("❌ 메인 메뉴 꽉 참 (최대 8개)");
                }
            }
        }
        
        EditorUtility.SetDirty(mainMenu);
        AssetDatabase.SaveAssets();
    }
}
#endif