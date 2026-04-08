using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace DiNeTool.InGameChecker
{
    /// <summary>
    /// VRChat 아바타의 애니메이션 레이어를 PlayableGraph로 에뮬레이션하고,
    /// 제스처·파라미터를 제어하는 독립 모듈.
    /// </summary>
    public class DiNeAvatarModule : IDisposable
    {
        // ─── Public State ────────────────────────────────────────────────────
        public GameObject Avatar { get; }
        public Animator AvatarAnimator { get; }
        public VRCAvatarDescriptor Descriptor { get; }
        public string Name => Avatar != null ? Avatar.name : "—";
        public bool Active { get; private set; }

        public int Left { get; private set; }
        public int Right { get; private set; }

        public Dictionary<string, DiNeAvatarParam> Params { get; } = new();

        // ─── Private ─────────────────────────────────────────────────────────
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _mixer;
        private bool _hooked;

        // VRC 레이어 정렬 순서
        private static readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, int> LayerSortOrder = new()
        {
            { VRCAvatarDescriptor.AnimLayerType.Base,     0 },
            { VRCAvatarDescriptor.AnimLayerType.Additive, 1 },
            { VRCAvatarDescriptor.AnimLayerType.Sitting,  2 },
            { VRCAvatarDescriptor.AnimLayerType.TPose,    3 },
            { VRCAvatarDescriptor.AnimLayerType.IKPose,   4 },
            { VRCAvatarDescriptor.AnimLayerType.Gesture,  5 },
            { VRCAvatarDescriptor.AnimLayerType.Action,   6 },
            { VRCAvatarDescriptor.AnimLayerType.FX,       7 },
        };

        // VRC SDK 샘플 컨트롤러 매핑 (isDefault일 때 사용)
        private static readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, string> DefaultControllerGuids = new()
        {
            { VRCAvatarDescriptor.AnimLayerType.Base,     "4e4e15e8aab8c9f4ab86f71bf8cbb2dc" }, // vrc_AvatarV3LocomotionLayer
            { VRCAvatarDescriptor.AnimLayerType.Additive, "573a1373059632b4d820876efe2d1f58" }, // vrc_AvatarV3IdleLayer
            { VRCAvatarDescriptor.AnimLayerType.Gesture,  "404d228aeae421f4590305bc4cdaba16" }, // vrc_AvatarV3HandsLayer
            { VRCAvatarDescriptor.AnimLayerType.Action,   "3e479eeb9db24704a828bffb15406397" }, // vrc_AvatarV3ActionLayer
            { VRCAvatarDescriptor.AnimLayerType.FX,       "" },                                  // FX는 기본 없음
            { VRCAvatarDescriptor.AnimLayerType.Sitting,  "1268460c14f873240981bf15aa88b21a" }, // vrc_AvatarV3SittingLayer
            { VRCAvatarDescriptor.AnimLayerType.TPose,    "634a15e2e8d084c478a3b944ce120ac3" }, // vrc_AvatarV3UtilityTPose
            { VRCAvatarDescriptor.AnimLayerType.IKPose,   "d60c0b20b1e4aa840a3e8ee2589ce56d" }, // vrc_AvatarV3UtilityIKPose
        };

        // ─── Construction ────────────────────────────────────────────────────
        public DiNeAvatarModule(VRCAvatarDescriptor descriptor)
        {
            Descriptor     = descriptor;
            Avatar         = descriptor.gameObject;
            AvatarAnimator = Avatar.GetComponent<Animator>();
        }

        // ─── Validation ──────────────────────────────────────────────────────
        public bool IsValid(out string error)
        {
            if (Avatar == null)           { error = "Avatar가 삭제되었습니다";          return false; }
            if (!Avatar.activeInHierarchy){ error = "Avatar가 비활성 상태입니다";       return false; }
            if (AvatarAnimator == null)   { error = "Animator 컴포넌트가 없습니다";     return false; }
            if (Descriptor == null)       { error = "VRCAvatarDescriptor가 없습니다";   return false; }
            error = null;
            return true;
        }

        // ─── Connect / Disconnect ────────────────────────────────────────────
        public void Connect()
        {
            if (Active) return;
            Active = true;
            InitForAvatar();
        }

        public void Disconnect()
        {
            if (!Active) return;
            StopVrcHooks();
            DestroyGraph();
            Params.Clear();
            Active = false;
        }

        public void Dispose() => Disconnect();

        // ─── Gesture Control ─────────────────────────────────────────────────
        public void SetLeftGesture(int index)
        {
            Left = Mathf.Clamp(index, 0, 7);
            if (Params.TryGetValue("GestureLeft", out var p)) p.Set(Left);
            if (Params.TryGetValue("GestureLeftWeight", out var w)) w.Set(Left != 0 ? 1f : 0f);
        }

        public void SetRightGesture(int index)
        {
            Right = Mathf.Clamp(index, 0, 7);
            if (Params.TryGetValue("GestureRight", out var p)) p.Set(Right);
            if (Params.TryGetValue("GestureRightWeight", out var w)) w.Set(Right != 0 ? 1f : 0f);
        }

        // ─── Update (매 프레임 호출) ─────────────────────────────────────────
        public void OnUpdate()
        {
            if (!Active || !_graph.IsValid()) return;
            // 그래프가 자동 평가 — 특별한 업데이트 없음
        }

        // ═════════════════════════════════════════════════════════════════════
        // Internal — PlayableGraph 초기화
        // ═════════════════════════════════════════════════════════════════════
        private void InitForAvatar()
        {
            // Animator 설정
            AvatarAnimator.applyRootMotion = false;
            AvatarAnimator.runtimeAnimatorController = null;
            AvatarAnimator.updateMode   = AnimatorUpdateMode.Normal;
            AvatarAnimator.cullingMode  = AnimatorCullingMode.AlwaysAnimate;

            // 레이어 수집 및 정렬
            var layers = CollectLayers();
            if (layers.Count == 0) return;

            // PlayableGraph 생성
            _graph = PlayableGraph.Create("DiNe_InGameChecker");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _mixer = AnimationLayerMixerPlayable.Create(_graph, layers.Count);
            var output = AnimationPlayableOutput.Create(_graph, "Avatar", AvatarAnimator);
            output.SetSourcePlayable(_mixer);

            // 각 레이어를 PlayableGraph에 연결
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var controller = layer.isDefault
                    ? LoadDefaultController(layer.type)
                    : layer.animatorController as AnimatorController;

                if (controller == null) continue;

                var playable = AnimatorControllerPlayable.Create(_graph, controller);
                _graph.Connect(playable, 0, _mixer, i);
                _mixer.SetInputWeight(i, 1f);

                // Additive 레이어 설정
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.Additive)
                    _mixer.SetLayerAdditive((uint)i, true);

                // 마스크 적용
                if (layer.mask != null)
                    _mixer.SetLayerMaskFromAvatarMask((uint)i, layer.mask);

                // 파라미터 수집
                CollectParams(controller, playable);
            }

            // VRC 기본 파라미터 추가
            AddDefaultVrcParams();

            // Expression Parameters 기본값 적용
            ApplyExpressionDefaults();

            // VRC SDK 후킹
            StartVrcHooks();

            // 초기 제스처 값 설정
            SetLeftGesture(0);
            SetRightGesture(0);

            // 기본 상태 파라미터 설정
            SetParamIfExists("IsLocal", 1f);
            SetParamIfExists("Grounded", 1f);
            SetParamIfExists("Upright", 1f);
            SetParamIfExists("TrackingType", 3f);
            SetParamIfExists("AvatarVersion", 3f);
            SetParamIfExists("IsAnimatorEnabled", 1f);
            SetParamIfExists("ScaleFactor", 1f);
            SetParamIfExists("ScaleFactorInverse", 1f);

            _graph.Play();
        }

        private List<VRCAvatarDescriptor.CustomAnimLayer> CollectLayers()
        {
            var all = new List<VRCAvatarDescriptor.CustomAnimLayer>();
            if (Descriptor.baseAnimationLayers != null)
                all.AddRange(Descriptor.baseAnimationLayers);
            if (Descriptor.specialAnimationLayers != null)
                all.AddRange(Descriptor.specialAnimationLayers);
            all.Sort((a, b) => GetSortOrder(a.type) - GetSortOrder(b.type));
            return all;
        }

        private static int GetSortOrder(VRCAvatarDescriptor.AnimLayerType type) =>
            LayerSortOrder.TryGetValue(type, out int v) ? v : 99;

        private static AnimatorController LoadDefaultController(VRCAvatarDescriptor.AnimLayerType type)
        {
            if (!DefaultControllerGuids.TryGetValue(type, out string guid) || string.IsNullOrEmpty(guid))
                return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        private void CollectParams(AnimatorController controller, AnimatorControllerPlayable playable)
        {
            if (controller.parameters == null) return;
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                var p = controller.parameters[i];
                if (!Params.TryGetValue(p.name, out var existing))
                {
                    existing = new DiNeAvatarParam(p.name, p.type);
                    Params[p.name] = existing;
                }
                existing.Subscribe(playable, i);
            }
        }

        private void AddDefaultVrcParams()
        {
            // VRC 기본 시스템 파라미터 — 없으면 추가
            var defaults = new (string name, AnimatorControllerParameterType type)[]
            {
                ("GestureLeft",          AnimatorControllerParameterType.Int),
                ("GestureRight",         AnimatorControllerParameterType.Int),
                ("GestureLeftWeight",    AnimatorControllerParameterType.Float),
                ("GestureRightWeight",   AnimatorControllerParameterType.Float),
                ("VRMode",               AnimatorControllerParameterType.Int),
                ("Viseme",               AnimatorControllerParameterType.Int),
                ("Voice",                AnimatorControllerParameterType.Float),
                ("Upright",              AnimatorControllerParameterType.Float),
                ("AngularY",             AnimatorControllerParameterType.Float),
                ("VelocityX",            AnimatorControllerParameterType.Float),
                ("VelocityY",            AnimatorControllerParameterType.Float),
                ("VelocityZ",            AnimatorControllerParameterType.Float),
                ("VelocityMagnitude",    AnimatorControllerParameterType.Float),
                ("Grounded",             AnimatorControllerParameterType.Bool),
                ("Seated",               AnimatorControllerParameterType.Bool),
                ("AFK",                  AnimatorControllerParameterType.Bool),
                ("IsLocal",              AnimatorControllerParameterType.Bool),
                ("IsOnFriendsList",      AnimatorControllerParameterType.Bool),
                ("InStation",            AnimatorControllerParameterType.Bool),
                ("MuteSelf",             AnimatorControllerParameterType.Bool),
                ("TrackingType",         AnimatorControllerParameterType.Int),
                ("AvatarVersion",        AnimatorControllerParameterType.Int),
                ("IsAnimatorEnabled",    AnimatorControllerParameterType.Bool),
                ("ScaleFactor",          AnimatorControllerParameterType.Float),
                ("ScaleFactorInverse",   AnimatorControllerParameterType.Float),
                ("EyeHeightAsMeters",    AnimatorControllerParameterType.Float),
                ("EyeHeightAsPercent",   AnimatorControllerParameterType.Float),
            };

            foreach (var (name, type) in defaults)
            {
                if (!Params.ContainsKey(name))
                    Params[name] = new DiNeAvatarParam(name, type);
            }
        }

        private void ApplyExpressionDefaults()
        {
            var exprParams = Descriptor.expressionParameters;
            if (exprParams?.parameters == null) return;
            foreach (var ep in exprParams.parameters)
            {
                if (string.IsNullOrEmpty(ep.name)) continue;
                if (Params.TryGetValue(ep.name, out var p))
                    p.Set(ep.defaultValue);
            }
        }

        private void SetParamIfExists(string name, float value)
        {
            if (Params.TryGetValue(name, out var p)) p.Set(value);
        }

        // ═════════════════════════════════════════════════════════════════════
        // VRC SDK Hooks
        // ═════════════════════════════════════════════════════════════════════
        private readonly HashSet<Animator> _avatarAnimators = new();

        private void StartVrcHooks()
        {
            if (_hooked) return;
            _hooked = true;

            // 아바타 내 모든 Animator 수집 (ParameterDriver 훅 필터용)
            foreach (var anim in Avatar.GetComponentsInChildren<Animator>(true))
                _avatarAnimators.Add(anim);

            VRC_AvatarParameterDriver.OnApplySettings += OnParameterDriverApply;
        }

        private void StopVrcHooks()
        {
            if (!_hooked) return;
            _hooked = false;

            VRC_AvatarParameterDriver.OnApplySettings -= OnParameterDriverApply;

            _avatarAnimators.Clear();
        }

        private void OnParameterDriverApply(VRC_AvatarParameterDriver driver, Animator animator)
        {
            if (!_avatarAnimators.Contains(animator)) return;
            foreach (var entry in driver.parameters)
            {
                if (!Params.TryGetValue(entry.name, out var param)) continue;
                switch (entry.type)
                {
                    case VRC_AvatarParameterDriver.ChangeType.Set:
                        param.Set(entry.value);
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Add:
                        param.Set(param.FloatValue() + entry.value);
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Random:
                        float rnd = param.Type == AnimatorControllerParameterType.Bool
                            ? (UnityEngine.Random.value <= entry.chance ? 1f : 0f)
                            : UnityEngine.Random.Range(entry.valueMin, entry.valueMax);
                        param.Set(rnd);
                        break;
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Cleanup
        // ═════════════════════════════════════════════════════════════════════
        private void DestroyGraph()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
