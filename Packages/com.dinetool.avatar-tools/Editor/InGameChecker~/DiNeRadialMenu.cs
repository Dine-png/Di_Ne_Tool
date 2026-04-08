using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DiNeTool.InGameChecker
{
    /// <summary>
    /// VRChat 레이디얼 메뉴를 IMGUI로 재현하는 클래스.
    /// EditorWindow.OnGUI() 안에서 Draw()를 호출하면 된다.
    /// </summary>
    public class DiNeRadialMenu
    {
        // ─── Constants ───────────────────────────────────────────────────────
        private const float MenuRadius   = 140f;
        private const float InnerRadius  = 50f;
        private const float IconSize     = 28f;
        private const float SliceGap     = 1.5f;
        private const int   CircleSegments = 64;

        // ─── Colors (VRChat 스타일) ──────────────────────────────────────────
        private static readonly Color ColBg       = new(0.12f, 0.15f, 0.17f, 0.95f);
        private static readonly Color ColBorder   = new(0.05f, 0.45f, 0.50f, 1f);
        private static readonly Color ColSelected = new(0.07f, 0.55f, 0.58f, 1f);
        private static readonly Color ColCenter   = new(0.06f, 0.27f, 0.29f, 1f);
        private static readonly Color ColCenterSel= new(0.06f, 0.20f, 0.22f, 1f);

        // ─── Default Icons ───────────────────────────────────────────────────
        private static Texture2D _iconBack;
        private static Texture2D _iconBackHome;
        private static Texture2D _iconToggle;
        private static Texture2D _iconRadial;
        private static Texture2D _iconTwoAxis;
        private static Texture2D _iconFourAxis;
        private static Texture2D _iconSubMenu;

        private static Texture2D LoadIconFallback(string tName)
        {
            string prefix = "Packages/com.dine.tool/Assets/RadialMenuIcons/";
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + tName);
            if (tex != null) return tex;
            var guids = UnityEditor.AssetDatabase.FindAssets(tName.Replace(".png", "") + " t:Texture2D");
            if (guids.Length > 0) return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        private void LoadIconsIfNeeded()
        {
            if (_iconBack != null) return;
            _iconBack = LoadIconFallback("BSX_GM_Back.png");
            _iconBackHome = LoadIconFallback("BSX_GM_BackHome.png");
            _iconToggle = LoadIconFallback("BSX_GM_Toggle.png");
            _iconRadial = LoadIconFallback("BSX_GM_Radial.png");
            _iconTwoAxis = LoadIconFallback("BSX_GM_2_Axis.png");
            _iconFourAxis = LoadIconFallback("BSX_GM_4_Axis.png");
            _iconSubMenu = LoadIconFallback("BSX_GM_Expressions.png");
        }
        private static readonly Color ColText     = new(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color ColTextDim  = new(0.55f, 0.55f, 0.60f, 1f);
        private static readonly Color ColActive   = new(0.20f, 0.75f, 0.70f, 1f);

        // ─── State ──────────────────────────────────────────────────────────
        private DiNeAvatarModule _module;
        private VRCExpressionsMenu _rootMenu;

        // 네비게이션 스택
        private readonly Stack<(VRCExpressionsMenu menu, string paramName, float paramValue)> _menuStack = new();
        private VRCExpressionsMenu _currentMenu;

        // 인터랙션
        private int  _hoverIndex = -1;
        private int  _selectedIndex = -1;
        private bool _isDragging;
        private Vector2 _mousePos;

        // Puppet 상태
        private PuppetMode _puppetMode = PuppetMode.None;
        private VRCExpressionsMenu.Control _puppetControl;
        private float _puppetRadialValue;
        private Vector2 _puppetAxisValue;

        private enum PuppetMode { None, Radial, TwoAxis, FourAxis }

        // ─── Init ────────────────────────────────────────────────────────────
        public void Init(DiNeAvatarModule module)
        {
            _module = module;
            _rootMenu = module.Descriptor.expressionsMenu;
            _currentMenu = _rootMenu;
            _menuStack.Clear();
            _puppetMode = PuppetMode.None;
        }

        public void SetRootMenu(VRCExpressionsMenu newRoot)
        {
            _rootMenu = newRoot;
            _currentMenu = _rootMenu;
            _menuStack.Clear();
            _puppetMode = PuppetMode.None;
        }

        public bool HasMenu => _currentMenu != null && _currentMenu.controls.Count > 0;

        // ═════════════════════════════════════════════════════════════════════
        // DRAW — EditorWindow.OnGUI() 에서 호출
        // ═════════════════════════════════════════════════════════════════════
        public void Draw(Rect area)
        {
            if (_currentMenu == null || _module == null) return;

            LoadIconsIfNeeded();

            var center = area.center;
            var evt = Event.current;
            _mousePos = evt.mousePosition - center;

            // 배경 원
            DrawFilledCircle(center, MenuRadius + 4, ColBg);
            DrawCircleOutline(center, MenuRadius + 4, ColBorder, 2f);

            if (_puppetMode != PuppetMode.None)
            {
                DrawPuppet(center);
                HandlePuppetInput(evt, center);
                return;
            }

            var controls = _currentMenu.controls;
            int count = controls.Count;
            if (count == 0) return;

            // 마우스 거리 & 각도로 hover 계산
            float mouseDist = _mousePos.magnitude;
            float mouseAngle = GetAngle(_mousePos);

            if (mouseDist > InnerRadius && mouseDist < MenuRadius + 10)
                _hoverIndex = GetSliceIndex(mouseAngle, count);
            else if (mouseDist <= InnerRadius)
                _hoverIndex = -2; // 중앙 (Back)
            else
                _hoverIndex = -1;

            // 슬라이스 그리기 (DrawSolidArc 사용)
            for (int i = 0; i < count; i++)
            {
                bool isHover = (i == _hoverIndex);
                DrawSlice(center, i, count, controls[i], isHover);
            }

            // 도넛 구멍 뚫기 (안쪽 부분 가리기)
            DrawFilledCircle(center, InnerRadius + SliceGap, ColBg);

            // 구분선 / 테두리선
            DrawSliceBorders(center, count);

            // 문자열 / 아이콘 패스 (도넛 구멍을 뚫은 뒤 그리기 위함)
            for (int i = 0; i < count; i++)
            {
                bool isHover = (i == _hoverIndex);
                DrawSliceContent(center, i, count, controls[i], isHover);
            }

            // 중앙 원 (Back 버튼)
            bool centerHover = (_hoverIndex == -2);
            DrawFilledCircle(center, InnerRadius, centerHover ? ColCenterSel : ColCenter);
            DrawCircleOutline(center, InnerRadius, ColBorder, 1.5f);

            var iconImg = _menuStack.Count > 0 ? _iconBack : _iconBackHome;
            if (iconImg != null)
            {
                var prevColor = GUI.color;
                GUI.color = centerHover ? new Color(1, 1, 1, 0.9f) : new Color(0.8f, 0.8f, 0.8f, 0.9f);
                GUI.DrawTexture(new Rect(center.x - 20, center.y - 20, 40, 40), iconImg, ScaleMode.ScaleToFit);
                GUI.color = prevColor;
            }
            else
            {
                // 중앙 텍스트 (아이콘 없을때 대비)
                string centerText = _menuStack.Count > 0 ? "Back" : "●";
                DrawCenteredText(center, centerText, centerHover ? ColText : ColTextDim, 12);
            }

            // 커서
            if (mouseDist > 5f && area.Contains(evt.mousePosition))
                DrawCursor(center + Vector2.ClampMagnitude(_mousePos, MenuRadius - 10));

            // 클릭 처리
            HandleMenuInput(evt, center, controls);
        }

        // ═════════════════════════════════════════════════════════════════════
        // SLICE DRAWING
        // ═════════════════════════════════════════════════════════════════════
        private void DrawSlice(Vector2 center, int index, int total, VRCExpressionsMenu.Control control, bool hover)
        {
            float anglePerSlice = 360f / total;
            float startAngle = -90f + index * anglePerSlice + 1.2f;
            float endAngle = -90f + (index + 1) * anglePerSlice - 1.2f;

            Color fillColor = hover ? ColSelected : ColBg;
            if (IsControlActive(control)) fillColor = hover ? ColSelected : new Color(0.10f, 0.40f, 0.42f, 0.95f);

            // 무결점 안티앨리어싱 원호 렌더링. 중앙 구멍은 루프 밖에서 뚫음.
            using (new Handles.DrawingScope(fillColor))
            {
                Handles.DrawSolidArc(center, Vector3.forward, AngleToVector(startAngle), endAngle - startAngle, MenuRadius);
            }
        }

        private void DrawSliceContent(Vector2 center, int index, int total, VRCExpressionsMenu.Control control, bool hover)
        {
            float anglePerSlice = 360f / total;
            float startAngle = -90f + index * anglePerSlice + 1.2f;
            float endAngle = -90f + (index + 1) * anglePerSlice - 1.2f;

            float midAngle = (startAngle + endAngle) / 2f;
            float textDist = (InnerRadius + MenuRadius) / 2f;
            var textPos = center + AngleToVector(midAngle) * textDist;

            Texture2D tex = control.icon;
            if (tex == null)
            {
                switch (control.type)
                {
                    case VRCExpressionsMenu.Control.ControlType.Button:
                    case VRCExpressionsMenu.Control.ControlType.Toggle:
                        tex = _iconToggle; break;
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                        tex = _iconSubMenu; break;
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                        tex = _iconRadial; break;
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        tex = _iconTwoAxis; break;
                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                        tex = _iconFourAxis; break;
                }
            }

            // 아이콘
            if (tex != null)
            {
                var iconRect = new Rect(textPos.x - IconSize / 2, textPos.y - IconSize / 2 - 8, IconSize, IconSize);
                var prevColor = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.9f);
                GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                GUI.color = prevColor;
                textPos.y += 10;
            }

            DrawCenteredText(textPos, control.name, hover ? ColText : ColTextDim, 10);
            DrawControlTypeIndicator(textPos, control);
        }

        private void DrawControlTypeIndicator(Vector2 textPos, VRCExpressionsMenu.Control control)
        {
            string indicator = control.type switch
            {
                VRCExpressionsMenu.Control.ControlType.SubMenu       => "▸",
                VRCExpressionsMenu.Control.ControlType.RadialPuppet  => "◎",
                VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet => "✚",
                VRCExpressionsMenu.Control.ControlType.FourAxisPuppet=> "✦",
                _ => null
            };
            if (indicator != null)
            {
                var pos = new Vector2(textPos.x + 20, textPos.y - 6);
                DrawCenteredText(pos, indicator, ColActive, 8);
            }
        }

        private void DrawSliceBorders(Vector2 center, int count)
        {
            // Angular Gap을 적용했으므로, 얇은 선 그리기 대신 각 슬라이스의 테두리를 따라 그려야 VRC 느낌이 더 살지만,
            // 기본 Border 선도 유지합니다. 겹치지 않게 조절 가능.
            float anglePerSlice = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = -90f + i * anglePerSlice - 0.5f;
                float angle2 = -90f + i * anglePerSlice + 0.5f;
                // 약간 두꺼운 블랙 라인으로 파이를 나누는 효과
                var dir1 = AngleToVector(angle);
                var dir2 = AngleToVector(angle2);
                DrawLine(center + dir1 * InnerRadius, center + dir1 * MenuRadius, ColBorder, 1f);
                DrawLine(center + dir2 * InnerRadius, center + dir2 * MenuRadius, ColBorder, 1f);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // PUPPET DRAWING
        // ═════════════════════════════════════════════════════════════════════
        private void DrawPuppet(Vector2 center)
        {
            switch (_puppetMode)
            {
                case PuppetMode.Radial:
                    DrawRadialPuppet(center);
                    break;
                case PuppetMode.TwoAxis:
                case PuppetMode.FourAxis:
                    DrawAxisPuppet(center);
                    break;
            }

            // 중앙 Back 버튼
            bool centerHover = _mousePos.magnitude <= InnerRadius;
            DrawFilledCircle(center, InnerRadius, centerHover ? ColCenterSel : ColCenter);
            DrawCircleOutline(center, InnerRadius, ColBorder, 1.5f);
            DrawCenteredText(center, "Back", centerHover ? ColText : ColTextDim, 12);
        }

        private void DrawRadialPuppet(Vector2 center)
        {
            // 외부 원
            DrawFilledCircle(center, MenuRadius, ColBg);
            DrawCircleOutline(center, MenuRadius, ColBorder, 2f);

            // 진행 아크
            float progressAngle = _puppetRadialValue * 360f;
            if (progressAngle > 0.5f)
            {
                using (new Handles.DrawingScope(ColSelected))
                {
                    Handles.DrawSolidArc(center, Vector3.forward, AngleToVector(-90f), progressAngle, MenuRadius - 2);
                }
                DrawFilledCircle(center, InnerRadius + 2, ColBg);
            }

            // 화살표 (현재 위치)
            float arrowAngle = -90f + progressAngle;
            var arrowPos = center + AngleToVector(arrowAngle) * (MenuRadius - 15);
            DrawFilledCircle(arrowPos, 8f, ColActive);

            // 퍼센트 텍스트
            string pctText = $"{Mathf.RoundToInt(_puppetRadialValue * 100)}%";
            DrawCenteredText(center + new Vector2(0, -MenuRadius + 25), pctText, ColText, 14);

            // 컨트롤 이름
            if (_puppetControl != null)
                DrawCenteredText(center + new Vector2(0, MenuRadius - 25), _puppetControl.name, ColTextDim, 10);
        }

        private void DrawAxisPuppet(Vector2 center)
        {
            // 외부 원
            DrawFilledCircle(center, MenuRadius, ColBg);
            DrawCircleOutline(center, MenuRadius, ColBorder, 2f);

            // 십자 가이드
            DrawLine(center + new Vector2(-MenuRadius + 10, 0), center + new Vector2(MenuRadius - 10, 0), ColBorder, 1f);
            DrawLine(center + new Vector2(0, -MenuRadius + 10), center + new Vector2(0, MenuRadius - 10), ColBorder, 1f);

            // 축 라벨
            var subParams = _puppetControl?.subParameters;
            if (subParams != null)
            {
                float labelDist = MenuRadius - 20;
                if (subParams.Length > 0) DrawCenteredText(center + new Vector2(0, -labelDist), subParams[0].name, ColTextDim, 9); // Up
                if (subParams.Length > 1) DrawCenteredText(center + new Vector2(labelDist, 0),  subParams[1].name, ColTextDim, 9); // Right
                if (subParams.Length > 2) DrawCenteredText(center + new Vector2(0, labelDist),  subParams[2].name, ColTextDim, 9); // Down
                if (subParams.Length > 3) DrawCenteredText(center + new Vector2(-labelDist, 0), subParams[3].name, ColTextDim, 9); // Left
            }

            // 커서 (현재 축 값)
            var cursorOffset = _puppetAxisValue * (MenuRadius - InnerRadius - 10);
            var cursorPos = center + cursorOffset;
            DrawFilledCircle(cursorPos, 12f, ColActive);
            DrawCircleOutline(cursorPos, 12f, ColBorder, 1.5f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // INPUT HANDLING
        // ═════════════════════════════════════════════════════════════════════
        private void HandleMenuInput(Event evt, Vector2 center, List<VRCExpressionsMenu.Control> controls)
        {
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                _isDragging = true;
                _selectedIndex = _hoverIndex;
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 0 && _isDragging)
            {
                _isDragging = false;

                if (_hoverIndex == -2) // 중앙 Back
                {
                    GoBack();
                }
                else if (_hoverIndex >= 0 && _hoverIndex < controls.Count)
                {
                    ActivateControl(controls[_hoverIndex]);
                }
                _selectedIndex = -1;
                evt.Use();
            }

            if (evt.type == EventType.Repaint || evt.type == EventType.Layout)
                HandleUtility.Repaint();
        }

        private void HandlePuppetInput(Event evt, Vector2 center)
        {
            float dist = _mousePos.magnitude;
            float clamp = MenuRadius - 20;

            switch (_puppetMode)
            {
                case PuppetMode.Radial:
                    if (dist > InnerRadius)
                    {
                        float angle = GetAngle(_mousePos);
                        _puppetRadialValue = Mathf.Clamp01((angle + 180f) / 360f);
                        // 위쪽(12시)이 0, 시계방향으로 증가
                        _puppetRadialValue = Mathf.Clamp01((-Mathf.Atan2(_mousePos.x, _mousePos.y) * Mathf.Rad2Deg + 180f) / 360f);
                        ApplyPuppetValues();
                    }
                    break;

                case PuppetMode.TwoAxis:
                case PuppetMode.FourAxis:
                    var clamped = Vector2.ClampMagnitude(_mousePos, clamp);
                    _puppetAxisValue = clamped / clamp;
                    ApplyPuppetValues();
                    break;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (dist <= InnerRadius)
                {
                    ClosePuppet();
                    evt.Use();
                }
            }

            if (evt.type == EventType.Repaint || evt.type == EventType.Layout)
                HandleUtility.Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        // CONTROL ACTIVATION
        // ═════════════════════════════════════════════════════════════════════
        private void ActivateControl(VRCExpressionsMenu.Control control)
        {
            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    SetControlParam(control, control.value);
                    // 버튼은 누르면 즉시 해제
                    EditorApplication.delayCall += () => SetControlParam(control, 0);
                    break;

                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    bool isActive = IsControlActive(control);
                    SetControlParam(control, isActive ? 0 : control.value);
                    break;

                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    if (control.subMenu != null)
                    {
                        _menuStack.Push((_currentMenu, control.parameter?.name, control.value));
                        SetControlParam(control, control.value);
                        _currentMenu = control.subMenu;
                    }
                    break;

                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    _puppetMode = PuppetMode.Radial;
                    _puppetControl = control;
                    _puppetRadialValue = GetSubParamValue(control, 0);
                    SetControlParam(control, control.value);
                    break;

                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    _puppetMode = PuppetMode.TwoAxis;
                    _puppetControl = control;
                    _puppetAxisValue = new Vector2(GetSubParamValue(control, 0), GetSubParamValue(control, 1));
                    SetControlParam(control, control.value);
                    break;

                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    _puppetMode = PuppetMode.FourAxis;
                    _puppetControl = control;
                    SetControlParam(control, control.value);
                    break;
            }
        }

        private void ClosePuppet()
        {
            if (_puppetControl != null)
                SetControlParam(_puppetControl, 0);

            // Puppet 서브 파라미터도 0으로 리셋
            if (_puppetControl?.subParameters != null)
            {
                foreach (var sub in _puppetControl.subParameters)
                {
                    if (!string.IsNullOrEmpty(sub.name) && _module.Params.TryGetValue(sub.name, out var p))
                        p.Set(0);
                }
            }

            _puppetMode = PuppetMode.None;
            _puppetControl = null;
        }

        private void ApplyPuppetValues()
        {
            if (_puppetControl?.subParameters == null) return;

            switch (_puppetMode)
            {
                case PuppetMode.Radial:
                    SetSubParam(_puppetControl, 0, _puppetRadialValue);
                    break;

                case PuppetMode.TwoAxis:
                    SetSubParam(_puppetControl, 0, _puppetAxisValue.y);  // Vertical (Up/Down)
                    SetSubParam(_puppetControl, 1, _puppetAxisValue.x);  // Horizontal (Left/Right)
                    break;

                case PuppetMode.FourAxis:
                    // 4축: Up, Right, Down, Left (각각 0~1)
                    SetSubParam(_puppetControl, 0, Mathf.Max(0, -_puppetAxisValue.y)); // Up
                    SetSubParam(_puppetControl, 1, Mathf.Max(0,  _puppetAxisValue.x)); // Right
                    SetSubParam(_puppetControl, 2, Mathf.Max(0,  _puppetAxisValue.y)); // Down
                    SetSubParam(_puppetControl, 3, Mathf.Max(0, -_puppetAxisValue.x)); // Left
                    break;
            }
        }

        // ─── Navigation ──────────────────────────────────────────────────────
        private void GoBack()
        {
            if (_menuStack.Count > 0)
            {
                var (menu, paramName, _) = _menuStack.Pop();
                // 서브메뉴 파라미터 리셋
                if (!string.IsNullOrEmpty(paramName) && _module.Params.TryGetValue(paramName, out var p))
                    p.Set(0);
                _currentMenu = menu;
            }
        }

        public void Reset()
        {
            _menuStack.Clear();
            _currentMenu = _rootMenu;
            _puppetMode = PuppetMode.None;
            _puppetControl = null;
        }

        // ─── Parameter Helpers ───────────────────────────────────────────────
        private void SetControlParam(VRCExpressionsMenu.Control control, float value)
        {
            if (control.parameter == null || string.IsNullOrEmpty(control.parameter.name)) return;
            if (_module.Params.TryGetValue(control.parameter.name, out var param))
                param.Set(value);
        }

        private void SetSubParam(VRCExpressionsMenu.Control control, int index, float value)
        {
            if (control.subParameters == null || index >= control.subParameters.Length) return;
            var sub = control.subParameters[index];
            if (string.IsNullOrEmpty(sub.name)) return;
            if (_module.Params.TryGetValue(sub.name, out var param))
                param.Set(value);
        }

        private float GetSubParamValue(VRCExpressionsMenu.Control control, int index)
        {
            if (control.subParameters == null || index >= control.subParameters.Length) return 0;
            var sub = control.subParameters[index];
            if (string.IsNullOrEmpty(sub.name)) return 0;
            return _module.Params.TryGetValue(sub.name, out var param) ? param.FloatValue() : 0;
        }

        private bool IsControlActive(VRCExpressionsMenu.Control control)
        {
            if (control.parameter == null || string.IsNullOrEmpty(control.parameter.name)) return false;
            if (!_module.Params.TryGetValue(control.parameter.name, out var param)) return false;
            return Mathf.Approximately(param.FloatValue(), control.value);
        }

        // ═════════════════════════════════════════════════════════════════════
        // DRAWING PRIMITIVES
        // ═════════════════════════════════════════════════════════════════════
        private static float GetAngle(Vector2 v) =>
            Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        private static Vector2 AngleToVector(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static int GetSliceIndex(float mouseAngleDeg, int count)
        {
            // 슬라이스 0은 위쪽(-90도)에서 시작
            float adjusted = (mouseAngleDeg + 90f + 360f) % 360f;
            float perSlice = 360f / count;
            return Mathf.Clamp((int)(adjusted / perSlice), 0, count - 1);
        }

        private static void DrawFilledCircle(Vector2 center, float radius, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.EndGUI();
        }

        private static void DrawCircleOutline(Vector2 center, float radius, Color color, float thickness)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.forward, radius, thickness);
            Handles.EndGUI();
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawLine((Vector3)from, (Vector3)to, thickness);
            Handles.EndGUI();
        }

        private static void DrawCursor(Vector2 pos)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.07f, 0.55f, 0.58f, 0.6f);
            Handles.DrawSolidDisc(pos, Vector3.forward, 10f);
            Handles.color = ColBorder;
            Handles.DrawWireDisc(pos, Vector3.forward, 10f, 1.5f);
            Handles.color = ColActive;
            Handles.DrawSolidDisc(pos, Vector3.forward, 3f);
            Handles.EndGUI();
        }

        private static void DrawCenteredText(Vector2 pos, string text, Color color, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = color },
                wordWrap = false,
            };
            var content = new GUIContent(text);
            var size = style.CalcSize(content);
            var rect = new Rect(pos.x - size.x / 2, pos.y - size.y / 2, size.x, size.y);
            GUI.Label(rect, content, style);
        }
    }
}
