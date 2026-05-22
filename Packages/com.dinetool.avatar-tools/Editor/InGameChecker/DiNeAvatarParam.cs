using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DiNeTool.InGameChecker
{
    /// <summary>
    /// VRC Animator 파라미터 하나를 추적하고, 연결된 모든 AnimatorControllerPlayable에 동기화하는 클래스.
    /// </summary>
    public class DiNeAvatarParam
    {
        public string Name { get; }
        public AnimatorControllerParameterType Type { get; }

        private readonly int _hashId;
        private float _value;
        private readonly List<(AnimatorControllerPlayable playable, int index)> _playables = new();

        public DiNeAvatarParam(string name, AnimatorControllerParameterType type)
        {
            Name = name;
            Type = type;
            _hashId = Animator.StringToHash(name);
        }

        public void Subscribe(AnimatorControllerPlayable playable, int index)
        {
            _playables.Add((playable, index));
        }

        public float FloatValue()
        {
            foreach (var (playable, _) in _playables)
            {
                if (!playable.IsValid()) continue;
                return Type switch
                {
                    AnimatorControllerParameterType.Float => playable.GetFloat(_hashId),
                    AnimatorControllerParameterType.Int   => playable.GetInteger(_hashId),
                    AnimatorControllerParameterType.Bool  => playable.GetBool(_hashId) ? 1f : 0f,
                    _ => _value
                };
            }
            return _value;
        }

        public int IntValue() => (int)FloatValue();
        public bool BoolValue() => FloatValue() != 0f;

        public void Set(float value)
        {
            _value = value;
            foreach (var (playable, _) in _playables)
            {
                if (!playable.IsValid()) continue;
                switch (Type)
                {
                    case AnimatorControllerParameterType.Float:
                        playable.SetFloat(_hashId, value);
                        break;
                    case AnimatorControllerParameterType.Int:
                        playable.SetInteger(_hashId, Mathf.RoundToInt(value));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        playable.SetBool(_hashId, value != 0f);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        if (value != 0f) playable.SetTrigger(_hashId);
                        break;
                }
            }
        }
    }
}
