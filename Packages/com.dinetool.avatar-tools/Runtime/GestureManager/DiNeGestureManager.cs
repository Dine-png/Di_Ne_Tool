using BlackStartX.GestureManager;
using UnityEngine;

namespace DiNeTool.GestureManager
{
    /// <summary>
    /// DiNe Gesture Manager — BlackStartX GestureManager 위에 DiNe UI를 얹는 래퍼 컴포넌트.
    /// GestureManager 컴포넌트를 자동으로 생성하고 인스펙터에서 숨깁니다.
    /// </summary>
    [AddComponentMenu("DiNe Tool/DiNe Gesture Manager")]
    [RequireComponent(typeof(BlackStartX.GestureManager.GestureManager))]
    [DisallowMultipleComponent]
    public class DiNeGestureManager : MonoBehaviour
    {
        private BlackStartX.GestureManager.GestureManager _gm;

        public BlackStartX.GestureManager.GestureManager GestureManager
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
            if (_gm != null)
                _gm.hideFlags = HideFlags.HideInInspector;
        }

        private void OnValidate()
        {
            var gm = GetComponent<BlackStartX.GestureManager.GestureManager>();
            if (gm != null) gm.hideFlags = HideFlags.HideInInspector;
        }
    }
}
