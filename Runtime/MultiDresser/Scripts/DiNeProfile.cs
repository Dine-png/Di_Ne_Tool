#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DiNeMultiProfile", menuName = "DiNe/MultiProfile", order = 1)]
public class DiNeProfile : ScriptableObject
{
    [System.Serializable]
    public class SavedLayer
    {
        public string layerName;
        public Texture2D layerIcon; // ✅ 추가됨: 카테고리 아이콘 저장용
        public List<GameObject> targets = new();
        public List<string> labels = new();
        public List<Texture2D> icons = new();
        
        // DiNeMultiDresser 클래스가 #if UNITY_EDITOR로 감싸져 있기 때문에,
        // 이 파일도 똑같이 감싸주지 않으면 빌드할 때 'DiNeMultiDresser를 찾을 수 없다'는 오류가 납니다.
        public List<DiNeMultiDresser.LinkedGroup> linkedObjects = new();
        public List<DiNeMultiDresser.ShapeKeyMeshList> perButtonShapeKeyStates = new();
    }

    public List<SavedLayer> savedLayers = new();
    public List<GameObject> shapeKeyTargets = new();
}
#endif