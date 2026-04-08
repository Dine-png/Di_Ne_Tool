#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class DiNeMultiDresserMenu
{
    private const string PrefabPath = "Packages/com.dine.tool/Assets/MultiDresser/MultiDresser.prefab";
    private const string LocalFallbackPath = "Assets/Di Ne/MultiDresser/MultiDresser.prefab";

    [MenuItem("GameObject/Di Ne/Multi Dresser", false, 0)]
    public static void AddMultiDresser(MenuCommand menuCommand)
    {
        // 1. 패키지 내부에 생성된 프리팹 확인
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        
        // 2. 만약 패키지 내부에서 못 찾았다면, 로컬 경로에서 확인 (개발 단계 대비)
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalFallbackPath);
        }

        // 3. 그럼에도 발견하지 못했다면 즉석에서 임시 생성 후 에셋으로 저장
        if (prefab == null)
        {
            GameObject tempObj = new GameObject("Multi Dresser");
            tempObj.AddComponent<DiNeMultiDresser>();

            string targetPath = PrefabPath;
            
            try 
            {
                // 패키지 내부 물리 경로 파악
                string physicalPath = Path.GetFullPath(targetPath);
                string dir = Path.GetDirectoryName(physicalPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                prefab = PrefabUtility.SaveAsPrefabAsset(tempObj, targetPath);
            }
            catch
            {
                // 읽기 전용 패키지라 저장이 불가능한 경우, 로컬 Assets로 우회
                targetPath = LocalFallbackPath;
                string dir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                prefab = PrefabUtility.SaveAsPrefabAsset(tempObj, targetPath);
            }

            GameObject.DestroyImmediate(tempObj);
            AssetDatabase.Refresh();
        }

        // 4. 프리팹 기반으로 씬에 인스턴스 소환 (푸른색 아이콘)
        GameObject dresser = null;
        if (prefab != null)
        {
            dresser = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        else
        {
            // 최악의 경우 생성 실패 시 폴백 (일반 오브젝트)
            dresser = new GameObject("Multi Dresser");
            dresser.AddComponent<DiNeMultiDresser>();
        }

        // 5. 핑거/계층 정리
        if (menuCommand.context is GameObject parent && dresser != null)
        {
            GameObjectUtility.SetParentAndAlign(dresser, parent);
        }

        if (dresser != null)
        {
            Undo.RegisterCreatedObjectUndo(dresser, "Create Multi Dresser");
            EditorApplication.delayCall += () => { Selection.activeObject = dresser; };
        }
    }
}
#endif