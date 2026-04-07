using BlackStartX.GestureManager;
using UnityEngine;

namespace DiNeTool.GestureManager
{
    /// <summary>
    /// DiNe 인게임 체커가 내부적으로 씬에 생성하는 래퍼 컴포넌트.
    /// 직접 씬에 추가하지 않아도 됩니다 — EditorWindow에서 자동 관리합니다.
    /// </summary>
    [AddComponentMenu("")] // 컴포넌트 메뉴에서 숨김
    [RequireComponent(typeof(BlackStartX.GestureManager.GestureManager))]
    [DisallowMultipleComponent]
    public class DiNeGestureManager : MonoBehaviour
    {
        private BlackStartX.GestureManager.GestureManager _gm;

        public BlackStartX.GestureManager.GestureManager Core
        {
            get
            {
                if (_gm == null) _gm = GetComponent<BlackStartX.GestureManager.GestureManager>();
                return _gm;
            }
        }

        private void Awake()
        {
            _gm = GetComponent<BlackStartX.GestureManager.GestureManager>();
            // GestureManager 컴포넌트는 인스펙터에서 숨김
            if (_gm != null) _gm.hideFlags = HideFlags.HideInInspector;
        }
    }
}
