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

        // 루트가 가득 차서 버튼을 "Next ▶" 페이지로 넘겨둔 경우(같은 세션의 재호출)에는 그쪽에서 찾는다.
        string rootOverflowPath = $"{folder}/MultiDresser_RootNext.asset";
        var rootOverflow = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(rootOverflowPath);
        var rootHost = mainMenu;
        if (exist == null && rootOverflow != null && mainMenu.controls.Exists(c => c.subMenu == rootOverflow))
        {
            exist = rootOverflow.controls.Find(c => c.name == rootBtnName);
            if (exist != null) rootHost = rootOverflow;
        }

        // 옷장이 하나라 루트 버튼이 레이어 메뉴를 직접 가리키던 경우, 그 메뉴를 Main으로 재사용하지 않는다.
        string existPath = exist?.subMenu != null ? AssetDatabase.GetAssetPath(exist.subMenu) : null;
        bool existIsLayerMenu = existPath != null && existPath.StartsWith(folder + "/") && existPath != multiMenuPath;

        VRCExpressionsMenu multiMenu = null;
        if ((mergeIntoExistingMenu || exist != null) && exist?.subMenu != null && !existIsLayerMenu)
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
            else if (!existIsLayerMenu)
            {
                // 앞선 Multi Dresser가 옷장 하나로 바로가기를 만든 경우엔 Main에 그 옷장이 남아 있으므로 비우지 않는다.
                multiMenu.controls.Clear();
            }
        }

        // 이전 호출이 Main을 페이지로 나눴다면 다시 한 목록으로 펼친 뒤 마지막에 다시 나눈다.
        while (multiMenu.controls.Count > 0)
        {
            var last = multiMenu.controls[multiMenu.controls.Count - 1];
            string lastPath = last?.subMenu != null ? AssetDatabase.GetAssetPath(last.subMenu) : null;
            if (last == null || last.name != "Next ▶" || lastPath == null || !lastPath.StartsWith(multiMenuPath.Replace(".asset", "_"))) break;
            multiMenu.controls.RemoveAt(multiMenu.controls.Count - 1);
            multiMenu.controls.AddRange(last.subMenu.controls);
            last.subMenu.controls.Clear();
        }

        bool isAnyLayerAdded = false;
        int addedLayerCount = 0;
        VRCExpressionsMenu lastLayerMenu = null;

        foreach (var layer in context.layers)
        {
            if (layer.targets == null || layer.targets.Count <= 1) continue;

            string safeLayerName = string.IsNullOrEmpty(layer.layerName) ? "Layer" : layer.layerName;
            // ✅ 경로 변경: DiNe/MultiDresser/LayerName
            string paramName = $"DiNe/MultiDresser/{safeLayerName}";

            string layerMenuBasePath = $"{folder}/{string.Join("_", safeLayerName.Split(System.IO.Path.GetInvalidFileNameChars()))}_Menu";
            var layerMenu = LoadOrCreateClearedMenu(layerMenuBasePath + ".asset");

            var outfitControls = new List<VRCExpressionsMenu.Control>();
            for (int i = 1; i < layer.targets.Count; i++)
            {
                if (layer.targets[i] == null) continue;

                string label = (i < layer.labels.Count) ? layer.labels[i] : layer.targets[i].name;
                Texture2D icon = (i < layer.icons.Count) ? layer.icons[i] : null;

                outfitControls.Add(new VRCExpressionsMenu.Control
                {
                    name = label,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    icon = icon,
                    value = i,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName }
                });
            }

            if (outfitControls.Count == 0) continue;

            // 메뉴 한 페이지는 8칸이라, 넘치면 8번째 칸을 "Next ▶"로 써서 다음 페이지로 잇는다.
            var page = layerMenu;
            int pageNumber = 1;
            for (int c = 0; c < outfitControls.Count; c++)
            {
                if (page.controls.Count == 7 && outfitControls.Count - c > 1)
                {
                    var nextPage = LoadOrCreateClearedMenu($"{layerMenuBasePath}_{++pageNumber}.asset");
                    page.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = "Next ▶",
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = nextPage
                    });
                    EditorUtility.SetDirty(page);
                    page = nextPage;
                }
                page.controls.Add(outfitControls[c]);
            }

            EditorUtility.SetDirty(page);
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
            addedLayerCount++;
            lastLayerMenu = layerMenu;
        }

        // 옷장이 하나뿐이면 Main을 건너뛰고 바로 옷 리스트로 들어가게 한다.
        var rootTargetMenu = (addedLayerCount == 1 && multiMenu.controls.Count == 1) ? lastLayerMenu : multiMenu;

        // 카테고리가 8개를 넘으면 8번째 칸을 "Next ▶"로 써서 다음 페이지로 잇는다.
        var mainPage = multiMenu;
        for (int pageNumber = 2; mainPage.controls.Count > 8; pageNumber++)
        {
            var nextPage = LoadOrCreateClearedMenu(multiMenuPath.Replace(".asset", $"_{pageNumber}.asset"));
            nextPage.controls.AddRange(mainPage.controls.GetRange(7, mainPage.controls.Count - 7));
            mainPage.controls.RemoveRange(7, mainPage.controls.Count - 7);
            mainPage.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Next ▶",
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = nextPage
            });
            EditorUtility.SetDirty(mainPage);
            mainPage = nextPage;
        }
        EditorUtility.SetDirty(mainPage);

        EditorUtility.SetDirty(multiMenu);
        
        if (!isAnyLayerAdded)
        {
             Debug.LogWarning("⚠ 생성할 버튼이 없어 메뉴가 연결되지 않았습니다.");
             if (exist != null) rootHost.controls.Remove(exist);
        }
        else
        {
            if (exist != null)
            {
                exist.subMenu = rootTargetMenu;
            }
            else
            {
                if (mainMenu.controls.Count >= 8)
                {
                    // 루트 8칸이 다 찼으면 8번째 항목을 "Next ▶" 페이지로 옮겨 자리를 만든다.
                    // (mainMenu는 빌드/플레이용 임시 복사본이라 원본 메뉴는 바뀌지 않는다)
                    rootOverflow = LoadOrCreateClearedMenu(rootOverflowPath);
                    rootOverflow.controls.Add(mainMenu.controls[7]);
                    mainMenu.controls[7] = new VRCExpressionsMenu.Control
                    {
                        name = "Next ▶",
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = rootOverflow
                    };
                    rootHost = rootOverflow;
                }

                rootHost.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = rootBtnName,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = rootTargetMenu,
                    icon = defaultIcon
                });
            }
        }

        EditorUtility.SetDirty(rootHost);
        EditorUtility.SetDirty(mainMenu);
        AssetDatabase.SaveAssets();
    }

    private static VRCExpressionsMenu LoadOrCreateClearedMenu(string path)
    {
        var menu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(path);
        if (menu == null)
        {
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            AssetDatabase.CreateAsset(menu, path);
        }
        else
        {
            menu.controls.Clear();
        }
        return menu;
    }
}
#endif
