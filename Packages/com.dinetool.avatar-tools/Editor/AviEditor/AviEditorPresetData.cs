using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArmatureScalerPresetData : ScriptableObject
{
    // 스케일 값
    [SerializeField]
    public SerializableVector3Dictionary scales = new SerializableVector3Dictionary();
    
    // 로테이션 값
    [SerializeField]
    public SerializableQuaternionDictionary rotations = new SerializableQuaternionDictionary();

    // 추가: 포지션 값 딕셔너리
    [SerializeField]
    public SerializableVector3Dictionary positions = new SerializableVector3Dictionary();

    [System.Serializable]
    public class SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(Vector3 vec)
        {
            x = vec.x;
            y = vec.y;
            z = vec.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }
    
    [System.Serializable]
    public class SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(Quaternion quat)
        {
            x = quat.x;
            y = quat.y;
            z = quat.z;
            w = quat.w;
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    [System.Serializable]
    public class SerializableVector3Dictionary : ISerializationCallbackReceiver
    {
        [SerializeField]
        public List<string> keys = new List<string>();
        [SerializeField]
        public List<SerializableVector3> values = new List<SerializableVector3>();

        public Dictionary<string, SerializableVector3> dictionary = new Dictionary<string, SerializableVector3>();

        public Dictionary<string, SerializableVector3> ToDictionary()
        {
            return dictionary;
        }

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in dictionary)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            dictionary.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!dictionary.ContainsKey(keys[i]))
                {
                    dictionary.Add(keys[i], values[i]);
                }
            }
        }

        public SerializableVector3 this[string key]
        {
            get => dictionary[key];
            set => dictionary[key] = value;
        }

        public bool TryGetValue(string key, out SerializableVector3 value)
        {
            return dictionary.TryGetValue(key, out value);
        }
    }

    [System.Serializable]
    public class SerializableQuaternionDictionary : ISerializationCallbackReceiver
    {
        [SerializeField]
        public List<string> keys = new List<string>();
        [SerializeField]
        public List<SerializableQuaternion> values = new List<SerializableQuaternion>();

        public Dictionary<string, SerializableQuaternion> dictionary = new Dictionary<string, SerializableQuaternion>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in dictionary)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            dictionary.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!dictionary.ContainsKey(keys[i]))
                {
                    dictionary.Add(keys[i], values[i]);
                }
            }
        }

        public SerializableQuaternion this[string key]
        {
            get => dictionary[key];
            set => dictionary[key] = value;
        }

        public bool TryGetValue(string key, out SerializableQuaternion value)
        {
            return dictionary.TryGetValue(key, out value);
        }
    }
}