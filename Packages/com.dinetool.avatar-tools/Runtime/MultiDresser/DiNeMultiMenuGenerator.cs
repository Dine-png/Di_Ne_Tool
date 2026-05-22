#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;

public static class DiNeMultiMenuGenerator
{
    public static void TryCreateExpressionMenu(
        DiNeMultiDresser context,
        string generatedRootFolder = "Assets/Di Ne/MultiDresser",
        bool mergeIntoExistingMenu = false)
    {
        var descriptor = context.rootTransform?.GetComponent<VRCAvatarDescriptor>();
        if (descriptor == null) return;

        // 수동 지정된 메뉴가 있으면 우선 사용, 없으면 descriptor에서 가져옴
        var mainMenu = context.expressionsMenu != null ? context.expressionsMenu : descriptor.expressionsMenu;
        if (mainMenu == null)
        {
            Debug.LogWarning("⚠ Expression Menu가 없습니다. 아바타에 설정하거나 Multi Dresser에서 직접 지정해주세요.");
            return;
        }

        string folder = $"{generatedRootFolder}/Menus";
        if (!System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        Texture2D defaultIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/MultiDresser/DNDresser.png");
        string rootBtnName = "Multi Dresser";
        string multiMenuPath = $"{folder}/MultiDresser_Main.asset";
        var exist = mainMenu.controls.Find(c => c.name == rootBtnName);

        VRCExpressionsMenu multiMenu = null;
        if ((mergeIntoExistingMenu || exist != null) && exist?.subMenu != null)
        {
            multiMenu = exist.subMenu;
        }
        else
        {
            multiMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(multiMenuPath);
            if (multiMenu == null)
            {
                multiMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(multiMenu, multiMenuPath);
            }
            else
            {
                multiMenu.controls.Clear();
            }
        }

        bool isAnyLayerAdded = false;

        foreach (var layer in context.layers)
        {
            if (layer.targets == null || layer.targets.Count <= 1) continue;

            string safeLayerName = string.IsNullOrEmpty(layer.layerName) ? "Layer" : layer.layerName;
            // ✅ 경로 변경: DiNe/MultiDresser/LayerName
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";

            string layerMenuPath = $"{folder}/{safeLayerName}_Menu.asset";
            var layerMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(layerMenuPath);
            if (layerMenu == null)
            {
                layerMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(layerMenu, layerMenuPath);
            }
            else
            {
                layerMenu.controls.Clear();
            }

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

            EditorUtility.SetDirty(layerMenu);

            var existingLayerControl = multiMenu.controls.Find(c => c.name == safeLayerName);
            if (existingLayerControl != null)
            {
                existingLayerControl.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                existingLayerControl.subMenu = layerMenu;
                existingLayerControl.icon = layer.layerIcon != null ? layer.layerIcon : defaultIcon;
            }
            else
            {
                multiMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = safeLayerName,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = layerMenu,
                    icon = layer.layerIcon != null ? layer.layerIcon : defaultIcon
                });
            }
            isAnyLayerAdded = true;
        }

        EditorUtility.SetDirty(multiMenu);
        
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
