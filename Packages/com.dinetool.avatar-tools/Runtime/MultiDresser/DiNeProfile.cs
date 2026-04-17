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
        public Texture2D layerIcon;
        public List<GameObject> targets = new();
        public List<string> labels = new();
        public List<Texture2D> icons = new();
        public List<DiNeMultiDresser.LinkedGroup>       linkedObjects           = new();
        public List<DiNeMultiDresser.ShapeKeyMeshList>  perButtonShapeKeyStates = new();
        public List<DiNeMultiDresser.MaterialSwapList>  perButtonMaterialSwaps  = new();
        public GameObject                               particleObject;
    }

    // ── 아바타 식별 ──
    // FX Controller 에셋 GUID (아바타별 고유, 이름 변경과 무관)
    public string avatarControllerGUID;
    // 보조 식별: 아바타 루트 이름 (GUID 못 찾을 때 fallback)
    public string avatarName;

    public List<SavedLayer> savedLayers = new();
    public List<GameObject> shapeKeyTargets = new();
}
#endif
